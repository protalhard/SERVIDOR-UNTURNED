using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Rocket.Unturned.Events;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Arena PvP 1x1 com aposta (pedido do dono). Os dois têm que estar dentro da safe pra desafiar e aceitar.
    /// Aceitou: a aposta sai do banco dos dois, eles vão pra arena (pontos marcados pelo dono com /arena marcar 1 e 2),
    /// a luta começa em 10 s e quem matar leva tudo. No fim, quem ganhou volta pra onde estava na safe;
    /// quem perdeu renasce normal (na safe). Ninguém de fora acerta os dois durante a luta.</summary>
    public partial class Plugin
    {
        class Desafio { public ulong De, Para; public long Valor; public float Expira; }
        class Luta
        {
            public ulong A, B; public long Valor; public Vector3 VoltaA, VoltaB;
            public float Comeca, Fim; public bool Acabou; public float VoltarEm; public ulong Vencedor;
        }
        public class FichaArena { public string Nome; public int Vitorias, Derrotas; public long Ganho; }

        readonly Dictionary<ulong, Desafio> desafios = new Dictionary<ulong, Desafio>(); // chave = quem foi desafiado
        Luta luta;
        Dictionary<ulong, FichaArena> rankArena;
        string ArqArena => Path.Combine(Directory, "arena.json");

        Dictionary<ulong, FichaArena> RankArena()
        {
            if (rankArena != null) return rankArena;
            try { rankArena = File.Exists(ArqArena) ? JsonConvert.DeserializeObject<Dictionary<ulong, FichaArena>>(File.ReadAllText(ArqArena)) : null; }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
            return rankArena = rankArena ?? new Dictionary<ulong, FichaArena>();
        }

        void SalvarArena()
        {
            try
            {
                var tmp = ArqArena + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(RankArena(), Formatting.Indented));
                if (File.Exists(ArqArena)) File.Delete(ArqArena);
                File.Move(tmp, ArqArena);
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }

        bool ArenaPronta => Configuration.Instance.ArenaA != null && Configuration.Instance.ArenaA.Definido
            && Configuration.Instance.ArenaB != null && Configuration.Instance.ArenaB.Definido;

        bool NaLuta(ulong id) => luta != null && (luta.A == id || luta.B == id);

        void LigarArena()
        {
            UnturnedPlayerEvents.OnPlayerDeath += AoMorrerArena;
            DamageTool.damagePlayerRequested += AoDanoArena;
        }

        void DesligarArena()
        {
            UnturnedPlayerEvents.OnPlayerDeath -= AoMorrerArena;
            DamageTool.damagePlayerRequested -= AoDanoArena;
            if (luta != null && !luta.Acabou) Empate("o servidor esta reiniciando");
        }

        // ---------- comandos ----------

        internal void ArenaDesafiar(UnturnedPlayer p, string nome, string valorTxt)
        {
            if (!ArenaPronta) { Chat.Erro(p, "A arena ainda nao foi marcada pelo dono."); return; }
            if (ZonaSeguraEm(p.Position) == null) { Chat.Erro(p, "Voce tem que estar dentro da safe pra desafiar."); return; }
            if (!long.TryParse((valorTxt ?? "").Replace(".", "").Replace("$", ""), out var valor) || valor < S.ArenaApostaMin)
            { Chat.Erro(p, $"Aposta minima: ${S.ArenaApostaMin:N0}. Use: /arena desafiar nome valor"); return; }
            if (S.ArenaApostaMax > 0 && valor > S.ArenaApostaMax) { Chat.Erro(p, $"Aposta maxima: ${S.ArenaApostaMax:N0}."); return; }
            var alvo = UnturnedPlayer.FromName(nome);
            if (alvo == null || alvo.CSteamID == p.CSteamID) { Chat.Erro(p, "Jogador nao encontrado."); return; }
            if (ZonaSeguraEm(alvo.Position) == null) { Chat.Erro(p, Chat.Limpar(alvo.CharacterName) + " tem que estar dentro da safe."); return; }
            if (Banco.Saldo(Id(p)) < valor) { Chat.Erro(p, $"Voce nao tem ${valor:N0} no banco."); return; }
            if (luta != null) { Chat.Erro(p, "Ja tem uma luta na arena. Espere acabar."); return; }
            desafios[Id(alvo)] = new Desafio { De = Id(p), Para = Id(alvo), Valor = valor, Expira = Time.realtimeSinceStartup + 60f };
            Chat.Info(p, $"Desafio mandado pra {Chat.Limpar(alvo.CharacterName)} valendo ${valor:N0}. Ele tem 60 s pra aceitar.");
            Chat.Para(alvo, Chat.Cor(S.CorDestaque, Chat.Forte("ARENA: ")) + Chat.Cor("#FFFFFF", $"{Chat.Limpar(p.CharacterName)} te desafiou pra um 1x1 valendo ")
                + Chat.Cor(S.CorDestaque, Chat.Forte($"${valor:N0}")) + Chat.Cor("#FFFFFF", ". Digite ") + Chat.Cor(S.CorDestaque, "/arena aceitar")
                + Chat.Cor("#FFFFFF", " ou ") + Chat.Cor(S.CorApagada, "/arena recusar") + Chat.Cor("#FFFFFF", " (60 s)."));
        }

        internal void ArenaResponder(UnturnedPlayer p, bool aceitar)
        {
            var id = Id(p);
            if (!desafios.TryGetValue(id, out var d) || Time.realtimeSinceStartup > d.Expira) { desafios.Remove(id); Chat.Erro(p, "Voce nao tem desafio pendente."); return; }
            desafios.Remove(id);
            var desafiante = PlayerTool.getSteamPlayer(d.De);
            var up = desafiante != null ? UnturnedPlayer.FromSteamPlayer(desafiante) : null;
            if (!aceitar)
            {
                Chat.Info(p, "Desafio recusado.");
                if (up != null) Chat.Info(up, Chat.Limpar(p.CharacterName) + " recusou o desafio.");
                return;
            }
            if (up == null || up.Dead) { Chat.Erro(p, "Quem te desafiou nao esta mais online."); return; }
            if (luta != null) { Chat.Erro(p, "Ja tem uma luta na arena. Espere acabar."); return; }
            if (ZonaSeguraEm(p.Position) == null || ZonaSeguraEm(up.Position) == null) { Chat.Erro(p, "Os dois tem que estar dentro da safe."); if (up != null) Chat.Erro(up, "A luta nao comecou: os dois tem que estar dentro da safe."); return; }
            if (EmCombate(p, out _) || EmCombate(up, out _)) { Chat.Erro(p, "Alguem esta em combate. Tente de novo daqui a pouco."); return; }
            if (Banco.Saldo(id) < d.Valor) { Chat.Erro(p, $"Voce nao tem ${d.Valor:N0} no banco."); Chat.Erro(up, Chat.Limpar(p.CharacterName) + " nao tem saldo pra aposta."); return; }
            if (Banco.Saldo(d.De) < d.Valor) { Chat.Erro(p, Chat.Limpar(up.CharacterName) + " nao tem mais saldo pra aposta."); Chat.Erro(up, $"Voce nao tem ${d.Valor:N0} no banco."); return; }

            Banco.Somar(d.De, -d.Valor);
            Banco.Somar(id, -d.Valor);
            var cfg = Configuration.Instance;
            luta = new Luta
            {
                A = d.De, B = id, Valor = d.Valor, VoltaA = up.Position, VoltaB = p.Position,
                Comeca = Time.realtimeSinceStartup + 10f, Fim = Time.realtimeSinceStartup + 10f + Math.Max(1, S.ArenaMinutos) * 60f
            };
            if (FecharTudoArena(up)) { }
            FecharTudoArena(p);
            up.Player.teleportToLocationUnsafe(new Vector3(cfg.ArenaA.X, cfg.ArenaA.Y + .3f, cfg.ArenaA.Z), cfg.ArenaA.Raio);
            p.Player.teleportToLocationUnsafe(new Vector3(cfg.ArenaB.X, cfg.ArenaB.Y + .3f, cfg.ArenaB.Z), cfg.ArenaB.Raio);
            Chat.Todos(Chat.Cor(S.CorDestaque, Chat.Forte("ARENA: ")) + Chat.Cor("#FFFFFF", $"{Chat.Limpar(up.CharacterName)} x {Chat.Limpar(p.CharacterName)} valendo ")
                + Chat.Cor(S.CorDestaque, Chat.Forte($"${d.Valor * 2:N0}")) + Chat.Cor("#FFFFFF", "! A luta comeca em 10 s."));
            Rocket.Core.Logging.Logger.Log($"[Arena] {up.CharacterName} ({d.De}) x {p.CharacterName} ({id}) valendo {d.Valor} cada");
        }

        bool FecharTudoArena(UnturnedPlayer p)
        {
            if (MenuAberto(p)) FecharMenu(p);
            FecharLoja(p); FecharAtm(p); FecharGaragem(p);
            teleportes.Remove(Id(p));
            if (p.Player.movement.getVehicle() != null) VehicleManager.forceRemovePlayer(p.CSteamID);
            return true;
        }

        internal void ArenaMarcar(UnturnedPlayer p, string qual)
        {
            if (!EhDono(p)) { Chat.Erro(p, "So o dono marca a arena."); return; }
            if (ZonaSeguraEm(p.Position) != null) { Chat.Erro(p, "A arena nao pode ficar dentro da safe (la ninguem leva dano)."); return; }
            var cfg = Configuration.Instance;
            var pt = new Ponto(qual == "2" ? "Arena B" : "Arena A", p.Player.look.yaw) { X = p.Position.x, Y = p.Position.y, Z = p.Position.z };
            if (qual == "2") cfg.ArenaB = pt; else cfg.ArenaA = pt;
            Configuration.Save();
            Chat.Info(p, $"{pt.Nome} marcada aqui (X {pt.X:0} Z {pt.Z:0}). {(ArenaPronta ? "Arena pronta!" : "Agora marque o outro lado com /arena marcar " + (qual == "2" ? "1" : "2") + ".")}");
        }

        internal void ArenaTop(UnturnedPlayer p)
        {
            var top = RankArena().OrderByDescending(x => x.Value.Vitorias).ThenByDescending(x => x.Value.Ganho).Take(5).ToList();
            if (top.Count == 0) { Chat.Info(p, "Ninguem lutou na arena ainda."); return; }
            Chat.Para(p, Chat.Cor(S.CorDestaque, Chat.Forte("TOP ARENA")));
            int i = 1;
            foreach (var t in top)
                Chat.Para(p, Chat.Cor("#FFFFFF", $"{i++}. {Chat.Limpar(t.Value.Nome)}: {t.Value.Vitorias}V {t.Value.Derrotas}D, ganhou ${t.Value.Ganho:N0}"));
        }

        // ---------- luta ----------

        void AoDanoArena(ref DamagePlayerParameters par, ref bool permitir)
        {
            if (luta == null || par.player == null) return;
            var vit = par.player.channel.owner.playerID.steamID.m_SteamID;
            var atk = par.killer.m_SteamID;
            bool vNa = NaLuta(vit), aNa = atk != 0 && NaLuta(atk);
            if (!vNa && !aNa) return;
            if (vNa && Time.realtimeSinceStartup < luta.Comeca) { permitir = false; return; } // contagem
            if (luta.Acabou && vNa) { permitir = false; return; }                            // vencedor esperando voltar
            if (atk != 0 && atk != vit && vNa != aNa) { permitir = false; return; }          // ninguém de fora se mete
            if (vNa && aNa && atk != vit && !luta.Acabou) permitir = true;                   // os dois lutando: vale, mesmo com a proteção de saída da safe
        }

        void AoMorrerArena(UnturnedPlayer vitima, EDeathCause causa, ELimb parte, CSteamID assassino)
        {
            if (luta == null || luta.Acabou) return;
            var id = Id(vitima);
            if (!NaLuta(id)) return;
            Vencer(id == luta.A ? luta.B : luta.A, id);
        }

        void Vencer(ulong vencedor, ulong perdedor)
        {
            luta.Acabou = true;
            luta.Vencedor = vencedor;
            luta.VoltarEm = Time.realtimeSinceStartup + Math.Max(5, S.ArenaSegundosPraLootear);
            long premio = luta.Valor * 2;
            long taxa = premio * Math.Max(0, Math.Min(50, S.ArenaTaxa)) / 100;
            Banco.Somar(vencedor, premio - taxa);
            var r = RankArena();
            if (!r.TryGetValue(vencedor, out var fv)) r[vencedor] = fv = new FichaArena();
            if (!r.TryGetValue(perdedor, out var fp)) r[perdedor] = fp = new FichaArena();
            fv.Nome = NomeDe(vencedor); fp.Nome = NomeDe(perdedor);
            fv.Vitorias++; fv.Ganho += luta.Valor - taxa; fp.Derrotas++; fp.Ganho -= luta.Valor;
            SalvarArena();
            Chat.Todos(Chat.Cor(S.CorDestaque, Chat.Forte("ARENA: ")) + Chat.Cor("#FFFFFF", $"{Chat.Limpar(NomeDe(vencedor))} venceu {Chat.Limpar(NomeDe(perdedor))} e levou ")
                + Chat.Cor(S.CorDestaque, Chat.Forte($"${premio - taxa:N0}")) + Chat.Cor("#FFFFFF", "!"));
            var sp = PlayerTool.getSteamPlayer(vencedor);
            if (sp != null) Chat.Info(UnturnedPlayer.FromSteamPlayer(sp), $"Voce tem {Math.Max(5, S.ArenaSegundosPraLootear)} s pra pegar o loot. Depois volta pra safe.");
            Rocket.Core.Logging.Logger.Log($"[Arena] venceu {vencedor}, perdeu {perdedor}, premio {premio - taxa}");
        }

        void Empate(string motivo)
        {
            if (luta == null) return;
            Banco.Somar(luta.A, luta.Valor);
            Banco.Somar(luta.B, luta.Valor);
            Chat.Todos(Chat.Cor(S.CorDestaque, Chat.Forte("ARENA: ")) + Chat.Cor("#FFFFFF", $"luta cancelada ({motivo}). A aposta voltou pros dois."));
            luta.Acabou = true; luta.Vencedor = 0;
            luta.VoltarEm = Time.realtimeSinceStartup;
        }

        void Voltar(ulong id, Vector3 pos)
        {
            var sp = PlayerTool.getSteamPlayer(id);
            if (sp?.player == null || sp.player.life.isDead) return;
            sp.player.teleportToLocationUnsafe(pos + Vector3.up * .3f, sp.player.look.yaw);
        }

        /// <summary>Roda todo segundo (TickAdmin).</summary>
        void TickArena()
        {
            foreach (var k in desafios.Where(x => Time.realtimeSinceStartup > x.Value.Expira).Select(x => x.Key).ToList()) desafios.Remove(k);
            if (luta == null) return;
            float agora = Time.realtimeSinceStartup;
            if (!luta.Acabou)
            {
                bool aOn = PlayerTool.getSteamPlayer(luta.A) != null, bOn = PlayerTool.getSteamPlayer(luta.B) != null;
                if (!aOn && !bOn) { Empate("os dois sairam"); }
                else if (!aOn) Vencer(luta.B, luta.A); // quem sai no meio perde
                else if (!bOn) Vencer(luta.A, luta.B);
                else if (agora >= luta.Fim) Empate("acabou o tempo");
                else if (agora < luta.Comeca)
                {
                    int s = Mathf.CeilToInt(luta.Comeca - agora);
                    if (s <= 3 || s == 5 || s == 10)
                        foreach (var id in new[] { luta.A, luta.B })
                        {
                            var sp = PlayerTool.getSteamPlayer(id);
                            if (sp != null) Chat.Para(UnturnedPlayer.FromSteamPlayer(sp), Chat.Cor(S.CorDestaque, Chat.Forte($"ARENA: {s}...")));
                        }
                }
                else if (agora - luta.Comeca < 1.05f)
                    foreach (var id in new[] { luta.A, luta.B })
                    {
                        saiuSafeEm.Remove(id);
                        var sp = PlayerTool.getSteamPlayer(id);
                        if (sp != null) Chat.Para(UnturnedPlayer.FromSteamPlayer(sp), Chat.Cor("#FF3B30", Chat.Forte("LUTEM!")));
                    }
                return;
            }
            if (agora < luta.VoltarEm) return;
            Voltar(luta.A, luta.VoltaA);
            Voltar(luta.B, luta.VoltaB);
            luta = null;
        }
    }

    /// <summary>/arena desafiar nome valor | aceitar | recusar | top | marcar 1|2 (dono)</summary>
    public class CmdArena : ComandoSistema
    {
        public override string Name => "arena";
        public override string Help => "Arena PvP 1x1 com aposta.";
        public override string Syntax => "desafiar <nome> <valor> | aceitar | recusar | top";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            var sub = a.Length > 0 ? a[0].ToLowerInvariant() : "";
            switch (sub)
            {
                case "desafiar":
                case "d":
                    if (a.Length < 3) { Chat.Info(p, "Use: /arena desafiar nome valor   (os dois dentro da safe, aposta sai do banco)"); return; }
                    P.ArenaDesafiar(p, string.Join(" ", a.Skip(1).Take(a.Length - 2)), a[a.Length - 1]);
                    return;
                case "aceitar": case "a": P.ArenaResponder(p, true); return;
                case "recusar": case "r": P.ArenaResponder(p, false); return;
                case "top": P.ArenaTop(p); return;
                case "marcar": P.ArenaMarcar(p, a.Length > 1 ? a[1] : "1"); return;
            }
            Chat.Info(p, "ARENA 1x1: /arena desafiar nome valor  |  /arena aceitar  |  /arena recusar  |  /arena top");
        }
    }
}
