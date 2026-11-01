using System.Collections.Generic;

namespace SobrevivenciaCore
{
    /// <summary>Lojas e preços iniciais com itens do California 2 (IDs 4000+).
    /// Safe zone: armas comuns, comida, roupas, remédios, equipamentos e garagem (carros).
    /// Zona PvP: caçador (compra o que o jogador caça) e mercado negro (raid, armas fortes, aeronaves).
    /// Os preços podem ser mudados no jogo pela aba ADMIN (editor de preços) ou no .xml.</summary>
    public static class LojasPadrao
    {
        public const int Versao = 2;

        static string cat = "";
        static Loja L(string id, string nome, string tipo, params ItemLoja[] itens)
            => new Loja { Id = id, Nome = nome, Tipo = tipo, Itens = new List<ItemLoja>(itens) };
        static ItemLoja Cat(string c) { cat = c; return null; }

        // compra, venda = 30% (o jogador pode vender de volta)
        static ItemLoja I(ushort id, int compra) => new ItemLoja(id, compra, compra * 3 / 10) { Categoria = cat };
        // só compra (sem revenda: explosivos, aeronaves)
        static ItemLoja S(ushort id, int compra) => new ItemLoja(id, compra, 0) { Categoria = cat };
        // só vende pro jogador comprar? não: só COMPRA do jogador (caçador)
        static ItemLoja V(ushort id, int venda) => new ItemLoja(id, 0, venda) { Categoria = cat };
        static ItemLoja Car(ushort id, int compra) => new ItemLoja(id, compra, 0) { Categoria = cat, Veiculo = true };

        static Loja Limpa(Loja l) { l.Itens.RemoveAll(i => i == null); return l; }

        /// <summary>NPC construtor (safe): tudo pra montar base.</summary>
        public static Loja Construcao() => Limpa(L("construcao", "Construtor", "safe",
            Cat("Paredes"),
            I(4435, 60), I(4438, 30), I(4867, 80), I(4168, 120), I(4161, 60), I(6071, 200),
            I(4564, 300), I(4557, 150), I(7201, 300), I(7133, 400), I(7200, 500),
            Cat("Muros e portoes"),
            I(6201, 300), I(6202, 350), I(6203, 450), I(6196, 400), I(6207, 500), I(6208, 550), I(6209, 700),
            I(6204, 900), I(6205, 950), I(6206, 1200), I(378, 250), I(1089, 200), I(457, 400),
            Cat("Cercas"),
            I(7120, 40), I(4188, 50), I(7132, 100), I(7121, 120), I(7122, 200), I(7046, 600),
            Cat("Bau e armazem"),
            I(4448, 250), I(4590, 250), I(4459, 300), I(4449, 600), I(4743, 600), I(6956, 1500), I(6957, 2000),
            Cat("Energia e luz"),
            I(4467, 20), I(4577, 100), I(6155, 150), I(6184, 1200),
            Cat("Moveis"),
            I(4893, 60), I(4463, 80), I(4446, 150),
            Cat("Base"),
            I(4995, 1500)));

        public static List<Loja> Criar() => new List<Loja>
        {
            // ===================== SAFE ZONE =====================
            Limpa(L("armas", "Armas", "safe",
                Cat("Pistolas"),
                I(4189, 350),  // Veneta
                I(4810, 400),  // Emperor
                I(4119, 500),  // Talon
                I(6211, 550),  // Sentinel
                I(4800, 650),  // Ace
                I(4520, 700),  // Romeo
                I(4416, 900),  // Havoc
                Cat("Submetralhadoras"),
                I(6256, 1200), // Arachnid
                I(6095, 1400), // Ramatkal
                I(6043, 1600), // Whopper
                I(4523, 1900), // Viper
                I(4724, 2000), // Quicksilver
                Cat("Escopetas"),
                I(4098, 800),  // Shorty
                I(6260, 900),  // Novuh
                I(6218, 1200), // Greyhound
                I(4526, 1500), // Doorkicker
                Cat("Rifles"),
                I(4828, 1800), // Balaur
                I(4817, 2500), // Razortooth
                I(4074, 3500), // Porto
                I(4076, 3500), // Rebel
                I(4826, 4000), // Palmov
                I(6049, 4200), // Akula
                I(4518, 4500), // Maplestrike
                I(4207, 4500), // Umbra
                Cat("Municao"),
                I(4118, 30),   // Lebel Magazine (pistolas)
                I(4801, 35),   // .357 Speedloader
                I(6224, 60),   // Arachnid Magazine
                I(4726, 70),   // Silver Magazine
                I(4525, 70),   // Viper Magazine
                I(4198, 40),   // 12 Gauge Buckshot
                I(4823, 50),   // Ranger Magazine (10)
                I(4737, 80),   // Sage Magazine
                I(4820, 100),  // Military Magazine
                I(4734, 90),   // Ranger Magazine (30)
                I(4031, 100),  // Polymer Magazine
                Cat("Acessorios"),
                I(4073, 250),  // Venus Reflex Sight
                I(4206, 400),  // Holo Magnifier
                I(6409, 600),  // Ares 4x
                I(6395, 900),  // 8x Scope
                I(6090, 150),  // Vertical Grip
                I(6078, 150),  // Tactical Light
                I(4537, 200),  // Red Tactical Laser
                I(4532, 500),  // Hotshot Suppressor
                Cat("Corpo a corpo"),
                I(4685, 80),   // Metal Bat
                I(4700, 100),  // Camp Axe
                I(4813, 120),  // Military Knife
                I(4694, 150))), // Machete

            Limpa(L("comida", "Comida", "safe",
                Cat("Enlatados"),
                I(4301, 20), I(4302, 20), I(4303, 20), I(4304, 25), I(4311, 25), I(4312, 30), I(4313, 30), I(4327, 30),
                Cat("Lanches"),
                I(4806, 8), I(4307, 10), I(4379, 10), I(4309, 12), I(4310, 15), I(4325, 15), I(4326, 15), I(4338, 50), I(4339, 60), I(4305, 120),
                Cat("Bebidas"),
                I(4400, 15), I(4396, 15), I(4395, 18), I(4381, 20), I(4402, 20), I(4314, 20), I(4399, 25), I(6036, 80),
                Cat("Acampamento"),
                I(4586, 40), I(4991, 120), I(4578, 150))),

            Limpa(L("remedios", "Remedios", "safe",
                Cat("Curativos"),
                I(4406, 5), I(4407, 20), I(4408, 50), I(4414, 80),
                Cat("Remedios"),
                I(4410, 30), I(4417, 40), I(4413, 60), I(4421, 60), I(4412, 120), I(4419, 150), I(4422, 200),
                Cat("Estimulantes"),
                I(6269, 500), I(6268, 1500), I(7018, 1500), I(7017, 2500),
                Cat("Anti-radiacao"),
                I(4812, 200), I(4021, 1500))),

            Limpa(L("roupas", "Roupas", "safe",
                Cat("Policia"),
                I(4022, 100), I(4023, 150), I(4025, 150),
                Cat("Militar"),
                I(4047, 80), I(4049, 80), I(4044, 200), I(4039, 250), I(4040, 300), I(4043, 300), I(4060, 1200),
                Cat("Mercenario"),
                I(4082, 300), I(4084, 300), I(4086, 300), I(4080, 1500),
                Cat("Choque"),
                I(4027, 800), I(4028, 1000), I(4026, 1200),
                Cat("Especiais"),
                I(4004, 1500), I(4002, 3000), I(4202, 1000), I(4185, 1500), I(4186, 1500))),

            Limpa(L("equipamentos", "Equipamentos", "safe",
                Cat("Mochilas"),
                I(4983, 150), I(4137, 200), I(4600, 300), I(4064, 900), I(4066, 1000), I(6055, 1800), I(6057, 2000),
                Cat("Coletes"),
                I(4104, 100), I(4126, 120), I(4652, 150), I(4609, 150), I(4052, 500), I(4036, 500), I(4088, 600), I(4053, 700), I(4054, 900), I(4077, 1200),
                Cat("Ferramentas"),
                I(4999, 50), I(4698, 80), I(4696, 100), I(4689, 120), I(4699, 150), I(4710, 150), I(4190, 250), I(6034, 1500), I(6035, 2500),
                Cat("Base"),
                I(6039, 150), I(4144, 250), I(4448, 300), I(6040, 300), I(7200, 500), I(4449, 600), I(7131, 800), I(6184, 1200), I(4995, 1500), I(6957, 2000),
                Cat("Fazenda"),
                I(4316, 20), I(4318, 20), I(4322, 20), I(4324, 20), I(4405, 20), I(4570, 40))),

            Limpa(L("garagem", "Garagem", "garagem",
                Cat("Carros"),
                Car(4012, 300),    // Bicycle
                Car(4013, 2500),   // Quad
                Car(4002, 5000),   // Hatchback
                Car(4015, 5500),   // Station Wagon
                Car(4017, 7000),   // Van
                Car(4028, 8000),   // Indian (moto)
                Car(4003, 9000),   // Truck
                Car(4004, 10000),  // Offroader
                Car(4006, 12000),  // Baja
                Car(4019, 12000),  // Wagon
                Car(4018, 15000),  // Yukon
                Car(4016, 16000),  // Arcadia
                Car(4005, 18000),  // Sportscar
                Car(4007, 18000),  // Roadster
                Car(4014, 20000),  // Royal
                Car(4030, 20000),  // Upgraded Offroader
                Car(4009, 22000),  // Merc
                Car(4029, 25000),  // Upgraded Yukon
                Car(4011, 35000),  // Knight
                Cat("Barcos"),
                Car(4024, 4000),   // Jetski
                Car(4026, 8000),   // Dinghy
                Car(4025, 10000),  // Runabout
                Cat("Oficina"),
                I(4179, 150), I(4706, 150), I(4707, 200), I(4702, 400), I(4187, 600))),

            // ===================== ZONA PVP =====================
            Limpa(L("cacador", "Cacador", "cacador",
                Cat("Couro"),
                V(4286, 25), V(4287, 40), V(516, 40), V(7134, 140), V(7135, 220),
                Cat("Carne"),
                V(4334, 15), V(514, 15), V(4335, 20), V(515, 20), V(6837, 35), V(6838, 45),
                Cat("Peixe"),
                V(4330, 12), V(4332, 18), V(4370, 25),
                Cat("Fazenda"),
                V(4383, 5), V(470, 5), V(4314, 10), V(462, 10), V(4380, 20), V(4384, 30),
                Cat("Equipamento"),
                S(4692, 60), S(4991, 120), S(6063, 200))),

            Limpa(L("negro", "Mercado Negro", "negro",
                Cat("Snipers"),
                S(4009, 9000), S(4101, 10000), S(4536, 11000), S(4097, 12000), S(6405, 12000), S(6351, 13000),
                S(4062, 14000), S(6087, 15000), S(4738, 22000), S(4173, 25000), S(6393, 28000),
                Cat("Rifles fortes"),
                S(7141, 7000), S(4546, 7000), S(6403, 7500), S(4723, 8000), S(4735, 8500), S(4030, 9000),
                S(4544, 9500), S(4727, 9500), S(7098, 15000),
                Cat("Metralhadoras"),
                S(6046, 6000), S(4730, 6500), S(6354, 14000), S(6110, 16000), S(6096, 18000), S(6106, 18000),
                Cat("Pistolas e escopetas"),
                S(4521, 3000), S(4818, 3500), S(4195, 4000), S(4842, 5000), S(6411, 5000), S(6400, 6000), S(6219, 9000),
                Cat("Municao rara"),
                S(4010, 250), S(4096, 300), S(6406, 300), S(6089, 350), S(4063, 350), S(4843, 400), S(4522, 150),
                S(4714, 150), S(7142, 150), S(4822, 200), S(4732, 250), S(4200, 120), S(6401, 150), S(6402, 150),
                S(6220, 250), S(6108, 500), S(4740, 600), S(4175, 600), S(6394, 700),
                Cat("Explosivos"),
                S(4091, 1500), S(6064, 3000), S(6022, 5000), S(6021, 6000), S(6023, 10000), S(6819, 12000),
                S(6820, 25000), S(6020, 30000), S(6817, 40000), S(7143, 60000),
                Cat("Granadas e lancadores"),
                S(4733, 600), S(4204, 800), S(4747, 1200), S(6261, 1500), S(4741, 2500), S(4205, 3500),
                S(4177, 35000), S(4178, 5000), S(4720, 35000), S(6262, 5000), S(6253, 45000), S(6254, 6000),
                S(6908, 80000), S(6910, 12000),
                Cat("Elite e torres"),
                S(7137, 2000), S(4099, 2000), S(6396, 2500), S(4722, 3000), S(7090, 2500), S(4683, 4000),
                S(7084, 6000), S(7085, 6000), S(7089, 6000), S(6079, 8000),
                S(7033, 15000), S(6527, 40000), S(7150, 50000),
                Cat("Veiculos"),
                Car(4027, 50000),  // Upgraded Knight
                Car(4071, 60000),  // Kodiak
                Car(4075, 70000),  // Matvee
                Car(4127, 60000),  // Sandpiper (heli)
                Car(4129, 65000),  // Watchsun (heli)
                Car(4128, 90000),  // Hummingbird (heli)
                Car(4010, 120000), // Navajo (heli)
                Car(4031, 120000), // Chopper (heli)
                Car(4123, 250000), // Fighter Jet
                Car(4121, 300000))), // Abraham (tanque)
        };
    }
}
