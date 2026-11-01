using System;
using System.Collections.Generic;
using System.Linq;
using Rocket.API;
using Rocket.Unturned.Player;
using UnityEngine;

namespace SobrevivenciaCore
{
    public abstract class ComandoBase : IRocketCommand
    {
        public virtual AllowedCaller AllowedCaller => AllowedCaller.Player;
        public abstract string Name { get; }
        public abstract string Help { get; }
        public virtual string Syntax => "";
        public virtual List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { Name };

        protected Plugin P => Plugin.Instancia;
        protected Config C => Plugin.Instancia.Configuration.Instance;
        protected void Salvar() => Plugin.Instancia.Configuration.Save();

        public void Execute(IRocketPlayer caller, string[] args)
        {
            try { Rodar((UnturnedPlayer)caller, args); }
            catch (Exception e)
            {
                Rocket.Core.Logging.Logger.LogException(e);
                P.Erro((UnturnedPlayer)caller, "Erro ao executar o comando.");
            }
        }

        protected abstract void Rodar(UnturnedPlayer p, string[] a);

        protected static bool LerValor(string s, out long v) => long.TryParse(s, out v) && v > 0;
    }

    // ======================= Jogadores =======================

    public class CmdSaldo : ComandoBase
    {
        public override string Name => "saldo";
        public override string Help => "Mostra seu saldo no banco e o dinheiro no bolso.";
        public override List<string> Aliases => new List<string> { "dinheiro" };
        protected override void Rodar(UnturnedPlayer p, string[] a)
            => P.Msg(p, $"Banco: ${P.Banco.Saldo(p.CSteamID.m_SteamID)} | No bolso: ${Dinheiro.NoBolso(p)}");
    }

    public class CmdDepositar : ComandoBase
    {
        public override string Name => "depositar";
        public override string Help => "Deposita dinheiro no ATM.";
        public override string Syntax => "<valor|tudo>";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (P.AtmPerto(p.Position) == null) { P.Erro(p, "Voce precisa estar perto de um ATM."); return; }
            if (a.Length < 1) { P.Erro(p, "Use: /depositar <valor|tudo>"); return; }
            var bolso = Dinheiro.NoBolso(p);
            long valor;
            if (a[0].Equals("tudo", StringComparison.OrdinalIgnoreCase)) valor = bolso;
            else if (!LerValor(a[0], out valor)) { P.Erro(p, "Valor invalido."); return; }
            if (valor <= 0 || valor > bolso) { P.Erro(p, $"Voce so tem ${bolso} no bolso."); return; }
            Dinheiro.Pagar(p, valor);
            P.Banco.Somar(p.CSteamID.m_SteamID, valor);
            P.Msg(p, $"Depositou ${valor}. Saldo no banco: ${P.Banco.Saldo(p.CSteamID.m_SteamID)}");
        }
    }

    public class CmdSacar : ComandoBase
    {
        public override string Name => "sacar";
        public override string Help => "Saca dinheiro do ATM.";
        public override string Syntax => "<valor|tudo>";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (P.AtmPerto(p.Position) == null) { P.Erro(p, "Voce precisa estar perto de um ATM."); return; }
            if (a.Length < 1) { P.Erro(p, "Use: /sacar <valor|tudo>"); return; }
            var id = p.CSteamID.m_SteamID;
            var saldo = P.Banco.Saldo(id);
            long valor;
            if (a[0].Equals("tudo", StringComparison.OrdinalIgnoreCase)) valor = saldo;
            else if (!LerValor(a[0], out valor)) { P.Erro(p, "Valor invalido."); return; }
            if (valor <= 0 || valor > saldo) { P.Erro(p, $"Saldo insuficiente. Banco: ${saldo}"); return; }
            if (valor > 20000) { P.Erro(p, "Limite por saque: $20000."); return; }
            P.Banco.Somar(id, -valor);
            Dinheiro.Dar(p, valor);
            P.Msg(p, $"Sacou ${valor}. Saldo no banco: ${P.Banco.Saldo(id)}");
        }
    }

    public class CmdLojas : ComandoBase
    {
        public override string Name => "lojas";
        public override string Help => "Lista as lojas, ATMs e safe zones e a distancia ate elas.";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            foreach (var l in C.Lojas.Where(l => l.Definido))
                P.Msg(p, $"{l.Nome}: {Mathf.RoundToInt(Vector3.Distance(p.Position, new Vector3(l.X, l.Y, l.Z)))}m");
            foreach (var t in C.Atms)
                P.Msg(p, $"ATM {t.Nome}: {Mathf.RoundToInt(Vector3.Distance(p.Position, new Vector3(t.X, t.Y, t.Z)))}m");
            foreach (var z in C.ZonasSeguras)
                P.Msg(p, $"Safe zone {z.Nome}: {Mathf.RoundToInt(Vector3.Distance(p.Position, new Vector3(z.X, z.Y, z.Z)))}m");
            if (!C.Lojas.Any(l => l.Definido)) P.Msg(p, "Nenhuma loja posicionada ainda.");
        }
    }

    public class CmdLoja : ComandoBase
    {
        public override string Name => "loja";
        public override string Help => "Mostra os itens da loja em que voce esta.";
        public override string Syntax => "[pagina]";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            var loja = P.LojaPerto(p.Position);
            if (loja == null) { P.Erro(p, "Voce nao esta em nenhuma loja. Use /lojas para ver onde ficam."); return; }
            int pagina = a.Length > 0 && int.TryParse(a[0], out var pg) ? Math.Max(1, pg) : 1;
            const int porPagina = 6;
            int total = (loja.Itens.Count + porPagina - 1) / porPagina;
            pagina = Math.Min(pagina, Math.Max(1, total));
            P.Msg(p, $"== {loja.Nome} (pag {pagina}/{total}) == /comprar <n> [qtd]  /vender <n> [qtd]");
            for (int i = (pagina - 1) * porPagina; i < Math.Min(loja.Itens.Count, pagina * porPagina); i++)
            {
                var it = loja.Itens[i];
                var compra = it.Compra > 0 ? $"compra ${it.Compra}" : "";
                var venda = it.Venda > 0 ? $"vende ${it.Venda}" : "";
                P.Msg(p, $"{i + 1}. {Itens.Nome(it.Id)}  {compra}  {venda}".Trim());
            }
        }
    }

    public class CmdComprar : ComandoBase
    {
        public override string Name => "comprar";
        public override string Help => "Compra um item da loja.";
        public override string Syntax => "<numero> [quantidade]";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            var loja = P.LojaPerto(p.Position);
            if (loja == null) { P.Erro(p, "Voce nao esta em nenhuma loja."); return; }
            if (a.Length < 1 || !int.TryParse(a[0], out var n) || n < 1 || n > loja.Itens.Count)
            { P.Erro(p, "Use: /comprar <numero> [quantidade] (veja os numeros em /loja)"); return; }
            int qtd = a.Length > 1 && int.TryParse(a[1], out var q) ? Mathf.Clamp(q, 1, 20) : 1;
            var it = loja.Itens[n - 1];
            if (it.Compra <= 0) { P.Erro(p, "Essa loja nao vende esse item."); return; }
            if (!Itens.Existe(it.Id)) { P.Erro(p, "Item invalido na configuracao."); return; }
            long preco = (long)it.Compra * qtd;
            if (!Dinheiro.Pagar(p, preco))
            { P.Erro(p, $"Dinheiro insuficiente. Custa ${preco}, voce tem ${Dinheiro.NoBolso(p)} no bolso. Saque no ATM."); return; }
            Itens.Dar(p, it.Id, qtd);
            P.Msg(p, $"Comprou {qtd}x {Itens.Nome(it.Id)} por ${preco}.");
        }
    }

    public class CmdVender : ComandoBase
    {
        public override string Name => "vender";
        public override string Help => "Vende um item para a loja.";
        public override string Syntax => "<numero> [quantidade|tudo]";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            var loja = P.LojaPerto(p.Position);
            if (loja == null) { P.Erro(p, "Voce nao esta em nenhuma loja."); return; }
            if (a.Length < 1 || !int.TryParse(a[0], out var n) || n < 1 || n > loja.Itens.Count)
            { P.Erro(p, "Use: /vender <numero> [quantidade|tudo]"); return; }
            var it = loja.Itens[n - 1];
            if (it.Venda <= 0) { P.Erro(p, "Essa loja nao compra esse item."); return; }
            int tem = Itens.Contar(p, it.Id);
            int qtd = a.Length > 1 && a[1].Equals("tudo", StringComparison.OrdinalIgnoreCase) ? tem
                : a.Length > 1 && int.TryParse(a[1], out var q) ? Math.Max(1, q) : 1;
            if (tem == 0) { P.Erro(p, $"Voce nao tem {Itens.Nome(it.Id)}."); return; }
            qtd = Math.Min(qtd, tem);
            int removidos = Itens.Remover(p, it.Id, qtd);
            long ganho = (long)it.Venda * removidos;
            Dinheiro.Dar(p, ganho);
            P.Msg(p, $"Vendeu {removidos}x {Itens.Nome(it.Id)} por ${ganho}.");
        }
    }

    public class CmdVenderTudo : ComandoBase
    {
        public override string Name => "vendertudo";
        public override string Help => "Vende tudo que a loja compra (bom para o cacador).";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            var loja = P.LojaPerto(p.Position);
            if (loja == null) { P.Erro(p, "Voce nao esta em nenhuma loja."); return; }
            long total = 0; int itens = 0;
            foreach (var it in loja.Itens.Where(i => i.Venda > 0 && i.Compra == 0))
            {
                int r = Itens.Remover(p, it.Id, Itens.Contar(p, it.Id));
                itens += r; total += (long)r * it.Venda;
            }
            if (itens == 0) { P.Erro(p, "Voce nao tem nada que essa loja compre."); return; }
            Dinheiro.Dar(p, total);
            P.Msg(p, $"Vendeu {itens} itens por ${total}.");
        }
    }

    // ======================= Admin =======================

    public abstract class ComandoAdmin : ComandoBase
    {
        public override AllowedCaller AllowedCaller => AllowedCaller.Player;
    }

    public class CmdSetLoja : ComandoAdmin
    {
        public override string Name => "setloja";
        public override string Help => "Coloca a loja na sua posicao atual.";
        public override string Syntax => "<armas|comida|roupas|remedios|equipamentos|garagem|cacador|negro> [raio]";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (a.Length < 1) { P.Erro(p, "Use: /setloja <id> [raio]. Ids: " + string.Join(", ", C.Lojas.Select(l => l.Id))); return; }
            var loja = C.Lojas.FirstOrDefault(l => l.Id.Equals(a[0], StringComparison.OrdinalIgnoreCase));
            if (loja == null) { P.Erro(p, "Loja nao encontrada. Ids: " + string.Join(", ", C.Lojas.Select(l => l.Id))); return; }
            loja.X = p.Position.x; loja.Y = p.Position.y; loja.Z = p.Position.z;
            if (a.Length > 1 && float.TryParse(a[1], out var r)) loja.Raio = r;
            Salvar();
            P.Msg(p, $"{loja.Nome} posicionada aqui (raio {loja.Raio}m).");
        }
    }

    public class CmdAddAtm : ComandoAdmin
    {
        public override string Name => "addatm";
        public override string Help => "Cria um ATM na sua posicao.";
        public override string Syntax => "<nome> [raio]";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            var nome = a.Length > 0 ? a[0] : "ATM" + (C.Atms.Count + 1);
            float raio = a.Length > 1 && float.TryParse(a[1], out var r) ? r : 4f;
            C.Atms.RemoveAll(x => x.Nome.Equals(nome, StringComparison.OrdinalIgnoreCase));
            C.Atms.Add(new Ponto(nome, raio) { X = p.Position.x, Y = p.Position.y, Z = p.Position.z });
            Salvar();
            P.Msg(p, $"ATM '{nome}' criado aqui (raio {raio}m).");
        }
    }

    public class CmdDelAtm : ComandoAdmin
    {
        public override string Name => "delatm";
        public override string Help => "Remove um ATM.";
        public override string Syntax => "<nome>";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (a.Length < 1) { P.Erro(p, "Use: /delatm <nome>"); return; }
            int n = C.Atms.RemoveAll(x => x.Nome.Equals(a[0], StringComparison.OrdinalIgnoreCase));
            Salvar();
            P.Msg(p, n > 0 ? "ATM removido." : "ATM nao encontrado.");
        }
    }

    public class CmdAddSafe : ComandoAdmin
    {
        public override string Name => "addsafe";
        public override string Help => "Cria uma safe zone (sem PvP) com centro na sua posicao.";
        public override string Syntax => "<nome> <raio>";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (a.Length < 2 || !float.TryParse(a[1], out var raio)) { P.Erro(p, "Use: /addsafe <nome> <raio em metros>"); return; }
            C.ZonasSeguras.RemoveAll(x => x.Nome.Equals(a[0], StringComparison.OrdinalIgnoreCase));
            C.ZonasSeguras.Add(new Ponto(a[0], raio) { X = p.Position.x, Y = p.Position.y, Z = p.Position.z });
            Salvar();
            P.Msg(p, $"Safe zone '{a[0]}' criada com raio {raio}m.");
        }
    }

    public class CmdDelSafe : ComandoAdmin
    {
        public override string Name => "delsafe";
        public override string Help => "Remove uma safe zone.";
        public override string Syntax => "<nome>";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (a.Length < 1) { P.Erro(p, "Use: /delsafe <nome>"); return; }
            int n = C.ZonasSeguras.RemoveAll(x => x.Nome.Equals(a[0], StringComparison.OrdinalIgnoreCase));
            Salvar();
            P.Msg(p, n > 0 ? "Safe zone removida." : "Safe zone nao encontrada.");
        }
    }

    public class CmdDarDinheiro : ComandoAdmin
    {
        public override string Name => "dardinheiro";
        public override string Help => "Coloca dinheiro no banco de um jogador (use negativo para tirar).";
        public override string Syntax => "<jogador> <valor>";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (a.Length < 2 || !long.TryParse(a[1], out var v)) { P.Erro(p, "Use: /dardinheiro <jogador> <valor>"); return; }
            var alvo = UnturnedPlayer.FromName(a[0]);
            if (alvo == null) { P.Erro(p, "Jogador nao encontrado."); return; }
            P.Banco.Somar(alvo.CSteamID.m_SteamID, v);
            P.Msg(p, $"Banco de {alvo.CharacterName}: ${P.Banco.Saldo(alvo.CSteamID.m_SteamID)}");
            P.Msg(alvo, $"Um admin alterou seu banco em ${v}.");
        }
    }

    public class CmdKitInicial : ComandoAdmin
    {
        public override string Name => "darkit";
        public override string Help => "Da o kit inicial para voce ou outro jogador.";
        public override string Syntax => "[jogador]";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            var alvo = a.Length > 0 ? UnturnedPlayer.FromName(a[0]) : p;
            if (alvo == null) { P.Erro(p, "Jogador nao encontrado."); return; }
            P.DarKit(alvo);
            P.Msg(p, $"Kit inicial entregue para {alvo.CharacterName}.");
        }
    }
}
