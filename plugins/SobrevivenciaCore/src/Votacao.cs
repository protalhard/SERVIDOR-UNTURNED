using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Votação no jogo criada pelo painel admin do site (enquete_jogo.txt).
    /// Jogador vota com /votar 1, /votar 2... (1 voto por Steam, pode trocar). Votos em enquete_votos.tsv,
    /// que o painel lê pra mostrar junto com os votos do Discord.</summary>
    public partial class Plugin
    {
        class Votacao
        {
            public string Id, Pergunta;
            public long Termina;
            public List<string> Opcoes = new List<string>();
            public bool Aberta => DateTimeOffset.UtcNow.ToUnixTimeSeconds() < Termina;
        }

        string ArqVotacao => Path.Combine(Directory, "enquete_jogo.txt");
        string ArqVotos => Path.Combine(Directory, "enquete_votos.tsv");
        Votacao votacao;
        DateTime votacaoLida = DateTime.MinValue;
        readonly Dictionary<ulong, int> votos = new Dictionary<ulong, int>();
        float proxAvisoVotacao, proxLerVotacao;

        // linha: id \t termina(unix) \t pergunta \t opcao1|opcao2|...
        void LerVotacao()
        {
            try
            {
                if (!File.Exists(ArqVotacao)) { votacao = null; return; }
                var quando = File.GetLastWriteTimeUtc(ArqVotacao);
                if (quando == votacaoLida && votacao != null) return;
                votacaoLida = quando;
                var c = File.ReadAllText(ArqVotacao).Trim().Split('\t');
                if (c.Length < 4 || !long.TryParse(c[1], out var termina)) { votacao = null; return; }
                var nova = new Votacao { Id = c[0], Termina = termina, Pergunta = Chat.Limpar(c[2]),
                    Opcoes = c[3].Split('|').Select(o => Chat.Limpar(o.Trim())).Where(o => o != "").Take(10).ToList() };
                if (nova.Opcoes.Count < 2) { votacao = null; return; }
                bool trocou = votacao == null || votacao.Id != nova.Id;
                votacao = nova;
                votos.Clear();
                if (File.Exists(ArqVotos))
                    foreach (var l in File.ReadAllLines(ArqVotos))
                    {
                        var v = l.Split('\t');
                        if (v.Length >= 3 && v[0] == nova.Id && ulong.TryParse(v[1], out var sid) && int.TryParse(v[2], out var o) && o >= 1 && o <= nova.Opcoes.Count)
                            votos[sid] = o;
                    }
                if (trocou && nova.Aberta) { AnunciarVotacao(null); proxAvisoVotacao = Time.realtimeSinceStartup + S.VotacaoAvisoMinutos * 60f; }
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }

        void SalvarVotos()
        {
            if (votacao == null) return;
            try
            {
                var outras = File.Exists(ArqVotos) ? File.ReadAllLines(ArqVotos).Where(l => !l.StartsWith(votacao.Id + "\t")) : Enumerable.Empty<string>();
                File.WriteAllLines(ArqVotos, outras.Concat(votos.Select(kv => $"{votacao.Id}\t{kv.Key}\t{kv.Value}")).ToArray());
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }

        /// <summary>Mostra a votação no chat (pra um jogador ou pra todos).</summary>
        void AnunciarVotacao(UnturnedPlayer p)
        {
            if (votacao == null || !votacao.Aberta) return;
            void Linha(string t) { if (p == null) Chat.Todos(t); else Chat.Para(p, t); }
            Linha(Chat.Cor(S.CorDestaque, Chat.Forte("VOTACAO: ")) + Chat.Cor("#FFFFFF", Chat.Forte(votacao.Pergunta)));
            for (int i = 0; i < votacao.Opcoes.Count; i++)
                Linha(Chat.Cor(S.CorPrincipal, Chat.Forte($"/votar {i + 1}")) + Chat.Cor(S.CorTexto, "  " + votacao.Opcoes[i]));
            var resta = votacao.Termina - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Linha(Chat.Cor(S.CorApagada, $"1 voto por pessoa (pode trocar). Termina em {Chat.Tempo(resta)}."));
        }

        void TickVotacao()
        {
            float agora = Time.realtimeSinceStartup;
            if (agora >= proxLerVotacao) { proxLerVotacao = agora + 10f; LerVotacao(); }
            if (votacao != null && votacao.Aberta && Provider.clients.Count > 0 && agora >= proxAvisoVotacao)
            {
                proxAvisoVotacao = agora + Math.Max(2, S.VotacaoAvisoMinutos) * 60f;
                AnunciarVotacao(null);
            }
        }

        void VotacaoAoEntrar(UnturnedPlayer p)
        {
            if (votacao == null || !votacao.Aberta) return;
            if (votos.ContainsKey(Id(p))) return;
            AnunciarVotacao(p);
        }

        public void Votar(UnturnedPlayer p, string[] a)
        {
            LerVotacao();
            if (votacao == null || !votacao.Aberta) { Chat.Erro(p, "Nenhuma votacao aberta agora."); return; }
            if (a.Length < 1 || !int.TryParse(a[0], out var n) || n < 1 || n > votacao.Opcoes.Count)
            {
                AnunciarVotacao(p);
                if (votos.TryGetValue(Id(p), out var atual)) Chat.Info(p, $"Seu voto atual: {atual} ({votacao.Opcoes[atual - 1]}).");
                return;
            }
            bool trocou = votos.ContainsKey(Id(p));
            votos[Id(p)] = n;
            SalvarVotos();
            Chat.Ok(p, (trocou ? "Voto trocado: " : "Voto registrado: ") + $"{n} ({votacao.Opcoes[n - 1]}). Obrigado!");
        }
    }

    /// <summary>/votar numero: vota na votação aberta pelo painel.</summary>
    public class CmdVotar : ComandoSistema
    {
        public override string Name => "votar";
        public override string Help => "Vota na votacao aberta (ex.: /votar 1).";
        public override string Syntax => "<numero>";
        public override List<string> Aliases => new List<string> { "voto", "vote" };
        protected override void Rodar(UnturnedPlayer p, string[] a) => P.Votar(p, a);
    }
}
