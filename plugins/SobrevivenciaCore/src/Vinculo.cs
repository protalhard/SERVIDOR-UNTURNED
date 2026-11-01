using System;
using System.Collections.Generic;
using System.IO;

namespace SobrevivenciaCore
{
    /// <summary>Steam vinculada no Discord: o site grava vinculos_discord.tsv (discord, steam, quando)
    /// quando o jogador clica em "Vincular Steam" no #vincular. Libera o kit "discord".</summary>
    public partial class Plugin
    {
        string ArqVinculos => Path.Combine(Directory, "vinculos_discord.tsv");
        HashSet<ulong> vinculados = new HashSet<ulong>();
        DateTime vinculosLidos = DateTime.MinValue;

        public bool VinculadoDiscord(ulong steam)
        {
            try
            {
                var t = File.Exists(ArqVinculos) ? File.GetLastWriteTimeUtc(ArqVinculos) : DateTime.MinValue;
                if (t != vinculosLidos)
                {
                    var novo = new HashSet<ulong>();
                    if (t != DateTime.MinValue)
                        foreach (var l in File.ReadAllLines(ArqVinculos))
                        {
                            var c = l.Split('\t');
                            if (c.Length >= 2 && ulong.TryParse(c[1], out var s)) novo.Add(s);
                        }
                    vinculados = novo;
                    vinculosLidos = t;
                }
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
            return vinculados.Contains(steam);
        }
    }
}
