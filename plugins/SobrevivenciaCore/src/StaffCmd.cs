using System;
using System.Collections.Generic;
using System.Linq;
using Rocket.Unturned.Player;
using SDG.Unturned;

namespace SobrevivenciaCore
{
    /// <summary>Gestão da staff fora do menu (chat e painel do site) e o /item pra spawnar itens.
    /// Só o dono mexe na staff; ninguém mexe no dono.</summary>
    public partial class Plugin
    {
        /// <summary>Liga/desliga uma permissão de alguém da staff. Devolve a mensagem pro dono.</summary>
        internal string MudarPermissao(ulong quem, string quemNome, ulong alvo, string perm, bool ligar)
        {
            if (!EhDono(quem)) return "So o dono mexe na staff.";
            perm = PermissoesAdmin.FirstOrDefault(x => x.Equals(perm, StringComparison.OrdinalIgnoreCase));
            if (perm == null) return "Permissao nao existe. Use: " + string.Join(", ", PermissoesAdmin);
            if (EhDono(alvo)) return "O dono ja tem tudo.";
            if (!staff.TryGetValue(alvo, out var m))
            {
                if (!ligar) return "Essa pessoa nao esta na staff.";
                staff[alvo] = m = new MembroStaff();
            }
            m.Nome = NomeDe(alvo);
            if (ligar) m.Perms.Add(perm); else m.Perms.Remove(perm);
            SalvarStaff();
            LogAdminNome(quemNome, quem, $"{(ligar ? "DEU" : "TIROU")} permissao {perm} {(ligar ? "para" : "de")} {m.Nome} ({alvo})");
            AtualizarStaffOnline(alvo);
            return $"{perm} {(ligar ? "liberado" : "tirado")} pra {Chat.Limpar(m.Nome)}.";
        }

        internal string RemoverStaffSite(ulong quem, string quemNome, ulong alvo)
        {
            if (!EhDono(quem)) return "So o dono mexe na staff.";
            if (EhDono(alvo)) return "Nao da pra tirar o dono.";
            if (!staff.TryGetValue(alvo, out var m)) return "Essa pessoa nao esta na staff.";
            staff.Remove(alvo);
            SalvarStaff();
            LogAdminNome(quemNome, quem, $"TIROU {m.Nome} ({alvo}) da staff");
            AtualizarStaffOnline(alvo);
            return Chat.Limpar(m.Nome) + " saiu da staff.";
        }

        void AtualizarStaffOnline(ulong alvo)
        {
            var sp = PlayerTool.getSteamPlayer(alvo);
            if (sp != null) AtualizarAcessoAdmin(UnturnedPlayer.FromSteamPlayer(sp));
        }

        /// <summary>Acha item por ID ou por parte do nome. Mais de um resultado = devolve a lista pra escolher.</summary>
        internal static List<ItemAsset> AcharItens(string busca)
        {
            if (ushort.TryParse(busca, out var id))
                return Assets.find(EAssetType.ITEM, id) is ItemAsset a ? new List<ItemAsset> { a } : new List<ItemAsset>();
            var todos = Assets.find(EAssetType.ITEM).OfType<ItemAsset>().Where(x => !string.IsNullOrEmpty(x.itemName)).ToList();
            var exato = todos.Where(x => x.itemName.Equals(busca, StringComparison.OrdinalIgnoreCase)).ToList();
            if (exato.Count > 0) return exato.Take(1).ToList();
            return todos.Where(x => x.itemName.IndexOf(busca, StringComparison.OrdinalIgnoreCase) >= 0)
                        .OrderBy(x => x.itemName.Length).Take(10).ToList();
        }
    }

    /// <summary>/item id|nome [qtd] [jogador]: spawna item (staff com a permissao Itens).</summary>
    public class CmdItem : ComandoSistema
    {
        public override string Name => "item";
        public override string Help => "Spawna um item (staff).";
        public override string Syntax => "<id ou nome> [quantidade] [jogador]";
        public override List<string> Aliases => new List<string> { "spawnar" };
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (!P.Pode(p, "Itens")) { Chat.Erro(p, "Voce nao tem permissao."); return; }
            if (a.Length < 1) { Chat.Info(p, "Use: /item id [qtd] [jogador]   Ex: /item 363 2   ou   /item \"maple rifle\""); return; }
            int qtd = 1; UnturnedPlayer alvo = p;
            var resto = a.ToList();
            // do fim pro começo: [jogador] e [qtd]
            if (resto.Count >= 3 || (resto.Count == 2 && !int.TryParse(resto[1], out _)))
            {
                var j = UnturnedPlayer.FromName(resto[resto.Count - 1]);
                if (j != null && resto.Count >= 2) { alvo = j; resto.RemoveAt(resto.Count - 1); }
            }
            if (resto.Count >= 2 && int.TryParse(resto[resto.Count - 1], out var q)) { qtd = q; resto.RemoveAt(resto.Count - 1); }
            qtd = Math.Max(1, Math.Min(qtd, 100));
            var busca = string.Join(" ", resto);
            var achados = Plugin.AcharItens(busca);
            if (achados.Count == 0) { Chat.Erro(p, "Item nao encontrado."); return; }
            if (achados.Count > 1)
            {
                Chat.Info(p, "Achei varios, use o ID:");
                foreach (var x in achados) Chat.Para(p, Cor(S.CorApagada, x.id + "  ") + Cor(S.CorTexto, x.itemName));
                return;
            }
            var it = achados[0];
            Itens.Dar(alvo, it.id, qtd);
            P.LogAdmin(p, $"DEU {qtd}x {it.itemName} ({it.id}) para {alvo.CharacterName} ({Plugin.Id(alvo)})");
            Chat.Ok(p, $"{qtd}x {Chat.Limpar(it.itemName)} para {Chat.Limpar(alvo.CharacterName)}.");
        }
    }

    public partial class Plugin
    {
        /// <summary>Skills no máximo (todas) ou zeradas, num jogador. Fica no log de admin.</summary>
        internal string MudarSkills(UnturnedPlayer adm, UnturnedPlayer alvo, bool max)
        {
            var sk = alvo.Player.skills;
            if (max) sk.ServerUnlockAllSkills();
            else
                for (int s = 0; s < sk.skills.Length; s++)
                    for (int k = 0; k < sk.skills[s].Length; k++)
                        sk.ServerSetSkillLevel(s, k, 0);
            LogAdmin(adm, (max ? "SKILLS FULL em " : "SKILLS ZERADAS em ") + $"{alvo.CharacterName} ({Id(alvo)})");
            if (Id(alvo) != Id(adm)) Chat.Ok(alvo, max ? "Um admin deixou suas skills no maximo." : "Um admin zerou suas skills.");
            return Chat.Limpar(alvo.CharacterName) + (max ? ": skills no maximo." : ": skills zeradas.");
        }
    }

    /// <summary>/skills [jogador] max|zerar: deixa as skills no máximo ou zera (staff com permissão Skills).</summary>
    public class CmdSkills : ComandoSistema
    {
        public override string Name => "skills";
        public override string Help => "Skills no maximo ou zeradas (staff).";
        public override string Syntax => "[jogador] max|zerar";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (!P.Pode(p, "Skills")) { Chat.Erro(p, "Voce nao tem permissao."); return; }
            if (a.Length < 1) { Chat.Info(p, "Use: /skills max  |  /skills zerar  |  /skills <jogador> max"); return; }
            var acao = a[a.Length - 1].ToLowerInvariant();
            bool max = acao == "max" || acao == "full" || acao == "tudo";
            if (!max && acao != "zerar" && acao != "zero") { Chat.Info(p, "Use: /skills [jogador] max|zerar"); return; }
            var alvo = p;
            if (a.Length > 1)
            {
                alvo = UnturnedPlayer.FromName(string.Join(" ", a.Take(a.Length - 1)));
                if (alvo == null) { Chat.Erro(p, "Jogador nao encontrado."); return; }
            }
            Chat.Ok(p, P.MudarSkills(p, alvo, max));
        }
    }
}
