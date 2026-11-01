using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Regras de raid:
    /// 1) Horário: base (barricada/estrutura de outro jogador) só toma dano de jogador dentro do horário de raid.
    /// 2) Proteção offline (igual Zumbas): base com Claim Flag fica protegida quando o dono e o grupo estão
    ///    offline há 10+ min. A Claim Flag precisa ser "renovada" a cada 3 dias: basta o dono ou alguém do grupo
    ///    passar perto dela. Senão a proteção cai.
    /// 3) Cada tentativa bloqueada ou raid de verdade vai pro raid_log.txt (o bot do Discord pode mandar alerta).</summary>
    public partial class Plugin
    {
        static readonly TimeSpan Fuso = TimeSpan.FromHours(-3); // horário de Brasília
        public static DateTime AgoraBrasil => DateTime.UtcNow + Fuso;

        readonly Dictionary<ulong, float> ultimoOnline = new Dictionary<ulong, float>(); // dono ou grupo -> Time.realtimeSinceStartup
        readonly Dictionary<ulong, DateTime> claimRenovada = new Dictionary<ulong, DateTime>(); // instanceID -> quando
        readonly Dictionary<ulong, float> avisoRaid = new Dictionary<ulong, float>();
        readonly Dictionary<ulong, float> alertaDono = new Dictionary<ulong, float>();
        List<(Vector3 pos, ulong dono, ulong grupo, uint id)> claims = new List<(Vector3, ulong, ulong, uint)>();
        float proximoTickRaid, proximaVarreduraClaims;
        bool? raidAbertoAntes;
        int ultimoAvisoMin = -1;
        string ArqClaims => Path.Combine(Directory, "claims_renovadas.tsv");
        string ArqRaidLog => Path.Combine(Directory, "raid_log.txt");

        // ---------- horário ----------
        public bool RaidAberto(out string quando)
        {
            quando = TextoHorarioRaid();
            if (!S.RaidHorarioLigado) return true;
            var agora = AgoraBrasil;
            if (!DiaDeRaid(agora.DayOfWeek)) return false;
            int min = agora.Hour * 60 + agora.Minute;
            int ini = S.RaidInicioHora * 60, fim = S.RaidFimHora * 60;
            return ini <= fim ? (min >= ini && min < fim) : (min >= ini || min < fim);
        }

        bool DiaDeRaid(DayOfWeek d)
        {
            var dias = (S.RaidDias ?? "todos").ToLowerInvariant();
            if (dias.Contains("todos")) return true;
            string[] nomes = { "dom", "seg", "ter", "qua", "qui", "sex", "sab" };
            return dias.Contains(nomes[(int)d]);
        }

        public string TextoHorarioRaid()
        {
            if (!S.RaidHorarioLigado) return "Raid liberado o dia todo.";
            var dias = (S.RaidDias ?? "todos").ToLowerInvariant().Contains("todos") ? "todo dia" : S.RaidDias;
            return $"Raid de base liberado {dias}, das {S.RaidInicioHora}h as {S.RaidFimHora}h (horario de Brasilia).";
        }

        // ---------- proteção offline ----------
        void CarregarClaims()
        {
            try
            {
                if (!File.Exists(ArqClaims)) return;
                foreach (var l in File.ReadAllLines(ArqClaims))
                {
                    var c = l.Split('\t');
                    if (c.Length > 1 && ulong.TryParse(c[0], out var id) && long.TryParse(c[1], out var t))
                        claimRenovada[id] = new DateTime(t, DateTimeKind.Utc);
                }
            }
            catch { }
        }

        void SalvarClaims()
        {
            try { File.WriteAllLines(ArqClaims, claimRenovada.Select(k => k.Key + "\t" + k.Value.Ticks)); } catch { }
        }

        void VarrerClaims()
        {
            var lista = new List<(Vector3, ulong, ulong, uint)>();
            foreach (var reg in BarricadeManager.regions)
                foreach (var d in reg.drops)
                {
                    if (!(d.asset is ItemBarricadeAsset a) || a.build != EBuild.CLAIM) continue;
                    var sd = d.GetServersideData();
                    lista.Add((d.model.position, sd.owner, sd.group, d.instanceID));
                    if (!claimRenovada.ContainsKey(d.instanceID)) claimRenovada[d.instanceID] = DateTime.UtcNow; // flag nova ou de antes do sistema
                }
            claims = lista;
        }

        bool Online(ulong dono, ulong grupo)
        {
            foreach (var c in Provider.clients)
            {
                if (c.playerID.steamID.m_SteamID == dono) return true;
                if (grupo != 0 && c.player != null && c.player.quests.groupID.m_SteamID == grupo) return true;
            }
            return false;
        }

        float MinutosOffline(ulong dono, ulong grupo)
        {
            if (Online(dono, grupo)) return 0;
            float ult = Mathf.Max(ultimoOnline.TryGetValue(dono, out var a) ? a : 0, grupo != 0 && ultimoOnline.TryGetValue(grupo, out var b) ? b : 0);
            return (Time.realtimeSinceStartup - ult) / 60f;
        }

        /// <summary>Claim Flag do dono/grupo cobrindo esse ponto e ainda válida (renovada nos últimos dias).</summary>
        bool TemClaimValida(Vector3 pos, ulong dono, ulong grupo)
        {
            float r2 = S.RaidRaioClaim * S.RaidRaioClaim;
            foreach (var c in claims)
            {
                if ((c.pos - pos).sqrMagnitude > r2) continue;
                if (!(c.dono == dono || (grupo != 0 && c.grupo == grupo))) continue;
                if (claimRenovada.TryGetValue(c.id, out var quando) && (DateTime.UtcNow - quando).TotalDays <= S.RaidDiasRenovarClaim) return true;
            }
            return false;
        }

        // ---------- dano ----------
        /// <summary>Devolve true se o dano deve ser bloqueado.</summary>
        bool BloquearRaid(CSteamID quem, Vector3 pos, ulong dono, ulong grupo)
        {
            if (quem == CSteamID.Nil || dono == 0) return false;
            var sp = PlayerTool.getSteamPlayer(quem);
            if (sp?.player == null) return false;
            if (quem.m_SteamID == dono) return false;
            var g = sp.player.quests.groupID.m_SteamID;
            if (g != 0 && g == grupo) return false;
            if (sp.isAdmin && S.RaidAdminIgnora) return false;

            string motivo = null;
            if (!RaidAberto(out var quando)) motivo = quando;
            else if (S.RaidProtecaoOffline && MinutosOffline(dono, grupo) >= S.RaidMinutosOffline && TemClaimValida(pos, dono, grupo))
                motivo = "Essa base esta protegida: o dono esta offline (protecao offline com Claim Flag).";

            if (motivo != null)
            {
                if (!avisoRaid.TryGetValue(quem.m_SteamID, out var t) || Time.realtimeSinceStartup > t)
                {
                    avisoRaid[quem.m_SteamID] = Time.realtimeSinceStartup + 6f;
                    sp.player.ServerShowHint("<color=#FF4040><b>RAID BLOQUEADO</b></color>\n" + motivo, 5f);
                }
                return true;
            }

            // raid valendo: avisa o dono (se online) e registra
            ulong chave = grupo != 0 ? grupo : dono;
            if (!alertaDono.TryGetValue(chave, out var prox) || Time.realtimeSinceStartup > prox)
            {
                alertaDono[chave] = Time.realtimeSinceStartup + 120f;
                foreach (var c in Provider.clients)
                    if (c.player != null && (c.playerID.steamID.m_SteamID == dono || (grupo != 0 && c.player.quests.groupID.m_SteamID == grupo)))
                        c.player.ServerShowHint($"<color=#FF4040><b>SUA BASE ESTA SENDO ATACADA!</b></color>\nPerto de {pos.x:0} {pos.z:0}", 8f);
                try
                {
                    File.AppendAllText(ArqRaidLog, $"{AgoraBrasil:dd/MM HH:mm} RAID: {sp.playerID.characterName} ({quem.m_SteamID}) atacando base de {NomeJogador(dono)} ({dono}) em {pos.x:0} {pos.z:0}{Environment.NewLine}");
                }
                catch { }
            }
            return false;
        }

        string NomeJogador(ulong id)
        {
            var sp = PlayerTool.getSteamPlayer(id);
            if (sp != null) return sp.playerID.characterName;
            return Stats.Todas.FirstOrDefault(x => x.Id == id)?.Nome ?? id.ToString();
        }

        public void DanoBarricadaRaid(CSteamID quem, Transform t, ref bool permitir)
        {
            if (!permitir || t == null) return;
            var d = BarricadeManager.FindBarricadeByRootTransform(t);
            if (d == null) return;
            var sd = d.GetServersideData();
            if (BloquearRaid(quem, t.position, sd.owner, sd.group)) permitir = false;
        }

        public void DanoEstruturaRaid(CSteamID quem, Transform t, ref bool permitir)
        {
            if (!permitir || t == null) return;
            var d = StructureManager.FindStructureByRootTransform(t);
            if (d == null) return;
            var sd = d.GetServersideData();
            if (BloquearRaid(quem, t.position, sd.owner, sd.group)) permitir = false;
        }

        // ---------- tick ----------
        void TickRaid()
        {
            if (Time.realtimeSinceStartup < proximoTickRaid) return;
            proximoTickRaid = Time.realtimeSinceStartup + 5f;
            float agora = Time.realtimeSinceStartup;
            foreach (var c in Provider.clients)
            {
                if (c.player == null) continue;
                ultimoOnline[c.playerID.steamID.m_SteamID] = agora;
                var g = c.player.quests.groupID.m_SteamID;
                if (g != 0) ultimoOnline[g] = agora;
            }
            if (agora >= proximaVarreduraClaims)
            {
                proximaVarreduraClaims = agora + 60f;
                VarrerClaims();
                // renova as Claim Flags que o dono/grupo visitou
                bool mudou = false;
                foreach (var cl in claims)
                    foreach (var c in Provider.clients)
                    {
                        if (c.player == null) continue;
                        var g = c.player.quests.groupID.m_SteamID;
                        if (!(c.playerID.steamID.m_SteamID == cl.dono || (g != 0 && g == cl.grupo))) continue;
                        if ((c.player.transform.position - cl.pos).sqrMagnitude > S.RaidRaioClaim * S.RaidRaioClaim) continue;
                        claimRenovada[cl.id] = DateTime.UtcNow; mudou = true; break;
                    }
                if (mudou) SalvarClaims();
            }

            // anúncios: 10 min antes, abriu, fechou
            if (!S.RaidHorarioLigado) return;
            bool aberto = RaidAberto(out _);
            if (raidAbertoAntes.HasValue && aberto != raidAbertoAntes.Value)
            {
                if (aberto) Chat.Todos(Chat.Cor(S.CorPerigo, "RAID LIBERADO! ") + Chat.Cor(S.CorApagada, $"Bases podem ser atacadas ate as {S.RaidFimHora}h."));
                else Chat.Todos(Chat.Cor(S.CorOk, "RAID ENCERRADO. ") + Chat.Cor(S.CorApagada, $"Bases protegidas ate as {S.RaidInicioHora}h."));
                foreach (var c in Provider.clients)
                    c.player?.ServerShowHint(aberto ? $"<color=#FF4040><b>RAID LIBERADO</b></color>\nAte as {S.RaidFimHora}h" : "<color=#55E07A><b>RAID ENCERRADO</b></color>\nBases protegidas", 6f);
            }
            raidAbertoAntes = aberto;
            if (!aberto)
            {
                var b = AgoraBrasil;
                int falta = (S.RaidInicioHora * 60 - (b.Hour * 60 + b.Minute) + 1440) % 1440;
                if (falta == 10 && DiaDeRaid(b.DayOfWeek) && ultimoAvisoMin != b.Minute)
                {
                    ultimoAvisoMin = b.Minute;
                    Chat.Todos(Chat.Cor(S.CorDestaque, "Raid de base libera em 10 minutos! ") + Chat.Cor(S.CorApagada, "Proteja sua base."));
                }
            }
        }

        void AvisarRaidAoEntrar(UnturnedPlayer p)
        {
            Chat.Para(p, Chat.Cor(S.CorDestaque, "RAID: ") + Chat.Cor(S.CorApagada, TextoHorarioRaid() +
                (S.RaidProtecaoOffline ? $" Base com Claim Flag fica protegida com o grupo offline ha {S.RaidMinutosOffline} min (passe na base a cada {S.RaidDiasRenovarClaim} dias)." : "")));
        }
    }

    public class CmdRaid : ComandoSistema
    {
        public override string Name => "raid";
        public override string Help => "Mostra o horario de raid. Dono: /raid <inicio> <fim> [dias] ou /raid desligar|ligar";
        public override string Syntax => "[inicio fim [todos|sex,sab,dom]] | ligar | desligar";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (a.Length >= 1 && (P.EhDono(p) || p.IsAdmin))
            {
                if (a[0] == "desligar" || a[0] == "ligar")
                {
                    S.RaidHorarioLigado = a[0] == "ligar"; P.Configuration.Save();
                    Chat.Ok(p, S.RaidHorarioLigado ? "Horario de raid ligado. " + P.TextoHorarioRaid() : "Horario de raid desligado: raid liberado o dia todo.");
                    return;
                }
                if (a.Length >= 2 && int.TryParse(a[0], out var ini) && int.TryParse(a[1], out var fim) && ini >= 0 && ini < 24 && fim >= 0 && fim <= 24)
                {
                    S.RaidInicioHora = ini; S.RaidFimHora = fim % 24;
                    if (a.Length >= 3) S.RaidDias = a[2].ToLowerInvariant();
                    S.RaidHorarioLigado = true; P.Configuration.Save();
                    Chat.Ok(p, "Salvo. " + P.TextoHorarioRaid());
                    return;
                }
            }
            bool aberto = P.RaidAberto(out var quando);
            Chat.Para(p, Cor(aberto ? S.CorPerigo : S.CorOk, aberto ? "RAID LIBERADO AGORA. " : "Raid fechado agora. ") + Cor(S.CorApagada, quando));
            if (S.RaidProtecaoOffline)
                Chat.Para(p, Cor(S.CorApagada, $"Protecao offline: base com Claim Flag fica protegida quando todo o grupo esta offline ha {S.RaidMinutosOffline} min. Passe perto da Claim Flag a cada {S.RaidDiasRenovarClaim} dias pra manter."));
        }
    }
}
