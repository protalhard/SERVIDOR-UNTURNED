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

        public void LoadDefaults()
        {
            CorMensagem = "yellow";
            BloquearZumbisNaSafe = true;
            AvisarEntradaSafe = true;
            SaldoInicialBanco = 0;
            KitAoRenascer = true;
            DinheiroKitInicial = 50;
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
                new Nota(1055, 100), new Nota(1054, 50), new Nota(1053, 20),
                new Nota(1052, 10), new Nota(1051, 5), new Nota(1057, 2), new Nota(1056, 1),
            };
            ZonasSeguras = new List<Ponto>();
            Atms = new List<Ponto>();
            Lojas = LojasPadrao.Criar();
        }
    }
}
