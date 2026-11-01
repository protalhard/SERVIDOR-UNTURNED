using System;
using System.Collections.Generic;
using System.Linq;
using SDG.Unturned;

namespace SobrevivenciaCore
{
    /// <summary>Conteúdo sorteado do airdrop: o admin escolhe só o tipo (Comida, Armas, Equipamentos, Base, Completo)
    /// e cada caixa vem com itens diferentes daquele tipo, pesados pela raridade. Arma sempre vem com o carregador dela.</summary>
    public partial class Plugin
    {
        static readonly Random sorte = new Random();
        Dictionary<string, List<ItemAsset>> poolsDrop;

        static int PesoRaridade(EItemRarity r)
        {
            switch (r)
            {
                case EItemRarity.COMMON: return 40;
                case EItemRarity.UNCOMMON: return 25;
                case EItemRarity.RARE: return 15;
                case EItemRarity.EPIC: return 7;
                case EItemRarity.LEGENDARY: return 3;
                default: return 1;
            }
        }

        Dictionary<string, List<ItemAsset>> PoolsDrop()
        {
            if (poolsDrop != null && poolsDrop["arma"].Count > 0) return poolsDrop;
            var notas = new HashSet<ushort>((Configuration.Instance.Notas ?? new List<Nota>()).Select(n => n.Id));
            var todos = new List<ItemAsset>();
            Assets.find(todos);
            var p = new Dictionary<string, List<ItemAsset>>();
            foreach (var k in new[] { "comida", "bebida", "remedio", "arma", "acessorio", "granada", "mochila", "colete", "chapeu", "ferramenta", "construcao", "bau", "energia" })
                p[k] = new List<ItemAsset>();
            foreach (var a in todos)
            {
                if (a == null || a.id == 0 || a.id >= 51000 || a.isPro || notas.Contains(a.id)) continue;
                var nome = (a.itemName ?? "").Trim();
                if (nome.Length < 2 || NomeProibido.IsMatch(nome)) continue;
                switch (a)
                {
                    case ItemGunAsset g when g.action != EAction.Rocket && g.action != EAction.Minigun: p["arma"].Add(a); break;
                    case ItemSightAsset _: case ItemGripAsset _: case ItemBarrelAsset _: case ItemTacticalAsset _: p["acessorio"].Add(a); break;
                    case ItemThrowableAsset _: p["granada"].Add(a); break;
                    case ItemBackpackAsset _: p["mochila"].Add(a); break;
                    case ItemVestAsset _: p["colete"].Add(a); break;
                    case ItemHatAsset _: p["chapeu"].Add(a); break;
                    case ItemMedicalAsset _: p["remedio"].Add(a); break;
                    case ItemWaterAsset _: p["bebida"].Add(a); break;
                    case ItemFoodAsset _: p["comida"].Add(a); break;
                    case ItemFuelAsset _: case ItemRefillAsset _: case ItemToolAsset _: case ItemFisherAsset _: case ItemOpticAsset _: p["ferramenta"].Add(a); break;
                    case ItemStorageAsset _: p["bau"].Add(a); break;
                    case ItemGeneratorAsset _: p["energia"].Add(a); break;
                    case ItemStructureAsset _: p["construcao"].Add(a); break;
                    case ItemBarricadeAsset b when b.build == EBuild.DOOR || b.build == EBuild.GATE || b.build == EBuild.HATCH
                        || b.build == EBuild.FORTIFICATION || b.build == EBuild.BARRICADE || b.build == EBuild.WIRE || b.build == EBuild.LADDER
                        || b.build == EBuild.CLAIM || b.build == EBuild.SPOT:
                        p["construcao"].Add(a); break;
                }
            }
            return poolsDrop = p;
        }

        ItemAsset Sortear(string pool)
        {
            var l = PoolsDrop()[pool];
            if (l.Count == 0) return null;
            int total = l.Sum(a => PesoRaridade(a.rarity)), x = sorte.Next(total);
            foreach (var a in l) { x -= PesoRaridade(a.rarity); if (x < 0) return a; }
            return l[l.Count - 1];
        }

        /// <summary>Carregador que serve na arma (o padrão dela, senão um do mesmo calibre).</summary>
        ushort CarregadorDe(ItemGunAsset g)
        {
            var padrao = g.getMagazineID();
            if (padrao != 0 && Itens.Existe(padrao)) return padrao;
            var todos = new List<ItemMagazineAsset>();
            Assets.find(todos);
            var serve = todos.Where(m => m != null && m.calibers != null && g.magazineCalibers != null && m.calibers.Any(c => g.magazineCalibers.Contains(c))).ToList();
            return serve.Count == 0 ? (ushort)0 : serve[sorte.Next(serve.Count)].id;
        }

        static string TipoDrop(string nome)
        {
            var n = (nome ?? "").ToLowerInvariant();
            if (n.Contains("complet")) return "completo";
            if (n.Contains("arma")) return "armas";
            if (n.Contains("comida") || n.Contains("aliment")) return "comida";
            if (n.Contains("medic") || n.Contains("remed")) return "medico";
            if (n.Contains("equip")) return "equipamentos";
            if (n.Contains("base") || n.Contains("constru")) return "base";
            return null;
        }

        /// <summary>Monta a caixa do airdrop. Tipo conhecido = sorteado; senão usa a lista fixa da config.</summary>
        List<ItemKit> ConteudoAirdrop(KitAirdrop kit)
        {
            var tipo = TipoDrop(kit?.Nome);
            if (tipo == null) return kit?.Itens ?? new List<ItemKit>();
            var r = new List<ItemKit>();
            void Por(string pool, int min, int max)
            {
                var a = Sortear(pool);
                if (a != null) r.Add(new ItemKit(a.id, sorte.Next(min, max + 1)));
            }
            void Arma(int pentes)
            {
                if (!(Sortear("arma") is ItemGunAsset g)) return;
                r.Add(new ItemKit(g.id));
                var mag = CarregadorDe(g);
                if (mag != 0) r.Add(new ItemKit(mag, pentes));
            }
            switch (tipo)
            {
                case "armas":
                    Arma(sorte.Next(3, 5)); Arma(sorte.Next(2, 4));
                    if (sorte.Next(2) == 0) Arma(2);
                    Por("acessorio", 1, 1); Por("acessorio", 1, 1);
                    if (sorte.Next(2) == 0) Por("granada", 1, 2);
                    break;
                case "comida":
                    for (int i = 0; i < 4; i++) Por("comida", 1, 3);
                    for (int i = 0; i < 3; i++) Por("bebida", 1, 2);
                    Por("remedio", 1, 2);
                    break;
                case "medico":
                    for (int i = 0; i < 5; i++) Por("remedio", 1, 3);
                    Por("bebida", 1, 2);
                    break;
                case "equipamentos":
                    Por("mochila", 1, 1); Por("colete", 1, 1); Por("chapeu", 1, 1);
                    Por("ferramenta", 1, 1); Por("ferramenta", 1, 1); Por("remedio", 1, 2);
                    break;
                case "base":
                    for (int i = 0; i < 5; i++) Por("construcao", 2, 6);
                    Por("bau", 1, 2);
                    if (sorte.Next(2) == 0) Por("energia", 1, 1);
                    break;
                default: // completo
                    Arma(sorte.Next(2, 4));
                    if (sorte.Next(2) == 0) Por("mochila", 1, 1); else Por("colete", 1, 1);
                    Por("comida", 1, 3); Por("comida", 1, 2); Por("bebida", 1, 2);
                    Por("remedio", 1, 2); Por("remedio", 1, 2);
                    break;
            }
            return r;
        }

        static string DescreverDrop(KitAirdrop kit)
        {
            switch (TipoDrop(kit?.Nome))
            {
                case "armas": return "sorteado: 2 ou 3 armas com carregadores, acessorios e as vezes granada";
                case "comida": return "sorteado: comida, bebida e remedio";
                case "medico": return "sorteado: remedios e bebida";
                case "equipamentos": return "sorteado: mochila, colete, capacete, ferramentas e remedio";
                case "base": return "sorteado: pecas de construcao, bau e as vezes gerador";
                case "completo": return "sorteado: arma com carregadores, mochila ou colete, comida e remedio";
            }
            return ListaItens(kit?.Itens);
        }

        /// <summary>Ícone do kit: a arma dele, senão o primeiro item que não é comida, remédio ou munição.</summary>
        string IconeKit(List<ItemKit> itens)
        {
            if (itens == null || itens.Count == 0 || string.IsNullOrEmpty(S.IconeUrl)) return null;
            var assets = itens.Select(i => Assets.find(EAssetType.ITEM, i.Id) as ItemAsset).Where(a => a != null).ToList();
            var principal = assets.FirstOrDefault(a => a is ItemGunAsset)
                ?? assets.FirstOrDefault(a => !(a is ItemFoodAsset || a is ItemWaterAsset || a is ItemMedicalAsset || a is ItemMagazineAsset || a is ItemSupplyAsset))
                ?? assets.FirstOrDefault();
            return principal == null ? null : S.IconeUrl.Replace("{id}", principal.id.ToString());
        }
    }
}
