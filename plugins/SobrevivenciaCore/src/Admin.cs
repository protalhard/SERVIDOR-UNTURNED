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
    /// <summary>Aba ADMIN do menu. Só aparece pra dono e staff; cada ação confere a permissão no servidor.
    /// Nomes dos elementos em NOMES_admin_dz.md. Staff fica em staff.tsv; ações vão pro admin_log.txt (o bot manda pro #logs).</summary>
    public partial class Plugin
    {
        public const ushort MarcadorId = 51232;
        const int JogadoresPorPagina = 10;

        /// <summary>Permissões que o dono marca pra cada staff.</summary>
        public static readonly string[] PermissoesAdmin =
            { "Kick", "Ban", "Tp", "Trazer", "Curar", "Matar", "Congelar", "Spy", "Itens", "Visao", "Deus", "Mundo", "Airdrop", "Reiniciar", "Precos" };

        // botão -> permissão
        static readonly Dictionary<string, string> BotaoPermissao = new Dictionary<string, string>
        {
            { "Adm_Kick", "Kick" }, { "Adm_Ban", "Ban" }, { "Adm_IrAte", "Tp" }, { "Adm_Trazer", "Trazer" },
            { "Adm_Curar", "Curar" }, { "Adm_Matar", "Matar" }, { "Adm_Congelar", "Congelar" }, { "Adm_Spy", "Spy" },
            { "Adm_Reiniciar", "Reiniciar" }, { "Adm_Visao", "Visao" }, { "Adm_Deus", "Deus" }, { "Adm_Precos", "Precos" },
        };

        class EstadoAdmin
        {
            public int Pagina;
            public string Busca = "";
            public ulong Alvo;
            public string Motivo = "", BanHoras = "", ItemId = "", ItemQtd = "1", Anuncio = "";
            public int KitDrop;
            public bool StaffAberto;
            public bool PrecosAberto;
            public int PrecoLoja, PrecoPagina;
            public string PrecoBusca = "", PrecoCompra = "", PrecoVenda = "", PrecoPorc = "";
            public ItemLoja PrecoSel;
            public float ProximaLista;
        }

        class MembroStaff
        {
            public string Nome;
            public HashSet<string> Perms = new HashSet<string>();
        }

        class AirdropPendente
        {
            public Vector3 Pos;
            public KitAirdrop Kit;
            public float Fim;
            public HashSet<uint> JaExistiam;
        }

        readonly Dictionary<ulong, EstadoAdmin> admins = new Dictionary<ulong, EstadoAdmin>();
        readonly Dictionary<ulong, MembroStaff> staff = new Dictionary<ulong, MembroStaff>();
        readonly HashSet<ulong> comVisao = new HashSet<ulong>();
        readonly HashSet<ulong> imortais = new HashSet<ulong>();
        readonly HashSet<ulong> congelados = new HashSet<ulong>();
        readonly List<AirdropPendente> airdrops = new List<AirdropPendente>();
        float reinicioEm = -1; int ultimoAvisoReinicio = -1;
        string arqStaff, arqLogAdmin;

        // ---------- Staff ----------

        void CarregarStaff()
        {
            arqStaff = Path.Combine(Directory, "staff.tsv");
            arqLogAdmin = Path.Combine(Directory, "admin_log.txt");
            staff.Clear();
            if (!File.Exists(arqStaff)) return;
            foreach (var l in File.ReadAllLines(arqStaff))
            {
                var c = l.Split('\t');
                if (c.Length < 3 || !ulong.TryParse(c[0], out var id)) continue;
                staff[id] = new MembroStaff
                {
                    Nome = c[1],
                    Perms = new HashSet<string>(c[2].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Where(x => PermissoesAdmin.Contains(x))),
                };
            }
        }

        void SalvarStaff()
        {
            var tmp = arqStaff + ".tmp";
            File.WriteAllLines(tmp, staff.Select(kv => kv.Key + "\t" + kv.Value.Nome.Replace('\t', ' ') + "\t" + string.Join(",", kv.Value.Perms)));
            if (File.Exists(arqStaff)) File.Delete(arqStaff);
            File.Move(tmp, arqStaff);
        }

        public bool EhDono(ulong id) => (S.Donos ?? "").Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries).Contains(id.ToString());
        public bool EhDono(UnturnedPlayer p) => p != null && EhDono(Id(p));
        public bool EhStaff(UnturnedPlayer p) => p != null && (EhDono(p) || staff.ContainsKey(Id(p)));
        public bool Pode(UnturnedPlayer p, string perm)
            => EhDono(p) || (p != null && staff.TryGetValue(Id(p), out var m) && m.Perms.Contains(perm));

        void LogAdmin(UnturnedPlayer adm, string acao)
        {
            var linha = $"{DateTime.Now:dd/MM HH:mm:ss} {Chat.Limpar(adm?.CharacterName ?? "console")} ({(adm != null ? Id(adm) : 0)}): {acao}";
            Rocket.Core.Logging.Logger.Log("[ADMIN] " + linha);
            try { File.AppendAllText(arqLogAdmin, linha + Environment.NewLine); } catch { }
        }

        EstadoAdmin EstadoAdm(UnturnedPlayer p)
        {
            if (!admins.TryGetValue(Id(p), out var e)) admins[Id(p)] = e = new EstadoAdmin();
            return e;
        }

        void AvisoAdm(UnturnedPlayer p, string msg, bool ok) => T(p, "Adm_Aviso", C(ok ? Verde : Vermelho, msg));

        // ---------- Preencher a aba ----------

        void PreencherAdmin(UnturnedPlayer p)
        {
            var e = EstadoAdm(p);
            foreach (var kv in BotaoPermissao) V(p, kv.Key, Pode(p, kv.Value));
            V(p, "Adm_Staff", EhDono(p));
            V(p, "Adm_Itens_Area", Pode(p, "Itens"));
            V(p, "Adm_Mundo_Area", Pode(p, "Mundo") || Pode(p, "Curar"));
            V(p, "Adm_Drop_Area", Pode(p, "Airdrop"));
            V(p, "Adm_Visao_On", comVisao.Contains(Id(p)));
            V(p, "Adm_Deus_On", imortais.Contains(Id(p)));
            V(p, "Adm_Confirmar", false);
            T(p, "Adm_Drop_Kit_Texto", KitDropAtual(e)?.Nome?.ToUpperInvariant() ?? "-");
            T(p, "Adm_Drop_Dica", "Marcador = o ponto que voce marcou no mapa (abra o mapa e clique com o botao direito).");
            T(p, "Adm_Aviso", "");
            PreencherListaAdmin(p, e);
            PreencherAlvo(p, e);
            V(p, "Adm_Staff_Area", e.StaffAberto && EhDono(p));
            if (e.StaffAberto && EhDono(p)) PreencherStaff(p, e);
            V(p, "Adm_Precos_Area", e.PrecosAberto && Pode(p, "Precos"));
            if (e.PrecosAberto && Pode(p, "Precos")) PreencherPrecos(p, e);
        }

        KitAirdrop KitDropAtual(EstadoAdmin e)
            => S.AirdropKits.Count == 0 ? null : S.AirdropKits[((e.KitDrop % S.AirdropKits.Count) + S.AirdropKits.Count) % S.AirdropKits.Count];

        List<SteamPlayer> JogadoresFiltrados(EstadoAdmin e)
            => Provider.clients.Where(sp => sp?.player != null &&
                    (e.Busca == "" || sp.playerID.characterName.IndexOf(e.Busca, StringComparison.OrdinalIgnoreCase) >= 0 ||
                     sp.playerID.playerName.IndexOf(e.Busca, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(sp => sp.playerID.characterName, StringComparer.OrdinalIgnoreCase).ToList();

        static string Direcao(Vector3 de, Vector3 para)
        {
            var d = para - de;
            float ang = (Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg + 360f) % 360f;
            string[] nomes = { "N", "NE", "L", "SE", "S", "SO", "O", "NO" };
            return nomes[Mathf.RoundToInt(ang / 45f) % 8];
        }

        void PreencherListaAdmin(UnturnedPlayer p, EstadoAdmin e)
        {
            var lista = JogadoresFiltrados(e);
            int paginas = Math.Max(1, (lista.Count + JogadoresPorPagina - 1) / JogadoresPorPagina);
            e.Pagina = Mathf.Clamp(e.Pagina, 0, paginas - 1);
            for (int i = 0; i < JogadoresPorPagina; i++)
            {
                int idx = e.Pagina * JogadoresPorPagina + i;
                string n = "Adm_Jog_" + (i + 1);
                if (idx >= lista.Count) { V(p, n, false); continue; }
                var sp = lista[idx];
                var id = sp.playerID.steamID.m_SteamID;
                var pos = sp.player.transform.position;
                int dist = Mathf.RoundToInt(Vector3.Distance(p.Position, pos));
                var f = Stats.Obter(id, sp.playerID.characterName);
                V(p, n, true);
                string nome = Chat.Limpar(sp.playerID.characterName);
                if (id == e.Alvo) nome = C(Laranja, "> " + nome);
                if (EhDono(id) || staff.ContainsKey(id)) nome += C(Ambar, " [STAFF]");
                T(p, n + "_Nome", nome);
                T(p, n + "_Info", id == Id(p) ? C(Cinza, "voce") :
                    $"Lv.{f.Nivel}  {dist} m {Direcao(p.Position, pos)}  {Mathf.RoundToInt(sp.ping * 1000)} ms" +
                    (congelados.Contains(id) ? C(Ambar, "  CONGELADO") : ""));
            }
            T(p, "Adm_Jog_Pagina", $"{e.Pagina + 1}/{paginas}  ({lista.Count} online)");
            e.ProximaLista = Time.realtimeSinceStartup + 3f;
        }

        UnturnedPlayer AlvoDe(EstadoAdmin e)
        {
            if (e.Alvo == 0) return null;
            var sp = PlayerTool.getSteamPlayer(e.Alvo);
            return sp?.player != null ? UnturnedPlayer.FromSteamPlayer(sp) : null;
        }

        void PreencherAlvo(UnturnedPlayer p, EstadoAdmin e)
        {
            var a = AlvoDe(e);
            if (a == null)
            {
                T(p, "Adm_Alvo_Nome", C(Cinza, "Nenhum jogador selecionado"));
                T(p, "Adm_Alvo_Info", "Clique num jogador da lista.");
                T(p, "Adm_Congelar_Texto", "CONGELAR");
                return;
            }
            var f = FichaDe(a);
            int dist = Mathf.RoundToInt(Vector3.Distance(p.Position, a.Position));
            T(p, "Adm_Alvo_Nome", Chat.Limpar(a.CharacterName) + C(Cinza, "  " + Chat.Limpar(a.SteamName)));
            T(p, "Adm_Alvo_Info", $"{Id(a)}\n{a.Health} HP  |  {dist} m {Direcao(p.Position, a.Position)}  |  " +
                (ZonaSeguraEm(a.Position) != null ? C(Verde, "SAFE") : C(Vermelho, "PVP")) +
                $"\nLv.{f.Nivel}  {f.Abates} abates  {f.Mortes} mortes  K/D {f.Kd:0.00}  {Chat.Tempo(f.Segundos)} jogado");
            T(p, "Adm_Congelar_Texto", congelados.Contains(Id(a)) ? "DESCONGELAR" : "CONGELAR");
        }

        void PreencherStaff(UnturnedPlayer p, EstadoAdmin e)
        {
            var a = AlvoDe(e);
            MembroStaff m = null;
            if (a != null) staff.TryGetValue(Id(a), out m);
            T(p, "Adm_Staff_Nome", a == null ? C(Cinza, "Selecione um jogador na lista") :
                Chat.Limpar(a.CharacterName) + (EhDono(a) ? C(Ambar, "  (DONO: tem tudo)") : m == null ? C(Cinza, "  (nao e staff)") : C(Verde, "  (staff)")));
            foreach (var perm in PermissoesAdmin)
                V(p, "Adm_P_" + perm + "_On", a != null && (EhDono(a) || (m != null && m.Perms.Contains(perm))));
            T(p, "Adm_Staff_Lista", staff.Count == 0 ? C(Cinza, "Ninguem na staff ainda.") :
                string.Join("\n", staff.Select(kv => C(Laranja, Chat.Limpar(kv.Value.Nome)) + C(Cinza, "  " +
                    (kv.Value.Perms.Count == 0 ? "sem permissao" : string.Join(", ", kv.Value.Perms))))));
        }

        void AoFecharMenuAdmin(UnturnedPlayer p)
        {
            if (admins.TryGetValue(Id(p), out var e)) e.StaffAberto = false;
        }

        // ---------- Campos de texto ----------

        void DigitouAdmin(UnturnedPlayer p, string campo, string texto)
        {
            if (!EhStaff(p)) return;
            var e = EstadoAdm(p);
            texto = (texto ?? "").Trim();
            switch (campo)
            {
                case "Adm_Busca": e.Busca = texto; e.Pagina = 0; PreencherListaAdmin(p, e); break;
                case "Adm_Motivo": e.Motivo = texto; break;
                case "Adm_BanHoras": e.BanHoras = texto; break;
                case "Adm_ItemId": e.ItemId = texto; break;
                case "Adm_ItemQtd": e.ItemQtd = texto; break;
                case "Adm_Anuncio": e.Anuncio = texto; break;
                case "Preco_Busca": e.PrecoBusca = texto; e.PrecoPagina = 0; PreencherPrecos(p, e); break;
                case "Preco_Compra": e.PrecoCompra = texto; break;
                case "Preco_Venda": e.PrecoVenda = texto; break;
                case "Preco_Porcento": e.PrecoPorc = texto; break;
            }
        }

        // ---------- Botões ----------

        void CliqueAdmin(UnturnedPlayer p, EstadoMenu menu, string botao)
        {
            if (!EhStaff(p)) return;
            var e = EstadoAdm(p);

            if (botao.StartsWith("Adm_Jog_") && int.TryParse(botao.Substring(8), out var nj))
            {
                var lista = JogadoresFiltrados(e);
                int idx = e.Pagina * JogadoresPorPagina + nj - 1;
                if (nj < 1 || idx >= lista.Count) return;
                e.Alvo = lista[idx].playerID.steamID.m_SteamID;
                PreencherListaAdmin(p, e);
                PreencherAlvo(p, e);
                if (e.StaffAberto && EhDono(p)) PreencherStaff(p, e);
                return;
            }
            if (botao.StartsWith("Adm_P_")) { AlternarPermissao(p, e, botao.Substring(6)); return; }
            if (botao == "Preco_Fechar") { e.PrecosAberto = false; V(p, "Adm_Precos_Area", false); return; }
            if (botao.StartsWith("Preco_")) { if (Pode(p, "Precos")) CliquePreco(p, e, botao); return; }
            if (BotaoPermissao.TryGetValue(botao, out var perm) && !Pode(p, perm)) { AvisoAdm(p, "Voce nao tem permissao pra isso.", false); return; }

            var a = AlvoDe(e);
            switch (botao)
            {
                case "Adm_Jog_Anterior": e.Pagina--; PreencherListaAdmin(p, e); break;
                case "Adm_Jog_Proximo": e.Pagina++; PreencherListaAdmin(p, e); break;

                case "Adm_Kick":
                    if (!AlvoValido(p, a, false)) return;
                    var mk = e.Motivo == "" ? "Kickado por um admin." : e.Motivo;
                    LogAdmin(p, $"KICK {a.CharacterName} ({Id(a)}): {mk}");
                    Provider.kick(a.CSteamID, mk);
                    AvisoAdm(p, Chat.Limpar(a.CharacterName) + " foi kickado.", true);
                    e.Alvo = 0; PreencherListaAdmin(p, e); PreencherAlvo(p, e);
                    break;

                case "Adm_Ban":
                    if (!AlvoValido(p, a, false)) return;
                    var mb = e.Motivo == "" ? "Banido por um admin." : e.Motivo;
                    uint dur = SteamBlacklist.PERMANENT;
                    if (double.TryParse(e.BanHoras.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var h) && h > 0)
                        dur = (uint)Math.Min(uint.MaxValue - 1, h * 3600);
                    LogAdmin(p, $"BAN {a.CharacterName} ({Id(a)}) {(dur == SteamBlacklist.PERMANENT ? "permanente" : e.BanHoras + "h")}: {mb}");
                    Provider.requestBanPlayer(p.CSteamID, a.CSteamID, a.Player.channel.owner.getIPv4AddressOrZero(), a.Player.channel.owner.playerID.GetHwids(), mb, dur);
                    AvisoAdm(p, Chat.Limpar(a.CharacterName) + " foi banido.", true);
                    e.Alvo = 0; PreencherListaAdmin(p, e); PreencherAlvo(p, e);
                    break;

                case "Adm_IrAte":
                    if (!AlvoValido(p, a, true)) return;
                    LogAdmin(p, $"TP ate {a.CharacterName} ({Id(a)})");
                    FecharMenu(p);
                    p.Teleport(a.Position + Vector3.up * 0.5f, p.Rotation);
                    break;

                case "Adm_Trazer":
                    if (!AlvoValido(p, a, false)) return;
                    if (a.Player.movement.getVehicle() != null) a.Player.movement.forceRemoveFromVehicle();
                    LogAdmin(p, $"TROUXE {a.CharacterName} ({Id(a)})");
                    a.Teleport(p.Position + p.Player.transform.forward * 1.5f + Vector3.up * 0.5f, a.Rotation);
                    Chat.Aviso(a, "Um admin te puxou ate ele.");
                    AvisoAdm(p, Chat.Limpar(a.CharacterName) + " foi trazido.", true);
                    break;

                case "Adm_Curar":
                    if (!AlvoValido(p, a, true)) return;
                    Curar(a);
                    LogAdmin(p, $"CUROU {a.CharacterName} ({Id(a)})");
                    AvisoAdm(p, Chat.Limpar(a.CharacterName) + " foi curado.", true);
                    break;

                case "Adm_CurarTodos":
                    if (!Pode(p, "Curar")) { AvisoAdm(p, "Voce nao tem permissao pra isso.", false); return; }
                    foreach (var sp in Provider.clients) if (sp?.player != null && !sp.player.life.isDead) Curar(UnturnedPlayer.FromSteamPlayer(sp));
                    Chat.Todos(Chat.Cor(S.CorOk, "Um admin curou todo mundo!"));
                    LogAdmin(p, "CUROU TODOS");
                    AvisoAdm(p, "Todos curados.", true);
                    break;

                case "Adm_Matar":
                    if (!AlvoValido(p, a, false)) return;
                    if (a.Dead) { AvisoAdm(p, "Ele ja esta morto.", false); return; }
                    LogAdmin(p, $"MATOU {a.CharacterName} ({Id(a)})");
                    a.Player.life.askDamage(byte.MaxValue, Vector3.up, EDeathCause.KILL, ELimb.SKULL, CSteamID.Nil, out _);
                    AvisoAdm(p, Chat.Limpar(a.CharacterName) + " foi morto.", true);
                    break;

                case "Adm_Congelar":
                    if (!AlvoValido(p, a, false)) return;
                    bool congelar = !congelados.Contains(Id(a));
                    Congelar(a, congelar);
                    LogAdmin(p, (congelar ? "CONGELOU " : "DESCONGELOU ") + $"{a.CharacterName} ({Id(a)})");
                    AvisoAdm(p, Chat.Limpar(a.CharacterName) + (congelar ? " congelado." : " descongelado."), true);
                    PreencherAlvo(p, e);
                    break;

                case "Adm_Spy":
                    if (!AlvoValido(p, a, true)) return;
                    PedirFotoDaTela(p, a);
                    break;

                case "Adm_DarAlvo":
                case "Adm_DarMim":
                    if (!Pode(p, "Itens")) { AvisoAdm(p, "Voce nao tem permissao pra isso.", false); return; }
                    var quem = botao == "Adm_DarMim" ? p : a;
                    if (quem == null) { AvisoAdm(p, "Selecione um jogador na lista.", false); return; }
                    if (!ushort.TryParse(e.ItemId, out var item) || !Itens.Existe(item)) { AvisoAdm(p, "ID de item invalido. Digite o numero e aperte Enter.", false); return; }
                    if (!int.TryParse(e.ItemQtd, out var qtd) || qtd < 1) qtd = 1;
                    qtd = Math.Min(qtd, 100);
                    Itens.Dar(quem, item, qtd);
                    LogAdmin(p, $"DEU {qtd}x {Itens.Nome(item)} ({item}) para {quem.CharacterName} ({Id(quem)})");
                    AvisoAdm(p, $"{qtd}x {Chat.Limpar(Itens.Nome(item))} para {Chat.Limpar(quem.CharacterName)}.", true);
                    break;

                case "Adm_Dia":
                case "Adm_Noite":
                    if (!Pode(p, "Mundo")) { AvisoAdm(p, "Voce nao tem permissao pra isso.", false); return; }
                    bool dia = botao == "Adm_Dia";
                    LightingManager.time = (uint)(LightingManager.cycle * (dia ? LevelLighting.transition : LevelLighting.bias + LevelLighting.transition));
                    LogAdmin(p, dia ? "MUDOU PRA DIA" : "MUDOU PRA NOITE");
                    AvisoAdm(p, dia ? "Agora e dia." : "Agora e noite.", true);
                    break;

                case "Adm_Anunciar":
                    if (!Pode(p, "Mundo")) { AvisoAdm(p, "Voce nao tem permissao pra isso.", false); return; }
                    if (e.Anuncio == "") { AvisoAdm(p, "Digite o anuncio e aperte Enter antes.", false); return; }
                    var txt = Chat.Limpar(e.Anuncio);
                    for (int i = 0; i < 2; i++) Chat.Todos(Chat.Cor(S.CorDestaque, Chat.Forte("ANUNCIO: ")) + Chat.Cor("#FFFFFF", Chat.Forte(txt)));
                    LogAdmin(p, "ANUNCIO: " + e.Anuncio);
                    AvisoAdm(p, "Anuncio enviado.", true);
                    break;

                case "Adm_Visao":
                    bool ligar = !comVisao.Contains(Id(p));
                    Visao(p, ligar);
                    V(p, "Adm_Visao_On", ligar);
                    LogAdmin(p, ligar ? "LIGOU visao de admin" : "DESLIGOU visao de admin");
                    AvisoAdm(p, ligar ? "Visao ligada: marcadores nos jogadores e freecam liberada." : "Visao desligada.", true);
                    break;

                case "Adm_Deus":
                    bool imortal = imortais.Add(Id(p));
                    if (!imortal) imortais.Remove(Id(p));
                    V(p, "Adm_Deus_On", imortal);
                    if (imortal) Curar(p);
                    LogAdmin(p, imortal ? "LIGOU imortal" : "DESLIGOU imortal");
                    AvisoAdm(p, imortal ? "Imortal ligado." : "Imortal desligado.", true);
                    break;

                case "Adm_Drop_Kit":
                    if (!Pode(p, "Airdrop")) return;
                    e.KitDrop++;
                    T(p, "Adm_Drop_Kit_Texto", KitDropAtual(e)?.Nome?.ToUpperInvariant() ?? "-");
                    AvisoAdm(p, "Conteudo: " + ListaItens(KitDropAtual(e)?.Itens), true);
                    break;

                case "Adm_Drop_Aqui":
                case "Adm_Drop_Marcador":
                case "Adm_Drop_Alvo":
                    if (!Pode(p, "Airdrop")) { AvisoAdm(p, "Voce nao tem permissao pra isso.", false); return; }
                    Vector3 onde;
                    if (botao == "Adm_Drop_Aqui") onde = p.Position;
                    else if (botao == "Adm_Drop_Alvo") { if (a == null) { AvisoAdm(p, "Selecione um jogador na lista.", false); return; } onde = a.Position; }
                    else
                    {
                        if (!p.Player.quests.isMarkerPlaced) { AvisoAdm(p, "Marque um ponto no mapa primeiro (mapa, botao direito).", false); return; }
                        onde = p.Player.quests.markerPosition;
                    }
                    if (MandarAirdrop(onde, KitDropAtual(e), out var erro))
                    {
                        LogAdmin(p, $"AIRDROP {KitDropAtual(e)?.Nome} em {onde.x:0} {onde.z:0}");
                        AvisoAdm(p, "Airdrop a caminho!", true);
                    }
                    else AvisoAdm(p, erro, false);
                    break;

                case "Adm_Reiniciar":
                    if (reinicioEm > 0) { AvisoAdm(p, "O reinicio ja esta marcado.", false); return; }
                    T(p, "Adm_Confirmar_Texto", $"Reiniciar o servidor? Os jogadores ({Provider.clients.Count} online) recebem aviso de 60 segundos.");
                    V(p, "Adm_Confirmar", true);
                    break;
                case "Adm_Confirmar_Nao": V(p, "Adm_Confirmar", false); break;
                case "Adm_Confirmar_Sim":
                    V(p, "Adm_Confirmar", false);
                    if (!Pode(p, "Reiniciar")) { AvisoAdm(p, "Voce nao tem permissao pra isso.", false); return; }
                    reinicioEm = Time.realtimeSinceStartup + 60f; ultimoAvisoReinicio = -1;
                    LogAdmin(p, "REINICIO do servidor em 60s");
                    AvisoAdm(p, "Reinicio em 60 segundos.", true);
                    break;

                case "Adm_Precos":
                    e.PrecosAberto = !e.PrecosAberto;
                    if (e.PrecosAberto) { e.StaffAberto = false; V(p, "Adm_Staff_Area", false); }
                    V(p, "Adm_Precos_Area", e.PrecosAberto);
                    if (e.PrecosAberto) PreencherPrecos(p, e);
                    break;

                case "Adm_Staff":
                    if (!EhDono(p)) return;
                    e.PrecosAberto = false; V(p, "Adm_Precos_Area", false);
                    e.StaffAberto = !e.StaffAberto;
                    V(p, "Adm_Staff_Area", e.StaffAberto);
                    if (e.StaffAberto) PreencherStaff(p, e);
                    break;
                case "Adm_Staff_Fechar": e.StaffAberto = false; V(p, "Adm_Staff_Area", false); break;
                case "Adm_Staff_Remover":
                    if (!EhDono(p)) return;
                    if (a == null || !staff.Remove(Id(a))) { AvisoAdm(p, "Selecione alguem da staff na lista.", false); return; }
                    SalvarStaff();
                    LogAdmin(p, $"TIROU {a.CharacterName} ({Id(a)}) da staff");
                    AvisoAdm(p, Chat.Limpar(a.CharacterName) + " saiu da staff.", true);
                    AtualizarAcessoAdmin(a);
                    PreencherStaff(p, e);
                    PreencherListaAdmin(p, e);
                    break;
            }
        }

        // ---------- Editor de preços ----------

        List<Loja> LojasCfg => Configuration.Instance.Lojas;
        Loja LojaPreco(EstadoAdmin e) => LojasCfg.Count == 0 ? null : LojasCfg[((e.PrecoLoja % LojasCfg.Count) + LojasCfg.Count) % LojasCfg.Count];

        List<ItemLoja> ItensPreco(EstadoAdmin e)
        {
            var loja = LojaPreco(e);
            if (loja == null) return new List<ItemLoja>();
            return loja.Itens.Where(i => e.PrecoBusca == "" || i.Id.ToString() == e.PrecoBusca ||
                NomeDe(i).IndexOf(e.PrecoBusca, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        void PreencherPrecos(UnturnedPlayer p, EstadoAdmin e)
        {
            var loja = LojaPreco(e);
            if (loja == null) { T(p, "Preco_Loja_Texto", "SEM LOJAS"); return; }
            T(p, "Preco_Loja_Texto", Chat.Limpar(loja.Nome).ToUpperInvariant());
            var lista = ItensPreco(e);
            int paginas = Math.Max(1, (lista.Count + 7) / 8);
            e.PrecoPagina = Mathf.Clamp(e.PrecoPagina, 0, paginas - 1);
            for (int i = 0; i < 8; i++)
            {
                int idx = e.PrecoPagina * 8 + i;
                string n = "Preco_Item_" + (i + 1);
                if (idx >= lista.Count) { V(p, n, false); continue; }
                var it = lista[idx];
                V(p, n, true);
                T(p, n + "_Nome", (it == e.PrecoSel ? C(Laranja, "> ") : "") + Chat.Limpar(NomeDe(it)) + C(Cinza, " #" + it.Id));
                T(p, n + "_Compra", it.Compra > 0 ? "$ " + it.Compra.ToString("N0") : C(Cinza, "-"));
                T(p, n + "_Venda", it.Venda > 0 ? "$ " + it.Venda.ToString("N0") : C(Cinza, "-"));
            }
            T(p, "Preco_Pagina", $"{e.PrecoPagina + 1}/{paginas}");
            T(p, "Preco_Sel_Nome", e.PrecoSel == null ? C(Cinza, "Clique num item") :
                Chat.Limpar(NomeDe(e.PrecoSel)) + C(Cinza, $"  compra $ {e.PrecoSel.Compra:N0} | venda $ {e.PrecoSel.Venda:N0}"));
        }

        static int AplicarPorcento(int v, double porc) => v <= 0 ? v : Math.Max(1, (int)Math.Round(v * (1 + porc / 100.0)));

        void CliquePreco(UnturnedPlayer p, EstadoAdmin e, string botao)
        {
            if (botao.StartsWith("Preco_Item_") && int.TryParse(botao.Substring(11), out var n))
            {
                var lista = ItensPreco(e);
                int idx = e.PrecoPagina * 8 + n - 1;
                if (idx < lista.Count) e.PrecoSel = lista[idx];
                PreencherPrecos(p, e);
                return;
            }
            switch (botao)
            {
                case "Preco_Loja": e.PrecoLoja++; e.PrecoPagina = 0; e.PrecoSel = null; PreencherPrecos(p, e); break;
                case "Preco_Anterior": e.PrecoPagina--; PreencherPrecos(p, e); break;
                case "Preco_Proximo": e.PrecoPagina++; PreencherPrecos(p, e); break;
                case "Preco_Salvar":
                    if (e.PrecoSel == null) { AvisoAdm(p, "Escolha um item na lista.", false); return; }
                    bool mudou = false;
                    if (int.TryParse(e.PrecoCompra, out var c) && c >= 0) { e.PrecoSel.Compra = c; mudou = true; }
                    if (int.TryParse(e.PrecoVenda, out var v) && v >= 0) { e.PrecoSel.Venda = v; mudou = true; }
                    if (!mudou) { AvisoAdm(p, "Digite o novo preco (compra e/ou venda) e aperte Enter.", false); return; }
                    Configuration.Save();
                    LogAdmin(p, $"PRECO {NomeDe(e.PrecoSel)} ({e.PrecoSel.Id}): compra {e.PrecoSel.Compra}, venda {e.PrecoSel.Venda}");
                    AvisoAdm(p, "Preco salvo. 0 = nao vende/nao compra.", true);
                    PreencherPrecos(p, e);
                    break;
                case "Preco_AplicarLoja":
                case "Preco_AplicarTodas":
                    if (!double.TryParse(e.PrecoPorc.Replace("%", "").Replace(',', '.'), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var porc) || porc == 0 || porc <= -95 || porc > 1000)
                    { AvisoAdm(p, "Digite a porcentagem (ex.: 10 ou -15) e aperte Enter.", false); return; }
                    var lojas = botao == "Preco_AplicarTodas" ? LojasCfg : new List<Loja> { LojaPreco(e) };
                    foreach (var l in lojas.Where(x => x != null))
                        foreach (var it in l.Itens) { it.Compra = AplicarPorcento(it.Compra, porc); it.Venda = AplicarPorcento(it.Venda, porc); }
                    Configuration.Save();
                    LogAdmin(p, $"PRECOS {porc:+0.#;-0.#}% em {(lojas.Count == 1 ? lojas[0]?.Nome : "todas as lojas")}");
                    AvisoAdm(p, $"Precos ajustados em {porc:+0.#;-0.#}%.", true);
                    PreencherPrecos(p, e);
                    break;
            }
        }

        /// <summary>Confere se tem alvo e se pode mexer nele (staff não mexe no dono; ninguém se kicka).</summary>
        bool AlvoValido(UnturnedPlayer p, UnturnedPlayer a, bool podeSerEleMesmo)
        {
            if (a == null) { AvisoAdm(p, "Selecione um jogador na lista.", false); return false; }
            if (!podeSerEleMesmo && Id(a) == Id(p)) { AvisoAdm(p, "Nao da pra fazer isso com voce mesmo.", false); return false; }
            if (Id(a) != Id(p) && EhDono(a) && !EhDono(p)) { AvisoAdm(p, "Nao da pra fazer isso com o dono.", false); return false; }
            return true;
        }

        void AlternarPermissao(UnturnedPlayer p, EstadoAdmin e, string perm)
        {
            if (!EhDono(p) || !PermissoesAdmin.Contains(perm)) return;
            var a = AlvoDe(e);
            if (a == null) { AvisoAdm(p, "Selecione um jogador na lista.", false); return; }
            if (EhDono(a)) { AvisoAdm(p, "O dono ja tem tudo.", false); return; }
            if (!staff.TryGetValue(Id(a), out var m)) staff[Id(a)] = m = new MembroStaff();
            m.Nome = a.CharacterName;
            bool ligou = m.Perms.Add(perm);
            if (!ligou) m.Perms.Remove(perm);
            SalvarStaff();
            LogAdmin(p, $"{(ligou ? "DEU" : "TIROU")} permissao {perm} {(ligou ? "para" : "de")} {a.CharacterName} ({Id(a)})");
            V(p, "Adm_P_" + perm + "_On", ligou);
            AvisoAdm(p, $"{perm} {(ligou ? "liberado" : "tirado")} pra {Chat.Limpar(a.CharacterName)}.", true);
            AtualizarAcessoAdmin(a);
            PreencherStaff(p, e);
        }

        /// <summary>Se a pessoa estiver com o menu aberto, mostra/esconde a aba ADM na hora.</summary>
        void AtualizarAcessoAdmin(UnturnedPlayer a)
        {
            if (!menus.TryGetValue(Id(a), out var m)) return;
            V(a, "Aba_Admin", EhStaff(a));
            if (m.Aba == "Admin") MostrarAba(a, m, EhStaff(a) ? "Admin" : "Inicio");
            if (!EhStaff(a)) { Visao(a, false); imortais.Remove(Id(a)); }
        }

        // ---------- Ações ----------

        static void Curar(UnturnedPlayer a)
        {
            if (a?.Player == null || a.Dead) return;
            a.Player.life.sendRevive();
        }

        void Congelar(UnturnedPlayer a, bool sim)
        {
            if (sim) congelados.Add(Id(a)); else congelados.Remove(Id(a));
            a.Player.movement.sendPluginSpeedMultiplier(sim ? 0f : 1f);
            a.Player.movement.sendPluginJumpMultiplier(sim ? 0f : 1f);
            if (sim) Chat.Erro(a, "Voce foi congelado por um admin."); else Chat.Ok(a, "Voce foi descongelado.");
        }

        void Visao(UnturnedPlayer p, bool ligar)
        {
            if (ligar) comVisao.Add(Id(p)); else comVisao.Remove(Id(p));
            if (p?.Player == null) return;
            // freecam e nomes por cima (ferramenta de admin do próprio jogo)
            bool liberar = ligar || p.IsAdmin;
            p.Player.look.sendFreecamAllowed(liberar);
            p.Player.look.sendSpecStatsAllowed(liberar);
        }

        void PedirFotoDaTela(UnturnedPlayer adm, UnturnedPlayer a)
        {
            var pasta = Path.Combine(Directory, "spy");
            System.IO.Directory.CreateDirectory(pasta);
            var nome = Chat.Limpar(a.CharacterName);
            var alvoId = Id(a);
            a.Player.sendScreenshot(adm.CSteamID, (quem, jpg) =>
            {
                try { File.WriteAllBytes(Path.Combine(pasta, $"{alvoId}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg"), jpg); } catch { }
            });
            LogAdmin(adm, $"SPY (foto da tela) de {a.CharacterName} ({alvoId})");
            AvisoAdm(adm, "Foto da tela pedida. Feche o menu: ela aparece na sua tela em alguns segundos.", true);
        }

        // ---------- Airdrop ----------

        static SpawnAsset tabelaAirdrop;

        bool MandarAirdrop(Vector3 onde, KitAirdrop kit, out string erro)
        {
            erro = null;
            if (kit == null) { erro = "Nenhum conteudo de airdrop configurado."; return false; }
            if (tabelaAirdrop == null)
            {
                var todas = new List<SpawnAsset>();
                Assets.find(todas);
                tabelaAirdrop = todas.FirstOrDefault(t => t.name != null && t.name.IndexOf("airdrop", StringComparison.OrdinalIgnoreCase) >= 0) ?? todas.FirstOrDefault();
            }
            if (tabelaAirdrop == null) { erro = "Nao achei tabela de airdrop no servidor."; return false; }
            airdrops.Add(new AirdropPendente { Pos = onde, Kit = kit, Fim = Time.realtimeSinceStartup + 300f, JaExistiam = new HashSet<uint>(CaixasPerto(onde).Select(d => d.instanceID)) });
            LevelManager.SpawnAirdrop(onde, tabelaAirdrop);
            Chat.Todos(Chat.Cor(S.CorDestaque, Chat.Forte("AIRDROP " + kit.Nome.ToUpperInvariant() + " A CAMINHO!")) + Chat.Cor(S.CorApagada, " Olhe pro ceu."));
            return true;
        }

        static IEnumerable<BarricadeDrop> CaixasPerto(Vector3 pos, float raio = 80f)
        {
            if (!Regions.tryGetCoordinate(pos, out var rx, out var ry)) yield break;
            for (int x = Math.Max(0, rx - 1); x <= Math.Min(Regions.WORLD_SIZE - 1, rx + 1); x++)
                for (int y = Math.Max(0, ry - 1); y <= Math.Min(Regions.WORLD_SIZE - 1, ry + 1); y++)
                    foreach (var d in BarricadeManager.regions[x, y].drops)
                    {
                        if (!(d.interactable is InteractableStorage) || d.model == null) continue;
                        var dx = d.model.position.x - pos.x; var dz = d.model.position.z - pos.z;
                        if (dx * dx + dz * dz <= raio * raio) yield return d;
                    }
        }

        /// <summary>Quando a caixa do airdrop cai, troca o que veio pelo kit escolhido.</summary>
        void TickAirdrops()
        {
            for (int i = airdrops.Count - 1; i >= 0; i--)
            {
                var ad = airdrops[i];
                if (Time.realtimeSinceStartup > ad.Fim) { airdrops.RemoveAt(i); continue; }
                var caixa = CaixasPerto(ad.Pos).FirstOrDefault(d => !ad.JaExistiam.Contains(d.instanceID));
                if (caixa == null) continue;
                airdrops.RemoveAt(i);
                var st = (InteractableStorage)caixa.interactable;
                st.items.clear();
                foreach (var it in ad.Kit.Itens ?? new List<ItemKit>())
                {
                    if (!Itens.Existe(it.Id)) continue;
                    for (int q = 0; q < it.Quantidade; q++)
                    {
                        var item = new Item(it.Id, true);
                        if (!st.items.tryAddItem(item)) ItemManager.dropItem(item, caixa.model.position + Vector3.up, false, true, true);
                    }
                }
            }
        }

        // ---------- Tick ----------

        void TickAdmin()
        {
            TickAirdrops();
            TickReinicio();

            // marcadores da visão de admin (efeito 51232, só o admin vê)
            if (comVisao.Count > 0 && Assets.find(EAssetType.EFFECT, MarcadorId) is EffectAsset marcador)
                foreach (var id in comVisao.ToList())
                {
                    var adm = PlayerTool.getSteamPlayer(id);
                    if (adm?.player == null) { comVisao.Remove(id); continue; }
                    var meu = adm.player.transform.position;
                    foreach (var sp in Provider.clients)
                    {
                        if (sp?.player == null || sp == adm || sp.player.life.isDead) continue;
                        var pos = sp.player.transform.position;
                        if (Vector3.Distance(meu, pos) > 1500f) continue;
                        var par = new TriggerEffectParameters(marcador) { position = pos, reliable = false };
                        par.SetRelevantPlayer(adm);
                        EffectManager.triggerEffect(par);
                    }
                }

            // lista de jogadores da aba ADM se atualiza sozinha
            foreach (var kv in menus)
            {
                if (kv.Value.Aba != "Admin" || !admins.TryGetValue(kv.Key, out var e) || Time.realtimeSinceStartup < e.ProximaLista) continue;
                var sp = PlayerTool.getSteamPlayer(kv.Key);
                if (sp?.player == null) continue;
                var p = UnturnedPlayer.FromSteamPlayer(sp);
                PreencherListaAdmin(p, e);
                PreencherAlvo(p, e);
            }
        }

        void TickReinicio()
        {
            if (reinicioEm < 0) return;
            int resta = Mathf.CeilToInt(reinicioEm - Time.realtimeSinceStartup);
            if (resta == ultimoAvisoReinicio) return;
            ultimoAvisoReinicio = resta;
            if (resta == 60 || resta == 30 || resta == 10 || (resta > 0 && resta <= 5))
                Chat.Todos(Chat.Cor(S.CorPerigo, Chat.Forte($"O SERVIDOR VAI REINICIAR EM {resta}s.")) + Chat.Cor(S.CorApagada, " Volta em 1 a 2 minutos."));
            if (resta > 0) return;
            reinicioEm = -1;
            try { Stats.Salvar(true); Cofres.SalvarTodos(); SaveManager.save(); } catch (Exception ex) { Rocket.Core.Logging.Logger.LogException(ex); }
            Provider.shutdown(0, "Reiniciando. Volta em 1 a 2 minutos.");
        }

        // ---------- Ao sair / morrer ----------

        void LimparAdmin(UnturnedPlayer p)
        {
            var id = Id(p);
            comVisao.Remove(id);
            imortais.Remove(id);
            congelados.Remove(id);
            admins.Remove(id);
        }

        /// <summary>Imortal: bloqueia todo dano no admin.</summary>
        public bool EhImortal(Player pl) => pl != null && imortais.Contains(pl.channel.owner.playerID.steamID.m_SteamID);
    }

    /// <summary>/adm abre o menu direto na aba ADMIN (só staff).</summary>
    public class CmdAdm : ComandoSistema
    {
        public override string Name => "adm";
        public override string Help => "Abre o painel de admin.";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (!P.EhStaff(p)) { Chat.Erro(p, "Voce nao e da staff."); return; }
            if (P.MenuAberto(p)) P.FecharMenu(p);
            if (!P.AbrirMenu(p, "Admin")) Chat.Erro(p, "O menu ainda nao foi instalado no servidor.");
        }
    }

    /// <summary>/staff: o dono lista ou tira alguém da staff mesmo offline.</summary>
    public class CmdStaff : ComandoSistema
    {
        public override string Name => "staff";
        public override string Help => "Lista a staff ou tira alguem (dono).";
        public override string Syntax => "[remover <steamid>]";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (!P.EhDono(p)) { Chat.Erro(p, "So o dono usa isso."); return; }
            if (a.Length >= 2 && a[0].Equals("remover", StringComparison.OrdinalIgnoreCase) && ulong.TryParse(a[1], out var id))
            {
                Chat.Info(p, P.RemoverStaff(p, id) ? "Removido da staff." : "Esse Steam ID nao esta na staff.");
                return;
            }
            Chat.Para(p, Cor(S.CorPrincipal, Chat.Forte("STAFF")) + Cor(S.CorApagada, "  (/staff remover steamid)"));
            foreach (var l in P.ListaStaff()) Chat.Para(p, Cor(S.CorTexto, l));
        }
    }

    public partial class Plugin
    {
        public bool RemoverStaff(UnturnedPlayer dono, ulong id)
        {
            if (!staff.TryGetValue(id, out var m)) return false;
            staff.Remove(id);
            SalvarStaff();
            LogAdmin(dono, $"TIROU {m.Nome} ({id}) da staff");
            var sp = PlayerTool.getSteamPlayer(id);
            if (sp?.player != null) AtualizarAcessoAdmin(UnturnedPlayer.FromSteamPlayer(sp));
            return true;
        }

        public IEnumerable<string> ListaStaff()
            => staff.Count == 0 ? new[] { "Ninguem na staff ainda. Use a aba ADMIN do /menu." } :
               staff.Select(kv => $"{Chat.Limpar(kv.Value.Nome)} ({kv.Key}): {(kv.Value.Perms.Count == 0 ? "sem permissao" : string.Join(", ", kv.Value.Perms))}");
    }
}
