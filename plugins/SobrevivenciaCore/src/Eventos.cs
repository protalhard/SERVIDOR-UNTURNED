using System;
using System.Collections.Generic;
using System.Linq;
using SDG.Unturned;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Evento de clima (pedido do dono): de tempos em tempos o servidor avisa no chat, liga um clima
    /// (chuva, tempestade, neve, neblina... o que o jogo tiver carregado) e no começo dele cai o BAÚ DO EVENTO
    /// longe das safes, com zumbis em volta. O baú some sozinho (a caixa de airdrop do jogo dura 10 min).</summary>
    public partial class Plugin
    {
        float proximoEvento = -1f;
        float eventoComecaEm = -1f;
        Vector3 eventoPonto;
        string eventoNome;
        List<WeatherAssetBase> climas;

        static readonly string[] PalavrasClima = { "storm", "thunder", "rain", "snow", "blizzard", "fog", "mist", "sand", "dust", "hail" };

        List<WeatherAssetBase> Climas()
        {
            if (climas != null) return climas;
            var todos = new List<WeatherAssetBase>();
            Assets.find(todos);
            Rocket.Core.Logging.Logger.Log("[Evento] climas no servidor: " + string.Join(", ", todos.Where(c => c != null).Select(c => c.name)));
            climas = todos.Where(c => c != null && c.name != null
                && PalavrasClima.Any(p => c.name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            var chuva = WeatherAssetBase.DEFAULT_RAIN.Find();
            if (climas.Count == 0 && chuva != null) climas.Add(chuva);
            return climas;
        }

        static string NomeClima(WeatherAssetBase c, out string emoji)
        {
            var n = (c.name ?? "").ToLowerInvariant();
            emoji = "";
            if (n.Contains("sand") || n.Contains("dust")) return "TEMPESTADE DE AREIA";
            if (n.Contains("blizzard")) return "NEVASCA";
            if (n.Contains("snow")) return "NEVE";
            if (n.Contains("fog") || n.Contains("mist")) return "NEBLINA";
            if (n.Contains("storm") || n.Contains("thunder") || c.hasLightning) return "TEMPESTADE";
            if (n.Contains("hail")) return "GRANIZO";
            return "CHUVA";
        }

        /// <summary>Ponto de queda longe das safes, mas não longe demais (entre DropMinSafe e DropMaxSafe metros da borda da safe mais perto).</summary>
        Vector3? PontoDeQueda()
        {
            var safes = Configuration.Instance.ZonasSeguras.Where(z => z.Definido).ToList();
            float DistSafe(Vector3 p) => safes.Count == 0 ? 999999f : safes.Min(z => DistanciaPlana(p, z.X, z.Z) - z.Raio);
            var candidatos = new List<Vector3>();
            for (int t = 0; t < 30; t++)
            {
                var no = LevelManager.GetRandomAirdropNode();
                if (no != null) candidatos.Add(no.transform.position);
            }
            candidatos.AddRange(PontosAirdropCa2);
            var fora = candidatos.Where(p => ZonaSeguraEm(p) == null).ToList();
            // prefere pontos dentro de área de zumbi do mapa (fora delas não dá pra ter zumbi no evento)
            var comZumbi = fora.Where(p => LevelNavigation.tryGetBounds(p, out var b) && ZombieManager.regions != null && b < ZombieManager.regions.Length
                && ZombieManager.regions[b]?.zombies?.Count > 0).ToList();
            if (comZumbi.Count > 0) fora = comZumbi;
            if (fora.Count == 0) return null;
            var bons = fora.Where(p => { var d = DistSafe(p); return d >= S.DropMinSafe && d <= S.DropMaxSafe; }).ToList();
            if (bons.Count == 0) bons = fora.Where(p => DistSafe(p) >= S.DropMinSafe).ToList();
            if (bons.Count == 0) bons = fora;
            return bons[sorte.Next(bons.Count)];
        }

        /// <summary>Ponto em qualquer lugar do mapa (Heli Crash): chão plano, fora da água, fora de telhado e longe das safes.
        /// Como o Heli Crash usa só as nossas criaturas (animais), não precisa de área de zumbi.</summary>
        Vector3? PontoQualquerDoMapa()
        {
            var safes = Configuration.Instance.ZonasSeguras.Where(z => z.Definido).ToList();
            float DistSafe(Vector3 p) => safes.Count == 0 ? 999999f : safes.Min(z => DistanciaPlana(p, z.X, z.Z) - z.Raio);
            float metade = Level.size / 2f - Level.border - 60f;
            if (metade < 100f) return null;
            for (int t = 0; t < 300; t++)
            {
                float x = (float)(sorte.NextDouble() * 2 - 1) * metade, z = (float)(sorte.NextDouble() * 2 - 1) * metade;
                float y = Terreno(x, z);
                if (float.IsNaN(y)) continue;
                var p = new Vector3(x, y, z);
                if (SDG.Framework.Water.WaterUtility.isPointUnderwater(p + Vector3.up * 0.5f)) continue;
                if (ZonaSeguraEm(p) != null || DistSafe(p) < S.DropMinSafe) continue;
                // chão mais ou menos plano em volta (as caixas ficam a ~10 m)
                bool plano = true;
                foreach (var d in new[] { new Vector3(10, 0, 0), new Vector3(-10, 0, 0), new Vector3(0, 0, 10), new Vector3(0, 0, -10) })
                {
                    float h = Terreno(x + d.x, z + d.z);
                    if (float.IsNaN(h) || Mathf.Abs(h - y) > 4f) { plano = false; break; }
                }
                if (!plano) continue;
                // nada em cima (prédio, árvore grande, pedra)
                if (Physics.Raycast(p + Vector3.up * 40f, Vector3.down, out var hit, 45f, RayMasks.BLOCK_COLLISION, QueryTriggerInteraction.Ignore) && hit.point.y > y + 2f) continue;
                return p;
            }
            return null;
        }

        /// <summary>Começa um evento agora (o painel ou o /evento chamam com aviso menor). Devolve o que aconteceu.</summary>
        internal string ComecarEvento(int avisoSegundos)
        {
            if (!Level.isLoaded) return "mapa ainda carregando";
            if (eventoComecaEm > 0) return "ja tem um evento marcado";
            var lista = Climas();
            if (lista.Count == 0) return "nenhum clima carregado no servidor";
            var ponto = PontoDeQueda();
            if (ponto == null) return "sem ponto de queda fora das safes";
            var clima = lista[sorte.Next(lista.Count)];
            eventoNome = NomeClima(clima, out _);
            eventoPonto = ponto.Value;
            avisoSegundos = Math.Max(10, avisoSegundos);
            float duracao = Math.Max(2, S.EventoDuracaoMinutos) * 60f;
            LightingManager.SetScheduledWeather(clima, avisoSegundos, duracao);
            eventoComecaEm = Time.realtimeSinceStartup + avisoSegundos;
            var min = Mathf.CeilToInt(avisoSegundos / 60f);
            Chat.TodosIcone("evento", Chat.Cor(S.CorDestaque, Chat.Forte($"EVENTO: {eventoNome} CHEGANDO EM {min} MIN!"))
                + Chat.Cor("#FFFFFF", " Quando comecar, cai o ") + Chat.Cor(S.CorDestaque, Chat.Forte("BAU DO EVENTO"))
                + Chat.Cor("#FFFFFF", " com loot top e zumbis em volta. Fique de olho no ceu."));
            Rocket.Core.Logging.Logger.Log($"[Evento] {eventoNome} ({clima.name}) em {avisoSegundos}s, por {duracao / 60f:0} min. Bau em X {eventoPonto.x:0} Z {eventoPonto.z:0}");
            return $"{eventoNome} em {min} min (bau em X {eventoPonto.x:0} Z {eventoPonto.z:0})";
        }

        void TickEvento()
        {
            if (!Level.isLoaded) return;
            float agora = Time.realtimeSinceStartup;
            if (eventoComecaEm > 0 && agora >= eventoComecaEm)
            {
                eventoComecaEm = -1f;
                var kit = new KitAirdrop { Nome = "auto:auto_evento" };
                if (!MandarAirdrop(eventoPonto, kit, out var erro, "BAU DA " + eventoNome, S.EventoZumbis))
                    Rocket.Core.Logging.Logger.Log("[Evento] " + erro);
                else
                {
                    Chat.TodosIcone("evento", Chat.Cor(S.CorDestaque, Chat.Forte(eventoNome + " COMECOU!")) + Chat.Cor("#FFFFFF", " O bau some em 10 minutos. Cuidado com os zumbis."));
                    RegistrarEvento(eventoNome + " comecou perto de " + Chat.Limpar(RegiaoDe(eventoPonto)));
                    Rocket.Core.Logging.Logger.Log($"[Evento] bau caiu em X {eventoPonto.x:0} Z {eventoPonto.z:0}");
                }
            }
            if (S.EventoMinutos <= 0) return;
            if (proximoEvento < 0) { proximoEvento = agora + S.EventoMinutos * 60f; return; }
            if (agora < proximoEvento) return;
            proximoEvento = agora + S.EventoMinutos * 60f;
            if (Provider.clients.Count == 0) return;
            var r = ComecarEvento(S.EventoAvisoMinutos * 60);
            Rocket.Core.Logging.Logger.Log("[Evento auto] " + r);
        }

        // ---------- Zumbis dos eventos ----------
        // O cliente só conhece os zumbis da área em que entrou: a lista da área chega uma vez só (o cliente ignora
        // uma segunda lista da mesma área). Então zumbi criado depois que o jogador já está na área fica invisível.
        // Solução: logo que o mapa carrega (antes de ter gente), cada área ganha uma RESERVA de zumbis mortos.
        // Todo jogador recebe a reserva junto com a área, e os eventos só reavivam e movem zumbis dessa área.
        // A reserva não renasce sozinha pelo jogo (o plugin segura o tempo de morte deles).
        const int ReservaZumbisPorArea = 16;
        readonly HashSet<Zombie> reservaZumbis = new HashSet<Zombie>();
        bool reservaPronta;
        float proxSegurarReserva;
        static System.Reflection.FieldInfo campoUltimaMorte;

        void TickReservaZumbis()
        {
            if (!Level.isLoaded || ZombieManager.regions == null || ZombieManager.instance == null) return;
            if (!reservaPronta) PrepararReservaZumbis();
            if (Time.realtimeSinceStartup < proxSegurarReserva) return;
            proxSegurarReserva = Time.realtimeSinceStartup + 30f;
            if (campoUltimaMorte == null)
                campoUltimaMorte = typeof(Zombie).GetField("_lastDead", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            foreach (var z in reservaZumbis)
                if (z != null && z.isDead) campoUltimaMorte?.SetValue(z, Time.realtimeSinceStartup);
        }

        void PrepararReservaZumbis()
        {
            reservaPronta = true;
            int criados = 0, areas = 0;
            for (int b = 0; b < ZombieManager.regions.Length && b < 255; b++)
            {
                var reg = ZombieManager.regions[b];
                if (reg?.zombies == null || reg.zombies.Count == 0) continue;
                // jogador já dentro da área não receberia a reserva: pula (só acontece se o plugin recarregar com gente)
                if (Provider.clients.Any(c => c?.player?.movement != null && c.player.movement.bound == b)) continue;
                var modelo = reg.zombies.FirstOrDefault(z => z != null);
                if (modelo == null) continue;
                byte tipo = reg.zombies.Where(z => z != null).GroupBy(z => z.type).OrderByDescending(g => g.Count()).First().Key;
                for (int i = 0; i < ReservaZumbisPorArea && reg.zombies.Count < ushort.MaxValue; i++)
                {
                    ZombieManager.instance.addZombie((byte)b, tipo, (byte)EZombieSpeciality.NORMAL, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue,
                        (byte)sorte.Next(4), (byte)sorte.Next(3), modelo.transform.position, 0f, true);
                    reservaZumbis.Add(reg.zombies[reg.zombies.Count - 1]);
                    criados++;
                }
                areas++;
            }
            Rocket.Core.Logging.Logger.Log($"[Zumbis] reserva pros eventos: {criados} zumbis em {areas} areas");
        }

        /// <summary>Zumbis em volta do ponto (eventos, airdrop, heli crash). Usa só zumbis da área do ponto (os que os
        /// jogadores dali enxergam): primeiro a reserva, depois os mortos, depois os vivos longe de todo mundo.</summary>
        int NascerZumbis(Vector3 centro, int quantos) => NascerZumbis(centro, quantos, false);

        /// <summary>militar = vira zumbi de tabela militar e um deles vira chefe (mega).</summary>
        int NascerZumbis(Vector3 centro, int quantos, bool militar)
        {
            if (quantos <= 0 || ZombieManager.regions == null) return 0;
            if (!LevelNavigation.tryGetBounds(centro, out var bound) || bound >= ZombieManager.regions.Length || ZombieManager.regions[bound]?.zombies == null)
            {
                Rocket.Core.Logging.Logger.Log($"[Zumbis] ponto X {centro.x:0} Z {centro.z:0} fora de area de zumbi do mapa: sem zumbis ali");
                return 0;
            }
            var reg = ZombieManager.regions[bound];
            bool LongeDeTodos(Zombie z) => Provider.clients.All(c => c.player == null || (c.player.transform.position - z.transform.position).sqrMagnitude > 150f * 150f);
            int tipoMilitar = -1;
            if (militar && LevelZombies.tables != null)
                tipoMilitar = Enumerable.Range(0, LevelZombies.tables.Count)
                    .Where(t => (LevelZombies.tables[t].name ?? "").IndexOf("milit", StringComparison.OrdinalIgnoreCase) >= 0).DefaultIfEmpty(-1).First();
            var vivos = reg.zombies.Where(z => z != null && !z.isBoss).ToList();
            var usar = vivos.Where(z => z.isDead && reservaZumbis.Contains(z))
                .Concat(vivos.Where(z => z.isDead && !reservaZumbis.Contains(z)))
                .Concat(vivos.Where(z => !z.isDead && LongeDeTodos(z)))
                .Take(quantos).ToList();
            bool chefe = militar;
            int n = 0;
            foreach (var z in usar)
            {
                float ang = (float)(sorte.NextDouble() * Math.PI * 2), dist = 6f + (float)sorte.NextDouble() * 16f;
                var p = centro + new Vector3(Mathf.Cos(ang) * dist, 0, Mathf.Sin(ang) * dist);
                if (Physics.Raycast(new Vector3(p.x, centro.y + 40f, p.z), Vector3.down, out var hit, 90f, RayMasks.BLOCK_COLLISION, QueryTriggerInteraction.Ignore))
                    p = hit.point;
                p += Vector3.up * 0.5f;
                byte tipo = tipoMilitar >= 0 ? (byte)tipoMilitar : z.type;
                byte shirt = z.shirt, pants = z.pants, hat = z.hat, gear = z.gear;
                if (tipo != z.type || reservaZumbis.Contains(z))
                {
                    var tab = LevelZombies.tables[tipo];
                    byte Roupa(int slot) => tab.slots != null && slot < tab.slots.Length && tab.slots[slot].table.Count > 0 && sorte.NextDouble() < tab.slots[slot].chance
                        ? (byte)sorte.Next(tab.slots[slot].table.Count) : byte.MaxValue;
                    shirt = Roupa(0); pants = Roupa(1); hat = Roupa(2); gear = Roupa(3);
                }
                var esp = z.speciality == EZombieSpeciality.NONE ? EZombieSpeciality.NORMAL : z.speciality;
                if (chefe) { esp = EZombieSpeciality.MEGA; chefe = false; }
                else if (esp.IsBoss()) esp = EZombieSpeciality.NORMAL;
                try { z.sendRevive(tipo, (byte)esp, shirt, pants, hat, gear, p, (float)(sorte.NextDouble() * 360)); n++; }
                catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); break; }
            }
            return n;
        }
    }

    /// <summary>/evento: staff com permissão Mundo (ou o dono) começa um evento de clima em 1 minuto.</summary>
    public class CmdEvento : ComandoSistema
    {
        public override string Name => "evento";
        public override string Help => "Comeca um evento de clima com o bau (staff).";
        protected override void Rodar(Rocket.Unturned.Player.UnturnedPlayer p, string[] a)
        {
            if (!P.Pode(p, "Mundo")) { Chat.Erro(p, "Voce nao tem permissao."); return; }
            var r = P.ComecarEvento(60);
            P.LogAdmin(p, "EVENTO: " + r);
            Chat.Info(p, "Evento: " + r);
        }
    }
}
