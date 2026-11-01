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
    /// <summary>Lojas com NPC: o jogador aperta F no boneco vendedor (barricada 51240-51249) e abre a janela
    /// DZ_Loja (efeito 51234). Nomes em NOMES_loja_dz.md. Paga com as notas da mochila e devolve troco.</summary>
    public partial class Plugin
    {
        public const ushort LojaId = 51234;
        public const short LojaKey = 5125;
        const int ItensPorPagina = 12;
        const int NpcVault = 51246, NpcBanco = 51249;

        /// <summary>Barricada do NPC -> tipo de loja que ela abre (a loja mais perto desse tipo).</summary>
        static readonly Dictionary<ushort, string> NpcTipo = new Dictionary<ushort, string>
        {
            { 51240, "armas" }, { 51241, "comida" }, { 51242, "roupas" }, { 51243, "remedios" }, { 51244, "equipamentos" },
            { 51245, "garagem" }, { 51247, "negro" }, { 51248, "cacador" },
        };

        public static bool EhNpc(ushort id) => id >= 51240 && id <= 51249;

        class EstadoLoja
        {
            public Loja Loja;
            public bool Vender;
            public int Cat, Pagina, Sel = -1, Qtd = 1;
            public string Busca = "";
            public Vector3 Npc;
            public List<string> Cats = new List<string>();
            public List<ItemLoja> Lista = new List<ItemLoja>();
        }

        readonly Dictionary<ulong, EstadoLoja> lojasAbertas = new Dictionary<ulong, EstadoLoja>();
        readonly Dictionary<ulong, HashSet<string>> vaultsComprados = new Dictionary<ulong, HashSet<string>>();
        string ArqVaults => Path.Combine(Directory, "vaults_comprados.tsv");

        static void LT(UnturnedPlayer p, string nome, string texto) => EffectManager.sendUIEffectText(LojaKey, Con(p), true, nome, texto ?? "");
        static void LV(UnturnedPlayer p, string nome, bool on) => EffectManager.sendUIEffectVisibility(LojaKey, Con(p), true, nome, on);

        void LigarLojas()
        {
            BarricadeManager.onOpenStorageRequested += AoAbrirBau;
            BarricadeManager.onSalvageBarricadeRequested += AoRecolherBarricada;
            CarregarVaultsComprados();
        }

        void DesligarLojas()
        {
            BarricadeManager.onOpenStorageRequested -= AoAbrirBau;
            BarricadeManager.onSalvageBarricadeRequested -= AoRecolherBarricada;
            foreach (var id in lojasAbertas.Keys.ToList())
            {
                var sp = PlayerTool.getSteamPlayer(id);
                if (sp?.player != null) FecharLoja(UnturnedPlayer.FromSteamPlayer(sp));
            }
        }

        /// <summary>Config antiga: troca as lojas pelas novas (mantém a posição das que já estavam no mapa)
        /// e põe as notas do California 2 na frente das vanilla.</summary>
        void MigrarLojas()
        {
            var cfg = Configuration.Instance;
            bool mudou = false;
            var notasCa2 = new[] { new Nota(4258, 500), new Nota(4257, 100), new Nota(4256, 50), new Nota(4255, 10), new Nota(4254, 5) };
            if (cfg.Notas == null) cfg.Notas = new List<Nota>();
            if (!cfg.Notas.Any(n => n.Id == 4257))
            {
                cfg.Notas.InsertRange(0, notasCa2);
                mudou = true;
            }
            if (cfg.VersaoLojas < LojasPadrao.Versao)
            {
                var antigas = cfg.Lojas ?? new List<Loja>();
                var mapa = new Dictionary<string, string> { { "caca", "cacador" }, { "mercadonegro", "negro" } };
                var novas = LojasPadrao.Criar();
                foreach (var a in antigas.Where(l => l.Definido))
                {
                    var id = mapa.TryGetValue(a.Id, out var m) ? m : a.Id;
                    var n = novas.FirstOrDefault(x => x.Id == id);
                    if (n != null) { n.X = a.X; n.Y = a.Y; n.Z = a.Z; n.Raio = a.Raio; }
                }
                cfg.Lojas = novas;
                cfg.VersaoLojas = LojasPadrao.Versao;
                mudou = true;
                Rocket.Core.Logging.Logger.Log($"SobrevivenciaCore: lojas atualizadas pra versao {LojasPadrao.Versao} ({novas.Sum(l => l.Itens.Count)} itens).");
            }
            if (!cfg.Notas.Any(n => n.Id == 7012))
            {
                cfg.Notas.Add(new Nota(7012, 10000) { SoAceita = true });
                cfg.Notas.Add(new Nota(7011, 5000) { SoAceita = true });
                cfg.Notas.Add(new Nota(7015, 1000) { SoAceita = true });
                mudou = true;
            }
            if (mudou) Configuration.Save();
        }

        // ---------- NPC: F no boneco ----------

        void AoAbrirBau(CSteamID quem, InteractableStorage bau, ref bool permitir)
        {
            try
            {
                var drop = BarricadeManager.FindBarricadeByRootTransform(bau.transform);
                if (drop?.asset == null || !EhNpc(drop.asset.id)) return;
                permitir = false;
                var sp = PlayerTool.getSteamPlayer(quem);
                if (sp?.player == null) return;
                var p = UnturnedPlayer.FromSteamPlayer(sp);
                var pos = drop.model.position;
                ushort id = drop.asset.id;
                if (id == NpcVault) { AbrirVaultNpc(p); return; }
                if (id == NpcBanco)
                {
                    Chat.Info(p, $"Banco: ${Banco.Saldo(Id(p)):N0}  |  No bolso: ${Dinheiro.NoBolso(p):N0}");
                    Chat.Para(p, Chat.Cor(S.CorApagada, "Use ") + Chat.Cor(S.CorPrincipal, "/depositar valor") + Chat.Cor(S.CorApagada, " ou ") + Chat.Cor(S.CorPrincipal, "/sacar valor") + Chat.Cor(S.CorApagada, " aqui do lado."));
                    return;
                }
                if (!NpcTipo.TryGetValue(id, out var tipo)) return;
                var loja = Configuration.Instance.Lojas
                    .Where(l => l.Id.Equals(tipo, StringComparison.OrdinalIgnoreCase) || l.Tipo.Equals(tipo, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(l => l.Definido ? Vector3.Distance(pos, new Vector3(l.X, l.Y, l.Z)) : 99999f).FirstOrDefault();
                if (loja == null) { Chat.Erro(p, "Esse vendedor ainda nao tem loja configurada."); return; }
                AbrirLoja(p, loja, pos);
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }

        void AoRecolherBarricada(CSteamID quem, byte x, byte y, ushort plant, ushort index, ref bool permitir)
        {
            if (plant != ushort.MaxValue) return;
            try
            {
                var drops = BarricadeManager.regions[x, y].drops;
                if (index >= drops.Count || !EhNpc(drops[index].asset.id)) return;
                var sp = PlayerTool.getSteamPlayer(quem);
                if (sp == null || !(sp.isAdmin || EhDono(quem.m_SteamID))) permitir = false;
            }
            catch { }
        }

        /// <summary>NPC é imortal (chamado pelo dano em barricada da safe zone).</summary>
        public static bool BarricadaEhNpc(Transform t)
        {
            var d = BarricadeManager.FindBarricadeByRootTransform(t);
            return d?.asset != null && EhNpc(d.asset.id);
        }

        // ---------- Janela ----------

        public bool AbrirLoja(UnturnedPlayer p, Loja loja, Vector3 npc)
        {
            if (!(Assets.find(EAssetType.EFFECT, LojaId) is EffectAsset asset)) { Chat.Info(p, "Use /loja pra ver os itens (janela ainda nao instalada)."); return false; }
            if (MenuAberto(p)) FecharMenu(p);
            var e = new EstadoLoja { Loja = loja, Npc = npc };
            e.Vender = !loja.Itens.Any(i => i.Compra > 0);
            lojasAbertas[Id(p)] = e;
            EffectManager.SendUIEffect(asset, LojaKey, Con(p), true);
            p.Player.enablePluginWidgetFlag(EPluginWidgetFlags.Modal);
            LT(p, "Loja_Titulo", Chat.Limpar(loja.Nome).ToUpperInvariant());
            LT(p, "Loja_Subtitulo", SubtituloLoja(loja));
            LT(p, "Loja_Aviso", "");
            bool compra = loja.Itens.Any(i => i.Compra > 0), venda = loja.Itens.Any(i => i.Venda > 0);
            LV(p, "Loja_ModoComprar", compra && venda);
            LV(p, "Loja_ModoVender", compra && venda);
            LV(p, "Loja_VenderTudo", loja.Tipo == "cacador");
            Recarregar(p, e);
            return true;
        }

        static string SubtituloLoja(Loja l)
        {
            switch (l.Tipo)
            {
                case "negro": return C(Vermelho, "Zona PvP. Itens raros e caros. Cuidado na saida!");
                case "cacador": return C(Ambar, "Zona PvP. Venda carne, couro e tudo que cacar.");
                case "garagem": return "Veiculo aparece aqui do lado, trancado pra voce.";
                default: return C(Verde, "Safe zone. Saque no ATM e pague em dinheiro.");
            }
        }

        public void FecharLoja(UnturnedPlayer p)
        {
            if (!lojasAbertas.Remove(Id(p))) return;
            EffectManager.askEffectClearByID(LojaId, Con(p));
            if (!MenuAberto(p)) p.Player.disablePluginWidgetFlag(EPluginWidgetFlags.Modal);
        }

        bool ValeNoModo(EstadoLoja e, ItemLoja i) => e.Vender ? i.Venda > 0 : i.Compra > 0;

        static string NomeDe(ItemLoja i)
            => i.Veiculo ? ((Assets.find(EAssetType.VEHICLE, i.Id) as VehicleAsset)?.vehicleName ?? ("Veiculo " + i.Id)) : Itens.Nome(i.Id);

        void Recarregar(UnturnedPlayer p, EstadoLoja e)
        {
            var validos = e.Loja.Itens.Where(i => ValeNoModo(e, i) && (i.Veiculo ? Assets.find(EAssetType.VEHICLE, i.Id) != null : Itens.Existe(i.Id))).ToList();
            e.Cats = validos.Select(i => string.IsNullOrEmpty(i.Categoria) ? "Geral" : i.Categoria).Distinct().ToList();
            if (e.Cats.Count > 8) e.Cats = e.Cats.Take(7).Concat(new[] { "Outros" }).ToList();
            e.Cat = Mathf.Clamp(e.Cat, 0, Math.Max(0, e.Cats.Count - 1));
            string cat = e.Cats.Count > 0 ? e.Cats[e.Cat] : "";
            e.Lista = validos.Where(i =>
            {
                if (e.Busca != "") return NomeDe(i).IndexOf(e.Busca, StringComparison.OrdinalIgnoreCase) >= 0;
                var c = string.IsNullOrEmpty(i.Categoria) ? "Geral" : i.Categoria;
                return cat == "Outros" ? !e.Cats.Take(7).Contains(c) : c == cat;
            }).ToList();
            for (int n = 0; n < 8; n++)
            {
                bool tem = n < e.Cats.Count;
                LV(p, "Cat_" + (n + 1), tem);
                if (!tem) continue;
                LT(p, "Cat_" + (n + 1) + "_Texto", e.Cats[n].ToUpperInvariant());
                LV(p, "Cat_" + (n + 1) + "_Sel", n == e.Cat && e.Busca == "");
            }
            LV(p, "Loja_ModoComprar_Sel", !e.Vender);
            LV(p, "Loja_ModoVender_Sel", e.Vender);
            LT(p, "Loja_Comprar_Texto", e.Vender ? "VENDER" : "COMPRAR");
            PreencherItensLoja(p, e);
            PreencherDetalhe(p, e);
            LT(p, "Loja_Dinheiro", C(Verde, "$ " + Dinheiro.NoBolso(p).ToString("N0")) + C(Cinza, " no bolso"));
        }

        void PreencherItensLoja(UnturnedPlayer p, EstadoLoja e)
        {
            int paginas = Math.Max(1, (e.Lista.Count + ItensPorPagina - 1) / ItensPorPagina);
            e.Pagina = Mathf.Clamp(e.Pagina, 0, paginas - 1);
            for (int i = 0; i < ItensPorPagina; i++)
            {
                int idx = e.Pagina * ItensPorPagina + i;
                string n = "Item_" + (i + 1);
                if (idx >= e.Lista.Count) { LV(p, n, false); continue; }
                var it = e.Lista[idx];
                LV(p, n, true);
                LT(p, n + "_Nome", Chat.Limpar(NomeDe(it)).ToUpperInvariant());
                LT(p, n + "_Preco", "$ " + (e.Vender ? it.Venda : it.Compra).ToString("N0"));
                LV(p, n + "_Sel", idx == e.Sel);
            }
            LT(p, "Itens_Pagina", $"{e.Pagina + 1}/{paginas}");
        }

        void PreencherDetalhe(UnturnedPlayer p, EstadoLoja e)
        {
            if (e.Sel < 0 || e.Sel >= e.Lista.Count)
            {
                LT(p, "Det_Nome", C(Cinza, "Escolha um item"));
                LT(p, "Det_Desc", e.Vender ? "Clique no item que quer vender." : "Clique no item que quer comprar.");
                LT(p, "Det_Preco", ""); LT(p, "Det_Total", "");
                return;
            }
            var it = e.Lista[e.Sel];
            if (it.Veiculo) e.Qtd = 1;
            long preco = e.Vender ? it.Venda : it.Compra;
            LT(p, "Det_Nome", Chat.Limpar(NomeDe(it)).ToUpperInvariant());
            string desc = it.Veiculo ? "Veiculo. Aparece aqui do lado, trancado pra voce."
                : (Assets.find(EAssetType.ITEM, it.Id) as ItemAsset)?.itemDescription ?? "";
            if (e.Vender) desc = $"Voce tem {Itens.Contar(p, it.Id)}. " + desc;
            LT(p, "Det_Desc", Chat.Limpar(desc.Length > 160 ? desc.Substring(0, 160) + "..." : desc));
            LT(p, "Det_Preco", "$ " + preco.ToString("N0") + C(Cinza, " cada"));
            long total = preco * e.Qtd;
            bool da = e.Vender || Dinheiro.NoBolso(p) >= total;
            LT(p, "Det_Total", C(da ? Verde : Vermelho, $"{e.Qtd}x = $ {total:N0}"));
            LT(p, "Loja_Qtd", e.Qtd.ToString());
        }

        void AvisoLoja(UnturnedPlayer p, string msg, bool ok) => LT(p, "Loja_Aviso", C(ok ? Verde : Vermelho, msg));

        // ---------- Botões e campos ----------

        bool CliqueLoja(UnturnedPlayer p, string botao)
        {
            if (!lojasAbertas.TryGetValue(Id(p), out var e)) return false;
            if (botao.StartsWith("Cat_") && int.TryParse(botao.Substring(4), out var nc))
            { e.Cat = nc - 1; e.Busca = ""; e.Pagina = 0; e.Sel = -1; Recarregar(p, e); return true; }
            if (botao.StartsWith("Item_") && int.TryParse(botao.Substring(5), out var ni))
            {
                int idx = e.Pagina * ItensPorPagina + ni - 1;
                if (idx < e.Lista.Count) { e.Sel = idx; e.Qtd = 1; PreencherItensLoja(p, e); PreencherDetalhe(p, e); }
                return true;
            }
            switch (botao)
            {
                case "Loja_Fechar": FecharLoja(p); return true;
                case "Loja_ModoComprar": e.Vender = false; e.Cat = 0; e.Sel = -1; e.Pagina = 0; Recarregar(p, e); return true;
                case "Loja_ModoVender": e.Vender = true; e.Cat = 0; e.Sel = -1; e.Pagina = 0; Recarregar(p, e); return true;
                case "Itens_Anterior": e.Pagina--; PreencherItensLoja(p, e); return true;
                case "Itens_Proximo": e.Pagina++; PreencherItensLoja(p, e); return true;
                case "Qtd_Menos": e.Qtd = Math.Max(1, e.Qtd - 1); PreencherDetalhe(p, e); return true;
                case "Qtd_Mais": e.Qtd = Math.Min(e.Vender ? 250 : 50, e.Qtd + 1); PreencherDetalhe(p, e); return true;
                case "Loja_Comprar": if (e.Vender) VenderItem(p, e); else ComprarItem(p, e); return true;
                case "Loja_VenderTudo": VenderTudoLoja(p, e); return true;
            }
            return false;
        }

        bool DigitouLoja(UnturnedPlayer p, string campo, string texto)
        {
            if (!lojasAbertas.TryGetValue(Id(p), out var e)) return false;
            texto = (texto ?? "").Trim();
            if (campo == "Loja_Busca") { e.Busca = texto; e.Pagina = 0; e.Sel = -1; Recarregar(p, e); return true; }
            if (campo == "Loja_Qtd") { e.Qtd = int.TryParse(texto, out var q) ? Mathf.Clamp(q, 1, e.Vender ? 250 : 50) : 1; PreencherDetalhe(p, e); return true; }
            return false;
        }

        bool PertoDoNpc(UnturnedPlayer p, EstadoLoja e)
        {
            if (Vector3.Distance(p.Position, e.Npc) <= 8f && !p.Dead) return true;
            AvisoLoja(p, "Chegue perto do vendedor.", false);
            return false;
        }

        void ComprarItem(UnturnedPlayer p, EstadoLoja e)
        {
            if (e.Sel < 0 || e.Sel >= e.Lista.Count) { AvisoLoja(p, "Escolha um item.", false); return; }
            if (!PertoDoNpc(p, e)) return;
            var it = e.Lista[e.Sel];
            if (it.Compra <= 0) return;
            int qtd = it.Veiculo ? 1 : e.Qtd;
            long preco = (long)it.Compra * qtd;
            long bolso = Dinheiro.NoBolso(p);
            if (bolso < preco) { AvisoLoja(p, $"Dinheiro insuficiente: custa $ {preco:N0}, voce tem $ {bolso:N0} no bolso. Saque no ATM da safe.", false); return; }
            if (it.Veiculo)
            {
                var asset = Assets.find(EAssetType.VEHICLE, it.Id) as VehicleAsset;
                if (asset == null) { AvisoLoja(p, "Veiculo invalido na configuracao.", false); return; }
                Vector3 onde = e.Loja.VX != 0 || e.Loja.VZ != 0 ? new Vector3(e.Loja.VX, e.Loja.VY, e.Loja.VZ)
                    : p.Position + p.Player.transform.forward * 8f;
                if (!Dinheiro.Pagar(p, preco)) { AvisoLoja(p, "Dinheiro insuficiente.", false); return; }
                VehicleManager.spawnLockedVehicleForPlayerV2(asset, onde + Vector3.up * 3f, Quaternion.Euler(0, p.Player.transform.eulerAngles.y, 0), p.Player);
                Rocket.Core.Logging.Logger.Log($"[LOJA] {p.CharacterName} comprou veiculo {asset.vehicleName} por {preco}");
            }
            else
            {
                if (!Dinheiro.Pagar(p, preco)) { AvisoLoja(p, "Dinheiro insuficiente.", false); return; }
                Itens.Dar(p, it.Id, qtd);
            }
            AvisoLoja(p, $"Comprou {qtd}x {Chat.Limpar(NomeDe(it))} por $ {preco:N0}. Sobrou $ {bolso - preco:N0} no bolso.", true);
            LT(p, "Loja_Dinheiro", C(Verde, "$ " + Dinheiro.NoBolso(p).ToString("N0")) + C(Cinza, " no bolso"));
            PreencherDetalhe(p, e);
        }

        void VenderItem(UnturnedPlayer p, EstadoLoja e)
        {
            if (e.Sel < 0 || e.Sel >= e.Lista.Count) { AvisoLoja(p, "Escolha um item.", false); return; }
            if (!PertoDoNpc(p, e)) return;
            var it = e.Lista[e.Sel];
            if (it.Venda <= 0 || it.Veiculo) return;
            int tem = Itens.Contar(p, it.Id);
            if (tem == 0) { AvisoLoja(p, $"Voce nao tem {Chat.Limpar(NomeDe(it))}.", false); return; }
            int r = Itens.Remover(p, it.Id, Math.Min(tem, e.Qtd));
            long ganho = (long)it.Venda * r;
            Dinheiro.Dar(p, ganho);
            AvisoLoja(p, $"Vendeu {r}x {Chat.Limpar(NomeDe(it))} por $ {ganho:N0}.", true);
            LT(p, "Loja_Dinheiro", C(Verde, "$ " + Dinheiro.NoBolso(p).ToString("N0")) + C(Cinza, " no bolso"));
            PreencherDetalhe(p, e);
        }

        void VenderTudoLoja(UnturnedPlayer p, EstadoLoja e)
        {
            if (!PertoDoNpc(p, e)) return;
            long total = 0; int qtd = 0;
            foreach (var it in e.Loja.Itens.Where(i => i.Venda > 0 && i.Compra == 0 && !i.Veiculo))
            {
                int r = Itens.Remover(p, it.Id, Itens.Contar(p, it.Id));
                qtd += r; total += (long)r * it.Venda;
            }
            if (qtd == 0) { AvisoLoja(p, "Voce nao tem nada que esse vendedor compre.", false); return; }
            Dinheiro.Dar(p, total);
            AvisoLoja(p, $"Vendeu {qtd} itens por $ {total:N0}.", true);
            LT(p, "Loja_Dinheiro", C(Verde, "$ " + Dinheiro.NoBolso(p).ToString("N0")) + C(Cinza, " no bolso"));
        }

        // ---------- Vault comprado com dinheiro ----------

        void CarregarVaultsComprados()
        {
            vaultsComprados.Clear();
            if (!File.Exists(ArqVaults)) return;
            foreach (var l in File.ReadAllLines(ArqVaults))
            {
                var c = l.Split('\t');
                if (c.Length < 2 || !ulong.TryParse(c[0], out var id)) continue;
                if (!vaultsComprados.TryGetValue(id, out var s)) vaultsComprados[id] = s = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                s.Add(c[1]);
            }
        }

        public bool TemVault(UnturnedPlayer p, TamanhoVault v)
            => TemPermissao(p, v.Permissao) || (vaultsComprados.TryGetValue(Id(p), out var s) && s.Contains(v.Nome));

        public TamanhoVault MaiorVault(UnturnedPlayer p)
            => S.Vaults.Where(v => TemVault(p, v)).OrderByDescending(v => v.Largura * v.Altura).FirstOrDefault();

        public bool ComprarVault(UnturnedPlayer p, TamanhoVault v, out string msg)
        {
            if (TemVault(p, v)) { msg = "Voce ja tem esse cofre."; return false; }
            if (v.Preco <= 0) { msg = "Esse cofre e so pra VIP."; return false; }
            if (ZonaSeguraEm(p.Position) == null) { msg = "Compre o cofre na safe zone, no NPC Vault."; return false; }
            long bolso = Dinheiro.NoBolso(p);
            if (bolso < v.Preco || !Dinheiro.Pagar(p, v.Preco)) { msg = $"Custa $ {v.Preco:N0}. Voce tem $ {bolso:N0} no bolso."; return false; }
            if (!vaultsComprados.TryGetValue(Id(p), out var s)) vaultsComprados[Id(p)] = s = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            s.Add(v.Nome);
            try { File.AppendAllText(ArqVaults, Id(p) + "\t" + v.Nome + Environment.NewLine); } catch { }
            msg = $"Voce comprou o {v.Nome} ({v.Largura}x{v.Altura})!";
            return true;
        }

        void AbrirVaultNpc(UnturnedPlayer p)
        {
            // abre o menu na aba Vaults (ver, abrir e comprar); sem o mod, abre o maior cofre direto
            if (MenuDisponivel && AbrirMenu(p, "Vaults")) return;
            var tam = MaiorVault(p);
            if (tam != null) CmdVault.Abrir(this, S, p, tam);
        }
    }
}
