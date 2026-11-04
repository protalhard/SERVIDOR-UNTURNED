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
    /// <summary>Recompensa na cabeça (regras do dono):
    /// - Quem coloca paga na hora (sai do banco e fica preso).
    /// - Quando um JOGADOR mata o alvo, cai uma TAG no chão. Qualquer um (menos o próprio alvo) que entrar na safe com ela recebe tudo na hora.
    /// - Se em 24 h ninguém levar a tag pra safe, o dinheiro volta pra quem colocou e a tag perde a validade.
    /// - Só dá pra cancelar enquanto ninguém matou o alvo.</summary>
    public partial class Plugin
    {
        /// <summary>Item da tag: o da config, ou a TAG do mod (51303) assim que ela existir no servidor.</summary>
        ushort TagItem => S.RecompensaTagItem != 0 ? S.RecompensaTagItem : (Assets.find(EAssetType.ITEM, (ushort)51303) != null ? (ushort)51303 : (ushort)0);

        public class Recompensa
        {
            public int Id;
            public ulong Alvo; public string AlvoNome;
            public ulong Quem; public string QuemNome;
            public long Valor;
            public DateTime Criada;
            public int Tag;              // 0 = alvo vivo; senão o número da tag que caiu
            public DateTime TagExpira;
        }
        class EstadoRecompensas { public int ProximoId = 1; public List<Recompensa> Lista = new List<Recompensa>(); }

        EstadoRecompensas recompensas;
        string ArqRecompensas => Path.Combine(Directory, "recompensas.json");
        static readonly byte[] MarcaTag = { (byte)'D', (byte)'Z', (byte)'T', (byte)'G' };
        float proximaChecagemTag;

        EstadoRecompensas Recompensas()
        {
            if (recompensas != null) return recompensas;
            try { if (File.Exists(ArqRecompensas)) recompensas = JsonConvert.DeserializeObject<EstadoRecompensas>(File.ReadAllText(ArqRecompensas)); }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
            return recompensas = recompensas ?? new EstadoRecompensas();
        }

        void SalvarRecompensas()
        {
            try
            {
                var tmp = ArqRecompensas + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(Recompensas(), Formatting.Indented));
                if (File.Exists(ArqRecompensas)) File.Delete(ArqRecompensas);
                File.Move(tmp, ArqRecompensas);
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }

        void LigarRecompensas() => UnturnedPlayerEvents.OnPlayerDeath += AoMorrerRecompensa;
        void DesligarRecompensas() => UnturnedPlayerEvents.OnPlayerDeath -= AoMorrerRecompensa;

        long TotalNaCabeca(ulong alvo) => Recompensas().Lista.Where(r => r.Alvo == alvo && r.Tag == 0).Sum(r => r.Valor);

        // ---------- comandos ----------

        internal void RecompensaColocar(UnturnedPlayer p, string nome, string valorTxt)
        {
            if (TagItem == 0) { Chat.Erro(p, "A recompensa ainda nao esta liberada."); return; }
            if (!long.TryParse((valorTxt ?? "").Replace(".", "").Replace("$", ""), out var valor) || valor < S.RecompensaMin)
            { Chat.Erro(p, $"Valor minimo: ${S.RecompensaMin:N0}. Use: /recompensa colocar nome valor"); return; }
            var alvo = UnturnedPlayer.FromName(nome);
            if (alvo == null) { Chat.Erro(p, "Jogador nao encontrado (tem que estar online)."); return; }
            if (alvo.CSteamID == p.CSteamID) { Chat.Erro(p, "Nao da pra colocar recompensa em voce mesmo."); return; }
            if (Banco.Saldo(Id(p)) < valor) { Chat.Erro(p, $"Voce nao tem ${valor:N0} no banco."); return; }
            Banco.Somar(Id(p), -valor);
            var st = Recompensas();
            st.Lista.Add(new Recompensa { Id = st.ProximoId++, Alvo = Id(alvo), AlvoNome = alvo.CharacterName, Quem = Id(p), QuemNome = p.CharacterName, Valor = valor, Criada = DateTime.UtcNow });
            SalvarRecompensas();
            var total = TotalNaCabeca(Id(alvo));
            Chat.Todos(Chat.Cor("#FF3B30", Chat.Forte("RECOMPENSA: ")) + Chat.Cor("#FFFFFF", $"agora tem ") + Chat.Cor(S.CorDestaque, Chat.Forte($"${total:N0}"))
                + Chat.Cor("#FFFFFF", $" na cabeca de {Chat.Limpar(alvo.CharacterName)}. Mate, pegue a TAG e leve pra safe."));
            Rocket.Core.Logging.Logger.Log($"[Recompensa] {p.CharacterName} ({Id(p)}) colocou {valor} em {alvo.CharacterName} ({Id(alvo)})");
        }

        internal void RecompensaCancelar(UnturnedPlayer p, string nome)
        {
            var st = Recompensas();
            var minhas = st.Lista.Where(r => r.Quem == Id(p) && (string.IsNullOrWhiteSpace(nome) || (r.AlvoNome ?? "").IndexOf(nome, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            if (minhas.Count == 0) { Chat.Erro(p, "Voce nao tem recompensa com esse nome."); return; }
            var podem = minhas.Where(r => r.Tag == 0).ToList();
            if (podem.Count == 0) { Chat.Erro(p, "Nao da mais pra cancelar: o alvo ja foi morto e a tag caiu. Se ninguem levar pra safe em 24 h, o dinheiro volta."); return; }
            foreach (var r in podem) { Banco.Somar(Id(p), r.Valor); st.Lista.Remove(r); }
            SalvarRecompensas();
            Chat.Info(p, $"Recompensa cancelada. ${podem.Sum(r => r.Valor):N0} voltou pro seu banco.");
        }

        internal void RecompensaLista(UnturnedPlayer p)
        {
            var st = Recompensas();
            var vivos = st.Lista.Where(r => r.Tag == 0).GroupBy(r => r.Alvo).Select(g => (nome: g.Last().AlvoNome, total: g.Sum(r => r.Valor))).OrderByDescending(x => x.total).Take(8).ToList();
            var tags = st.Lista.Where(r => r.Tag != 0).GroupBy(r => r.Tag).Select(g => (nome: g.First().AlvoNome, total: g.Sum(r => r.Valor), exp: g.First().TagExpira)).ToList();
            if (vivos.Count == 0 && tags.Count == 0) { Chat.Info(p, "Nenhuma recompensa ativa. Coloque com /recompensa colocar nome valor"); return; }
            Chat.Para(p, Chat.Cor("#FF3B30", Chat.Forte("RECOMPENSAS")));
            foreach (var v in vivos) Chat.Para(p, Chat.Cor("#FFFFFF", $"{Chat.Limpar(v.nome)}: ${v.total:N0}"));
            foreach (var t in tags) Chat.Para(p, Chat.Cor(S.CorApagada, $"TAG de {Chat.Limpar(t.nome)} no mapa: ${t.total:N0} (vale por mais {Math.Max(0, (t.exp - DateTime.UtcNow).TotalHours):0}h)"));
        }

        // ---------- morte e tag ----------

        void AoMorrerRecompensa(UnturnedPlayer vitima, EDeathCause causa, ELimb parte, CSteamID assassino)
        {
            try
            {
                var id = Id(vitima);
                var st = Recompensas();
                var ativas = st.Lista.Where(r => r.Alvo == id && r.Tag == 0).ToList();
                if (ativas.Count == 0) return;
                if (assassino == CSteamID.Nil || assassino == vitima.CSteamID || PlayerTool.getPlayer(assassino) == null) return; // só vale morto por jogador
                if (NaLuta(id)) return; // arena não conta
                int tag = st.ProximoId++;
                var exp = DateTime.UtcNow.AddHours(Math.Max(1, S.RecompensaHorasTag));
                foreach (var r in ativas) { r.Tag = tag; r.TagExpira = exp; }
                SalvarRecompensas();
                var estado = new byte[8];
                Array.Copy(MarcaTag, estado, 4);
                Array.Copy(BitConverter.GetBytes(tag), 0, estado, 4, 4);
                ItemManager.dropItem(new Item(TagItem, 1, 100, estado), vitima.Position + Vector3.up, false, true, false);
                var total = ativas.Sum(r => r.Valor);
                var matador = PlayerTool.getPlayer(assassino);
                Chat.Todos(Chat.Cor("#FF3B30", Chat.Forte("RECOMPENSA: ")) + Chat.Cor("#FFFFFF", $"{Chat.Limpar(matador.channel.owner.playerID.characterName)} matou {Chat.Limpar(vitima.CharacterName)}! A TAG de ")
                    + Chat.Cor(S.CorDestaque, Chat.Forte($"${total:N0}")) + Chat.Cor("#FFFFFF", $" caiu perto de X {vitima.Position.x:0} Z {vitima.Position.z:0}. Quem levar pra safe em {S.RecompensaHorasTag}h leva o dinheiro."));
                Rocket.Core.Logging.Logger.Log($"[Recompensa] tag {tag} de {vitima.CharacterName} ({id}) caiu, {total}, morto por {assassino}");
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }

        static int NumeroTag(Item it)
        {
            var s = it?.state;
            if (s == null || s.Length < 8) return 0;
            for (int i = 0; i < 4; i++) if (s[i] != MarcaTag[i]) return 0;
            return BitConverter.ToInt32(s, 4);
        }

        /// <summary>A cada 2 s: quem está na safe com tag recebe; tags vencidas devolvem o dinheiro.</summary>
        void TickRecompensas()
        {
            if (TagItem == 0 || Time.realtimeSinceStartup < proximaChecagemTag) return;
            proximaChecagemTag = Time.realtimeSinceStartup + 2f;
            var st = Recompensas();
            bool mudou = false;

            foreach (var g in st.Lista.Where(r => r.Tag != 0 && DateTime.UtcNow > r.TagExpira).GroupBy(r => r.Tag).ToList())
            {
                foreach (var r in g) { Banco.Somar(r.Quem, r.Valor); st.Lista.Remove(r); }
                Chat.Todos(Chat.Cor("#FF3B30", Chat.Forte("RECOMPENSA: ")) + Chat.Cor("#FFFFFF", $"ninguem levou a TAG de {Chat.Limpar(g.First().AlvoNome)} pra safe. O dinheiro voltou pra quem colocou."));
                mudou = true;
            }

            foreach (var c in Provider.clients)
            {
                var pl = c.player;
                if (pl?.inventory?.items == null || pl.life.isDead || ZonaSeguraEm(pl.transform.position) == null) continue;
                var cid = c.playerID.steamID.m_SteamID;
                for (byte pg = 0; pg < PlayerInventory.PAGES - 2; pg++)
                {
                    var items = pl.inventory.items[pg];
                    if (items == null) continue;
                    for (int i = items.getItemCount() - 1; i >= 0; i--)
                    {
                        var it = items.getItem((byte)i).item;
                        if (it.id != TagItem) continue;
                        int tag = NumeroTag(it);
                        if (tag == 0) continue;
                        var dela = st.Lista.Where(r => r.Tag == tag).ToList();
                        var up = UnturnedPlayer.FromSteamPlayer(c);
                        if (dela.Count == 0) { pl.inventory.removeItem(pg, (byte)i); Chat.Info(up, "Essa tag de recompensa ja venceu."); continue; }
                        if (dela[0].Alvo == cid) continue; // o próprio alvo não resgata a tag dele
                        long total = dela.Sum(r => r.Valor);
                        pl.inventory.removeItem(pg, (byte)i);
                        Banco.Somar(cid, total);
                        foreach (var r in dela) st.Lista.Remove(r);
                        mudou = true;
                        Chat.Todos(Chat.Cor("#FF3B30", Chat.Forte("RECOMPENSA: ")) + Chat.Cor("#FFFFFF", $"{Chat.Limpar(c.playerID.characterName)} entregou a TAG de {Chat.Limpar(dela[0].AlvoNome)} e levou ")
                            + Chat.Cor(S.CorDestaque, Chat.Forte($"${total:N0}")) + Chat.Cor("#FFFFFF", "!"));
                        Rocket.Core.Logging.Logger.Log($"[Recompensa] tag {tag} entregue por {c.playerID.characterName} ({cid}), {total}");
                    }
                }
            }
            if (mudou) SalvarRecompensas();
        }
    }

    /// <summary>/recompensa colocar nome valor | cancelar nome | lista</summary>
    public class CmdRecompensa : ComandoSistema
    {
        public override string Name => "recompensa";
        public override string Help => "Recompensa na cabeca de um jogador.";
        public override string Syntax => "colocar <nome> <valor> | cancelar <nome> | lista";
        public override List<string> Aliases => new List<string> { "bounty" };
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            var sub = a.Length > 0 ? a[0].ToLowerInvariant() : "";
            switch (sub)
            {
                case "colocar":
                case "por":
                    if (a.Length < 3) { Chat.Info(p, "Use: /recompensa colocar nome valor   (sai do seu banco)"); return; }
                    P.RecompensaColocar(p, string.Join(" ", a.Skip(1).Take(a.Length - 2)), a[a.Length - 1]);
                    return;
                case "cancelar": P.RecompensaCancelar(p, string.Join(" ", a.Skip(1))); return;
                case "lista": case "": P.RecompensaLista(p); return;
            }
            Chat.Info(p, "RECOMPENSA: /recompensa colocar nome valor  |  /recompensa cancelar nome  |  /recompensa lista");
        }
    }
}
