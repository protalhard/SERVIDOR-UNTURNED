using System;
using System.Collections.Generic;
using System.Linq;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Abas novas do menu (mod DZ Menu):
    /// - EVENTOS (Pag_Eventos): Ev_Heli_Status, Ev_Comboio_Status, Ev_Airdrop_Status, Ev_Clima_Status (contagem ou onde está
    ///   acontecendo agora), Ev_Heli_Ativo / Ev_Comboio_Ativo / Ev_Clima_Ativo (destaque de "acontecendo"), Ev_Online,
    ///   Ev_Ultimo_1..6 (o que aconteceu por último) e os botões Ev_Marcar_Heli / Ev_Marcar_Comboio (marca no mapa).
    /// - COMANDOS (Pag_Comandos): Cmd_Atalho_1..5 (teclas Plugin Hotkey 1 a 5) e Cmd_Grupo_1..6_Titulo / _Texto.
    /// - PERFIL ganha "Sua interface": botões Perfil_Op_HudNovo, Perfil_Op_Stats, Perfil_Op_Dano, Perfil_Op_Sons e o
    ///   visto de cada um (Perfil_Op_X_On).</summary>
    public partial class Plugin
    {
        const int EventosGuardados = 6;
        readonly List<(string Texto, float Quando)> ultimosEventos = new List<(string, float)>();

        /// <summary>Guarda o que aconteceu num evento pra aba EVENTOS (o mais novo em cima).</summary>
        void RegistrarEvento(string texto)
        {
            ultimosEventos.Insert(0, (texto, Time.realtimeSinceStartup));
            if (ultimosEventos.Count > EventosGuardados) ultimosEventos.RemoveAt(ultimosEventos.Count - 1);
        }

        static string Ha(float quando)
        {
            int s = Mathf.Max(0, Mathf.RoundToInt(Time.realtimeSinceStartup - quando));
            if (s < 60) return "agora";
            if (s < 3600) return $"ha {s / 60} min";
            return $"ha {s / 3600}h{s / 60 % 60:00}";
        }

        static string Contagem(float alvo) => "em " + Tempo(alvo - Time.realtimeSinceStartup);

        void PreencherEventos(UnturnedPlayer p)
        {
            float agora = Time.realtimeSinceStartup;

            // Heli Crash
            string heliTxt; bool heliAtivo = false;
            if (heli != null && heli.Caiu) { heliAtivo = true; heliTxt = C(Vermelho, "CAIU") + $" perto de {Chat.Limpar(heli.Regiao)} · some {Contagem(heli.Fim)}"; }
            else if (heli != null || heliComecaEm > 0) { heliAtivo = true; heliTxt = C(Ambar, "CAINDO AGORA") + " · olhe pro ceu"; }
            else if (S.HeliCrashIntervaloMinutos <= 0) heliTxt = C(Cinza, "desligado");
            else if (proximoHeli > agora) heliTxt = "Proximo " + C(Laranja, Contagem(proximoHeli));
            else heliTxt = "Proximo " + C(Laranja, "em breve");
            T(p, "Ev_Heli_Status", heliTxt);
            V(p, "Ev_Heli_Ativo", heliAtivo);
            V(p, "Ev_Marcar_Heli", heli != null && heli.Caiu);

            // Comboio
            string comTxt; bool comAtivo = comboio != null;
            if (comboio != null) comTxt = C(Vermelho, comboio.Aberto ? "ARROMBADO" : "EMBOSCADO") + $" perto de {Chat.Limpar(RegiaoDe(comboio.Pos))} · explode {Contagem(comboio.Fim)}";
            else if (S.ComboioIntervaloMinutos <= 0) comTxt = C(Cinza, "desligado");
            else if (proximoComboio > agora) comTxt = "Proximo " + C(Laranja, Contagem(proximoComboio));
            else comTxt = "Proximo " + C(Laranja, "em breve");
            T(p, "Ev_Comboio_Status", comTxt);
            V(p, "Ev_Comboio_Ativo", comAtivo);
            V(p, "Ev_Marcar_Comboio", comAtivo);

            // Airdrop automático
            string airTxt;
            if (S.AirdropAutoMinutos <= 0) airTxt = C(Cinza, "desligado");
            else if (proximoAirdropAuto > agora) airTxt = "Proximo " + C(Laranja, Contagem(proximoAirdropAuto));
            else airTxt = "Proximo " + C(Laranja, "em breve");
            T(p, "Ev_Airdrop_Status", airTxt);

            // Clima com baú
            string cliTxt; bool cliAtivo = eventoComecaEm > 0;
            if (eventoComecaEm > 0) cliTxt = C(Ambar, Chat.Limpar(eventoNome ?? "Evento")) + " " + Contagem(eventoComecaEm) + $" perto de {Chat.Limpar(RegiaoDe(eventoPonto))}";
            else if (S.EventoMinutos <= 0) cliTxt = C(Cinza, "desligado");
            else if (proximoEvento > agora) cliTxt = "Proximo " + C(Laranja, Contagem(proximoEvento));
            else cliTxt = "Proximo " + C(Laranja, "em breve");
            T(p, "Ev_Clima_Status", cliTxt);
            V(p, "Ev_Clima_Ativo", cliAtivo);

            T(p, "Ev_Online", $"{Provider.clients.Count} / {Provider.maxPlayers} online");
            for (int i = 0; i < EventosGuardados; i++)
            {
                bool tem = i < ultimosEventos.Count;
                V(p, "Ev_Ultimo_" + (i + 1), tem);
                if (tem) T(p, "Ev_Ultimo_" + (i + 1), ultimosEventos[i].Texto + C(Cinza, " · " + Ha(ultimosEventos[i].Quando)));
            }
            V(p, "Ev_Vazio", ultimosEventos.Count == 0);
        }

        bool CliqueEventos(UnturnedPlayer p, string botao)
        {
            Vector3? onde = null; string nome = null;
            if (botao == "Ev_Marcar_Heli" && heli != null && heli.Caiu) { onde = heli.Queda; nome = "HELI CRASH"; }
            else if (botao == "Ev_Marcar_Comboio" && comboio != null) { onde = comboio.Pos; nome = "COMBOIO"; }
            else if (!botao.StartsWith("Ev_")) return false;
            if (onde == null) return true;
            p.Player.quests.replicateSetMarker(true, onde.Value, nome);
            Aviso(p, nome + " marcado no seu mapa (tecla M).", true);
            return true;
        }

        static readonly string[,] GruposComandos =
        {
            { "TELEPORTE", "/safe ou /warp safe  ir pra safe zone\n/home [nome]  ir pra sua cama ou home\n/tpa nome  pedir TP pra um amigo\n/tpaceitar  /tpanegar" },
            { "KITS E LOJA", "/kits  ver os kits e o que falta\n/kit nome  pegar um kit\n/lojaxp  trocar XP por equipamento\n/loja  /comprar  /vender  /vendertudo" },
            { "DINHEIRO", "/saldo  ver seu dinheiro\n/atm  caixa eletronico\n/depositar  /sacar\n/leilao  leilao entre jogadores" },
            { "BASE E COFRE", "/sethome nome  salvar home na cama\n/homes  /delhome  /renamehome\n/vault  seu cofre pessoal\n/garagem  seus veiculos" },
            { "FACCAO", "/faccao  ajuda das faccoes\n/f mensagem  falar so com a faccao\n/raid  regras do raid\n/recompensa  colocar preco na cabeca" },
            { "INFO", "/menu  este menu\n/perfil  /top  suas stats e o ranking\n/missoes  missoes diarias\n/report nome motivo  denunciar hack\n/hud  esconder o painel de stats" },
        };

        void PreencherComandos(UnturnedPlayer p)
        {
            T(p, "Cmd_Atalho_1", "Abre e fecha o menu");
            T(p, "Cmd_Atalho_2", "Kits");
            T(p, "Cmd_Atalho_3", "Vaults");
            T(p, "Cmd_Atalho_4", "Homes e TP pra safe");
            T(p, "Cmd_Atalho_5", "Liga e desliga o painel de stats");
            T(p, "Cmd_Dica", "Configure em Esc > Options > Controls > Plugin Hotkey 1 a 5");
            for (int i = 0; i < GruposComandos.GetLength(0); i++)
            {
                T(p, $"Cmd_Grupo_{i + 1}_Titulo", GruposComandos[i, 0]);
                T(p, $"Cmd_Grupo_{i + 1}_Texto", GruposComandos[i, 1]);
            }
        }

        void PreencherOpcoesPerfil(UnturnedPlayer p)
        {
            var o = OpcoesDe(Id(p));
            V(p, "Perfil_Op_HudNovo_On", o.HudNovo);
            V(p, "Perfil_Op_Stats_On", !HudDesligado.Contains(Id(p)));
            V(p, "Perfil_Op_Dano_On", o.Dano);
            V(p, "Perfil_Op_Sons_On", o.Sons);
        }

        bool CliqueOpcoesPerfil(UnturnedPlayer p, string botao)
        {
            if (!botao.StartsWith("Perfil_Op_")) return false;
            string qual = botao.Substring(10);
            bool ligado;
            if (qual == "Stats")
            {
                ligado = AlternarHud(p);
                EsconderHud(p); // o menu está aberto: o painel volta quando fechar
            }
            else ligado = AlternarOpcao(p, qual);
            string nome = qual == "HudNovo" ? "HUD novo" : qual == "Stats" ? "Painel de stats" : qual == "Dano" ? "Numeros de dano" : "Sons da interface";
            Aviso(p, nome + (ligado ? " ligado." : " desligado."), true);
            PreencherOpcoesPerfil(p);
            return true;
        }
    }
}
