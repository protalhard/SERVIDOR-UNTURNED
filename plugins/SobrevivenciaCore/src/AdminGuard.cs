using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Aba ADMIN: modo espião (invisível + imortal + câmera livre) e a lista de Banidos / Suspeitos / Reports
    /// com DESBANIR, BANIR, IGNORAR e ESPIAR. Nomes dos elementos em NOMES_admin_guard_dz.md.</summary>
    public partial class Plugin
    {
        readonly HashSet<ulong> espioes = new HashSet<ulong>();
        const int LinhasLista = 8;

        class EstadoLista
        {
            public bool Aberta; public string Aba = "Banidos"; public int Pagina; public string Busca = ""; public ulong Sel; public int SelReport = -1;
        }
        readonly Dictionary<ulong, EstadoLista> listas = new Dictionary<ulong, EstadoLista>();
        EstadoLista ListaDe(UnturnedPlayer p) { if (!listas.TryGetValue(Id(p), out var l)) listas[Id(p)] = l = new EstadoLista(); return l; }

        // ---------- Modo espião ----------

        public bool EmModoEspiao(UnturnedPlayer p) => espioes.Contains(Id(p));

        public void ModoEspiao(UnturnedPlayer p, bool ligar)
        {
            if (ligar) espioes.Add(Id(p)); else espioes.Remove(Id(p));
            Invisivel(p, ligar);
            Visao(p, ligar);
            if (ligar) { imortais.Add(Id(p)); Curar(p); } else imortais.Remove(Id(p));
            LogAdmin(p, ligar ? "LIGOU modo espiao (invisivel + imortal + camera livre)" : "DESLIGOU modo espiao");
        }

        void Invisivel(UnturnedPlayer p, bool ligar)
        {
            try { p.VanishMode = ligar; } catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }

        void PreencherEspiao(UnturnedPlayer p)
        {
            bool pode = Pode(p, "Visao");
            V(p, "Adm_Espiao", pode);
            V(p, "Adm_Invisivel", pode);
            V(p, "Adm_Espiao_On", espioes.Contains(Id(p)));
            V(p, "Adm_Invisivel_On", p.VanishMode);
            V(p, "Adm_Banidos", Pode(p, "Ban"));
        }

        // ---------- Lista de Banidos / Suspeitos / Reports ----------

        class LinhaLista { public ulong Id; public string Nome, Info, Detalhe; public bool Banido; public int Report = -1; }

        List<LinhaLista> LinhasDaAba(EstadoLista l)
        {
            IEnumerable<LinhaLista> r;
            switch (l.Aba)
            {
                case "Suspeitos":
                    r = Suspeitos.Values.OrderByDescending(s => s.Pontos).Select(s => new LinhaLista
                    {
                        Id = s.Id, Nome = s.Nome,
                        Info = C(s.Pontos >= S.GuardPontosAlerta ? Vermelho : Ambar, s.Pontos + " pontos") + C(Cinza, "  " + (PlayerTool.getSteamPlayer(s.Id) != null ? "ONLINE" : "offline")),
                        Detalhe = C(Laranja, Chat.Limpar(s.Nome)) + C(Cinza, $"  {s.Id}  ({s.Pontos} pontos)") + "\n" +
                                  string.Join("\n", s.Motivos.AsEnumerable().Reverse().Take(8).Select(Chat.Limpar)),
                    });
                    break;
                case "Reports":
                    r = LerReports().Select((c, i) => new LinhaLista
                    {
                        Id = ulong.TryParse(c[1], out var a) ? a : 0, Nome = c[2], Report = i,
                        Info = C(Cinza, Data(c[0]) + "  por " + Chat.Limpar(c[4])),
                        Detalhe = C(Laranja, Chat.Limpar(c[2])) + C(Cinza, $"  {c[1]}") + $"\nReportado por {Chat.Limpar(c[4])} em {Data(c[0])}\n" +
                                  C(Ambar, "Motivo: ") + Chat.Limpar(c[5]) + "\n" + C(Cinza, "Foto da tela: no site /admin") + "\n" +
                                  Chat.Limpar(Cortar(c.Length > 7 ? c[7].Replace(" | ", "\n") : "", 900)),
                    });
                    break;
                default:
                    r = ListaBanidos().Select(b => new LinhaLista
                    {
                        Id = b.Id, Nome = b.Nome, Banido = true,
                        Info = C(Cinza, Cortar(Chat.Limpar(b.Motivo), 40) + "  " + (b.Permanente ? "permanente" : "falta " + Chat.Tempo(b.Resta))),
                        Detalhe = C(Laranja, Chat.Limpar(b.Nome)) + C(Cinza, $"  {b.Id}") + "\n" +
                                  C(Ambar, "Motivo: ") + Chat.Limpar(b.Motivo) + "\n" +
                                  C(Ambar, "Quem baniu: ") + Chat.Limpar(b.JuizNome) + C(Cinza, $"  em {Data(b.Banido_.ToString())}") + "\n" +
                                  C(Ambar, "Duracao: ") + (b.Permanente ? "permanente" : Chat.Tempo(b.Duracao) + " (falta " + Chat.Tempo(b.Resta) + ")") + "\n" +
                                  (b.Evidencia == "" ? C(Cinza, "Sem log registrado.") : C(Ambar, "Log: ") + Chat.Limpar(Cortar(b.Evidencia.Replace(" | ", "\n"), 900))),
                    });
                    break;
            }
            if (l.Busca != "")
                r = r.Where(x => (x.Nome ?? "").IndexOf(l.Busca, StringComparison.OrdinalIgnoreCase) >= 0 || x.Id.ToString() == l.Busca);
            return r.ToList();
        }

        static string Cortar(string s, int n) => s == null ? "" : s.Length <= n ? s : s.Substring(0, n) + "...";
        static string Data(string unix)
            => long.TryParse(unix, out var u) && u > 0 ? DateTimeOffset.FromUnixTimeSeconds(u).ToOffset(TimeSpan.FromHours(-3)).ToString("dd/MM HH:mm") : "-";

        List<string[]> LerReports()
        {
            try
            {
                if (!File.Exists(ArqReports)) return new List<string[]>();
                return File.ReadAllLines(ArqReports).Select(x => x.Split('\t')).Where(c => c.Length >= 7).Reverse().Take(100).ToList();
            }
            catch { return new List<string[]>(); }
        }

        void PreencherLista(UnturnedPlayer p)
        {
            var l = ListaDe(p);
            bool mostrar = l.Aberta && Pode(p, "Ban");
            V(p, "Adm_Lista_Area", mostrar);
            if (!mostrar) return;
            foreach (var aba in new[] { "Banidos", "Suspeitos", "Reports" }) V(p, $"Adm_Lista_Aba_{aba}_Ativa", l.Aba == aba);
            var linhas = LinhasDaAba(l);
            int paginas = Math.Max(1, (linhas.Count + LinhasLista - 1) / LinhasLista);
            l.Pagina = Mathf.Clamp(l.Pagina, 0, paginas - 1);
            for (int i = 0; i < LinhasLista; i++)
            {
                int idx = l.Pagina * LinhasLista + i;
                var n = "Adm_Lista_" + (i + 1);
                if (idx >= linhas.Count) { V(p, n, false); continue; }
                var x = linhas[idx];
                bool sel = l.Aba == "Reports" ? x.Report == l.SelReport : x.Id == l.Sel;
                V(p, n, true);
                T(p, n + "_Nome", sel ? C(Laranja, "> " + Chat.Limpar(x.Nome)) : Chat.Limpar(x.Nome));
                T(p, n + "_Info", x.Info);
            }
            T(p, "Adm_Lista_Pagina", $"{l.Pagina + 1}/{paginas}  ({linhas.Count})");
            var s = linhas.FirstOrDefault(x => l.Aba == "Reports" ? x.Report == l.SelReport : x.Id == l.Sel);
            T(p, "Adm_Lista_Detalhe", s?.Detalhe ?? C(Cinza, linhas.Count == 0 ? "Nada aqui." : "Clique em alguem da lista pra ver o motivo e o log."));
            V(p, "Adm_Lista_Desbanir", l.Aba == "Banidos" && s != null);
            V(p, "Adm_Lista_Banir", l.Aba != "Banidos" && s != null);
            V(p, "Adm_Lista_Ignorar", l.Aba == "Suspeitos" && s != null);
            V(p, "Adm_Lista_Espiar", l.Aba != "Banidos" && s != null && PlayerTool.getSteamPlayer(s.Id) != null && Pode(p, "Visao"));
        }

        void CliqueGuardAdmin(UnturnedPlayer p, EstadoAdmin e, string botao)
        {
            var l = ListaDe(p);
            switch (botao)
            {
                case "Adm_Espiao":
                    if (!Pode(p, "Visao")) { AvisoAdm(p, "Voce nao tem permissao pra isso.", false); return; }
                    bool ligar = !espioes.Contains(Id(p));
                    ModoEspiao(p, ligar);
                    PreencherEspiao(p);
                    V(p, "Adm_Visao_On", comVisao.Contains(Id(p)));
                    V(p, "Adm_Deus_On", imortais.Contains(Id(p)));
                    AvisoAdm(p, ligar ? "Modo espiao LIGADO: invisivel, imortal e camera livre." : "Modo espiao desligado.", true);
                    return;
                case "Adm_Invisivel":
                    if (!Pode(p, "Visao")) { AvisoAdm(p, "Voce nao tem permissao pra isso.", false); return; }
                    bool inv = !p.VanishMode;
                    Invisivel(p, inv);
                    LogAdmin(p, inv ? "LIGOU invisivel" : "DESLIGOU invisivel");
                    PreencherEspiao(p);
                    AvisoAdm(p, inv ? "Invisivel ligado: ninguem te ve." : "Invisivel desligado.", true);
                    return;
                case "Adm_Banidos":
                    if (!Pode(p, "Ban")) { AvisoAdm(p, "Voce nao tem permissao pra isso.", false); return; }
                    l.Aberta = !l.Aberta;
                    if (l.Aberta) { e.StaffAberto = false; e.PrecosAberto = false; V(p, "Adm_Staff_Area", false); V(p, "Adm_Precos_Area", false); }
                    PreencherLista(p);
                    return;
                case "Adm_Lista_Fechar": l.Aberta = false; PreencherLista(p); return;
            }
            if (!Pode(p, "Ban")) { AvisoAdm(p, "Voce nao tem permissao pra isso.", false); return; }
            if (botao.StartsWith("Adm_Lista_Aba_")) { l.Aba = botao.Substring(14); l.Pagina = 0; l.Sel = 0; l.SelReport = -1; PreencherLista(p); return; }
            if (int.TryParse(botao.Substring(10), out var n))
            {
                var linhas = LinhasDaAba(l);
                int idx = l.Pagina * LinhasLista + n - 1;
                if (n < 1 || idx >= linhas.Count) return;
                l.Sel = linhas[idx].Id; l.SelReport = linhas[idx].Report;
                PreencherLista(p);
                return;
            }
            var quem = Id(p);
            switch (botao)
            {
                case "Adm_Lista_Buscar": l.Pagina = 0; break;
                case "Adm_Lista_Anterior": l.Pagina--; break;
                case "Adm_Lista_Proximo": l.Pagina++; break;
                case "Adm_Lista_Desbanir":
                    if (l.Sel == 0) return;
                    AvisoAdm(p, Desbanir(quem, p.CharacterName, l.Sel) ? "Desbanido." : "Ele nao esta mais banido.", true);
                    l.Sel = 0;
                    break;
                case "Adm_Lista_Banir":
                    if (l.Sel == 0) return;
                    if (!double.TryParse(e.BanHoras.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var h)) h = 0;
                    BanirPorId(quem, p.CharacterName, l.Sel, h, e.Motivo == "" ? "Banido pela staff (DZ GUARD)." : e.Motivo);
                    AvisoAdm(p, NomeDe(l.Sel) + " foi banido.", true);
                    break;
                case "Adm_Lista_Ignorar":
                    if (l.Sel == 0) return;
                    IgnorarSuspeito(quem, p.CharacterName, l.Sel);
                    AvisoAdm(p, "Suspeita apagada.", true);
                    l.Sel = 0;
                    break;
                case "Adm_Lista_Espiar":
                    var alvo = PlayerTool.getSteamPlayer(l.Sel);
                    if (alvo?.player == null || !Pode(p, "Visao")) { AvisoAdm(p, "Ele nao esta online.", false); return; }
                    if (!espioes.Contains(quem)) ModoEspiao(p, true);
                    var t = alvo.player.transform;
                    FecharMenu(p);
                    p.Teleport(t.position - t.forward * 10f + Vector3.up * 3f, p.Rotation);
                    LogAdmin(p, $"ESPIANDO {alvo.playerID.characterName} ({l.Sel})");
                    Chat.Ok(p, "Voce esta invisivel atras dele. Use a camera livre do admin pra seguir. /espiao pra desligar.");
                    return;
            }
            PreencherLista(p);
        }
    }
}
