using System;
using System.Collections.Generic;
using System.Linq;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Avisos no HUD (efeito DZ_Avisos, S.AvisosEfeito): até 4 faixas pequenas empilhadas com contagem.
    /// Pedido de TPA, TPA enviado, teleporte (/home, /warp, /safe), em combate, modo raid, heli crash e a horda do evento.
    /// Elementos: Av_1..Av_4 (a faixa), Av_i_Texto (texto) e Av_i_Tpa, Av_i_Teleporte, Av_i_Combate, Av_i_Raid,
    /// Av_i_Heli, Av_i_Horda, Av_i_Limpa (ícone e borda de cada tipo; o plugin liga um por faixa).</summary>
    public partial class Plugin
    {
        const short AvisosKey = 5134;
        const int AvisosFaixas = 4;
        static readonly string[] AvisoTipos = { "Tpa", "Teleporte", "Combate", "Raid", "Heli", "Horda", "Limpa", "Base", "Comboio", "Evento", "Dica" };

        // base sendo atacada (dono, grupo e facção): até quando mostrar, quem ataca e onde
        readonly Dictionary<ulong, (float Ate, string Quem, Vector3 Onde)> baseAtacada = new Dictionary<ulong, (float, string, Vector3)>();
        readonly Dictionary<ulong, float> baseAtacadaMarcada = new Dictionary<ulong, float>();
        // dica que passa no topo junto com o anúncio do chat
        string dicaTopo; float dicaTopoAte;

        void MostrarDicaNoTopo(string texto)
        {
            dicaTopo = Chat.Limpar(System.Text.RegularExpressions.Regex.Replace(texto ?? "", "<.*?>", ""));
            dicaTopoAte = Time.realtimeSinceStartup + 10f;
        }

        /// <summary>Faixa BASE ATACADA pra todo mundo que é da base (chamado a cada golpe de raid valendo, no máximo 1x a cada 3 s por base).</summary>
        void AvisarBaseAtacada(ulong chave, ulong dono, ulong grupo, Faccao fac, string quem, Vector3 onde)
        {
            float agora = Time.realtimeSinceStartup;
            if (baseAtacadaMarcada.TryGetValue(chave, out var t) && agora < t) return;
            baseAtacadaMarcada[chave] = agora + 3f;
            foreach (var c in Provider.clients)
            {
                if (c?.player == null) continue;
                var cid = c.playerID.steamID.m_SteamID;
                if (cid == dono || (grupo != 0 && c.player.quests.groupID.m_SteamID == grupo) || (fac != null && FaccaoDe(cid) == fac))
                    baseAtacada[cid] = (agora + 45f, quem, onde);
            }
        }

        class EstadoAvisos
        {
            public bool Aberto;
            public readonly Dictionary<string, string> Textos = new Dictionary<string, string>();
            public readonly Dictionary<string, bool> Visiveis = new Dictionary<string, bool>();
        }
        readonly Dictionary<ulong, EstadoAvisos> avisos = new Dictionary<ulong, EstadoAvisos>();

        // horda do evento (heli crash ou comboio): quantos zumbis ainda estão vivos em volta
        Vector3? hordaCentro;
        int hordaVivos, hordaMax;
        float hordaLimpaAte;

        static string Tempo(float seg)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seg));
            return s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : s >= 60 ? $"{s / 60}:{s % 60:00}" : s + "s";
        }

        void ContarHorda()
        {
            Vector3? centro = heli != null && heli.Caiu ? heli.Queda : comboio != null ? comboio.Pos : (Vector3?)null;
            if (centro == null)
            {
                hordaCentro = null; hordaMax = 0; hordaVivos = 0;
                return;
            }
            if (hordaCentro == null || (hordaCentro.Value - centro.Value).sqrMagnitude > 1f) { hordaCentro = centro; hordaMax = 0; }
            int vivos = 0;
            if (ZombieManager.regions != null && LevelNavigation.tryGetBounds(centro.Value, out var b) && b < ZombieManager.regions.Length)
                foreach (var z in ZombieManager.regions[b].zombies)
                    if (z != null && !z.isDead && (z.transform.position - centro.Value).sqrMagnitude < 45f * 45f) vivos++;
            if (vivos > hordaMax) hordaMax = vivos;
            if (hordaVivos > 0 && vivos == 0) hordaLimpaAte = Time.realtimeSinceStartup + 6f;
            hordaVivos = vivos;
        }

        List<(string tipo, string texto)> AvisosDe(UnturnedPlayer p)
        {
            var l = new List<(string, string)>();
            var id = Id(p);
            float agora = Time.realtimeSinceStartup;
            if (teleportes.TryGetValue(id, out var tp)) l.Add(("Teleporte", $"TELEPORTE {Chat.Limpar(tp.Nome ?? "").ToUpperInvariant()}  {Tempo(tp.Fim - agora)}"));
            if (pedidosTpa.TryGetValue(id, out var ped) && ped.expira > agora)
                l.Add(("Tpa", $"PEDIDO DE TPA  {Tempo(ped.expira - agora)}  {Chat.Limpar(NomeDe(ped.de))}"));
            foreach (var kv in pedidosTpa)
                if (kv.Value.de == id && kv.Value.expira > agora) { l.Add(("Tpa", $"TPA ENVIADO  {Tempo(kv.Value.expira - agora)}")); break; }
            if (EmCombate(p, out var resta)) l.Add(("Combate", $"EM COMBATE  {Tempo(resta)}"));
            if (heli != null)
            {
                if (heli.Caiu)
                {
                    int d = Mathf.RoundToInt(Vector3.Distance(p.Position, heli.Queda));
                    l.Add(("Heli", $"HELI CRASH  {(d >= 1000 ? (d / 1000f).ToString("0.0") + " km" : d + " m")}  SOME EM {Tempo(heli.Fim - agora)}"));
                }
                else l.Add(("Heli", "HELI CAINDO"));
            }
            else if (heliComecaEm > 0) l.Add(("Heli", $"HELI CRASH  {Tempo(heliComecaEm - agora)}"));
            if (baseAtacada.TryGetValue(id, out var ba))
            {
                if (agora < ba.Ate)
                {
                    int d = Mathf.RoundToInt(Vector3.Distance(p.Position, ba.Onde));
                    l.Insert(0, ("Base", $"SUA BASE ESTA SENDO ATACADA  {(d >= 1000 ? (d / 1000f).ToString("0.0") + " km" : d + " m")}  {Chat.Limpar(ba.Quem)}"));
                }
                else baseAtacada.Remove(id);
            }
            if (comboio != null)
            {
                int d = Mathf.RoundToInt(Vector3.Distance(p.Position, comboio.Pos));
                l.Add(("Comboio", $"COMBOIO  {(d >= 1000 ? (d / 1000f).ToString("0.0") + " km" : d + " m")}  EXPLODE EM {Tempo(comboio.Fim - agora)}"));
            }
            if (eventoComecaEm > agora) l.Add(("Evento", $"{Chat.Limpar(eventoNome ?? "EVENTO").ToUpperInvariant()}  {Tempo(eventoComecaEm - agora)}"));
            if (dicaTopo != null && agora < dicaTopoAte) l.Add(("Dica", dicaTopo));
            if (hordaCentro != null && (p.Position - hordaCentro.Value).sqrMagnitude < 150f * 150f)
            {
                if (hordaVivos > 0) l.Add(("Horda", $"HORDA  {hordaVivos}/{Mathf.Max(hordaMax, hordaVivos)}"));
                else if (agora < hordaLimpaAte) l.Add(("Limpa", "HORDA LIMPA"));
            }
            if (S.RaidHorarioLigado && RaidAberto(out _))
            {
                var h = AgoraBrasil;
                int min = h.Hour * 60 + h.Minute, fim = S.RaidFimHora * 60;
                int faltam = (fim - min + 1440) % 1440, passou = (min - S.RaidInicioHora * 60 + 1440) % 1440;
                // só no primeiro minuto do raid e nos últimos 5 (pedido do dono: não fica parado no meio da tela)
                int seg = faltam * 60 - h.Second;
                if (passou < 1 || seg <= 300) l.Add(("Raid", $"MODO RAID  {Tempo(seg)}"));
            }
            return l;
        }

        void TickAvisos()
        {
            var ef = Efeito(S.AvisosEfeito);
            if (ef == null && avisos.Count == 0) return;
            ContarHorda();
            var vistos = new HashSet<ulong>();
            foreach (var sp in Provider.clients)
            {
                var pl = sp?.player;
                if (pl == null) continue;
                var id = sp.playerID.steamID.m_SteamID;
                vistos.Add(id);
                if (!avisos.TryGetValue(id, out var e)) avisos[id] = e = new EstadoAvisos();
                var p = UnturnedPlayer.FromPlayer(pl);
                var lista = ef == null || HudFixoEscondido(pl) ? new List<(string, string)>() : AvisosDe(p);
                var con = ConDe(pl);
                if (lista.Count == 0)
                {
                    if (e.Aberto) { try { EffectManager.askEffectClearByID(ef?.id ?? S.AvisosEfeito, con); } catch { } e.Aberto = false; }
                    continue;
                }
                if (!e.Aberto)
                {
                    EffectManager.sendUIEffect(ef.id, AvisosKey, con, true);
                    e.Aberto = true; e.Textos.Clear(); e.Visiveis.Clear();
                }
                void T(string nome, string v)
                {
                    if (e.Textos.TryGetValue(nome, out var old) && old == v) return;
                    e.Textos[nome] = v;
                    EffectManager.sendUIEffectText(AvisosKey, con, true, nome, v);
                }
                void V(string nome, bool v)
                {
                    if (e.Visiveis.TryGetValue(nome, out var old) && old == v) return;
                    e.Visiveis[nome] = v;
                    EffectManager.sendUIEffectVisibility(AvisosKey, con, true, nome, v);
                }
                for (int i = 1; i <= AvisosFaixas; i++)
                {
                    bool tem = i <= lista.Count;
                    V("Av_" + i, tem);
                    if (!tem) continue;
                    var (tipo, texto) = lista[i - 1];
                    T($"Av_{i}_Texto", texto);
                    foreach (var t in AvisoTipos) V($"Av_{i}_{t}", t == tipo);
                }
            }
            foreach (var id in avisos.Keys.ToList()) if (!vistos.Contains(id)) avisos.Remove(id);
        }
    }
}
