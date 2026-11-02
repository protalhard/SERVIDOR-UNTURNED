using System;
using System.Collections.Generic;
using System.Linq;
using SDG.Unturned;
using UnityEngine;
using Random = System.Random;

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
            foreach (var k in new[] { "comida", "bebida", "remedio", "arma", "acessorio", "granada", "mochila", "colete", "chapeu", "ferramenta", "construcao", "construcao_forte", "bau", "bau_forte", "energia", "arma_boa" })
                p[k] = new List<ItemAsset>();
            foreach (var a in todos)
            {
                if (a == null || a.id == 0 || a.id >= 51000 || a.isPro || notas.Contains(a.id)) continue;
                var nome = (a.itemName ?? "").Trim();
                if (nome.Length < 2 || NomeProibido.IsMatch(nome) || nome.IndexOf("unobtainable", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                var forte = EhForte(a) ? "_forte" : ""; // metal, tijolo, cofre: vai pra lista rara
                switch (a)
                {
                    case ItemGunAsset g when g.action != EAction.Rocket && g.action != EAction.Minigun:
                        p["arma"].Add(a);
                        // arma boa (airdrop automático): arma principal (rifle, metralhadora, sniper, escopeta), não pistola
                        if (g.slot == ESlotType.PRIMARY && g.rarity != EItemRarity.COMMON) p["arma_boa"].Add(a);
                        break;
                    case ItemSightAsset _: case ItemGripAsset _: case ItemBarrelAsset _: case ItemTacticalAsset _: p["acessorio"].Add(a); break;
                    case ItemThrowableAsset _: p["granada"].Add(a); break;
                    case ItemBackpackAsset _: p["mochila"].Add(a); break;
                    case ItemVestAsset _: p["colete"].Add(a); break;
                    case ItemHatAsset _: p["chapeu"].Add(a); break;
                    case ItemMedicalAsset _: p["remedio"].Add(a); break;
                    case ItemWaterAsset _: p["bebida"].Add(a); break;
                    case ItemFoodAsset _: p["comida"].Add(a); break;
                    case ItemFuelAsset _: case ItemRefillAsset _: case ItemToolAsset _: case ItemFisherAsset _: case ItemOpticAsset _: p["ferramenta"].Add(a); break;
                    case ItemStorageAsset _: p["bau" + forte].Add(a); break;
                    case ItemGeneratorAsset _: p["energia"].Add(a); break;
                    case ItemStructureAsset _: p["construcao" + forte].Add(a); break;
                    case ItemBarricadeAsset b when b.build == EBuild.DOOR || b.build == EBuild.GATE || b.build == EBuild.HATCH
                        || b.build == EBuild.FORTIFICATION || b.build == EBuild.BARRICADE || b.build == EBuild.WIRE || b.build == EBuild.LADDER
                        || b.build == EBuild.CLAIM || b.build == EBuild.SPOT:
                        p["construcao" + forte].Add(a); break;
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
            if (n.StartsWith("auto:")) return n.Substring(5);
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
            void Arma(int pentes, string pool = "arma")
            {
                if (!(Sortear(pool) is ItemGunAsset g) && !(Sortear("arma") is ItemGunAsset g2 && (g = g2) != null)) return;
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
                    // o normal e madeira; metal, tijolo, porta reforcada e cofre caem pouco
                    for (int i = 0; i < 5; i++) Por(sorte.NextDouble() < ChanceForteAirdrop ? "construcao_forte" : "construcao", 2, 6);
                    Por(sorte.NextDouble() < ChanceForteAirdrop ? "bau_forte" : "bau", 1, 2);
                    if (sorte.Next(2) == 0) Por("energia", 1, 1);
                    break;
                case "auto_armas":
                    // airdrop automático de armas: sempre arma boa (as raras saem menos), com pentes e um equipamento
                    Arma(sorte.Next(3, 5), "arma_boa");
                    if (sorte.Next(100) < 40) Arma(sorte.Next(2, 4), "arma_boa");
                    Por("acessorio", 1, 1);
                    if (sorte.Next(2) == 0) Por("colete", 1, 1); else Por("mochila", 1, 1);
                    if (sorte.Next(3) == 0) Por("granada", 1, 2);
                    Por("remedio", 1, 2);
                    break;
                case "auto_equip":
                    Por("mochila", 1, 1); Por("colete", 1, 1); Por("chapeu", 1, 1);
                    Por("ferramenta", 1, 1); Por("remedio", 1, 3); Por("remedio", 1, 2);
                    if (sorte.Next(2) == 0) Arma(sorte.Next(2, 4), "arma_boa");
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

        float proximoAirdropAuto = -1f;

        // California 2 vem com o airdrop nativo desligado e o jogo não carrega os pontos de queda;
        // estes são os 37 pontos de airdrop do próprio arquivo do mapa (Nodes.dat).
        static readonly Vector3[] PontosAirdropCa2 = {
            new Vector3(497.5f, 96.5f, 865.9f),
            new Vector3(751.4f, 125.4f, -46.7f),
            new Vector3(731.0f, 91.8f, -18.3f),
            new Vector3(-185.5f, 92.0f, 125.4f),
            new Vector3(-769.8f, 36.4f, 114.7f),
            new Vector3(409.6f, 108.0f, -581.9f),
            new Vector3(785.8f, 127.0f, -853.8f),
            new Vector3(-440.6f, 39.9f, 706.9f),
            new Vector3(165.1f, 154.9f, 265.7f),
            new Vector3(545.0f, 80.8f, -75.2f),
            new Vector3(-117.1f, 83.9f, -661.0f),
            new Vector3(-311.6f, 40.3f, -779.3f),
            new Vector3(1201.3f, 129.2f, 111.8f),
            new Vector3(1327.9f, 91.2f, -550.1f),
            new Vector3(1567.1f, 82.5f, -1219.5f),
            new Vector3(1626.2f, 80.5f, -721.8f),
            new Vector3(-333.9f, 38.4f, 1613.8f),
            new Vector3(18.3f, 38.3f, 1789.7f),
            new Vector3(998.4f, 90.9f, 1758.2f),
            new Vector3(1830.4f, 212.6f, 1575.1f),
            new Vector3(1793.4f, 155.4f, 695.9f),
            new Vector3(2131.1f, 171.4f, 630.0f),
            new Vector3(1929.0f, 64.1f, 2907.5f),
            new Vector3(1462.3f, 45.3f, 3090.1f),
            new Vector3(1654.9f, 54.3f, 3395.1f),
            new Vector3(1814.2f, 97.2f, 3667.2f),
            new Vector3(532.3f, 66.4f, 3523.0f),
            new Vector3(428.6f, 95.1f, 3769.3f),
            new Vector3(-595.7f, 31.3f, 3582.2f),
            new Vector3(-148.0f, 46.8f, 3745.6f),
            new Vector3(-403.1f, 36.3f, 3352.7f),
            new Vector3(1971.9f, 211.3f, 2157.9f),
            new Vector3(2168.3f, 60.6f, 3348.7f),
            new Vector3(1359.1f, 98.4f, 2532.7f),
            new Vector3(672.7f, 113.9f, 2382.7f),
            new Vector3(738.6f, 142.9f, 1135.4f),
            new Vector3(1552.6f, 234.5f, 647.5f)
        };

        /// <summary>Airdrop automático (pedido do dono): a cada AirdropAutoMinutos, num ponto de airdrop do mapa fora das safes,
        /// 70% armas boas e 30% equipamento. Cai mesmo com pouca gente online.</summary>
        void TickAirdropAuto()
        {
            if (S.AirdropAutoMinutos <= 0 || !Level.isLoaded) return;
            float agora = Time.realtimeSinceStartup;
            if (proximoAirdropAuto < 0) { proximoAirdropAuto = agora + S.AirdropAutoMinutos * 60f; return; }
            if (agora < proximoAirdropAuto) return;
            proximoAirdropAuto = agora + S.AirdropAutoMinutos * 60f;
            if (Provider.clients.Count == 0) return;
            Vector3? onde = null;
            for (int t = 0; t < 15 && onde == null; t++)
            {
                var no = LevelManager.GetRandomAirdropNode();
                var pos = no != null ? no.transform.position : PontosAirdropCa2[sorte.Next(PontosAirdropCa2.Length)];
                if (ZonaSeguraEm(pos) == null) onde = pos;
            }
            if (onde == null) { Rocket.Core.Logging.Logger.Log("[Airdrop auto] mapa sem ponto de airdrop fora das safes."); return; }
            bool armas = sorte.Next(100) < S.AirdropAutoChanceArmas;
            var kit = new KitAirdrop { Nome = armas ? "auto:auto_armas" : "auto:auto_equip" };
            if (!MandarAirdrop(onde.Value, kit, out var erro, armas ? "ARMAS" : "EQUIPAMENTO"))
                Rocket.Core.Logging.Logger.Log("[Airdrop auto] " + erro);
        }

        static string DescreverDrop(KitAirdrop kit)
        {
            switch (TipoDrop(kit?.Nome))
            {
                case "armas": return "sorteado: 2 ou 3 armas com carregadores, acessorios e as vezes granada";
                case "comida": return "sorteado: comida, bebida e remedio";
                case "medico": return "sorteado: remedios e bebida";
                case "equipamentos": return "sorteado: mochila, colete, capacete, ferramentas e remedio";
                case "base": return "sorteado: pecas de madeira, bau e as vezes gerador (metal e cofre sao raros)";
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
