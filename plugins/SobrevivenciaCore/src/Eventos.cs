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
            if (fora.Count == 0) return null;
            var bons = fora.Where(p => { var d = DistSafe(p); return d >= S.DropMinSafe && d <= S.DropMaxSafe; }).ToList();
            if (bons.Count == 0) bons = fora.Where(p => DistSafe(p) >= S.DropMinSafe).ToList();
            if (bons.Count == 0) bons = fora;
            return bons[sorte.Next(bons.Count)];
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
            Chat.Todos(Chat.Cor(S.CorDestaque, Chat.Forte($"EVENTO: {eventoNome} CHEGANDO EM {min} MIN!"))
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
                    Chat.Todos(Chat.Cor(S.CorDestaque, Chat.Forte(eventoNome + " COMECOU!")) + Chat.Cor("#FFFFFF", " O bau some em 10 minutos. Cuidado com os zumbis."));
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

        /// <summary>Zumbis em volta do ponto (eventos, airdrop, heli crash). Primeiro acorda os mortos da área;
        /// se faltar, CRIA zumbis novos na área (ZombieManager.addZombie, o mesmo que o jogo usa ao carregar o mapa)
        /// e manda a lista nova pra quem está ali. Sem área de zumbi no ponto, puxa os das áreas mais perto.</summary>
        int NascerZumbis(Vector3 centro, int quantos) => NascerZumbis(centro, quantos, false);

        // quantos zumbis o plugin já criou em cada área (o jogo não apaga; limite pra não crescer sem fim)
        readonly Dictionary<byte, int> zumbisCriados = new Dictionary<byte, int>();
        const int MaxZumbisCriadosPorArea = 40;
        static System.Reflection.MethodInfo mandarZumbis;

        /// <summary>militar = prefere zumbis de tabela militar e faz um deles virar chefe (mega).</summary>
        int NascerZumbis(Vector3 centro, int quantos, bool militar)
        {
            if (quantos <= 0 || ZombieManager.regions == null) return 0;
            bool LongeDeTodos(Zombie z) => Provider.clients.All(c => c.player == null || (c.player.transform.position - z.transform.position).sqrMagnitude > 150f * 150f);
            bool TabelaMilitar(int t) => LevelZombies.tables != null && t < LevelZombies.tables.Count
                && (LevelZombies.tables[t].name ?? "").IndexOf("milit", StringComparison.OrdinalIgnoreCase) >= 0;
            Vector3 Ponto()
            {
                float ang = (float)(sorte.NextDouble() * Math.PI * 2), dist = 6f + (float)sorte.NextDouble() * 16f;
                var p = centro + new Vector3(Mathf.Cos(ang) * dist, 0, Mathf.Sin(ang) * dist);
                if (Physics.Raycast(new Vector3(p.x, centro.y + 40f, p.z), Vector3.down, out var hit, 90f, RayMasks.BLOCK_COLLISION, QueryTriggerInteraction.Ignore))
                    p = hit.point;
                return p + Vector3.up * 0.5f;
            }
            bool chefe = militar;
            int n = 0;
            void Reviver(Zombie z)
            {
                var esp = z.speciality == EZombieSpeciality.NONE ? EZombieSpeciality.NORMAL : z.speciality;
                if (chefe) { esp = EZombieSpeciality.MEGA; chefe = false; }
                z.sendRevive(z.type, (byte)esp, z.shirt, z.pants, z.hat, z.gear, Ponto(), (float)(sorte.NextDouble() * 360));
                n++;
            }

            bool temArea = LevelNavigation.tryGetBounds(centro, out var bound) && bound < ZombieManager.regions.Length && ZombieManager.regions[bound] != null;
            var reg = temArea ? ZombieManager.regions[bound] : null;
            try
            {
                // 1) os mortos da área
                if (reg?.zombies != null)
                {
                    var mortos = reg.zombies.Where(z => z != null && z.isDead && !z.isBoss);
                    if (militar) mortos = mortos.OrderByDescending(z => TabelaMilitar(z.type) ? 1 : 0);
                    foreach (var z in mortos.Take(quantos).ToList()) Reviver(z);
                }
                // 2) faltou: cria zumbis novos na área
                if (n < quantos && reg != null && LevelZombies.tables != null && LevelZombies.tables.Count > 0)
                {
                    zumbisCriados.TryGetValue(bound, out var ja);
                    int criar = Math.Min(quantos - n, MaxZumbisCriadosPorArea - ja);
                    if (criar > 0)
                    {
                        // tabela: militar se pedir e existir; senão a mais comum da área; senão a primeira
                        int tipo = -1;
                        if (militar) tipo = Enumerable.Range(0, LevelZombies.tables.Count).Where(TabelaMilitar).DefaultIfEmpty(-1).First();
                        if (tipo < 0 && reg.zombies.Count > 0) tipo = reg.zombies.Where(z => z != null).GroupBy(z => (int)z.type).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
                        if (tipo < 0 || tipo >= LevelZombies.tables.Count) tipo = 0;
                        var tab = LevelZombies.tables[tipo];
                        byte Roupa(int slot) => tab.slots != null && slot < tab.slots.Length && tab.slots[slot].table.Count > 0 && sorte.NextDouble() < tab.slots[slot].chance
                            ? (byte)sorte.Next(tab.slots[slot].table.Count) : byte.MaxValue;
                        var novos = new List<Zombie>();
                        for (int i = 0; i < criar && reg.zombies.Count < ushort.MaxValue; i++)
                        {
                            ZombieManager.instance.addZombie(bound, (byte)tipo, (byte)EZombieSpeciality.NORMAL, Roupa(0), Roupa(1), Roupa(2), Roupa(3),
                                (byte)sorte.Next(4), (byte)sorte.Next(3), Ponto(), (float)(sorte.NextDouble() * 360), true);
                            novos.Add(reg.zombies[reg.zombies.Count - 1]);
                        }
                        zumbisCriados[bound] = ja + novos.Count;
                        // quem está nessa área recebe a lista nova (senão não vê os zumbis criados)
                        if (mandarZumbis == null)
                            mandarZumbis = typeof(ZombieManager).GetMethod("SendZombiesToPlayer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        foreach (var c in Provider.clients)
                            if (c?.player?.movement != null && c.player.movement.bound == bound)
                                mandarZumbis?.Invoke(ZombieManager.instance, new object[] { c.transportConnection, bound });
                        foreach (var z in novos) Reviver(z);
                    }
                }
                // 3) sem área no ponto (ou cheia): puxa zumbis longe de todo mundo das áreas mais perto
                if (n < quantos)
                {
                    var perto = ZombieManager.regions.Where(r => r?.zombies != null && r.zombies.Count > 0)
                        .OrderBy(r => r.zombies.Where(z => z != null).Select(z => (z.transform.position - centro).sqrMagnitude).DefaultIfEmpty(float.MaxValue).Min())
                        .Take(3).SelectMany(r => r.zombies)
                        .Where(z => z != null && !z.isBoss && (z.isDead || LongeDeTodos(z)));
                    foreach (var z in perto.Take(quantos - n).ToList()) Reviver(z);
                }
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
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
