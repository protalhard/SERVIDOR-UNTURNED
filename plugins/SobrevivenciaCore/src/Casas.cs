using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SDG.Unturned;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Uma home com nome, presa a uma cama que o jogador colocou.</summary>
    public class Casa
    {
        public string Nome;
        public Vector3 Pos;
    }

    /// <summary>Homes com nome (/sethome, /home nome, /homes, /delhome, /renamehome). Arquivo homes.tsv.</summary>
    public class Casas
    {
        readonly string arquivo;
        readonly Dictionary<ulong, List<Casa>> casas = new Dictionary<ulong, List<Casa>>();
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public Casas(string pasta)
        {
            arquivo = Path.Combine(pasta, "homes.tsv");
            if (!File.Exists(arquivo)) return;
            foreach (var linha in File.ReadAllLines(arquivo))
            {
                var c = linha.Split('\t');
                if (c.Length < 5 || !ulong.TryParse(c[0], out var id)) continue;
                if (!float.TryParse(c[2], NumberStyles.Float, Inv, out var x) ||
                    !float.TryParse(c[3], NumberStyles.Float, Inv, out var y) ||
                    !float.TryParse(c[4], NumberStyles.Float, Inv, out var z)) continue;
                De(id).Add(new Casa { Nome = c[1], Pos = new Vector3(x, y, z) });
            }
        }

        public List<Casa> De(ulong id)
        {
            if (!casas.TryGetValue(id, out var l)) casas[id] = l = new List<Casa>();
            return l;
        }

        public Casa Achar(ulong id, string nome)
            => De(id).FirstOrDefault(c => c.Nome.Equals(nome, StringComparison.OrdinalIgnoreCase));

        public void Salvar()
        {
            var linhas = casas.SelectMany(kv => kv.Value.Select(c => string.Join("\t", kv.Key, c.Nome,
                c.Pos.x.ToString("0.##", Inv), c.Pos.y.ToString("0.##", Inv), c.Pos.z.ToString("0.##", Inv))));
            var tmp = arquivo + ".tmp";
            File.WriteAllLines(tmp, linhas);
            if (File.Exists(arquivo)) File.Delete(arquivo);
            File.Move(tmp, arquivo);
        }

        /// <summary>Cama colocada pelo jogador (ou pelo grupo dele) a até `raio` metros.</summary>
        public static BarricadeDrop CamaPerto(Vector3 pos, ulong dono, ulong grupo, float raio = 4f)
        {
            if (!Regions.tryGetCoordinate(pos, out var rx, out var ry)) return null;
            BarricadeDrop melhor = null;
            float menor = raio;
            for (int x = Math.Max(0, rx - 1); x <= Math.Min(Regions.WORLD_SIZE - 1, rx + 1); x++)
                for (int y = Math.Max(0, ry - 1); y <= Math.Min(Regions.WORLD_SIZE - 1, ry + 1); y++)
                    foreach (var d in BarricadeManager.regions[x, y].drops)
                    {
                        if (!(d.interactable is InteractableBed) || d.model == null) continue;
                        var sd = d.GetServersideData();
                        if (sd.owner != dono && (grupo == 0 || sd.group != grupo)) continue;
                        float dist = Vector3.Distance(pos, d.model.position);
                        if (dist <= menor) { menor = dist; melhor = d; }
                    }
            return melhor;
        }
    }
}
