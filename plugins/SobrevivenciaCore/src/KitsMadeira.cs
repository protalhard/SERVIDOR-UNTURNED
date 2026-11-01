using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SDG.Unturned;

namespace SobrevivenciaCore
{
    /// <summary>Construção forte (metal, tijolo, cofre, tranca) só nos kits pagos e rara no airdrop.
    /// Kit grátis leva só madeira: a peça de metal vira a de madeira do mesmo tipo, ou sai do kit.</summary>
    public partial class Plugin
    {
        public const int VersaoKits = 1;
        const double ChanceForteAirdrop = 0.12;

        static readonly Regex NomeForte = new Regex(@"\b(metal|steel|iron|brick|concrete|stone|sandstone|scrap|adobe|glass|bulletproof|armou?red|reinforced|vault|safe|locker|lock|padlock|keypad|jail|bars|roadblock|tanktrap)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex NomeMadeira = new Regex(@"\b(maple|birch|pine|wood|wooden|plank|log|twig|crate)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly string[] Madeiras = { "Maple", "Wooden", "Wood", "Birch", "Pine" };

        static bool EhConstrucao(ItemAsset a) => a is ItemStructureAsset || a is ItemBarricadeAsset;

        /// <summary>Peça de construção de metal, tijolo, cofre ou tranca.</summary>
        static bool EhForte(ItemAsset a) => a != null && EhConstrucao(a) && NomeForte.IsMatch(a.itemName ?? "");

        /// <summary>A mesma peça em madeira: primeiro pelo nome (Metal Wall -> Maple Wall), senão do mesmo tipo de encaixe.</summary>
        ItemAsset VersaoMadeira(ItemAsset forte, List<ItemAsset> todos)
        {
            var nome = (forte.itemName ?? "").Trim();
            var porNome = todos.Where(a => EhConstrucao(a) && !EhForte(a) && a.GetType() == forte.GetType())
                .GroupBy(a => (a.itemName ?? "").Trim().ToLowerInvariant()).ToDictionary(g => g.Key, g => g.OrderBy(a => a.id).First());
            var sem = Regex.Replace(nome, @"\b(metal|steel|iron|brick|concrete|sandstone|scrap|adobe|reinforced)\b", "{M}", RegexOptions.IgnoreCase);
            if (sem.Contains("{M}"))
                foreach (var m in Madeiras)
                    if (porNome.TryGetValue(Regex.Replace(sem.Replace("{M}", m), @"\s+", " ").Trim().ToLowerInvariant(), out var achou)) return achou;

            bool MesmoEncaixe(ItemAsset a)
            {
                if (forte is ItemStructureAsset fs) return a is ItemStructureAsset s && s.construct == fs.construct;
                if (forte is ItemStorageAsset) return a is ItemStorageAsset;
                if (forte is ItemBarricadeAsset fb) return a is ItemBarricadeAsset b && !(a is ItemStorageAsset) && b.build == fb.build;
                return false;
            }
            return todos.Where(a => EhConstrucao(a) && !EhForte(a) && a.id < 51000 && NomeMadeira.IsMatch(a.itemName ?? "") && !NomeProibido.IsMatch(a.itemName ?? "") && MesmoEncaixe(a))
                .OrderBy(a => a.id).FirstOrDefault();
        }

        /// <summary>Tábuas de madeira (material pra construir), no lugar de chapa ou sucata de metal.</summary>
        static ItemAsset Tabuas(List<ItemAsset> todos) =>
            todos.Where(a => a is ItemSupplyAsset && Regex.IsMatch(a.itemName ?? "", @"\bplanks?\b", RegexOptions.IgnoreCase))
                .OrderBy(a => (a.itemName ?? "").StartsWith("Maple", StringComparison.OrdinalIgnoreCase) ? 0 : 1).ThenBy(a => a.id).FirstOrDefault();

        /// <summary>Roda uma vez depois que os itens do mapa carregam (versão salva no config).</summary>
        void AjustarKitsMadeira()
        {
            var cfg = Configuration.Instance;
            if (cfg.VersaoKits >= VersaoKits || S.Kits == null) return;
            var todos = new List<ItemAsset>();
            Assets.find(todos);
            todos = todos.Where(a => a != null && a.id != 0).ToList();
            if (!todos.Any(a => a.id >= 4000 && a.id < 51000)) return; // itens do California 2 ainda não carregaram

            foreach (var k in S.Kits)
            {
                if (!string.IsNullOrWhiteSpace(k.Permissao) || k.Itens == null) continue; // pago (ou com permissão): pode ter metal
                var novos = new List<ItemKit>();
                foreach (var it in k.Itens)
                {
                    var a = Assets.find(EAssetType.ITEM, it.Id) as ItemAsset;
                    bool materialMetal = a is ItemSupplyAsset && Regex.IsMatch(a.itemName ?? "", @"\b(metal|scrap|steel|iron)\b", RegexOptions.IgnoreCase);
                    if (!EhForte(a) && !materialMetal) { novos.Add(it); continue; }
                    var mad = materialMetal ? Tabuas(todos) : VersaoMadeira(a, todos);
                    if (materialMetal && k.Descricao != null) k.Descricao = k.Descricao.Replace("chapas", "tabuas");
                    if (mad != null)
                    {
                        var igual = novos.FirstOrDefault(x => x.Id == mad.id);
                        if (igual != null) igual.Quantidade += it.Quantidade; else novos.Add(new ItemKit(mad.id, it.Quantidade));
                    }
                    Rocket.Core.Logging.Logger.Log($"[Kits] kit gratis {k.Nome}: {a.itemName} ({a.id}) -> " + (mad == null ? "removido" : $"{mad.itemName} ({mad.id})"));
                }
                k.Itens = novos;
                if (k.Descricao != null) k.Descricao = Regex.Replace(k.Descricao, @"\bde metal\b", "de madeira", RegexOptions.IgnoreCase);
                if (k.Nome == "base" && k.Espera < 14400) k.Espera = 14400; // pedido: kit de base a cada 4 horas
            }
            cfg.VersaoKits = VersaoKits;
            Configuration.Save();
            Rocket.Core.Logging.Logger.Log("SobrevivenciaCore: kits gratis so com madeira (v1).");
        }
    }
}
