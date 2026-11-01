using System;
using System.Collections.Generic;
using System.Linq;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Aba "Facções" do /menu. Os nomes dos elementos estão em NOMES_faccoes_dz.md.</summary>
    public partial class Plugin
    {
        public class EstadoFac
        {
            public string Tela = "";
            public int PagLista, PagMem;
            public string Busca = "", PedidoMsg = "", Valor = "", Convidar = "";
            public string CriarNome = "", CriarTag = "", CriarDesc = "";
            public int CriarCor, CriarModo = 1;
            public bool CriarBanco = true;
            public ulong Alvo;
            public float ConfirmarDissolver;
        }

        const int FacLinhas = 6, FacMaxConvites = 5, FacPedidos = 4, FacMembros = 8, FacMaxRanking = 10;
        static readonly string[] TelasSem = { "Procurar", "Criar", "Convites", "Ranking" };
        static readonly string[] TelasCom = { "Minha", "Cofre", "Gerenciar", "Permissoes", "Ranking" };
        static readonly string[] TodasTelas = { "Procurar", "Criar", "Convites", "Minha", "Cofre", "Gerenciar", "Permissoes", "Ranking" };

        static string Quando(long unix)
        {
            var s = Math.Max(0, Agora - unix);
            if (s < 120) return "agora";
            if (s < 3600) return $"ha {s / 60} min";
            if (s < 86400) return $"ha {s / 3600}h";
            return $"ha {s / 86400} dias";
        }

        bool PodeGerenciar(Faccao f, ulong id) => TemPerm(f, id, PermFac.Recrutar) || TemPerm(f, id, PermFac.Expulsar) || TemPerm(f, id, PermFac.Promover);

        void PreencherFaccoes(UnturnedPlayer p, EstadoMenu e)
        {
            var f = FaccaoDe(p);
            var telas = f == null ? TelasSem : TelasCom;
            var st = e.Fac;
            if (!telas.Contains(st.Tela)) st.Tela = telas[0];
            bool lider = f?.Membro(Id(p))?.Cargo == 0;
            if ((st.Tela == "Gerenciar" && !PodeGerenciar(f, Id(p))) || (st.Tela == "Permissoes" && !lider)) st.Tela = "Minha";
            T(p, "Fac_Titulo", f == null ? "FACCOES" : $"{f.TagCor} {Chat.Limpar(f.Nome).ToUpperInvariant()}");
            foreach (var t in TodasTelas)
            {
                bool mostra = telas.Contains(t) && (t != "Gerenciar" || PodeGerenciar(f, Id(p))) && (t != "Permissoes" || lider);
                V(p, "Fac_Tab_" + t, mostra);
                V(p, "Fac_Tab_" + t + "_Sel", t == st.Tela);
                V(p, "Fac_Pag_" + t, t == st.Tela);
            }
            switch (st.Tela)
            {
                case "Procurar": FacProcurar(p, st); break;
                case "Criar": FacCriar(p, st); break;
                case "Convites": FacConvites(p); break;
                case "Minha": FacMinha(p, f, st); break;
                case "Cofre": FacCofre(p, f); break;
                case "Gerenciar": FacGerenciar(p, f, st); break;
                case "Permissoes": FacPermissoes(p, f); break;
                case "Ranking": FacRanking(p); break;
            }
        }

        List<Faccao> ListaBusca(EstadoFac st)
            => fac.Lista.Where(f => st.Busca == "" || f.Nome.IndexOf(st.Busca, StringComparison.OrdinalIgnoreCase) >= 0 || f.Tag.IndexOf(st.Busca, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(f => f.Modo == 2).ThenByDescending(f => f.Nivel).ThenByDescending(f => f.Membros.Count).ToList();

        void FacProcurar(UnturnedPlayer p, EstadoFac st)
        {
            var lista = ListaBusca(st);
            int pags = Math.Max(1, (lista.Count + FacLinhas - 1) / FacLinhas);
            st.PagLista = Mathf.Clamp(st.PagLista, 0, pags - 1);
            var id = Id(p);
            for (int i = 0; i < FacLinhas; i++)
            {
                string n = "Fac_Lista_" + (i + 1);
                int idx = st.PagLista * FacLinhas + i;
                if (idx >= lista.Count) { V(p, n, false); continue; }
                var f = lista[idx];
                int vagas = NivelDe(f).Vagas;
                V(p, n, true);
                T(p, n + "_Tag", $"<color={f.Cor}><b>{f.Tag}</b></color>");
                T(p, n + "_Nome", Chat.Limpar(f.Nome).ToUpperInvariant());
                T(p, n + "_Info", $"{f.Membros.Count}/{vagas} membros  •  nivel {f.Nivel}  •  lider {Chat.Limpar(f.Lider?.Nome)}  •  " +
                    C(f.Modo == 0 ? Verde : f.Modo == 1 ? Ambar : Vermelho, NomesModo[f.Modo]));
                T(p, n + "_Desc", string.IsNullOrEmpty(f.Descricao) ? C(Cinza, "Sem descricao.") : f.Descricao);
                string botao = f.Convites.Any(c => c.Id == id) ? C(Verde, "ACEITAR CONVITE")
                    : f.Membros.Count >= vagas ? C(Cinza, "CHEIA")
                    : f.Pedidos.Any(x => x.Id == id) ? C(Ambar, "PEDIDO ENVIADO")
                    : f.Modo == 0 ? "ENTRAR" : f.Modo == 1 ? "PEDIR PRA ENTRAR" : C(Cinza, "SO CONVITE");
                T(p, n + "_Pedir_Texto", botao);
            }
            V(p, "Fac_Lista_Vazio", lista.Count == 0);
            T(p, "Fac_Lista_Vazio", st.Busca == "" ? "Ainda nao existe nenhuma faccao. Seja o primeiro: aba CRIAR." : "Nenhuma faccao com esse nome.");
            T(p, "Fac_Lista_Pagina", $"{st.PagLista + 1}/{pags}");
            V(p, "Fac_Lista_Anterior", pags > 1);
            V(p, "Fac_Lista_Proximo", pags > 1);
            var r = EsperaRestante(id);
            T(p, "Fac_Lista_Dica", r > 0 ? C(Ambar, $"Voce saiu de uma faccao: pode entrar em outra daqui a {Chat.Tempo(r)}.") : "Escreva uma mensagem curta pro lider (opcional) e clique em PEDIR PRA ENTRAR.");
        }

        void FacCriar(UnturnedPlayer p, EstadoFac st)
        {
            long preco = S.FaccaoNiveis.OrderBy(n => n.Nivel).First().Preco;
            var cor = CoresFaccao[st.CriarCor % CoresFaccao.Length];
            var tag = st.CriarTag.Trim().ToUpperInvariant();
            T(p, "Fac_Criar_Preco", $"Custa {C(Laranja, "$ " + preco.ToString("N0"))}");
            T(p, "Fac_Criar_Saldo", $"Banco: $ {Banco.Saldo(Id(p)):N0}   Bolso: $ {Dinheiro.NoBolso(p):N0}");
            T(p, "Fac_Criar_Cor_Texto", $"<color={cor}>■ COR DA TAG</color>");
            T(p, "Fac_Criar_Modo_Texto", NomesModo[st.CriarModo]);
            T(p, "Fac_Criar_Pagar_Texto", st.CriarBanco ? "PAGAR DO BANCO" : "PAGAR DO BOLSO");
            T(p, "Fac_Criar_Previa", (tag == "" && st.CriarNome == "") ? C(Cinza, "[TAG] Nome da faccao")
                : $"<color={cor}>[{Chat.Limpar(tag)}]</color> {Chat.Limpar(st.CriarNome)}");
            T(p, "Fac_Criar_Dica", "Nome: 3 a 20 letras. TAG: 2 a 4 letras. Aberta = entra direto. Por pedido = voce aceita. Fechada = so convite.");
        }

        void FacConvites(UnturnedPlayer p)
        {
            var id = Id(p);
            var conv = fac.Lista.Where(f => f.Convites.Any(c => c.Id == id)).ToList();
            for (int i = 0; i < FacMaxConvites; i++)
            {
                string n = "Fac_Conv_" + (i + 1);
                if (i >= conv.Count) { V(p, n, false); continue; }
                var f = conv[i];
                V(p, n, true);
                T(p, n + "_Nome", $"{f.TagCor} {Chat.Limpar(f.Nome)}");
                T(p, n + "_Info", $"{f.Membros.Count}/{NivelDe(f).Vagas} membros  •  nivel {f.Nivel}  •  convite {Quando(f.Convites.First(c => c.Id == id).Quando)}");
            }
            V(p, "Fac_Conv_Vazio", conv.Count == 0);
            T(p, "Fac_Conv_Vazio", "Nenhum convite. Peca pra um lider te convidar, ou procure uma faccao aberta.");
        }

        void FacMinha(UnturnedPlayer p, Faccao f, EstadoFac st)
        {
            var eu = f.Membro(Id(p));
            var nivel = NivelDe(f);
            var prox = ProximoNivel(f);
            T(p, "Fac_Minha_Tag", $"<color={f.Cor}><b>[{f.Tag}]</b></color>");
            T(p, "Fac_Minha_Nome", Chat.Limpar(f.Nome).ToUpperInvariant());
            T(p, "Fac_Minha_Desc", string.IsNullOrEmpty(f.Descricao) ? C(Cinza, "Sem descricao.") : f.Descricao);
            T(p, "Fac_Minha_Nivel", "NIVEL " + f.Nivel);
            T(p, "Fac_Minha_Vagas", $"{f.Membros.Count}/{nivel.Vagas}");
            T(p, "Fac_Minha_Cofre", "$ " + f.Cofre.ToString("N0"));
            T(p, "Fac_Minha_Lider", Chat.Limpar(f.Lider?.Nome));
            T(p, "Fac_Minha_Cargo", "Seu cargo: " + C(Laranja, Chat.Limpar(f.NomeCargo(eu.Cargo))));
            T(p, "Fac_Minha_Online", C(Verde, "● ") + Online(f).Count() + " online");
            T(p, "Fac_Minha_Protecao", "Protecao da base: " + (FaccaoAbandonada(f.Id) ? C(Vermelho, $"perdida ({S.FaccaoDiasProtecao}+ dias sem ninguem)")
                : C(Verde, "ativa") + C(Cinza, $"  (Claim Flag, todos offline ha {S.RaidMinutosOffline}+ min)")));
            T(p, "Fac_Minha_Membros", string.Join("\n", f.Membros.OrderBy(m => m.Cargo).ThenBy(m => m.Nome).Select(m =>
                (PlayerTool.getSteamPlayer(m.Id) != null ? C(Verde, "● ") : C(Cinza, "● ")) + Chat.Limpar(m.Nome) + "  " + C(Cinza, Chat.Limpar(f.NomeCargo(m.Cargo))))));
            V(p, "Fac_Bau", S.FaccaoBauLigado);
            T(p, "Fac_Bau_Texto", $"BAU DA FACCAO ({S.FaccaoBauLargura}x{S.FaccaoBauAltura})");
            bool lider = eu.Cargo == 0;
            V(p, "Fac_Sair", !lider);
            V(p, "Fac_Subir", lider && prox != null);
            if (prox != null) T(p, "Fac_Subir_Texto", $"SUBIR PRO NIVEL {prox.Nivel}  ($ {prox.Preco:N0} do cofre)");
            V(p, "Fac_Dissolver", lider);
            T(p, "Fac_Dissolver_Texto", Time.realtimeSinceStartup < st.ConfirmarDissolver ? C(Vermelho, "CLIQUE DE NOVO PRA CONFIRMAR") : "DISSOLVER");
        }

        void FacCofre(UnturnedPlayer p, Faccao f)
        {
            var id = Id(p);
            T(p, "Fac_Cofre_Saldo", "$ " + f.Cofre.ToString("N0"));
            T(p, "Fac_Cofre_Banco", "Seu banco: $ " + Banco.Saldo(id).ToString("N0"));
            V(p, "Fac_Cofre_Depositar", TemPerm(f, id, PermFac.Depositar));
            V(p, "Fac_Cofre_Sacar", TemPerm(f, id, PermFac.Sacar));
            T(p, "Fac_Cofre_Historico", f.Historico.Count == 0 ? C(Cinza, "Nada ainda.") :
                string.Join("\n", f.Historico.Take(14).Select(h => C(Cinza, Quando(h.Quando).PadRight(10)) + " " + h.Texto)));
        }

        void FacGerenciar(UnturnedPlayer p, Faccao f, EstadoFac st)
        {
            var id = Id(p);
            bool recruta = TemPerm(f, id, PermFac.Recrutar);
            for (int i = 0; i < FacPedidos; i++)
            {
                string n = "Fac_Ped_" + (i + 1);
                if (!recruta || i >= f.Pedidos.Count) { V(p, n, false); continue; }
                var ped = f.Pedidos[i];
                var fi = Stats.Todas.FirstOrDefault(x => x.Id == ped.Id);
                V(p, n, true);
                T(p, n + "_Nome", Chat.Limpar(ped.Nome));
                T(p, n + "_Info", (fi != null ? $"{Chat.Tempo(fi.Segundos)} jogadas  •  {fi.Abates} abates  •  " : "") + Quando(ped.Quando)
                    + (string.IsNullOrEmpty(ped.Msg) ? "" : C(Cinza, "  \"" + ped.Msg + "\"")));
            }
            V(p, "Fac_Ped_Vazio", !recruta || f.Pedidos.Count == 0);
            T(p, "Fac_Ped_Vazio", recruta ? "Nenhum pedido pra entrar." : "Seu cargo nao aceita pedidos.");
            V(p, "Fac_Convidar_Area", recruta);

            var mem = f.Membros.OrderBy(m => m.Cargo).ThenBy(m => m.Nome).ToList();
            int pags = Math.Max(1, (mem.Count + FacMembros - 1) / FacMembros);
            st.PagMem = Mathf.Clamp(st.PagMem, 0, pags - 1);
            if (f.Membro(st.Alvo) == null) st.Alvo = 0;
            for (int i = 0; i < FacMembros; i++)
            {
                string n = "Fac_Mem_" + (i + 1);
                int idx = st.PagMem * FacMembros + i;
                if (idx >= mem.Count) { V(p, n, false); continue; }
                var m = mem[idx];
                V(p, n, true);
                V(p, n + "_Sel", m.Id == st.Alvo);
                T(p, n + "_Nome", (PlayerTool.getSteamPlayer(m.Id) != null ? C(Verde, "● ") : C(Cinza, "● ")) + Chat.Limpar(m.Nome));
                T(p, n + "_Cargo", Chat.Limpar(f.NomeCargo(m.Cargo)));
                T(p, n + "_Info", PlayerTool.getSteamPlayer(m.Id) != null ? C(Verde, "online") : "visto " + Quando(m.Visto));
            }
            T(p, "Fac_Mem_Pagina", $"{st.PagMem + 1}/{pags}");
            V(p, "Fac_Mem_Anterior", pags > 1);
            V(p, "Fac_Mem_Proximo", pags > 1);

            var eu = f.Membro(id);
            var alvo = f.Membro(st.Alvo);
            bool acima = alvo != null && alvo.Id != id && eu.Cargo < alvo.Cargo;
            T(p, "Fac_Alvo_Nome", alvo == null ? C(Cinza, "Clique em um membro") : Chat.Limpar(alvo.Nome) + "  " + C(Cinza, Chat.Limpar(f.NomeCargo(alvo.Cargo))));
            V(p, "Fac_Promover", acima && TemPerm(f, id, PermFac.Promover) && alvo.Cargo - 1 > eu.Cargo);
            V(p, "Fac_Rebaixar", acima && TemPerm(f, id, PermFac.Promover) && alvo.Cargo < 5);
            V(p, "Fac_Expulsar", acima && TemPerm(f, id, PermFac.Expulsar));
            V(p, "Fac_Lideranca", alvo != null && alvo.Id != id && eu.Cargo == 0);

            bool lider = eu.Cargo == 0;
            V(p, "Fac_Config", lider);
            if (lider)
            {
                T(p, "Fac_Modo_Texto", "RECRUTAMENTO: " + NomesModo[f.Modo]);
                T(p, "Fac_Cor_Texto", $"<color={f.Cor}>■ COR DA TAG [{f.Tag}]</color>");
            }
        }

        void FacPermissoes(UnturnedPlayer p, Faccao f)
        {
            bool lider = f.Membro(Id(p))?.Cargo == 0;
            for (int c = 0; c < 6; c++)
            {
                T(p, $"Fac_Cargo_{c + 1}_Nome", Chat.Limpar(f.NomeCargo(c)).ToUpperInvariant());
                V(p, $"Fac_Cargo_{c + 1}_Input", lider);
                for (int k = 0; k < PermsGrade.Length; k++)
                    V(p, $"Fac_Perm_{c + 1}_{k + 1}_On", c == 0 || (f.Permissoes[c] & (int)PermsGrade[k]) != 0);
            }
            T(p, "Fac_Perm_Dica", lider ? "Clique num quadrado pra liberar ou bloquear. Digite no campo do cargo pra trocar o nome. O lider sempre pode tudo."
                                        : "So o lider muda as permissoes.");
        }

        void FacRanking(UnturnedPlayer p)
        {
            var top = fac.Lista.OrderByDescending(f => f.Abates).ThenByDescending(f => f.Nivel).ThenByDescending(f => f.Zumbis).Take(FacMaxRanking).ToList();
            for (int i = 0; i < FacMaxRanking; i++)
            {
                string n = "Fac_Rank_" + (i + 1);
                if (i >= top.Count) { V(p, n, false); continue; }
                var f = top[i];
                V(p, n, true);
                T(p, n + "_Pos", "#" + (i + 1));
                T(p, n + "_Tag", $"<color={f.Cor}><b>{f.Tag}</b></color>");
                T(p, n + "_Nome", Chat.Limpar(f.Nome));
                T(p, n + "_Info", $"{f.Abates:N0} abates  •  {f.Zumbis:N0} zumbis  •  nivel {f.Nivel}  •  {f.Membros.Count} membros");
            }
            V(p, "Fac_Rank_Vazio", top.Count == 0);
        }

        // ---------- cliques ----------

        void ResultadoFac(UnturnedPlayer p, EstadoMenu e, bool ok, string msg)
        {
            Aviso(p, msg, ok);
            if (ok) Chat.Ok(p, msg);
            if (MenuAberto(p)) PreencherFaccoes(p, e);
        }

        void CliqueFaccao(UnturnedPlayer p, EstadoMenu e, string b)
        {
            var st = e.Fac;
            var f = FaccaoDe(p);
            var id = Id(p);
            bool ok; string msg;
            if (b.StartsWith("Fac_Tab_")) { st.Tela = b.Substring(8); PreencherFaccoes(p, e); return; }

            var partes = b.Split('_');
            int Num(int i) => partes.Length > i && int.TryParse(partes[i], out var v) ? v : 0;

            if (b.StartsWith("Fac_Lista_") && b.EndsWith("_Pedir"))
            {
                var lista = ListaBusca(st);
                int idx = st.PagLista * FacLinhas + Num(2) - 1;
                if (idx < 0 || idx >= lista.Count) return;
                ok = PedirEntrada(p, lista[idx], st.PedidoMsg, out msg);
                if (ok && FaccaoDe(p) != null) st.Tela = "Minha";
                ResultadoFac(p, e, ok, msg); return;
            }
            if (b.StartsWith("Fac_Conv_"))
            {
                var conv = fac.Lista.Where(x => x.Convites.Any(c => c.Id == id)).ToList();
                int idx = Num(2) - 1;
                if (idx < 0 || idx >= conv.Count) return;
                if (b.EndsWith("_Aceitar")) { ok = PedirEntrada(p, conv[idx], "", out msg); if (ok) st.Tela = "Minha"; }
                else ok = RecusarConvite(p, conv[idx], out msg);
                ResultadoFac(p, e, ok, msg); return;
            }
            if (b.StartsWith("Fac_Ped_") && f != null)
            {
                int idx = Num(2) - 1;
                if (idx < 0 || idx >= f.Pedidos.Count) return;
                ok = ResponderPedido(p, f.Pedidos[idx].Id, b.EndsWith("_Aceitar"), out msg);
                ResultadoFac(p, e, ok, msg); return;
            }
            if (b.StartsWith("Fac_Mem_") && f != null && partes.Length == 3 && Num(2) > 0)
            {
                var mem = f.Membros.OrderBy(m => m.Cargo).ThenBy(m => m.Nome).ToList();
                int idx = st.PagMem * FacMembros + Num(2) - 1;
                if (idx >= 0 && idx < mem.Count) st.Alvo = mem[idx].Id;
                PreencherFaccoes(p, e); return;
            }
            if (b.StartsWith("Fac_Perm_") && f != null)
            {
                ok = AlternarPermissao(p, Num(2) - 1, Num(3) - 1, out msg);
                ResultadoFac(p, e, ok, msg); return;
            }

            switch (b)
            {
                case "Fac_Buscar": st.PagLista = 0; PreencherFaccoes(p, e); return;
                case "Fac_Lista_Anterior": st.PagLista--; if (st.PagLista < 0) st.PagLista = int.MaxValue; PreencherFaccoes(p, e); return;
                case "Fac_Lista_Proximo":
                    { int pags = Math.Max(1, (ListaBusca(st).Count + FacLinhas - 1) / FacLinhas); st.PagLista = (st.PagLista + 1) % pags; PreencherFaccoes(p, e); return; }
                case "Fac_Mem_Anterior": st.PagMem--; if (st.PagMem < 0) st.PagMem = int.MaxValue; PreencherFaccoes(p, e); return;
                case "Fac_Mem_Proximo":
                    { int pags = f == null ? 1 : Math.Max(1, (f.Membros.Count + FacMembros - 1) / FacMembros); st.PagMem = (st.PagMem + 1) % pags; PreencherFaccoes(p, e); return; }
                case "Fac_Criar_Cor": st.CriarCor = (st.CriarCor + 1) % CoresFaccao.Length; PreencherFaccoes(p, e); return;
                case "Fac_Criar_Modo": st.CriarModo = (st.CriarModo + 1) % 3; PreencherFaccoes(p, e); return;
                case "Fac_Criar_Pagar": st.CriarBanco = !st.CriarBanco; PreencherFaccoes(p, e); return;
                case "Fac_Criar_Ok":
                    ok = CriarFaccao(p, st.CriarNome, st.CriarTag, CoresFaccao[st.CriarCor % CoresFaccao.Length], st.CriarDesc, st.CriarModo, st.CriarBanco, out msg);
                    if (ok) st.Tela = "Minha";
                    ResultadoFac(p, e, ok, msg); return;
                case "Fac_Bau":
                    ok = AbrirBauFaccao(p, out msg); if (!ok) Aviso(p, msg, false); return;
                case "Fac_Sair": ok = SairDaFaccao(p, out msg); if (ok) st.Tela = ""; ResultadoFac(p, e, ok, msg); return;
                case "Fac_Subir": ok = SubirNivel(p, out msg); ResultadoFac(p, e, ok, msg); return;
                case "Fac_Dissolver":
                    if (Time.realtimeSinceStartup >= st.ConfirmarDissolver)
                    {
                        st.ConfirmarDissolver = Time.realtimeSinceStartup + 8f;
                        Aviso(p, "Voce fica com a base e o dinheiro do cofre; os outros perdem o acesso. Clique de novo pra confirmar.", false);
                        PreencherFaccoes(p, e); return;
                    }
                    st.ConfirmarDissolver = 0;
                    ok = Dissolver(p, out msg); if (ok) st.Tela = ""; ResultadoFac(p, e, ok, msg); return;
                case "Fac_Cofre_Depositar": case "Fac_Cofre_Sacar":
                    if (!long.TryParse(st.Valor.Replace(".", "").Replace(",", "").Replace("$", "").Trim(), out var v)) { Aviso(p, "Digite o valor no campo.", false); return; }
                    ok = MexerCofre(p, v, b == "Fac_Cofre_Depositar", out msg); ResultadoFac(p, e, ok, msg); return;
                case "Fac_Convidar": ok = Convidar(p, st.Convidar, out msg); ResultadoFac(p, e, ok, msg); return;
                case "Fac_Promover": ok = MudarCargo(p, st.Alvo, -1, out msg); ResultadoFac(p, e, ok, msg); return;
                case "Fac_Rebaixar": ok = MudarCargo(p, st.Alvo, 1, out msg); ResultadoFac(p, e, ok, msg); return;
                case "Fac_Expulsar": ok = Expulsar(p, st.Alvo, out msg); if (ok) st.Alvo = 0; ResultadoFac(p, e, ok, msg); return;
                case "Fac_Lideranca": ok = PassarLideranca(p, st.Alvo, out msg); ResultadoFac(p, e, ok, msg); return;
                case "Fac_Modo": ok = MudarModo(p, out msg); ResultadoFac(p, e, ok, msg); return;
                case "Fac_Cor": ok = MudarCor(p, out msg); ResultadoFac(p, e, ok, msg); return;
                case "Fac_Rank_Site": AbrirSite(p, "/faccoes"); return;
            }
        }

        void DigitouFaccao(UnturnedPlayer p, EstadoMenu e, string campo, string texto)
        {
            var st = e.Fac;
            texto = (texto ?? "").Trim();
            bool ok; string msg;
            switch (campo)
            {
                case "Fac_Busca": st.Busca = texto; st.PagLista = 0; PreencherFaccoes(p, e); return;
                case "Fac_Pedido_Msg": st.PedidoMsg = texto; return;
                case "Fac_Criar_Nome": st.CriarNome = texto; PreencherFaccoes(p, e); return;
                case "Fac_Criar_Tag": st.CriarTag = texto; PreencherFaccoes(p, e); return;
                case "Fac_Criar_Desc": st.CriarDesc = texto; return;
                case "Fac_Cofre_Valor": st.Valor = texto; return;
                case "Fac_Convidar_Nome": st.Convidar = texto; return;
                case "Fac_Config_Tag": if (texto == "") return; ok = MudarTag(p, texto, out msg); ResultadoFac(p, e, ok, msg); return;
                case "Fac_Config_Desc": ok = MudarDescricao(p, texto, out msg); ResultadoFac(p, e, ok, msg); return;
            }
            if (campo.StartsWith("Fac_Cargo_") && campo.EndsWith("_Input") && int.TryParse(campo.Split('_')[2], out var c))
            {
                ok = RenomearCargo(p, c - 1, texto, out msg); ResultadoFac(p, e, ok, msg);
            }
        }
    }
}
