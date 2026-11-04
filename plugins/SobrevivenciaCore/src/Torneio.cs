using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Torneio do fim de semana (pedido do dono): de sexta 18h a domingo 22h (Brasília) conta
    /// ZUMBIS (normal 1 ponto, mega 5) e CAÇA (bicho 1 ponto, peixe 1 ponto). No fim, os 3 primeiros de cada
    /// categoria ganham o prêmio no banco sozinhos. O site mostra o ranking ao vivo (torneio.json).</summary>
    public partial class Plugin
    {
        public class PontoTorneio { public string Nome; public int Pts; }
        public class EstadoTorneio
        {
            public string Semana = "";       // data (aaaa-mm-dd) da sexta do torneio
            public DateTime Inicio, Fim;      // UTC
            public bool Pago;
            public Dictionary<string, Dictionary<ulong, PontoTorneio>> Pontos = new Dictionary<string, Dictionary<ulong, PontoTorneio>>();
            public List<string> Vencedores = new List<string>();
            public DateTime Atualizado;
            public string Premios;
        }

        static readonly string[] CategoriasTorneio = { "zumbis", "caca" };
        EstadoTorneio torneio;
        float proximoSalvarTorneio;
        bool torneioMudou;
        string ArqTorneio => Path.Combine(Directory, "torneio.json");

        /// <summary>Sexta 18h até domingo 22h da semana de 'agora' (horário de Brasília). Devolve em UTC.</summary>
        void JanelaTorneio(DateTime agoraBr, out DateTime iniBr, out DateTime fimBr)
        {
            int desdeSexta = ((int)agoraBr.DayOfWeek - (int)DayOfWeek.Friday + 7) % 7;
            var sexta = agoraBr.Date.AddDays(-desdeSexta);
            iniBr = sexta.AddHours(S.TorneioInicioHora);
            fimBr = sexta.AddDays(2).AddHours(S.TorneioFimHora);
            if (agoraBr >= fimBr) { iniBr = iniBr.AddDays(7); fimBr = fimBr.AddDays(7); } // já acabou: o próximo
        }

        EstadoTorneio Torneio()
        {
            if (torneio == null)
            {
                try { if (File.Exists(ArqTorneio)) torneio = JsonConvert.DeserializeObject<EstadoTorneio>(File.ReadAllText(ArqTorneio)); }
                catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
                torneio = torneio ?? new EstadoTorneio();
            }
            return torneio;
        }

        void SalvarTorneio()
        {
            try
            {
                Torneio().Atualizado = DateTime.UtcNow;
                Torneio().Premios = S.TorneioPremios;
                var tmp = ArqTorneio + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(Torneio(), Formatting.Indented));
                if (File.Exists(ArqTorneio)) File.Delete(ArqTorneio);
                File.Move(tmp, ArqTorneio);
                torneioMudou = false;
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }

        bool TorneioAberto => S.TorneioLigado && DateTime.UtcNow >= Torneio().Inicio && DateTime.UtcNow < Torneio().Fim && !Torneio().Pago;

        /// <summary>Tudo que conta ponto passa aqui (torneio e missões): zumbi, mega, animal, peixe, pvp.</summary>
        void ContarAcao(UnturnedPlayer p, string acao, int qtd = 1)
        {
            try
            {
                if (TorneioAberto)
                {
                    string cat = null; int pts = 0;
                    switch (acao)
                    {
                        case "zumbi": cat = "zumbis"; pts = 1; break;
                        case "mega": cat = "zumbis"; pts = 5; break;
                        case "animal": cat = "caca"; pts = 1; break;
                        case "peixe": cat = "caca"; pts = 1; break;
                    }
                    if (cat != null)
                    {
                        var t = Torneio();
                        if (!t.Pontos.TryGetValue(cat, out var lista)) t.Pontos[cat] = lista = new Dictionary<ulong, PontoTorneio>();
                        if (!lista.TryGetValue(Id(p), out var pt)) lista[Id(p)] = pt = new PontoTorneio();
                        pt.Nome = p.CharacterName; pt.Pts += pts * qtd;
                        torneioMudou = true;
                    }
                }
                ContarMissao(p, acao, qtd);
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }

        void TickTorneio()
        {
            if (!S.TorneioLigado) return;
            var t = Torneio();
            var agoraBr = AgoraBrasil;
            // torneio novo quando passou o fim do anterior (e ele já foi pago)
            if (t.Semana == "" || (t.Pago && DateTime.UtcNow >= t.Fim))
            {
                JanelaTorneio(agoraBr, out var ini, out var fim);
                var semana = ini.ToString("yyyy-MM-dd");
                if (semana != t.Semana)
                {
                    torneio = t = new EstadoTorneio { Semana = semana, Inicio = ini - Fuso, Fim = fim - Fuso };
                    SalvarTorneio();
                }
            }
            if (!t.Pago && DateTime.UtcNow >= t.Fim) PagarTorneio();
            if (torneioMudou && Time.realtimeSinceStartup > proximoSalvarTorneio)
            {
                proximoSalvarTorneio = Time.realtimeSinceStartup + 30f;
                SalvarTorneio();
            }
        }

        long[] PremiosTorneio() => (S.TorneioPremios ?? "").Split(',').Select(x => long.TryParse(x.Trim(), out var v) ? v : 0).ToArray();

        void PagarTorneio()
        {
            var t = Torneio();
            t.Pago = true;
            var premios = PremiosTorneio();
            foreach (var cat in CategoriasTorneio)
            {
                if (!t.Pontos.TryGetValue(cat, out var lista)) continue;
                var top = lista.Where(x => x.Value.Pts > 0).OrderByDescending(x => x.Value.Pts).Take(premios.Length).ToList();
                for (int i = 0; i < top.Count; i++)
                {
                    if (premios[i] <= 0) continue;
                    Banco.Somar(top[i].Key, premios[i]);
                    var linha = $"{(cat == "zumbis" ? "Zumbis" : "Caca")} {i + 1}o: {top[i].Value.Nome} ({top[i].Value.Pts} pts) +${premios[i]:N0}";
                    t.Vencedores.Add(linha);
                    Rocket.Core.Logging.Logger.Log("[Torneio] " + linha);
                    var sp = PlayerTool.getSteamPlayer(top[i].Key);
                    if (sp != null) Chat.Info(UnturnedPlayer.FromSteamPlayer(sp), $"Voce ficou em {i + 1}o no torneio ({(cat == "zumbis" ? "zumbis" : "caca")}) e ganhou ${premios[i]:N0} no banco!");
                }
            }
            SalvarTorneio();
            if (t.Vencedores.Count > 0)
                Chat.Todos(Chat.Cor(S.CorDestaque, Chat.Forte("TORNEIO ACABOU! ")) + Chat.Cor("#FFFFFF", string.Join("  |  ", t.Vencedores.Select(Chat.Limpar))));
        }

        internal void TorneioMostrar(UnturnedPlayer p)
        {
            var t = Torneio();
            var br = t.Inicio + Fuso; var fb = t.Fim + Fuso;
            var estado = TorneioAberto ? $"valendo ate domingo {fb:HH}h" : (DateTime.UtcNow < t.Inicio ? $"comeca {br:dd/MM} as {br:HH}h" : "acabou");
            Chat.Para(p, Chat.Cor(S.CorDestaque, Chat.Forte("TORNEIO DO FIM DE SEMANA ")) + Chat.Cor(S.CorApagada, $"({estado})"));
            var premios = PremiosTorneio();
            Chat.Para(p, Chat.Cor("#FFFFFF", "Premios por categoria: " + string.Join(" / ", premios.Select((v, i) => $"{i + 1}o ${v:N0}"))));
            foreach (var cat in CategoriasTorneio)
            {
                var nome = cat == "zumbis" ? "ZUMBIS (mega vale 5)" : "CACA (bicho ou peixe)";
                var top = t.Pontos.TryGetValue(cat, out var l) ? l.OrderByDescending(x => x.Value.Pts).Take(3).ToList() : new List<KeyValuePair<ulong, PontoTorneio>>();
                var meu = l != null && l.TryGetValue(Id(p), out var m) ? m.Pts : 0;
                Chat.Para(p, Chat.Cor(S.CorDestaque, nome + ": ") + Chat.Cor("#FFFFFF", top.Count == 0 ? "ninguem ainda" : string.Join(", ", top.Select((x, i) => $"{i + 1}. {Chat.Limpar(x.Value.Nome)} {x.Value.Pts}")))
                    + Chat.Cor(S.CorApagada, $"  (voce: {meu})"));
            }
        }
    }

    public class CmdTorneio : ComandoSistema
    {
        public override string Name => "torneio";
        public override string Help => "Ranking do torneio do fim de semana.";
        protected override void Rodar(UnturnedPlayer p, string[] a) => P.TorneioMostrar(p);
    }
}
