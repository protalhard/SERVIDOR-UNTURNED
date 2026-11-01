using System;
using System.Collections.Generic;
using System.Linq;
using Rocket.Unturned.Player;

namespace SobrevivenciaCore
{
    /// <summary>/faccao: tudo que a aba Facções do menu faz, pelo chat.</summary>
    public class CmdFaccao : ComandoSistema
    {
        public override string Name => "faccao";
        public override string Help => "Faccoes: /faccao (ajuda), criar, entrar, sair, aceitar, convidar, expulsar, cofre, bau...";
        public override string Syntax => "[criar|entrar|sair|lista|info|aceitar|recusar|convidar|expulsar|promover|rebaixar|lider|depositar|sacar|bau|nivel|modo|dissolver]";
        public override List<string> Aliases => new List<string> { "fac", "faccoes", "clan", "cla" };

        static string Junta(string[] a, int de) => string.Join(" ", a.Skip(de));

        void Res(UnturnedPlayer p, bool ok, string msg) { if (ok) Chat.Ok(p, msg); else Chat.Erro(p, msg); }

        Faccao Achar(string t)
        {
            t = (t ?? "").Trim();
            return P.Faccoes.FirstOrDefault(f => f.Tag.Equals(t, StringComparison.OrdinalIgnoreCase))
                ?? P.Faccoes.FirstOrDefault(f => f.Nome.Equals(t, StringComparison.OrdinalIgnoreCase))
                ?? P.Faccoes.FirstOrDefault(f => f.Nome.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>Acha um membro (ou quem pediu pra entrar) pelo nome.</summary>
        ulong Alvo(Faccao f, string nome)
        {
            if (f == null || string.IsNullOrWhiteSpace(nome)) return 0;
            var m = f.Membros.FirstOrDefault(x => x.Nome.Equals(nome, StringComparison.OrdinalIgnoreCase))
                 ?? f.Membros.FirstOrDefault(x => x.Nome.IndexOf(nome, StringComparison.OrdinalIgnoreCase) >= 0);
            if (m != null) return m.Id;
            var pd = f.Pedidos.FirstOrDefault(x => x.Nome.IndexOf(nome, StringComparison.OrdinalIgnoreCase) >= 0);
            return pd?.Id ?? 0;
        }

        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            var f = P.FaccaoDe(p);
            string sub = a.Length > 0 ? a[0].ToLowerInvariant() : "";
            bool ok; string msg;
            switch (sub)
            {
                case "criar":
                    if (a.Length < 3) { Chat.Erro(p, $"Use: /faccao criar <TAG> <nome>  (custa $ {S.FaccaoPreco:N0}, sai do bolso; pra pagar do banco use o /menu)"); return; }
                    ok = P.CriarFaccao(p, Junta(a, 2), a[1], Plugin.CoresFaccao[0], "", 1, false, out msg); Res(p, ok, msg); return;
                case "entrar": case "pedir":
                    if (a.Length < 2) { Chat.Erro(p, "Use: /faccao entrar <TAG>"); return; }
                    ok = P.PedirEntrada(p, Achar(Junta(a, 1)), "", out msg); Res(p, ok, msg); return;
                case "sair":
                    ok = P.SairDaFaccao(p, out msg); Res(p, ok, msg); return;
                case "lista":
                    foreach (var x in P.Faccoes.OrderByDescending(x => x.Nivel).ThenByDescending(x => x.Membros.Count).Take(10))
                        Chat.Para(p, $"{x.TagCor} {Chat.Limpar(x.Nome)} " + Cor(S.CorApagada, $"- {x.Membros.Count}/{P.NivelDe(x).Vagas} membros, nivel {x.Nivel}, {Plugin.NomesModo[x.Modo].ToLowerInvariant()}"));
                    if (!P.Faccoes.Any()) Chat.Info(p, "Ainda nao existe nenhuma faccao. Crie a sua no /menu > Faccoes.");
                    return;
                case "aceitar": case "recusar":
                    if (a.Length < 2) { Chat.Erro(p, $"Use: /faccao {sub} <jogador>   (convite recebido: /faccao entrar <TAG>)"); return; }
                    if (f == null && sub == "recusar") { ok = P.RecusarConvite(p, Achar(a[1]), out msg); Res(p, ok, msg); return; }
                    ok = P.ResponderPedido(p, Alvo(f, Junta(a, 1)), sub == "aceitar", out msg); Res(p, ok, msg); return;
                case "convidar":
                    if (a.Length < 2) { Chat.Erro(p, "Use: /faccao convidar <jogador>"); return; }
                    ok = P.Convidar(p, Junta(a, 1), out msg); Res(p, ok, msg); return;
                case "expulsar":
                    ok = P.Expulsar(p, Alvo(f, Junta(a, 1)), out msg); Res(p, ok, msg); return;
                case "promover": case "rebaixar":
                    ok = P.MudarCargo(p, Alvo(f, Junta(a, 1)), sub == "promover" ? -1 : 1, out msg); Res(p, ok, msg); return;
                case "lider":
                    ok = P.PassarLideranca(p, Alvo(f, Junta(a, 1)), out msg); Res(p, ok, msg); return;
                case "depositar": case "sacar":
                    if (a.Length < 2 || !LerValor(a[1], out var v)) { Chat.Erro(p, $"Use: /faccao {sub} <valor>  (usa o seu banco)"); return; }
                    ok = P.MexerCofre(p, v, sub == "depositar", out msg); Res(p, ok, msg); return;
                case "bau":
                    ok = P.AbrirBauFaccao(p, out msg); if (!ok) Chat.Erro(p, msg); return;
                case "nivel":
                    ok = P.SubirNivel(p, out msg); Res(p, ok, msg); return;
                case "modo":
                    ok = P.MudarModo(p, out msg); Res(p, ok, msg); return;
                case "tag":
                    ok = P.MudarTag(p, a.Length > 1 ? a[1] : "", out msg); Res(p, ok, msg); return;
                case "dissolver":
                    if (a.Length < 2 || a[1] != "confirmar") { Chat.Aviso(p, "Isso fecha a faccao: voce fica com a base e o dinheiro do cofre, os outros perdem o acesso. Pra confirmar: /faccao dissolver confirmar"); return; }
                    ok = P.Dissolver(p, out msg); Res(p, ok, msg); return;
                case "info":
                    var alvo = a.Length > 1 ? Achar(Junta(a, 1)) : f;
                    if (alvo == null) { Chat.Erro(p, "Faccao nao encontrada."); return; }
                    Chat.Para(p, $"{alvo.TagCor} {Cor("#FFFFFF", Chat.Limpar(alvo.Nome))} " + Cor(S.CorApagada, $"nivel {alvo.Nivel}, {alvo.Membros.Count}/{P.NivelDe(alvo).Vagas} membros, lider {Chat.Limpar(alvo.Lider?.Nome)}"));
                    if (!string.IsNullOrEmpty(alvo.Descricao)) Chat.Para(p, Cor(S.CorApagada, alvo.Descricao));
                    if (alvo == f) Chat.Para(p, Cor(S.CorApagada, "Membros: " + string.Join(", ", alvo.Membros.OrderBy(m => m.Cargo).Select(m => $"{Chat.Limpar(m.Nome)} ({alvo.NomeCargo(m.Cargo)})")) + $" | Cofre: $ {alvo.Cofre:N0}"));
                    return;
            }
            if (P.AbrirMenu(p, "Faccoes")) return;
            Chat.Para(p, Cor(S.CorPrincipal, "FACCOES ") + Cor(S.CorApagada, "/faccao criar <TAG> <nome> | entrar <TAG> | sair | lista | info | convidar | aceitar | expulsar | promover | rebaixar | depositar | sacar | bau | nivel | dissolver. Chat da faccao: /f <msg>"));
        }
    }

    public class CmdF : ComandoSistema
    {
        public override string Name => "f";
        public override string Help => "Fala so com a sua faccao.";
        public override string Syntax => "<mensagem>";
        protected override void Rodar(UnturnedPlayer p, string[] a) => P.ChatFaccao(p, string.Join(" ", a));
    }
}
