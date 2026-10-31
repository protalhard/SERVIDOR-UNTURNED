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
        public string LinkSite = "https://snaplag.com.br/";

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
        public List<string> Anuncios;
        public List<string> Tutorial;

        /// <summary>Preenche o que estiver faltando (config antiga). Devolve true se mudou algo.</summary>
        public bool Completar()
        {
            bool mudou = false;
            if (Patentes == null || Patentes.Count == 0) { Patentes = PatentesPadrao(); mudou = true; }
            if (Kits == null) { Kits = KitsPadrao(); mudou = true; }
            if (LojaXp == null) { LojaXp = LojaXpPadrao(); mudou = true; }
            if (Vaults == null || Vaults.Count == 0) { Vaults = VaultsPadrao(); mudou = true; }
            if (Anuncios == null) { Anuncios = AnunciosPadrao(); mudou = true; }
            if (Tutorial == null) { Tutorial = TutorialPadrao(); mudou = true; }
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
        };

        static List<TamanhoVault> VaultsPadrao() => new List<TamanhoVault>
        {
            new TamanhoVault { Nome = "Vault", Largura = 5, Altura = 5 },
            new TamanhoVault { Nome = "Vault VIP", Permissao = "vault.vip", Largura = 7, Altura = 7 },
            new TamanhoVault { Nome = "Vault Grande", Permissao = "vault.grande", Largura = 11, Altura = 9 },
            new TamanhoVault { Nome = "Vault Ultra", Permissao = "vault.ultra", Largura = 15, Altura = 13 },
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
