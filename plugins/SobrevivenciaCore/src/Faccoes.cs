using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using UnityEngine;

namespace SobrevivenciaCore
{
    // ======================= Dados (faccoes.xml) =======================

    public class NivelFaccao
    {
        [XmlAttribute] public int Nivel;
        [XmlAttribute] public int Vagas;
        // preço pra chegar nesse nível (o 1 é o preço de criar)
        [XmlAttribute] public int Preco;
        [XmlAttribute] public byte BauLargura;
        [XmlAttribute] public byte BauAltura;

        public static List<NivelFaccao> Padrao() => new List<NivelFaccao>
        {
            new NivelFaccao { Nivel = 1, Vagas = 6,  Preco = 5000,  BauLargura = 6,  BauAltura = 4 },
            new NivelFaccao { Nivel = 2, Vagas = 8,  Preco = 15000, BauLargura = 8,  BauAltura = 5 },
            new NivelFaccao { Nivel = 3, Vagas = 10, Preco = 40000, BauLargura = 8,  BauAltura = 7 },
            new NivelFaccao { Nivel = 4, Vagas = 12, Preco = 80000, BauLargura = 10, BauAltura = 8 },
        };
    }

    public class MembroFaccao
    {
        [XmlAttribute] public ulong Id;
        [XmlAttribute] public string Nome = "";
        [XmlAttribute] public int Cargo;
        [XmlAttribute] public long Entrou;
        [XmlAttribute] public long Visto;
    }

    /// <summary>Pedido pra entrar (jogador pediu) ou convite (facção chamou).</summary>
    public class PedidoFaccao
    {
        [XmlAttribute] public ulong Id;
        [XmlAttribute] public string Nome = "";
        [XmlAttribute] public string Msg = "";
        [XmlAttribute] public long Quando;
    }

    public class MovimentoFaccao
    {
        [XmlAttribute] public long Quando;
        [XmlAttribute] public string Texto = "";
    }

    public class Faccao
    {
        // é o mesmo id do grupo nativo do Unturned
        [XmlAttribute] public ulong Id;
        [XmlAttribute] public string Nome = "";
        [XmlAttribute] public string Tag = "";
        [XmlAttribute] public string Cor = "#FF5A00";
        [XmlAttribute] public string Descricao = "";
        // 0 = aberta (entra direto), 1 = só por pedido, 2 = fechada (só convite)
        [XmlAttribute] public int Modo = 1;
        [XmlAttribute] public int Nivel = 1;
        [XmlAttribute] public long Cofre;
        [XmlAttribute] public long Criada;
        // última vez que alguém da facção esteve online (proteção offline acaba depois de N dias)
        [XmlAttribute] public long UltimoOnline;
        // preenchidos ao salvar, pro site e o Discord
        [XmlAttribute] public int Abates;
        [XmlAttribute] public int Zumbis;
        public List<string> Cargos = new List<string>();
        public List<int> Permissoes = new List<int>();
        public List<MembroFaccao> Membros = new List<MembroFaccao>();
        public List<PedidoFaccao> Pedidos = new List<PedidoFaccao>();
        public List<PedidoFaccao> Convites = new List<PedidoFaccao>();
        public List<MovimentoFaccao> Historico = new List<MovimentoFaccao>();

        [XmlIgnore] public MembroFaccao Lider => Membros.FirstOrDefault(m => m.Cargo == 0);
        public MembroFaccao Membro(ulong id) => Membros.FirstOrDefault(m => m.Id == id);
        public string NomeCargo(int c) => c >= 0 && c < Cargos.Count ? Cargos[c] : Plugin.CargosPadrao[Mathf.Clamp(c, 0, 5)];
        public string TagCor => $"<color={Cor}>[{Tag}]</color>";
    }

    public class EsperaFaccao
    {
        [XmlAttribute] public ulong Id;
        [XmlAttribute] public long Ate;
    }

    /// <summary>Grupo nativo de uma facção dissolvida: só o ex-líder continua nele (a base fica com ele).</summary>
    public class GrupoAntigo
    {
        [XmlAttribute] public ulong Grupo;
        [XmlAttribute] public ulong Dono;
    }

    [XmlRoot("Faccoes")]
    public class ArquivoFaccoes
    {
        public List<Faccao> Lista = new List<Faccao>();
        public List<EsperaFaccao> Esperas = new List<EsperaFaccao>();
        public List<GrupoAntigo> Dissolvidas = new List<GrupoAntigo>();
    }

    [Flags]
    public enum PermFac
    {
        Portas = 1, Baus = 2, Construir = 4, Desmontar = 8, Veiculos = 16,
        Depositar = 32, Sacar = 64, Recrutar = 128, Expulsar = 256, Promover = 512,
    }

    // ======================= Regras =======================

    public partial class Plugin
    {
        public static readonly string[] CargosPadrao = { "Lider", "Co-lider", "Tesoureiro", "Oficial", "Membro", "Recruta" };
        public static readonly PermFac[] PermsGrade =
            { PermFac.Portas, PermFac.Baus, PermFac.Construir, PermFac.Desmontar, PermFac.Veiculos,
              PermFac.Depositar, PermFac.Sacar, PermFac.Recrutar, PermFac.Expulsar, PermFac.Promover };
        public static readonly string[] NomesPerms =
            { "Portas", "Baus", "Construir", "Desmontar", "Veiculos", "Depositar", "Sacar", "Recrutar", "Expulsar", "Promover" };
        public static readonly string[] CoresFaccao = { "#FF5A00", "#FF4D4D", "#FFB020", "#FFD84D", "#3DDC84", "#36D6E7", "#3D8BFF", "#A066FF", "#FF66B3", "#F5F6F7" };
        public static readonly string[] NomesModo = { "ABERTA", "POR PEDIDO", "FECHADA" };

        static int[] PermsPadrao()
        {
            var t = PermFac.Portas | PermFac.Baus | PermFac.Veiculos | PermFac.Depositar;
            return new[]
            {
                (int)(PermFac)1023,
                (int)(t | PermFac.Construir | PermFac.Desmontar | PermFac.Recrutar | PermFac.Expulsar | PermFac.Promover),
                (int)(t | PermFac.Sacar),
                (int)(t | PermFac.Construir | PermFac.Recrutar),
                (int)t,
                (int)PermFac.Depositar,
            };
        }

        const long DuasDias = 48 * 3600;
        ArquivoFaccoes fac = new ArquivoFaccoes();
        readonly Dictionary<ulong, Faccao> facDoJogador = new Dictionary<ulong, Faccao>();
        public Vaults BauFac;
        bool facSujo;
        float proximoTickFac;
        bool mudandoGrupo;
        readonly HashSet<ulong> conferirGrupo = new HashSet<ulong>();

        string ArqFaccoes => Path.Combine(Directory, "faccoes.xml");
        static long Agora => Estatisticas.Agora;

        public IEnumerable<Faccao> Faccoes => fac.Lista;
        public Faccao FaccaoDe(ulong jogador) => facDoJogador.TryGetValue(jogador, out var f) ? f : null;
        public Faccao FaccaoDe(UnturnedPlayer p) => p == null ? null : FaccaoDe(Id(p));
        public Faccao FaccaoPorGrupo(ulong grupo) => grupo == 0 ? null : fac.Lista.FirstOrDefault(f => f.Id == grupo);
        public NivelFaccao NivelDe(Faccao f) => S.FaccaoNiveis.OrderBy(n => n.Nivel).LastOrDefault(n => n.Nivel <= f.Nivel) ?? S.FaccaoNiveis[0];
        public NivelFaccao ProximoNivel(Faccao f) => S.FaccaoNiveis.OrderBy(n => n.Nivel).FirstOrDefault(n => n.Nivel > f.Nivel);

        public bool TemPerm(Faccao f, ulong id, PermFac perm)
        {
            var m = f?.Membro(id);
            if (m == null) return false;
            if (m.Cargo == 0) return true;
            return m.Cargo < f.Permissoes.Count && (f.Permissoes[m.Cargo] & (int)perm) != 0;
        }

        // ---------- carregar / salvar ----------

        void LigarFaccoes()
        {
            BauFac = new Vaults(Directory, "faccoes_bau");
            try
            {
                if (File.Exists(ArqFaccoes))
                    using (var r = File.OpenRead(ArqFaccoes))
                        fac = (ArquivoFaccoes)new XmlSerializer(typeof(ArquivoFaccoes)).Deserialize(r) ?? new ArquivoFaccoes();
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e, "SobrevivenciaCore: faccoes.xml com erro"); fac = new ArquivoFaccoes(); }
            foreach (var f in fac.Lista) Completar(f);
            Reindexar();
            PlayerQuests.onGroupChanged += AoMudarGrupo;
            BarricadeManager.onOpenStorageRequested += AoAbrirBauFac;
            BarricadeManager.onSalvageBarricadeRequested += AoDesmontarBarricadaFac;
            StructureManager.onSalvageStructureRequested += AoDesmontarEstruturaFac;
            BarricadeManager.onDeployBarricadeRequested += AoColocarBarricadaFac;
            StructureManager.onDeployStructureRequested += AoColocarEstruturaFac;
            VehicleManager.onEnterVehicleRequested += AoEntrarVeiculoFac;
            PortasFaccao.Ligar();
        }

        void DesligarFaccoes()
        {
            PlayerQuests.onGroupChanged -= AoMudarGrupo;
            BarricadeManager.onOpenStorageRequested -= AoAbrirBauFac;
            BarricadeManager.onSalvageBarricadeRequested -= AoDesmontarBarricadaFac;
            StructureManager.onSalvageStructureRequested -= AoDesmontarEstruturaFac;
            BarricadeManager.onDeployBarricadeRequested -= AoColocarBarricadaFac;
            StructureManager.onDeployStructureRequested -= AoColocarEstruturaFac;
            VehicleManager.onEnterVehicleRequested -= AoEntrarVeiculoFac;
            PortasFaccao.Desligar();
            BauFac?.SalvarTodos();
            SalvarFaccoes();
        }

        static void Completar(Faccao f)
        {
            while (f.Cargos.Count < 6) f.Cargos.Add(CargosPadrao[f.Cargos.Count]);
            var pad = PermsPadrao();
            while (f.Permissoes.Count < 6) f.Permissoes.Add(pad[f.Permissoes.Count]);
            f.Permissoes[0] = pad[0];
        }

        void Reindexar()
        {
            facDoJogador.Clear();
            foreach (var f in fac.Lista)
                foreach (var m in f.Membros) facDoJogador[m.Id] = f;
        }

        void SalvarFaccoes()
        {
            try
            {
                foreach (var f in fac.Lista)
                {
                    var fichas = f.Membros.Select(m => Stats?.Todas.FirstOrDefault(x => x.Id == m.Id)).Where(x => x != null).ToList();
                    f.Abates = fichas.Sum(x => x.Abates);
                    f.Zumbis = fichas.Sum(x => x.Zumbis);
                }
                var tmp = ArqFaccoes + ".tmp";
                using (var w = File.Create(tmp))
                    new XmlSerializer(typeof(ArquivoFaccoes)).Serialize(w, fac);
                if (File.Exists(ArqFaccoes)) File.Delete(ArqFaccoes);
                File.Move(tmp, ArqFaccoes);
                facSujo = false;
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }

        void Mudou() => facSujo = true;

        void Registrar(Faccao f, string texto)
        {
            f.Historico.Insert(0, new MovimentoFaccao { Quando = Agora, Texto = texto });
            if (f.Historico.Count > 40) f.Historico.RemoveRange(40, f.Historico.Count - 40);
        }

        // ---------- tick (1x por segundo, chamado pelo ChecarZonas) ----------

        void TickFaccoes()
        {
            if (conferirGrupo.Count > 0)
            {
                foreach (var id in conferirGrupo.ToList())
                {
                    var sp = PlayerTool.getSteamPlayer(id);
                    if (sp?.player != null) AplicarGrupo(sp.player);
                }
                conferirGrupo.Clear();
            }
            if (Time.realtimeSinceStartup < proximoTickFac) { if (facSujo) SalvarFaccoes(); return; }
            proximoTickFac = Time.realtimeSinceStartup + 60f;
            var agora = Agora;
            foreach (var f in fac.Lista)
            {
                foreach (var m in f.Membros)
                    if (PlayerTool.getSteamPlayer(m.Id) != null) { m.Visto = agora; f.UltimoOnline = agora; facSujo = true; }
                if (f.Pedidos.RemoveAll(x => agora - x.Quando > DuasDias) > 0) facSujo = true;
                if (f.Convites.RemoveAll(x => agora - x.Quando > DuasDias) > 0) facSujo = true;
            }
            if (fac.Esperas.RemoveAll(e => e.Ate < agora) > 0) facSujo = true;
            if (facSujo) SalvarFaccoes();
        }

        // ---------- grupo nativo ----------

        /// <summary>Deixa o grupo nativo do jogador igual à facção dele (ou tira de um grupo de facção que não é dele).</summary>
        void AplicarGrupo(Player pl)
        {
            if (pl == null) return;
            var id = pl.channel.owner.playerID.steamID.m_SteamID;
            var atual = pl.quests.groupID.m_SteamID;
            var f = FaccaoDe(id);
            mudandoGrupo = true;
            try
            {
                if (f != null)
                {
                    if (atual == f.Id) return;
                    if (atual != 0) pl.quests.leaveGroup(true);
                    var g = GroupManager.getOrAddGroup(new CSteamID(f.Id), $"[{f.Tag}] {f.Nome}", out _);
                    g.name = $"[{f.Tag}] {f.Nome}";
                    pl.quests.ServerAssignToGroup(g.groupID, EPlayerGroupRank.MEMBER, true);
                    return;
                }
                if (atual == 0) return;
                var dona = FaccaoPorGrupo(atual);
                var antigo = fac.Dissolvidas.FirstOrDefault(d => d.Grupo == atual);
                if (dona != null || (antigo != null && antigo.Dono != id))
                {
                    pl.quests.leaveGroup(true);
                    Chat.Aviso(UnturnedPlayer.FromPlayer(pl), dona != null
                        ? $"Voce saiu do grupo: ele agora e a faccao {dona.Tag}. Peca pra entrar em /menu > Faccoes."
                        : "Voce saiu do grupo: a faccao foi dissolvida.");
                }
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
            finally { mudandoGrupo = false; }
        }

        void AoMudarGrupo(PlayerQuests q, CSteamID antes, EPlayerGroupRank ra, CSteamID depois, EPlayerGroupRank rd)
        {
            if (mudandoGrupo || q?.player == null) return;
            var id = q.player.channel.owner.playerID.steamID.m_SteamID;
            var f = FaccaoDe(id);
            if ((f != null && depois.m_SteamID != f.Id) || (f == null && FaccaoPorGrupo(depois.m_SteamID) != null))
            {
                conferirGrupo.Add(id);
                if (f != null) Chat.Aviso(UnturnedPlayer.FromPlayer(q.player), "Pra sair da faccao use /menu > Faccoes ou /faccao sair.");
            }
        }

        void AoEntrarFaccao(UnturnedPlayer p)
        {
            var f = FaccaoDe(p);
            if (f != null)
            {
                var m = f.Membro(Id(p));
                m.Nome = p.CharacterName; m.Visto = Agora; f.UltimoOnline = Agora; Mudou();
                if (f.Pedidos.Count > 0 && TemPerm(f, m.Id, PermFac.Recrutar))
                    Chat.Aviso(p, $"Sua faccao tem {f.Pedidos.Count} pedido(s) pra entrar. Veja em /menu > Faccoes > Gerenciar.");
            }
            else
            {
                var conv = fac.Lista.Where(x => x.Convites.Any(c => c.Id == Id(p))).ToList();
                if (conv.Count > 0) Chat.Aviso(p, $"Voce tem convite de faccao: {string.Join(", ", conv.Select(x => x.Tag))}. Veja em /menu > Faccoes.");
            }
            AplicarGrupo(p.Player);
        }

        void AoSairFaccao(UnturnedPlayer p)
        {
            var f = FaccaoDe(p);
            var m = f?.Membro(Id(p));
            if (m != null) { m.Visto = Agora; f.UltimoOnline = Agora; Mudou(); }
        }

        /// <summary>Proteção offline da base acaba se ninguém da facção entra há N dias.</summary>
        public bool FaccaoAbandonada(ulong grupo)
        {
            var f = FaccaoPorGrupo(grupo);
            return f != null && S.FaccaoDiasProtecao > 0 && f.UltimoOnline > 0 && Agora - f.UltimoOnline > S.FaccaoDiasProtecao * 86400L;
        }

        static IEnumerable<UnturnedPlayer> Online(Faccao f)
        {
            foreach (var m in f.Membros)
            {
                var sp = PlayerTool.getSteamPlayer(m.Id);
                if (sp?.player != null) yield return UnturnedPlayer.FromSteamPlayer(sp);
            }
        }

        public void AvisarFaccao(Faccao f, string texto, PermFac? so = null)
        {
            foreach (var p in Online(f))
                if (so == null || TemPerm(f, Id(p), so.Value)) Chat.Para(p, f.TagCor + " " + texto);
        }

        // ---------- validação ----------

        static readonly string[] Proibidos = { "porra", "caralho", "buceta", "viado", "puta", "merda", "fdp", "pnc", "nazi", "hitler", "admin", "staff" };

        static bool NomeRuim(string s) { var l = s.ToLowerInvariant(); return Proibidos.Any(l.Contains); }

        bool ValidarNome(string nome, Faccao propria, out string erro)
        {
            erro = null;
            nome = (nome ?? "").Trim();
            if (!Regex.IsMatch(nome, @"^[\p{L}0-9 ]{3,20}$")) erro = "O nome precisa ter de 3 a 20 letras ou numeros.";
            else if (NomeRuim(nome)) erro = "Esse nome nao e permitido.";
            else if (fac.Lista.Any(f => f != propria && f.Nome.Equals(nome, StringComparison.OrdinalIgnoreCase))) erro = "Ja existe uma faccao com esse nome.";
            return erro == null;
        }

        bool ValidarTag(string tag, Faccao propria, out string erro)
        {
            erro = null;
            tag = (tag ?? "").Trim().ToUpperInvariant();
            if (!Regex.IsMatch(tag, @"^[A-Z0-9]{2,4}$")) erro = "A TAG precisa ter de 2 a 4 letras ou numeros.";
            else if (NomeRuim(tag)) erro = "Essa TAG nao e permitida.";
            else if (fac.Lista.Any(f => f != propria && f.Tag == tag)) erro = "Ja existe uma faccao com essa TAG.";
            return erro == null;
        }

        static string Descricao(string d) => Chat.Limpar((d ?? "").Trim()).Length > 80 ? Chat.Limpar(d.Trim()).Substring(0, 80) : Chat.Limpar((d ?? "").Trim());

        long EsperaRestante(ulong id)
        {
            var e = fac.Esperas.FirstOrDefault(x => x.Id == id);
            return e == null ? 0 : Math.Max(0, e.Ate - Agora);
        }

        void ComecarEspera(ulong id)
        {
            fac.Esperas.RemoveAll(x => x.Id == id);
            if (S.FaccaoEsperaHoras > 0) fac.Esperas.Add(new EsperaFaccao { Id = id, Ate = Agora + S.FaccaoEsperaHoras * 3600L });
        }

        bool PodeEntrar(ulong id, out string erro)
        {
            erro = null;
            if (FaccaoDe(id) != null) erro = "Voce ja esta em uma faccao.";
            else { var r = EsperaRestante(id); if (r > 0) erro = $"Voce saiu de uma faccao ha pouco. Espere {Chat.Tempo(r)}."; }
            return erro == null;
        }

        static string NomeOnline(ulong id) => PlayerTool.getSteamPlayer(id)?.playerID.characterName;

        // ======================= Ações (menu e comandos usam as mesmas) =======================

        public bool CriarFaccao(UnturnedPlayer p, string nome, string tag, string cor, string desc, int modo, bool doBanco, out string msg)
        {
            var id = Id(p);
            if (!PodeEntrar(id, out msg)) return false;
            if (!ValidarNome(nome, null, out msg) || !ValidarTag(tag, null, out msg)) return false;
            long preco = S.FaccaoNiveis.OrderBy(n => n.Nivel).First().Preco;
            if (preco <= 0) preco = S.FaccaoPreco;
            if (doBanco)
            {
                if (Banco.Saldo(id) < preco) { msg = $"Falta dinheiro no banco: precisa de $ {preco:N0} (voce tem $ {Banco.Saldo(id):N0})."; return false; }
                Banco.Somar(id, -preco);
            }
            else if (!Dinheiro.Pagar(p, preco)) { msg = $"Falta dinheiro no bolso: precisa de $ {preco:N0} (voce tem $ {Dinheiro.NoBolso(p):N0})."; return false; }

            // reaproveita o grupo que o jogador já tinha (a base continua abrindo pra ele)
            ulong grupo = p.Player.quests.groupID.m_SteamID;
            if (grupo == 0 || FaccaoPorGrupo(grupo) != null || fac.Dissolvidas.Any(d => d.Grupo == grupo && d.Dono != id) || grupo == p.Player.channel.owner.playerID.group.m_SteamID)
                grupo = GroupManager.generateUniqueGroupID().m_SteamID;
            var antigos = Provider.clients.Where(c => c.player != null && c.playerID.steamID.m_SteamID != id && c.player.quests.groupID.m_SteamID == grupo).ToList();
            fac.Dissolvidas.RemoveAll(d => d.Grupo == grupo);

            var f = new Faccao
            {
                Id = grupo, Nome = nome.Trim(), Tag = tag.Trim().ToUpperInvariant(), Cor = CoresFaccao.Contains(cor) ? cor : CoresFaccao[0],
                Descricao = Descricao(desc), Modo = Mathf.Clamp(modo, 0, 2), Nivel = 1, Criada = Agora, UltimoOnline = Agora,
            };
            Completar(f);
            f.Membros.Add(new MembroFaccao { Id = id, Nome = p.CharacterName, Cargo = 0, Entrou = Agora, Visto = Agora });
            Registrar(f, $"{Chat.Limpar(p.CharacterName)} criou a faccao (pagou $ {preco:N0})");
            fac.Lista.Add(f);
            facDoJogador[id] = f;
            // quem já estava no grupo e está online entra como Membro (até encher)
            foreach (var c in antigos)
            {
                var cid = c.playerID.steamID.m_SteamID;
                if (FaccaoDe(cid) != null || f.Membros.Count >= NivelDe(f).Vagas) continue;
                f.Membros.Add(new MembroFaccao { Id = cid, Nome = c.playerID.characterName, Cargo = 4, Entrou = Agora, Visto = Agora });
                facDoJogador[cid] = f;
                Chat.Ok(UnturnedPlayer.FromSteamPlayer(c), $"Seu grupo virou a faccao {f.TagCor} {Chat.Limpar(f.Nome)}. Voce entrou como {f.NomeCargo(4)}.");
            }
            foreach (var c in antigos) conferirGrupo.Add(c.playerID.steamID.m_SteamID);
            AplicarGrupo(p.Player);
            SalvarFaccoes();
            Chat.Todos($"{Chat.Limpar(p.CharacterName)} criou a faccao {f.TagCor} {Chat.Cor("#FFFFFF", Chat.Limpar(f.Nome))}!");
            msg = $"Faccao [{f.Tag}] {f.Nome} criada! Voce e o {f.NomeCargo(0)}.";
            return true;
        }

        void Entrar(Faccao f, ulong id, string nome, int cargo = 5)
        {
            f.Membros.Add(new MembroFaccao { Id = id, Nome = nome ?? id.ToString(), Cargo = cargo, Entrou = Agora, Visto = Agora });
            facDoJogador[id] = f;
            f.Pedidos.RemoveAll(x => x.Id == id);
            f.Convites.RemoveAll(x => x.Id == id);
            foreach (var o in fac.Lista) { o.Pedidos.RemoveAll(x => x.Id == id); o.Convites.RemoveAll(x => x.Id == id); }
            Registrar(f, $"{Chat.Limpar(nome)} entrou na faccao");
            conferirGrupo.Add(id);
            Mudou();
            AvisarFaccao(f, Chat.Cor("#3DDC84", $"{Chat.Limpar(nome)} entrou na faccao!"));
        }

        public bool PedirEntrada(UnturnedPlayer p, Faccao f, string texto, out string msg)
        {
            var id = Id(p);
            if (f == null) { msg = "Faccao nao encontrada."; return false; }
            if (!PodeEntrar(id, out msg)) return false;
            if (f.Membros.Count >= NivelDe(f).Vagas) { msg = "Essa faccao esta cheia."; return false; }
            var conv = f.Convites.FirstOrDefault(c => c.Id == id);
            if (f.Modo == 0 || conv != null)
            {
                Entrar(f, id, p.CharacterName);
                msg = $"Voce entrou na faccao [{f.Tag}] {f.Nome} como {f.NomeCargo(5)}.";
                return true;
            }
            if (f.Modo == 2) { msg = "Essa faccao esta fechada: so entra quem for convidado."; return false; }
            if (f.Pedidos.Any(x => x.Id == id)) { msg = "Voce ja pediu pra entrar nessa faccao. Espere a resposta."; return false; }
            if (fac.Lista.Count(x => x.Pedidos.Any(y => y.Id == id)) >= 3) { msg = "Voce ja tem 3 pedidos abertos. Espere alguma resposta."; return false; }
            f.Pedidos.Add(new PedidoFaccao { Id = id, Nome = p.CharacterName, Msg = Descricao(texto), Quando = Agora });
            Mudou();
            AvisarFaccao(f, Chat.Cor("#FFB020", $"{Chat.Limpar(p.CharacterName)} pediu pra entrar na faccao.") + Chat.Cor("#9C9CA6", " /menu > Faccoes > Gerenciar"), PermFac.Recrutar);
            msg = $"Pedido enviado pra [{f.Tag}]. A resposta chega aqui no chat.";
            return true;
        }

        public bool ResponderPedido(UnturnedPlayer p, ulong alvo, bool aceitar, out string msg)
        {
            var f = FaccaoDe(p);
            if (f == null || !TemPerm(f, Id(p), PermFac.Recrutar)) { msg = "Seu cargo nao pode aceitar pedidos."; return false; }
            var ped = f.Pedidos.FirstOrDefault(x => x.Id == alvo);
            if (ped == null) { msg = "Esse pedido nao existe mais."; return false; }
            if (!aceitar)
            {
                f.Pedidos.Remove(ped); Mudou();
                var sp = PlayerTool.getSteamPlayer(alvo);
                if (sp != null) Chat.Aviso(UnturnedPlayer.FromSteamPlayer(sp), $"A faccao [{f.Tag}] recusou seu pedido.");
                msg = $"Pedido de {Chat.Limpar(ped.Nome)} recusado.";
                return true;
            }
            if (!PodeEntrar(alvo, out msg)) { f.Pedidos.Remove(ped); Mudou(); msg = Chat.Limpar(ped.Nome) + ": " + msg.Replace("Voce", "o jogador"); return false; }
            if (f.Membros.Count >= NivelDe(f).Vagas) { msg = "A faccao esta cheia. Suba o nivel pra ter mais vagas."; return false; }
            Entrar(f, alvo, NomeOnline(alvo) ?? ped.Nome);
            Registrar(f, $"{Chat.Limpar(p.CharacterName)} aceitou {Chat.Limpar(ped.Nome)}");
            var s2 = PlayerTool.getSteamPlayer(alvo);
            if (s2 != null) Chat.Ok(UnturnedPlayer.FromSteamPlayer(s2), $"Voce foi aceito na faccao [{f.Tag}] {Chat.Limpar(f.Nome)}!");
            msg = $"{Chat.Limpar(ped.Nome)} entrou na faccao.";
            return true;
        }

        public bool Convidar(UnturnedPlayer p, string nome, out string msg)
        {
            var f = FaccaoDe(p);
            if (f == null || !TemPerm(f, Id(p), PermFac.Recrutar)) { msg = "Seu cargo nao pode convidar."; return false; }
            var alvo = UnturnedPlayer.FromName(nome ?? "");
            if (alvo?.Player == null) { msg = "Jogador nao encontrado (ele precisa estar online)."; return false; }
            var aid = Id(alvo);
            if (!PodeEntrar(aid, out msg)) { msg = Chat.Limpar(alvo.CharacterName) + ": " + msg.Replace("Voce", "o jogador"); return false; }
            if (f.Membros.Count >= NivelDe(f).Vagas) { msg = "A faccao esta cheia."; return false; }
            f.Convites.RemoveAll(x => x.Id == aid);
            f.Convites.Add(new PedidoFaccao { Id = aid, Nome = alvo.CharacterName, Quando = Agora });
            Mudou();
            Chat.Aviso(alvo, $"Voce foi convidado pra faccao {f.TagCor} {Chat.Limpar(f.Nome)}. Aceite em /menu > Faccoes > Convites ou /faccao entrar {f.Tag}.");
            msg = $"Convite enviado pra {Chat.Limpar(alvo.CharacterName)}.";
            return true;
        }

        public bool RecusarConvite(UnturnedPlayer p, Faccao f, out string msg)
        {
            if (f == null || f.Convites.RemoveAll(x => x.Id == Id(p)) == 0) { msg = "Esse convite nao existe mais."; return false; }
            Mudou();
            msg = $"Convite de [{f.Tag}] recusado.";
            return true;
        }

        bool AcimaDe(Faccao f, ulong quem, ulong alvo, out MembroFaccao a, out MembroFaccao b, out string msg)
        {
            a = f?.Membro(quem); b = f?.Membro(alvo); msg = null;
            if (a == null || b == null) msg = "Esse jogador nao e da sua faccao.";
            else if (a.Id == b.Id) msg = "Nao da pra fazer isso com voce mesmo.";
            else if (a.Cargo >= b.Cargo) msg = "So da pra mexer com quem tem cargo abaixo do seu.";
            return msg == null;
        }

        void TirarDaFaccao(Faccao f, MembroFaccao m)
        {
            f.Membros.Remove(m);
            facDoJogador.Remove(m.Id);
            ComecarEspera(m.Id);
            if (BauFac.Aberto(m.Id)) { var sp = PlayerTool.getSteamPlayer(m.Id); if (sp != null) BauFac.Fechar(UnturnedPlayer.FromSteamPlayer(sp)); }
            conferirGrupo.Add(m.Id);
            Mudou();
        }

        public bool Expulsar(UnturnedPlayer p, ulong alvo, out string msg)
        {
            var f = FaccaoDe(p);
            if (f == null || !TemPerm(f, Id(p), PermFac.Expulsar)) { msg = "Seu cargo nao pode expulsar."; return false; }
            if (!AcimaDe(f, Id(p), alvo, out _, out var b, out msg)) return false;
            TirarDaFaccao(f, b);
            Registrar(f, $"{Chat.Limpar(p.CharacterName)} expulsou {Chat.Limpar(b.Nome)}");
            var sp = PlayerTool.getSteamPlayer(alvo);
            if (sp != null) Chat.Erro(UnturnedPlayer.FromSteamPlayer(sp), $"Voce foi expulso da faccao [{f.Tag}].");
            AvisarFaccao(f, Chat.Cor("#FF6060", $"{Chat.Limpar(b.Nome)} foi expulso por {Chat.Limpar(p.CharacterName)}."));
            msg = $"{Chat.Limpar(b.Nome)} foi expulso.";
            return true;
        }

        public bool MudarCargo(UnturnedPlayer p, ulong alvo, int delta, out string msg)
        {
            var f = FaccaoDe(p);
            if (f == null || !TemPerm(f, Id(p), PermFac.Promover)) { msg = "Seu cargo nao pode promover nem rebaixar."; return false; }
            if (!AcimaDe(f, Id(p), alvo, out var a, out var b, out msg)) return false;
            int novo = b.Cargo + delta;
            if (novo > 5) { msg = $"{Chat.Limpar(b.Nome)} ja esta no cargo mais baixo."; return false; }
            if (novo <= a.Cargo) { msg = "So da pra promover ate o cargo abaixo do seu. Pra passar a lideranca use o botao LIDERANCA."; return false; }
            b.Cargo = novo;
            Mudou();
            var txt = $"{Chat.Limpar(b.Nome)} agora e {f.NomeCargo(novo)}";
            Registrar(f, $"{Chat.Limpar(p.CharacterName)}: {txt}");
            AvisarFaccao(f, Chat.Cor("#FFB020", txt + "."));
            msg = txt + ".";
            return true;
        }

        public bool PassarLideranca(UnturnedPlayer p, ulong alvo, out string msg)
        {
            var f = FaccaoDe(p);
            var a = f?.Membro(Id(p));
            var b = f?.Membro(alvo);
            if (a == null || a.Cargo != 0) { msg = "So o lider pode passar a lideranca."; return false; }
            if (b == null || b.Id == a.Id) { msg = "Escolha alguem da faccao."; return false; }
            b.Cargo = 0; a.Cargo = 1;
            Mudou();
            Registrar(f, $"{Chat.Limpar(a.Nome)} passou a lideranca pra {Chat.Limpar(b.Nome)}");
            AvisarFaccao(f, Chat.Cor("#FFB020", $"{Chat.Limpar(b.Nome)} e o novo {f.NomeCargo(0)}!"));
            msg = $"Agora {Chat.Limpar(b.Nome)} e o lider. Voce virou {f.NomeCargo(1)}.";
            return true;
        }

        public bool SairDaFaccao(UnturnedPlayer p, out string msg)
        {
            var f = FaccaoDe(p);
            var m = f?.Membro(Id(p));
            if (m == null) { msg = "Voce nao esta em nenhuma faccao."; return false; }
            if (m.Cargo == 0) { msg = f.Membros.Count > 1 ? "O lider nao pode sair: passe a lideranca pra alguem ou dissolva a faccao." : "Voce e o unico membro: use DISSOLVER pra fechar a faccao."; return false; }
            TirarDaFaccao(f, m);
            Registrar(f, $"{Chat.Limpar(m.Nome)} saiu da faccao");
            AvisarFaccao(f, Chat.Cor("#FF6060", $"{Chat.Limpar(m.Nome)} saiu da faccao."));
            msg = $"Voce saiu da faccao [{f.Tag}]." + (S.FaccaoEsperaHoras > 0 ? $" So pode entrar em outra daqui a {S.FaccaoEsperaHoras}h." : "");
            return true;
        }

        /// <summary>Dissolve: o líder fica com o dinheiro do cofre e com a base (continua no grupo); os outros perdem o acesso.</summary>
        public bool Dissolver(UnturnedPlayer p, out string msg)
        {
            var f = FaccaoDe(p);
            var lider = f?.Membro(Id(p));
            if (lider == null || lider.Cargo != 0) { msg = "So o lider pode dissolver a faccao."; return false; }
            if (BauFac.EmUso(f.Id)) { msg = "Alguem esta com o bau da faccao aberto. Espere fechar."; return false; }
            if (BauFac.Contar(f.Id) > 0) { msg = "Esvazie o bau da faccao antes de dissolver (as coisas sao suas, e so pegar)."; return false; }
            long cofre = f.Cofre;
            FecharFaccao(f, lider, "pelo lider");
            msg = "Faccao dissolvida. A base continua sua" + (cofre > 0 ? $" e os $ {cofre:N0} do cofre foram pro seu banco." : ".");
            return true;
        }

        /// <summary>Dissolver pelo painel do site (só o dono do servidor). O que estiver no baú fica guardado em faccoes_bau/.</summary>
        public string DissolverPelaStaff(ulong liderId)
        {
            var f = FaccaoDe(liderId);
            var lider = f?.Membro(liderId);
            if (lider == null || lider.Cargo != 0) return "esse jogador nao e lider de nenhuma faccao";
            foreach (var m in f.Membros)
            {
                var sp = PlayerTool.getSteamPlayer(m.Id);
                if (sp != null && BauFac.Aberto(m.Id)) BauFac.Fechar(UnturnedPlayer.FromSteamPlayer(sp));
            }
            try
            {
                var arq = Path.Combine(Directory, "faccoes_bau", f.Id + ".txt");
                if (File.Exists(arq)) File.Move(arq, Path.Combine(Directory, "faccoes_bau", $"{f.Id}_dissolvida_{Agora}.txt"));
            }
            catch { }
            FecharFaccao(f, lider, "pela staff");
            return $"faccao [{f.Tag}] {f.Nome} dissolvida";
        }

        void FecharFaccao(Faccao f, MembroFaccao lider, string por)
        {
            if (f.Cofre > 0) Banco.Somar(lider.Id, f.Cofre);
            foreach (var m in f.Membros.ToList())
            {
                facDoJogador.Remove(m.Id);
                if (m.Id != lider.Id) { ComecarEspera(m.Id); conferirGrupo.Add(m.Id); }
                var sp = PlayerTool.getSteamPlayer(m.Id);
                if (sp != null && m.Id != lider.Id) Chat.Erro(UnturnedPlayer.FromSteamPlayer(sp), $"A faccao [{f.Tag}] foi dissolvida {por}.");
            }
            fac.Lista.Remove(f);
            fac.Dissolvidas.RemoveAll(d => d.Grupo == f.Id);
            fac.Dissolvidas.Add(new GrupoAntigo { Grupo = f.Id, Dono = lider.Id });
            SalvarFaccoes();
            Chat.Todos($"A faccao {f.TagCor} {Chat.Limpar(f.Nome)} foi dissolvida.");
        }

        public bool MexerCofre(UnturnedPlayer p, long valor, bool depositar, out string msg)
        {
            var f = FaccaoDe(p);
            var id = Id(p);
            if (f == null) { msg = "Voce nao esta em nenhuma faccao."; return false; }
            if (valor <= 0) { msg = "Digite um valor maior que zero."; return false; }
            if (depositar)
            {
                if (!TemPerm(f, id, PermFac.Depositar)) { msg = "Seu cargo nao pode depositar no cofre."; return false; }
                if (Banco.Saldo(id) < valor) { msg = $"Seu banco so tem $ {Banco.Saldo(id):N0}."; return false; }
                Banco.Somar(id, -valor); f.Cofre += valor;
                Registrar(f, $"{Chat.Limpar(p.CharacterName)} depositou $ {valor:N0}");
                msg = $"Depositou $ {valor:N0} no cofre da faccao.";
            }
            else
            {
                if (!TemPerm(f, id, PermFac.Sacar)) { msg = "Seu cargo nao pode sacar do cofre."; return false; }
                if (f.Cofre < valor) { msg = $"O cofre so tem $ {f.Cofre:N0}."; return false; }
                f.Cofre -= valor; Banco.Somar(id, valor);
                Registrar(f, $"{Chat.Limpar(p.CharacterName)} sacou $ {valor:N0}");
                msg = $"Sacou $ {valor:N0} do cofre pro seu banco.";
            }
            SalvarFaccoes();
            return true;
        }

        public bool SubirNivel(UnturnedPlayer p, out string msg)
        {
            var f = FaccaoDe(p);
            if (f?.Membro(Id(p))?.Cargo != 0) { msg = "So o lider sobe o nivel da faccao."; return false; }
            var prox = ProximoNivel(f);
            if (prox == null) { msg = "A faccao ja esta no nivel maximo."; return false; }
            if (f.Cofre < prox.Preco) { msg = $"O cofre precisa de $ {prox.Preco:N0} (tem $ {f.Cofre:N0}). Deposite no cofre primeiro."; return false; }
            f.Cofre -= prox.Preco; f.Nivel = prox.Nivel;
            Registrar(f, $"{Chat.Limpar(p.CharacterName)} subiu a faccao pro nivel {prox.Nivel} ($ {prox.Preco:N0})");
            SalvarFaccoes();
            AvisarFaccao(f, Chat.Cor("#3DDC84", $"A faccao subiu pro nivel {prox.Nivel}! Agora sao {prox.Vagas} vagas e bau {prox.BauLargura}x{prox.BauAltura}."));
            msg = $"Nivel {prox.Nivel}!";
            return true;
        }

        bool SoLider(UnturnedPlayer p, out Faccao f, out string msg)
        {
            f = FaccaoDe(p); msg = null;
            if (f?.Membro(Id(p))?.Cargo != 0) msg = "So o lider pode mudar isso.";
            return msg == null;
        }

        public bool AlternarPermissao(UnturnedPlayer p, int cargo, int perm, out string msg)
        {
            if (!SoLider(p, out var f, out msg)) return false;
            if (cargo < 1 || cargo > 5 || perm < 0 || perm >= PermsGrade.Length) { msg = "O lider sempre pode tudo."; return false; }
            f.Permissoes[cargo] ^= (int)PermsGrade[perm];
            bool liga = (f.Permissoes[cargo] & (int)PermsGrade[perm]) != 0;
            Registrar(f, $"{f.NomeCargo(cargo)}: {NomesPerms[perm]} {(liga ? "liberado" : "bloqueado")}");
            Mudou();
            msg = $"{f.NomeCargo(cargo)}: {NomesPerms[perm]} {(liga ? "LIBERADO" : "BLOQUEADO")}.";
            return true;
        }

        public bool RenomearCargo(UnturnedPlayer p, int cargo, string nome, out string msg)
        {
            if (!SoLider(p, out var f, out msg)) return false;
            nome = (nome ?? "").Trim();
            if (cargo < 0 || cargo > 5) { msg = "Cargo invalido."; return false; }
            if (nome.Length == 0) nome = CargosPadrao[cargo];
            if (!Regex.IsMatch(nome, @"^[\p{L}0-9 \-]{2,14}$") || NomeRuim(nome)) { msg = "Nome de cargo: de 2 a 14 letras."; return false; }
            f.Cargos[cargo] = nome; Mudou();
            msg = $"Cargo renomeado pra {nome}.";
            return true;
        }

        public bool MudarModo(UnturnedPlayer p, out string msg)
        {
            if (!SoLider(p, out var f, out msg)) return false;
            f.Modo = (f.Modo + 1) % 3; Mudou();
            msg = "Recrutamento: " + NomesModo[f.Modo];
            return true;
        }

        public bool MudarCor(UnturnedPlayer p, out string msg)
        {
            if (!SoLider(p, out var f, out msg)) return false;
            f.Cor = CoresFaccao[(Array.IndexOf(CoresFaccao, f.Cor) + 1) % CoresFaccao.Length]; Mudou();
            msg = "Cor da TAG trocada.";
            return true;
        }

        public bool MudarDescricao(UnturnedPlayer p, string d, out string msg)
        {
            if (!SoLider(p, out var f, out msg)) return false;
            f.Descricao = Descricao(d); Mudou();
            msg = "Descricao salva.";
            return true;
        }

        public bool MudarTag(UnturnedPlayer p, string tag, out string msg)
        {
            if (!SoLider(p, out var f, out msg)) return false;
            if (!ValidarTag(tag, f, out msg)) return false;
            f.Tag = tag.Trim().ToUpperInvariant();
            var g = GroupManager.getGroupInfo(new CSteamID(f.Id));
            if (g != null) { g.name = $"[{f.Tag}] {f.Nome}"; GroupManager.sendGroupInfo(g); }
            Registrar(f, $"TAG trocada pra [{f.Tag}]"); Mudou();
            msg = $"TAG agora e [{f.Tag}].";
            return true;
        }

        public bool AbrirBauFaccao(UnturnedPlayer p, out string msg)
        {
            var f = FaccaoDe(p);
            if (f == null) { msg = "Voce nao esta em nenhuma faccao."; return false; }
            if (!TemPerm(f, Id(p), PermFac.Baus)) { msg = "Seu cargo nao abre o bau da faccao."; return false; }
            if (S.FaccaoBauSoNaSafe && ZonaSeguraEm(p.Position) == null) { msg = "O bau da faccao so abre dentro da safe zone."; return false; }
            if (EmCombate(p, out var resta)) { msg = $"Voce esta em combate. Espere {resta}s."; return false; }
            if (BauFac.EmUso(f.Id) && !BauFac.Aberto(Id(p))) { msg = "Outro membro esta usando o bau agora. Tente daqui a pouco."; return false; }
            var n = NivelDe(f);
            if (MenuAberto(p)) FecharMenu(p);
            BauFac.Abrir(p, new TamanhoVault { Nome = "Faccao", Largura = n.BauLargura, Altura = n.BauAltura }, f.Id);
            msg = "Bau da faccao aberto.";
            return true;
        }

        public void ChatFaccao(UnturnedPlayer p, string texto)
        {
            var f = FaccaoDe(p);
            if (f == null) { Chat.Erro(p, "Voce nao esta em nenhuma faccao."); return; }
            if (string.IsNullOrWhiteSpace(texto)) { Chat.Erro(p, "Use: /f <mensagem>"); return; }
            var m = f.Membro(Id(p));
            var linha = $"<color={f.Cor}><b>[{f.Tag}]</b></color> <color=#9C9CA6>{Chat.Limpar(f.NomeCargo(m.Cargo))}</color> <color=#FFFFFF>{Chat.Limpar(p.CharacterName)}</color><color=#9C9CA6>:</color> <color=#BDF5CF>{Chat.Limpar(texto)}</color>";
            foreach (var o in Online(f))
                ChatManager.serverSendMessage(linha, Color.white, p.Player.channel.owner, o.Player.channel.owner, EChatMode.SAY, null, true);
        }

        // ======================= Permissões na base =======================

        static float avisoPerm;
        static void AvisoSemPerm(ulong id, string txt)
        {
            if (Time.realtimeSinceStartup < avisoPerm) return;
            avisoPerm = Time.realtimeSinceStartup + 1.5f;
            PlayerTool.getPlayer(new CSteamID(id))?.ServerShowHint("<color=#FF6060><b>SEM PERMISSAO</b></color>\n" + txt, 3f);
        }

        /// <summary>Usado pelo patch da porta: false = não deixa abrir.</summary>
        public bool PodeUsarPorta(Player pl, InteractableDoor porta)
        {
            if (pl == null || porta == null) return true;
            var id = pl.channel.owner.playerID.steamID.m_SteamID;
            if (porta.owner.m_SteamID == id) return true;
            var f = FaccaoPorGrupo(porta.group.m_SteamID);
            if (f == null || f.Membro(id) == null || TemPerm(f, id, PermFac.Portas)) return true;
            var m = f.Membro(id);
            AvisoSemPerm(id, $"{f.NomeCargo(m.Cargo)} nao abre portas da faccao.");
            return false;
        }

        void AoAbrirBauFac(CSteamID quem, InteractableStorage bau, ref bool permitir)
        {
            if (!permitir || bau == null) return;
            var id = quem.m_SteamID;
            if (bau.owner.m_SteamID == id) return;
            var f = FaccaoPorGrupo(bau.group.m_SteamID);
            if (f == null || f.Membro(id) == null || TemPerm(f, id, PermFac.Baus)) return;
            permitir = false;
            AvisoSemPerm(id, $"{f.NomeCargo(f.Membro(id).Cargo)} nao abre baus da faccao.");
        }

        bool BloquearDesmontar(ulong id, ulong dono, ulong grupo)
        {
            if (dono == id) return false;
            var f = FaccaoPorGrupo(grupo);
            if (f == null || f.Membro(id) == null || TemPerm(f, id, PermFac.Desmontar)) return false;
            AvisoSemPerm(id, $"{f.NomeCargo(f.Membro(id).Cargo)} nao desmonta pecas da faccao.");
            return true;
        }

        void AoDesmontarBarricadaFac(CSteamID quem, byte x, byte y, ushort plant, ushort index, ref bool permitir)
        {
            if (!permitir) return;
            try
            {
                if (!BarricadeManager.tryGetRegion(x, y, plant, out var reg) || index >= reg.drops.Count) return;
                var sd = reg.drops[index].GetServersideData();
                if (sd != null && BloquearDesmontar(quem.m_SteamID, sd.owner, sd.group)) permitir = false;
            }
            catch { }
        }

        void AoDesmontarEstruturaFac(CSteamID quem, byte x, byte y, ushort index, ref bool permitir)
        {
            if (!permitir) return;
            try
            {
                if (!StructureManager.tryGetRegion(x, y, out var reg) || index >= reg.drops.Count) return;
                var sd = reg.drops[index].GetServersideData();
                if (sd != null && BloquearDesmontar(quem.m_SteamID, sd.owner, sd.group)) permitir = false;
            }
            catch { }
        }

        /// <summary>Sem permissão de construir: não coloca peça perto da Claim Flag da facção.</summary>
        bool BloquearConstruir(ulong dono, ulong grupo, Vector3 ponto)
        {
            var f = FaccaoPorGrupo(grupo);
            if (f == null || f.Membro(dono) == null || TemPerm(f, dono, PermFac.Construir)) return false;
            float r2 = S.RaidRaioClaim * S.RaidRaioClaim;
            if (!claims.Any(c => c.grupo == f.Id && (c.pos - ponto).sqrMagnitude <= r2)) return false;
            AvisoSemPerm(dono, $"{f.NomeCargo(f.Membro(dono).Cargo)} nao constroi na base da faccao.");
            return true;
        }

        void AoColocarBarricadaFac(Barricade b, ItemBarricadeAsset a, Transform hit, ref Vector3 ponto,
            ref float ax, ref float ay, ref float az, ref ulong dono, ref ulong grupo, ref bool permitir)
        {
            if (permitir && BloquearConstruir(dono, grupo, ponto)) permitir = false;
        }

        void AoColocarEstruturaFac(Structure s, ItemStructureAsset a, ref Vector3 ponto,
            ref float ax, ref float ay, ref float az, ref ulong dono, ref ulong grupo, ref bool permitir)
        {
            if (permitir && BloquearConstruir(dono, grupo, ponto)) permitir = false;
        }

        void AoEntrarVeiculoFac(Player pl, InteractableVehicle v, ref bool permitir)
        {
            if (!permitir || pl == null || v == null || !v.isLocked) return;
            var id = pl.channel.owner.playerID.steamID.m_SteamID;
            if (v.lockedOwner.m_SteamID == id) return;
            var f = FaccaoPorGrupo(v.lockedGroup.m_SteamID);
            if (f == null || f.Membro(id) == null || TemPerm(f, id, PermFac.Veiculos)) return;
            permitir = false;
            AvisoSemPerm(id, $"{f.NomeCargo(f.Membro(id).Cargo)} nao usa veiculos trancados da faccao.");
        }
    }
}
