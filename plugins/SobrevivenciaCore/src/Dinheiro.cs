using System.Collections.Generic;
using System.Linq;
using Rocket.Unturned.Player;
using SDG.Unturned;

namespace SobrevivenciaCore
{
    /// <summary>Dinheiro físico: as notas e moedas do próprio Unturned que ficam no inventário.</summary>
    public static class Dinheiro
    {
        static List<Nota> Notas => Plugin.Instancia.Configuration.Instance.Notas;

        static int Valor(ushort id) => Notas.FirstOrDefault(n => n.Id == id)?.Valor ?? 0;

        static List<InventorySearch> Buscar(UnturnedPlayer p, ushort id)
        {
            var lista = new List<InventorySearch>();
            p.Player.inventory.search(lista, id, true, true);
            return lista.Where(s => s.page < PlayerInventory.STORAGE).ToList();
        }

        static int Unidades(Item item)
        {
            var asset = Assets.find(EAssetType.ITEM, item.id) as ItemAsset;
            return asset != null && asset.amount > 1 ? System.Math.Max(1, (int)item.amount) : 1;
        }

        public static long NoBolso(UnturnedPlayer p)
        {
            long total = 0;
            foreach (var n in Notas)
                foreach (var s in Buscar(p, n.Id))
                    total += (long)n.Valor * Unidades(s.jar.item);
            return total;
        }

        static void Remover(UnturnedPlayer p, InventorySearch s)
        {
            var inv = p.Player.inventory;
            var idx = inv.getIndex(s.page, s.jar.x, s.jar.y);
            if (idx != byte.MaxValue) inv.removeItem(s.page, idx);
        }

        /// <summary>Tira todo o dinheiro do inventário e devolve o valor.</summary>
        public static long RecolherTudo(UnturnedPlayer p)
        {
            long total = 0;
            foreach (var n in Notas)
                foreach (var s in Buscar(p, n.Id))
                {
                    total += (long)n.Valor * Unidades(s.jar.item);
                    Remover(p, s);
                }
            return total;
        }

        /// <summary>Entrega o valor em notas/moedas (o que não couber cai no chão).</summary>
        public static void Dar(UnturnedPlayer p, long valor)
        {
            foreach (var n in Notas.OrderByDescending(n => n.Valor))
            {
                while (valor >= n.Valor)
                {
                    p.Player.inventory.forceAddItem(new Item(n.Id, true), true);
                    valor -= n.Valor;
                }
            }
        }

        /// <summary>Cobra o valor do dinheiro no bolso e devolve o troco.</summary>
        public static bool Pagar(UnturnedPlayer p, long valor)
        {
            if (NoBolso(p) < valor) return false;
            var total = RecolherTudo(p);
            Dar(p, total - valor);
            return true;
        }
    }
}
