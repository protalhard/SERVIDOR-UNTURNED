using System;
using System.Collections.Generic;
using Rocket.Unturned.Player;
using SDG.NetTransport;
using SDG.Unturned;
using Steamworks;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Relógio tático: segurando a tecla de plugin (o jogador põe o H nela), o braço ergue o relógio
    /// (item do mod, se RelogioItem > 0) e a tela do relógio (efeito RelogioEfeito) mostra a hora do mundo
    /// e os 6 estados reais. Soltar, morrer, trocar de item, entrar em veículo ou nadar fecha.
    /// Elementos esperados no efeito: Rel_Hora (texto), e para cada estado E em Vida, Fome, Sede, Imunidade,
    /// Stamina, Oxigenio: Rel_E_Txt (texto "87%") e Rel_E_1 .. Rel_E_10 (segmentos da barra, liga/desliga).</summary>
    public partial class Plugin
    {
        const short RelogioKey = 5131;
        static readonly string[] RelEstados = { "Vida", "Fome", "Sede", "Imunidade", "Stamina", "Oxigenio" };

        class EstadoRelogio
        {
            public bool Segurando, Aberto;
            public float AbriuEm, ProximaAtualizacao;
            public bool ItemNaMao;               // equipamos o relógio
            public byte VoltarPagina = 255, VoltarX, VoltarY; // o que estava na mão antes
            public readonly Dictionary<string, string> Textos = new Dictionary<string, string>();
            public readonly Dictionary<string, bool> Visiveis = new Dictionary<string, bool>();
        }
        readonly Dictionary<ulong, EstadoRelogio> relogios = new Dictionary<ulong, EstadoRelogio>();
        bool relogioAvisouSemEfeito;

        EstadoRelogio Rel(ulong id)
        {
            if (!relogios.TryGetValue(id, out var e)) relogios[id] = e = new EstadoRelogio();
            return e;
        }

        void RelogioEntrou(UnturnedPlayer p)
        {
            if (Configuration.Instance.RelogioEsconderHudNativo && RelogioEfeitoAsset != null)
                EsconderHudNativo(p.Player, true);
        }

        void RelogioSaiu(UnturnedPlayer p) => relogios.Remove(p.CSteamID.m_SteamID);

        static void EsconderHudNativo(Player pl, bool esconder)
        {
            var f = EPluginWidgetFlags.ShowHealth | EPluginWidgetFlags.ShowFood | EPluginWidgetFlags.ShowWater |
                    EPluginWidgetFlags.ShowVirus | EPluginWidgetFlags.ShowStamina | EPluginWidgetFlags.ShowOxygen;
            if (esconder) pl.disablePluginWidgetFlag(f); else pl.enablePluginWidgetFlag(f);
        }

        EffectAsset RelogioEfeitoAsset
        {
            get
            {
                ushort id = Configuration.Instance.RelogioEfeito;
                return id == 0 ? null : Assets.find(EAssetType.EFFECT, id) as EffectAsset;
            }
        }

        // chamado pelo jogo a cada pacote de entrada, pra cada tecla de plugin (key 0 = Hotkey 1)
        void RelogioTecla(Player player, uint simulation, byte key, bool state)
        {
            try
            {
                var cfg = Configuration.Instance;
                if (cfg.RelogioTecla < 1 || cfg.RelogioTecla > 5 || key != cfg.RelogioTecla - 1 || player == null) return;
                var e = Rel(player.channel.owner.playerID.steamID.m_SteamID);
                if (state == e.Segurando) return;
                e.Segurando = state;
                if (state) AbrirRelogio(player, e); else FecharRelogio(player, e, true);
            }
            catch (Exception ex) { Rocket.Core.Logging.Logger.LogException(ex); }
        }

        static ITransportConnection ConDe(Player pl) => pl.channel.owner.transportConnection;

        /// <summary>Motivo pra não abrir agora, ou null se pode.</summary>
        static string RelogioBloqueado(Player pl)
        {
            if (pl.life.isDead) return "morto";
            if (pl.stance.stance == EPlayerStance.SWIM) return "nadando";
            if (pl.equipment.isBusy) return "ocupado";
            return null;
        }

        void AbrirRelogio(Player pl, EstadoRelogio e)
        {
            if (e.Aberto || RelogioBloqueado(pl) != null) return;
            var asset = RelogioEfeitoAsset;
            if (asset == null)
            {
                if (!relogioAvisouSemEfeito && Configuration.Instance.RelogioEfeito != 0)
                    Rocket.Core.Logging.Logger.LogWarning($"Relogio: efeito {Configuration.Instance.RelogioEfeito} nao existe (falta o mod atualizado?).");
                relogioAvisouSemEfeito = true;
                return;
            }
            e.Aberto = true;
            e.AbriuEm = Time.realtimeSinceStartup;
            e.Textos.Clear(); e.Visiveis.Clear();
            EffectManager.sendUIEffect(asset.id, RelogioKey, ConDe(pl), true);
            // braço erguendo o relógio: só a pé e se o item estiver no inventário
            ushort item = Configuration.Instance.RelogioItem;
            if (item != 0 && pl.movement.getVehicle() == null && AcharItem(pl, item, out byte pg, out byte x, out byte y))
            {
                var eq = pl.equipment;
                e.VoltarPagina = eq.asset != null ? eq.equippedPage : (byte)255;
                e.VoltarX = eq.equipped_x; e.VoltarY = eq.equipped_y;
                eq.ServerEquip(pg, x, y);
                e.ItemNaMao = true;
            }
            AtualizarRelogio(pl, e);
        }

        void FecharRelogio(Player pl, EstadoRelogio e, bool devolverArma)
        {
            if (!e.Aberto) return;
            e.Aberto = false;
            try { EffectManager.askEffectClearByID(Configuration.Instance.RelogioEfeito, ConDe(pl)); } catch { }
            if (e.ItemNaMao)
            {
                e.ItemNaMao = false;
                var eq = pl.equipment;
                // só mexe na mão se ainda estiver com o relógio (se trocou de item, respeita a troca)
                if (eq.asset != null && eq.asset.id == Configuration.Instance.RelogioItem && !pl.life.isDead)
                {
                    if (devolverArma && e.VoltarPagina != 255) eq.ServerEquip(e.VoltarPagina, e.VoltarX, e.VoltarY);
                    else eq.dequip();
                }
            }
        }

        static bool AcharItem(Player pl, ushort id, out byte page, out byte x, out byte y)
        {
            page = x = y = 0;
            for (byte pg = 0; pg < PlayerInventory.PAGES - 2; pg++)
            {
                var items = pl.inventory.items[pg];
                if (items == null) continue;
                foreach (var j in items.items)
                    if (j.item.id == id) { page = pg; x = j.x; y = j.y; return true; }
            }
            return false;
        }

        /// <summary>Hora do mundo: 0 = nascer do sol (06:00), bias = pôr do sol (18:00).</summary>
        static string HoraDoMundo()
        {
            float t = LightingManager.cycle > 0 ? (float)LightingManager.time / LightingManager.cycle : 0f;
            float bias = LevelLighting.bias > 0.05f && LevelLighting.bias < 0.95f ? LevelLighting.bias : 0.5f;
            float h = t < bias ? 6f + 12f * t / bias : 18f + 12f * (t - bias) / (1f - bias);
            int min = (int)(h * 60f) % (24 * 60);
            return $"{min / 60:00}:{min % 60:00}";
        }

        /// <summary>Roda no tick do admin (~a cada frame): atualiza quem está com o relógio aberto e fecha se precisar.</summary>
        void TickRelogio()
        {
            if (relogios.Count == 0) return;
            float agora = Time.realtimeSinceStartup;
            foreach (var sp in Provider.clients)
            {
                var pl = sp?.player;
                if (pl == null || !relogios.TryGetValue(sp.playerID.steamID.m_SteamID, out var e)) continue;
                if (e.Segurando && !e.Aberto && RelogioBloqueado(pl) == null) { AbrirRelogio(pl, e); continue; }
                if (!e.Aberto) continue;
                // cancela: morreu, foi nadar, ou trocou o relógio por outra coisa na mão
                if (pl.life.isDead || pl.stance.stance == EPlayerStance.SWIM ||
                    (e.ItemNaMao && (pl.equipment.asset == null || pl.equipment.asset.id != Configuration.Instance.RelogioItem) && agora - e.AbriuEm > 1.5f))
                {
                    e.ItemNaMao = false;
                    FecharRelogio(pl, e, false);
                    continue;
                }
                if (agora >= e.ProximaAtualizacao) AtualizarRelogio(pl, e);
            }
        }

        void AtualizarRelogio(Player pl, EstadoRelogio e)
        {
            e.ProximaAtualizacao = Time.realtimeSinceStartup + 0.5f;
            var con = ConDe(pl);
            void T(string nome, string v)
            {
                if (e.Textos.TryGetValue(nome, out var old) && old == v) return;
                e.Textos[nome] = v;
                EffectManager.sendUIEffectText(RelogioKey, con, true, nome, v);
            }
            void V(string nome, bool v)
            {
                if (e.Visiveis.TryGetValue(nome, out var old) && old == v) return;
                e.Visiveis[nome] = v;
                EffectManager.sendUIEffectVisibility(RelogioKey, con, true, nome, v);
            }
            T("Rel_Hora", HoraDoMundo());
            var l = pl.life;
            int[] valores = { l.health, l.food, l.water, l.virus, l.stamina, l.oxygen };
            for (int i = 0; i < RelEstados.Length; i++)
            {
                int v = Mathf.Clamp(valores[i], 0, 100);
                T($"Rel_{RelEstados[i]}_Txt", v + "%");
                int seg = Mathf.CeilToInt(v / 10f);
                for (int s = 1; s <= 10; s++) V($"Rel_{RelEstados[i]}_{s}", s <= seg);
            }
        }
    }
}
