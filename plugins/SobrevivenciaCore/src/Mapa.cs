using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Marcar as áreas no mapa (M): o servidor não consegue pôr ícone fixo no mapa do jogo, então o /marcar
    /// (e os botões Marcar_* do menu) põe o marcador do jogador na área mais perto, com o nome dela.</summary>
    public partial class Plugin
    {
        public class AreaMapa { public string Tipo, Nome; public Vector3 Pos; }

        static readonly Dictionary<string, string> NomeArea = new Dictionary<string, string>
        { { "safe", "SAFE ZONE" }, { "negro", "MERCADO NEGRO" }, { "cacador", "ACAMPAMENTO DO CACADOR" }, { "atm", "ATM" } };

        /// <summary>Áreas montadas (montagens.tsv) + safes e ATMs da config.</summary>
        public List<AreaMapa> AreasDoMapa()
        {
            var r = new List<AreaMapa>();
            try
            {
                if (File.Exists(ArqMontagens))
                    foreach (var l in File.ReadAllLines(ArqMontagens))
                    {
                        var c = l.Split('\t');
                        if (c.Length < 4) continue;
                        var tipo = new string(c[1].TakeWhile(char.IsLetter).ToArray());
                        var n = c[3].Split(' ');
                        if (!NomeArea.ContainsKey(tipo) || n.Length < 3) continue;
                        if (float.TryParse(n[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) &&
                            float.TryParse(n[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y) &&
                            float.TryParse(n[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z))
                            r.Add(new AreaMapa { Tipo = tipo, Nome = NomeArea[tipo], Pos = new Vector3(x, y, z) });
                    }
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
            var cfg = Configuration.Instance;
            foreach (var z in cfg.ZonasSeguras ?? new List<Ponto>())
            {
                var pos = new Vector3(z.X, z.Y, z.Z);
                if (!r.Any(a => a.Tipo == "safe" && (a.Pos - pos).sqrMagnitude < 50f * 50f))
                    r.Add(new AreaMapa { Tipo = "safe", Nome = "SAFE ZONE", Pos = pos });
            }
            foreach (var a in cfg.Atms ?? new List<Ponto>())
                r.Add(new AreaMapa { Tipo = "atm", Nome = "ATM", Pos = new Vector3(a.X, a.Y, a.Z) });
            return r;
        }

        public static string TipoArea(string s)
        {
            s = (s ?? "").ToLowerInvariant().Replace("ç", "c");
            if (s.StartsWith("safe")) return "safe";
            if (s.StartsWith("negro") || s.StartsWith("mercado")) return "negro";
            if (s.StartsWith("cac") || s.StartsWith("acamp")) return "cacador";
            if (s.StartsWith("atm") || s.StartsWith("banco")) return "atm";
            return null;
        }

        /// <summary>Põe o marcador do jogador na área mais perto desse tipo. Devolve a mensagem.</summary>
        public string MarcarArea(UnturnedPlayer p, string tipo)
        {
            var a = AreasDoMapa().Where(x => x.Tipo == tipo).OrderBy(x => (x.Pos - p.Position).sqrMagnitude).FirstOrDefault();
            if (a == null) return $"Ainda nao tem {NomeArea[tipo]} no mapa.";
            p.Player.quests.replicateSetMarker(true, a.Pos, a.Nome);
            int m = Mathf.RoundToInt(Vector3.Distance(p.Position, a.Pos));
            return $"{a.Nome} marcada no mapa (aperte M). Fica a {(m >= 1000 ? (m / 1000f).ToString("0.0") + " km" : m + " m")} daqui.";
        }

        /// <summary>Botões Marcar_Safe / Marcar_Negro / Marcar_Cacador / Marcar_Atm (menu DZ) e Marcar_Limpar.</summary>
        bool CliqueMarcar(UnturnedPlayer p, string botao)
        {
            if (!botao.StartsWith("Marcar_")) return false;
            var t = botao.Substring(7);
            if (t == "Limpar") { p.Player.quests.replicateSetMarker(false, Vector3.zero, ""); Chat.Info(p, "Marcador apagado."); return true; }
            var tipo = TipoArea(t);
            if (tipo != null) Chat.Info(p, MarcarArea(p, tipo));
            return true;
        }
    }

    public class CmdMarcar : ComandoSistema
    {
        public override string Name => "marcar";
        public override string Help => "Marca no mapa (M) a safe, o mercado negro, o cacador ou o ATM mais perto.";
        public override string Syntax => "<safe | negro | cacador | atm | limpar>";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (a.Length >= 1 && a[0].ToLowerInvariant() == "limpar") { p.Player.quests.replicateSetMarker(false, Vector3.zero, ""); Chat.Info(p, "Marcador apagado."); return; }
            var tipo = a.Length >= 1 ? Plugin.TipoArea(a[0]) : null;
            if (tipo == null) { Chat.Info(p, "Use: /marcar safe | /marcar negro | /marcar cacador | /marcar atm | /marcar limpar"); return; }
            Chat.Info(p, P.MarcarArea(p, tipo));
        }
    }
}
