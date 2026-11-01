using System;
using System.Collections.Generic;
using System.Linq;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;

namespace SobrevivenciaCore
{
    public abstract class ComandoSistema : ComandoBase
    {
        protected Sistemas S => Plugin.Instancia.S;
        protected static string Cor(string c, string t) => Chat.Cor(c, t);

        /// <summary>Bloqueia se o jogador estiver em combate. Devolve true se pode seguir.</summary>
        protected bool ForaDeCombate(UnturnedPlayer p)
        {
            if (!P.EmCombate(p, out var resta)) return true;
            Chat.Erro(p, $"Voce esta em combate. Espere {resta}s.");
            return false;
        }

        protected bool SemIntervalo(UnturnedPlayer p, string tipo)
        {
            if (!P.EmIntervalo(p, tipo, out var resta)) return true;
            Chat.Erro(p, $"Espere {Chat.Tempo(resta)} pra usar de novo.");
            return false;
        }

        protected bool NaSafe(UnturnedPlayer p) => P.ZonaSeguraEm(p.Position) != null;
    }

    // ---------------- Teleportes ----------------

    public class CmdSafe : ComandoSistema
    {
        public override string Name => "safe";
        public override string Help => "Teleporta pra safe zone (fique parado).";
        protected override void Rodar(UnturnedPlayer p, string[] a) => Warp(P, S, p, this);

        internal static void Warp(Plugin P, Sistemas S, UnturnedPlayer p, ComandoSistema cmd)
        {
            var zonas = P.Configuration.Instance.ZonasSeguras.Where(z => z.Definido).ToList();
            if (zonas.Count == 0) { Chat.Erro(p, "Ainda nao tem safe zone marcada."); return; }
            if (P.ZonaSeguraEm(p.Position) != null) { Chat.Info(p, "Voce ja esta na safe zone."); return; }
            if (P.EmCombate(p, out var r)) { Chat.Erro(p, $"Voce esta em combate. Espere {r}s."); return; }
            if (P.EmIntervalo(p, "safe", out var ri)) { Chat.Erro(p, $"Espere {Chat.Tempo(ri)} pra usar de novo."); return; }
            var alvo = zonas.OrderBy(z => Vector3.Distance(p.Position, new Vector3(z.X, z.Y, z.Z))).First();
            P.AgendarTeleporte(p, "a safe zone " + Chat.Limpar(alvo.Nome), S.WarpEspera,
                () => new Vector3(alvo.X, alvo.Y, alvo.Z),
                () => P.IniciarIntervalo(p, "safe", S.WarpIntervalo));
        }
    }

    public class CmdWarp : ComandoSistema
    {
        public override string Name => "warp";
        public override string Help => "Use /warp safe pra ir pra safe zone.";
        public override string Syntax => "safe";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (a.Length > 0 && a[0].Equals("safe", StringComparison.OrdinalIgnoreCase)) CmdSafe.Warp(P, S, p, this);
            else Chat.Info(p, "Use /warp safe");
        }
    }

    public class CmdHome : ComandoSistema
    {
        public override string Name => "home";
        public override string Help => "Teleporta pra sua cama ou pra uma home com nome (fique parado).";
        public override string Syntax => "[nome]";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            var id = Plugin.Id(p);
            var minhas = P.Homes.De(id);
            Casa casa = null;
            if (a.Length > 0)
            {
                casa = P.Homes.Achar(id, string.Join(" ", a));
                if (casa == null) { Chat.Erro(p, "Home nao encontrada. Veja as suas com /homes."); return; }
            }
            else if (minhas.Count == 1) casa = minhas[0];
            else if (minhas.Count > 1) { CmdHomes.Listar(P, S, p); Chat.Info(p, "Use /home nome"); return; }

            if (casa == null)
            {
                if (!BarricadeManager.tryGetBed(p.CSteamID, out _, out _))
                { Chat.Erro(p, "Voce nao tem home. Coloque uma cama, fique do lado e use /sethome nome."); return; }
                if (!ForaDeCombate(p) || !SemIntervalo(p, "home")) return;
                P.AgendarTeleporte(p, "sua cama", S.HomeEspera,
                    () => BarricadeManager.tryGetBed(p.CSteamID, out var c, out _) ? c : (Vector3?)null,
                    () => P.IniciarIntervalo(p, "home", S.HomeIntervalo));
                return;
            }
            IrPara(P, S, p, casa);
        }

        /// <summary>Teleporta pra uma home com nome (checa combate e intervalo). Usado pelo /home e pelo menu.</summary>
        internal static void IrPara(Plugin P, Sistemas S, UnturnedPlayer p, Casa casa)
        {
            if (P.EmCombate(p, out var r)) { Chat.Erro(p, $"Voce esta em combate. Espere {r}s."); return; }
            if (P.EmIntervalo(p, "home", out var ri)) { Chat.Erro(p, $"Espere {Chat.Tempo(ri)} pra usar de novo."); return; }
            var id = Plugin.Id(p);
            var minhas = P.Homes.De(id);
            var alvo = casa;
            ulong grupo = p.Player.quests.groupID.m_SteamID;
            P.AgendarTeleporte(p, "home " + Chat.Limpar(alvo.Nome), S.HomeEspera,
                () =>
                {
                    var cama = Casas.CamaPerto(alvo.Pos, id, grupo);
                    if (cama != null) return cama.model.position;
                    minhas.Remove(alvo);
                    P.Homes.Salvar();
                    Chat.Aviso(p, "A cama da home " + Chat.Limpar(alvo.Nome) + " foi destruida. Home apagada.");
                    return null;
                },
                () => P.IniciarIntervalo(p, "home", S.HomeIntervalo));
        }
    }

    public class CmdSetHome : ComandoSistema
    {
        public override string Name => "sethome";
        public override string Help => "Cria uma home com nome na cama do seu lado.";
        public override string Syntax => "<nome>";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (a.Length < 1) { Chat.Info(p, "Fique do lado da sua cama e use: /sethome nome"); return; }
            var nome = Chat.Limpar(string.Join(" ", a)).Replace('\t', ' ');
            if (nome.Length > 20) nome = nome.Substring(0, 20);
            var id = Plugin.Id(p);
            var cama = Casas.CamaPerto(p.Position, id, p.Player.quests.groupID.m_SteamID);
            if (cama == null) { Chat.Erro(p, "Fique do lado de uma cama sua (ate 4 m)."); return; }
            if (NaSafe(p)) { Chat.Erro(p, "Nao da pra criar home na safe zone."); return; }
            var minhas = P.Homes.De(id);
            var existente = P.Homes.Achar(id, nome);
            if (existente != null) { existente.Pos = cama.model.position; P.Homes.Salvar(); Chat.Ok(p, $"Home {nome} atualizada."); return; }
            int max = P.TemPermissao(p, "home.vip") ? S.HomesVip : S.HomesMax;
            if (minhas.Count >= max) { Chat.Erro(p, $"Voce ja tem {minhas.Count}/{max} homes. Apague uma com /delhome nome."); return; }
            if (minhas.Any(c => Vector3.Distance(c.Pos, cama.model.position) < 1f))
            { Chat.Erro(p, "Essa cama ja e uma home sua."); return; }
            minhas.Add(new Casa { Nome = nome, Pos = cama.model.position });
            P.Homes.Salvar();
            Chat.Ok(p, $"Home {nome} criada ({minhas.Count}/{max}). Va com /home {nome}");
        }
    }

    public class CmdHomes : ComandoSistema
    {
        public override string Name => "homes";
        public override string Help => "Lista suas homes.";
        protected override void Rodar(UnturnedPlayer p, string[] a) => Listar(P, S, p);

        internal static void Listar(Plugin P, Sistemas S, UnturnedPlayer p)
        {
            var minhas = P.Homes.De(Plugin.Id(p));
            int max = P.TemPermissao(p, "home.vip") ? S.HomesVip : S.HomesMax;
            if (minhas.Count == 0) { Chat.Info(p, $"Voce nao tem homes (0/{max}). Fique do lado da sua cama e use /sethome nome."); return; }
            Chat.Para(p, Cor(S.CorTexto, Chat.Forte($"SUAS HOMES ({minhas.Count}/{max})")));
            foreach (var c in minhas)
            {
                int dist = Mathf.RoundToInt(Vector3.Distance(p.Position, c.Pos));
                Chat.Para(p, Cor(S.CorPrincipal, "/home " + c.Nome) + Cor(S.CorApagada, $"  a {dist}m"));
            }
            Chat.Para(p, Cor(S.CorApagada, "/renamehome nome novo   /delhome nome"));
        }
    }

    public class CmdDelHome : ComandoSistema
    {
        public override string Name => "delhome";
        public override string Help => "Apaga uma home (a cama continua la).";
        public override string Syntax => "<nome>";
        public override List<string> Aliases => new List<string> { "destroyhome" };
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (a.Length < 1) { Chat.Info(p, "Use: /delhome nome"); return; }
            var id = Plugin.Id(p);
            var c = P.Homes.Achar(id, string.Join(" ", a));
            if (c == null) { Chat.Erro(p, "Home nao encontrada. Veja com /homes."); return; }
            P.Homes.De(id).Remove(c);
            P.Homes.Salvar();
            Chat.Ok(p, $"Home {c.Nome} apagada.");
        }
    }

    public class CmdRenameHome : ComandoSistema
    {
        public override string Name => "renamehome";
        public override string Help => "Renomeia uma home.";
        public override string Syntax => "<nome> <novo>";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (a.Length < 2) { Chat.Info(p, "Use: /renamehome nome novo"); return; }
            var id = Plugin.Id(p);
            var c = P.Homes.Achar(id, a[0]);
            if (c == null) { Chat.Erro(p, "Home nao encontrada. Veja com /homes."); return; }
            var novo = Chat.Limpar(string.Join(" ", a.Skip(1))).Replace('\t', ' ');
            if (novo.Length > 20) novo = novo.Substring(0, 20);
            if (P.Homes.Achar(id, novo) != null) { Chat.Erro(p, "Voce ja tem uma home com esse nome."); return; }
            var antigo = c.Nome;
            c.Nome = novo;
            P.Homes.Salvar();
            Chat.Ok(p, $"Home {antigo} agora se chama {novo}.");
        }
    }

    public class CmdTutorial : ComandoSistema
    {
        public override string Name => "tutorial";
        public override string Help => "Guia do servidor por assunto.";
        public override string Syntax => "[assunto]";
        public override List<string> Aliases => new List<string> { "ajuda", "guia" };

        static readonly (string chave, string titulo, string[] linhas)[] Paginas =
        {
            ("safe", "SAFE ZONE", new[] {
                "Dentro da safe zone nao tem PvP, nao da pra construir e carros e bases ficam protegidos.",
                "Fora dela o PvP e liberado. Use /safe pra voltar (fique parado; cancela se mexer).",
                "Se levar ou der tiro, entra em modo de combate e nao pode teleportar por alguns segundos." }),
            ("atm", "DINHEIRO E ATM", new[] {
                "O dinheiro e fisico: as notas ficam no inventario e caem quando voce morre.",
                "Perto de um ATM: /depositar valor guarda no banco, /sacar valor tira, /saldo mostra.",
                "Dinheiro no banco nao se perde. Deposite antes de sair pra lootear." }),
            ("lojas", "LOJAS", new[] {
                "Use /lojas pra ver as lojas e /loja nome pra ver os itens e precos.",
                "Compre com /comprar e venda com /vender. As lojas ficam na safe zone.",
                "O mercado negro paga mais por itens raros." }),
            ("kits", "KITS", new[] {
                "/kits mostra seus kits. /kit nome pega um.",
                "Kits livres tem tempo de espera. Kits de rank sao liberados por abates e dao bonus de XP.",
                "Membros do Discord tem /kit discord." }),
            ("xp", "XP, NIVEL E PATENTE", new[] {
                "Voce ganha XP matando jogadores (mais na cabeca), zumbis, megas e jogando.",
                "Sua patente sobe com o XP total. Veja com /perfil e o ranking com /top.",
                "Gaste XP em equipamento na /lojaxp (dentro da safe zone). Novatos ganham XP de boas-vindas." }),
            ("homes", "HOMES E TELEPORTE", new[] {
                "Coloque uma cama, fique do lado e use /sethome nome. Depois /home nome te leva la.",
                "/homes lista, /renamehome muda o nome, /delhome apaga.",
                "/tpa nome pede pra ir ate um amigo; ele aceita com /tpaceitar." }),
            ("vault", "VAULT", new[] {
                "/vault abre seu cofre pessoal (dentro da safe zone).",
                "O que fica no vault nao se perde quando voce morre. VIPs tem vault maior." }),
        };

        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (a.Length == 0)
            {
                Chat.Para(p, Cor(S.CorTexto, Chat.Forte("TUTORIAL - DAY Z BRASIL OFICIAL")));
                Chat.Para(p, Cor(S.CorApagada, "Escolha: ") + string.Join(" ", Paginas.Select(x => Cor(S.CorPrincipal, "/tutorial " + x.chave))));
                return;
            }
            var pg = Paginas.FirstOrDefault(x => x.chave.Equals(a[0], StringComparison.OrdinalIgnoreCase));
            if (pg.linhas == null) { Chat.Erro(p, "Assunto nao encontrado. Use /tutorial"); return; }
            Chat.Para(p, Cor(S.CorPrincipal, Chat.Forte(pg.titulo)));
            foreach (var l in pg.linhas) Chat.Para(p, Cor(S.CorTexto, l));
        }
    }

    public class CmdTpa : ComandoSistema
    {
        public override string Name => "tpa";
        public override string Help => "Pede pra teleportar ate um jogador.";
        public override string Syntax => "<jogador>";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (a.Length < 1) { Chat.Info(p, "Use: /tpa nome. O outro aceita com /tpaceitar."); return; }
            var alvo = UnturnedPlayer.FromName(string.Join(" ", a));
            if (alvo == null || alvo.CSteamID == p.CSteamID) { Chat.Erro(p, "Jogador nao encontrado."); return; }
            if (!ForaDeCombate(p) || !SemIntervalo(p, "tpa")) return;
            P.pedidosTpa[Plugin.Id(alvo)] = (Plugin.Id(p), Time.realtimeSinceStartup + S.TpaExpira);
            Chat.Ok(p, $"Pedido enviado pra {Chat.Limpar(alvo.CharacterName)}. Expira em {S.TpaExpira}s.");
            Chat.Para(alvo, Cor(S.CorPrincipal, Chat.Limpar(p.CharacterName)) + Cor(S.CorTexto, " quer teleportar ate voce. ") +
                Cor(S.CorOk, "/tpaceitar") + Cor(S.CorTexto, " ou ") + Cor(S.CorPerigo, "/tpanegar"));
        }
    }

    public class CmdTpaAceitar : ComandoSistema
    {
        public override string Name => "tpaceitar";
        public override string Help => "Aceita o pedido de teleporte.";
        public override List<string> Aliases => new List<string> { "tpaccept" };
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (!P.pedidosTpa.TryGetValue(Plugin.Id(p), out var ped)) { Chat.Erro(p, "Nenhum pedido de teleporte."); return; }
            P.pedidosTpa.Remove(Plugin.Id(p));
            var de = UnturnedPlayer.FromCSteamID(new Steamworks.CSteamID(ped.de));
            if (de?.Player == null) { Chat.Erro(p, "O jogador saiu."); return; }
            if (P.EmCombate(de, out _)) { Chat.Erro(p, "Ele esta em combate."); Chat.Erro(de, "Teleporte negado: voce esta em combate."); return; }
            Chat.Ok(p, $"Voce aceitou. {Chat.Limpar(de.CharacterName)} chega em {S.TpaEspera}s.");
            var destino = p;
            P.AgendarTeleporte(de, Chat.Limpar(p.CharacterName), S.TpaEspera,
                () => destino?.Player != null && !destino.Dead ? destino.Position : (Vector3?)null,
                () => P.IniciarIntervalo(de, "tpa", S.TpaIntervalo));
        }
    }

    public class CmdTpaNegar : ComandoSistema
    {
        public override string Name => "tpanegar";
        public override string Help => "Nega o pedido de teleporte.";
        public override List<string> Aliases => new List<string> { "tpdeny" };
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (!P.pedidosTpa.TryGetValue(Plugin.Id(p), out var ped)) { Chat.Erro(p, "Nenhum pedido de teleporte."); return; }
            P.pedidosTpa.Remove(Plugin.Id(p));
            Chat.Info(p, "Pedido negado.");
            var de = UnturnedPlayer.FromCSteamID(new Steamworks.CSteamID(ped.de));
            if (de?.Player != null) Chat.Erro(de, $"{Chat.Limpar(p.CharacterName)} negou seu teleporte.");
        }
    }

    // ---------------- Kits ----------------

    public class CmdKits : ComandoSistema
    {
        public override string Name => "kits";
        public override string Help => "Lista os kits e o que falta pra cada um.";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            var f = P.FichaDe(p);
            Chat.Para(p, Cor(S.CorPrincipal, Chat.Forte("KITS")) + Cor(S.CorApagada, $"  (voce tem {f.Abates} abates)  /kit nome"));
            foreach (var k in S.Kits)
                Chat.Para(p, Cor(S.CorTexto, Chat.Forte(k.Nome)) + " " + Status(P, p, f, k) + Cor(S.CorApagada, " " + Chat.Limpar(k.Descricao)));
        }

        internal static string Status(Plugin P, UnturnedPlayer p, Ficha f, Kit k)
        {
            var S = P.S;
            if (!P.TemPermissao(p, k.Permissao)) return Chat.Cor(S.CorPerigo, "[VIP]");
            if (f.Abates < k.Abates) return Chat.Cor(S.CorPerigo, $"[{f.Abates}/{k.Abates} abates]");
            var resta = P.Stats.UltimoUsoKit(f.Id, k.Nome) + k.Espera - Estatisticas.Agora;
            if (resta > 0) return Chat.Cor(S.CorDestaque, "[" + Chat.Tempo(resta) + "]");
            return Chat.Cor(S.CorOk, "[PEGAR]");
        }
    }

    public class CmdKit : ComandoSistema
    {
        public override string Name => "kit";
        public override string Help => "Pega um kit.";
        public override string Syntax => "<nome>";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (a.Length < 1) { Chat.Info(p, "Use: /kit nome. Veja os kits com /kits."); return; }
            var k = S.Kits.FirstOrDefault(x => x.Nome.Equals(a[0], StringComparison.OrdinalIgnoreCase));
            if (k == null) { Chat.Erro(p, "Kit nao encontrado. Veja /kits."); return; }
            if (Pegar(P, p, k, out var msg)) Chat.Ok(p, msg); else Chat.Erro(p, msg);
        }

        /// <summary>Entrega o kit se puder. Usado pelo /kit e pelo menu.</summary>
        internal static bool Pegar(Plugin P, UnturnedPlayer p, Kit k, out string msg)
        {
            var f = P.FichaDe(p);
            if (!P.TemPermissao(p, k.Permissao)) { msg = "Esse kit e VIP."; return false; }
            if (f.Abates < k.Abates) { msg = $"Precisa de {k.Abates} abates. Voce tem {f.Abates}."; return false; }
            var resta = P.Stats.UltimoUsoKit(f.Id, k.Nome) + k.Espera - Estatisticas.Agora;
            if (resta > 0) { msg = $"Espere {Chat.Tempo(resta)} pra pegar de novo."; return false; }
            foreach (var i in k.Itens ?? new List<ItemKit>())
                if (Itens.Existe(i.Id)) Itens.Dar(p, i.Id, i.Quantidade);
            P.Stats.MarcarKit(f.Id, k.Nome);
            P.Stats.Salvar();
            msg = $"Voce recebeu o kit {k.Nome}.";
            return true;
        }
    }

    // ---------------- Perfil e ranking ----------------

    public class CmdPerfil : ComandoSistema
    {
        public override string Name => "perfil";
        public override string Help => "Mostra suas estatisticas (ou de outro jogador).";
        public override string Syntax => "[jogador]";
        public override List<string> Aliases => new List<string> { "stats" };
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            var f = a.Length > 0 ? P.Stats.Buscar(string.Join(" ", a)) : P.FichaDe(p);
            if (f == null) { Chat.Erro(p, "Jogador nao encontrado."); return; }
            var prox = Ficha.XpDoNivel(f.Nivel + 1) - f.Xp;
            Chat.Para(p, P.NomeColorido(f) + Cor(S.CorApagada, "  proximo nivel em ") + Cor(S.CorDestaque, prox + " XP"));
            Chat.Para(p, Linha("Abates", f.Abates) + Linha("Mortes", f.Mortes) + Linha("K/D", f.Kd.ToString("0.00")) +
                Linha("Cabeca", f.PorcentoCabeca + "%"));
            Chat.Para(p, Linha("Zumbis", f.Zumbis) + Linha("Megas", f.Megas) + Linha("Tempo", Chat.Tempo(f.Segundos)) +
                Linha("XP", f.Xp) + Linha("XP pra gastar", f.XpSaldo));
            var k = P.ProximoKitPorAbates(f);
            if (k != null) Chat.Para(p, Cor(S.CorApagada, "Proximo kit: ") + Cor(S.CorPrincipal, k.Nome) + Cor(S.CorTexto, $" {f.Abates}/{k.Abates} abates"));
        }

        string Linha(string nome, object v) => Cor(S.CorApagada, nome + " ") + Cor(S.CorTexto, Chat.Forte(v.ToString())) + "   ";
    }

    public class CmdTop : ComandoSistema
    {
        public override string Name => "top";
        public override string Help => "Ranking: abates, kd, xp, tempo ou zumbis.";
        public override string Syntax => "[abates|kd|xp|tempo|zumbis]";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            var tipo = a.Length > 0 ? a[0].ToLowerInvariant() : "abates";
            Func<Ficha, double> chave; Func<Ficha, string> mostra;
            switch (tipo)
            {
                case "kd": chave = f => f.Abates >= 5 ? f.Kd : -1; mostra = f => f.Kd.ToString("0.00") + " K/D"; break;
                case "xp": chave = f => f.Xp; mostra = f => f.Xp + " XP"; break;
                case "tempo": chave = f => f.Segundos; mostra = f => Chat.Tempo(f.Segundos); break;
                case "zumbis": chave = f => f.Zumbis; mostra = f => f.Zumbis + " zumbis"; break;
                default: tipo = "abates"; chave = f => f.Abates; mostra = f => f.Abates + " abates"; break;
            }
            Chat.Para(p, Cor(S.CorPrincipal, Chat.Forte("TOP 10 - " + tipo.ToUpperInvariant())));
            int i = 1;
            foreach (var f in P.Stats.Todas.Where(f => chave(f) > 0).OrderByDescending(chave).Take(10))
                Chat.Para(p, Cor(i <= 3 ? S.CorDestaque : S.CorApagada, $"{i++}. ") + P.NomeColorido(f) + Cor(S.CorTexto, "  " + mostra(f)));
            if (i == 1) Chat.Info(p, "Ninguem no ranking ainda.");
        }
    }

    // ---------------- Loja de XP ----------------

    public class CmdLojaXp : ComandoSistema
    {
        public override string Name => "lojaxp";
        public override string Help => "Troca XP por equipamento (na safe zone).";
        public override string Syntax => "[comprar <numero>]";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            var f = P.FichaDe(p);
            if (a.Length >= 2 && a[0].Equals("comprar", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(a[1], out var n)) n = 0;
                if (Comprar(P, p, n, out var msg)) Chat.Ok(p, msg); else Chat.Erro(p, msg);
                return;
            }
            Chat.Para(p, Cor(S.CorPrincipal, Chat.Forte("LOJA DE XP")) + Cor(S.CorApagada, $"  voce tem ") +
                Cor(S.CorDestaque, f.XpSaldo + " XP") + Cor(S.CorApagada, "  /lojaxp comprar numero"));
            for (int i = 0; i < S.LojaXp.Count; i++)
            {
                var pac = S.LojaXp[i];
                var cor = f.XpSaldo >= pac.Preco ? S.CorOk : S.CorPerigo;
                Chat.Para(p, Cor(S.CorApagada, $"{i + 1}. ") + Cor(S.CorTexto, Chat.Forte(pac.Nome)) + " " +
                    Cor(cor, pac.Preco + " XP") + Cor(S.CorApagada, " " + Chat.Limpar(pac.Descricao)));
            }
        }

        /// <summary>Compra o pacote n (1..). Usado pelo /lojaxp e pelo menu.</summary>
        internal static bool Comprar(Plugin P, UnturnedPlayer p, int n, out string msg)
        {
            var S = P.S;
            if (n < 1 || n > S.LojaXp.Count) { msg = "Numero invalido. Veja /lojaxp."; return false; }
            if (S.LojaXpSoNaSafe && P.ZonaSeguraEm(p.Position) == null) { msg = "A loja de XP so funciona dentro da safe zone."; return false; }
            var f = P.FichaDe(p);
            var pac = S.LojaXp[n - 1];
            if (f.XpSaldo < pac.Preco) { msg = $"Voce tem {f.XpSaldo} XP. Precisa de {pac.Preco}."; return false; }
            f.XpGasto += pac.Preco;
            P.Stats.Mudou();
            P.Stats.Salvar();
            foreach (var i in pac.Itens ?? new List<ItemKit>())
                if (Itens.Existe(i.Id)) Itens.Dar(p, i.Id, i.Quantidade);
            msg = $"Voce comprou o pacote {pac.Nome}. Sobrou {f.XpSaldo} XP.";
            return true;
        }
    }

    // ---------------- Vault ----------------

    public class CmdVault : ComandoSistema
    {
        public override string Name => "vault";
        public override string Help => "Abre seu cofre pessoal.";
        public override List<string> Aliases => new List<string> { "cofre" };
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            var tam = S.Vaults.Where(v => P.TemPermissao(p, v.Permissao))
                .OrderByDescending(v => v.Largura * v.Altura).FirstOrDefault();
            if (tam == null) { Chat.Erro(p, "Voce nao tem cofre."); return; }
            Abrir(P, S, p, tam);
        }

        /// <summary>Abre o cofre do tamanho pedido (checa safe, combate e veiculo). Usado pelo /vault e pelo menu.</summary>
        internal static void Abrir(Plugin P, Sistemas S, UnturnedPlayer p, TamanhoVault tam)
        {
            if (p.Dead) return;
            if (S.VaultSoNaSafe && P.ZonaSeguraEm(p.Position) == null) { Chat.Erro(p, "O cofre so abre dentro da safe zone."); return; }
            if (P.EmCombate(p, out var r)) { Chat.Erro(p, $"Voce esta em combate. Espere {r}s."); return; }
            if (p.Player.movement.getVehicle() != null) { Chat.Erro(p, "Saia do veiculo pra abrir o cofre."); return; }
            if (!P.TemPermissao(p, tam.Permissao)) { Chat.Erro(p, "Esse cofre e VIP."); return; }
            P.Cofres.Abrir(p, tam);
            Chat.Ok(p, $"{tam.Nome} aberto ({tam.Largura}x{tam.Altura}).");
        }
    }

    // ---------------- Menu e links ----------------

    public class CmdMenu : ComandoSistema
    {
        public override string Name => "menu";
        public override string Help => "Mostra tudo que o servidor tem.";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            // menu com janela (mod do Workshop); "/menu chat" mostra a versao em texto
            if (!(a.Length > 0 && a[0].Equals("chat", StringComparison.OrdinalIgnoreCase)) && P.AbrirMenu(p)) return;
            string C(string cmd) => Cor(S.CorPrincipal, cmd);
            string T(string t) => Cor(S.CorApagada, t);
            Chat.Para(p, Cor(S.CorTexto, Chat.Forte("MENU - DAY Z BRASIL OFICIAL")));
            Chat.Para(p, T("Kits: ") + C("/kits") + " " + C("/kit nome") + T("   Loja de XP: ") + C("/lojaxp"));
            Chat.Para(p, T("Dinheiro: ") + C("/saldo") + " " + C("/depositar") + " " + C("/sacar") + T("   Lojas: ") + C("/lojas") + " " + C("/loja"));
            Chat.Para(p, T("Teleporte: ") + C("/safe") + " " + C("/home") + " " + C("/homes") + " " + C("/tpa nome") + " " + C("/tpaceitar"));
            Chat.Para(p, T("Voce: ") + C("/perfil") + " " + C("/top") + " " + C("/vault") + T("   Airdrop: ") + C("/whenairdrop"));
            Chat.Para(p, T("Comunidade: ") + C("/discord") + " " + C("/site") + T("   Guia: ") + C("/tutorial"));
        }
    }

    public class CmdDiscord : ComandoSistema
    {
        public override string Name => "discord";
        public override string Help => "Link do Discord do servidor.";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            var link = S.Discord;
            if (link == "") { Chat.Info(p, "O Discord ainda vai ser divulgado. Fique ligado!"); return; }
            p.Player.sendBrowserRequest("Discord do DAY Z BRASIL OFICIAL", link);
            Chat.Info(p, "Discord: " + link);
        }
    }

    public class CmdSite : ComandoSistema
    {
        public override string Name => "site";
        public override string Help => "Abre o site.";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            p.Player.sendBrowserRequest("SnapLag", S.LinkSite);
            Chat.Info(p, "Site: " + S.LinkSite);
        }
    }

    // ---------------- Admin ----------------

    public class CmdDarXp : ComandoAdmin
    {
        public override string Name => "darxp";
        public override string Help => "Da XP pra um jogador.";
        public override string Syntax => "<jogador> <quantidade>";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (a.Length < 2 || !long.TryParse(a[1], out var q) || q <= 0) { P.Erro(p, "Use: /darxp jogador quantidade"); return; }
            var alvo = UnturnedPlayer.FromName(a[0]);
            if (alvo == null) { P.Erro(p, "Jogador nao encontrado."); return; }
            P.DarXp(alvo, q, "presente da administracao");
            P.Msg(p, $"Deu {q} XP pra {alvo.CharacterName}.");
        }
    }

    public class CmdLimparItens : ComandoAdmin
    {
        public override string Name => "limparitens";
        public override string Help => "Agenda a limpeza dos itens do chao.";
        public override string Syntax => "[segundos]";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            int s = a.Length > 0 && int.TryParse(a[0], out var v) ? Math.Max(0, v) : 60;
            if (s == 0) { P.LimparItens(); return; }
            P.AgendarLimpeza(s);
            P.Msg(p, $"Limpeza agendada pra daqui {s}s.");
        }
    }
}
