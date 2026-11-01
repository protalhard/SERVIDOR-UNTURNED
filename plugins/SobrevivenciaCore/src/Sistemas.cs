using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace SobrevivenciaCore
{
    public class Patente
    {
        [XmlAttribute] public string Nome;
        [XmlAttribute] public long XpMinimo;
        [XmlAttribute] public string Cor;
        public Patente() { }
        public Patente(string nome, long xp, string cor) { Nome = nome; XpMinimo = xp; Cor = cor; }
    }

    /// <summary>Kit. Espera em segundos. Abates = abates necessários pra liberar.
    /// Permissao vazia = todo mundo; senão só quem tem a permissão do Rocket (ex.: kit.vip).</summary>
    public class Kit
    {
        [XmlAttribute] public string Nome;
        [XmlAttribute] public string Descricao;
        [XmlAttribute] public int Espera;
        [XmlAttribute] public int Abates;
        [XmlAttribute] public string Permissao = "";
        [XmlAttribute] public int BonusXp;
        [XmlAttribute] public string Titulo = "";
        public List<ItemKit> Itens;
    }

    public class PacoteXp
    {
        [XmlAttribute] public string Nome;
        [XmlAttribute] public string Descricao;
        [XmlAttribute] public long Preco;
        public List<ItemKit> Itens;
    }

    /// <summary>Conteúdo de airdrop que o admin escolhe no menu (Comida, Armas, ...).</summary>
    public class KitAirdrop
    {
        [XmlAttribute] public string Nome;
        public List<ItemKit> Itens;
    }

    public class TamanhoVault
    {
        [XmlAttribute] public string Nome;
        [XmlAttribute] public string Permissao = "";
        [XmlAttribute] public byte Largura;
        [XmlAttribute] public byte Altura;
        // Preço pra comprar com dinheiro do jogo no NPC Vault (0 = só por permissão/VIP)
        [XmlAttribute] public int Preco;
    }

    /// <summary>Configuração dos sistemas estilo DayZ (warp, combate, XP, kits, vault...).</summary>
    public class Sistemas
    {
        // Visual (cores SnapLag)
        public string CorPrincipal = "#FF5A00";
        public string CorDestaque = "#FFB020";
        public string CorTexto = "#F5F6F7";
        public string CorApagada = "#8A9099";
        public string CorOk = "#5BE37D";
        public string CorPerigo = "#FF4D4D";
        public string Prefixo = "DZ";
        public string IconeChat = "https://raw.githubusercontent.com/protalhard/SERVIDOR-UNTURNED/main/branding/icon64.png";
        public string LinkDiscord = "";

        /// <summary>LinkDiscord da config ou, se vazio, o convite que o bot do Discord criou.</summary>
        [XmlIgnore]
        public string Discord
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(LinkDiscord)) return LinkDiscord.Trim();
                try
                {
                    var arq = "/home/unturned/dz-bot/convite.txt";
                    if (!System.IO.File.Exists(arq)) return "";
                    var l = System.IO.File.ReadAllText(arq).Trim();
                    return l.StartsWith("https://discord.gg/") ? l : "";
                }
                catch { return ""; }
            }
        }
        public string LinkSite = "https://snaplag.com.br/";
        // Site do servidor (ranking, perfil). Usado pelos botões do menu.
        public string LinkSiteServidor = "https://dayz.snaplag.com.br";
        // Donos: Steam IDs separados por vírgula. Veem a aba ADM inteira e montam a staff.
        public string Donos = "76561198168015168";

        // Teleportes
        public int WarpEspera = 15;
        public int WarpIntervalo = 30;
        /// <summary>Warp pra safe só vale até X segundos depois de renascer (morreu). 0 = livre sempre.</summary>
        public int WarpSafeAposMorrer = 60;
        // segundos pra escolher a safe depois de entrar ou renascer
        public int EscolherSafeSegundos = 120;
        // mochila que o /itens dá pro dono (Mercenary Alicepack)
        public ushort MochilaItens = 6057;
        public int HomeEspera = 10;
        public int HomeIntervalo = 120;
        public int TpaEspera = 10;
        public int TpaIntervalo = 60;
        public int TpaExpira = 30;

        // Combate e limpeza
        public int CombateSegundos = 120;
        // Quem sai do jogo em combate morre e deixa as coisas no chão
        public bool PunirDeslogarEmCombate = true;
        public int LimpezaMinutos = 30;
        public bool MostrarTodasMortes = true;

        // XP
        public int XpAbate = 100;
        public int XpCabeca = 50;
        public int XpZumbi = 5;
        public int XpMega = 50;
        public int XpPorMinuto = 5;
        // XP de boas-vindas pra gastar na /lojaxp (não conta pra patente)
        public int XpInicial = 1500;
        // Marca no mapa o lugar onde o jogador morreu
        public bool MarcarMorteNoMapa = true;
        // Homes com nome (cama do lado). VIP = permissão home.vip
        public int HomesMax = 2;
        public int HomesVip = 5;
        // Mostra [Patente] Lv.X antes do nome no chat global
        public bool PatenteNoChat = true;

        // Anúncios
        public int AnuncioSegundos = 300;
        public bool VaultSoNaSafe = true;
        public bool SafeSemConstrucao = true;
        public bool SafeProtegeBases = true;
        public bool SafeProtegeVeiculos = true;
        public bool LojaXpSoNaSafe = true;
        // ATM só funciona dentro da safe zone (no mercado negro e no caçador não tem)
        public bool AtmSoNaSafe = true;
        // segundos que o aviso grande de entrada na safe fica na tela
        public float SafeAvisoSegundos = 8f;
        /// <summary>Depois de sair da safe, continua protegido por X segundos (contagem na tela). Voltou pra dentro, cancela.</summary>
        public int SafeSaidaSegundos = 10;
        // foto dos itens na loja ({id} = ID do item). Os PNGs ficam na pasta icones do site do servidor.
        public string IconeUrl = "http://54.94.93.87/icones/{id}.png";
        public string IconeVeiculoUrl = "http://54.94.93.87/icones/v{id}.png";
        // Raid de base: só no horário (Brasília). Dias: "todos" ou "sex,sab,dom"...
        public bool RaidHorarioLigado = true;
        public int RaidInicioHora = 18;
        public int RaidFimHora = 23;
        public string RaidDias = "todos";
        // proteção offline (igual Zumbas): base com Claim Flag protegida com o grupo offline há X min
        public bool RaidProtecaoOffline = true;
        public int RaidMinutosOffline = 10;
        public int RaidDiasRenovarClaim = 3;
        // votação no jogo (painel do site): repete o aviso no chat a cada X minutos
        public int VotacaoAvisoMinutos = 10;
        public float RaidRaioClaim = 32f;
        public bool RaidAdminIgnora = true;
        // /montar (antigo, sem uso desde os modelos)
        public ushort MontarCercaId = 7121;
        public float MontarCercaEspaco = 3.5f;
        public float MontarEntrada = 6f;
        // /montar <safe|negro|cacador> <1-3>: rotação X das barricadas (modelo do Unturned vem deitado: -90)
        public float MontarRotacaoX = -90f;
        // mesma coisa pros NPCs DZ (feitos no Unity, frente +Z)
        public float MontarRotacaoXNpc = -90f;
        // giro extra (graus) das peças de cerca/parede pra ficarem ao longo da linha
        public float MontarGiroParede = 0f;
        public float MontarEspacoCerca = 3f;
        public float MontarEspacoParede = 2f;
        // /montar: diferença máxima (m) entre o ponto mais alto e o mais baixo do chão do modelo. Acima disso pede "forcar".
        public float MontarDesnivelMax = 10f;
        // peça=id ou peça@area=id (area: safe, negro, cacador). id 0 = não coloca.
        public List<string> MontarPecas;

        public List<Patente> Patentes;
        public List<Kit> Kits;
        public List<PacoteXp> LojaXp;
        public List<TamanhoVault> Vaults;
        // Facções: preço pra criar, espera pra trocar de facção, dias sem ninguém entrar até perder a proteção offline
        public int FaccaoPreco = 5000;
        public int FaccaoEsperaHoras = 24;
        public int FaccaoDiasProtecao = 15;
        public bool FaccaoBauSoNaSafe = true;
        public List<NivelFaccao> FaccaoNiveis;
        public List<KitAirdrop> AirdropKits;
        public List<string> Anuncios;
        public List<string> Tutorial;
        // Versão dos padrões já aplicada (pra adicionar kits/pacotes novos em config antiga)
        public int VersaoPadroes = 0;

        // DZ GUARD (anti-cheat do servidor)
        /// <summary>Reinício automático todo dia (horário de Brasília, HH:mm; vazio = desligado). Avisa 15 min antes.</summary>
        public string ReinicioDiario = "07:00";
        /// <summary>Máximo de helis/aviões soltos (sem dono) no mapa ao mesmo tempo. 0 = sem limite.</summary>
        public int LimiteAeronavesSoltas = 3;
        public bool GuardLigado = true;
        public bool GuardBanAutomatico = true;   // caso claro (speed/voo absurdo) bane sozinho
        public double GuardBanHoras = 0;          // 0 = permanente
        public float GuardVelocidadeMax = 11f;    // m/s a pé (correndo normal fica perto de 7)
        public float GuardTeleporteMetros = 60f;  // pulo de posição em 0,5 s
        public int GuardPontosAlerta = 60;        // avisa a staff a partir disso
        public int ReportIntervaloSegundos = 180;

        public static List<string> MontarPecasPadrao() => new List<string>
        {
            "cerca@safe=7122", "cerca@negro=7121", "cerca@cacador=7120",
            "parede@safe=6204", "parede@negro=6196", "parede@cacador=6201",
            "balcao=6148", "balcao@cacador=4446",
            "rack=4459", "prateleira=4968", "manequim=4575", "mesa=4885", "mesa@cacador=4463",
            "cofre=6957", "caixa=4449", "caixa_grande=6956", "barril=4587",
            "luz=7130", "fogueira=7130", "torre=6956", "barraca=0", "banco_praca=4893",
            "bandeira=51253", "bandeira_placa=4996",
            "hangar=51256", "heliponto=51255", "pista=51257",
            "terminal=51258", "vaga=51259", "placa=51260",
            // v11: enfeites da oficina, estacionamento e heliponto (pack DZ)
            "elevador=51261", "macaco=51262", "bancada=51263", "pneus=51264", "galao=51265", "bomba=51266|6173", "cone=51267", "biruta=51268", "luz_pouso=51269", "caixa_ferramentas=51270",
                    // v12: enfeites de cada cantinho (pack DZ)
            "rack_armas=51271", "prateleira_comida=51272", "manequim_dz=51273", "arara_roupas=51274", "armario_remedios=51275", "maca=51276", "prateleira_equip=51277", "cofre_dz=51278", "caixas_militares=51279", "varal_caca=51280", "caixa_tnt=51281", "mesa_dinheiro=51282", "materiais=51283", "pecas_aviao=51284", "balcao_dz=51285", "alvo=51286", "sacos_areia=51287", "barril_toxico=51288", "espelho=51289", "soro=51290", "trofeu=51291", "geladeira=51292", "fogao=51293", "caixa_frutas=51294",
        };

        /// <summary>Preenche o que estiver faltando (config antiga). Devolve true se mudou algo.</summary>
        public bool Completar()
        {
            bool mudou = false;
            if (Patentes == null || Patentes.Count == 0) { Patentes = PatentesPadrao(); mudou = true; }
            if (Kits == null) { Kits = KitsPadrao(); mudou = true; }
            if (LojaXp == null) { LojaXp = LojaXpPadrao(); mudou = true; }
            if (Vaults == null || Vaults.Count == 0) { Vaults = VaultsPadrao(); mudou = true; }
            if (Anuncios == null) { Anuncios = AnunciosPadrao(); mudou = true; }
            if (AirdropKits == null || AirdropKits.Count == 0) { AirdropKits = AirdropPadrao(); mudou = true; }
            if (MontarPecas == null || MontarPecas.Count == 0) { MontarPecas = MontarPecasPadrao(); mudou = true; }
            if (Tutorial == null) { Tutorial = TutorialPadrao(); mudou = true; }
            if (FaccaoNiveis == null || FaccaoNiveis.Count == 0) { FaccaoNiveis = NivelFaccao.Padrao(); mudou = true; }
            // padrões novos (v2, v3 = kits do California 2): adiciona kits e pacotes que ainda não existem, sem mexer nos que o admin editou
            if (VersaoPadroes < 3)
            {
                foreach (var k in KitsPadrao())
                    if (!Kits.Exists(x => x.Nome.Equals(k.Nome, StringComparison.OrdinalIgnoreCase))) Kits.Add(k);
                foreach (var pc in LojaXpPadrao())
                    if (!LojaXp.Exists(x => x.Nome.Equals(pc.Nome, StringComparison.OrdinalIgnoreCase))) LojaXp.Add(pc);
                VersaoPadroes = 3;
                mudou = true;
            }
            // v5: vaults maiores podem ser comprados com dinheiro do jogo
            if (VersaoPadroes < 5)
            {
                foreach (var v in Vaults)
                    if (v.Preco == 0 && v.Largura * v.Altura > 25) v.Preco = v.Largura * v.Altura >= 150 ? 40000 : v.Largura * v.Altura >= 90 ? 15000 : 5000;
            }
            // v4: combate de 2 minutos (era 20s)
            if (VersaoPadroes < 4)
            {
                if (CombateSegundos == 20) CombateSegundos = 120;
                VersaoPadroes = 4;
            }
            if (VersaoPadroes < 5)
            {
                VersaoPadroes = 5;
                mudou = true;
            }
            // v6: cerca do /montar era 7200 (estrutura, nao barricada): passa pra Metal Fence 7121
            if (VersaoPadroes < 6)
            {
                if (MontarCercaId == 7200) MontarCercaId = 7121;
                VersaoPadroes = 6;
                mudou = true;
            }
            // v7: manequim do California 2 (4575)
            if (VersaoPadroes < 7)
            {
                if (MontarPecas != null) MontarPecas = MontarPecas.ConvertAll(x => x.Replace(" ", "") == "manequim=0" ? "manequim=4575" : x);
                VersaoPadroes = 7;
                mudou = true;
            }
            // v8: hangar, heliponto e pista (peças DZ)
            if (VersaoPadroes < 8)
            {
                if (MontarPecas != null)
                    foreach (var x in new[] { "hangar=51256", "heliponto=51255", "pista=51257", "terminal=51258", "vaga=51259", "placa=51260" })
                        if (!MontarPecas.Exists(y => y.StartsWith(x.Split('=')[0] + "="))) MontarPecas.Add(x);
                VersaoPadroes = 8;
                mudou = true;
            }
            // v9: garagem (terminal, vaga, placa)
            if (VersaoPadroes < 9)
            {
                if (MontarPecas != null)
                    foreach (var x in new[] { "terminal=51258", "vaga=51259", "placa=51260" })
                        if (!MontarPecas.Exists(y => y.StartsWith(x.Split('=')[0] + "="))) MontarPecas.Add(x);
                VersaoPadroes = 9;
                mudou = true;
            }
            // v10: kits VIP do DZ (bandido, mercenario, comandante, especiais), kit do Discord e cofres com nome nosso
            if (VersaoPadroes < 10)
            {
                foreach (var k in KitsVipPadrao())
                    if (!Kits.Exists(x => x.Nome.Equals(k.Nome, StringComparison.OrdinalIgnoreCase))) Kits.Add(k);
                foreach (var v in Vaults)
                {
                    if (v.Nome == "Vault VIP") { v.Nome = "Cofre Mochila"; if (v.Largura == 7 && v.Altura == 7) v.Largura = 8; }
                    else if (v.Nome == "Vault Grande") { v.Nome = "Cofre Bunker"; if (v.Largura == 11 && v.Altura == 9) v.Largura = 12; }
                    else if (v.Nome == "Vault Ultra") { v.Nome = "Cofre Fortaleza"; if (v.Largura == 15 && v.Altura == 13) v.Altura = 14; }
                }
                VersaoPadroes = 10;
                mudou = true;
            }
            // v11: enfeites da oficina, estacionamento e heliponto
            if (VersaoPadroes < 11)
            {
                if (MontarPecas != null)
                    foreach (var x in new[] { "elevador=51261", "macaco=51262", "bancada=51263", "pneus=51264", "galao=51265", "bomba=51266", "cone=51267", "biruta=51268", "luz_pouso=51269", "caixa_ferramentas=51270" })
                        if (!MontarPecas.Exists(y => y.StartsWith(x.Split('=')[0] + "="))) MontarPecas.Add(x);
                VersaoPadroes = 11;
                mudou = true;
            }
            // v12: enfeites de cada cantinho (armas, roupas, comida, remedios, construtor, banco, cacador...)
            if (VersaoPadroes < 12)
            {
                if (MontarPecas != null)
                    foreach (var x in new[] { "rack_armas=51271", "prateleira_comida=51272", "manequim_dz=51273", "arara_roupas=51274", "armario_remedios=51275", "maca=51276", "prateleira_equip=51277", "cofre_dz=51278", "caixas_militares=51279", "varal_caca=51280", "caixa_tnt=51281", "mesa_dinheiro=51282", "materiais=51283", "pecas_aviao=51284", "balcao_dz=51285", "alvo=51286", "sacos_areia=51287", "barril_toxico=51288", "espelho=51289", "soro=51290", "trofeu=51291", "geladeira=51292", "fogao=51293", "caixa_frutas=51294" })
                        if (!MontarPecas.Exists(y => y.StartsWith(x.Split('=')[0] + "="))) MontarPecas.Add(x);
                VersaoPadroes = 12;
                mudou = true;
            }
            // v13: bomba de gasolina vira tanque (galão enche à vontade); sem o pack, usa o Fuel Drum do California 2
            if (VersaoPadroes < 13)
            {
                if (MontarPecas != null)
                {
                    int i = MontarPecas.FindIndex(y => y.StartsWith("bomba="));
                    if (i >= 0 && MontarPecas[i] == "bomba=51266") MontarPecas[i] = "bomba=51266|6173";
                }
                VersaoPadroes = 13;
                mudou = true;
            }
            // v14: site com endereço próprio e cadeado
            if (VersaoPadroes < 14)
            {
                if (string.IsNullOrWhiteSpace(LinkSiteServidor) || LinkSiteServidor.TrimEnd('/') == "http://54.94.93.87")
                    LinkSiteServidor = "https://dayz.snaplag.com.br";
                VersaoPadroes = 14;
                mudou = true;
            }
            if (VersaoPadroes < 15)
            {
                if (string.IsNullOrWhiteSpace(ReinicioDiario)) ReinicioDiario = "07:00";
                VersaoPadroes = 15;
                mudou = true;
            }
            // v17: piso e pilar do /montar ... nivelar (concreto do CA2, metal do jogo base no lugar)
            if (VersaoPadroes < 17)
            {
                if (MontarPecas != null)
                    foreach (var x in new[] { "piso=4552|369", "pilar=4556|374" })
                        if (!MontarPecas.Exists(y => y.StartsWith(x.Split('=')[0] + "="))) MontarPecas.Add(x);
                VersaoPadroes = 17;
                mudou = true;
            }
            // v16: California 2 é montanhoso; com as cercas inclinando, 10 m de desnível fica bom
            if (VersaoPadroes < 16)
            {
                if (MontarDesnivelMax < 10f) MontarDesnivelMax = 10f;
                VersaoPadroes = 16;
                mudou = true;
            }
            // config antiga sem bônus/título nos kits de rank: copia do padrão
            var padrao = KitsPadrao();
            foreach (var k in Kits)
            {
                var d = padrao.Find(x => x.Nome == k.Nome);
                if (d == null) continue;
                if (k.BonusXp == 0 && d.BonusXp > 0) { k.BonusXp = d.BonusXp; mudou = true; }
                if (string.IsNullOrEmpty(k.Titulo) && !string.IsNullOrEmpty(d.Titulo)) { k.Titulo = d.Titulo; mudou = true; }
            }
            return mudou;
        }

        static ItemKit I(ushort id, int q = 1) => new ItemKit(id, q);
        static List<ItemKit> L(params ItemKit[] i) => new List<ItemKit>(i);

        static List<Patente> PatentesPadrao() => new List<Patente>
        {
            new Patente("Recruta", 0, "#8A9099"),
            new Patente("Sobrevivente", 500, "#C9CDD3"),
            new Patente("Cacador", 2000, "#5BE37D"),
            new Patente("Veterano", 6000, "#FFB020"),
            new Patente("Elite", 15000, "#FF7A2E"),
            new Patente("Lenda", 40000, "#FF5A00"),
        };

        // Permissões VIP (o site vende; o admin dá com /p ou pelo grupo do Rocket). Cada nível inclui os de baixo.
        const string PIniciante = "vip.iniciante,vip.bandido,vip.mercenario,vip.comandante,vip.supremo";
        const string PBandido = "vip.bandido,vip.mercenario,vip.comandante,vip.supremo";
        const string PMercenario = "vip.mercenario,vip.comandante,vip.supremo";
        const string PComandante = "vip.comandante,vip.supremo";

        /// <summary>Kits VIP do DAY Z BRASIL (nomes e conteúdo nossos; o admin pode mudar no .xml).</summary>
        public static List<Kit> KitsVipPadrao() => new List<Kit>
        {
            new Kit { Nome = "discord", Titulo = "Discord", Permissao = "discord", Espera = 14400,
                Descricao = "Quem vinculou a Steam no Discord: mochila, comida, agua, curativos e uma Viper",
                Itens = L(I(4600), I(4523), I(4525, 2), I(4301, 2), I(4400, 2), I(4406, 4)) },
            // iniciantes (o pacote de entrada libera 1 dos 3; os níveis VIP liberam os 3)
            new Kit { Nome = "andarilho", Titulo = "Andarilho", Permissao = "vip.andarilho," + PBandido, Espera = 1800,
                Descricao = "Viper, mochila media, comida, agua e curativos",
                Itens = L(I(4523), I(4525, 3), I(4137), I(4301, 2), I(4400, 2), I(4407, 3)) },
            new Kit { Nome = "batedor", Titulo = "Batedor", Permissao = "vip.batedor," + PBandido, Espera = 1800,
                Descricao = "Quicksilver, colete leve, binoculo e curativos",
                Itens = L(I(4724), I(4726, 3), I(4104), I(4407, 3), I(4400)) },
            new Kit { Nome = "saqueador", Titulo = "Saqueador", Permissao = "vip.saqueador," + PBandido, Espera = 1800,
                Descricao = "Shorty com cartuchos, mochila e ferramentas",
                Itens = L(I(4098), I(4198, 4), I(4600), I(4700), I(4407, 2)) },
            // BANDIDO
            new Kit { Nome = "bandido1", Titulo = "Bandido", Permissao = PBandido, Espera = 1800,
                Descricao = "Maplestrike com 3 pentes militares, colete e medkit",
                Itens = L(I(4518), I(4820, 3), I(4052), I(4414), I(4407, 3)) },
            new Kit { Nome = "bandido2", Titulo = "Bandido", Permissao = PBandido, Espera = 1800,
                Descricao = "Arachnid com 3 pentes, mochila militar e mira holografica",
                Itens = L(I(6256), I(6224, 3), I(4064), I(4206), I(4407, 3)) },
            // MERCENARIO
            new Kit { Nome = "mercenario1", Titulo = "Mercenario", Permissao = PMercenario, Espera = 1800,
                Descricao = "Roupa de mercenario, Maplestrike com Ares 4x e 4 pentes",
                Itens = L(I(4080), I(4082), I(4084), I(4086), I(4518), I(4820, 4), I(6409), I(4414)) },
            new Kit { Nome = "mercenario2", Titulo = "Mercenario", Permissao = PMercenario, Espera = 1800,
                Descricao = "Doorkicker com 6 cartuchos, colete pesado e estimulante",
                Itens = L(I(4526), I(4198, 6), I(4077), I(6269), I(4414)) },
            new Kit { Nome = "base", Titulo = "Base", Permissao = "vip.base," + PMercenario, Espera = 43200,
                Descricao = "Pra levantar a base: portoes, cercas, bau grande e cofre",
                Itens = L(I(7200, 2), I(4449, 2), I(6184), I(6957), I(4995)) },
            // COMANDANTE
            new Kit { Nome = "comandante1", Titulo = "Comandante", Permissao = PComandante, Espera = 1800,
                Descricao = "Sniper do mercado negro com luneta 8x e municao",
                Itens = L(I(4009), I(4010, 3), I(6395), I(4077), I(4414, 2)) },
            new Kit { Nome = "comandante2", Titulo = "Comandante", Permissao = PComandante, Espera = 1800,
                Descricao = "Umbra com supressor, mochila grande e estimulantes",
                Itens = L(I(4207), I(4820, 4), I(4532), I(6055), I(6269, 2), I(4414)) },
            new Kit { Nome = "zonaquente", Titulo = "Zona Quente", Permissao = "vip.zonaquente," + PComandante, Espera = 3600,
                Descricao = "Pra entrar na radiacao: anti-radiacao, estimulantes e medkits",
                Itens = L(I(4812, 2), I(4021), I(6268), I(4414, 3)) },
            // especial
            new Kit { Nome = "raid", Titulo = "Raid", Permissao = "vip.raid,vip.supremo", Espera = 86400,
                Descricao = "Explosivos pra raid (so funciona no horario de raid)",
                Itens = L(I(4091, 3), I(6064, 2)) },
        };

        static List<Kit> KitsPadrao() => new List<Kit>
        {
            new Kit { Nome = "inicial", Descricao = "Comida, agua, bandagens e uma 1911", Espera = 600,
                Itens = L(I(13), I(14), I(95, 3), I(97), I(98, 2)) },
            new Kit { Nome = "medico", Descricao = "Medkit, bandagens e curativos", Espera = 1800,
                Itens = L(I(15), I(95, 5), I(394, 3), I(96)) },
            new Kit { Nome = "cacador", Descricao = "Maple Rifle, municao e faca de acougueiro", Espera = 1800,
                Itens = L(I(474), I(478, 3), I(137)) },
            new Kit { Nome = "construtor", Descricao = "Machado, picareta, pregos e chapas", Espera = 3600,
                Itens = L(I(16), I(1198), I(71, 4), I(68, 4), I(67, 6)) },
            new Kit { Nome = "recruta", Titulo = "Recruta", Descricao = "Cobra com 3 pentes", Espera = 3600, Abates = 10, BonusXp = 1000,
                Itens = L(I(99), I(100, 3), I(95, 3)) },
            new Kit { Nome = "patrulha", Titulo = "Patrulheiro", Descricao = "Timberwolf, pentes e colete", Espera = 3600, Abates = 25, BonusXp = 2500,
                Itens = L(I(18), I(20, 3), I(310)) },
            new Kit { Nome = "assalto", Titulo = "Operador", Descricao = "Maplestrike com 4 pentes militares", Espera = 5400, Abates = 50, BonusXp = 5000,
                Itens = L(I(363), I(6, 4), I(15)) },
            new Kit { Nome = "atirador", Titulo = "Atirador de Elite", Descricao = "Grizzly com 3 pentes", Espera = 7200, Abates = 100, BonusXp = 10000,
                Itens = L(I(297), I(298, 3), I(333)) },
            new Kit { Nome = "discord", Descricao = "Agradecimento por entrar no Discord", Espera = 86400, Permissao = "kit.discord",
                Itens = L(I(81, 2), I(15), I(9)) },

            // livres por tema
            new Kit { Nome = "fazendeiro", Descricao = "Sementes, fertilizante, rastelo e agua", Espera = 1800,
                Itens = L(I(330, 2), I(336, 2), I(343, 2), I(345, 2), I(332, 2), I(142), I(14)) },
            new Kit { Nome = "camper", Descricao = "MREs, cantil, fogueira e saco de dormir", Espera = 1800,
                Itens = L(I(81, 2), I(337), I(362), I(288), I(14, 2)) },
            new Kit { Nome = "rush", Descricao = "Ace com 3 pentes e bandagens", Espera = 3600,
                Itens = L(I(107), I(108, 3), I(95, 3)) },
            new Kit { Nome = "base", Descricao = "Porta e paredes de metal, chapas, bau e bandeira", Espera = 7200,
                Itens = L(I(378), I(370), I(371, 2), I(1092, 4), I(366), I(1158)) },
            new Kit { Nome = "airdrop", Descricao = "Binoculo, sinalizador, radio e MRE", Espera = 7200,
                Itens = L(I(333), I(255, 2), I(1445), I(81)) },

            // por abates
            new Kit { Nome = "sentinela", Titulo = "Sentinela", Descricao = "Yuri com 3 pentes", Espera = 3600, Abates = 5, BonusXp = 500,
                Itens = L(I(1041), I(1042, 3), I(95, 2)) },
            new Kit { Nome = "comando", Titulo = "Comando", Descricao = "Kryzkarek, 3 pentes e capacete militar", Espera = 5400, Abates = 75, BonusXp = 7500,
                Itens = L(I(1039), I(1040, 3), I(309), I(15)) },
            new Kit { Nome = "ceifador", Titulo = "Ceifador", Descricao = "Dragonfang, roupa anti-radiacao e mascara", Espera = 7200, Abates = 150, BonusXp = 15000,
                Itens = L(I(132), I(133, 2), I(1015), I(1016), I(1017), I(1270), I(1271, 2)) },
            new Kit { Nome = "imortal", Titulo = "Imortal", Descricao = "Nykorev, colete, Alicepack e 2 medkits", Espera = 10800, Abates = 250, BonusXp = 25000,
                Itens = L(I(126), I(127, 2), I(310), I(253), I(15, 2)) },

            // VIP (dar a permissao no grupo VIP)
            new Kit { Nome = "vip", Descricao = "Maplestrike, 4 pentes, colete e Alicepack", Espera = 7200, Permissao = "kit.vip",
                Itens = L(I(363), I(6, 4), I(310), I(253)) },
            new Kit { Nome = "vipbase", Descricao = "Kit de base reforcado com gerador", Espera = 14400, Permissao = "kit.vip",
                Itens = L(I(378, 2), I(371, 4), I(1091, 4), I(328), I(458), I(1158)) },
            new Kit { Nome = "vipelite", Descricao = "Grizzly, 3 pentes, farda do deserto e 2 medkits", Espera = 14400, Permissao = "kit.vipelite",
                Itens = L(I(297), I(298, 3), I(1011), I(1012), I(1013), I(1010), I(15, 2)) },

            // ===== Kits com itens do California 2 (IDs 4000+), no estilo dos kits do Zumbas =====
            // Basicos do C2: 4407 bandagem, 4408 curativo, 4419 morfina, 4422 adrenalina, 6268 stim, 4400 agua, 4305 MRE

            // por abates (mesmos degraus do Zumbas: 25/50/100/150/200/250)
            new Kit { Nome = "capanga", Titulo = "Capanga", Descricao = "Greyhound, cartuchos e mochila de couro", Espera = 600, Abates = 25, BonusXp = 5000,
                Itens = L(I(6218), I(4198, 3), I(4137), I(4407, 3), I(4400), I(4301)) },
            new Kit { Nome = "policia", Titulo = "Policial", Descricao = "Doorkicker e farda policial com colete SWAT", Espera = 900, Abates = 50, BonusXp = 10000,
                Itens = L(I(4526), I(4198, 3), I(4025), I(4023), I(4022), I(4036), I(4407, 3)) },
            new Kit { Nome = "cabo", Titulo = "Cabo", Descricao = "Banshee, pistola Talon e mochila de mercenario", Espera = 900, Abates = 100, BonusXp = 15000,
                Itens = L(I(7157), I(7158, 3), I(4119), I(4118, 2), I(4066), I(4407, 4), I(4408, 2)) },
            new Kit { Nome = "merc", Titulo = "Mercenario", Descricao = "Palmov e roupa completa de mercenario", Espera = 1800, Abates = 150, BonusXp = 20000,
                Itens = L(I(4826), I(4734, 3), I(4080), I(4082), I(4084), I(4088), I(4066), I(4407, 4)) },
            new Kit { Nome = "soldado", Titulo = "Soldado", Descricao = "Metralhadora Sumendi e farda militar", Espera = 1800, Abates = 200, BonusXp = 25000,
                Itens = L(I(6354), I(6108, 2), I(4060), I(4043), I(4039), I(4054), I(6055), I(4408, 3)) },
            new Kit { Nome = "sniper", Titulo = "Sniper", Descricao = "Black Arrow, Romeo e roupa ghillie", Espera = 1800, Abates = 250, BonusXp = 30000,
                Itens = L(I(6393), I(6394, 3), I(4520), I(4118, 2), I(4002), I(4004), I(6055), I(4419, 2)) },

            // VIP de entrada (o jogador escolhe 1: kit.vipbronze, kit.vipprata ou kit.vipouro)
            new Kit { Nome = "vipbronze", Descricao = "Quicksilver, machete e armadura de sucata", Espera = 1800, Permissao = "kit.vipbronze",
                Itens = L(I(4724), I(4726, 3), I(4694), I(4137), I(6054), I(6053), I(4407, 3), I(4400), I(4305)) },
            new Kit { Nome = "vipprata", Descricao = "Tempest, faca militar e colete de carga", Espera = 1800, Permissao = "kit.vipprata",
                Itens = L(I(6046), I(4732, 3), I(4813), I(4066), I(4054), I(4407, 3), I(4400), I(4305)) },
            new Kit { Nome = "vipouro", Descricao = "Whopper, machete e colete de mercenario", Espera = 1800, Permissao = "kit.vipouro",
                Itens = L(I(6043), I(4726, 3), I(4694), I(4066), I(4088), I(4407, 3), I(4400), I(4305)) },

            // VIP diamante (kit.vipdiamante)
            new Kit { Nome = "vipdiamante1", Descricao = "Esfalka, Talon e roupa de mercenario com Alicepack", Espera = 1800, Permissao = "kit.vipdiamante",
                Itens = L(I(4097), I(4096, 3), I(4119), I(4118, 2), I(6057), I(4088), I(4082), I(4084), I(4407, 5), I(4419)) },
            new Kit { Nome = "vipdiamante2", Descricao = "Vihor, Talon e roupa de mercenario com Alicepack", Espera = 1800, Permissao = "kit.vipdiamante",
                Itens = L(I(4819), I(4805, 3), I(4119), I(4118, 2), I(6057), I(4088), I(4086), I(4084), I(4407, 5), I(4419)) },

            // VIP elite (kit.vipelite, a mesma do vipelite)
            new Kit { Nome = "vipelite1", Descricao = "Sabotage, Romeo, colete blindado e 2 stims", Espera = 1800, Permissao = "kit.vipelite",
                Itens = L(I(4536), I(4010, 3), I(4520), I(4118, 2), I(6055), I(7085), I(4060), I(4043), I(4039), I(6268, 2), I(4407, 5)) },
            new Kit { Nome = "vipelite2", Descricao = "Maplestrike, Romeo, colete blindado e 2 stims", Espera = 1800, Permissao = "kit.vipelite",
                Itens = L(I(4518), I(4031, 4), I(4520), I(4118, 2), I(6055), I(7085), I(4060), I(4043), I(4039), I(6268, 2), I(4407, 5)) },

            // VIP mestre (kit.vipmestre)
            new Kit { Nome = "vipmestre1", Descricao = "Monarch anti-material, Talon e farda militar avancada", Espera = 1800, Permissao = "kit.vipmestre",
                Itens = L(I(4738), I(4740, 3), I(4119), I(4118, 3), I(7166), I(4077), I(6225), I(6226), I(4060), I(6268, 2), I(4422, 2), I(4407, 6)) },
            new Kit { Nome = "vipmestre2", Descricao = "Sabertooth, Talon e farda militar avancada", Espera = 1800, Permissao = "kit.vipmestre",
                Itens = L(I(4735), I(4737, 4), I(4119), I(4118, 3), I(7166), I(4077), I(6225), I(6226), I(4060), I(6268, 2), I(4422, 2), I(4407, 6)) },

            // VIP especiais (kit.vipespecial)
            new Kit { Nome = "vipdeadzone", Descricao = "Ratel, Magnus e roupa anti-radiacao completa", Espera = 3600, Permissao = "kit.vipespecial",
                Itens = L(I(4727), I(4822, 3), I(4818), I(4522, 3), I(4185), I(4186), I(4202), I(6057), I(7018, 2), I(6268, 2), I(4407, 6)) },
            new Kit { Nome = "vipairdrop", Descricao = "Metralhadora Eotens, Magnus e 3 sinalizadores", Espera = 3600, Permissao = "kit.vipespecial",
                Itens = L(I(6096), I(6108, 2), I(4818), I(4522, 3), I(4750, 3), I(6057), I(4077), I(4407, 4)) },
            new Kit { Nome = "vipconstrutor", Descricao = "Britadeira, motosserra, caixas de metal e itens de base", Espera = 7200, Permissao = "kit.vipespecial",
                Itens = L(I(6035), I(6034), I(6957, 2), I(4995), I(378, 2), I(371, 4), I(1091, 4), I(328), I(458)) },
        };

        static List<PacoteXp> LojaXpPadrao() => new List<PacoteXp>
        {
            new PacoteXp { Nome = "Mercenario", Descricao = "Cobra, 3 pentes, medkit e mochila", Preco = 1500,
                Itens = L(I(99), I(100, 3), I(15), I(9)) },
            new PacoteXp { Nome = "Atirador", Descricao = "Timberwolf, 4 pentes e binoculo", Preco = 4000,
                Itens = L(I(18), I(20, 4), I(333)) },
            new PacoteXp { Nome = "Assalto", Descricao = "Maplestrike, 4 pentes e colete militar", Preco = 7000,
                Itens = L(I(363), I(6, 4), I(310)) },
            new PacoteXp { Nome = "Especialista", Descricao = "Zubeknakov, 4 pentes, colete e mochila Spec Ops", Preco = 12000,
                Itens = L(I(122), I(123, 4), I(1169), I(1170)) },
            new PacoteXp { Nome = "Lenda", Descricao = "Grizzly, 3 pentes, colete militar e 2 medkits", Preco = 30000,
                Itens = L(I(297), I(298, 3), I(1336), I(15, 2)) },
            new PacoteXp { Nome = "SWAT", Descricao = "Farda policial, PDW, 3 pentes, flashbangs e fumaca", Preco = 8500,
                Itens = L(I(223), I(224), I(225), I(10), I(116), I(6, 3), I(1346, 2), I(266, 2)) },
            new PacoteXp { Nome = "Ghillie", Descricao = "Roupa ghillie completa e Snayperskya com 3 pentes", Preco = 10000,
                Itens = L(I(235), I(236), I(237), I(238), I(129), I(130, 3)) },
            new PacoteXp { Nome = "Cientista", Descricao = "Roupa anti-radiacao, mascara, 3 filtros e medkit (pra deadzone)", Preco = 15000,
                Itens = L(I(1015), I(1016), I(1017), I(1270), I(1271, 3), I(15)) },
            new PacoteXp { Nome = "Engenheiro", Descricao = "3 sentinelas automaticas com Maplestrike e pentes", Preco = 55000,
                Itens = L(I(1372, 3), I(363, 3), I(6, 6)) },
        };

        static List<TamanhoVault> VaultsPadrao() => new List<TamanhoVault>
        {
            new TamanhoVault { Nome = "Vault", Largura = 5, Altura = 5 },
            new TamanhoVault { Nome = "Cofre Mochila", Permissao = "vault.vip,vip.supremo", Largura = 8, Altura = 7 },
            new TamanhoVault { Nome = "Cofre Bunker", Permissao = "vault.grande,vip.supremo", Largura = 12, Altura = 9 },
            new TamanhoVault { Nome = "Cofre Fortaleza", Permissao = "vault.ultra,vip.supremo", Largura = 15, Altura = 14 },
        };

        static List<KitAirdrop> AirdropPadrao() => new List<KitAirdrop>
        {
            new KitAirdrop { Nome = "Comida", Itens = L(I(81, 4), I(13, 3), I(14, 4), I(15, 2), I(95, 5)) },
            new KitAirdrop { Nome = "Armas", Itens = L(I(363), I(6, 4), I(297), I(298, 3), I(18), I(20, 3)) },
            new KitAirdrop { Nome = "Equipamentos", Itens = L(I(310), I(253), I(309), I(333), I(15, 2)) },
            new KitAirdrop { Nome = "Completo", Itens = L(I(363), I(6, 4), I(310), I(253), I(309), I(15, 2), I(81, 2), I(14, 2)) },
            new KitAirdrop { Nome = "Base", Itens = L(I(378, 2), I(370, 2), I(371, 4), I(1092, 8), I(366, 2), I(1158)) },
        };

        static List<string> AnunciosPadrao() => new List<string>
        {
            "Digite /menu pra ver tudo que o servidor tem.",
            "Guarde seu dinheiro no ATM com /depositar. Dinheiro no bolso cai quando voce morre.",
            "Abates liberam kits melhores. Veja com /kits.",
            "Seu cofre pessoal: /vault (dentro da safe zone).",
            "Perdido? /safe te leva pra safe zone (fique parado).",
            "Troque seu XP por equipamento na /lojaxp.",
            "Ranking dos melhores: /top",
        };

        static List<string> TutorialPadrao() => new List<string>
        {
            "Bem-vindo ao DAY Z BRASIL OFICIAL! Aqui vai um guia rapido.",
            "Na SAFE ZONE nao tem PvP. Saindo dela, PvP liberado. Use /safe pra voltar (fique parado).",
            "Dinheiro: as notas do jogo. Guarde no ATM com /depositar e saque com /sacar. Lojas: /lojas.",
            "Kits: /kits mostra os seus. Abates liberam kits melhores. Pegue o seu com /kit inicial.",
            "Seu perfil e patente: /perfil. Cofre pessoal: /vault. Teleporte pra amigo: /tpa.",
            "Tudo isso e mais em /menu. Bom jogo, sobrevivente!",
        };
    }
}
