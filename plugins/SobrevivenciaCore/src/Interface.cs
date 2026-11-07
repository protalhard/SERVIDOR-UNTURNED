using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>HUD novo e avisos de tiro (mods do DZ Menu):
    /// - DZ_Status (S.StatusEfeito): quadrados de vida, fome, sede, energia e radiação embaixo à esquerda, faixas EM COMBATE e
    ///   SACOLA em cima deles, e o painel da arma embaixo à direita. Esconde as barras e a munição do próprio jogo.
    ///   Elementos: Status_Vida_Txt, Status_Fome_Txt, Status_Sede_Txt, Status_Energia_Txt, Status_Radiacao (+ _Txt),
    ///   Status_X_Baixo (borda vermelha abaixo de 25%), Status_Combate (+ _Txt), Status_Sacola (+ _Txt),
    ///   Arma, Arma_Nome, Arma_Municao, Arma_Max, Arma_Modo, Arma_Vazia.
    /// - DZ_Acerto (S.AcertoEfeito), pra quem atira: Acerto_Dano ("-54"), Acerto_Cabeca (cabecinha no meio), Acerto_Abate,
    ///   Acerto_Som (tique do acerto), Acerto_Som_Abate (som de abate) e Acerto_Som_Cabeca (som do headshot). Os sons são objetos com AudioSource
    ///   "Play On Awake" que começam desligados; o plugin liga o que deve tocar.
    /// - DZ_Atingido (S.AtingidoEfeito), pra quem levou o tiro: Atingido_Foto, Atingido_Nome, Atingido_Arma, Atingido_Dano,
    ///   Atingido_Cabeca.
    /// Cada jogador escolhe no Perfil do menu: HUD novo, números de dano e sons da interface (opcoes_interface.txt).</summary>
    public partial class Plugin
    {
        const short AcertoKey = 5136, AtingidoKey = 5137, StatusKey = 5138;
        const EPluginWidgetFlags BarrasDoJogo = EPluginWidgetFlags.ShowHealth | EPluginWidgetFlags.ShowFood |
            EPluginWidgetFlags.ShowWater | EPluginWidgetFlags.ShowVirus | EPluginWidgetFlags.ShowStamina;

        // ---------- Opções de cada jogador ----------

        public class OpcoesInterface { public bool HudNovo = true, Sons = true, Dano = true; }
        Dictionary<ulong, OpcoesInterface> opcoesInterface;
        string ArqOpcoes => Path.Combine(Directory, "opcoes_interface.txt");

        Dictionary<ulong, OpcoesInterface> Opcoes
        {
            get
            {
                if (opcoesInterface != null) return opcoesInterface;
                opcoesInterface = new Dictionary<ulong, OpcoesInterface>();
                try
                {
                    if (File.Exists(ArqOpcoes))
                        foreach (var l in File.ReadAllLines(ArqOpcoes))
                        {
                            var c = l.Split(' ');
                            if (c.Length >= 4 && ulong.TryParse(c[0], out var id))
                                opcoesInterface[id] = new OpcoesInterface { HudNovo = c[1] == "1", Sons = c[2] == "1", Dano = c[3] == "1" };
                        }
                }
                catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
                return opcoesInterface;
            }
        }

        public OpcoesInterface OpcoesDe(ulong id) => Opcoes.TryGetValue(id, out var o) ? o : new OpcoesInterface();

        /// <summary>Liga/desliga uma opção ("HudNovo", "Sons", "Dano"). Devolve o valor novo.</summary>
        public bool AlternarOpcao(UnturnedPlayer p, string qual)
        {
            var id = Id(p);
            if (!Opcoes.TryGetValue(id, out var o)) Opcoes[id] = o = new OpcoesInterface();
            bool novo;
            switch (qual)
            {
                case "HudNovo": novo = o.HudNovo = !o.HudNovo; break;
                case "Sons": novo = o.Sons = !o.Sons; break;
                case "Dano": novo = o.Dano = !o.Dano; break;
                default: return false;
            }
            if (o.HudNovo && o.Sons && o.Dano) Opcoes.Remove(id);
            try
            {
                File.WriteAllLines(ArqOpcoes, Opcoes.Select(kv =>
                    $"{kv.Key} {(kv.Value.HudNovo ? 1 : 0)} {(kv.Value.Sons ? 1 : 0)} {(kv.Value.Dano ? 1 : 0)}"));
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
            return novo;
        }

        // ---------- Sacola (as coisas que caíram na morte) ----------

        readonly Dictionary<ulong, (Vector3 Pos, float Fim)> sacolas = new Dictionary<ulong, (Vector3, float)>();

        /// <summary>Quanto tempo as coisas jogadas no chão ficam antes de sumir (Config do jogo, padrão 10 min).</summary>
        static float TempoSacola
        {
            get
            {
                float t = Provider.modeConfigData?.Items?.Despawn_Dropped_Time ?? 600f;
                return t > 0 ? t : 600f;
            }
        }

        void ComecarSacola(UnturnedPlayer p) => sacolas[Id(p)] = (p.Position, Time.realtimeSinceStartup + TempoSacola);

        int SacolaResta(ulong id)
        {
            if (!sacolas.TryGetValue(id, out var s)) return 0;
            int r = Mathf.CeilToInt(s.Fim - Time.realtimeSinceStartup);
            if (r <= 0) sacolas.Remove(id);
            return Mathf.Max(0, r);
        }

        // ---------- HUD de status e arma ----------

        class EstadoStatus
        {
            public bool Aberto, BarrasEscondidas;
            public readonly Dictionary<string, string> Textos = new Dictionary<string, string>();
            public readonly Dictionary<string, bool> Visiveis = new Dictionary<string, bool>();
        }
        readonly Dictionary<ulong, EstadoStatus> statusHud = new Dictionary<ulong, EstadoStatus>();

        void LigarInterface()
        {
            DamageTool.damageZombieRequested += AoBaterZumbiAcerto;
            DamageTool.damageAnimalRequested += AoBaterAnimalAcerto;
            PlayerInput.onPluginKeyTick += AoTeclaAtalho;
            InvokeRepeating(nameof(TickHudStatus), 2f, 0.25f);
        }

        void DesligarInterface()
        {
            DamageTool.damageZombieRequested -= AoBaterZumbiAcerto;
            DamageTool.damageAnimalRequested -= AoBaterAnimalAcerto;
            PlayerInput.onPluginKeyTick -= AoTeclaAtalho;
            CancelInvoke(nameof(TickHudStatus));
            foreach (var sp in Provider.clients)
                try
                {
                    if (sp?.player == null) continue;
                    sp.player.enablePluginWidgetFlag(BarrasDoJogo | EPluginWidgetFlags.ShowUseableGunStatus);
                    EffectManager.askEffectClearByID(S.StatusEfeito, sp.transportConnection);
                }
                catch { }
            statusHud.Clear();
        }

        void TickHudStatus()
        {
            try
            {
                var ef = Efeito(S.StatusEfeito);
                if (statusHud.Count > Provider.clients.Count)
                    foreach (var id in statusHud.Keys.ToList())
                        if (PlayerTool.getSteamPlayer(id) == null) statusHud.Remove(id);
                foreach (var sp in Provider.clients)
                {
                    var pl = sp?.player;
                    if (pl == null) continue;
                    var id = sp.playerID.steamID.m_SteamID;
                    if (!statusHud.TryGetValue(id, out var e)) statusHud[id] = e = new EstadoStatus();
                    bool ligado = ef != null && OpcoesDe(id).HudNovo;

                    // barras e munição do jogo: escondidas enquanto o nosso HUD estiver ligado pra essa pessoa
                    if (ligado != e.BarrasEscondidas)
                    {
                        e.BarrasEscondidas = ligado;
                        if (ligado) pl.disablePluginWidgetFlag(BarrasDoJogo | EPluginWidgetFlags.ShowUseableGunStatus);
                        else pl.enablePluginWidgetFlag(BarrasDoJogo | EPluginWidgetFlags.ShowUseableGunStatus);
                    }

                    bool mostrar = ligado && !HudFixoEscondido(pl);
                    if (!mostrar)
                    {
                        if (e.Aberto)
                        {
                            e.Aberto = false;
                            try { EffectManager.askEffectClearByID(ef?.id ?? S.StatusEfeito, sp.transportConnection); } catch { }
                        }
                        continue;
                    }
                    if (!e.Aberto)
                    {
                        e.Aberto = true;
                        e.Textos.Clear(); e.Visiveis.Clear();
                        EffectManager.sendUIEffect(ef.id, StatusKey, sp.transportConnection, true);
                    }
                    AtualizarStatus(sp, pl, id, e);
                }
            }
            catch (Exception ex) { Rocket.Core.Logging.Logger.LogException(ex); }
        }

        void AtualizarStatus(SteamPlayer sp, Player pl, ulong id, EstadoStatus e)
        {
            var con = sp.transportConnection;
            void T(string n, string v)
            {
                if (e.Textos.TryGetValue(n, out var a) && a == v) return;
                e.Textos[n] = v;
                EffectManager.sendUIEffectText(StatusKey, con, true, n, v);
            }
            void V(string n, bool v)
            {
                if (e.Visiveis.TryGetValue(n, out var a) && a == v) return;
                e.Visiveis[n] = v;
                EffectManager.sendUIEffectVisibility(StatusKey, con, true, n, v);
            }

            var vida = pl.life;
            T("Status_Vida_Txt", vida.health + "%");
            T("Status_Fome_Txt", vida.food + "%");
            T("Status_Sede_Txt", vida.water + "%");
            T("Status_Energia_Txt", vida.stamina + "%");
            V("Status_Vida_Baixo", vida.health < 25);
            V("Status_Fome_Baixo", vida.food < 25);
            V("Status_Sede_Baixo", vida.water < 25);
            V("Status_Energia_Baixo", vida.stamina < 25);
            // radiação/infecção: o quadrado só aparece quando a imunidade caiu
            V("Status_Radiacao", vida.virus < 100);
            if (vida.virus < 100) T("Status_Radiacao_Txt", vida.virus + "%");

            var up = UnturnedPlayer.FromSteamPlayer(sp);
            bool combate = EmCombate(up, out var resta);
            V("Status_Combate", combate);
            if (combate) T("Status_Combate_Txt", "EM COMBATE " + Tempo(resta));
            int sacola = SacolaResta(id);
            V("Status_Sacola", sacola > 0);
            if (sacola > 0) T("Status_Sacola_Txt", "SACOLA " + Tempo(sacola));

            // painel da arma: só com arma de fogo na mão
            var arma = pl.equipment?.asset as ItemGunAsset;
            V("Arma", arma != null);
            if (arma == null) return;
            var st = pl.equipment.state;
            int bala = st != null && st.Length > 10 ? st[10] : 0;
            int max = 0;
            if (st != null && st.Length > 9)
            {
                ushort mag = BitConverter.ToUInt16(st, 8);
                if (mag != 0 && Assets.find(EAssetType.ITEM, mag) is ItemMagazineAsset ma) max = ma.amount;
            }
            var modo = st != null && st.Length > 11 ? (EFiremode)st[11] : EFiremode.SAFETY;
            T("Arma_Nome", Chat.Limpar(arma.itemName));
            T("Arma_Municao", bala.ToString());
            T("Arma_Max", "/" + max);
            T("Arma_Modo", NomeModo(modo));
            V("Arma_Vazia", bala == 0);
        }

        static string NomeModo(EFiremode m)
        {
            switch (m)
            {
                case EFiremode.SAFETY: return "TRAVA";
                case EFiremode.SEMI: return "SEMI";
                case EFiremode.AUTO: return "AUTO";
                case EFiremode.BURST: return "RAJADA";
                default: return m.ToString();
            }
        }

        // ---------- Acerto (quem atira) e atingido (quem leva) ----------

        void MostrarAcerto(Player atirador, int dano, bool cabeca, bool abate)
        {
            var ef = Efeito(S.AcertoEfeito);
            var sp = atirador?.channel?.owner;
            if (ef == null || sp == null || dano <= 0) return;
            var op = OpcoesDe(sp.playerID.steamID.m_SteamID);
            if (!op.Dano && !cabeca && !abate) return;
            var con = sp.transportConnection;
            EffectManager.sendUIEffect(ef.id, AcertoKey, con, true);
            if (op.Dano) EffectManager.sendUIEffectText(AcertoKey, con, true, "Acerto_Dano", "-" + dano);
            if (cabeca) EffectManager.sendUIEffectVisibility(AcertoKey, con, true, "Acerto_Cabeca", true);
            if (abate) EffectManager.sendUIEffectVisibility(AcertoKey, con, true, "Acerto_Abate", true);
            if (op.Sons) EffectManager.sendUIEffectVisibility(AcertoKey, con, true, cabeca ? "Acerto_Som_Cabeca" : abate ? "Acerto_Som_Abate" : "Acerto_Som", true);
        }

        void MostrarAtingido(Player vitima, Player atirador, int dano, bool cabeca)
        {
            var ef = Efeito(S.AtingidoEfeito);
            var sp = vitima?.channel?.owner;
            var spA = atirador?.channel?.owner;
            if (ef == null || sp == null || spA == null || vitima.life.isDead) return;
            if (!OpcoesDe(sp.playerID.steamID.m_SteamID).Dano) return;
            var con = sp.transportConnection;
            EffectManager.sendUIEffect(ef.id, AtingidoKey, con, true);
            EffectManager.sendUIEffectText(AtingidoKey, con, true, "Atingido_Nome", Chat.Limpar(spA.playerID.characterName));
            EffectManager.sendUIEffectText(AtingidoKey, con, true, "Atingido_Arma", Chat.Limpar(atirador.equipment?.asset?.itemName ?? "Mao"));
            EffectManager.sendUIEffectText(AtingidoKey, con, true, "Atingido_Dano", "-" + dano);
            EffectManager.sendUIEffectVisibility(AtingidoKey, con, true, "Atingido_Cabeca", cabeca);
            if (avatares.TryGetValue(spA.playerID.steamID.m_SteamID, out var url) && !string.IsNullOrEmpty(url))
                EffectManager.sendUIEffectImageURL(AtingidoKey, con, true, "Atingido_Foto", url, true, false);
        }

        /// <summary>Chamado no dano final de um jogador (onHurt): acerto pra quem atirou, aviso pra quem levou.</summary>
        void AcertoEmJogador(Player vitima, byte dano, ELimb parte, CSteamID matador)
        {
            if (matador == CSteamID.Nil) return;
            var atirador = PlayerTool.getPlayer(matador);
            if (atirador == null || atirador == vitima) return;
            bool cabeca = parte == ELimb.SKULL;
            MostrarAcerto(atirador, dano, cabeca, vitima.life.health == 0 || vitima.life.isDead);
            MostrarAtingido(vitima, atirador, dano, cabeca);
        }

        void AoBaterZumbiAcerto(ref DamageZombieParameters par, ref bool permitir)
        {
            if (!permitir || par.zombie == null || par.zombie.isDead || !(par.instigator is Player pl)) return;
            int dano = Mathf.RoundToInt(par.damage * par.times);
            MostrarAcerto(pl, dano, par.limb == ELimb.SKULL, par.zombie.GetHealth() <= dano);
        }

        void AoBaterAnimalAcerto(ref DamageAnimalParameters par, ref bool permitir)
        {
            if (!permitir || par.animal == null || par.animal.isDead || !(par.instigator is Player pl)) return;
            int dano = Mathf.RoundToInt(par.damage * par.times);
            MostrarAcerto(pl, dano, par.limb == ELimb.SKULL, par.animal.GetHealth() <= dano);
        }

        // ---------- Teclas de atalho (Esc > Controles > Plugin Hotkey 1 a 5) ----------

        readonly Dictionary<ulong, byte> teclasApertadas = new Dictionary<ulong, byte>();

        void AoTeclaAtalho(Player pl, uint simulacao, byte tecla, bool apertada)
        {
            try
            {
                if (pl?.channel?.owner == null || tecla > 4) return;
                var id = pl.channel.owner.playerID.steamID.m_SteamID;
                teclasApertadas.TryGetValue(id, out var antes);
                byte bit = (byte)(1 << tecla);
                bool estava = (antes & bit) != 0;
                teclasApertadas[id] = apertada ? (byte)(antes | bit) : (byte)(antes & ~bit);
                if (!apertada || estava || pl.life.isDead) return; // só na hora que aperta
                var p = UnturnedPlayer.FromPlayer(pl);
                switch (tecla)
                {
                    case 0: AbrirMenu(p); break;
                    case 1: AbrirOuTrocarAba(p, "Kits"); break;
                    case 2: AbrirOuTrocarAba(p, "Vaults"); break;
                    case 3: AbrirOuTrocarAba(p, "Homes"); break;
                    case 4:
                        bool ligou = AlternarHud(p);
                        Chat.ParaIcone(p, "dica", ligou ? "Painel de stats ligado." : "Painel de stats desligado. Aperte a tecla 5 de novo pra voltar.");
                        break;
                }
            }
            catch (Exception ex) { Rocket.Core.Logging.Logger.LogException(ex); }
        }

        void AbrirOuTrocarAba(UnturnedPlayer p, string aba)
        {
            if (menus.TryGetValue(Id(p), out var e))
            {
                if (e.Aba == aba) FecharMenu(p);
                else MostrarAba(p, e, aba);
                return;
            }
            AbrirMenu(p, aba);
        }
    }
}
