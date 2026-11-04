using System.Collections.Generic;
using Rocket.Unturned.Player;
using SDG.Unturned;

namespace SobrevivenciaCore
{
    /// <summary>Tela de missões (efeito S.MissoesEfeito), aberta pelo NPC de missão (51302) ou /missoes.
    /// Elementos: Mis_Data, Mis_Aviso, Mis_Fechar (botão) e, pra i = 1, 2, 3: Mis_i_Nivel, Mis_i_Titulo, Mis_i_Premio,
    /// Mis_i_Prog, Mis_i_Barra_1..10 (segmentos), Mis_i_Receber (botão), Mis_i_Recebida (selo "RECEBIDA").</summary>
    public partial class Plugin
    {
        const ushort NpcMissoes = 51302;
        const short MissoesKey = 5134;
        readonly HashSet<ulong> missoesAbertas = new HashSet<ulong>();

        /// <summary>Abre a tela. Devolve false se o mod ainda não tem a tela (aí o chamador mostra no chat).</summary>
        internal bool AbrirMissoes(UnturnedPlayer p)
        {
            if (!S.MissoesLigado || S.MissoesEfeito == 0 || !(Assets.find(EAssetType.EFFECT, S.MissoesEfeito) is EffectAsset)) return false;
            if (MenuAberto(p)) FecharMenu(p);
            EffectManager.sendUIEffect(S.MissoesEfeito, MissoesKey, Con(p), true);
            p.Player.enablePluginWidgetFlag(EPluginWidgetFlags.Modal);
            missoesAbertas.Add(Id(p));
            PreencherMissoes(p, "");
            return true;
        }

        void FecharMissoes(UnturnedPlayer p)
        {
            if (!missoesAbertas.Remove(Id(p))) return;
            EffectManager.askEffectClearByID(S.MissoesEfeito, Con(p));
            if (!MenuAberto(p) && !lojasAbertas.ContainsKey(Id(p)) && !AtmAberto(p)) p.Player.disablePluginWidgetFlag(EPluginWidgetFlags.Modal);
        }

        void PreencherMissoes(UnturnedPlayer p, string aviso)
        {
            var con = Con(p);
            void T(string n, string v) => EffectManager.sendUIEffectText(MissoesKey, con, true, n, v);
            void V(string n, bool v) => EffectManager.sendUIEffectVisibility(MissoesKey, con, true, n, v);
            var hoje = MissoesDeHoje(); var pr = ProgressoDe(p);
            T("Mis_Data", "MISSOES DE HOJE  " + AgoraBrasil.ToString("dd/MM"));
            T("Mis_Aviso", aviso ?? "");
            for (int i = 0; i < 3; i++)
            {
                string k = "Mis_" + (i + 1);
                var m = i < hoje.Count ? hoje[i] : null;
                if (m == null) { T(k + "_Titulo", "-"); V(k + "_Receber", false); V(k + "_Recebida", false); continue; }
                T(k + "_Nivel", m.Nivel == "facil" ? "FACIL" : m.Nivel == "media" ? "MEDIA" : "DIFICIL");
                T(k + "_Titulo", m.Texto);
                T(k + "_Premio", $"${m.Dinheiro:N0} + {m.Xp} XP");
                int feito = System.Math.Min(pr.Feito[i], m.Meta);
                T(k + "_Prog", $"{feito}/{m.Meta}");
                int seg = m.Meta <= 0 ? 10 : feito * 10 / m.Meta;
                for (int s = 1; s <= 10; s++) V($"{k}_Barra_{s}", s <= seg);
                V(k + "_Receber", !pr.Recebeu[i] && feito >= m.Meta);
                V(k + "_Recebida", pr.Recebeu[i]);
            }
        }

        bool CliqueMissoes(UnturnedPlayer p, string botao)
        {
            if (!botao.StartsWith("Mis_") || !missoesAbertas.Contains(Id(p))) return false;
            if (botao == "Mis_Fechar") { FecharMissoes(p); return true; }
            if (botao.Length == 13 && botao.EndsWith("_Receber") && int.TryParse(botao.Substring(4, 1), out var n) && n >= 1 && n <= 3)
            {
                var erro = MissoesReceber(p, n - 1);
                PreencherMissoes(p, erro ?? "Recebido! O dinheiro foi pro banco.");
            }
            return true;
        }
    }
}
