using System.Collections.Generic;

namespace SobrevivenciaCore
{
    /// <summary>Lojas e preços iniciais. Depois de gerado, edite o arquivo
    /// SobrevivenciaCore.configuration.xml no servidor e use /rocket reload SobrevivenciaCore.</summary>
    public static class LojasPadrao
    {
        static Loja L(string id, string nome, params ItemLoja[] itens)
            => new Loja { Id = id, Nome = nome, Itens = new List<ItemLoja>(itens) };
        static ItemLoja I(ushort id, int compra, int venda = 0) => new ItemLoja(id, compra, venda);

        public static List<Loja> Criar() => new List<Loja>
        {
            L("armas", "Loja de Armas",
                I(97, 250, 60),    // 1911
                I(98, 40, 8),      // 1911 Magazine
                I(99, 350, 90),    // Cobra
                I(100, 50, 10),    // Cobra Magazine
                I(484, 600, 150),  // Sportshot
                I(485, 60, 12),    // Sportshot Magazine
                I(112, 700, 170),  // Bluntforce
                I(113, 50, 10),    // 12 Gauge Shells
                I(18, 900, 220),   // Timberwolf
                I(20, 80, 15),     // Timberwolf Magazine
                I(1021, 1100, 260),// Avenger
                I(1022, 90, 18),   // Avenger Magazine
                I(1027, 1300, 300),// Viper
                I(1029, 90, 18),   // Viper Magazine
                I(363, 1800, 400), // Maplestrike
                I(4, 2200, 500),   // Eaglefire
                I(6, 120, 25),     // Military Magazine
                I(1035, 150, 30),  // Machete
                I(121, 120, 25)),  // Military Knife

            L("comida", "Mercado (comida e bebida)",
                I(13, 15, 3),      // Canned Beans
                I(79, 18, 3),      // Canned Tuna
                I(87, 18, 3),      // Canned Pasta
                I(89, 20, 4),      // Canned Beef
                I(460, 12, 2),     // Bread
                I(81, 60, 10),     // MRE
                I(86, 25, 5),      // Energy Bar
                I(14, 12, 2),      // Bottled Water
                I(80, 10, 2),      // Canned Cola
                I(91, 14, 3),      // Apple Juice
                I(93, 30, 5),      // Bottled Energy
                I(337, 40, 8)),    // Canteen

            L("geral", "Loja Geral",
                I(95, 15, 3),      // Bandage
                I(394, 35, 7),     // Dressing
                I(96, 40, 8),      // Splint
                I(389, 80, 15),    // Antibiotics
                I(395, 120, 25),   // Bloodbag
                I(15, 250, 50),    // Medkit
                I(9, 120, 25),     // Red Daypack
                I(245, 300, 60),   // Black Travelpack
                I(253, 700, 140),  // Alicepack
                I(310, 450, 90),   // Forest Military Vest
                I(333, 150, 30),   // Binoculars
                I(1508, 80, 15),   // Compass
                I(1176, 400, 80),  // GPS
                I(1199, 90, 18),   // Headlamp
                I(277, 120, 25),   // Carjack
                I(28, 150, 30),    // Portable Gas Can
                I(16, 90, 18),     // Camp Axe
                I(1198, 120, 25),  // Pickaxe
                I(71, 10, 2),      // Nails
                I(67, 15, 3),      // Metal Scrap
                I(68, 40, 8),      // Metal Sheet
                I(64, 15, 3),      // Rope
                I(66, 5, 1)),      // Cloth

            L("caca", "Cacador (compra carne e couro)",
                I(514, 0, 25),     // Raw Venison
                I(1120, 0, 30),    // Raw Beef
                I(1117, 0, 20),    // Pork
                I(516, 0, 35),     // Leather
                I(504, 0, 15),     // Raw Trout
                I(505, 0, 20),     // Raw Salmon
                I(1351, 0, 15),    // Raw Bass
                I(1959, 0, 25),    // Raw Northern Pike
                I(1961, 0, 40),    // Raw Tuna
                I(353, 200, 40),   // Maple Bow
                I(348, 5, 1),      // Maple Arrow
                I(346, 600, 120),  // Crossbow
                I(347, 8, 2),      // Arrow
                I(474, 500, 100),  // Maple Rifle
                I(478, 40, 8),     // Rifle Clip
                I(137, 80, 15)),   // Butcher Knife

            L("mercadonegro", "Mercado Negro",
                I(109, 3500, 900),   // Hawkhound
                I(111, 180, 40),     // Hawkhound Magazine
                I(122, 3800, 950),   // Zubeknakov
                I(123, 200, 45),     // Ranger Magazine
                I(126, 6000, 1500),  // Nykorev
                I(127, 450, 100),    // Nykorev Box
                I(132, 5500, 1400),  // Dragonfang
                I(133, 400, 90),     // Dragonfang Box
                I(129, 8000, 2000),  // Snayperskya
                I(130, 350, 80),     // Snayperskya Magazine
                I(297, 9000, 2200),  // Grizzly
                I(298, 400, 90),     // Grizzly Magazine
                I(1000, 4500, 1100), // Matamorez
                I(1003, 220, 50),    // Matamorez Magazine
                I(6, 300, 60),       // Military Magazine
                I(17, 700, 150),     // Military Drum
                I(1336, 1500, 350),  // Military Vest
                I(1169, 2200, 500),  // Spec Ops Vest
                I(1170, 2500, 600),  // Spec Ops Rucksack
                I(15, 600, 120),     // Medkit
                I(387, 400, 80),     // Adrenaline
                I(81, 150, 25)),     // MRE
        };
    }
}
