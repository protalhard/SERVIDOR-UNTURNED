using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SDG.Framework.Devkit;
using SDG.Unturned;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Loot melhor (pedido do dono em 10/11):
    /// - Pontos de loot das bases militares (tabela do mapa com "milit" no nome): o plugin cuida deles e cada um
    ///   ganha um monte de LootMilitarMultiplicador itens (10x) da própria tabela militar.
    /// - Pontos dentro das zonas de gás (deadzone/radiação): os melhores itens do jogo (armas do mercado negro com
    ///   carregador, equipamento top, visão noturna, acessórios, granadas).
    /// - Zumbi militar: dropa LootZumbiMilitarExtra itens a mais e às vezes um item top.
    /// O jogo não nasce nada nesses pontos (onServerSpawningItemDrop bloqueia); o plugin repõe quando ficam vazios,
    /// no mesmo tempo de respawn do Config.txt. Assim o limite de itens por região do jogo não segura o 10x.</summary>
    public partial class Plugin
    {
        class PontoEspecial
        {
            public Vector3 Pos;
            public byte Tabela;
            public bool Militar, Gas;
            public float Proximo;
        }

        List<PontoEspecial>[,] pontosEspeciais;
        HashSet<long> chavesEspeciais;
        bool soltandoLoot;
        float proximaRevisaoLoot;
        readonly Queue<(Vector3 pos, ushort id)> filaLoot = new Queue<(Vector3, ushort)>();
        readonly Dictionary<Zombie, float> zumbisMilitaresVistos = new Dictionary<Zombie, float>();
        HashSet<int> tabelasZumbiMilitar;

        static long ChaveLoot(Vector3 p)
            => ((long)Mathf.RoundToInt(p.x * 20f) & 0x1FFFFF) << 42 | ((long)Mathf.RoundToInt(p.y * 20f) & 0x1FFFFF) << 21 | ((long)Mathf.RoundToInt(p.z * 20f) & 0x1FFFFF);

        Regex FiltroMilitar => new Regex(string.IsNullOrWhiteSpace(S.LootMilitarFiltro) ? "milit" : S.LootMilitarFiltro, RegexOptions.IgnoreCase);

        static bool NaZonaDeGas(Vector3 p)
        {
            try { return DeadzoneVolumeManager.Get()?.GetMostDangerousOverlappingVolume(p) != null; }
            catch { return false; }
        }

        void LigarLootExtra()
        {
            ItemManager.onServerSpawningItemDrop += AoNascerItemDoMapa;
            DamageTool.damageZombieRequested += AoBaterZumbiMilitar;
        }

        void DesligarLootExtra()
        {
            ItemManager.onServerSpawningItemDrop -= AoNascerItemDoMapa;
            DamageTool.damageZombieRequested -= AoBaterZumbiMilitar;
        }

        /// <summary>Acha os pontos de loot militares e os de zona de gás (uma vez, depois que o mapa carrega).</summary>
        bool MapearPontosEspeciais()
        {
            if (pontosEspeciais != null) return true;
            if (LevelItems.spawns == null || LevelItems.tables == null || LevelItems.tables.Count == 0) return false;
            int w = LevelItems.spawns.GetLength(0), h = LevelItems.spawns.GetLength(1);
            var filtro = FiltroMilitar;
            var militares = new HashSet<byte>();
            for (int t = 0; t < LevelItems.tables.Count && t < 256; t++)
                if (filtro.IsMatch(LevelItems.tables[t].name ?? "")) militares.Add((byte)t);
            var mapa = new List<PontoEspecial>[w, h];
            var chaves = new HashSet<long>();
            int nMil = 0, nGas = 0;
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    var lista = LevelItems.spawns[x, y];
                    if (lista == null) continue;
                    foreach (var sp in lista)
                    {
                        bool mil = S.LootMilitarMultiplicador > 1 && militares.Contains(sp.type);
                        bool gas = S.LootGasLigado && NaZonaDeGas(sp.point);
                        if (!mil && !gas) continue;
                        if (mapa[x, y] == null) mapa[x, y] = new List<PontoEspecial>();
                        mapa[x, y].Add(new PontoEspecial { Pos = sp.point, Tabela = sp.type, Militar = mil, Gas = gas });
                        chaves.Add(ChaveLoot(sp.point));
                        if (mil) nMil++;
                        if (gas) nGas++;
                    }
                }
            pontosEspeciais = mapa;
            chavesEspeciais = chaves;
            Rocket.Core.Logging.Logger.Log($"[Loot] tabelas militares: {string.Join(", ", militares.Select(t => LevelItems.tables[t].name))}. " +
                $"Pontos militares: {nMil} (x{S.LootMilitarMultiplicador}). Pontos na zona de gas: {nGas}.");
            return true;
        }

        /// <summary>O jogo vai nascer um item num ponto do mapa: nos pontos especiais quem nasce é o plugin.</summary>
        void AoNascerItemDoMapa(Item item, ref Vector3 location, ref bool shouldAllow)
        {
            if (soltandoLoot || !shouldAllow) return;
            try
            {
                if (!MapearPontosEspeciais()) return;
                if (chavesEspeciais.Contains(ChaveLoot(location))) shouldAllow = false;
            }
            catch { }
        }

        /// <summary>1x por segundo (ChecarZonas): solta a fila e, a cada 20 s, repõe os pontos especiais vazios.</summary>
        void TickLootExtra()
        {
            if (!Level.isLoaded) return;
            int n = 0;
            while (filaLoot.Count > 0 && n++ < 250)
            {
                var (pos, id) = filaLoot.Dequeue();
                if (!Itens.Existe(id)) continue;
                soltandoLoot = true;
                try { ItemManager.dropItem(new Item(id, EItemOrigin.WORLD), pos, false, false, false); }
                catch { }
                finally { soltandoLoot = false; }
            }
            TickZumbisMilitares();

            float agora = Time.realtimeSinceStartup;
            if (agora < proximaRevisaoLoot) return;
            proximaRevisaoLoot = agora + 20f;
            if (!MapearPontosEspeciais() || ItemManager.regions == null) return;
            float respawn = Mathf.Max(120f, Provider.modeConfigData?.Items?.Respawn_Time ?? 900f);
            int w = pontosEspeciais.GetLength(0), h = pontosEspeciais.GetLength(1);
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    var pts = pontosEspeciais[x, y];
                    if (pts == null) continue;
                    var itens = ItemManager.regions[x, y]?.items;
                    foreach (var pt in pts)
                    {
                        bool temItem = false;
                        if (itens != null)
                            foreach (var it in itens)
                                if ((it.point - pt.Pos).sqrMagnitude < 2.25f) { temItem = true; break; }
                        // ainda tem item: o tempo de respawn só começa a contar quando o ponto fica vazio
                        if (temItem) { pt.Proximo = agora + respawn; continue; }
                        if (agora < pt.Proximo) continue;
                        pt.Proximo = agora + respawn;
                        EncherPonto(pt);
                    }
                }
        }

        void Soltar(Vector3 centro, ushort id, float espalhar)
        {
            if (id == 0) return;
            var p = centro + new Vector3((float)(sorte.NextDouble() * 2 - 1) * espalhar, 0.3f, (float)(sorte.NextDouble() * 2 - 1) * espalhar);
            filaLoot.Enqueue((p, id));
        }

        void EncherPonto(PontoEspecial pt)
        {
            if (pt.Militar)
                for (int i = 0; i < S.LootMilitarMultiplicador; i++)
                    Soltar(pt.Pos, ItemDaTabela(pt.Tabela), 0.7f);
            if (pt.Gas)
            {
                if (!pt.Militar) Soltar(pt.Pos, ItemDaTabela(pt.Tabela), 0.5f); // o item normal do ponto continua
                for (int i = 0; i < Math.Max(1, S.LootGasItensTop); i++) SoltarTop(pt.Pos, 0.7f);
            }
        }

        static ushort ItemDaTabela(byte tabela)
        {
            try { return tabela < LevelItems.tables.Count ? LevelItems.getItem(tabela) : (ushort)0; }
            catch { return 0; }
        }

        /// <summary>Um dos melhores itens do jogo: arma do mercado negro com 2 carregadores, equipamento top,
        /// visão noturna, acessório ou granada.</summary>
        void SoltarTop(Vector3 centro, float espalhar)
        {
            int x = sorte.Next(100);
            if (x < 40)
            {
                if (Sortear("arma_top") is ItemGunAsset g)
                {
                    Soltar(centro, g.id, espalhar);
                    var mag = CarregadorDe(g);
                    for (int i = 0; i < 2; i++) Soltar(centro, mag, espalhar);
                }
                return;
            }
            string pool = x < 65 ? "equip_top" : x < 77 ? "visao" : x < 90 ? "acessorio" : "granada";
            Soltar(centro, Sortear(pool)?.id ?? 0, espalhar);
        }

        // ---------- zumbi militar ----------

        bool ZumbiMilitar(Zombie z)
        {
            if (tabelasZumbiMilitar == null)
            {
                if (LevelZombies.tables == null) return false;
                var filtro = FiltroMilitar;
                tabelasZumbiMilitar = new HashSet<int>(Enumerable.Range(0, LevelZombies.tables.Count).Where(t => filtro.IsMatch(LevelZombies.tables[t].name ?? "")));
                Rocket.Core.Logging.Logger.Log("[Loot] zumbis militares: " + string.Join(", ", tabelasZumbiMilitar.Select(t => LevelZombies.tables[t].name)));
            }
            return tabelasZumbiMilitar.Contains(z.type);
        }

        void AoBaterZumbiMilitar(ref DamageZombieParameters par, ref bool permitir)
        {
            if (!permitir || par.zombie == null || par.zombie.isDead) return;
            if (S.LootZumbiMilitarExtra <= 0 && S.LootZumbiMilitarChanceTop <= 0) return;
            try { if (ZumbiMilitar(par.zombie)) zumbisMilitaresVistos[par.zombie] = Time.realtimeSinceStartup + 15f; } catch { }
        }

        /// <summary>Zumbi militar que levou dano e morreu: solta o loot extra no corpo.</summary>
        void TickZumbisMilitares()
        {
            if (zumbisMilitaresVistos.Count == 0) return;
            float agora = Time.realtimeSinceStartup;
            foreach (var z in zumbisMilitaresVistos.Keys.ToList())
            {
                if (z == null) { zumbisMilitaresVistos.Remove(z); continue; }
                if (z.isDead)
                {
                    zumbisMilitaresVistos.Remove(z);
                    var pos = z.transform.position;
                    var tab = LevelZombies.tables[z.type];
                    for (int i = 0; i < S.LootZumbiMilitarExtra; i++)
                    {
                        ushort id = 0;
                        try
                        {
                            if (tab.lootID != 0) id = SpawnTableTool.ResolveLegacyId(tab.lootID, EAssetType.ITEM, () => "loot do zumbi militar");
                            else if (tab.lootIndex < LevelItems.tables.Count) id = LevelItems.getItem(tab.lootIndex);
                        }
                        catch { }
                        Soltar(pos, id, 1f);
                    }
                    if (sorte.Next(100) < S.LootZumbiMilitarChanceTop)
                    {
                        if (sorte.Next(2) == 0 && Sortear("arma_boa") is ItemGunAsset g) { Soltar(pos, g.id, 1f); Soltar(pos, CarregadorDe(g), 1f); }
                        else Soltar(pos, Sortear("equip_top")?.id ?? 0, 1f);
                    }
                }
                else if (agora > zumbisMilitaresVistos[z]) zumbisMilitaresVistos.Remove(z);
            }
        }
    }
}
