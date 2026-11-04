using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Leilão (pedido do dono): o jogador põe o item que está na mão com /leilao vender preço [horas].
    /// O item sai do inventário e fica guardado. Os lances são com o dinheiro do banco, no jogo (/leilao lance id valor)
    /// ou no site (leilao_comandos.txt). O lance fica preso; se alguém cobrir, o anterior recebe de volta.
    /// No fim, quem ganhou retira o item na safe (/leilao retirar) e o vendedor recebe menos a taxa.
    /// Sem lance, o item volta pro vendedor (também retira na safe).</summary>
    public partial class Plugin
    {
        public class ItemGuardado { public ushort Id; public byte Qtd, Qualidade; public string Estado; public string Nome; }
        public class Lote
        {
            public int Id; public ulong Vendedor; public string VendedorNome;
            public ItemGuardado Item; public long Minimo, Lance; public ulong Lider; public string LiderNome;
            public DateTime Fim; public bool Encerrado; public int Lances;
        }
        public class EstadoLeilao
        {
            public int ProximoId = 1;
            public List<Lote> Lotes = new List<Lote>();
            public Dictionary<ulong, List<ItemGuardado>> Retirar = new Dictionary<ulong, List<ItemGuardado>>();
            public List<string> Historico = new List<string>();
            public DateTime Atualizado;
        }

        EstadoLeilao leilao;
        float proximoTickLeilao;
        string ArqLeilao => Path.Combine(Directory, "leilao.json");
        string ArqLeilaoCmd => Path.Combine(Directory, "leilao_comandos.txt");

        EstadoLeilao Leilao()
        {
            if (leilao != null) return leilao;
            try { if (File.Exists(ArqLeilao)) leilao = JsonConvert.DeserializeObject<EstadoLeilao>(File.ReadAllText(ArqLeilao)); }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
            return leilao = leilao ?? new EstadoLeilao();
        }

        void SalvarLeilao()
        {
            try
            {
                var l = Leilao();
                l.Atualizado = DateTime.UtcNow;
                if (l.Historico.Count > 200) l.Historico = l.Historico.Skip(l.Historico.Count - 200).ToList();
                var tmp = ArqLeilao + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(l, Formatting.Indented));
                if (File.Exists(ArqLeilao)) File.Delete(ArqLeilao);
                File.Move(tmp, ArqLeilao);
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }

        void Avisar(ulong id, string texto)
        {
            var sp = PlayerTool.getSteamPlayer(id);
            if (sp != null) Chat.Info(UnturnedPlayer.FromSteamPlayer(sp), texto);
        }

        // ---------- comandos ----------

        internal void LeilaoVender(UnturnedPlayer p, string precoTxt, string horasTxt)
        {
            if (!S.LeilaoLigado) { Chat.Erro(p, "Leilao desligado."); return; }
            if (ZonaSeguraEm(p.Position) == null) { Chat.Erro(p, "Coloque itens no leilao dentro da safe."); return; }
            if (!long.TryParse((precoTxt ?? "").Replace(".", "").Replace("$", ""), out var minimo) || minimo < 1)
            { Chat.Erro(p, "Use: /leilao vender preco_minimo [horas]   (com o item na mao)"); return; }
            int horas = int.TryParse(horasTxt, out var h) ? Mathf.Clamp(h, 1, 48) : 24;
            var st = Leilao();
            if (st.Lotes.Count(x => !x.Encerrado && x.Vendedor == Id(p)) >= S.LeilaoMaxPorJogador) { Chat.Erro(p, $"Voce ja tem {S.LeilaoMaxPorJogador} itens no leilao."); return; }
            var eq = p.Player.equipment;
            if (eq.asset == null || eq.isBusy) { Chat.Erro(p, "Segure na mao o item que quer leiloar."); return; }
            if (Configuration.Instance.RelogioItem != 0 && eq.asset.id == Configuration.Instance.RelogioItem) { Chat.Erro(p, "Esse item nao pode ir pro leilao."); return; }
            byte pg = eq.equippedPage, x = eq.equipped_x, y = eq.equipped_y;
            var items = p.Player.inventory.items[pg];
            var idx = items.getIndex(x, y);
            if (idx == 255) { Chat.Erro(p, "Nao achei o item na mochila."); return; }
            var it = items.getItem(idx).item;
            var guardado = new ItemGuardado { Id = it.id, Qtd = it.amount, Qualidade = it.quality, Estado = it.state != null ? Convert.ToBase64String(it.state) : "", Nome = eq.asset.itemName };
            eq.dequip();
            p.Player.inventory.removeItem(pg, items.getIndex(x, y));
            var lote = new Lote { Id = st.ProximoId++, Vendedor = Id(p), VendedorNome = p.CharacterName, Item = guardado, Minimo = minimo, Fim = DateTime.UtcNow.AddHours(horas) };
            st.Lotes.Add(lote);
            SalvarLeilao();
            Chat.Info(p, $"{guardado.Nome} no leilao (#{lote.Id}) por {horas}h, lance minimo ${minimo:N0}. Taxa de {S.LeilaoTaxa}% quando vender.");
            Chat.Todos(Chat.Cor(S.CorDestaque, Chat.Forte("LEILAO: ")) + Chat.Cor("#FFFFFF", $"{Chat.Limpar(p.CharacterName)} colocou {Chat.Limpar(guardado.Nome)} (#{lote.Id}) a partir de ${minimo:N0}. Lance no site ou com /leilao lance {lote.Id} valor."));
            Rocket.Core.Logging.Logger.Log($"[Leilao] #{lote.Id} {p.CharacterName} ({Id(p)}) {guardado.Nome} id {guardado.Id} min {minimo}");
        }

        /// <summary>Lance (do jogo ou do site). Devolve a resposta pra quem deu o lance.</summary>
        string LeilaoLance(ulong quem, string nome, int id, long valor)
        {
            if (!S.LeilaoLigado) return "Leilao desligado.";
            var st = Leilao();
            var lote = st.Lotes.FirstOrDefault(l => l.Id == id && !l.Encerrado);
            if (lote == null) return "Esse leilao nao existe ou ja acabou.";
            if (lote.Vendedor == quem) return "Voce nao pode dar lance no seu proprio item.";
            if (lote.Lider == quem) return "Voce ja e o maior lance.";
            long minimo = lote.Lance == 0 ? lote.Minimo : lote.Lance + Math.Max(1, lote.Lance * 5 / 100);
            if (valor < minimo) return $"O lance minimo agora e ${minimo:N0}.";
            if (Banco.Saldo(quem) < valor) return $"Voce nao tem ${valor:N0} no banco.";
            Banco.Somar(quem, -valor);
            if (lote.Lider != 0)
            {
                Banco.Somar(lote.Lider, lote.Lance);
                Avisar(lote.Lider, $"Cobriram seu lance no leilao #{lote.Id} ({Chat.Limpar(lote.Item.Nome)}). ${lote.Lance:N0} voltou pro banco.");
            }
            lote.Lance = valor; lote.Lider = quem; lote.LiderNome = nome; lote.Lances++;
            if (lote.Fim - DateTime.UtcNow < TimeSpan.FromMinutes(2)) lote.Fim = DateTime.UtcNow.AddMinutes(2); // ninguém ganha no último segundo
            SalvarLeilao();
            Avisar(lote.Vendedor, $"Novo lance no seu {Chat.Limpar(lote.Item.Nome)} (#{lote.Id}): ${valor:N0}.");
            Rocket.Core.Logging.Logger.Log($"[Leilao] lance #{lote.Id} {nome} ({quem}) {valor}");
            return $"Lance de ${valor:N0} no {lote.Item.Nome} (#{lote.Id}). O dinheiro fica preso ate alguem cobrir ou acabar.";
        }

        internal void LeilaoLanceJogo(UnturnedPlayer p, string idTxt, string valorTxt)
        {
            if (!int.TryParse((idTxt ?? "").TrimStart('#'), out var id) || !long.TryParse((valorTxt ?? "").Replace(".", "").Replace("$", ""), out var valor))
            { Chat.Erro(p, "Use: /leilao lance numero valor"); return; }
            Chat.Info(p, LeilaoLance(Id(p), p.CharacterName, id, valor));
        }

        internal void LeilaoLista(UnturnedPlayer p)
        {
            var ativos = Leilao().Lotes.Where(l => !l.Encerrado).OrderBy(l => l.Fim).Take(8).ToList();
            if (ativos.Count == 0) { Chat.Info(p, "Nenhum item no leilao. Coloque o seu com /leilao vender preco (item na mao)."); }
            else
            {
                Chat.Para(p, Chat.Cor(S.CorDestaque, Chat.Forte("LEILAO ")) + Chat.Cor(S.CorApagada, "(lista completa no site)"));
                foreach (var l in ativos)
                {
                    var resta = l.Fim - DateTime.UtcNow;
                    Chat.Para(p, Chat.Cor("#FFFFFF", $"#{l.Id} {Chat.Limpar(l.Item.Nome)}{(l.Item.Qtd > 1 ? " x" + l.Item.Qtd : "")}: ")
                        + Chat.Cor(S.CorDestaque, l.Lance > 0 ? $"${l.Lance:N0}" : $"min ${l.Minimo:N0}")
                        + Chat.Cor(S.CorApagada, $" ({(resta.TotalHours >= 1 ? $"{(int)resta.TotalHours}h" : $"{Math.Max(0, (int)resta.TotalMinutes)}min")})"));
                }
            }
            if (Leilao().Retirar.TryGetValue(Id(p), out var r) && r.Count > 0)
                Chat.Para(p, Chat.Cor("#3DDC84", $"Voce tem {r.Count} item(ns) do leilao pra retirar: /leilao retirar (na safe)."));
        }

        internal void LeilaoRetirar(UnturnedPlayer p)
        {
            var st = Leilao();
            if (!st.Retirar.TryGetValue(Id(p), out var lista) || lista.Count == 0) { Chat.Erro(p, "Voce nao tem item do leilao pra retirar."); return; }
            if (ZonaSeguraEm(p.Position) == null) { Chat.Erro(p, "Retire os itens do leilao dentro da safe."); return; }
            int n = 0;
            foreach (var g in lista.ToList())
            {
                var item = new Item(g.Id, g.Qtd, g.Qualidade, string.IsNullOrEmpty(g.Estado) ? new byte[0] : Convert.FromBase64String(g.Estado));
                if (!p.Player.inventory.tryAddItem(item, true)) ItemManager.dropItem(item, p.Position + Vector3.up, false, true, false);
                lista.Remove(g); n++;
            }
            st.Retirar.Remove(Id(p));
            SalvarLeilao();
            Chat.Info(p, $"Voce retirou {n} item(ns) do leilao. O que nao coube caiu no chao do seu lado.");
        }

        // ---------- tick ----------

        void TickLeilao()
        {
            if (!S.LeilaoLigado || Time.realtimeSinceStartup < proximoTickLeilao) return;
            proximoTickLeilao = Time.realtimeSinceStartup + 2f;
            var st = Leilao();
            bool mudou = false;

            // lances vindos do site (o site só grava se a pessoa estiver logada com a Steam)
            if (File.Exists(ArqLeilaoCmd))
            {
                string[] linhas = null;
                var proc = ArqLeilaoCmd + ".proc";
                try { if (File.Exists(proc)) File.Delete(proc); File.Move(ArqLeilaoCmd, proc); linhas = File.ReadAllLines(proc); File.Delete(proc); }
                catch { }
                foreach (var l in linhas ?? new string[0])
                {
                    var c = l.Split('\t');
                    if (c.Length < 4 || c[0] != "lance" || !int.TryParse(c[1], out var id) || !ulong.TryParse(c[2], out var quem) || !long.TryParse(c[3], out var valor)) continue;
                    var r = LeilaoLance(quem, NomeDe(quem), id, valor);
                    try { File.AppendAllText(Path.Combine(Directory, "leilao_respostas.tsv"), $"{DateTime.UtcNow:o}\t{quem}\t{id}\t{r.Replace('\t', ' ')}{Environment.NewLine}"); } catch { }
                }
            }

            foreach (var lote in st.Lotes.Where(l => !l.Encerrado && DateTime.UtcNow >= l.Fim).ToList())
            {
                lote.Encerrado = true; mudou = true;
                if (lote.Lider == 0)
                {
                    Guardar(st, lote.Vendedor, lote.Item);
                    Avisar(lote.Vendedor, $"Ninguem deu lance no seu {Chat.Limpar(lote.Item.Nome)} (#{lote.Id}). Retire na safe com /leilao retirar.");
                    st.Historico.Add($"{DateTime.UtcNow:o}\t#{lote.Id}\t{lote.Item.Nome}\tsem lance");
                    continue;
                }
                long taxa = lote.Lance * Math.Max(0, Math.Min(50, S.LeilaoTaxa)) / 100;
                Banco.Somar(lote.Vendedor, lote.Lance - taxa);
                Guardar(st, lote.Lider, lote.Item);
                Avisar(lote.Vendedor, $"Seu {Chat.Limpar(lote.Item.Nome)} (#{lote.Id}) foi vendido por ${lote.Lance:N0}. Voce recebeu ${lote.Lance - taxa:N0} no banco.");
                Avisar(lote.Lider, $"Voce ganhou o leilao #{lote.Id}: {Chat.Limpar(lote.Item.Nome)}. Retire na safe com /leilao retirar.");
                st.Historico.Add($"{DateTime.UtcNow:o}\t#{lote.Id}\t{lote.Item.Nome}\t{lote.LiderNome}\t{lote.Lance}");
                Rocket.Core.Logging.Logger.Log($"[Leilao] #{lote.Id} vendido para {lote.LiderNome} ({lote.Lider}) por {lote.Lance}, taxa {taxa}");
            }
            // lote encerrado sai da lista depois de 1 dia (fica no histórico)
            if (st.Lotes.RemoveAll(l => l.Encerrado && DateTime.UtcNow - l.Fim > TimeSpan.FromDays(1)) > 0) mudou = true;
            if (mudou) SalvarLeilao();
        }

        static void Guardar(EstadoLeilao st, ulong dono, ItemGuardado it)
        {
            if (!st.Retirar.TryGetValue(dono, out var l)) st.Retirar[dono] = l = new List<ItemGuardado>();
            l.Add(it);
        }

        void AvisarLeilaoAoEntrar(UnturnedPlayer p)
        {
            if (S.LeilaoLigado && Leilao().Retirar.TryGetValue(Id(p), out var r) && r.Count > 0)
                Chat.Para(p, Chat.Cor("#3DDC84", $"LEILAO: voce tem {r.Count} item(ns) pra retirar na safe com /leilao retirar."));
        }
    }

    public class CmdLeilao : ComandoSistema
    {
        public override string Name => "leilao";
        public override string Help => "Leilao entre jogadores.";
        public override string Syntax => "vender preco [horas] | lance numero valor | retirar | lista";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            var sub = a.Length > 0 ? a[0].ToLowerInvariant() : "";
            switch (sub)
            {
                case "vender": P.LeilaoVender(p, a.Length > 1 ? a[1] : "", a.Length > 2 ? a[2] : ""); return;
                case "lance": P.LeilaoLanceJogo(p, a.Length > 1 ? a[1] : "", a.Length > 2 ? a[2] : ""); return;
                case "retirar": P.LeilaoRetirar(p); return;
            }
            P.LeilaoLista(p);
            Chat.Para(p, Chat.Cor(Plugin.Instancia.S.CorApagada, "/leilao vender preco [horas] (item na mao)  |  /leilao lance numero valor  |  /leilao retirar"));
        }
    }
}
