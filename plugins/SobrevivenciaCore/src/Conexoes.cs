using System;
using System.IO;
using System.Linq;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;

namespace SobrevivenciaCore
{
    /// <summary>Log de jogadores pro painel do site: quem tentou entrar, quem entrou e saiu, e quem foi recusado (e por quê).
    /// Arquivo jogadores_log.tsv: data (UTC, ISO) \t steam \t nome \t evento \t detalhe. Guarda as últimas 5000 linhas.</summary>
    public partial class Plugin
    {
        string ArqJogadoresLog => Path.Combine(Directory, "jogadores_log.tsv");
        readonly object travaJogLog = new object();
        int linhasDesdeCorte;

        void LigarLogJogadores()
        {
            Provider.onRejectingPlayer += AoRecusar;
            Provider.onCheckValidWithExplanation += AoValidar;
        }

        void DesligarLogJogadores()
        {
            Provider.onRejectingPlayer -= AoRecusar;
            Provider.onCheckValidWithExplanation -= AoValidar;
        }

        static string NomePendente(CSteamID id)
        {
            try
            {
                var p = Provider.pending.FirstOrDefault(x => x.playerID.steamID == id);
                return p?.playerID.playerName ?? "";
            }
            catch { return ""; }
        }

        void AoValidar(ValidateAuthTicketResponse_t c, ref bool valido, ref string explicacao)
        {
            if (c.m_eAuthSessionResponse != EAuthSessionResponse.k_EAuthSessionResponseOK)
                LogJogador(c.m_SteamID, NomePendente(c.m_SteamID), "recusado", "Falha na autenticacao da Steam: " + c.m_eAuthSessionResponse);
            else
                LogJogador(c.m_SteamID, NomePendente(c.m_SteamID), "tentando", "Steam confirmou a conta, entrando...");
        }

        void AoRecusar(CSteamID id, ESteamRejection motivo, string explicacao)
        {
            LogJogador(id, NomePendente(id), "recusado", MotivoRecusa(motivo) + (string.IsNullOrWhiteSpace(explicacao) ? "" : " (" + explicacao.Trim() + ")"));
        }

        static string MotivoRecusa(ESteamRejection r)
        {
            switch (r)
            {
                case ESteamRejection.SERVER_FULL: return "Servidor cheio";
                case ESteamRejection.WRONG_PASSWORD: return "Senha errada";
                case ESteamRejection.WRONG_VERSION: return "Versao do jogo diferente da do servidor";
                case ESteamRejection.WRONG_HASH_LEVEL: case ESteamRejection.WRONG_HASH_MASTER_BUNDLE: case ESteamRejection.WRONG_HASH_RESOURCES: case ESteamRejection.WRONG_LEVEL_VERSION: case ESteamRejection.MOD_VERSION_MISMATCH: case ESteamRejection.MOD_NAME_MISMATCH: return "Arquivos do mapa ou dos mods diferentes (atualizar o Workshop)";
                case ESteamRejection.AUTH_VAC_BAN: return "Conta com VAC ban";
                case ESteamRejection.AUTH_PUB_BAN: return "Banido do servidor";
                case ESteamRejection.WHITELISTED: return "Fora da whitelist";
                case ESteamRejection.PING: return "Ping alto demais";
                case ESteamRejection.NAME_PLAYER_SHORT: case ESteamRejection.NAME_PLAYER_LONG: case ESteamRejection.NAME_PLAYER_INVALID:
                case ESteamRejection.NAME_CHARACTER_SHORT: case ESteamRejection.NAME_CHARACTER_LONG: case ESteamRejection.NAME_CHARACTER_INVALID:
                    return "Nome invalido (curto, longo ou com caracteres proibidos)";
                case ESteamRejection.AUTH_TIMED_OUT: return "Demorou demais pra autenticar (internet ou Steam)";
                case ESteamRejection.PLUGIN: return "Barrado por plugin";
                default: return r.ToString();
            }
        }

        public void LogJogador(CSteamID id, string nome, string evento, string detalhe)
        {
            try
            {
                var linha = $"{DateTime.UtcNow:o}\t{id.m_SteamID}\t{Chat.Limpar(nome ?? "").Replace('\t', ' ')}\t{evento}\t{(detalhe ?? "").Replace('\t', ' ').Replace('\n', ' ')}";
                lock (travaJogLog)
                {
                    File.AppendAllText(ArqJogadoresLog, linha + Environment.NewLine);
                    if (++linhasDesdeCorte >= 500)
                    {
                        linhasDesdeCorte = 0;
                        var l = File.ReadAllLines(ArqJogadoresLog);
                        if (l.Length > 60000) File.WriteAllLines(ArqJogadoresLog, l.Skip(l.Length - 60000));
                    }
                }
            }
            catch { }
        }

        void LogEntrou(UnturnedPlayer p) => LogJogador(p.CSteamID, p.SteamName, "entrou", "Personagem: " + Chat.Limpar(p.CharacterName) + (EmPausa && !EhStaff(p) ? " (em manutencao: vai ser desconectado)" : ""));
        void LogSaiu(UnturnedPlayer p) => LogJogador(p.CSteamID, p.SteamName, "saiu", "");
    }
}
