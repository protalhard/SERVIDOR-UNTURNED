using System;
using System.Collections.Generic;
using System.Linq;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Comboio emboscado (aprovado pelo dono): de tempos em tempos um caminhão militar aparece parado e quebrado
    /// numa estrada do mapa (longe das safes), com caixas trancadas de loot militar. Zumbis em volta, e eles voltam a
    /// nascer enquanto tem gente perto. Pra abrir, alguém fica 45 s do lado das caixas (arrombando). Em 15 min o
    /// caminhão explode e as caixas somem.</summary>
    public partial class Plugin
    {
        class EstadoComboio
        {
            public Vector3 Pos; public InteractableVehicle Caminhao; public List<Transform> Caixas = new List<Transform>();
            public float Fim, ProximaHorda, Arrombando; public bool Aberto; public ulong QuemArromba;
        }

        EstadoComboio comboio;
        float proximoComboio = -1f;
        const float RaioComboio = 10f;

        Vector3? PontoDeEstrada()
        {
            var safes = Configuration.Instance.ZonasSeguras.Where(z => z.Definido).ToList();
            float DistSafe(Vector3 p) => safes.Count == 0 ? 999999f : safes.Min(z => DistanciaPlana(p, z.X, z.Z) - z.Raio);
            var pontos = new List<Vector3>();
            for (int i = 0; ; i++)
            {
                var r = LevelRoads.getRoad(i);
                if (r == null) break;
                foreach (var j in r.joints) pontos.Add(j.vertex);
            }
            var bons = pontos.Where(p => ZonaSeguraEm(p) == null && DistSafe(p) >= S.DropMinSafe && DistSafe(p) <= S.DropMaxSafe * 1.5f).ToList();
            if (bons.Count == 0) bons = pontos.Where(p => ZonaSeguraEm(p) == null && DistSafe(p) >= S.DropMinSafe).ToList();
            if (bons.Count == 0) return null;
            var p0 = bons[sorte.Next(bons.Count)];
            if (Physics.Raycast(p0 + Vector3.up * 30f, Vector3.down, out var hit, 60f, RayMasks.BLOCK_COLLISION, QueryTriggerInteraction.Ignore)) p0 = hit.point;
            return p0;
        }

        VehicleAsset CaminhaoMilitar()
        {
            if (S.ComboioVeiculo != 0 && Assets.find(EAssetType.VEHICLE, S.ComboioVeiculo) is VehicleAsset v) return v;
            var todos = new List<VehicleAsset>();
            Assets.find(todos);
            string[] nomes = { "military truck", "army truck", "humvee", "apc", "truck" };
            foreach (var n in nomes)
            {
                var a = todos.FirstOrDefault(x => x != null && (x.vehicleName ?? "").IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0 && x.engine == EEngine.CAR);
                if (a != null) return a;
            }
            return null;
        }

        static string RegiaoDe(Vector3 p)
        {
            LocationDevkitNode perto = null; float melhor = float.MaxValue;
            foreach (var n in LocationDevkitNodeSystem.Get().GetAllNodes())
            {
                var d = (n.transform.position - p).sqrMagnitude;
                if (d < melhor) { melhor = d; perto = n; }
            }
            return perto != null ? perto.locationName : $"X {p.x:0} Z {p.z:0}";
        }

        internal string ComecarComboio()
        {
            if (!Level.isLoaded) return "mapa carregando";
            if (comboio != null) return "ja tem um comboio no mapa";
            var ponto = PontoDeEstrada();
            if (ponto == null) return "nao achei estrada longe das safes";
            var asset = CaminhaoMilitar();
            var c = new EstadoComboio { Pos = ponto.Value, Fim = Time.realtimeSinceStartup + Math.Max(5, S.ComboioMinutos) * 60f };
            var ang = Quaternion.Euler(0, (float)(sorte.NextDouble() * 360), 0);
            if (asset != null)
            {
                c.Caminhao = VehicleManager.spawnVehicleV2(asset, c.Pos + Vector3.up * 1.5f, ang);
                if (c.Caminhao != null)
                {
                    // parado e quebrado: sem combustível e com pouca vida
                    VehicleManager.sendVehicleFuel(c.Caminhao, 0);
                    VehicleManager.sendVehicleHealth(c.Caminhao, (ushort)Math.Max(1, c.Caminhao.asset.health / 4));
                }
            }
            // caixas: a mesma caixa do airdrop, atrás do caminhão
            var bau = Assets.find(EAssetType.ITEM, (ushort)1374) as ItemBarricadeAsset;
            if (bau != null)
                for (int i = 0; i < Math.Max(1, S.ComboioCaixas); i++)
                {
                    var off = ang * new Vector3(-2f + i * 2f, 0, -5f);
                    var pos = c.Pos + off;
                    if (Physics.Raycast(pos + Vector3.up * 10f, Vector3.down, out var h, 30f, RayMasks.BLOCK_COLLISION, QueryTriggerInteraction.Ignore)) pos = h.point;
                    var t = BarricadeManager.dropNonPlantedBarricade(new Barricade(bau), pos, ang, 0, 0);
                    if (t == null) continue;
                    c.Caixas.Add(t);
                    if (t.GetComponent<InteractableStorage>() is InteractableStorage st)
                    {
                        st.items.clear();
                        foreach (var it in ConteudoAirdrop(new KitAirdrop { Nome = "auto:auto_evento" }))
                        {
                            if (!Itens.Existe(it.Id)) continue;
                            for (int q = 0; q < it.Quantidade; q++) st.items.tryAddItem(new Item(it.Id, true));
                        }
                    }
                }
            comboio = c;
            NascerZumbis(c.Pos, S.ComboioZumbis, true);
            var regiao = RegiaoDe(c.Pos);
            Chat.Todos(Chat.Cor("#FF3B30", Chat.Forte("COMBOIO EMBOSCADO ")) + Chat.Cor("#FFFFFF", $"perto de {Chat.Limpar(regiao)}! Caixas militares trancadas e muitos zumbis. Fique {S.ComboioSegundosArrombar} s do lado das caixas pra arrombar. O caminhao explode em {S.ComboioMinutos} min."));
            Rocket.Core.Logging.Logger.Log($"[Comboio] {asset?.vehicleName ?? "sem caminhao"} em X {c.Pos.x:0} Z {c.Pos.z:0} ({regiao}), {c.Caixas.Count} caixas");
            return $"comboio em {regiao} (X {c.Pos.x:0} Z {c.Pos.z:0})";
        }

        void AcabarComboio()
        {
            var c = comboio; comboio = null;
            if (c == null) return;
            try { if (c.Caminhao != null && !c.Caminhao.isExploded) VehicleManager.damage(c.Caminhao, 100000f, 1f, false, default(CSteamID), EDamageOrigin.Unknown); }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
            foreach (var t in c.Caixas)
            {
                try
                {
                    if (t == null) continue;
                    var d = BarricadeManager.FindBarricadeByRootTransform(t);
                    if (d != null && BarricadeManager.tryGetRegion(t, out var x, out var y, out var plant, out _)) BarricadeManager.destroyBarricade(d, x, y, plant);
                }
                catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
            }
            Chat.Todos(Chat.Cor("#FF3B30", Chat.Forte("COMBOIO: ")) + Chat.Cor("#FFFFFF", "o caminhao explodiu. Acabou o comboio."));
        }

        /// <summary>Caixa do comboio só abre depois de arrombada (chamado no onOpenStorageRequested).</summary>
        void AoAbrirCaixaComboio(CSteamID quem, InteractableStorage bau, ref bool permitir)
        {
            if (!permitir || comboio == null || comboio.Aberto || bau == null) return;
            if (!comboio.Caixas.Contains(bau.transform)) return;
            permitir = false;
            var sp = PlayerTool.getSteamPlayer(quem);
            if (sp?.player != null) sp.player.ServerShowHint("<color=#FF3B30><b>CAIXA TRANCADA</b></color>\nFique perto das caixas pra arrombar.", 3f);
        }

        void TickComboio()
        {
            float agora = Time.realtimeSinceStartup;
            if (comboio == null)
            {
                if (S.ComboioIntervaloMinutos <= 0 || !Level.isLoaded) return;
                if (proximoComboio < 0) { proximoComboio = agora + S.ComboioIntervaloMinutos * 60f; return; }
                if (agora < proximoComboio) return;
                proximoComboio = agora + S.ComboioIntervaloMinutos * 60f;
                if (Provider.clients.Count < Math.Max(1, S.ComboioMinJogadores)) return;
                Rocket.Core.Logging.Logger.Log("[Comboio auto] " + ComecarComboio());
                return;
            }
            var c = comboio;
            if (agora >= c.Fim || (c.Caminhao != null && c.Caminhao.isExploded)) { AcabarComboio(); return; }
            var perto = Provider.clients.Where(x => x.player != null && !x.player.life.isDead && (x.player.transform.position - c.Pos).sqrMagnitude < RaioComboio * RaioComboio).ToList();
            var chegando = Provider.clients.Any(x => x.player != null && (x.player.transform.position - c.Pos).sqrMagnitude < 120f * 120f);
            // zumbis voltam enquanto tem gente por perto
            if (chegando && agora >= c.ProximaHorda)
            {
                c.ProximaHorda = agora + 25f;
                NascerZumbis(c.Pos, Math.Max(2, S.ComboioZumbis / 3), true);
            }
            if (c.Aberto) return;
            if (perto.Count == 0) { if (c.Arrombando > 0) c.Arrombando = Math.Max(0, c.Arrombando - 2f); return; }
            c.Arrombando += 1f;
            int alvo = Math.Max(5, S.ComboioSegundosArrombar);
            foreach (var x in perto) x.player.ServerShowHint($"<color=#FFB020><b>ARROMBANDO AS CAIXAS</b></color>\n{Mathf.Min(100, (int)(c.Arrombando * 100 / alvo))}%  (fique perto)", 1.5f);
            if (c.Arrombando >= alvo)
            {
                c.Aberto = true;
                Chat.Todos(Chat.Cor("#FF3B30", Chat.Forte("COMBOIO: ")) + Chat.Cor("#FFFFFF", $"{Chat.Limpar(perto[0].playerID.characterName)} arrombou as caixas do comboio!"));
            }
        }
    }

    public class CmdComboio : ComandoSistema
    {
        public override string Name => "comboio";
        public override string Help => "Comeca um comboio emboscado (staff).";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (!P.Pode(p, "Mundo")) { Chat.Erro(p, "Voce nao tem permissao."); return; }
            var r = P.ComecarComboio();
            P.LogAdmin(p, "COMBOIO: " + r);
            Chat.Info(p, "Comboio: " + r);
        }
    }
}
