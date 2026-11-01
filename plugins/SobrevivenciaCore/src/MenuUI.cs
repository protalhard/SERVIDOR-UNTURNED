using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using Rocket.Unturned.Player;
using SDG.NetTransport;
using SDG.Unturned;
using UnityEngine;
using Action = System.Action;

namespace SobrevivenciaCore
{
    /// <summary>Menu com janela no jogo (mod do Workshop 3831267100, efeito 51231).
    /// Os nomes dos elementos estão em NOMES_menu_dz.md.</summary>
    public partial class Plugin
    {
        public const ushort MenuId = 51231;
        public const short MenuKey = 5123;
        const int KitsPorPagina = 24;

        static readonly string[] Abas = { "Inicio", "Kits", "Vips", "Vaults", "Homes", "Comandos", "Info", "Perfil", "Admin" };

        class EstadoMenu
        {
            public string Aba = "Inicio";
            public int PaginaKit;
            public int PaginaGuia = 1;
        }

        readonly Dictionary<ulong, EstadoMenu> menus = new Dictionary<ulong, EstadoMenu>();
        readonly ConcurrentDictionary<ulong, string> avatares = new ConcurrentDictionary<ulong, string>();
        readonly ConcurrentQueue<Action> naThreadPrincipal = new ConcurrentQueue<Action>();

        public static bool MenuDisponivel => Assets.find(EAssetType.EFFECT, MenuId) is EffectAsset;

        static ITransportConnection Con(UnturnedPlayer p) => p.Player.channel.owner.transportConnection;
        static void V(UnturnedPlayer p, string nome, bool ligado) => EffectManager.sendUIEffectVisibility(MenuKey, Con(p), true, nome, ligado);
        static void T(UnturnedPlayer p, string nome, string texto) => EffectManager.sendUIEffectText(MenuKey, Con(p), true, nome, texto ?? "");
        static void Img(UnturnedPlayer p, string nome, string url) => EffectManager.sendUIEffectImageURL(MenuKey, Con(p), true, nome, url, true, false);

        const string Verde = "#3DDC84", Ambar = "#FFB020", Vermelho = "#FF6060", Laranja = "#FF5A00", Cinza = "#9C9CA6";
        static string C(string cor, string t) => $"<color={cor}>{t}</color>";

        public bool MenuAberto(UnturnedPlayer p) => menus.ContainsKey(Id(p));

        void LigarMenu()
        {
            EffectManager.onEffectButtonClicked += AoClicarBotao;
            EffectManager.onEffectTextCommitted += AoDigitar;
        }

        // Os mods do Workshop carregam depois do plugin: confere o efeito só quando o mapa já abriu.
        bool menuConferido;
        void ConferirMenu()
        {
            if (menuConferido || !Level.isLoaded) return;
            menuConferido = true;
            ValidarItens();
            CompletarLojasDepois(); // itens do Workshop (California 2) só existem depois do mapa abrir
            foreach (var sp in Provider.clients)
                if (sp?.player != null) MostrarHud(UnturnedPlayer.FromSteamPlayer(sp));
            if (MenuDisponivel) Rocket.Core.Logging.Logger.Log("SobrevivenciaCore: menu com janela (efeito 51231) pronto.");
            else Rocket.Core.Logging.Logger.LogWarning("SobrevivenciaCore: efeito do menu (51231) nao carregou. Falta o mod 3831267100 no WorkshopDownloadConfig? /menu fica so no chat.");
        }

        void DesligarMenu()
        {
            EffectManager.onEffectButtonClicked -= AoClicarBotao;
            EffectManager.onEffectTextCommitted -= AoDigitar;
            foreach (var id in menus.Keys.ToList())
            {
                var sp = PlayerTool.getSteamPlayer(id);
                if (sp?.player != null) FecharMenu(UnturnedPlayer.FromSteamPlayer(sp));
            }
            menus.Clear();
        }

        // ---------- Abrir e fechar ----------

        /// <summary>Abre o menu (ou fecha, se já estiver aberto). Devolve false se o mod não está no servidor.</summary>
        public bool AbrirMenu(UnturnedPlayer p, string aba = "Inicio")
        {
            if (!MenuDisponivel || p?.Player == null) return false;
            if (MenuAberto(p)) { FecharMenu(p); return true; }
            FecharLoja(p);
            var e = new EstadoMenu();
            menus[Id(p)] = e;
            EffectManager.SendUIEffect((EffectAsset)Assets.find(EAssetType.EFFECT, MenuId), MenuKey, Con(p), true);
            p.Player.enablePluginWidgetFlag(EPluginWidgetFlags.Modal);
            V(p, "Aba_Admin", EhStaff(p));
            T(p, "Menu_Aviso", "");
            MostrarAba(p, e, aba);
            return true;
        }

        /// <summary>Guia de boas-vindas em 4 páginas (jogador novo, ou botão Guia).</summary>
        public bool AbrirGuia(UnturnedPlayer p)
        {
            if (!MenuDisponivel || p?.Player == null) return false;
            if (!MenuAberto(p))
            {
                menus[Id(p)] = new EstadoMenu();
                EffectManager.SendUIEffect((EffectAsset)Assets.find(EAssetType.EFFECT, MenuId), MenuKey, Con(p), true);
                p.Player.enablePluginWidgetFlag(EPluginWidgetFlags.Modal);
                V(p, "Aba_Admin", EhStaff(p));
            }
            V(p, "DZ_Menu", false);
            V(p, "DZ_Guia", true);
            MostrarGuia(p, menus[Id(p)], 1);
            return true;
        }

        public void FecharMenu(UnturnedPlayer p)
        {
            if (p?.Player == null) return;
            menus.Remove(Id(p));
            AoFecharMenuAdmin(p);
            EffectManager.askEffectClearByID(MenuId, Con(p));
            p.Player.disablePluginWidgetFlag(EPluginWidgetFlags.Modal);
        }

        void Aviso(UnturnedPlayer p, string msg, bool ok) => T(p, "Menu_Aviso", C(ok ? Verde : Vermelho, msg));

        void MostrarGuia(UnturnedPlayer p, EstadoMenu e, int pag)
        {
            pag = Mathf.Clamp(pag, 1, 4);
            V(p, "Guia_Pag" + e.PaginaGuia, false);
            V(p, "Guia_Pag" + pag, true);
            e.PaginaGuia = pag;
        }

        void MostrarAba(UnturnedPlayer p, EstadoMenu e, string aba)
        {
            if (aba == "Admin" && !EhStaff(p)) aba = "Inicio";
            if (e.Aba != aba)
            {
                V(p, "Pag_" + e.Aba, false);
                V(p, "Aba_" + e.Aba + "_Sel", false);
            }
            e.Aba = aba;
            V(p, "Pag_" + aba, true);
            V(p, "Aba_" + aba + "_Sel", true);
            switch (aba)
            {
                case "Inicio": PreencherInicio(p); break;
                case "Kits": PreencherKits(p, e); break;
                case "Vips": PreencherVips(p); break;
                case "Vaults": PreencherVaults(p); break;
                case "Homes": PreencherHomes(p); break;
                case "Perfil": PreencherPerfil(p); break;
                case "Admin": PreencherAdmin(p); break;
            }
        }

        // ---------- Páginas ----------

        void PreencherInicio(UnturnedPlayer p)
        {
            T(p, "Inicio_Online", C(Verde, "● ") + $"{Provider.clients.Count}/{Provider.maxPlayers} ONLINE");
        }

        string StatusKit(UnturnedPlayer p, Ficha f, Kit k)
        {
            if (Pode(p, "Kits")) return C(Verde, "PEGAR (ADM)");
            if (!TemPermissao(p, k.Permissao)) return C(Vermelho, "VIP");
            if (f.Abates < k.Abates) return C(Vermelho, $"{f.Abates}/{k.Abates} ABATES");
            var resta = Stats.UltimoUsoKit(f.Id, k.Nome) + k.Espera - Estatisticas.Agora;
            if (resta > 0) return C(Ambar, TimeSpan.FromSeconds(resta).ToString(resta >= 86400 ? @"d\d\ hh\:mm" : @"hh\:mm\:ss"));
            return C(Verde, "PEGAR");
        }

        int PaginasKit => Math.Max(1, (S.Kits.Count + KitsPorPagina - 1) / KitsPorPagina);

        void PreencherKits(UnturnedPlayer p, EstadoMenu e)
        {
            var f = FichaDe(p);
            e.PaginaKit = Mathf.Clamp(e.PaginaKit, 0, PaginasKit - 1);
            for (int i = 0; i < KitsPorPagina; i++)
            {
                int idx = e.PaginaKit * KitsPorPagina + i;
                string n = "Kit_" + (i + 1);
                if (idx >= S.Kits.Count) { V(p, n, false); continue; }
                var k = S.Kits[idx];
                V(p, n, true);
                T(p, n + "_Nome", Chat.Limpar(k.Nome).ToUpperInvariant());
                T(p, n + "_Status", StatusKit(p, f, k));
            }
            T(p, "Kits_Pagina", $"{e.PaginaKit + 1}/{PaginasKit}");
            V(p, "Kits_Anterior", PaginasKit > 1);
            V(p, "Kits_Proximo", PaginasKit > 1);
            T(p, "Kits_Legenda", $"Voce tem {C(Laranja, f.Abates.ToString())} abates");
        }

        /// <summary>Atualiza só o status dos kits em espera (contagem regressiva ao vivo).</summary>
        void AtualizarContagemKits(UnturnedPlayer p, EstadoMenu e)
        {
            var f = FichaDe(p);
            for (int i = 0; i < KitsPorPagina; i++)
            {
                int idx = e.PaginaKit * KitsPorPagina + i;
                if (idx >= S.Kits.Count) break;
                var k = S.Kits[idx];
                var resta = Stats.UltimoUsoKit(f.Id, k.Nome) + k.Espera - Estatisticas.Agora;
                if (resta >= 0 && resta <= k.Espera && !Pode(p, "Kits") && TemPermissao(p, k.Permissao) && f.Abates >= k.Abates)
                    T(p, "Kit_" + (i + 1) + "_Status", StatusKit(p, f, k));
            }
        }

        // Aba VIPs = loja de XP (troca XP por pacote). Os 7 primeiros pacotes; o resto fica no /lojaxp.
        void PreencherVips(UnturnedPlayer p)
        {
            var f = FichaDe(p);
            T(p, "Vips_Titulo", "LOJA DE XP");
            T(p, "Vips_Saldo", "Seu saldo: " + C(Ambar, f.XpSaldo.ToString("N0") + " XP") + (S.LojaXp.Count > 7 ? C(Cinza, "   (todos: /lojaxp)") : ""));
            for (int i = 0; i < 7; i++)
            {
                string n = "Vip_" + (i + 1);
                if (i >= S.LojaXp.Count) { V(p, n, false); continue; }
                var pac = S.LojaXp[i];
                V(p, n, true);
                T(p, n + "_Nome", Chat.Limpar(pac.Nome).ToUpperInvariant());
                T(p, n + "_Desc", Chat.Limpar(pac.Descricao));
                T(p, n + "_Preco", C(f.XpSaldo >= pac.Preco ? Verde : Vermelho, pac.Preco.ToString("N0") + " XP"));
                T(p, n + "_Comprar_Texto", "COMPRAR");
            }
        }


        static string ListaItens(List<ItemKit> itens)
            => string.Join(", ", (itens ?? new List<ItemKit>()).Where(i => Itens.Existe(i.Id))
                .Select(i => (i.Quantidade > 1 ? i.Quantidade + "x " : "") + Chat.Limpar(Itens.Nome(i.Id))));

        void PreencherVaults(UnturnedPlayer p)
        {
            for (int i = 0; i < 4 && i < S.Vaults.Count; i++)
            {
                var v = S.Vaults[i];
                string n = "Vault_" + (i + 1);
                bool liberado = TemVault(p, v);
                T(p, n + "_Nome", Chat.Limpar(v.Nome).ToUpperInvariant());
                T(p, n + "_Tamanho", $"{v.Largura} x {v.Altura}");
                T(p, n + "_Status", liberado ? C(Verde, "LIBERADO") : C(Vermelho, "BLOQUEADO"));
                T(p, n + "_Preco", liberado ? (string.IsNullOrEmpty(v.Permissao) && v.Preco == 0 ? "GRATIS" : "SEU") : v.Preco > 0 ? "$ " + v.Preco.ToString("N0") : "SO VIP");
                T(p, n + "_Abrir_Texto", liberado ? "ABRIR" : v.Preco > 0 ? "COMPRAR" : "VER VIP");
            }
            T(p, "Vaults_Dica", S.VaultSoNaSafe ? "O cofre abre dentro da safe zone. O que fica nele nao se perde quando voce morre." : "O que fica no cofre nao se perde quando voce morre.");
        }

        void PreencherHomes(UnturnedPlayer p)
        {
            var minhas = Homes.De(Id(p));
            int max = TemPermissao(p, "home.vip") ? S.HomesVip : S.HomesMax;
            for (int i = 0; i < 6; i++)
            {
                string n = "Home_" + (i + 1);
                if (i >= minhas.Count) { V(p, n, false); continue; }
                var c = minhas[i];
                V(p, n, true);
                T(p, n + "_Nome", Chat.Limpar(c.Nome).ToUpperInvariant());
                T(p, n + "_Info", "a " + Mathf.RoundToInt(Vector3.Distance(p.Position, c.Pos)) + " m");
            }
            V(p, "Homes_Vazio", minhas.Count <= 2);
            T(p, "Homes_Contador", $"{minhas.Count} / {max} homes");
            if (S.WarpSafeAposMorrer > 0)
            {
                float desde = SegundosDesdeRenascer(p);
                int resta = desde < 0 ? 0 : Mathf.Max(0, S.WarpSafeAposMorrer - Mathf.FloorToInt(desde));
                T(p, "Homes_Safe_Texto", $"So funciona ate {S.WarpSafeAposMorrer}s depois de morrer. Fique parado na contagem: se mexer ou levar dano, cancela."
                    + (resta > 0 ? $" Liberado agora: {resta}s." : " Agora: bloqueado."));
            }
        }

        void PreencherPerfil(UnturnedPlayer p)
        {
            var f = FichaDe(p);
            var pat = PatenteDe(f.Xp);
            T(p, "Perfil_Nome", Chat.Limpar(p.CharacterName));
            T(p, "Perfil_Rank", C(pat.Cor, pat.Nome.ToUpperInvariant()));
            T(p, "Perfil_Nivel", "NIVEL " + f.Nivel);
            T(p, "Perfil_SteamId", Id(p).ToString());
            T(p, "Perfil_Proximo", "Proximo nivel em " + C(Ambar, (Ficha.XpDoNivel(f.Nivel + 1) - f.Xp).ToString("N0") + " XP"));
            T(p, "Perfil_Abates", f.Abates.ToString("N0"));
            T(p, "Perfil_Mortes", f.Mortes.ToString("N0"));
            T(p, "Perfil_Xp", f.Xp.ToString("N0"));
            T(p, "Perfil_Hs", f.PorcentoCabeca + "%");
            T(p, "Perfil_Kdr", f.Kd.ToString("0.00"));
            T(p, "Perfil_Zumbis", f.Zumbis.ToString("N0"));
            T(p, "Perfil_Dinheiro", "$ " + Banco.Saldo(Id(p)).ToString("N0"));
            T(p, "Perfil_Tempo", Chat.Tempo(f.Segundos));
            if (avatares.TryGetValue(Id(p), out var url) && !string.IsNullOrEmpty(url)) Img(p, "Perfil_Avatar", url);
        }

        /// <summary>Busca a foto da Steam numa thread separada e guarda o link.</summary>
        void BuscarAvatar(ulong id)
        {
            if (avatares.ContainsKey(id)) return;
            avatares[id] = "";
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    using (var w = new WebClient())
                    {
                        var xml = w.DownloadString($"https://steamcommunity.com/profiles/{id}/?xml=1");
                        var m = Regex.Match(xml, @"<avatarFull><!\[CDATA\[(.*?)\]\]>");
                        if (m.Success) avatares[id] = m.Groups[1].Value.Trim();
                    }
                }
                catch { }
            });
        }

        // ---------- Botões ----------

        void Navegador(UnturnedPlayer p, string titulo, string link)
        {
            if (string.IsNullOrEmpty(link)) { Aviso(p, "Link ainda nao divulgado.", false); return; }
            p.Player.sendBrowserRequest(titulo, link);
        }

        void AbrirDiscord(UnturnedPlayer p) => Navegador(p, "Discord do DAY Z BRASIL OFICIAL", S.Discord);
        void AbrirSite(UnturnedPlayer p, string caminho = "") => Navegador(p, "Site do DAY Z BRASIL OFICIAL", S.LinkSiteServidor.TrimEnd('/') + caminho);

        void AoClicarBotao(Player pl, string botao)
        {
            try
            {
                var p = UnturnedPlayer.FromPlayer(pl);
                if (p == null) return;
                if (CliqueGaragem(p, botao) || CliqueAtm(p, botao) || CliqueMarcar(p, botao)) return;
                if (CliqueSpawn(p, botao)) return;
                if (CliqueLoja(p, botao)) return;
                if (!menus.TryGetValue(Id(p), out var e)) return;
                if (botao.StartsWith("Adm_") || botao.StartsWith("Preco_")) { CliqueAdmin(p, e, botao); return; }
                if (botao.StartsWith("Aba_")) { MostrarAba(p, e, botao.Substring(4)); return; }
                if (botao.StartsWith("Kit_") && int.TryParse(botao.Substring(4), out var nk)) { CliqueKit(p, e, nk); return; }
                if (botao.StartsWith("Vip_")) { CliqueVip(p, botao); return; }
                if (botao.StartsWith("Vault_")) { CliqueVault(p, botao); return; }
                if (botao.StartsWith("Home_")) { CliqueHome(p, botao); return; }
                if (botao.StartsWith("Guia")) { CliqueGuia(p, e, botao); return; }
                switch (botao)
                {
                    case "Menu_Fechar": FecharMenu(p); break;
                    case "Kits_Anterior": e.PaginaKit = (e.PaginaKit + PaginasKit - 1) % PaginasKit; PreencherKits(p, e); break;
                    case "Kits_Proximo": e.PaginaKit = (e.PaginaKit + 1) % PaginasKit; PreencherKits(p, e); break;
                    case "Inicio_Discord": case "Info_Discord": case "Perfil_Discord": AbrirDiscord(p); break;
                    case "Inicio_Site": case "Info_Site": AbrirSite(p); break;
                    case "Info_Ranking": case "Perfil_Ranking": AbrirSite(p, "/ranking"); break;
                    case "Inicio_Loja": MostrarAba(p, e, "Vips"); break;
                    case "Inicio_Guia": case "Info_Guia":
                        V(p, "DZ_Menu", false); V(p, "DZ_Guia", true); MostrarGuia(p, e, 1); break;
                    case "Homes_Safe_Ir": FecharMenu(p); CmdSafe.Warp(this, S, p, null); break;
                }
            }
            catch (Exception ex) { Rocket.Core.Logging.Logger.LogException(ex); }
        }

        void CliqueKit(UnturnedPlayer p, EstadoMenu e, int n)
        {
            int idx = e.PaginaKit * KitsPorPagina + n - 1;
            if (n < 1 || n > KitsPorPagina || idx >= S.Kits.Count) return;
            var k = S.Kits[idx];
            bool ok = CmdKit.Pegar(this, p, k, out var msg);
            Aviso(p, msg, ok);
            if (ok) Chat.Ok(p, msg);
            T(p, "Kit_" + n + "_Status", StatusKit(p, FichaDe(p), k));
        }

        void CliqueVip(UnturnedPlayer p, string botao)
        {
            var partes = botao.Split('_'); // Vip_N_Ver / Vip_N_Comprar
            if (partes.Length < 3 || !int.TryParse(partes[1], out var n) || n < 1 || n > S.LojaXp.Count) return;
            var pac = S.LojaXp[n - 1];
            if (partes[2] == "Ver")
            {
                var itens = ListaItens(pac.Itens);
                Chat.Para(p, Chat.Cor(S.CorPrincipal, Chat.Forte(pac.Nome)) + Chat.Cor(S.CorTexto, ": " + itens));
                Aviso(p, Chat.Limpar(pac.Nome) + ": " + itens, true);
            }
            else if (partes[2] == "Comprar")
            {
                bool ok = CmdLojaXp.Comprar(this, p, n, out var msg);
                Aviso(p, msg, ok);
                if (ok) { Chat.Ok(p, msg); PreencherVips(p); }
            }
        }

        void CliqueVault(UnturnedPlayer p, string botao)
        {
            var partes = botao.Split('_'); // Vault_N_Abrir
            if (partes.Length < 3 || partes[2] != "Abrir" || !int.TryParse(partes[1], out var n) || n < 1 || n > S.Vaults.Count) return;
            var tam = S.Vaults[n - 1];
            if (!TemVault(p, tam))
            {
                if (tam.Preco <= 0) { Aviso(p, "Esse cofre e so VIP. Os pacotes VIP abrem em breve, fique de olho no Discord.", false); return; }
                bool ok = ComprarVault(p, tam, out var msg);
                Aviso(p, msg, ok);
                if (ok) { Chat.Ok(p, msg); PreencherVaults(p); }
                return;
            }
            FecharMenu(p);
            CmdVault.Abrir(this, S, p, tam);
        }

        void CliqueHome(UnturnedPlayer p, string botao)
        {
            var partes = botao.Split('_'); // Home_N_Ir / Home_N_Apagar
            if (partes.Length < 3 || !int.TryParse(partes[1], out var n)) return;
            var minhas = Homes.De(Id(p));
            if (n < 1 || n > minhas.Count) return;
            var casa = minhas[n - 1];
            if (partes[2] == "Ir") { FecharMenu(p); CmdHome.IrPara(this, S, p, casa); }
            else if (partes[2] == "Apagar")
            {
                minhas.Remove(casa);
                Homes.Salvar();
                Aviso(p, "Home " + Chat.Limpar(casa.Nome) + " apagada.", true);
                PreencherHomes(p);
            }
        }

        void CliqueGuia(UnturnedPlayer p, EstadoMenu e, string botao)
        {
            if (botao.EndsWith("_Proximo")) MostrarGuia(p, e, e.PaginaGuia + 1);
            else if (botao.EndsWith("_Anterior")) MostrarGuia(p, e, e.PaginaGuia - 1);
            else if (botao == "Guia_Pular") FecharMenu(p);
            else if (botao == "Guia4_Discord") AbrirDiscord(p);
            else if (botao == "Guia4_Safe") { FecharMenu(p); CmdSafe.Warp(this, S, p, null); }
            else if (botao == "Guia4_Comecar")
            {
                V(p, "DZ_Guia", false);
                V(p, "DZ_Menu", true);
                MostrarAba(p, e, "Inicio");
            }
        }

        void AoDigitar(Player pl, string campo, string texto)
        {
            try
            {
                var p = UnturnedPlayer.FromPlayer(pl);
                if (p == null || DigitouAtm(p, campo, texto) || DigitouLoja(p, campo, texto) || !menus.ContainsKey(Id(p))) return;
                if (campo.StartsWith("Adm_") || campo.StartsWith("Preco_")) DigitouAdmin(p, campo, texto);
            }
            catch (Exception ex) { Rocket.Core.Logging.Logger.LogException(ex); }
        }

        // ---------- Tick (1x por segundo) ----------

        void TickMenus()
        {
            ConferirMenu();
            while (naThreadPrincipal.TryDequeue(out var a)) a();
            foreach (var kv in menus.ToList())
            {
                var sp = PlayerTool.getSteamPlayer(kv.Key);
                if (sp?.player == null) { menus.Remove(kv.Key); continue; }
                if (kv.Value.Aba == "Kits") AtualizarContagemKits(UnturnedPlayer.FromSteamPlayer(sp), kv.Value);
            }
            TickAdmin();
            TickHud();
        }
    }
}
