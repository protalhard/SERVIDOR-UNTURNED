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
            public int DanoDado, DanoLevado, Abates, Acertos, DanoBichos;
        }
        class TelaMorte
        {
            public float MorreuEm; public bool Cama; public string Renascer, Casa, Aleatorio; public string[] Safes = new string[8];
            public ulong Matador; public string MatadorNome = ""; public bool Reportado; public string Motivo = "";
        }

        /// <summary>Onde o jogador escolheu renascer na tela de morte ("safe:Nome" ou "aleatorio"). O SpawnAoRenascer usa.</summary>
        readonly Dictionary<ulong, string> escolhaRenascer = new Dictionary<ulong, string>();

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
            try { AcertoEmJogador(vitima, dano, parte, matador); } catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
            if (matador != CSteamID.Nil && matador.m_SteamID != idV && PlayerTool.getPlayer(matador) != null)
            {
                var v = VidaDe(matador.m_SteamID);
                v.DanoDado += dano; v.Acertos++;
            }
        }

        /// <summary>Dano que o jogador deu em zumbis e animais (pra tela de morte).</summary>
        void AoBaterZumbiTela(ref DamageZombieParameters par, ref bool permitir)
        {
            if (permitir && par.instigator is Player pl && pl.channel?.owner != null)
                VidaDe(pl.channel.owner.playerID.steamID.m_SteamID).DanoBichos += Mathf.Clamp(Mathf.FloorToInt(par.damage * par.times), 0, 255);
        }

        void AoBaterAnimalTela(ref DamageAnimalParameters par, ref bool permitir)
        {
            if (permitir && par.instigator is Player pl && pl.channel?.owner != null)
                VidaDe(pl.channel.owner.playerID.steamID.m_SteamID).DanoBichos += Mathf.Clamp(Mathf.FloorToInt(par.damage * par.times), 0, 255);
        }

        /// <summary>A tela de morte do jogo (Home/Respawn) fica escondida pra todos enquanto a nossa existe no servidor.
        /// O jogo decide na hora da morte se abre a dele, então tem que estar desligada antes.</summary>
        void EsconderMorteDoJogo(Player pl)
        {
            if (pl == null) return;
            if (Efeito(S.MorteEfeito) != null) pl.disablePluginWidgetFlag(EPluginWidgetFlags.ShowDeathMenu);
            else pl.enablePluginWidgetFlag(EPluginWidgetFlags.ShowDeathMenu);
        }

        void LigarTelaMorte()
        {
            DamageTool.damageZombieRequested += AoBaterZumbiTela;
            DamageTool.damageAnimalRequested += AoBaterAnimalTela;
            foreach (var sp in Provider.clients) EsconderMorteDoJogo(sp?.player);
        }

        void DesligarTelaMorte()
        {
            DamageTool.damageZombieRequested -= AoBaterZumbiTela;
            DamageTool.damageAnimalRequested -= AoBaterAnimalTela;
            foreach (var sp in Provider.clients) try { sp?.player?.enablePluginWidgetFlag(EPluginWidgetFlags.ShowDeathMenu); } catch { }
        }

        /// <summary>Quem matou quando não foi jogador: o animal ou zumbi mais perto (nome e distância).</summary>
        string BichoQueMatou(Vector3 pos, EDeathCause causa, out string dist)
        {
            dist = "-";
            if (causa == EDeathCause.ANIMAL && AnimalManager.animals != null)
            {
                var a = AnimalManager.animals.Where(x => x != null && !x.isDead).OrderBy(x => (x.transform.position - pos).sqrMagnitude).FirstOrDefault();
                if (a != null && (a.transform.position - pos).sqrMagnitude < 30f * 30f)
                {
                    dist = Mathf.RoundToInt(Vector3.Distance(a.transform.position, pos)) + " m";
                    if (a.id == S.CriaturaInfectadoMilitar) return "Infectado militar";
                    if (a.id == S.CriaturaInfectadoRadioativo) return "Infectado radioativo";
                    if (a.id == S.CriaturaUrso) return "Urso radioativo";
                    return Chat.Limpar(a.asset?.animalName ?? "Animal");
                }
                return "Animal";
            }
            if (causa == EDeathCause.ZOMBIE)
            {
                var lista = new List<Zombie>();
                ZombieManager.getZombiesInRadius(pos, 30f * 30f, lista);
                var z = lista.Where(x => x != null && !x.isDead).OrderBy(x => (x.transform.position - pos).sqrMagnitude).FirstOrDefault();
                if (z != null)
                {
                    dist = Mathf.RoundToInt(Vector3.Distance(z.transform.position, pos)) + " m";
                    switch (z.speciality)
                    {
                        case EZombieSpeciality.MEGA: return "Zumbi chefe";
                        case EZombieSpeciality.SPRINTER: return "Zumbi corredor";
                        case EZombieSpeciality.CRAWLER: return "Zumbi rastejante";
                        case EZombieSpeciality.BURNER: return "Zumbi de fogo";
                        case EZombieSpeciality.ACID: return "Zumbi acido";
                        default: return z.speciality.ToString().StartsWith("BOSS") ? "Zumbi chefe" : "Zumbi";
                    }
                }
                return "Zumbi";
            }
            return null;
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
            if (Efeito(S.MorteEfeito) == null) return;

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
            else
            {
                por = Chat.Limpar(char.ToUpper(DescreverMorte(causa)[0]) + DescreverMorte(causa).Substring(1));
                var bicho = BichoQueMatou(vitima.Position, causa, out var d);
                if (bicho != null) { por = "Morto por " + Chat.Cor(S.CorPerigo, bicho); arma = bicho; dist = d; }
            }
            int vivoSeg = Mathf.RoundToInt(Time.realtimeSinceStartup - vida.Inicio);
            var f = FichaDe(vitima);
            ulong idM = matador != null ? assassino.m_SteamID : 0;
            string nomeM = matador != null ? Chat.Limpar(matador.channel.owner.playerID.characterName) : "";
            bool cabeca = parte == ELimb.SKULL;
            StartCoroutine(Depois(1.2f, vitima, () =>
            {
                MostrarTelaMorte(vitima, matador != null, por, arma, dist, vidaMatador,
                    Tempo(vivoSeg), vida.Abates, vida.DanoDado, vida.DanoLevado, f.Kd, vida.DanoBichos);
                MostrarMatador(vitima, idM, nomeM, cabeca);
            }));
        }

        void MostrarTelaMorte(UnturnedPlayer p, bool porJogador, string por, string arma, string dist, string vidaMatador,
            string vivo, int abates, int danoDado, int danoLevado, double kd, int danoBichos)
        {
            var ef = Efeito(S.MorteEfeito);
            if (ef == null || p?.Player == null || !p.Player.life.isDead) return;
            var pl = p.Player;
            var con = ConDe(pl);
            if (MenuAberto(p)) FecharMenu(p);
            EffectManager.sendUIEffect(ef.id, MorteKey, con, true);
            pl.enablePluginWidgetFlag(EPluginWidgetFlags.Modal);
            EsconderMorteDoJogo(pl);
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
            T("Morte_DanoBichos", danoBichos.ToString("N0"));
            T("Morte_Causa", arma);
            var tela = new TelaMorte { MorreuEm = pl.life.lastDeath, Cama = BarricadeManager.tryGetBed(p.CSteamID, out _, out _) };
            telasMorte[Id(p)] = tela;
            // casa: sempre aparece; sem cama fica apagada (cinza)
            EffectManager.sendUIEffectVisibility(MorteKey, con, true, "Morte_Casa", true);
            EffectManager.sendUIEffectVisibility(MorteKey, con, true, "Morte_Casa_Apagado", !tela.Cama);
            if (!tela.Cama) { tela.Casa = "SEM CASA"; T("Morte_Casa_Texto", "SEM CASA"); }
            var nomes = SafesComSpawn();
            for (int i = 0; i < 8; i++)
            {
                bool tem = i < nomes.Count;
                EffectManager.sendUIEffectVisibility(MorteKey, con, true, "Morte_Safe_" + (i + 1), tem);
                if (tem) T("Morte_Safe_" + (i + 1) + "_Nome", Chat.Limpar(nomes[i]).ToUpperInvariant());
            }
            AtualizarTelaMorte(p, tela);
        }

        /// <summary>Cartão QUEM TE MATOU: foto da Steam, nome, abates, K/D e HS% do matador, etiqueta NA CABEÇA e o botão
        /// de reportar (abre Morte_Report com o campo Morte_Report_Motivo). Elementos: Morte_Matador_Foto, Morte_Matador_Nome,
        /// Morte_Matador_Stats, Morte_Cabeca, Morte_Reportar (+ _Texto), Morte_Report, Morte_Report_Titulo.</summary>
        void MostrarMatador(UnturnedPlayer p, ulong idM, string nomeM, bool cabeca)
        {
            if (p?.Player == null || !telasMorte.TryGetValue(Id(p), out var tela)) return;
            var con = ConDe(p.Player);
            void T(string n, string v) => EffectManager.sendUIEffectText(MorteKey, con, true, n, v);
            void V(string n, bool v) => EffectManager.sendUIEffectVisibility(MorteKey, con, true, n, v);
            tela.Matador = idM; tela.MatadorNome = nomeM;
            V("Morte_Cabeca", cabeca && idM != 0);
            V("Morte_Reportar", idM != 0);
            V("Morte_Report", false);
            if (idM == 0) return;
            var fm = Stats.Obter(idM);
            T("Morte_Matador_Nome", nomeM);
            T("Morte_Matador_Stats", $"{fm.Abates:N0} abates · K/D {fm.Kd:0.00} · HS {fm.PorcentoCabeca}%");
            T("Morte_Reportar_Texto", "REPORTAR");
            if (avatares.TryGetValue(idM, out var url) && !string.IsNullOrEmpty(url))
                EffectManager.sendUIEffectImageURL(MorteKey, con, true, "Morte_Matador_Foto", url, true, false);
        }

        /// <summary>Botões extras da tela de morte: reportar, Discord e site.</summary>
        bool CliqueMorteExtra(UnturnedPlayer p, string botao)
        {
            switch (botao)
            {
                case "Morte_Discord": AbrirDiscord(p); return true;
                case "Morte_Site": AbrirSite(p); return true;
                case "Morte_Reportar": case "Morte_Report_Cancelar": case "Morte_Report_Enviar": break;
                default: return false;
            }
            if (!telasMorte.TryGetValue(Id(p), out var t) || t.Matador == 0) return true;
            var con = ConDe(p.Player);
            if (botao == "Morte_Report_Cancelar") { EffectManager.sendUIEffectVisibility(MorteKey, con, true, "Morte_Report", false); return true; }
            if (t.Reportado) { Chat.Aviso(p, "Voce ja reportou esse jogador. A staff vai olhar."); return true; }
            if (botao == "Morte_Reportar")
            {
                EffectManager.sendUIEffectText(MorteKey, con, true, "Morte_Report_Titulo", "REPORTAR " + t.MatadorNome);
                EffectManager.sendUIEffectVisibility(MorteKey, con, true, "Morte_Report", true);
                return true;
            }
            // enviar
            int resta = ReportResta(p);
            if (resta > 0) { Chat.Erro(p, $"Espere {resta}s pra reportar de novo."); return true; }
            var alvo = PlayerTool.getPlayer(new CSteamID(t.Matador));
            if (alvo == null) { Chat.Erro(p, "Ele saiu do servidor. Quando ele voltar, use /report nome motivo."); return true; }
            Reportar(p, UnturnedPlayer.FromPlayer(alvo), string.IsNullOrWhiteSpace(t.Motivo) ? "pela tela de morte" : t.Motivo.Trim());
            t.Reportado = true;
            EffectManager.sendUIEffectVisibility(MorteKey, con, true, "Morte_Report", false);
            EffectManager.sendUIEffectText(MorteKey, con, true, "Morte_Reportar_Texto", "REPORTADO");
            Chat.Ok(p, "Report enviado pra staff, com foto da tela dele. Obrigado!");
            return true;
        }

        /// <summary>Campo do motivo do report. Devolve true se era dele.</summary>
        bool DigitouMorte(UnturnedPlayer p, string campo, string texto)
        {
            if (campo != "Morte_Report_Motivo") return false;
            if (telasMorte.TryGetValue(Id(p), out var t)) t.Motivo = (texto ?? "").Length > 120 ? texto.Substring(0, 120) : texto ?? "";
            return true;
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
            var txtA = r > 0 ? $"ALEATORIO EM {Mathf.CeilToInt(r)}s" : "LUGAR ALEATORIO";
            if (txtA != t.Aleatorio) { t.Aleatorio = txtA; EffectManager.sendUIEffectText(MorteKey, con, true, "Morte_Aleatorio_Texto", txtA); }
            var nomes = SafesComSpawn();
            for (int i = 0; i < 8 && i < nomes.Count; i++)
            {
                // safe usada há pouco fica apagada com o tempo que falta; as outras mostram a contagem normal
                int espera = Mathf.Max(EsperaSafe(Id(p), nomes[i]), Mathf.CeilToInt(r));
                bool bloqueada = EsperaSafe(Id(p), nomes[i]) > 0;
                var txt = espera > 0 ? $"{espera / 60}:{espera % 60:00}" : "RENASCER AQUI";
                var chave = (bloqueada ? "x" : "") + txt;
                if (t.Safes[i] == chave) continue;
                t.Safes[i] = chave;
                EffectManager.sendUIEffectText(MorteKey, con, true, "Morte_Safe_" + (i + 1) + "_Info", txt);
                EffectManager.sendUIEffectVisibility(MorteKey, con, true, "Morte_Safe_" + (i + 1) + "_Apagado", bloqueada);
            }
        }

        void FecharTelaMorte(UnturnedPlayer p)
        {
            if (p?.Player == null || !telasMorte.Remove(Id(p))) return;
            try { EffectManager.askEffectClearByID(S.MorteEfeito, ConDe(p.Player)); } catch { }
            EsconderMorteDoJogo(p.Player);
            if (!MenuAberto(p)) p.Player.disablePluginWidgetFlag(EPluginWidgetFlags.Modal);
        }

        /// <summary>Botões da tela de morte. Devolve true se era dela.</summary>
        bool CliqueMorte(UnturnedPlayer p, string botao)
        {
            if (botao.StartsWith("Morte_") && CliqueMorteExtra(p, botao)) return true;
            if (botao != "Morte_Renascer" && botao != "Morte_Casa" && botao != "Morte_Aleatorio" && !botao.StartsWith("Morte_Safe_")) return false;
            if (!telasMorte.TryGetValue(Id(p), out var t)) return true;
            if (!p.Player.life.isDead) { FecharTelaMorte(p); return true; }
            if (botao == "Morte_Casa" && !t.Cama) return true; // sem casa: botão apagado
            bool cama = botao == "Morte_Casa";
            if (Time.realtimeSinceStartup - t.MorreuEm < EsperaRenascer(cama)) return true; // ainda contando
            string escolha = null;
            if (botao == "Morte_Aleatorio") escolha = "aleatorio";
            else if (botao.StartsWith("Morte_Safe_") && int.TryParse(botao.Substring(11), out var n))
            {
                var nomes = SafesComSpawn();
                if (n < 1 || n > nomes.Count) return true;
                if (EsperaSafe(Id(p), nomes[n - 1]) > 0) return true; // safe apagada
                escolha = "safe:" + nomes[n - 1];
            }
            if (escolha != null) escolhaRenascer[Id(p)] = escolha; else escolhaRenascer.Remove(Id(p));
            FecharTelaMorte(p);
            p.Player.life.ServerRespawn(cama);
            return true;
        }

        void TickTelaMorte()
        {
            // morto sem a nossa tela (entrou já morto, ou a tela não abriu): abre agora, senão fica sem botão de renascer
            if (Efeito(S.MorteEfeito) != null)
                foreach (var sp in Provider.clients)
                {
                    var pl = sp?.player;
                    if (pl == null || !pl.life.isDead || telasMorte.ContainsKey(sp.playerID.steamID.m_SteamID)) continue;
                    if (Time.realtimeSinceStartup - pl.life.lastDeath < 4f) continue;
                    var up = UnturnedPlayer.FromSteamPlayer(sp);
                    var f = FichaDe(up);
                    MostrarTelaMorte(up, false, "Voce esta morto", "-", "-", "-", "-", 0, 0, 0, f.Kd, 0);
                }
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
