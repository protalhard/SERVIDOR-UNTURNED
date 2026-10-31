using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SobrevivenciaCore
{
    public class Ficha
    {
        public ulong Id;
        public string Nome = "";
        public int Abates, Mortes, Cabeca, Zumbis, Megas;
        public long Segundos, Xp, XpGasto, PrimeiraVez;

        public long XpSaldo => Math.Max(0, Xp - XpGasto);
        public double Kd => Mortes == 0 ? Abates : Math.Round((double)Abates / Mortes, 2);
        public int PorcentoCabeca => Abates == 0 ? 0 : (int)Math.Round(100.0 * Cabeca / Abates);

        public static int NivelDe(long xp) => 1 + (int)Math.Floor(Math.Sqrt(Math.Max(0, xp) / 100.0));
        public static long XpDoNivel(int nivel) => (long)(nivel - 1) * (nivel - 1) * 100;
        public int Nivel => NivelDe(Xp);
    }

    /// <summary>Estatísticas, XP e esperas de kit de cada jogador (arquivos .tsv na pasta do plugin).</summary>
    public class Estatisticas
    {
        readonly string arqFichas, arqKits;
        readonly Dictionary<ulong, Ficha> fichas = new Dictionary<ulong, Ficha>();
        readonly Dictionary<string, long> usoKit = new Dictionary<string, long>(); // "steamid|kit" -> unix
        bool sujo, sujoKits;

        public Estatisticas(string pasta)
        {
            Directory.CreateDirectory(pasta);
            arqFichas = Path.Combine(pasta, "estatisticas.tsv");
            arqKits = Path.Combine(pasta, "kits_uso.tsv");
            if (File.Exists(arqFichas))
                foreach (var l in File.ReadAllLines(arqFichas))
                {
                    var c = l.Split('\t');
                    if (c.Length < 12 || !ulong.TryParse(c[0], out var id)) continue;
                    fichas[id] = new Ficha
                    {
                        Id = id, Nome = c[1], Abates = Int(c[2]), Mortes = Int(c[3]), Cabeca = Int(c[4]),
                        Zumbis = Int(c[5]), Megas = Int(c[6]), Segundos = Long(c[7]), Xp = Long(c[8]),
                        XpGasto = Long(c[9]), PrimeiraVez = Long(c[10]),
                    };
                }
            if (File.Exists(arqKits))
                foreach (var l in File.ReadAllLines(arqKits))
                {
                    var c = l.Split('\t');
                    if (c.Length == 2 && long.TryParse(c[1], out var t)) usoKit[c[0]] = t;
                }
        }

        static int Int(string s) => int.TryParse(s, out var v) ? v : 0;
        static long Long(string s) => long.TryParse(s, out var v) ? v : 0;
        public static long Agora => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public bool Existe(ulong id) => fichas.ContainsKey(id);

        public Ficha Obter(ulong id, string nome = null)
        {
            if (!fichas.TryGetValue(id, out var f))
            {
                f = new Ficha { Id = id, PrimeiraVez = Agora };
                fichas[id] = f;
                sujo = true;
            }
            if (nome != null && f.Nome != nome) { f.Nome = nome.Replace('\t', ' '); sujo = true; }
            return f;
        }

        public Ficha Buscar(string nome)
        {
            if (string.IsNullOrWhiteSpace(nome)) return null;
            return fichas.Values.FirstOrDefault(f => f.Nome.Equals(nome, StringComparison.OrdinalIgnoreCase))
                ?? fichas.Values.FirstOrDefault(f => f.Nome.IndexOf(nome, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public IEnumerable<Ficha> Todas => fichas.Values;
        public void Mudou() => sujo = true;

        public long UltimoUsoKit(ulong id, string kit)
            => usoKit.TryGetValue(id + "|" + kit.ToLowerInvariant(), out var t) ? t : 0;

        public void MarcarKit(ulong id, string kit)
        {
            usoKit[id + "|" + kit.ToLowerInvariant()] = Agora;
            sujoKits = true;
        }

        public void Salvar(bool forcar = false)
        {
            if (sujo || forcar)
            {
                Gravar(arqFichas, fichas.Values.Select(f => string.Join("\t", f.Id, f.Nome, f.Abates, f.Mortes,
                    f.Cabeca, f.Zumbis, f.Megas, f.Segundos, f.Xp, f.XpGasto, f.PrimeiraVez, "")));
                sujo = false;
            }
            if (sujoKits || forcar)
            {
                Gravar(arqKits, usoKit.Select(kv => kv.Key + "\t" + kv.Value));
                sujoKits = false;
            }
        }

        static void Gravar(string arquivo, IEnumerable<string> linhas)
        {
            var tmp = arquivo + ".tmp";
            File.WriteAllLines(tmp, linhas);
            if (File.Exists(arquivo)) File.Delete(arquivo);
            File.Move(tmp, arquivo);
        }
    }
}
