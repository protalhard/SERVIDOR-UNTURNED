using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Rocket.Unturned.Player;
using SDG.Unturned;

namespace SobrevivenciaCore
{
    /// <summary>Kits pedidos pelo dono em 10/11:
    /// - base (grátis, 2 h): casa 4x4 de madeira (16 pisos, 16 tetos, 20 pilares, 17 paredes e 1 batente com porta de metal),
    ///   gerador com gasolina, caixa de metal, 4 cofres, cama, picareta, machado, Claim Flag e a maior mochila do jogo.
    /// - arqueiro (grátis, 30 min): arco (não a besta), 5 flechas, mochila, faca e 1 MRE.
    /// - policial: o kit com que o jogador nasce (entrou ou renasceu): Colt com 2 carregadores, comida, água e faca.
    /// Os IDs saem pelo nome do item (o jogo base e o California 2 têm nomes diferentes), uma vez, e ficam salvos na config.
    /// O que não cabe no inventário fica guardado: /kit nome de novo entrega o resto, sem contar o tempo.</summary>
    public partial class Plugin
    {
        public const int VersaoKitsNovos = 1;

        List<ItemAsset> todosItensCache;

        List<ItemAsset> TodosItens()
        {
            if (todosItensCache != null) return todosItensCache;
            var l = new List<ItemAsset>();
            Assets.find(l);
            return todosItensCache = l.Where(a => a != null && a.id != 0 && a.id < 51000 && !a.isPro).ToList();
        }

        /// <summary>Primeiro nome da lista que existir (nome exato, sem diferenciar maiúscula); senão o primeiro que passar no filtro.</summary>
        ushort AcharItem(string oQue, Func<ItemAsset, bool> filtro, params string[] nomes)
        {
            var todos = TodosItens().Where(filtro).ToList();
            foreach (var n in nomes)
            {
                var a = todos.Where(x => string.Equals((x.itemName ?? "").Trim(), n, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.id).FirstOrDefault();
                if (a != null) { Rocket.Core.Logging.Logger.Log($"[Kits] {oQue}: {a.itemName} ({a.id})"); return a.id; }
            }
            var b = todos.Where(x => !NomeProibido.IsMatch(x.itemName ?? "")).OrderBy(x => x.id).FirstOrDefault();
            Rocket.Core.Logging.Logger.Log($"[Kits] {oQue}: " + (b == null ? "NAO ACHEI" : $"{b.itemName} ({b.id}) (pelo tipo)"));
            return b?.id ?? 0;
        }

        static bool Nome(ItemAsset a, string regex) => Regex.IsMatch(a.itemName ?? "", regex, RegexOptions.IgnoreCase);
        static bool Estrutura(ItemAsset a, EConstruct c) => a is ItemStructureAsset s && s.construct == c;
        static bool Barricada(ItemAsset a, EBuild b) => a is ItemBarricadeAsset x && !(a is ItemStorageAsset) && x.build == b;

        /// <summary>Roda depois que os itens do mapa carregam (junto com o ajuste dos kits de madeira).</summary>
        void AjustarKitsNovos()
        {
            var cfg = Configuration.Instance;
            if (cfg.VersaoKitsNovos >= VersaoKitsNovos || S.Kits == null) return;
            todosItensCache = null;
            if (!TodosItens().Any(a => a.id >= 4000)) return; // itens do California 2 ainda não carregaram

            bool Madeira(ItemAsset a) => Nome(a, @"\b(maple|wood|wooden|birch|pine)\b");
            ushort piso = AcharItem("piso", a => Estrutura(a, EConstruct.FLOOR) && Madeira(a), "Maple Floor", "Wooden Floor", "Birch Floor", "Pine Floor");
            ushort teto = AcharItem("teto", a => Estrutura(a, EConstruct.ROOF) && Madeira(a), "Maple Roof", "Wooden Roof", "Birch Roof", "Pine Roof");
            if (teto == 0) teto = piso;
            ushort pilar = AcharItem("pilar", a => Estrutura(a, EConstruct.PILLAR) && Madeira(a), "Maple Pillar", "Wooden Pillar", "Birch Pillar", "Pine Pillar");
            ushort parede = AcharItem("parede", a => Estrutura(a, EConstruct.WALL) && Madeira(a) && !Nome(a, @"door|window|garage|gate|rampart"), "Maple Wall", "Wooden Wall", "Birch Wall", "Pine Wall");
            ushort batente = AcharItem("batente", a => a is ItemStructureAsset && Madeira(a) && Nome(a, @"doorway"), "Maple Doorway", "Wooden Doorway", "Birch Doorway", "Pine Doorway");
            ushort porta = AcharItem("porta de metal", a => Barricada(a, EBuild.DOOR) && Nome(a, @"\bmetal\b") && !Nome(a, @"jail|garage|vault|bars"), "Metal Door");
            ushort gerador = AcharItem("gerador", a => a is ItemGeneratorAsset, "Generator", "Portable Generator");
            ushort gasolina = AcharItem("gasolina", a => a is ItemFuelAsset && Nome(a, @"gas|fuel|petrol"), "Gasoline Canister", "Gas Can", "Gasoline Can", "Jerrycan");
            ushort caixaMetal = Assets.find(EAssetType.ITEM, (ushort)6957) is ItemStorageAsset ? (ushort)6957
                : AcharItem("caixa de metal", a => a is ItemStorageAsset && Nome(a, @"\bmetal\b") && Nome(a, @"crate|box|chest"), "Metal Crate", "Industrial Metal Crate");
            ushort cofre = AcharItem("cofre", a => a is ItemStorageAsset && Nome(a, @"\b(safe|vault)\b"), "Safe", "Vault", "Metal Safe");
            if (cofre == 0) cofre = caixaMetal;
            ushort cama = AcharItem("cama", a => a is ItemBarricadeAsset b && b.build == EBuild.BED && !Nome(a, @"roll|sleeping"), "Bed", "Wooden Bed", "Metal Bed");
            if (cama == 0) cama = AcharItem("cama (saco de dormir)", a => a is ItemBarricadeAsset b && b.build == EBuild.BED, "Black Bedroll", "Bedroll");
            ushort picareta = Itens.Existe(1198) && Nome(Assets.find(EAssetType.ITEM, (ushort)1198) as ItemAsset, "pick") ? (ushort)1198
                : AcharItem("picareta", a => a is ItemMeleeAsset && Nome(a, @"pick"), "Pickaxe");
            ushort machado = AcharItem("machado", a => a is ItemMeleeAsset && Nome(a, @"\baxe\b") && !Nome(a, @"pick"), "Fire Axe", "Camp Axe", "Axe");
            ushort claim = Assets.find(EAssetType.ITEM, (ushort)4995) is ItemBarricadeAsset cb && cb.build == EBuild.CLAIM ? (ushort)4995
                : AcharItem("claim flag", a => a is ItemBarricadeAsset b && b.build == EBuild.CLAIM, "Claim Flag");
            ushort mochila = MaiorMochila();

            var itensBase = new List<ItemKit>();
            void Por(ushort id, int q) { if (id != 0 && q > 0) { var j = itensBase.FirstOrDefault(x => x.Id == id); if (j != null) j.Quantidade += q; else itensBase.Add(new ItemKit(id, q)); } }
            Por(mochila, 1);                    // primeiro: ele veste e o resto cabe nela
            Por(claim, 1);
            Por(piso, 16); Por(teto, 16);       // casa 4x4
            Por(pilar, 20);                     // cantos em volta + os do meio pras 2 paredes de dividir
            Por(parede, batente != 0 ? 17 : 18); // 15 em volta + 2 pra dividir (sem batente, 16 em volta)
            Por(batente, 1); Por(porta, 1);
            Por(gerador, 1); Por(gasolina, 1);
            Por(caixaMetal, 1); Por(cofre, 4);
            Por(cama, 1); Por(picareta, 1); Por(machado, 1);

            var kb = S.Kits.FirstOrDefault(k => k.Nome.Equals("base", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(k.Permissao));
            if (kb == null) { kb = new Kit { Nome = "base" }; S.Kits.Add(kb); }
            kb.Titulo = "Base"; kb.Espera = 7200; kb.Abates = 0; kb.Permissao = "";
            kb.Descricao = "Casa 4x4 de madeira com porta de metal, gerador com gasolina, caixa de metal, 4 cofres, cama, picareta, machado e Claim Flag";
            kb.Itens = itensBase;

            // arqueiro: arco de puxar (não a besta) e as flechas dele
            var arco = TodosItens().OfType<ItemGunAsset>().Where(g => Nome(g, @"\bbow\b") && !Nome(g, @"cross"))
                .OrderBy(g => Nome(g, @"maple|birch|pine|wood") ? 0 : 1).ThenBy(g => g.id).FirstOrDefault();
            Rocket.Core.Logging.Logger.Log("[Kits] arco: " + (arco == null ? "NAO ACHEI" : $"{arco.itemName} ({arco.id})"));
            ushort flecha = arco != null ? CarregadorDe(arco) : (ushort)0;
            var itensArq = new List<ItemKit>();
            if (arco != null) itensArq.Add(new ItemKit(arco.id));
            if (flecha != 0) itensArq.Add(new ItemKit(flecha, 5));
            foreach (var id in new ushort[] { 4137, 4813, 4305 }) // mochila de couro, faca militar, MRE
                if (Itens.Existe(id)) itensArq.Add(new ItemKit(id));
            var ka = S.Kits.FirstOrDefault(k => k.Nome.Equals("arqueiro", StringComparison.OrdinalIgnoreCase));
            if (ka == null) { ka = new Kit { Nome = "arqueiro" }; S.Kits.Add(ka); }
            ka.Titulo = "Arqueiro"; ka.Espera = 1800; ka.Abates = 0; ka.Permissao = "";
            ka.Descricao = "Arco, 5 flechas, mochila, faca e 1 MRE";
            ka.Itens = itensArq;

            // policial: o kit de nascer
            var pol = new List<ItemKit>();
            foreach (var (id, q) in new[] { ((ushort)97, 1), ((ushort)98, 2), ((ushort)4301, 1), ((ushort)4400, 1), ((ushort)4813, 1) }) // Colt, 2 carregadores, feijão, água, faca
                if (Itens.Existe(id)) pol.Add(new ItemKit(id, q));
            if (pol.Count >= 3) { cfg.KitInicial = pol; cfg.KitAoRenascer = true; }

            cfg.VersaoKitsNovos = VersaoKitsNovos;
            Configuration.Save();
            Rocket.Core.Logging.Logger.Log($"SobrevivenciaCore: kits novos (base {itensBase.Sum(i => i.Quantidade)} itens, arqueiro {itensArq.Count}, policial {pol.Count}).");
        }

        // ---------- entrega com resto guardado ----------

        // jogador -> itens do kit que ainda não couberam (fica salvo até ele pegar)
        Dictionary<ulong, List<ushort>> kitPendentes;
        string ArqKitPendentes => Path.Combine(Directory, "kit_pendentes.tsv");

        Dictionary<ulong, List<ushort>> KitPendentes()
        {
            if (kitPendentes != null) return kitPendentes;
            kitPendentes = new Dictionary<ulong, List<ushort>>();
            try
            {
                if (File.Exists(ArqKitPendentes))
                    foreach (var l in File.ReadAllLines(ArqKitPendentes))
                    {
                        var c = l.Split('\t');
                        if (c.Length < 3 || !ulong.TryParse(c[0], out var id)) continue;
                        var lista = c[2].Split(',').Select(x => ushort.TryParse(x, out var v) ? v : (ushort)0).Where(v => v != 0).ToList();
                        if (lista.Count > 0) kitPendentes[id] = lista;
                        kitPendenteNome[id] = c[1];
                    }
            }
            catch { }
            return kitPendentes;
        }

        readonly Dictionary<ulong, string> kitPendenteNome = new Dictionary<ulong, string>();

        void SalvarKitPendentes()
        {
            try
            {
                File.WriteAllLines(ArqKitPendentes, KitPendentes().Where(k => k.Value.Count > 0)
                    .Select(k => k.Key + "\t" + (kitPendenteNome.TryGetValue(k.Key, out var n) ? n : "") + "\t" + string.Join(",", k.Value)));
            }
            catch { }
        }

        /// <summary>Esse jogador tem resto desse kit pra pegar.</summary>
        public bool TemRestoDoKit(ulong id, string kit)
            => KitPendentes().TryGetValue(id, out var l) && l.Count > 0 && kitPendenteNome.TryGetValue(id, out var n) && n.Equals(kit, StringComparison.OrdinalIgnoreCase);

        /// <summary>Entrega o kit: mochila primeiro (veste se estiver sem), depois o que couber. O resto fica guardado.</summary>
        public string EntregarKit(UnturnedPlayer p, Kit k)
        {
            var id = Id(p);
            var lista = new List<ushort>();
            foreach (var i in k.Itens ?? new List<ItemKit>())
            {
                if (!Itens.Existe(i.Id)) continue;
                if (Assets.find(EAssetType.ITEM, i.Id) is ItemBackpackAsset mb && p.Player.clothing.backpack == 0)
                {
                    p.Player.clothing.askWearBackpack(i.Id, 100, mb.getState(), true);
                    for (int q = 1; q < i.Quantidade; q++) lista.Add(i.Id);
                    continue;
                }
                for (int q = 0; q < i.Quantidade; q++) lista.Add(i.Id);
            }
            // sobrou resto de outro kit: junta (não perde nada)
            if (KitPendentes().TryGetValue(id, out var antes) && antes.Count > 0) lista.AddRange(antes);
            KitPendentes()[id] = lista;
            kitPendenteNome[id] = k.Nome;
            return EntregarRestoKit(p);
        }

        /// <summary>Tenta pôr o resto guardado no inventário. Devolve o texto pro chat ("" = entregou tudo).</summary>
        public string EntregarRestoKit(UnturnedPlayer p)
        {
            var id = Id(p);
            if (!KitPendentes().TryGetValue(id, out var lista) || lista.Count == 0) return "";
            int i = 0;
            while (i < lista.Count)
            {
                if (p.Inventory.tryAddItem(new Item(lista[i], true), true)) lista.RemoveAt(i);
                else i++; // não coube esse: tenta os menores
            }
            string nome = kitPendenteNome.TryGetValue(id, out var n) ? n : "";
            if (lista.Count == 0) { KitPendentes().Remove(id); SalvarKitPendentes(); return ""; }
            SalvarKitPendentes();
            return $" Nao coube tudo: faltam {lista.Count} itens guardados. Coloque/guarde o que tem e use /kit {nome} de novo pra pegar o resto (nao conta tempo).";
        }
    }
}
