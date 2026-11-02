using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Aba Economia do painel do site: o plugin grava economia.json a cada 2 min com o dinheiro do jogo
    /// (banco, notas no bolso, nos baús e no chão) e recebe do site os ajustes de revenda e de preço.</summary>
    public partial class Plugin
    {
        float proximaEconomia;
        // último dinheiro no bolso de cada jogador (o de quem está offline fica o da última vez que saiu)
        readonly Dictionary<ulong, long> bolsoVisto = new Dictionary<ulong, long>();
        bool bolsoCarregado;

        string ArqBolsos => Path.Combine(Directory, "bolsos.tsv");

        void CarregarBolsos()
        {
            if (bolsoCarregado) return;
            bolsoCarregado = true;
            try
            {
                if (File.Exists(ArqBolsos))
                    foreach (var l in File.ReadAllLines(ArqBolsos))
                    {
                        var c = l.Split('\t');
                        if (c.Length >= 2 && ulong.TryParse(c[0], out var id) && long.TryParse(c[1], out var v)) bolsoVisto[id] = v;
                    }
            }
            catch { }
        }

        int ValorNota(ushort id)
        {
            foreach (var n in Configuration.Instance.Notas ?? new List<Nota>()) if (n.Id == id) return n.Valor;
            return 0;
        }

        static int UnidadesItem(Item item)
        {
            var a = Assets.find(EAssetType.ITEM, item.id) as ItemAsset;
            return a != null && a.amount > 1 ? Math.Max(1, (int)item.amount) : 1;
        }

        void TickEconomia()
        {
            if (Time.realtimeSinceStartup < proximaEconomia || !Level.isLoaded) return;
            proximaEconomia = Time.realtimeSinceStartup + 120f;
            try { GravarEconomia(); } catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }

        void GravarEconomia()
        {
            CarregarBolsos();
            var notasValor = (Configuration.Instance.Notas ?? new List<Nota>()).Where(n => n.Valor > 0).Select(n => n.Valor).Distinct().OrderByDescending(v => v).ToList();
            var porNota = new Dictionary<int, long[]>(); // valor -> [bolso, baus, chao] em quantidade de notas
            foreach (var v in notasValor) porNota[v] = new long[3];
            long emBaus = 0, noChao = 0;
            int baus = 0;

            // bolso dos online (atualiza o cache)
            foreach (var sp in Provider.clients)
            {
                if (sp?.player == null) continue;
                var p = UnturnedPlayer.FromSteamPlayer(sp);
                long b = 0;
                for (byte pg = 0; pg < PlayerInventory.STORAGE; pg++)
                {
                    var items = p.Player.inventory.items[pg];
                    if (items == null) continue;
                    foreach (var j in items.items)
                    {
                        int v = ValorNota(j.item.id);
                        if (v <= 0) continue;
                        int u = UnidadesItem(j.item);
                        b += (long)v * u;
                        if (porNota.TryGetValue(v, out var a)) a[0] += u;
                    }
                }
                bolsoVisto[Id(p)] = b;
            }
            // baús e caixas do mapa
            foreach (var reg in BarricadeManager.regions)
            {
                if (reg?.drops == null) continue;
                foreach (var d in reg.drops)
                {
                    if (!(d?.interactable is InteractableStorage st) || st.items == null) continue;
                    long nesse = 0;
                    foreach (var j in st.items.items)
                    {
                        int v = ValorNota(j.item.id);
                        if (v <= 0) continue;
                        int u = UnidadesItem(j.item);
                        nesse += (long)v * u;
                        if (porNota.TryGetValue(v, out var a)) a[1] += u;
                    }
                    if (nesse > 0) { emBaus += nesse; baus++; }
                }
            }
            // chão
            if (ItemManager.regions != null)
                foreach (var reg in ItemManager.regions)
                {
                    if (reg?.items == null) continue;
                    foreach (var it in reg.items)
                    {
                        int v = ValorNota(it.item.id);
                        if (v <= 0) continue;
                        int u = UnidadesItem(it.item);
                        noChao += (long)v * u;
                        if (porNota.TryGetValue(v, out var a)) a[2] += u;
                    }
                }

            try { File.WriteAllLines(ArqBolsos, bolsoVisto.Select(kv => kv.Key + "\t" + kv.Value)); } catch { }

            var online = new HashSet<ulong>(Provider.clients.Where(c => c?.player != null).Select(c => c.playerID.steamID.m_SteamID));
            var ids = new HashSet<ulong>(Banco.Todos.Select(kv => kv.Key));
            ids.UnionWith(bolsoVisto.Keys);
            long totalBanco = 0, totalBolso = 0;
            var linhas = new List<(ulong id, string nome, long banco, long bolso, bool on)>();
            foreach (var id in ids)
            {
                long bc = Banco.Saldo(id), bl = bolsoVisto.TryGetValue(id, out var x) ? x : 0;
                totalBanco += bc; totalBolso += bl;
                if (bc == 0 && bl == 0) continue;
                var f = Stats.Existe(id) ? Stats.Obter(id) : null;
                linhas.Add((id, f?.Nome ?? id.ToString(), bc, bl, online.Contains(id)));
            }

            var sb = new StringBuilder();
            sb.Append("{\"atualizado\":").Append(Estatisticas.Agora);
            sb.Append(",\"revenda\":").Append(ItemLoja.RevendaPorcento);
            sb.Append(",\"totais\":{\"banco\":").Append(totalBanco).Append(",\"bolso\":").Append(totalBolso)
              .Append(",\"baus\":").Append(emBaus).Append(",\"bausComDinheiro\":").Append(baus).Append(",\"chao\":").Append(noChao)
              .Append(",\"total\":").Append(totalBanco + totalBolso + emBaus + noChao).Append('}');
            sb.Append(",\"notas\":[");
            bool pri = true;
            foreach (var v in notasValor)
            {
                var a = porNota[v];
                if (!pri) sb.Append(',');
                pri = false;
                sb.Append("{\"valor\":").Append(v).Append(",\"bolso\":").Append(a[0]).Append(",\"baus\":").Append(a[1]).Append(",\"chao\":").Append(a[2]).Append('}');
            }
            sb.Append("],\"jogadores\":[");
            pri = true;
            foreach (var l in linhas.OrderByDescending(l => l.banco + l.bolso).Take(500))
            {
                if (!pri) sb.Append(',');
                pri = false;
                sb.Append("{\"id\":\"").Append(l.id).Append("\",\"nome\":").Append(Json(l.nome)).Append(",\"banco\":").Append(l.banco)
                  .Append(",\"bolso\":").Append(l.bolso).Append(",\"online\":").Append(l.on ? "true" : "false").Append('}');
            }
            sb.Append("],\"lojas\":[");
            pri = true;
            foreach (var lj in Configuration.Instance.Lojas ?? new List<Loja>())
            {
                if (!pri) sb.Append(',');
                pri = false;
                var compra = lj.Itens.Where(i => i.Compra > 0).ToList();
                var caca = lj.Itens.Where(i => i.Compra <= 0 && i.Venda > 0 && !i.Veiculo).ToList();
                sb.Append("{\"id\":").Append(Json(lj.Id)).Append(",\"nome\":").Append(Json(lj.Nome)).Append(",\"tipo\":").Append(Json(lj.Tipo))
                  .Append(",\"itensCompra\":").Append(compra.Count).Append(",\"mediaCompra\":").Append(compra.Count == 0 ? 0 : (long)compra.Average(i => i.Compra))
                  .Append(",\"itensSoCompra\":").Append(caca.Count).Append('}');
            }
            sb.Append("]}");
            var arq = Path.Combine(Directory, "economia.json");
            File.WriteAllText(arq + ".tmp", sb.ToString());
            if (File.Exists(arq)) File.Delete(arq);
            File.Move(arq + ".tmp", arq);
        }

        void GuardarBolsoAoSair(UnturnedPlayer p)
        {
            try { CarregarBolsos(); bolsoVisto[Id(p)] = Dinheiro.NoBolso(p); } catch { }
        }

        /// <summary>Comando do site (aba Economia). texto: "revenda|60", "compra|10|loja" ou "soCompra|-5|loja" (loja vazia = todas).</summary>
        string AjusteEconomia(string texto)
        {
            var c = (texto ?? "").Split('|');
            if (c.Length < 2 || !double.TryParse(c[1].Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n))
                return "valor invalido";
            var cfg = Configuration.Instance;
            if (c[0] == "revenda")
            {
                int pct = Mathf.Clamp((int)Math.Round(n), 0, 100);
                cfg.RevendaPorcento = pct;
                ItemLoja.RevendaPorcento = pct;
                Configuration.Save();
                proximaEconomia = 0;
                return $"revenda agora {pct}% do preco de compra";
            }
            if (n == 0 || n <= -95 || n > 1000) return "porcentagem fora do limite (-95 a 1000)";
            var loja = c.Length > 2 ? c[2].Trim() : "";
            var lojas = (cfg.Lojas ?? new List<Loja>()).Where(l => loja == "" || l.Id == loja).ToList();
            int mudou = 0;
            foreach (var l in lojas)
                foreach (var it in l.Itens)
                {
                    if (c[0] == "compra" && it.Compra > 0) { it.Compra = AplicarPorcento(it.Compra, n); mudou++; }
                    else if (c[0] == "soCompra" && it.Compra <= 0 && it.Venda > 0 && !it.Veiculo) { it.Venda = AplicarPorcento(it.Venda, n); mudou++; }
                }
            Configuration.Save();
            proximaEconomia = 0;
            return $"{(c[0] == "compra" ? "precos de compra" : "precos que a loja paga (caca e itens que ela so compra)")} {n:+0.#;-0.#}% em {mudou} itens ({(loja == "" ? "todas as lojas" : loja)})";
        }
    }
}
