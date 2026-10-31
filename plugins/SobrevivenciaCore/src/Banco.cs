using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SobrevivenciaCore
{
    /// <summary>Saldo do banco (ATM) de cada jogador, salvo em banco.txt (steamid=saldo).</summary>
    public class Banco
    {
        readonly string arquivo;
        readonly string arquivoKit;
        readonly Dictionary<ulong, long> saldos = new Dictionary<ulong, long>();
        readonly HashSet<ulong> jaRecebeuKit = new HashSet<ulong>();

        public Banco(string pasta)
        {
            Directory.CreateDirectory(pasta);
            arquivo = Path.Combine(pasta, "banco.txt");
            arquivoKit = Path.Combine(pasta, "kit_recebido.txt");
            if (File.Exists(arquivo))
                foreach (var linha in File.ReadAllLines(arquivo))
                {
                    var p = linha.Split('=');
                    if (p.Length == 2 && ulong.TryParse(p[0], out var id) && long.TryParse(p[1], out var v))
                        saldos[id] = v;
                }
            if (File.Exists(arquivoKit))
                foreach (var linha in File.ReadAllLines(arquivoKit))
                    if (ulong.TryParse(linha.Trim(), out var id)) jaRecebeuKit.Add(id);
        }

        public bool Existe(ulong id) => saldos.ContainsKey(id);
        public long Saldo(ulong id) => saldos.TryGetValue(id, out var v) ? v : 0;

        public void Somar(ulong id, long valor)
        {
            saldos[id] = Math.Max(0, Saldo(id) + valor);
            Salvar();
        }

        public bool PrimeiroKit(ulong id)
        {
            if (!jaRecebeuKit.Add(id)) return false;
            File.AppendAllText(arquivoKit, id + Environment.NewLine);
            return true;
        }

        void Salvar()
        {
            var tmp = arquivo + ".tmp";
            File.WriteAllLines(tmp, saldos.Select(kv => kv.Key + "=" + kv.Value));
            if (File.Exists(arquivo)) File.Delete(arquivo);
            File.Move(tmp, arquivo);
        }
    }
}
