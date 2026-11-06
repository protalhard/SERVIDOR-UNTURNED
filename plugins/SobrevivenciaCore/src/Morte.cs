using System;
using System.Collections.Generic;
using System.Linq;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Tela de morte (efeito DZ_Morte, S.MorteEfeito): VOCÊ MORREU, quem matou (facção, nome, arma, distância,
    /// parte do corpo, vida que sobrou nele), o resumo da vida (tempo vivo, abates, dano dado e levado) e os botões
    /// Renascer e Renascer na cama, com a contagem do jogo. Sem o efeito no servidor, fica a tela normal do Unturned.</summary>
    public partial class Plugin
    {
        const short MorteKey = 5135;

        class VidaAtual
        {
            public float Inicio = Time.realtimeSinceStartup;
            public int DanoDado, DanoLevado, Abates, Acertos;
        }
        class TelaMorte { public float MorreuEm; public bool Cama; public string Renascer, Casa; }

        readonly Dictionary<ulong, VidaAtual> vidas = new Dictionary<ulong, VidaAtual>();
        readonly Dictionary<ulong, TelaMorte> telasMorte = new Dictionary<ulong, TelaMorte>();

        VidaAtual VidaDe(ulong id) => vidas.TryGetValue(id, out var v) ? v : vidas[id] = new VidaAtual();

        void OuvirDano(Player pl)
        {
            if (pl?.life == null) return;
            pl.life.onHurt -= AoMachucar;
            pl.life.onHurt += AoMachucar;
        }

        void PararDeOuvirDano(Player pl)
        {
            if (pl?.life != null) pl.life.onHurt -= AoMachucar;
        }

        void AoMachucar(Player vitima, byte dano, Vector3 forca, EDeathCause causa, ELimb parte, CSteamID matador)
        {
            if (vitima == null || dano == 0) return;
            var idV = vitima.channel.owner.playerID.steamID.m_SteamID;
            VidaDe(idV).DanoLevado += dano;
            if (matador != CSteamID.Nil && matador.m_SteamID != idV && PlayerTool.getPlayer(matador) != null)
            {
                var v = VidaDe(matador.m_SteamID);
                v.DanoDado += dano; v.Acertos++;
            }
        }

        static string NomeParte(ELimb l)
        {
            switch (l)
            {
                case ELimb.SKULL: return "na cabeca";
                case ELimb.SPINE: case ELimb.LEFT_BACK: case ELimb.RIGHT_BACK: case ELimb.LEFT_FRONT: case ELimb.RIGHT_FRONT: return "no peito";
                case ELimb.LEFT_HAND: case ELimb.RIGHT_HAND: case ELimb.LEFT_ARM: case ELimb.RIGHT_ARM: return "no braco";
                case ELimb.LEFT_FOOT: case ELimb.RIGHT_FOOT: case ELimb.LEFT_LEG: case ELimb.RIGHT_LEG: return "na perna";
                default: return "";
            }
        }

        void AoMorrerTela(UnturnedPlayer vitima, EDeathCause causa, ELimb parte, CSteamID assassino)
        {
            var idV = Id(vitima);
            var vida = VidaDe(idV);
            vidas.Remove(idV);
            var matador = assassino != CSteamID.Nil && assassino != vitima.CSteamID ? PlayerTool.getPlayer(assassino) : null;
            if (matador != null) VidaDe(assassino.m_SteamID).Abates++;
            if (Efeito(S.MorteEfeito) == null || NaLuta(idV)) return;

            // o que aparece na tela, guardado agora (o matador pode trocar de arma ou sair)
            string por, arma = "-", dist = "-", vidaMatador = "-";
            if (matador != null)
            {
                var pm = UnturnedPlayer.FromPlayer(matador);
                var fc = FaccaoDe(pm);
                por = "Morto por " + (fc != null ? fc.TagCor + " " : "") + Chat.Cor(S.CorPrincipal, Chat.Limpar(pm.CharacterName))
                    + (NomeParte(parte).Length > 0 ? Chat.Cor(S.CorPerigo, " · " + NomeParte(parte)) : "");
                arma = Chat.Limpar(matador.equipment?.asset?.itemName ?? "Mao");
                dist = Mathf.RoundToInt(Vector3.Distance(pm.Position, vitima.Position)) + " m";
                vidaMatador = matador.life.health + "%";
            }
            else por = Chat.Limpar(char.ToUpper(DescreverMorte(causa)[0]) + DescreverMorte(causa).Substring(1));
            int vivoSeg = Mathf.RoundToInt(Time.realtimeSinceStartup - vida.Inicio);
            var f = FichaDe(vitima);
            StartCoroutine(Depois(1.2f, vitima, () => MostrarTelaMorte(vitima, matador != null, por, arma, dist, vidaMatador,
                Tempo(vivoSeg), vida.Abates, vida.DanoDado, vida.DanoLevado, f.Kd)));
        }

        void MostrarTelaMorte(UnturnedPlayer p, bool porJogador, string por, string arma, string dist, string vidaMatador,
            string vivo, int abates, int danoDado, int danoLevado, double kd)
        {
            var ef = Efeito(S.MorteEfeito);
            if (ef == null || p?.Player == null || !p.Player.life.isDead) return;
            var pl = p.Player;
            var con = ConDe(pl);
            if (MenuAberto(p)) FecharMenu(p);
            EffectManager.sendUIEffect(ef.id, MorteKey, con, true);
            pl.enablePluginWidgetFlag(EPluginWidgetFlags.Modal);
            pl.disablePluginWidgetFlag(EPluginWidgetFlags.ShowDeathMenu);
            void T(string n, string v) => EffectManager.sendUIEffectText(MorteKey, con, true, n, v);
            T("Morte_Titulo", "VOCE MORREU");
            T("Morte_Por", por);
            EffectManager.sendUIEffectVisibility(MorteKey, con, true, "Morte_Matador", porJogador);
            T("Morte_Arma", arma);
            T("Morte_Distancia", dist);
            T("Morte_Matador_Vida", vidaMatador);
            T("Morte_Vivo", vivo);
            T("Morte_Abates", abates.ToString());
            T("Morte_DanoDado", danoDado.ToString("N0"));
            T("Morte_DanoLevado", danoLevado.ToString("N0"));
            T("Morte_Kd", kd.ToString("0.00"));
            var tela = new TelaMorte { MorreuEm = pl.life.lastDeath, Cama = BarricadeManager.tryGetBed(p.CSteamID, out _, out _) };
            telasMorte[Id(p)] = tela;
            EffectManager.sendUIEffectVisibility(MorteKey, con, true, "Morte_Casa", tela.Cama);
            AtualizarTelaMorte(p, tela);
        }

        float EsperaRenascer(bool cama)
        {
            var g = Provider.modeConfigData.Gameplay;
            return cama && Provider.isPvP ? g.Timer_Home : g.Timer_Respawn;
        }

        void AtualizarTelaMorte(UnturnedPlayer p, TelaMorte t)
        {
            var con = ConDe(p.Player);
            float passou = Time.realtimeSinceStartup - t.MorreuEm;
            float r = EsperaRenascer(false) - passou, c = EsperaRenascer(true) - passou;
            var txtR = r > 0 ? $"RENASCER EM {Mathf.CeilToInt(r)}s" : "RENASCER";
            var txtC = c > 0 ? $"NA CAMA EM {Mathf.CeilToInt(c)}s" : "RENASCER NA CAMA";
            if (txtR != t.Renascer) { t.Renascer = txtR; EffectManager.sendUIEffectText(MorteKey, con, true, "Morte_Renascer_Texto", txtR); }
            if (t.Cama && txtC != t.Casa) { t.Casa = txtC; EffectManager.sendUIEffectText(MorteKey, con, true, "Morte_Casa_Texto", txtC); }
        }

        void FecharTelaMorte(UnturnedPlayer p)
        {
            if (p?.Player == null || !telasMorte.Remove(Id(p))) return;
            try { EffectManager.askEffectClearByID(S.MorteEfeito, ConDe(p.Player)); } catch { }
            p.Player.enablePluginWidgetFlag(EPluginWidgetFlags.ShowDeathMenu);
            if (!MenuAberto(p)) p.Player.disablePluginWidgetFlag(EPluginWidgetFlags.Modal);
        }

        /// <summary>Botões da tela de morte. Devolve true se era dela.</summary>
        bool CliqueMorte(UnturnedPlayer p, string botao)
        {
            if (botao != "Morte_Renascer" && botao != "Morte_Casa") return false;
            if (!telasMorte.TryGetValue(Id(p), out var t)) return true;
            if (!p.Player.life.isDead) { FecharTelaMorte(p); return true; }
            bool cama = botao == "Morte_Casa" && t.Cama;
            if (Time.realtimeSinceStartup - t.MorreuEm < EsperaRenascer(cama)) return true; // ainda contando
            FecharTelaMorte(p);
            p.Player.life.ServerRespawn(cama);
            return true;
        }

        void TickTelaMorte()
        {
            if (telasMorte.Count == 0) return;
            foreach (var id in telasMorte.Keys.ToList())
            {
                var sp = PlayerTool.getSteamPlayer(id);
                if (sp?.player == null) { telasMorte.Remove(id); continue; }
                var p = UnturnedPlayer.FromSteamPlayer(sp);
                if (!sp.player.life.isDead) { FecharTelaMorte(p); continue; }
                AtualizarTelaMorte(p, telasMorte[id]);
            }
        }
    }
}
