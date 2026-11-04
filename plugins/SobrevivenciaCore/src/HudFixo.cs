using System;
using System.Collections.Generic;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Relógio fixo (modelo A) e minimapa, sempre na tela. Somem com o inventário, menus do servidor ou morto.
    /// Relógio (S.RelogioFixoEfeito): os mesmos elementos do relógio de tecla (Rel_Hora, Rel_E_Txt, Rel_E_1..10) e Rel_Dia (MANHÃ, TARDE...).
    /// Minimapa (S.MiniMapaEfeito): Mini_Mapa (Image, não RawImage: o cliente só aplica URL em UI.Image), Mini_Seta_0..7 (seta pra onde o
    /// jogador olha: 0 = norte, 2 = leste, 4 = sul, 6 = oeste) e Mini_Local (nome do lugar).</summary>
    public partial class Plugin
    {
        const short RelFixoKey = 5132, MiniKey = 5133;
        const int PassoMini = 8; // a imagem do minimapa muda a cada 8 m andados

        class EstadoHudFixo
        {
            public bool Rel, Mini, Gps = true;
            public float ProximoGps, ProximoAviso;
            public string Aviso = "";
            public string Url, Local;
            public int Seta = -1;
            public readonly Dictionary<string, string> Textos = new Dictionary<string, string>();
            public readonly Dictionary<string, bool> Visiveis = new Dictionary<string, bool>();
        }
        readonly Dictionary<ulong, EstadoHudFixo> hudFixo = new Dictionary<ulong, EstadoHudFixo>();
        bool miniLogou;

        static EffectAsset Efeito(ushort id) => id == 0 ? null : Assets.find(EAssetType.EFFECT, id) as EffectAsset;

        /// <summary>Esconde quando o jogador está no inventário, num menu do servidor ou morto.</summary>
        bool HudFixoEscondido(Player pl)
        {
            if (pl.life.isDead) return true;
            if (pl.animator != null && pl.animator.gesture == EPlayerGesture.INVENTORY_START) return true;
            if (pl.isPluginWidgetFlagActive(EPluginWidgetFlags.Modal)) return true;
            var up = UnturnedPlayer.FromPlayer(pl);
            return up != null && MenuAberto(up);
        }

        static bool TemGps(Player pl)
        {
            for (byte pg = 0; pg < PlayerInventory.PAGES - 2; pg++)
            {
                var items = pl.inventory.items[pg];
                if (items == null) continue;
                foreach (var j in items.items)
                    if (j?.GetAsset<ItemMapAsset>() is ItemMapAsset m && m.enablesMap) return true;
            }
            return false;
        }

        /// <summary>Posição no mapa (0..1, de cima pra baixo) e pra onde olha, igual ao mapa do jogo (CartographyVolume).</summary>
        Vector2 NoMapa(Vector3 pos, Vector3 frente, out Vector3 frenteMapa)
        {
            var vol = CartographyVolumeManager.Get()?.GetMainVolume();
            if (!miniLogou)
            {
                miniLogou = true;
                Rocket.Core.Logging.Logger.Log(vol != null
                    ? $"[Minimapa] volume do mapa em {vol.transform.position}, rot {vol.transform.eulerAngles}, escala {vol.transform.localScale}"
                    : $"[Minimapa] sem volume de mapa; usando o tamanho do nivel ({Level.size}, borda {Level.border})");
            }
            if (vol != null)
            {
                var l = vol.transform.InverseTransformPoint(pos);
                frenteMapa = Quaternion.Inverse(vol.transform.rotation) * frente;
                return new Vector2(l.x + 0.5f, 0.5f - l.z);
            }
            float tam = Level.size - Level.border * 2f;
            frenteMapa = frente;
            return new Vector2(pos.x / tam + 0.5f, 0.5f - pos.z / tam);
        }

        void TickHudFixo()
        {
            var rel = Efeito(S.RelogioFixoEfeito);
            var mini = Efeito(S.MiniMapaEfeito);
            if (rel == null && mini == null && hudFixo.Count == 0) return;
            float agora = Time.realtimeSinceStartup;
            var vistos = new HashSet<ulong>();
            foreach (var sp in Provider.clients)
            {
                var pl = sp?.player;
                if (pl == null) continue;
                ulong id = sp.playerID.steamID.m_SteamID;
                vistos.Add(id);
                if (!hudFixo.TryGetValue(id, out var e)) hudFixo[id] = e = new EstadoHudFixo();
                var con = ConDe(pl);
                bool esconder = HudFixoEscondido(pl);

                // relógio
                bool querRel = rel != null && !esconder;
                if (querRel && !e.Rel)
                {
                    EffectManager.sendUIEffect(rel.id, RelFixoKey, con, true);
                    EsconderHudNativo(pl, true);
                    e.Rel = true;
                    foreach (var k in new List<string>(e.Textos.Keys)) if (k.StartsWith("Rel_")) e.Textos.Remove(k);
                    foreach (var k in new List<string>(e.Visiveis.Keys)) if (k.StartsWith("Rel_")) e.Visiveis.Remove(k);
                }
                else if (!querRel && e.Rel)
                {
                    try { EffectManager.askEffectClearByID(rel?.id ?? S.RelogioFixoEfeito, con); } catch { }
                    e.Rel = false;
                }
                if (e.Rel) AtualizarRelogioFixo(pl, e);
                if (e.Rel) AvisoSaude(pl, e, agora);

                // minimapa
                if (mini != null && S.MiniMapaPrecisaGps && agora >= e.ProximoGps) { e.ProximoGps = agora + 3f; e.Gps = TemGps(pl); }
                bool querMini = mini != null && !esconder && (!S.MiniMapaPrecisaGps || e.Gps);
                if (querMini && !e.Mini)
                {
                    EffectManager.sendUIEffect(mini.id, MiniKey, con, true);
                    e.Mini = true; e.Url = null; e.Local = null; e.Seta = -1;
                }
                else if (!querMini && e.Mini)
                {
                    try { EffectManager.askEffectClearByID(mini?.id ?? S.MiniMapaEfeito, con); } catch { }
                    e.Mini = false;
                }
                if (e.Mini) AtualizarMinimapa(pl, e);
            }
            foreach (var id in new List<ulong>(hudFixo.Keys)) if (!vistos.Contains(id)) hudFixo.Remove(id);
        }

        // os ícones nativos ficam escondidos com o relógio; sangramento e perna quebrada viram aviso na tela
        static void AvisoSaude(Player pl, EstadoHudFixo e, float agora)
        {
            var l = pl.life;
            string aviso = l.isBleeding && l.isBroken ? "SANGRANDO E PERNA QUEBRADA: use bandagem e tala"
                : l.isBleeding ? "SANGRANDO: use bandagem ou curativo"
                : l.isBroken ? "PERNA QUEBRADA: use uma tala" : "";
            if (l.isDead) aviso = "";
            if (aviso.Length > 0 && (aviso != e.Aviso || agora >= e.ProximoAviso))
            {
                pl.ServerShowHint(aviso, 3f);
                e.ProximoAviso = agora + 2.5f;
            }
            e.Aviso = aviso;
        }

        void AtualizarRelogioFixo(Player pl, EstadoHudFixo e)
        {
            var con = ConDe(pl);
            void T(string nome, string v)
            {
                if (e.Textos.TryGetValue(nome, out var old) && old == v) return;
                e.Textos[nome] = v;
                EffectManager.sendUIEffectText(RelFixoKey, con, true, nome, v);
            }
            void V(string nome, bool v)
            {
                if (e.Visiveis.TryGetValue(nome, out var old) && old == v) return;
                e.Visiveis[nome] = v;
                EffectManager.sendUIEffectVisibility(RelFixoKey, con, true, nome, v);
            }
            var hora = HoraDoMundo();
            T("Rel_Hora", hora);
            int h = int.Parse(hora.Substring(0, 2));
            T("Rel_Dia", h < 5 ? "MADRUGADA" : h < 12 ? "MANHÃ" : h < 18 ? "TARDE" : "NOITE");
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

        void AtualizarMinimapa(Player pl, EstadoHudFixo e)
        {
            var con = ConDe(pl);
            var carro = pl.movement.getVehicle();
            var frente = carro != null ? carro.transform.forward : pl.transform.forward;
            var uv = NoMapa(pl.transform.position, frente, out var f);
            int x = Mathf.RoundToInt(uv.x * S.MiniMapaLargura / PassoMini) * PassoMini;
            int y = Mathf.RoundToInt(uv.y * S.MiniMapaAltura / PassoMini) * PassoMini;
            x = Mathf.Clamp(x, 0, S.MiniMapaLargura); y = Mathf.Clamp(y, 0, S.MiniMapaAltura);
            var url = (S.MiniMapaUrl ?? "").Replace("{x}", x.ToString()).Replace("{y}", y.ToString());
            if (url != e.Url && url.Length > 0)
            {
                e.Url = url;
                EffectManager.sendUIEffectImageURL(MiniKey, con, true, "Mini_Mapa", url, true, false);
            }
            float ang = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            int seta = ((int)Math.Round(ang / 45f) % 8 + 8) % 8;
            if (seta != e.Seta)
            {
                if (e.Seta >= 0) EffectManager.sendUIEffectVisibility(MiniKey, con, true, "Mini_Seta_" + e.Seta, false);
                else for (int i = 0; i < 8; i++) if (i != seta) EffectManager.sendUIEffectVisibility(MiniKey, con, true, "Mini_Seta_" + i, false);
                EffectManager.sendUIEffectVisibility(MiniKey, con, true, "Mini_Seta_" + seta, true);
                e.Seta = seta;
            }
            var local = RegiaoDe(pl.transform.position);
            if (local != e.Local)
            {
                e.Local = local;
                EffectManager.sendUIEffectText(MiniKey, con, true, "Mini_Local", local);
            }
        }

        /// <summary>Ao descarregar o plugin: devolve as barras do jogo e limpa as telas.</summary>
        void DesligarHudFixo()
        {
            foreach (var sp in Provider.clients)
            {
                if (sp?.player == null || !hudFixo.TryGetValue(sp.playerID.steamID.m_SteamID, out var e)) continue;
                try
                {
                    if (e.Rel) { EffectManager.askEffectClearByID(S.RelogioFixoEfeito, ConDe(sp.player)); EsconderHudNativo(sp.player, false); }
                    if (e.Mini) EffectManager.askEffectClearByID(S.MiniMapaEfeito, ConDe(sp.player));
                }
                catch { }
            }
            hudFixo.Clear();
        }
    }
}
