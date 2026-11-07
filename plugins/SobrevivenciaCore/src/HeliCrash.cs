using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>HELI CRASH (pedido do dono, igual ao do Zumbas): o servidor avisa no chat, um helicóptero militar de
    /// verdade (veículo do jogo) atravessa o céu pegando fogo, perde o controle e cai longe das safes com um estrondo.
    /// O que sobra fica queimando, com fumaça marcando o lugar, caixas de loot bom em volta e zumbis.
    /// Tudo some depois de HeliCrashMinutos.</summary>
    public partial class Plugin
    {
        class EstadoHeli
        {
            public InteractableVehicle Heli;
            public Vector3 Inicio, Queda, Direcao;
            public float Altura, Comecou, Duracao, Fim, CaiuEm, ProximaFumaca, ProximaHorda;
            public bool Caiu, Achado;
            public string Regiao;
            public List<Transform> Caixas = new List<Transform>();
            /// <summary>Quem já recebeu o marcador do local no mapa.</summary>
            public HashSet<ulong> Marcados = new HashSet<ulong>();
        }

        EstadoHeli heli;
        float proximoHeli = -1f, heliComecaEm = -1f;
        Vector3 heliPontoMarcado;
        List<EffectAsset> efeitosFogo;

        public bool HeliCrashAtivo => heli != null || heliComecaEm > 0;

        /// <summary>Onde o heli caiu (pro aviso do HUD mostrar a distância). Null antes de cair.</summary>
        public Vector3? HeliCrashLocal => heli != null && heli.Caiu ? heli.Queda : (Vector3?)null;

        VehicleAsset HeliMilitar()
        {
            if (S.HeliCrashVeiculo != 0 && Assets.find(EAssetType.VEHICLE, S.HeliCrashVeiculo) is VehicleAsset v) return v;
            var helis = new List<VehicleAsset>();
            Assets.find(helis);
            // o Chopper do California 2 é motor "PLANE" no .dat, então também vale pelo nome
            helis = helis.Where(x => x != null && (x.engine == EEngine.HELICOPTER
                || (x.vehicleName ?? "").IndexOf("chopper", StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            if (helis.Count == 0) return null;
            string[] nomes = { "chopper", "military", "army", "black", "hawk", "huey", "transport", "police" };
            foreach (var n in nomes)
            {
                var a = helis.FirstOrDefault(x => (x.vehicleName ?? "").IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0);
                if (a != null) return a;
            }
            return helis.OrderByDescending(x => x.health).First();
        }

        /// <summary>Efeitos de fogo/fumaça que o servidor tem (do mod DZ, senão os do jogo, achados pelo nome).</summary>
        List<EffectAsset> EfeitosFogo()
        {
            if (efeitosFogo != null) return efeitosFogo;
            efeitosFogo = new List<EffectAsset>();
            if (S.HeliCrashFumacaEfeito != 0 && Assets.find(EAssetType.EFFECT, S.HeliCrashFumacaEfeito) is EffectAsset dz) efeitosFogo.Add(dz);
            var todos = new List<EffectAsset>();
            Assets.find(todos);
            var achados = todos.Where(e => e != null && e.name != null && !e.name.StartsWith("DZ_")
                && (e.name.IndexOf("Fire", StringComparison.OrdinalIgnoreCase) >= 0 || e.name.IndexOf("Smoke", StringComparison.OrdinalIgnoreCase) >= 0)
                && e.name.IndexOf("UI", StringComparison.Ordinal) < 0 && e.lifetime > 0).Take(6).ToList();
            Rocket.Core.Logging.Logger.Log("[Heli Crash] efeitos de fogo/fumaca: " + (achados.Count == 0 ? "nenhum" : string.Join(", ", achados.Select(e => $"{e.name} ({e.id})"))));
            if (efeitosFogo.Count == 0) efeitosFogo.AddRange(achados);
            return efeitosFogo;
        }

        void Efeito(EffectAsset e, Vector3 pos, bool longe)
        {
            if (e == null) return;
            var par = new TriggerEffectParameters(e) { position = pos, relevantDistance = longe ? EffectManager.INSANE : EffectManager.LARGE, reliable = false };
            EffectManager.triggerEffect(par);
        }

        /// <summary>Marca o heli crash (aviso no chat agora, o heli sai depois de avisoSegundos). Devolve o que aconteceu.</summary>
        internal string ComecarHeliCrash(int avisoSegundos, Vector3? aqui = null)
        {
            if (!Level.isLoaded) return "mapa carregando";
            if (HeliCrashAtivo) return "ja tem um heli crash acontecendo";
            if (HeliMilitar() == null) return "nenhum helicoptero carregado no servidor";
            var ponto = aqui ?? PontoDeQueda();
            if (ponto == null) return "sem ponto de queda fora das safes";
            heliPontoMarcado = ponto.Value;
            avisoSegundos = Math.Max(5, avisoSegundos);
            heliComecaEm = Time.realtimeSinceStartup + avisoSegundos;
            Chat.Todos(Chat.Cor("#FF3B30", Chat.Forte("HELI CRASH VINDO! ")) + Chat.Cor("#FFFFFF", "um helicoptero militar esta com pane e vai cair no mapa")
                + Chat.Cor(S.CorDestaque, Chat.Forte($" em {avisoSegundos} s")) + Chat.Cor("#FFFFFF", ". Va preparado: nos destrocos ficam os zumbis mais fortes do servidor, mas o loot e o melhor."));
            Rocket.Core.Logging.Logger.Log($"[Heli Crash] marcado pra {avisoSegundos}s, queda em X {heliPontoMarcado.x:0} Z {heliPontoMarcado.z:0}");
            return $"heli sai em {avisoSegundos} s e cai perto de {RegiaoDe(heliPontoMarcado)} (X {heliPontoMarcado.x:0} Z {heliPontoMarcado.z:0})";
        }

        /// <summary>Chão de verdade no ponto: o terreno, ou o que estiver até 40 m acima dele (casa, pedra).
        /// Antes o raio vinha de 1000 m e batia num colisor invisível no céu do California 2 (o heli "caía" no ar).</summary>
        static Vector3 ChaoDaQueda(Vector3 p)
        {
            float t = Terreno(p.x, p.z);
            if (float.IsNaN(t)) t = p.y;
            if (Physics.Raycast(new Vector3(p.x, t + 40f, p.z), Vector3.down, out var hit, 80f, RayMasks.BLOCK_COLLISION, QueryTriggerInteraction.Ignore))
                return hit.point;
            return new Vector3(p.x, t, p.z);
        }

        void LargarHeli()
        {
            heliComecaEm = -1f;
            var asset = HeliMilitar();
            if (asset == null) { Rocket.Core.Logging.Logger.Log("[Heli Crash] sem helicoptero"); return; }
            var queda = heliPontoMarcado;
            queda = ChaoDaQueda(queda);
            float ang = (float)(sorte.NextDouble() * Math.PI * 2);
            var dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
            float voo = Mathf.Max(300f, S.HeliCrashDistanciaVoo);
            var inicio = queda - dir * voo;
            float altura = Mathf.Max(60f, S.HeliCrashAltura);
            var h = new EstadoHeli
            {
                Inicio = inicio, Queda = queda, Direcao = dir, Altura = altura,
                Comecou = Time.realtimeSinceStartup, Duracao = voo / Mathf.Max(15f, S.HeliCrashVelocidade),
                Regiao = RegiaoDe(queda),
            };
            var pos0 = inicio + Vector3.up * altura;
            float chaoInicio = Terreno(inicio.x, inicio.z);
            pos0.y = Mathf.Max(queda.y + altura, float.IsNaN(chaoInicio) ? 0f : chaoInicio + altura);
            h.Heli = VehicleManager.spawnVehicleV2(asset, pos0, Quaternion.LookRotation(dir));
            if (h.Heli == null) { Rocket.Core.Logging.Logger.Log("[Heli Crash] o jogo nao criou o helicoptero"); return; }
            try
            {
                h.Heli.tellLocked(CSteamID.Nil, CSteamID.Nil, true); // ninguém entra no heli caindo
                VehicleManager.sendVehicleFuel(h.Heli, 0);
                // vida cheia no voo (com pouca vida ele explodia logo no começo, longe de todo mundo); a fumaça vem dos efeitos
                VehicleManager.sendVehicleHealth(h.Heli, h.Heli.asset.health);
                var rb0 = h.Heli.GetComponent<Rigidbody>();
                if (rb0 != null) { rb0.useGravity = false; rb0.isKinematic = true; }
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
            heli = h;
            Chat.Todos(Chat.Cor("#FF3B30", Chat.Forte("HELI CRASH: ")) + Chat.Cor("#FFFFFF", $"o helicoptero esta caindo perto de {Chat.Limpar(h.Regiao)}! Olhe pro ceu."));
            Rocket.Core.Logging.Logger.Log($"[Heli Crash] {asset.vehicleName} saiu de X {inicio.x:0} Z {inicio.z:0}, cai em X {queda.x:0} Z {queda.z:0} ({h.Regiao}) em {h.Duracao:0}s");
            StartCoroutine(VooHeli(h));
        }

        /// <summary>Voo: reto e alto até 55% do caminho, depois perde o controle (gira e mergulha) até bater no chão.</summary>
        IEnumerator VooHeli(EstadoHeli h)
        {
            var rb = h.Heli.GetComponent<Rigidbody>();
            var explosao = h.Heli.asset.FindExplosionEffectAsset();
            float giro = 0f, proxFogo = 0f;
            bool estourou = false;
            while (heli == h && h.Heli != null && !h.Heli.isExploded)
            {
                yield return new WaitForFixedUpdate();
                float t = (Time.realtimeSinceStartup - h.Comecou) / h.Duracao;
                if (t >= 1f) break;
                var plano = Vector3.Lerp(h.Inicio, h.Queda, t);
                float y;
                if (t < 0.55f) y = h.Queda.y + h.Altura;
                else { float k = (t - 0.55f) / 0.45f; y = h.Queda.y + h.Altura * (1f - k * k); }
                // não entra no morro no caminho
                float chao = Terreno(plano.x, plano.z);
                if (!float.IsNaN(chao)) y = Mathf.Max(y, chao + (t < 0.97f ? 6f : 0.5f));
                // nada derruba o heli antes da hora
                if (h.Heli.health < h.Heli.asset.health / 2) VehicleManager.sendVehicleHealth(h.Heli, h.Heli.asset.health);
                var pos = new Vector3(plano.x, y, plano.z);
                if (t >= 0.55f) giro += Time.fixedDeltaTime * Mathf.Lerp(40f, 260f, (t - 0.55f) / 0.45f);
                float nariz = t < 0.55f ? 6f : Mathf.Lerp(6f, 35f, (t - 0.55f) / 0.45f);
                var rot = Quaternion.LookRotation(h.Direcao) * Quaternion.Euler(nariz, giro, t < 0.55f ? 0f : Mathf.Sin(giro * Mathf.Deg2Rad) * 20f);
                if (rb != null)
                {
                    rb.useGravity = false;
                    rb.isKinematic = true;
                    rb.MovePosition(pos);
                    rb.MoveRotation(rot);
                }
                else { h.Heli.transform.position = pos; h.Heli.transform.rotation = rot; }
                // o primeiro estouro: o motor pega fogo no ar
                if (!estourou && t >= 0.55f)
                {
                    estourou = true;
                    Efeito(explosao, pos, true);
                }
                // rastro de fogo e fumaça atrás do heli
                if (Time.realtimeSinceStartup >= proxFogo)
                {
                    proxFogo = Time.realtimeSinceStartup + (t < 0.55f ? 0.6f : 0.25f);
                    var fogo = EfeitosFogo();
                    if (fogo.Count > 0) Efeito(fogo[sorte.Next(fogo.Count)], pos - h.Direcao * 4f, true);
                }
            }
            if (heli != h) yield break;
            if (h.Heli == null || h.Heli.isExploded)
                Rocket.Core.Logging.Logger.Log($"[Heli Crash] o voo acabou antes da hora ({(Time.realtimeSinceStartup - h.Comecou):0}s de {h.Duracao:0}s): heli {(h.Heli == null ? "sumiu" : "explodiu")}");
            Cair(h, explosao);
        }

        void Cair(EstadoHeli h, EffectAsset explosao)
        {
            h.Caiu = true;
            h.CaiuEm = Time.realtimeSinceStartup;
            h.Fim = Time.realtimeSinceStartup + Math.Max(5, S.HeliCrashMinutos) * 60f;
            try
            {
                if (h.Heli != null && !h.Heli.isExploded)
                {
                    var rb = h.Heli.GetComponent<Rigidbody>();
                    if (rb != null) { rb.isKinematic = false; rb.useGravity = true; }
                    // ESTRONDO: o heli explode (efeito e som de explosão do próprio jogo, ouvido de longe)
                    VehicleManager.damage(h.Heli, 100000f, 1f, false, default(CSteamID), EDamageOrigin.Unknown);
                }
                Efeito(explosao, h.Queda + Vector3.up * 2f, true);
                Efeito(explosao, h.Queda + h.Direcao * 6f + Vector3.up, true);
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }

            // caixas de loot espalhadas em volta dos destroços (a caixa do airdrop solta fumaça vermelha)
            var bau = Assets.find(EAssetType.ITEM, (ushort)1374) as ItemBarricadeAsset;
            int n = Math.Max(1, S.HeliCrashCaixas);
            if (bau != null)
                for (int i = 0; i < n; i++)
                {
                    float a = i * Mathf.PI * 2f / n + (float)sorte.NextDouble() * 0.5f;
                    var pos = h.Queda + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (7f + (float)sorte.NextDouble() * 4f);
                    if (Physics.Raycast(pos + Vector3.up * 30f, Vector3.down, out var hit, 60f, RayMasks.BLOCK_COLLISION, QueryTriggerInteraction.Ignore)) pos = hit.point;
                    var rot = Quaternion.Euler(0, (float)(sorte.NextDouble() * 360), 0);
                    var t = BarricadeManager.dropNonPlantedBarricade(new Barricade(bau), pos, rot, 0, 0);
                    if (t == null) continue;
                    h.Caixas.Add(t);
                    if (t.GetComponent<InteractableStorage>() is InteractableStorage st)
                    {
                        st.items.clear();
                        // a primeira caixa é a do ITEM RARO
                        foreach (var it in ConteudoAirdrop(new KitAirdrop { Nome = i == 0 ? "auto:auto_helicrash" : "auto:auto_helicrash2" }))
                        {
                            if (!Itens.Existe(it.Id)) continue;
                            for (int q = 0; q < it.Quantidade; q++) st.items.tryAddItem(new Item(it.Id, true));
                        }
                    }
                }
            int zumbis = NascerZumbis(h.Queda, S.HeliCrashZumbis, true);
            int bichos = NascerCriaturas(h.Queda, S.HeliCrashInfectados + (zumbis == 0 ? S.CriaturasSemZumbi : 0), S.HeliCrashUrsos, S.HeliCrashMinutos * 60f + 120f);
            Rocket.Core.Logging.Logger.Log($"[Heli Crash] {zumbis} zumbis e {bichos} criaturas nos destrocos, fumaca: {string.Join(", ", EfeitosFogo().Select(e => $"{e.name} ({e.id})"))}");
            h.ProximaHorda = Time.realtimeSinceStartup + 30f;
            Chat.Todos(Chat.Cor("#FF3B30", Chat.Forte("HELI CRASH CAIU ")) + Chat.Cor("#FFFFFF", $"perto de {Chat.Limpar(h.Regiao)}! O local ficou ")
                + Chat.Cor(S.CorDestaque, Chat.Forte("marcado no mapa")) + Chat.Cor("#FFFFFF", " e tem fumaca alta saindo dos destrocos. ")
                + Chat.Cor(S.CorDestaque, Chat.Forte($"{h.Caixas.Count} caixas com o melhor loot")) + Chat.Cor("#FFFFFF", $", zumbis fortes e infectados. Some em {S.HeliCrashMinutos} min."));
            MarcarHeliNoMapa(h);
            Rocket.Core.Logging.Logger.Log($"[Heli Crash] caiu em X {h.Queda.x:0} Z {h.Queda.z:0} ({h.Regiao}), {h.Caixas.Count} caixas");
        }

        /// <summary>Marca o local da queda no mapa de quem ainda não recebeu (quem entra durante o evento também).</summary>
        void MarcarHeliNoMapa(EstadoHeli h)
        {
            foreach (var sp in Provider.clients)
            {
                try
                {
                    if (sp?.player == null || !h.Marcados.Add(sp.playerID.steamID.m_SteamID)) continue;
                    sp.player.quests.replicateSetMarker(true, h.Queda, "HELI CRASH");
                }
                catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
            }
        }

        /// <summary>Fim do evento: tira o marcador de quem ainda está com ele no local do heli.</summary>
        void DesmarcarHeliNoMapa(EstadoHeli h)
        {
            foreach (var sp in Provider.clients)
            {
                try
                {
                    var q = sp?.player?.quests;
                    if (q == null || !q.isMarkerPlaced || (q.markerPosition - h.Queda).sqrMagnitude > 4f) continue;
                    q.replicateSetMarker(false, Vector3.zero, "");
                }
                catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
            }
        }

        internal void PararHeliCrash() => AcabarHeliCrash(true);

        void AcabarHeliCrash(bool avisar)
        {
            heliComecaEm = -1f;
            var h = heli; heli = null;
            if (h == null) return;
            // o próximo conta a partir de quando este some (pedido do dono)
            if (S.HeliCrashIntervaloMinutos > 0) proximoHeli = Time.realtimeSinceStartup + S.HeliCrashIntervaloMinutos * 60f;
            try { if (h.Heli != null) VehicleManager.askVehicleDestroy(h.Heli); } catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
            foreach (var t in h.Caixas)
            {
                try
                {
                    if (t == null) continue;
                    var d = BarricadeManager.FindBarricadeByRootTransform(t);
                    if (d != null && BarricadeManager.tryGetRegion(t, out var x, out var y, out var plant, out _)) BarricadeManager.destroyBarricade(d, x, y, plant);
                }
                catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
            }
            if (h.Caiu) { LimparCriaturas(h.Queda); DesmarcarHeliNoMapa(h); }
            if (avisar) Chat.Todos(Chat.Cor("#FF3B30", Chat.Forte("HELI CRASH: ")) + Chat.Cor("#FFFFFF", "os destrocos sumiram. Acabou o evento."));
        }

        void TickHeliCrash()
        {
            if (!Level.isLoaded) return;
            float agora = Time.realtimeSinceStartup;
            if (heliComecaEm > 0 && agora >= heliComecaEm) LargarHeli();
            var h = heli;
            if (h != null)
            {
                // se o voo travou por algum motivo (heli apagado), encerra
                if (!h.Caiu && (h.Heli == null || agora > h.Comecou + h.Duracao + 20f)) { AcabarHeliCrash(false); return; }
                if (!h.Caiu) return;
                // ninguém chegou: some em HeliCrashMinutos (15). Alguém chegou nos destroços: some 10 min depois disso.
                if (!h.Achado && Provider.clients.Any(x => x.player != null && (x.player.transform.position - h.Queda).sqrMagnitude < 40f * 40f))
                {
                    h.Achado = true;
                    h.Fim = agora + 600f;
                    Chat.Todos(Chat.Cor("#FF3B30", Chat.Forte("HELI CRASH: ")) + Chat.Cor("#FFFFFF", "alguem chegou nos destrocos! Eles somem em 10 min."));
                }
                if (agora >= h.Fim) { AcabarHeliCrash(true); return; }
                MarcarHeliNoMapa(h);
                // fumaça subindo dos destroços pra todo mundo achar
                if (agora >= h.ProximaFumaca)
                {
                    h.ProximaFumaca = agora + Mathf.Max(2f, S.HeliCrashFumacaSegundos);
                    var fogo = EfeitosFogo();
                    if (fogo.Count > 0) Efeito(fogo[0], h.Queda + Vector3.up, true);
                }
                // zumbis voltam enquanto tem gente chegando
                if (agora >= h.ProximaHorda && Provider.clients.Any(x => x.player != null && (x.player.transform.position - h.Queda).sqrMagnitude < 150f * 150f))
                {
                    h.ProximaHorda = agora + 35f;
                    NascerZumbis(h.Queda, Math.Max(2, S.HeliCrashZumbis / 3), true);
                }
                return;
            }
            if (S.HeliCrashIntervaloMinutos <= 0) return;
            if (proximoHeli < 0) { proximoHeli = agora + S.HeliCrashIntervaloMinutos * 60f; return; }
            if (agora < proximoHeli) return;
            proximoHeli = agora + S.HeliCrashIntervaloMinutos * 60f;
            if (Provider.clients.Count < Math.Max(1, S.HeliCrashMinJogadores)) return;
            Rocket.Core.Logging.Logger.Log("[Heli Crash auto] " + ComecarHeliCrash(S.HeliCrashAvisoSegundos));
        }
    }

    /// <summary>/helicrash: staff com permissão Mundo começa o heli crash (o heli sai em 30 s).
    /// /helicrash aqui = cai 30 m na sua frente em 10 s (pra testar). /helicrash parar encerra.</summary>
    public class CmdHeliCrash : ComandoSistema
    {
        public override string Name => "helicrash";
        public override string Help => "Comeca o evento Heli Crash (staff).";
        public override List<string> Aliases => new List<string> { "lcrash" };
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (!P.Pode(p, "Mundo")) { Chat.Erro(p, "Voce nao tem permissao."); return; }
            if (a.Length > 0 && a[0].Equals("parar", StringComparison.OrdinalIgnoreCase))
            {
                P.PararHeliCrash();
                P.LogAdmin(p, "HELI CRASH: parado");
                Chat.Info(p, "Heli crash encerrado.");
                return;
            }
            if (a.Length > 0 && a[0].Equals("aqui", StringComparison.OrdinalIgnoreCase))
            {
                // teste do dono: cai 30 m na frente de quem digitou (longe o bastante pra explosão não matar)
                var frente = p.Player.transform.forward; frente.y = 0;
                if (frente.sqrMagnitude < 0.01f) frente = Vector3.forward;
                var r2 = P.ComecarHeliCrash(10, p.Position + frente.normalized * 30f);
                P.LogAdmin(p, "HELI CRASH AQUI: " + r2);
                Chat.Info(p, "Heli crash: " + r2 + ". Olhe pro ceu!");
                return;
            }
            var r = P.ComecarHeliCrash(30);
            P.LogAdmin(p, "HELI CRASH: " + r);
            Chat.Info(p, "Heli crash: " + r);
        }
    }
}
