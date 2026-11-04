using System;
using System.IO;
using System.Linq;
using SDG.Unturned;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Estatísticas do painel: a cada 5 min grava quantos estão online (online_hist.tsv: quando UTC, quantos).
    /// O site junta isso com o jogadores_log.tsv pra montar pico, horário, tempo de jogo e novatos que voltaram.</summary>
    public partial class Plugin
    {
        float proximaFotoOnline = -1f;
        int fotosOnline;
        string ArqOnlineHist => Path.Combine(Directory, "online_hist.tsv");

        void TickFotoOnline()
        {
            if (Time.realtimeSinceStartup < proximaFotoOnline) return;
            proximaFotoOnline = Time.realtimeSinceStartup + 300f;
            try
            {
                File.AppendAllText(ArqOnlineHist, $"{DateTime.UtcNow:o}\t{Provider.clients.Count}{Environment.NewLine}");
                if (++fotosOnline >= 200)
                {
                    fotosOnline = 0;
                    var l = File.ReadAllLines(ArqOnlineHist);
                    if (l.Length > 30000) File.WriteAllLines(ArqOnlineHist, l.Skip(l.Length - 30000));
                }
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }
    }
}
