using System;
using System.IO;
using System.Linq;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Pausa de manutenção (botão do painel do site): o jogo continua ligado, mas só a staff fica e entra.
    /// Quem não é staff é chutado com aviso. O estado fica num arquivo, então vale também depois de um reinício.</summary>
    public partial class Plugin
    {
        string ArqPausa => Path.Combine(Directory, "pausa.txt");

        public bool EmPausa => File.Exists(ArqPausa);

        const string MsgPausa = "Servidor em manutencao rapida. Volte em alguns minutos!";

        public void MudarPausa(bool ligar, string motivo)
        {
            if (ligar)
            {
                try { File.WriteAllText(ArqPausa, DateTime.UtcNow.ToString("o") + "\t" + motivo); } catch { }
                Chat.Todos(Chat.Cor(S.CorPerigo, Chat.Forte("SERVIDOR EM MANUTENCAO: ")) + Chat.Cor("#FFFFFF", "voce sera desconectado em 10 segundos. Volte em alguns minutos."));
                try { SaveManager.save(); } catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
                StartCoroutine(ChutarForaDaStaff(10f));
            }
            else
            {
                try { if (File.Exists(ArqPausa)) File.Delete(ArqPausa); } catch { }
                Chat.Todos(Chat.Cor(S.CorOk, Chat.Forte("Manutencao terminada: o servidor esta liberado de novo.")));
            }
        }

        System.Collections.IEnumerator ChutarForaDaStaff(float espera)
        {
            yield return new WaitForSeconds(espera);
            if (!EmPausa) yield break;
            foreach (var sp in Provider.clients.ToList())
            {
                var p = UnturnedPlayer.FromSteamPlayer(sp);
                if (p == null || EhStaff(p) || p.IsAdmin) continue;
                Provider.kick(p.CSteamID, MsgPausa);
            }
        }

        /// <summary>Na entrada: em pausa, quem não é staff é chutado.</summary>
        void BarrarNaPausa(UnturnedPlayer p)
        {
            if (!EmPausa || p == null || EhStaff(p) || p.IsAdmin) return;
            StartCoroutine(Chutar(p));
        }

        System.Collections.IEnumerator Chutar(UnturnedPlayer p)
        {
            yield return new WaitForSeconds(1f);
            if (p?.Player != null) Provider.kick(p.CSteamID, MsgPausa);
        }
    }
}
