using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Painel lateral de estatísticas (efeito 51233, NOMES_hud_dz.md). Fica sempre na tela; /hud esconde.</summary>
    public partial class Plugin
    {
        public const ushort HudId = 51233;
        public const short HudKey = 5124;

        // por jogador: último texto enviado de cada elemento (só manda o que mudou)
        readonly Dictionary<ulong, Dictionary<string, string>> hudEnviado = new Dictionary<ulong, Dictionary<string, string>>();
        HashSet<ulong> hudDesligado;
        Dictionary<ulong, int> posicoes = new Dictionary<ulong, int>();
        float proximoRanking;

        static EffectAsset HudAsset => Assets.find(EAssetType.EFFECT, HudId) as EffectAsset;

        string ArqHudOff => Path.Combine(Directory, "hud_desligado.txt");

        HashSet<ulong> HudDesligado
        {
            get
            {
                if (hudDesligado != null) return hudDesligado;
                hudDesligado = new HashSet<ulong>();
                try
                {
                    if (File.Exists(ArqHudOff))
                        foreach (var l in File.ReadAllLines(ArqHudOff))
                            if (ulong.TryParse(l.Trim(), out var id)) hudDesligado.Add(id);
                }
                catch { }
                return hudDesligado;
            }
        }

        public bool AlternarHud(UnturnedPlayer p)
        {
            var id = Id(p);
            bool ligar = HudDesligado.Remove(id);
            if (!ligar) HudDesligado.Add(id);
            try { File.WriteAllLines(ArqHudOff, HudDesligado.Select(x => x.ToString())); } catch { }
            if (ligar) MostrarHud(p); else EsconderHud(p);
            return ligar;
        }

        public void MostrarHud(UnturnedPlayer p)
        {
            var asset = HudAsset;
            if (asset == null || p?.Player == null || HudDesligado.Contains(Id(p)) || MenuAberto(p)) return; // com o menu aberto o HUD some pra nao ficar por baixo dele
            EffectManager.SendUIEffect(asset, HudKey, Con(p), true);
            hudEnviado[Id(p)] = new Dictionary<string, string>();
            AtualizarHud(p);
        }

        void EsconderHud(UnturnedPlayer p)
        {
            hudEnviado.Remove(Id(p));
            EffectManager.askEffectClearByID(HudId, Con(p));
        }

        void HudT(UnturnedPlayer p, Dictionary<string, string> env, string nome, string texto)
        {
            if (env.TryGetValue(nome, out var antes) && antes == texto) return;
            env[nome] = texto;
            EffectManager.sendUIEffectText(HudKey, Con(p), true, nome, texto);
        }

        void HudV(UnturnedPlayer p, Dictionary<string, string> env, string nome, bool ligado)
        {
            var v = ligado ? "1" : "0";
            if (env.TryGetValue(nome, out var antes) && antes == v) return;
            env[nome] = v;
            EffectManager.sendUIEffectVisibility(HudKey, Con(p), true, nome, ligado);
        }

        void AtualizarHud(UnturnedPlayer p)
        {
            if (!hudEnviado.TryGetValue(Id(p), out var env)) return;
            var f = FichaDe(p);
            var k = ProximoKitPorAbates(f);
            if (k != null)
            {
                HudT(p, env, "Hud_Proximo", "Proximo: Kit " + Chat.Limpar(char.ToUpper(k.Nome[0]) + k.Nome.Substring(1)));
                HudT(p, env, "Hud_Proximo_Abates", $"{f.Abates}/{k.Abates} abates");
                int cheios = Mathf.Clamp(Mathf.FloorToInt(10f * f.Abates / Math.Max(1, k.Abates)), 0, 10);
                for (int i = 1; i <= 10; i++) HudV(p, env, "Hud_Barra_" + i, i <= cheios);
            }
            else
            {
                HudT(p, env, "Hud_Proximo", "Todos os kits liberados!");
                HudT(p, env, "Hud_Proximo_Abates", f.Abates + " abates");
                for (int i = 1; i <= 10; i++) HudV(p, env, "Hud_Barra_" + i, true);
            }
            HudT(p, env, "Hud_Abates", f.Abates.ToString("N0"));
            HudT(p, env, "Hud_Mortes", f.Mortes.ToString("N0"));
            HudT(p, env, "Hud_Xp", f.Xp.ToString("N0"));
            HudT(p, env, "Hud_Hs", f.PorcentoCabeca + "%");
            HudT(p, env, "Hud_Kd", f.Kd.ToString("0.00"));
            HudT(p, env, "Hud_Posicao", posicoes.TryGetValue(Id(p), out var pos) ? "#" + pos : "-");
            HudT(p, env, "Hud_Dica", "/hud para esconder");
            var fc = FaccaoDe(p);
            HudT(p, env, "Hud_Faccao", fc == null ? "" : fc.TagCor + " " + Chat.Cor("#9C9CA6", Chat.Limpar(fc.NomeCargo(fc.Membro(Id(p))?.Cargo ?? 5))));
            bool combate = EmCombate(p, out var resta);
            HudV(p, env, "Hud_Combate", combate);
            if (combate) HudT(p, env, "Hud_Combate_Texto", $"EM COMBATE {resta / 60}:{resta % 60:00}");
        }

        void TickHud()
        {
            if (Time.realtimeSinceStartup >= proximoRanking)
            {
                proximoRanking = Time.realtimeSinceStartup + 30f;
                int i = 1;
                posicoes = Stats.Todas.Where(x => x.Xp > 0).OrderByDescending(x => x.Xp).ToDictionary(x => x.Id, x => i++);
            }
            if (hudEnviado.Count == 0) return;
            foreach (var id in hudEnviado.Keys.ToList())
            {
                var sp = PlayerTool.getSteamPlayer(id);
                if (sp?.player == null) { hudEnviado.Remove(id); continue; }
                AtualizarHud(UnturnedPlayer.FromSteamPlayer(sp));
            }
        }
    }

    public class CmdHud : ComandoSistema
    {
        public override string Name => "hud";
        public override string Help => "Mostra ou esconde o painel de estatisticas.";
        public override List<string> Aliases => new List<string> { "statsui" };
        protected override void Rodar(UnturnedPlayer p, string[] a)
            => Chat.Info(p, P.AlternarHud(p) ? "Painel de estatisticas ligado." : "Painel de estatisticas escondido. Use /hud pra mostrar de novo.");
    }
}
