using System.Collections.Generic;
using System.Linq;
using Rocket.Unturned.Player;
using SDG.Unturned;

namespace SobrevivenciaCore
{
    public static class Itens
    {
        public static string Nome(ushort id)
            => (Assets.find(EAssetType.ITEM, id) as ItemAsset)?.itemName ?? ("Item " + id);

        public static bool Existe(ushort id) => Assets.find(EAssetType.ITEM, id) is ItemAsset;

        public static void Dar(UnturnedPlayer p, ushort id, int qtd)
        {
            for (int i = 0; i < qtd; i++)
                p.Player.inventory.forceAddItem(new Item(id, true), true);
        }

        public static int Contar(UnturnedPlayer p, ushort id) => Buscar(p, id).Count;

        static List<InventorySearch> Buscar(UnturnedPlayer p, ushort id)
        {
            var lista = new List<InventorySearch>();
            p.Player.inventory.search(lista, id, true, true);
            return lista.Where(s => s.page < PlayerInventory.STORAGE).ToList();
        }

        /// <summary>Remove até qtd unidades do item; devolve quantas removeu.</summary>
        public static int Remover(UnturnedPlayer p, ushort id, int qtd)
        {
            int removidos = 0;
            var inv = p.Player.inventory;
            foreach (var s in Buscar(p, id))
            {
                if (removidos >= qtd) break;
                var idx = inv.getIndex(s.page, s.jar.x, s.jar.y);
                if (idx == byte.MaxValue) continue;
                inv.removeItem(s.page, idx);
                removidos++;
            }
            return removidos;
        }
    }
}
