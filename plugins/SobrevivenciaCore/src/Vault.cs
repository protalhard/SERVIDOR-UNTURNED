using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Cofre virtual de cada jogador. Abre como se fosse uma caixa e salva a cada mudança.</summary>
    public class Vaults
    {
        readonly string pasta;
        readonly Dictionary<ulong, Items> abertos = new Dictionary<ulong, Items>();
        readonly HashSet<ulong> sujos = new HashSet<ulong>();

        public Vaults(string pastaPlugin)
        {
            pasta = Path.Combine(pastaPlugin, "vaults");
            Directory.CreateDirectory(pasta);
        }

        string Arquivo(ulong id) => Path.Combine(pasta, id + ".txt");

        public bool Aberto(ulong id) => abertos.ContainsKey(id);

        public void Abrir(UnturnedPlayer p, TamanhoVault tam)
        {
            var id = p.CSteamID.m_SteamID;
            Fechar(p, false);

            var items = new Items(PlayerInventory.STORAGE);
            items.resize(tam.Largura, tam.Altura);
            foreach (var linha in Ler(id))
            {
                var jar = linha;
                if (jar.x + jar.size_x <= tam.Largura && jar.y + jar.size_y <= tam.Altura)
                {
                    try { items.addItem(jar.x, jar.y, jar.rot, jar.item); continue; } catch { }
                }
                if (!items.tryAddItem(jar.item))
                    ItemManager.dropItem(jar.item, p.Position, false, true, true);
            }

            items.onItemAdded += (a, b, c) => Marcar(id);
            items.onItemRemoved += (a, b, c) => Marcar(id);
            items.onItemUpdated += (a, b, c) => Marcar(id);
            items.onStateUpdated += () => Marcar(id);
            abertos[id] = items;
            Gravar(id, items);

            var inv = p.Player.inventory;
            inv.isStoring = true;
            inv.isStorageTrunk = false;
            inv.storage = null;
            inv.updateItems(PlayerInventory.STORAGE, items);
            inv.sendStorage();
        }

        void Marcar(ulong id) { lock (sujos) sujos.Add(id); }

        /// <summary>Chamado a cada segundo: salva os cofres que mudaram e solta os que o jogador fechou.</summary>
        public void Tick()
        {
            List<ulong> ids;
            lock (sujos) { ids = sujos.ToList(); sujos.Clear(); }
            foreach (var id in ids)
                if (abertos.TryGetValue(id, out var items)) Gravar(id, items);

            foreach (var id in abertos.Keys.ToList())
            {
                var sp = PlayerTool.getSteamPlayer(new Steamworks.CSteamID(id));
                var inv = sp?.player?.inventory;
                if (inv == null || !inv.isStoring || inv.storage != null || inv.items[PlayerInventory.STORAGE] != abertos[id])
                {
                    Gravar(id, abertos[id]);
                    abertos.Remove(id);
                }
            }
        }

        public void Fechar(UnturnedPlayer p, bool avisarCliente = true)
        {
            var id = p.CSteamID.m_SteamID;
            if (!abertos.TryGetValue(id, out var items)) return;
            Gravar(id, items);
            abertos.Remove(id);
            var inv = p.Player?.inventory;
            if (inv != null && inv.isStoring && inv.storage == null)
            {
                if (avisarCliente) inv.closeStorageAndNotifyClient();
                else inv.closeStorage();
            }
        }

        public void SalvarTodos()
        {
            foreach (var kv in abertos) Gravar(kv.Key, kv.Value);
        }

        IEnumerable<ItemJar> Ler(ulong id)
        {
            var arq = Arquivo(id);
            if (!File.Exists(arq)) yield break;
            foreach (var l in File.ReadAllLines(arq))
            {
                var c = l.Split(',');
                if (c.Length < 7) continue;
                ItemJar jar;
                try
                {
                    var item = new Item(ushort.Parse(c[3]), byte.Parse(c[4]), byte.Parse(c[5]),
                        string.IsNullOrEmpty(c[6]) ? new byte[0] : Convert.FromBase64String(c[6]));
                    if (!(Assets.find(EAssetType.ITEM, item.id) is ItemAsset asset)) continue;
                    jar = new ItemJar(byte.Parse(c[0]), byte.Parse(c[1]), byte.Parse(c[2]), item);
                }
                catch { continue; }
                yield return jar;
            }
        }

        void Gravar(ulong id, Items items)
        {
            var arq = Arquivo(id);
            var tmp = arq + ".tmp";
            File.WriteAllLines(tmp, items.items.Select(j => string.Join(",", j.x, j.y, j.rot, j.item.id,
                j.item.amount, j.item.quality, Convert.ToBase64String(j.item.state ?? new byte[0]))));
            if (File.Exists(arq)) File.Delete(arq);
            File.Move(tmp, arq);
        }
    }
}
