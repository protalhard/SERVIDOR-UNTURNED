using System;
using System.Collections.Generic;
using System.Linq;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Janela do caixa eletrônico (efeito 51235, NOMES_atm_dz.md): F no ATM abre, deposita e saca.</summary>
    public partial class Plugin
    {
        public const ushort AtmId = 51235;
        public const short AtmKey = 5126;
        const long LimiteSaque = 20000;
        static readonly long[] ValoresRapidos = { 100, 500, 1000, 5000, 10000, 20000 };

        class EstadoAtm { public long Valor; public Vector3 Onde; }
        readonly Dictionary<ulong, EstadoAtm> atmsAbertos = new Dictionary<ulong, EstadoAtm>();
        readonly Dictionary<ulong, List<string>> extratos = new Dictionary<ulong, List<string>>();

        static void AT(UnturnedPlayer p, string nome, string texto) => EffectManager.sendUIEffectText(AtmKey, Con(p), true, nome, texto ?? "");

        public bool AtmAberto(UnturnedPlayer p) => atmsAbertos.ContainsKey(Id(p));

        /// <summary>Abre a janela. Devolve false se o mod ainda não tem a janela (aí usa o chat).</summary>
        public bool AbrirAtm(UnturnedPlayer p, Vector3 onde)
        {
            if (!(Assets.find(EAssetType.EFFECT, AtmId) is EffectAsset asset)) return false;
            if (MenuAberto(p)) FecharMenu(p);
            FecharLoja(p);
            atmsAbertos[Id(p)] = new EstadoAtm { Onde = onde };
            EffectManager.SendUIEffect(asset, AtmKey, Con(p), true);
            p.Player.enablePluginWidgetFlag(EPluginWidgetFlags.Modal);
            AT(p, "Atm_Titulo", "BANCO DZ");
            AT(p, "Atm_Jogador", Chat.Limpar(p.CharacterName));
            AT(p, "Atm_Limite", $"Limite por saque: $ {LimiteSaque:N0}");
            for (int i = 0; i < ValoresRapidos.Length; i++) AT(p, $"Atm_Rapido_{i + 1}_Texto", "$ " + ValoresRapidos[i].ToString("N0"));
            AT(p, "Atm_Aviso", "");
            AtualizarAtm(p);
            return true;
        }

        public void FecharAtm(UnturnedPlayer p)
        {
            if (!atmsAbertos.Remove(Id(p))) return;
            EffectManager.askEffectClearByID(AtmId, Con(p));
            if (!MenuAberto(p) && !lojasAbertas.ContainsKey(Id(p))) p.Player.disablePluginWidgetFlag(EPluginWidgetFlags.Modal);
        }

        void AtualizarAtm(UnturnedPlayer p)
        {
            if (!atmsAbertos.TryGetValue(Id(p), out var e)) return;
            AT(p, "Atm_Saldo", "$ " + Banco.Saldo(Id(p)).ToString("N0"));
            AT(p, "Atm_Bolso", "$ " + Dinheiro.NoBolso(p).ToString("N0"));
            AT(p, "Atm_Valor", e.Valor > 0 ? e.Valor.ToString() : "");
            extratos.TryGetValue(Id(p), out var ex);
            for (int i = 0; i < 5; i++) AT(p, $"Atm_Extrato_{i + 1}", ex != null && i < ex.Count ? ex[i] : "");
        }

        void AvisoAtm(UnturnedPlayer p, string texto, bool ok)
            => AT(p, "Atm_Aviso", Chat.Cor(ok ? S.CorOk : S.CorPerigo, texto));

        void Extrato(UnturnedPlayer p, string linha)
        {
            if (!extratos.TryGetValue(Id(p), out var ex)) extratos[Id(p)] = ex = new List<string>();
            ex.Insert(0, $"{AgoraBrasil:HH:mm}  {linha}");
            if (ex.Count > 5) ex.RemoveAt(5);
        }

        bool CliqueAtm(UnturnedPlayer p, string botao)
        {
            if (!botao.StartsWith("Atm_") || !atmsAbertos.TryGetValue(Id(p), out var e)) return false;
            if (botao == "Atm_Fechar") { FecharAtm(p); return true; }
            if ((p.Position - e.Onde).sqrMagnitude > 8f * 8f) { AvisoAtm(p, "Chegue mais perto do ATM.", false); return true; }
            var id = Id(p);
            if (botao.StartsWith("Atm_Rapido_") && int.TryParse(botao.Substring(11), out var n) && n >= 1 && n <= ValoresRapidos.Length)
            {
                e.Valor = ValoresRapidos[n - 1];
                AtualizarAtm(p);
                return true;
            }
            switch (botao)
            {
                case "Atm_Depositar":
                case "Atm_DepositarTudo":
                {
                    long bolso = Dinheiro.NoBolso(p);
                    long valor = botao == "Atm_DepositarTudo" ? bolso : e.Valor;
                    if (valor <= 0) { AvisoAtm(p, bolso <= 0 ? "Voce nao tem dinheiro no bolso." : "Digite um valor.", false); break; }
                    if (valor > bolso) { AvisoAtm(p, $"Voce so tem $ {bolso:N0} no bolso.", false); break; }
                    if (!Dinheiro.Pagar(p, valor)) { AvisoAtm(p, "Nao deu pra pegar o dinheiro da mochila.", false); break; }
                    Banco.Somar(id, valor);
                    Extrato(p, $"Deposito  + $ {valor:N0}");
                    AvisoAtm(p, $"Depositou $ {valor:N0}.", true);
                    e.Valor = 0;
                    break;
                }
                case "Atm_Sacar":
                case "Atm_SacarTudo":
                {
                    long saldo = Banco.Saldo(id);
                    long valor = botao == "Atm_SacarTudo" ? Math.Min(saldo, LimiteSaque) : e.Valor;
                    if (valor <= 0) { AvisoAtm(p, saldo <= 0 ? "Seu saldo esta zerado." : "Digite um valor.", false); break; }
                    if (valor > saldo) { AvisoAtm(p, $"Saldo insuficiente: $ {saldo:N0}.", false); break; }
                    if (valor > LimiteSaque) { AvisoAtm(p, $"Limite por saque: $ {LimiteSaque:N0}.", false); break; }
                    Banco.Somar(id, -valor);
                    Dinheiro.Dar(p, valor);
                    Extrato(p, $"Saque  - $ {valor:N0}");
                    AvisoAtm(p, $"Sacou $ {valor:N0}. As notas foram pra sua mochila.", true);
                    e.Valor = 0;
                    break;
                }
                default: return true;
            }
            AtualizarAtm(p);
            return true;
        }

        bool DigitouAtm(UnturnedPlayer p, string campo, string texto)
        {
            if (campo != "Atm_Valor" || !atmsAbertos.TryGetValue(Id(p), out var e)) return false;
            var limpo = new string((texto ?? "").Where(char.IsDigit).ToArray());
            e.Valor = long.TryParse(limpo, out var v) ? Math.Min(v, 100000000) : 0;
            return true;
        }
    }

    public class CmdAtm : ComandoSistema
    {
        public override string Name => "atm";
        public override string Help => "Abre o caixa eletronico (perto de um ATM).";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            var atm = P.AtmPerto(p.Position);
            if (atm == null) { Chat.Erro(p, "Voce precisa estar perto de um ATM (so tem na safe zone)."); return; }
            if (!P.AbrirAtm(p, p.Position)) Chat.Info(p, "Use /depositar valor e /sacar valor aqui.");
        }
    }
}
