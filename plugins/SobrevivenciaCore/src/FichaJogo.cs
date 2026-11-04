using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Ficha do jogo pro ticket do Discord: a cada 20 s grava fichas_jogo.json com onde cada jogador está,
    /// vida, dinheiro, facção e o que carrega. Quem saiu fica com a última foto (até 1000 jogadores).</summary>
    public partial class Plugin
    {
        public class FichaJogo
        {
            public string Id, Nome, Lugar, Safe, Carro, Faccao;
            public bool Online, Morto;
            public DateTime Visto;
            public int X, Y, Z, Vida, Fome, Sede;
            public long Banco, Bolso;
            public List<string> Itens = new List<string>();
        }

        Dictionary<string, FichaJogo> fichasJogo;
        float proximaFichaJogo;
        string ArqFichasJogo => Path.Combine(Directory, "fichas_jogo.json");

        FichaJogo FotoDoJogador(SteamPlayer sp)
        {
            var pl = sp.player;
            var up = UnturnedPlayer.FromSteamPlayer(sp);
            ulong id = sp.playerID.steamID.m_SteamID;
            var pos = pl.transform.position;
            var f = new FichaJogo
            {
                Id = id.ToString(), Nome = sp.playerID.characterName, Online = true, Visto = DateTime.UtcNow,
                X = Mathf.RoundToInt(pos.x), Y = Mathf.RoundToInt(pos.y), Z = Mathf.RoundToInt(pos.z),
                Lugar = RegiaoDe(pos), Safe = ZonaSeguraEm(pos)?.Nome ?? "",
                Vida = pl.life.health, Fome = pl.life.food, Sede = pl.life.water, Morto = pl.life.isDead,
                Carro = pl.movement.getVehicle()?.asset?.vehicleName ?? "",
                Banco = Banco.Saldo(id), Faccao = FaccaoDe(id)?.Nome ?? "",
            };
            try { f.Bolso = Dinheiro.NoBolso(up); } catch { }
            var contagem = new Dictionary<string, int>();
            var ordem = new List<string>();
            for (byte pg = 0; pg < PlayerInventory.PAGES - 2; pg++)
            {
                var items = pl.inventory.items[pg];
                if (items == null) continue;
                foreach (var j in items.items)
                {
                    var a = j?.GetAsset<ItemAsset>();
                    if (a == null) continue;
                    var nome = $"{a.itemName} ({a.id})";
                    if (!contagem.ContainsKey(nome)) { contagem[nome] = 0; ordem.Add(nome); }
                    contagem[nome] += Math.Max(1, (int)j.item.amount);
                }
            }
            f.Itens = ordem.Select(n => contagem[n] > 1 ? $"{n} x{contagem[n]}" : n).ToList();
            return f;
        }

        void TickFichaJogo()
        {
            float agora = Time.realtimeSinceStartup;
            if (agora < proximaFichaJogo) return;
            proximaFichaJogo = agora + 20f;
            if (fichasJogo == null)
            {
                try { if (File.Exists(ArqFichasJogo)) fichasJogo = JsonConvert.DeserializeObject<Dictionary<string, FichaJogo>>(File.ReadAllText(ArqFichasJogo)); }
                catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
                fichasJogo = fichasJogo ?? new Dictionary<string, FichaJogo>();
            }
            foreach (var f in fichasJogo.Values) f.Online = false;
            foreach (var sp in Provider.clients)
            {
                if (sp?.player == null) continue;
                try { var f = FotoDoJogador(sp); fichasJogo[f.Id] = f; }
                catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
            }
            if (fichasJogo.Count > 1000)
                foreach (var velho in fichasJogo.Values.OrderBy(f => f.Visto).Take(fichasJogo.Count - 1000).Select(f => f.Id).ToList())
                    fichasJogo.Remove(velho);
            var tmp = ArqFichasJogo + ".tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(fichasJogo));
            if (File.Exists(ArqFichasJogo)) File.Delete(ArqFichasJogo);
            File.Move(tmp, ArqFichasJogo);
        }
    }
}
