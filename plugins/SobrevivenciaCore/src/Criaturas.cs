using System;
using System.Collections.Generic;
using System.Linq;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Infectados e urso radioativo do mod DZ Menu (são animais, então nascem em qualquer lugar do mapa,
    /// mesmo sem área de zumbi). AnimalManager.spawnAnimal cria e manda pra todos os clientes.
    /// O pack de cada grupo sai da lista do jogo pra ele não renascer sozinho no ponto do evento.</summary>
    public partial class Plugin
    {
        class Criatura
        {
            public Animal A;
            public Vector3 Centro;
            public float Fim;
            public bool Urso;
        }

        readonly List<Criatura> criaturas = new List<Criatura>();
        static readonly System.Random sorteCriatura = new System.Random();

        bool CriaturaRadioativa(ushort id) => id == S.CriaturaUrso || id == S.CriaturaInfectadoRadioativo;

        /// <summary>Nasce infectados (militar ou radioativo, sorteado) e ursos em volta do ponto. Somem em "segundos"
        /// (ou antes, com LimparCriaturas). Retorna quantos nasceram.</summary>
        internal int NascerCriaturas(Vector3 centro, int infectados, int ursos, float segundos)
        {
            if (!Level.isLoaded || AnimalManager.animals == null) return 0;
            float agora = Time.realtimeSinceStartup;
            // não enche o ponto: conta o que ainda está vivo ali
            int vivosAli = criaturas.Count(c => c.A != null && !c.A.isDead && (c.Centro - centro).sqrMagnitude < 60f * 60f);
            int teto = Math.Max(0, S.CriaturasMaxPorPonto - vivosAli);
            var pack = new PackInfo();
            pack.spawns.Add(new AnimalSpawnpoint(0, centro));
            int feitos = 0;
            var pedidos = new List<(ushort id, bool urso)>();
            // urso primeiro, pra não ficar de fora quando bate o teto
            for (int i = 0; i < ursos; i++) pedidos.Add((S.CriaturaUrso, true));
            for (int i = 0; i < infectados; i++)
            {
                bool rad = S.CriaturaInfectadoRadioativo != 0 && (S.CriaturaInfectadoMilitar == 0 || sorteCriatura.Next(2) == 0);
                pedidos.Add((rad ? S.CriaturaInfectadoRadioativo : S.CriaturaInfectadoMilitar, false));
            }
            foreach (var (id, urso) in pedidos)
            {
                if (feitos >= teto) break;
                if (id == 0 || !(Assets.find(EAssetType.ANIMAL, id) is AnimalAsset)) continue;
                float ang = (float)(sorteCriatura.NextDouble() * Math.PI * 2);
                var pos = centro + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * (12f + (float)sorteCriatura.NextDouble() * 14f);
                pos.y = ChaoCriatura(pos);
                var a = AcordarCriatura(id, pos);
                if (a == null) continue;
                // tira do pack do jogo (senão ele renasce sozinho ali) e põe no grupo do evento
                if (a.pack != null && a.pack != pack)
                {
                    a.pack.animals.Remove(a);
                    if (a.pack.animals.Count == 0 && AnimalManager.packs != null && AnimalManager.packs.Count > 1) AnimalManager.packs.Remove(a.pack);
                }
                a.pack = pack;
                if (!pack.animals.Contains(a)) pack.animals.Add(a);
                criaturas.RemoveAll(c => c.A == a);
                criaturas.Add(new Criatura { A = a, Centro = centro, Fim = agora + segundos, Urso = urso });
                feitos++;
            }
            if (feitos > 0) Rocket.Core.Logging.Logger.Log($"[Criaturas] {feitos} em X {centro.x:0} Z {centro.z:0}");
            else if (pedidos.Count > 0 && teto > 0 && !pedidos.Any(x => x.id != 0 && Assets.find(EAssetType.ANIMAL, x.id) is AnimalAsset))
                Rocket.Core.Logging.Logger.Log("[Criaturas] os animais do mod DZ Menu nao estao carregados (IDs " + string.Join(", ", pedidos.Select(x => x.id).Distinct()) + ")");
            return feitos;
        }

        /// <summary>Reusa um morto do mesmo tipo (como o spawnAnimal do jogo) ou cria um novo.</summary>
        Animal AcordarCriatura(ushort id, Vector3 pos)
        {
            float giro = (float)(sorteCriatura.NextDouble() * 360);
            var morto = AnimalManager.animals.FirstOrDefault(x => x != null && x.id == id && x.isDead);
            if (morto != null) { morto.sendRevive(pos, giro); return morto; }
            int antes = AnimalManager.animals.Count;
            AnimalManager.spawnAnimal(id, pos, Quaternion.Euler(0, giro, 0));
            if (AnimalManager.animals.Count <= antes) return null;
            var novo = AnimalManager.animals[AnimalManager.animals.Count - 1];
            return novo != null && novo.id == id ? novo : null;
        }

        static float ChaoCriatura(Vector3 p)
        {
            float y = Terreno(p.x, p.z);
            if (Physics.Raycast(new Vector3(p.x, y + 40f, p.z), Vector3.down, out var hit, 45f, RayMasks.BLOCK_COLLISION, QueryTriggerInteraction.Ignore)) y = hit.point.y;
            return y + 0.3f;
        }

        /// <summary>Evento acabou: as criaturas dele somem (assim que ninguém estiver perto, no máximo 5 min depois).</summary>
        internal void LimparCriaturas(Vector3 centro, float raio = 80f)
        {
            float agora = Time.realtimeSinceStartup;
            foreach (var c in criaturas) if ((c.Centro - centro).sqrMagnitude < raio * raio && c.Fim > agora) c.Fim = agora;
        }

        void TickCriaturas()
        {
            if (criaturas.Count == 0) return;
            float agora = Time.realtimeSinceStartup;
            for (int i = criaturas.Count - 1; i >= 0; i--)
            {
                var c = criaturas[i];
                if (c.A == null) { criaturas.RemoveAt(i); continue; }
                if (c.A.isDead)
                {
                    // morreu na mão de jogador: o plugin solta o loot (o mod não tem carne/pele)
                    criaturas.RemoveAt(i);
                    try { LootCriatura(c); } catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
                    continue;
                }
                if (agora < c.Fim) continue;
                var pos = c.A.transform.position;
                bool gentePerto = Provider.clients.Any(x => x.player != null && (x.player.transform.position - pos).sqrMagnitude < 80f * 80f);
                if (gentePerto && agora < c.Fim + 300f) continue;
                criaturas.RemoveAt(i);
                try { c.A.askDamage(ushort.MaxValue, Vector3.up, out _, out _, false, false); } catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
            }
        }

        void LootCriatura(Criatura c)
        {
            int n = c.Urso ? sorteCriatura.Next(S.CriaturaUrsoLootMin, S.CriaturaUrsoLootMax + 1)
                           : sorteCriatura.Next(S.CriaturaInfectadoLootMin, S.CriaturaInfectadoLootMax + 1);
            var pos = c.A.transform.position;
            for (int i = 0; i < n; i++)
            {
                ushort item = ItemDeZumbiMilitar();
                if (item != 0 && Itens.Existe(item)) ItemManager.dropItem(new Item(item, EItemOrigin.WORLD), pos, false, true, true);
            }
        }

        /// <summary>Um item da tabela de loot do zumbi militar do mapa (ou de qualquer tabela de zumbi, se não tiver militar).</summary>
        static ushort ItemDeZumbiMilitar()
        {
            if (LevelZombies.tables == null || LevelZombies.tables.Count == 0) return 0;
            var tabs = Enumerable.Range(0, LevelZombies.tables.Count)
                .Where(t => (LevelZombies.tables[t].name ?? "").IndexOf("milit", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            if (tabs.Count == 0) tabs = Enumerable.Range(0, LevelZombies.tables.Count).ToList();
            var tab = LevelZombies.tables[tabs[sorteCriatura.Next(tabs.Count)]];
            if (tab.lootID != 0) return SpawnTableTool.ResolveLegacyId(tab.lootID, EAssetType.ITEM, () => "loot da criatura");
            if (LevelItems.tables != null && tab.lootIndex < LevelItems.tables.Count) return LevelItems.getItem(tab.lootIndex);
            return 0;
        }

        /// <summary>Golpe do urso ou do infectado radioativo deixa o jogador com radiação (virus).</summary>
        void AoDanoCriatura(ref DamagePlayerParameters par, ref bool permitir)
        {
            if (!permitir || par.cause != EDeathCause.ANIMAL || par.player == null || criaturas.Count == 0 || S.CriaturaRadiacao <= 0) return;
            var pos = par.player.transform.position;
            var c = criaturas.FirstOrDefault(x => x.A != null && !x.A.isDead && CriaturaRadioativa(x.A.id) && (x.A.transform.position - pos).sqrMagnitude < 7f * 7f);
            if (c == null) return;
            try { par.player.life.serverModifyVirus(-S.CriaturaRadiacao); } catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }

        void LigarCriaturas() => DamageTool.damagePlayerRequested += AoDanoCriatura;

        void DesligarCriaturas()
        {
            DamageTool.damagePlayerRequested -= AoDanoCriatura;
            foreach (var c in criaturas) try { if (c.A != null && !c.A.isDead) c.A.askDamage(ushort.MaxValue, Vector3.up, out _, out _, false, false); } catch { }
            criaturas.Clear();
        }
    }

    /// <summary>/infectados [quantos] [ursos]: staff com permissão Mundo faz nascer infectados e urso 20 m na frente (teste).
    /// /infectados limpar apaga todos.</summary>
    public class CmdInfectados : ComandoSistema
    {
        public override string Name => "infectados";
        public override string Help => "Nasce infectados e urso radioativo na sua frente (staff).";
        public override List<string> Aliases => new List<string> { "criaturas" };
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (!P.Pode(p, "Mundo")) { Chat.Erro(p, "Voce nao tem permissao."); return; }
            if (a.Length > 0 && a[0].Equals("limpar", StringComparison.OrdinalIgnoreCase))
            {
                P.LimparCriaturas(p.Position, 100000f);
                Chat.Info(p, "As criaturas somem quando ninguem estiver perto (no maximo 5 min).");
                return;
            }
            int inf = a.Length > 0 && int.TryParse(a[0], out var x) ? Mathf.Clamp(x, 0, 20) : 3;
            int ursos = a.Length > 1 && int.TryParse(a[1], out var y) ? Mathf.Clamp(y, 0, 5) : 1;
            var frente = p.Player.transform.forward; frente.y = 0;
            if (frente.sqrMagnitude < 0.01f) frente = Vector3.forward;
            int n = P.NascerCriaturas(p.Position + frente.normalized * 20f, inf, ursos, 600f);
            P.LogAdmin(p, $"INFECTADOS: {n}");
            if (n > 0) Chat.Info(p, $"{n} criaturas nasceram na sua frente. Somem em 10 min.");
            else Chat.Erro(p, "Nao nasceu nenhuma. O mod DZ Menu com os animais novos ja foi reenviado no Workshop e o servidor reiniciou?");
        }
    }
}
