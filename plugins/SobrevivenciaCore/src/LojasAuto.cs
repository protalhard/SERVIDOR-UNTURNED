using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SDG.Unturned;

namespace SobrevivenciaCore
{
    /// <summary>Completa as lojas com TODOS os itens que existem no servidor (jogo base + mods), pra comprar e vender.
    /// Os comuns vão pras lojas da safe; os apelões (armas raras, lançadores, explosivos, torretas) só no mercado negro.
    /// Itens que já estão em alguma loja ficam como estão (preço editado não muda). Roda uma vez por versão do catálogo.</summary>
    public partial class Plugin
    {
        public const int VersaoCatalogo = 3;

        // raridade a partir da qual a arma vai pro mercado negro
        const EItemRarity RaridadeNegro = EItemRarity.EPIC;

        static readonly Regex NomeProibido = new Regex(@"\b(bill|dollar|money|cash|coin|nota|dinheiro|skin|debug|admin|test|placeholder)\b" +
            @"|^CA_|sentry|cannon|steering|_jam|\bHMG\b|air bomb|focus lense", RegexOptions.IgnoreCase);

        static int Preco(EItemRarity r, params int[] tabela) => tabela[Math.Min((int)r, tabela.Length - 1)];

        /// <summary>Loja, categoria e preço de um item. null = não vende.</summary>
        (string loja, string cat, int preco)? Classificar(ItemAsset a)
        {
            var r = a.rarity;
            switch (a)
            {
                case ItemGunAsset g:
                {
                    bool apelao = r >= RaridadeNegro || g.action == EAction.Rocket || g.action == EAction.Minigun;
                    string cat = g.action == EAction.Rocket ? "Lancadores"
                        : g.action == EAction.Minigun ? "Metralhadoras"
                        : g.action == EAction.Pump || g.action == EAction.Break ? "Escopetas"
                        : g.action == EAction.String ? "Arcos"
                        : g.action == EAction.Bolt ? (apelao ? "Snipers" : "Rifles")
                        : g.slot == ESlotType.SECONDARY ? "Pistolas"
                        : apelao ? "Rifles fortes" : "Rifles";
                    return apelao ? ("negro", cat, Preco(r, 3000, 5000, 8000, 12000, 20000, 30000))
                                  : ("armas", cat, Preco(r, 400, 900, 1800, 3500));
                }
                case ItemMagazineAsset m:
                    return m.isExplosive || r >= RaridadeNegro ? ("negro", "Municao rara", Preco(r, 150, 200, 300, 400, 600, 800))
                                                               : ("armas", "Municao", Preco(r, 25, 40, 70, 120));
                case ItemSightAsset _:
                case ItemGripAsset _:
                case ItemBarrelAsset _:
                case ItemTacticalAsset _:
                    return ("armas", "Acessorios", Preco(r, 100, 200, 400, 800, 1500, 2500));
                case ItemMeleeAsset _:
                    return ("armas", "Corpo a corpo", Preco(r, 50, 100, 200, 400, 800, 1500));
                case ItemChargeAsset _:
                    return ("negro", "Explosivos", Preco(r, 1500, 2500, 4000, 6000, 10000, 15000));
                case ItemThrowableAsset t:
                    return t.isExplosive ? ("negro", "Granadas", Preco(r, 600, 900, 1500, 2500, 4000, 6000))
                                         : ("equipamentos", "Ferramentas", Preco(r, 40, 80, 150, 300));
                case ItemSentryAsset _:
                    return ("negro", "Torres", Preco(r, 3000, 5000, 8000, 12000, 20000, 30000));
                case ItemTrapAsset tr:
                    return tr.isExplosive ? ("negro", "Explosivos", Preco(r, 800, 1200, 2000, 3000, 5000))
                                          : ("construcao", "Armadilhas", Preco(r, 40, 80, 150, 300));
                case ItemBackpackAsset _:
                    return ("equipamentos", "Mochilas", Preco(r, 100, 250, 600, 1200, 2000, 3000));
                case ItemVestAsset _:
                    return ("equipamentos", "Coletes", Preco(r, 80, 200, 500, 1000, 1800, 3000));
                case ItemShirtAsset _:
                    return ("roupas", "Camisas", Preco(r, 40, 100, 250, 600, 1200, 2000));
                case ItemPantsAsset _:
                    return ("roupas", "Calcas", Preco(r, 40, 100, 250, 600, 1200, 2000));
                case ItemHatAsset _:
                    return ("roupas", "Chapeus e capacetes", Preco(r, 30, 80, 200, 500, 1000, 1800));
                case ItemMaskAsset _:
                    return ("roupas", "Mascaras", Preco(r, 30, 80, 200, 500, 1000, 1800));
                case ItemGlassesAsset _:
                    return ("roupas", "Oculos", Preco(r, 30, 80, 200, 500, 1000, 1800));
                case ItemMedicalAsset _:
                    return ("remedios", "Remedios", Preco(r, 10, 30, 80, 200, 500, 1000));
                case ItemWaterAsset _:
                    return ("comida", "Bebidas", Preco(r, 10, 20, 40, 80, 150));
                case ItemFoodAsset _:
                    return ("comida", "Comida", Preco(r, 10, 20, 40, 80, 150));
                case ItemFuelAsset _:
                case ItemRefillAsset _:
                case ItemToolAsset _:
                case ItemFisherAsset _:
                case ItemOpticAsset _:
                case ItemDetonatorAsset _:
                case ItemArrestStartAsset _:
                case ItemArrestEndAsset _:
                case ItemFilterAsset _:
                case ItemMapAsset _:
                    return ("equipamentos", "Ferramentas", Preco(r, 50, 100, 250, 500, 1000, 2000));
                case ItemGrowerAsset _:
                case ItemFarmAsset _:
                    return ("equipamentos", "Fazenda", Preco(r, 20, 40, 80, 150));
                case ItemSupplyAsset _:
                    return ("equipamentos", "Materiais", Preco(r, 10, 25, 60, 120));
                case ItemStorageAsset _:
                    return ("construcao", "Bau e armazem", Preco(r, 150, 300, 600, 1200, 2000));
                case ItemGeneratorAsset _:
                    return ("construcao", "Energia e luz", Preco(r, 300, 600, 1200, 2000));
                case ItemStructureAsset st:
                {
                    string cat;
                    switch (st.construct)
                    {
                        case EConstruct.FLOOR: case EConstruct.FLOOR_POLY: cat = "Pisos"; break;
                        case EConstruct.ROOF: case EConstruct.ROOF_POLY: cat = "Telhados"; break;
                        case EConstruct.PILLAR: case EConstruct.POST: cat = "Pilares"; break;
                        default: cat = "Paredes"; break;
                    }
                    return ("construcao", cat, Preco(r, 40, 80, 160, 300, 500));
                }
                case ItemBarricadeAsset b:
                {
                    string cat;
                    switch (b.build)
                    {
                        case EBuild.DOOR: case EBuild.GATE: case EBuild.HATCH: case EBuild.SHUTTER: case EBuild.GLASS: cat = "Portas e janelas"; break;
                        case EBuild.LADDER: cat = "Escadas e rampas"; break;
                        case EBuild.FORTIFICATION: case EBuild.BARRICADE: cat = "Muros e portoes"; break;
                        case EBuild.WIRE: case EBuild.SPIKE: cat = "Cercas"; break;
                        case EBuild.TORCH: case EBuild.CAMPFIRE: case EBuild.SPOT: case EBuild.OIL: cat = "Energia e luz"; break;
                        case EBuild.CLAIM: cat = "Base"; break;
                        case EBuild.SAFEZONE: case EBuild.BEACON: case EBuild.VEHICLE: case EBuild.CHARGE: case EBuild.MANNEQUIN: case EBuild.LIBRARY: return null;
                        default: cat = "Moveis"; break;
                    }
                    return ("construcao", cat, Preco(r, 30, 60, 120, 250, 500));
                }
            }
            return null;
        }

        void CompletarLojasDepois()
        {
            try { CompletarLojas(); } catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }

        /// <summary>Põe nas lojas todo item do servidor que ainda não está em nenhuma. Devolve quantos entraram.</summary>
        public int CompletarLojas(bool forcar = false)
        {
            var cfg = Configuration.Instance;
            if (!forcar && cfg.VersaoCatalogo >= VersaoCatalogo) return 0;
            var todos = new List<ItemAsset>();
            Assets.find(todos);
            if (todos.Count < 100) return 0; // itens ainda não carregaram

            var notas = new HashSet<ushort>((cfg.Notas ?? new List<Nota>()).Select(n => n.Id));
            var jaTem = new HashSet<ushort>(cfg.Lojas.SelectMany(l => l.Itens).Where(i => !i.Veiculo).Select(i => i.Id));
            var nomes = new HashSet<string>(jaTem.Select(Itens.Nome).Select(n => n.Trim().ToLowerInvariant()));
            int n0 = 0;
            var porLoja = new Dictionary<string, int>();
            foreach (var a in todos.OrderBy(x => x.id < 4000 ? 1 : 0).ThenBy(x => x.id)) // mods (California 2) antes do jogo base
            {
                if (a == null || a.id == 0 || jaTem.Contains(a.id) || notas.Contains(a.id) || a.isPro) continue;
                var nome = (a.itemName ?? "").Trim();
                if (nome.Length < 2 || NomeProibido.IsMatch(nome)) continue;
                if (a is ItemBoxAsset || a is ItemKeyAsset) continue;
                var c = Classificar(a);
                if (c == null) continue;
                var chave = nome.ToLowerInvariant();
                if (nomes.Contains(chave)) continue; // mesmo nome já vendido (versão do mod ou do jogo base)
                var loja = cfg.Lojas.FirstOrDefault(l => l.Id == c.Value.loja);
                if (loja == null) continue;
                int compra = c.Value.preco;
                loja.Itens.Add(new ItemLoja(a.id, compra, Math.Max(1, compra * 3 / 10)) { Categoria = c.Value.cat });
                jaTem.Add(a.id); nomes.Add(chave);
                porLoja[loja.Id] = porLoja.TryGetValue(loja.Id, out var k) ? k + 1 : 1;
                n0++;
            }
            // arma apelona (épica ou mais) que estava na loja de armas da safe vai pro mercado negro
            var armas = cfg.Lojas.FirstOrDefault(l => l.Id == "armas");
            var negro = cfg.Lojas.FirstOrDefault(l => l.Id == "negro");
            if (armas != null && negro != null)
                foreach (var i in armas.Itens.ToList())
                {
                    if (i.Veiculo) continue;
                    var a = Assets.find(EAssetType.ITEM, i.Id) as ItemAsset ?? todos.FirstOrDefault(x => x != null && x.id == i.Id);
                    if (a == null) { Rocket.Core.Logging.Logger.Log($"SobrevivenciaCore: arma {i.Id} sem asset"); continue; }
                    bool arma = a is ItemGunAsset || a.type == EItemType.GUN;
                    var c = a is ItemGunAsset g ? Classificar(g) : null;
                    bool apelao = arma && (a.rarity >= RaridadeNegro || (c != null && c.Value.loja == "negro"));
                    Rocket.Core.Logging.Logger.Log($"SobrevivenciaCore: arma {i.Id} {a.itemName} {a.GetType().Name} {a.type} {a.rarity} -> {(apelao ? "negro" : "armas")}");
                    if (!apelao) continue;
                    if (negro.Itens.Any(x => !x.Veiculo && x.Id == i.Id)) { armas.Itens.Remove(i); continue; }
                    if (c == null) c = ("negro", i.Categoria == "Pistolas" ? "Pistolas" : "Rifles fortes", Preco(a.rarity, 3000, 5000, 8000, 12000, 20000, 30000));
                    armas.Itens.Remove(i);
                    negro.Itens.Add(new ItemLoja(i.Id, Math.Max(i.Compra, c.Value.preco), 0) { Categoria = c.Value.cat });
                    n0++;
                    porLoja["negro"] = porLoja.TryGetValue("negro", out var k2) ? k2 + 1 : 1;
                }
            // tudo que a safe vende dá pra vender de volta (30%)
            foreach (var l in cfg.Lojas.Where(l => l.Tipo == "safe"))
                foreach (var i in l.Itens.Where(i => !i.Veiculo && i.Venda <= 0 && i.Compra > 0))
                    i.Venda = Math.Max(1, i.Compra * 3 / 10);
            cfg.VersaoCatalogo = VersaoCatalogo;
            Configuration.Save();
            Rocket.Core.Logging.Logger.Log($"SobrevivenciaCore: catalogo completo, {n0} itens novos nas lojas (" +
                string.Join(", ", porLoja.Select(kv => kv.Key + " " + kv.Value)) + ").");
            return n0;
        }
    }
}
