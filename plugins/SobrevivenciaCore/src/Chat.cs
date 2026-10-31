using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Mensagens no chat com o ícone do servidor e as cores SnapLag.</summary>
    public static class Chat
    {
        static Sistemas S => Plugin.Instancia.S;

        public static string Cor(string cor, string texto) => $"<color={cor}>{texto}</color>";
        public static string Forte(string texto) => $"<b>{texto}</b>";

        /// <summary>Tira os sinais que quebram o texto colorido (nomes de jogadores, sintaxe de comando).</summary>
        public static string Limpar(string texto) => (texto ?? "").Replace('<', '[').Replace('>', ']');

        static string Prefixo => Cor(S.CorPrincipal, Forte("[" + S.Prefixo + "]")) + " ";

        public static void Para(UnturnedPlayer p, string texto)
        {
            var sp = p?.Player?.channel?.owner;
            if (sp == null) return;
            ChatManager.serverSendMessage(Prefixo + texto, Color.white, null, sp, EChatMode.SAY, S.IconeChat, true);
        }

        public static void Todos(string texto)
            => ChatManager.serverSendMessage(Prefixo + texto, Color.white, null, null, EChatMode.SAY, S.IconeChat, true);

        public static void Info(UnturnedPlayer p, string texto) => Para(p, Cor(S.CorTexto, texto));
        public static void Ok(UnturnedPlayer p, string texto) => Para(p, Cor(S.CorOk, texto));
        public static void Aviso(UnturnedPlayer p, string texto) => Para(p, Cor(S.CorDestaque, texto));
        public static void Erro(UnturnedPlayer p, string texto) => Para(p, Cor(S.CorPerigo, texto));

        public static string Tempo(long segundos)
        {
            if (segundos < 60) return segundos + "s";
            if (segundos < 3600) return (segundos / 60) + "min" + (segundos % 60 > 0 ? " " + (segundos % 60) + "s" : "");
            return (segundos / 3600) + "h " + (segundos % 3600 / 60) + "min";
        }
    }
}
