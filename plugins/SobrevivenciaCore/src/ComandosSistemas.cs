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
        public override string Help => "Teleporta pra sua cama (fique parado).";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (!BarricadeManager.tryGetBed(p.CSteamID, out var cama, out _))
            { Chat.Erro(p, "Voce nao tem cama. Coloque uma cama e deite nela pra marcar."); return; }
            if (!ForaDeCombate(p) || !SemIntervalo(p, "home")) return;
            P.AgendarTeleporte(p, "sua cama", S.HomeEspera,
                () => BarricadeManager.tryGetBed(p.CSteamID, out var c, out _) ? c : (Vector3?)null,
                () => P.IniciarIntervalo(p, "home", S.HomeIntervalo));
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
            var f = P.FichaDe(p);
            if (!P.TemPermissao(p, k.Permissao)) { Chat.Erro(p, "Esse kit e VIP."); return; }
            if (f.Abates < k.Abates) { Chat.Erro(p, $"Precisa de {k.Abates} abates. Voce tem {f.Abates}."); return; }
            var resta = P.Stats.UltimoUsoKit(f.Id, k.Nome) + k.Espera - Estatisticas.Agora;
            if (resta > 0) { Chat.Erro(p, $"Espere {Chat.Tempo(resta)} pra pegar de novo."); return; }
            foreach (var i in k.Itens ?? new List<ItemKit>())
                if (Itens.Existe(i.Id)) Itens.Dar(p, i.Id, i.Quantidade);
            P.Stats.MarcarKit(f.Id, k.Nome);
            P.Stats.Salvar();
            Chat.Ok(p, $"Voce recebeu o kit {k.Nome}.");
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
                if (!int.TryParse(a[1], out var n) || n < 1 || n > S.LojaXp.Count) { Chat.Erro(p, "Numero invalido. Veja /lojaxp."); return; }
                if (S.LojaXpSoNaSafe && !NaSafe(p)) { Chat.Erro(p, "A loja de XP so funciona dentro da safe zone."); return; }
                var pac = S.LojaXp[n - 1];
                if (f.XpSaldo < pac.Preco) { Chat.Erro(p, $"Voce tem {f.XpSaldo} XP. Precisa de {pac.Preco}."); return; }
                f.XpGasto += pac.Preco;
                P.Stats.Mudou();
                P.Stats.Salvar();
                foreach (var i in pac.Itens ?? new List<ItemKit>())
                    if (Itens.Existe(i.Id)) Itens.Dar(p, i.Id, i.Quantidade);
                Chat.Ok(p, $"Voce comprou o pacote {pac.Nome}. Sobrou {f.XpSaldo} XP.");
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
    }

    // ---------------- Vault ----------------

    public class CmdVault : ComandoSistema
    {
        public override string Name => "vault";
        public override string Help => "Abre seu cofre pessoal.";
        public override List<string> Aliases => new List<string> { "cofre" };
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (p.Dead) return;
            if (S.VaultSoNaSafe && !NaSafe(p)) { Chat.Erro(p, "O cofre so abre dentro da safe zone."); return; }
            if (!ForaDeCombate(p)) return;
            if (p.Player.movement.getVehicle() != null) { Chat.Erro(p, "Saia do veiculo pra abrir o cofre."); return; }
            var tam = S.Vaults.Where(v => P.TemPermissao(p, v.Permissao))
                .OrderByDescending(v => v.Largura * v.Altura).FirstOrDefault();
            if (tam == null) { Chat.Erro(p, "Voce nao tem cofre."); return; }
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
            string C(string cmd) => Cor(S.CorPrincipal, cmd);
            string T(string t) => Cor(S.CorApagada, t);
            Chat.Para(p, Cor(S.CorTexto, Chat.Forte("MENU - DAY Z BRASIL OFICIAL")));
            Chat.Para(p, T("Kits: ") + C("/kits") + " " + C("/kit nome") + T("   Loja de XP: ") + C("/lojaxp"));
            Chat.Para(p, T("Dinheiro: ") + C("/saldo") + " " + C("/depositar") + " " + C("/sacar") + T("   Lojas: ") + C("/lojas") + " " + C("/loja"));
            Chat.Para(p, T("Teleporte: ") + C("/safe") + " " + C("/home") + " " + C("/tpa nome") + " " + C("/tpaceitar"));
            Chat.Para(p, T("Voce: ") + C("/perfil") + " " + C("/top") + " " + C("/vault") + T("   Airdrop: ") + C("/whenairdrop"));
            Chat.Para(p, T("Comunidade: ") + C("/discord") + " " + C("/site"));
        }
    }

    public class CmdDiscord : ComandoSistema
    {
        public override string Name => "discord";
        public override string Help => "Link do Discord do servidor.";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (string.IsNullOrWhiteSpace(S.LinkDiscord)) { Chat.Info(p, "O Discord ainda vai ser divulgado. Fique ligado!"); return; }
            p.Player.sendBrowserRequest("Discord do DAY Z BRASIL OFICIAL", S.LinkDiscord);
            Chat.Info(p, "Discord: " + S.LinkDiscord);
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
