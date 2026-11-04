using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Rocket.Unturned.Player;
using SDG.Unturned;

namespace SobrevivenciaCore
{
    /// <summary>Missões diárias (pedido do dono): todo dia (Brasília) saem 3 missões iguais pra todo mundo:
    /// uma fácil, uma média e uma difícil. Pagam dinheiro no banco + XP. Recebe na safe (no NPC de missão quando
    /// o boneco estiver no mod, ou com /missoes receber).</summary>
    public partial class Plugin
    {
        public class Missao
        {
            public string Id, Texto, Acao, Nivel; public int Meta; public long Dinheiro; public int Xp;
            public Missao() { }
            public Missao(string id, string nivel, string texto, string acao, int meta, long dinheiro, int xp)
            { Id = id; Nivel = nivel; Texto = texto; Acao = acao; Meta = meta; Dinheiro = dinheiro; Xp = xp; }
        }
        public class ProgressoMissao { public string Nome; public int[] Feito = new int[3]; public bool[] Recebeu = new bool[3]; }
        public class EstadoMissoes { public string Dia = ""; public List<string> Hoje = new List<string>(); public Dictionary<ulong, ProgressoMissao> Jogadores = new Dictionary<ulong, ProgressoMissao>(); }

        // valores pensados na economia: rifle comum 2-4,5 mil, mercado negro 6-28 mil, carne 15-45
        static readonly Missao[] TodasMissoes =
        {
            new Missao("z30", "facil", "Mate 30 zumbis", "zumbi", 30, 700, 100),
            new Missao("a3", "facil", "Cace 3 animais", "animal", 3, 600, 100),
            new Missao("p3", "facil", "Pesque 3 peixes", "peixe", 3, 600, 100),
            new Missao("z80", "media", "Mate 80 zumbis", "zumbi", 80, 1500, 250),
            new Missao("a8", "media", "Cace 8 animais", "animal", 8, 1500, 250),
            new Missao("p10", "media", "Pesque 10 peixes", "peixe", 10, 1500, 250),
            new Missao("m1", "media", "Mate 1 mega zumbi", "mega", 1, 1800, 300),
            new Missao("j3", "dificil", "Mate 3 jogadores", "pvp", 3, 3500, 500),
            new Missao("z200", "dificil", "Mate 200 zumbis", "zumbi", 200, 3000, 500),
            new Missao("m3", "dificil", "Mate 3 mega zumbis", "mega", 3, 3500, 500),
            new Missao("a20", "dificil", "Cace 20 animais", "animal", 20, 3000, 500),
        };

        EstadoMissoes missoes;
        bool missoesMudou;
        float proximoSalvarMissoes;
        string ArqMissoes => Path.Combine(Directory, "missoes.json");

        EstadoMissoes Missoes()
        {
            if (missoes == null)
            {
                try { if (File.Exists(ArqMissoes)) missoes = JsonConvert.DeserializeObject<EstadoMissoes>(File.ReadAllText(ArqMissoes)); }
                catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
                missoes = missoes ?? new EstadoMissoes();
            }
            var dia = AgoraBrasil.ToString("yyyy-MM-dd");
            if (missoes.Dia != dia)
            {
                // sorteio do dia: igual pra todo mundo e fixo no dia
                var r = new Random(dia.GetHashCode() ^ 0x5DA1);
                var hoje = new List<string>();
                foreach (var nivel in new[] { "facil", "media", "dificil" })
                {
                    var op = TodasMissoes.Where(m => m.Nivel == nivel).ToList();
                    hoje.Add(op[r.Next(op.Count)].Id);
                }
                missoes = new EstadoMissoes { Dia = dia, Hoje = hoje };
                SalvarMissoes();
            }
            return missoes;
        }

        List<Missao> MissoesDeHoje() => Missoes().Hoje.Select(id => TodasMissoes.FirstOrDefault(m => m.Id == id)).ToList();

        void SalvarMissoes()
        {
            try
            {
                var tmp = ArqMissoes + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(missoes, Formatting.Indented));
                if (File.Exists(ArqMissoes)) File.Delete(ArqMissoes);
                File.Move(tmp, ArqMissoes);
                missoesMudou = false;
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }

        ProgressoMissao ProgressoDe(UnturnedPlayer p)
        {
            var st = Missoes();
            if (!st.Jogadores.TryGetValue(Id(p), out var pr)) st.Jogadores[Id(p)] = pr = new ProgressoMissao();
            pr.Nome = p.CharacterName;
            return pr;
        }

        void ContarMissao(UnturnedPlayer p, string acao, int qtd)
        {
            if (!S.MissoesLigado) return;
            var hoje = MissoesDeHoje();
            ProgressoMissao pr = null;
            for (int i = 0; i < hoje.Count; i++)
            {
                var m = hoje[i];
                if (m == null || m.Acao != acao) continue;
                pr = pr ?? ProgressoDe(p);
                if (pr.Feito[i] >= m.Meta) continue;
                pr.Feito[i] = Math.Min(m.Meta, pr.Feito[i] + qtd);
                missoesMudou = true;
                if (pr.Feito[i] >= m.Meta)
                    Chat.Para(p, Chat.Cor(S.CorDestaque, Chat.Forte("MISSAO CUMPRIDA: ")) + Chat.Cor("#FFFFFF", $"{m.Texto}. Va na safe receber ${m.Dinheiro:N0} + {m.Xp} XP (/missoes receber)."));
            }
        }

        void TickMissoes()
        {
            if (!S.MissoesLigado) return;
            Missoes(); // vira o dia sozinho
            if (missoesMudou && UnityEngine.Time.realtimeSinceStartup > proximoSalvarMissoes)
            {
                proximoSalvarMissoes = UnityEngine.Time.realtimeSinceStartup + 30f;
                SalvarMissoes();
            }
        }

        internal void MissoesMostrar(UnturnedPlayer p)
        {
            if (!S.MissoesLigado) { Chat.Erro(p, "Missoes desligadas."); return; }
            var hoje = MissoesDeHoje(); var pr = ProgressoDe(p);
            Chat.Para(p, Chat.Cor(S.CorDestaque, Chat.Forte("MISSOES DE HOJE ")) + Chat.Cor(S.CorApagada, "(trocam a meia-noite)"));
            for (int i = 0; i < hoje.Count; i++)
            {
                var m = hoje[i]; if (m == null) continue;
                var nivel = m.Nivel == "facil" ? "FACIL" : m.Nivel == "media" ? "MEDIA" : "DIFICIL";
                var st = pr.Recebeu[i] ? Chat.Cor("#3DDC84", "recebida") : pr.Feito[i] >= m.Meta ? Chat.Cor(S.CorDestaque, "PRONTA, receba na safe") : Chat.Cor("#FFFFFF", $"{pr.Feito[i]}/{m.Meta}");
                Chat.Para(p, Chat.Cor(S.CorApagada, nivel + " ") + Chat.Cor("#FFFFFF", $"{m.Texto} ") + Chat.Cor(S.CorDestaque, $"${m.Dinheiro:N0} + {m.Xp} XP ") + st);
            }
        }

        internal void MissoesReceber(UnturnedPlayer p) => MissoesReceber(p, -1);

        /// <summary>qual = 0..2 recebe só aquela; -1 recebe todas as prontas. Devolve a mensagem (ou null se deu certo).</summary>
        internal string MissoesReceber(UnturnedPlayer p, int qual)
        {
            if (!S.MissoesLigado) { Chat.Erro(p, "Missoes desligadas."); return "Missoes desligadas."; }
            if (ZonaSeguraEm(p.Position) == null) { Chat.Erro(p, "Receba as missoes dentro da safe."); return "Receba dentro da safe."; }
            var hoje = MissoesDeHoje(); var pr = ProgressoDe(p);
            long din = 0; int xp = 0, n = 0;
            for (int i = 0; i < hoje.Count; i++)
            {
                var m = hoje[i];
                if (qual >= 0 && i != qual) continue;
                if (m == null || pr.Recebeu[i] || pr.Feito[i] < m.Meta) continue;
                pr.Recebeu[i] = true; din += m.Dinheiro; xp += m.Xp; n++;
            }
            if (n == 0) { Chat.Erro(p, "Nenhuma missao pronta pra receber. Veja com /missoes."); return "Nenhuma missao pronta."; }
            Banco.Somar(Id(p), din);
            DarXp(p, xp, "missao");
            SalvarMissoes();
            Chat.Info(p, $"Recebeu {n} missao(oes): ${din:N0} no banco + {xp} XP.");
            Rocket.Core.Logging.Logger.Log($"[Missoes] {p.CharacterName} ({Id(p)}) recebeu {n}: {din} + {xp} XP");
            return null;
        }
    }

    public class CmdMissoes : ComandoSistema
    {
        public override string Name => "missoes";
        public override string Help => "Missoes diarias.";
        public override List<string> Aliases => new List<string> { "missao" };
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (a.Length > 0 && a[0].ToLowerInvariant().StartsWith("rec")) P.MissoesReceber(p);
            else if (!P.AbrirMissoes(p)) P.MissoesMostrar(p);
        }
    }
}
