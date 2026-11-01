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
        public string LinkSiteServidor = "http://54.94.93.87";
        // Donos: Steam IDs separados por vírgula. Veem a aba ADM inteira e montam a staff.
        public string Donos = "76561198168015168";

        // Teleportes
        public int WarpEspera = 15;
        public int WarpIntervalo = 30;
        public int HomeEspera = 10;
        public int HomeIntervalo = 120;
        public int TpaEspera = 10;
        public int TpaIntervalo = 60;
        public int TpaExpira = 30;

        // Combate e limpeza
        public int CombateSegundos = 20;
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

        public List<Patente> Patentes;
        public List<Kit> Kits;
        public List<PacoteXp> LojaXp;
        public List<TamanhoVault> Vaults;
        public List<KitAirdrop> AirdropKits;
        public List<string> Anuncios;
        public List<string> Tutorial;
        // Versão dos padrões já aplicada (pra adicionar kits/pacotes novos em config antiga)
        public int VersaoPadroes = 0;

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
            if (Tutorial == null) { Tutorial = TutorialPadrao(); mudou = true; }
            // padrões novos (v2): adiciona kits e pacotes que ainda não existem, sem mexer nos que o admin editou
            if (VersaoPadroes < 2)
            {
                foreach (var k in KitsPadrao())
                    if (!Kits.Exists(x => x.Nome.Equals(k.Nome, StringComparison.OrdinalIgnoreCase))) Kits.Add(k);
                foreach (var pc in LojaXpPadrao())
                    if (!LojaXp.Exists(x => x.Nome.Equals(pc.Nome, StringComparison.OrdinalIgnoreCase))) LojaXp.Add(pc);
                VersaoPadroes = 2;
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
            new TamanhoVault { Nome = "Vault VIP", Permissao = "vault.vip", Largura = 7, Altura = 7 },
            new TamanhoVault { Nome = "Vault Grande", Permissao = "vault.grande", Largura = 11, Altura = 9 },
            new TamanhoVault { Nome = "Vault Ultra", Permissao = "vault.ultra", Largura = 15, Altura = 13 },
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
