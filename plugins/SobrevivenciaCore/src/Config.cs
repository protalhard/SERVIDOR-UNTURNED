using System.Collections.Generic;
using System.Xml.Serialization;
using Rocket.API;

namespace SobrevivenciaCore
{
    public class Ponto
    {
        [XmlAttribute] public string Nome;
        [XmlAttribute] public float X;
        [XmlAttribute] public float Y;
        [XmlAttribute] public float Z;
        [XmlAttribute] public float Raio;

        public Ponto() { }
        public Ponto(string nome, float raio) { Nome = nome; Raio = raio; }
        public bool Definido => !(X == 0 && Y == 0 && Z == 0);
    }

    public class Nota
    {
        [XmlAttribute] public ushort Id;
        [XmlAttribute] public int Valor;
        // true = só aceita como pagamento, não é usada pra dar troco (maços de $1000/$5000/$10000)
        [XmlAttribute] public bool SoAceita;
        public Nota() { }
        public Nota(ushort id, int valor) { Id = id; Valor = valor; }
    }

    public class ItemKit
    {
        [XmlAttribute] public ushort Id;
        [XmlAttribute] public int Quantidade = 1;
        public ItemKit() { }
        public ItemKit(ushort id, int qtd = 1) { Id = id; Quantidade = qtd; }
    }

    /// <summary>Item de loja. Compra = preço que o jogador paga (0 = a loja não vende).
    /// Venda = quanto a loja paga ao jogador (0 = a loja não compra).</summary>
    public class ItemLoja
    {
        [XmlAttribute] public ushort Id;
        [XmlAttribute] public int Compra;
        [XmlAttribute] public int Venda;
        // Categoria na janela da loja (Enlatados, Rifles...). Vazio = "Geral".
        [XmlAttribute] public string Categoria = "";
        // true = Id é de veículo (aparece do lado do vendedor, trancado pro comprador)
        [XmlAttribute] public bool Veiculo;
        public ItemLoja() { }
        public ItemLoja(ushort id, int compra, int venda = 0) { Id = id; Compra = compra; Venda = venda; }
    }

    public class Loja
    {
        [XmlAttribute] public string Id;
        [XmlAttribute] public string Nome;
        [XmlAttribute] public float X;
        [XmlAttribute] public float Y;
        [XmlAttribute] public float Z;
        [XmlAttribute] public float Raio = 6;
        // safe, negro, cacador ou garagem (só muda o texto da janela)
        [XmlAttribute] public string Tipo = "safe";
        // onde os veículos comprados aparecem (0,0,0 = na frente do jogador)
        [XmlAttribute] public float VX;
        [XmlAttribute] public float VY;
        [XmlAttribute] public float VZ;
        // onde nasce o avião comprado (cabeceira da pista) e pra onde fica virado
        [XmlAttribute] public float AX;
        [XmlAttribute] public float AY;
        [XmlAttribute] public float AZ;
        [XmlAttribute] public float AR;
        public List<ItemLoja> Itens = new List<ItemLoja>();
        public bool Definido => !(X == 0 && Y == 0 && Z == 0);
    }

    public class Config : IRocketPluginConfiguration
    {
        public string CorMensagem;
        public bool BloquearZumbisNaSafe;
        public bool AvisarEntradaSafe;
        public int SaldoInicialBanco;
        public bool KitAoRenascer;
        public List<ItemKit> KitInicial;
        public int DinheiroKitInicial;
        public List<Nota> Notas;
        public List<Ponto> ZonasSeguras;
        public List<Ponto> Atms;
        public List<Loja> Lojas;
        // vagas da garagem (carro, heli, aviao), criadas pelo /montar
        public List<Vaga> Vagas;
        public Sistemas Sistemas;
        // Versão das lojas padrão já aplicada (2 = lojas com NPC e itens do California 2)
        public int VersaoLojas;
        public int VersaoCatalogo;

        public void LoadDefaults()
        {
            CorMensagem = "orange";
            BloquearZumbisNaSafe = true;
            AvisarEntradaSafe = true;
            SaldoInicialBanco = 0;
            KitAoRenascer = true;
            DinheiroKitInicial = 0;
            KitInicial = new List<ItemKit>
            {
                new ItemKit(13),      // Canned Beans
                new ItemKit(14),      // Bottled Water
                new ItemKit(95, 2),   // Bandage
                new ItemKit(276),     // Flashlight
                new ItemKit(139),     // Pocketknife
                new ItemKit(202),     // Green Daypack
            };
            // Dinheiro vanilla do Unturned (notas e moedas canadenses)
            Notas = new List<Nota>
            {
                // notas do California 2 primeiro (é com elas que o troco é dado)
                new Nota(4258, 500), new Nota(4257, 100), new Nota(4256, 50), new Nota(4255, 10), new Nota(4254, 5),
                new Nota(1055, 100), new Nota(1054, 50), new Nota(1053, 20),
                new Nota(1052, 10), new Nota(1051, 5), new Nota(1057, 2), new Nota(1056, 1),
            };
            ZonasSeguras = new List<Ponto>();
            Atms = new List<Ponto>();
            Lojas = LojasPadrao.Criar();
            VersaoLojas = LojasPadrao.Versao;
            Sistemas = new Sistemas();
            Sistemas.Completar();
        }
    }
}
