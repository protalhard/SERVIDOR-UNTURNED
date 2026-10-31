using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Rocket.API;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using UnityEngine;
using Action = System.Action;

namespace SobrevivenciaCore
{
    /// <summary>Teleporte que espera o jogador ficar parado.</summary>
    public class Teleporte
    {
        public string Nome;
        public Vector3 Inicio;
        public float Fim;
        public Func<Vector3?> Destino;
        public Action AoChegar;
        public int UltimoAviso = -1;
    }

    public partial class Plugin
    {
        public Sistemas S => Configuration.Instance.Sistemas;
        public Estatisticas Stats;
        public Vaults Cofres;

        readonly Dictionary<ulong, float> combateAte = new Dictionary<ulong, float>();
        readonly Dictionary<ulong, Teleporte> teleportes = new Dictionary<ulong, Teleporte>();
        readonly Dictionary<string, float> intervalos = new Dictionary<string, float>(); // "id|tipo" -> fim
        public readonly Dictionary<ulong, (ulong de, float expira)> pedidosTpa = new Dictionary<ulong, (ulong, float)>();

        float proximaLimpeza, proximoAnuncio;
        int indiceAnuncio, ultimaContagemLimpeza = -1;
        int segundosParaSalvar = 60;

        void IniciarSistemas()
        {
            if (Configuration.Instance.Sistemas == null) Configuration.Instance.Sistemas = new Sistemas();
            if (S.Completar()) Configuration.Save();
            Stats = new Estatisticas(Directory);
            Cofres = new Vaults(Directory);
            proximaLimpeza = S.LimpezaMinutos > 0 ? Time.realtimeSinceStartup + S.LimpezaMinutos * 60 : float.MaxValue;
            proximoAnuncio = Time.realtimeSinceStartup + S.AnuncioSegundos;
            ValidarItens();
        }

        void PararSistemas()
        {
            Cofres?.SalvarTodos();
            Stats?.Salvar(true);
        }

        void ValidarItens()
        {
            var ids = S.Kits.SelectMany(k => k.Itens ?? new List<ItemKit>())
                .Concat(S.LojaXp.SelectMany(p => p.Itens ?? new List<ItemKit>()))
                .Concat(Configuration.Instance.Lojas.SelectMany(l => l.Itens.Select(i => new ItemKit(i.Id))))
                .Concat(Configuration.Instance.KitInicial)
                .Select(i => i.Id).Distinct().Where(id => !Itens.Existe(id)).ToList();
            if (ids.Count > 0)
                Rocket.Core.Logging.Logger.LogWarning("SobrevivenciaCore: IDs de item que nao existem neste servidor: " + string.Join(", ", ids));
        }

        // ---------- Ajudas ----------

        public static ulong Id(UnturnedPlayer p) => p.CSteamID.m_SteamID;

        public Ficha FichaDe(UnturnedPlayer p) => Stats.Obter(Id(p), p.CharacterName);

        public Patente PatenteDe(long xp)
            => S.Patentes.Where(x => xp >= x.XpMinimo).OrderByDescending(x => x.XpMinimo).FirstOrDefault() ?? S.Patentes[0];

        public string NomeColorido(Ficha f)
        {
            var pat = PatenteDe(f.Xp);
            return Chat.Cor(pat.Cor, "[" + pat.Nome + "]") + " " + Chat.Cor(S.CorTexto, Chat.Limpar(f.Nome)) +
                   " " + Chat.Cor(S.CorApagada, "Lv." + f.Nivel);
        }

        public bool EmCombate(UnturnedPlayer p, out int resta)
        {
            resta = 0;
            if (!combateAte.TryGetValue(Id(p), out var fim)) return false;
            resta = Mathf.CeilToInt(fim - Time.realtimeSinceStartup);
            return resta > 0;
        }

        public bool EmIntervalo(UnturnedPlayer p, string tipo, out int resta)
        {
            resta = 0;
            if (!intervalos.TryGetValue(Id(p) + "|" + tipo, out var fim)) return false;
            resta = Mathf.CeilToInt(fim - Time.realtimeSinceStartup);
            return resta > 0;
        }

        public void IniciarIntervalo(UnturnedPlayer p, string tipo, int segundos)
            => intervalos[Id(p) + "|" + tipo] = Time.realtimeSinceStartup + segundos;

        public bool TemPermissao(UnturnedPlayer p, string perm)
            => string.IsNullOrEmpty(perm) || p.IsAdmin || p.HasPermission(perm);

        // ---------- XP ----------

        public void DarXp(UnturnedPlayer p, long qtd, string motivo = null)
        {
            if (qtd <= 0) return;
            var f = FichaDe(p);
            int nivelAntes = f.Nivel;
            var patAntes = PatenteDe(f.Xp);
            f.Xp += qtd;
            Stats.Mudou();
            if (motivo != null) Chat.Para(p, Chat.Cor(S.CorDestaque, "+" + qtd + " XP") + " " + Chat.Cor(S.CorApagada, motivo));
            if (f.Nivel > nivelAntes)
                Chat.Para(p, Chat.Cor(S.CorOk, "Subiu pro nivel " + f.Nivel + "!"));
            var patDepois = PatenteDe(f.Xp);
            if (patDepois != patAntes)
                Chat.Todos(Chat.Cor(S.CorTexto, Chat.Limpar(f.Nome)) + " agora e " + Chat.Cor(patDepois.Cor, Chat.Forte(patDepois.Nome)) + "!");
        }

        public Kit ProximoKitPorAbates(Ficha f)
            => S.Kits.Where(k => k.Abates > f.Abates).OrderBy(k => k.Abates).FirstOrDefault();

        // ---------- Combate ----------

        void MarcarCombate(Player pl)
        {
            if (pl == null) return;
            var id = pl.channel.owner.playerID.steamID.m_SteamID;
            bool novo = !combateAte.TryGetValue(id, out var fim) || fim <= Time.realtimeSinceStartup;
            combateAte[id] = Time.realtimeSinceStartup + S.CombateSegundos;
            var p = UnturnedPlayer.FromPlayer(pl);
            if (teleportes.Remove(id)) Chat.Erro(p, "Teleporte cancelado: voce entrou em combate.");
            if (novo) Chat.Para(p, Chat.Cor(S.CorPerigo, Chat.Forte("Modo de combate ATIVADO")) +
                Chat.Cor(S.CorApagada, " (" + S.CombateSegundos + "s sem /safe, /home, /tpa e /vault)"));
            if (Cofres.Aberto(id)) Cofres.Fechar(p);
        }

        // ---------- Teleportes ----------

        public void AgendarTeleporte(UnturnedPlayer p, string nome, int espera, Func<Vector3?> destino, Action aoChegar = null)
        {
            teleportes[Id(p)] = new Teleporte
            {
                Nome = nome, Inicio = p.Position, Fim = Time.realtimeSinceStartup + espera,
                Destino = destino, AoChegar = aoChegar,
            };
            Chat.Aviso(p, $"Teleportando pra {nome} em {espera}s. Fique parado.");
        }

        void TickTeleportes()
        {
            foreach (var id in teleportes.Keys.ToList())
            {
                var t = teleportes[id];
                var sp = PlayerTool.getSteamPlayer(id);
                if (sp?.player == null || sp.player.life.isDead) { teleportes.Remove(id); continue; }
                var p = UnturnedPlayer.FromSteamPlayer(sp);
                if (Vector3.Distance(p.Position, t.Inicio) > 1.0f)
                {
                    teleportes.Remove(id);
                    Chat.Erro(p, "Teleporte cancelado: voce se mexeu.");
                    continue;
                }
                int resta = Mathf.CeilToInt(t.Fim - Time.realtimeSinceStartup);
                if (resta > 0)
                {
                    if (resta <= 3 && resta != t.UltimoAviso) { t.UltimoAviso = resta; Chat.Info(p, $"Teleportando em {resta}..."); }
                    continue;
                }
                teleportes.Remove(id);
                var destino = t.Destino();
                if (destino == null) { Chat.Erro(p, "O destino nao existe mais."); continue; }
                p.Teleport(destino.Value + Vector3.up * 0.5f, p.Rotation);
                Chat.Ok(p, $"Voce chegou em {t.Nome}.");
                t.AoChegar?.Invoke();
            }
        }

        public void CancelarTeleporte(UnturnedPlayer p) => teleportes.Remove(Id(p));

        // ---------- Tick (1x por segundo) ----------

        void TickSistemas()
        {
            var agora = Time.realtimeSinceStartup;

            foreach (var id in combateAte.Keys.ToList())
                if (combateAte[id] <= agora)
                {
                    combateAte.Remove(id);
                    var sp = PlayerTool.getSteamPlayer(id);
                    if (sp?.player != null) Chat.Ok(UnturnedPlayer.FromSteamPlayer(sp), "Modo de combate encerrado.");
                }

            foreach (var id in pedidosTpa.Where(kv => kv.Value.expira <= agora).Select(kv => kv.Key).ToList())
                pedidosTpa.Remove(id);

            TickTeleportes();
            Cofres.Tick();

            // tempo jogado e XP por minuto
            foreach (var sp in Provider.clients)
            {
                if (sp?.player == null) continue;
                var f = Stats.Obter(sp.playerID.steamID.m_SteamID);
                f.Segundos++;
                if (f.Segundos % 60 == 0 && S.XpPorMinuto > 0)
                    DarXp(UnturnedPlayer.FromSteamPlayer(sp), S.XpPorMinuto);
            }
            if (Provider.clients.Count > 0) Stats.Mudou();

            TickLimpeza(agora);

            if (S.AnuncioSegundos > 0 && agora >= proximoAnuncio && S.Anuncios.Count > 0)
            {
                proximoAnuncio = agora + S.AnuncioSegundos;
                Chat.Todos(Chat.Cor(S.CorTexto, S.Anuncios[indiceAnuncio++ % S.Anuncios.Count]));
            }

            if (--segundosParaSalvar <= 0) { segundosParaSalvar = 60; Stats.Salvar(); }
        }

        void TickLimpeza(float agora)
        {
            if (proximaLimpeza == float.MaxValue) return;
            int resta = Mathf.CeilToInt(proximaLimpeza - agora);
            if (resta == ultimaContagemLimpeza) return;
            ultimaContagemLimpeza = resta;
            if (resta == 60)
                Chat.Todos(Chat.Cor(S.CorDestaque, Chat.Forte("Limpando os itens do chao em 60s.")) +
                    Chat.Cor(S.CorApagada, " Pegue o que for seu! Itens em caixas nao somem."));
            else if (resta > 0 && resta <= 5)
                Chat.Todos(Chat.Cor(S.CorDestaque, "Limpando itens em " + resta + "s"));
            else if (resta <= 0)
            {
                LimparItens();
                proximaLimpeza = agora + S.LimpezaMinutos * 60;
            }
        }

        public void LimparItens()
        {
            ItemManager.askClearAllItems();
            Chat.Todos(Chat.Cor(S.CorOk, "Todos os itens do chao foram limpos!"));
        }

        public void AgendarLimpeza(int segundos)
        {
            proximaLimpeza = Time.realtimeSinceStartup + segundos;
            ultimaContagemLimpeza = -1;
        }

        // ---------- Mortes e estatísticas ----------

        void AoMorrer(UnturnedPlayer vitima, EDeathCause causa, ELimb parte, CSteamID assassino)
        {
            var id = Id(vitima);
            combateAte.Remove(id);
            teleportes.Remove(id);
            if (Cofres.Aberto(id)) Cofres.Fechar(vitima, false);

            var fv = FichaDe(vitima);
            fv.Mortes++;
            Stats.Mudou();

            var matador = assassino != CSteamID.Nil && assassino != vitima.CSteamID ? PlayerTool.getPlayer(assassino) : null;
            if (matador != null)
            {
                var pm = UnturnedPlayer.FromPlayer(matador);
                var fm = FichaDe(pm);
                fm.Abates++;
                bool cabeca = parte == ELimb.SKULL;
                if (cabeca) fm.Cabeca++;
                var arma = matador.equipment?.asset?.itemName;
                int dist = Mathf.RoundToInt(Vector3.Distance(pm.Position, vitima.Position));
                Chat.Todos(NomeColorido(fm) + Chat.Cor(S.CorPrincipal, " matou ") + NomeColorido(fv) +
                    Chat.Cor(S.CorApagada, (arma != null ? " com " + Chat.Limpar(arma) : "") + " a " + dist + "m") + (cabeca ? Chat.Cor(S.CorPerigo, " (na cabeca)") : ""));
                DarXp(pm, S.XpAbate + (cabeca ? S.XpCabeca : 0), cabeca ? "abate na cabeca" : "abate");
                var prox = ProximoKitPorAbates(fm);
                if (prox != null)
                    Chat.Para(pm, Chat.Cor(S.CorApagada, "Proximo kit: ") + Chat.Cor(S.CorPrincipal, prox.Nome) +
                        Chat.Cor(S.CorTexto, $" {fm.Abates}/{prox.Abates} abates"));
                var liberado = S.Kits.FirstOrDefault(k => k.Abates == fm.Abates && k.Abates > 0);
                if (liberado != null)
                    Chat.Para(pm, Chat.Cor(S.CorOk, Chat.Forte($"Kit {liberado.Nome} liberado!")) + Chat.Cor(S.CorTexto, $" Use /kit {liberado.Nome}"));
            }
            else if (S.MostrarTodasMortes)
            {
                Chat.Todos(NomeColorido(fv) + Chat.Cor(S.CorApagada, " " + DescreverMorte(causa)));
            }
        }

        static string DescreverMorte(EDeathCause c)
        {
            switch (c)
            {
                case EDeathCause.ZOMBIE: return "foi devorado por zumbis";
                case EDeathCause.ANIMAL: return "foi atacado por um animal";
                case EDeathCause.BLEEDING: return "sangrou ate morrer";
                case EDeathCause.BONES: return "caiu de muito alto";
                case EDeathCause.FOOD: return "morreu de fome";
                case EDeathCause.WATER: return "morreu de sede";
                case EDeathCause.FREEZING: return "morreu de frio";
                case EDeathCause.BURNING: case EDeathCause.BURNER: return "morreu queimado";
                case EDeathCause.INFECTION: case EDeathCause.ACID: case EDeathCause.SPIT: return "morreu infectado";
                case EDeathCause.SUICIDE: return "desistiu da vida";
                case EDeathCause.BREATH: return "se afogou";
                case EDeathCause.ROADKILL: case EDeathCause.VEHICLE: return "foi atropelado";
                case EDeathCause.GRENADE: case EDeathCause.MISSILE: case EDeathCause.CHARGE: case EDeathCause.SPLASH: return "explodiu";
                case EDeathCause.LANDMINE: return "pisou numa mina";
                case EDeathCause.SHRED: return "se cortou no arame farpado";
                case EDeathCause.SENTRY: return "foi baleado por uma sentinela";
                case EDeathCause.GUN: return "foi baleado";
                default: return "morreu";
            }
        }

        void AoMudarStat(UnturnedPlayer p, EPlayerStat stat)
        {
            if (stat == EPlayerStat.KILLS_ZOMBIES_NORMAL) { FichaDe(p).Zumbis++; DarXp(p, S.XpZumbi); }
            else if (stat == EPlayerStat.KILLS_ZOMBIES_MEGA) { FichaDe(p).Megas++; DarXp(p, S.XpMega, "mega zumbi"); }
        }

        // ---------- Entrada ----------

        void BoasVindas(UnturnedPlayer p)
        {
            bool novo = !Stats.Existe(Id(p));
            var f = FichaDe(p);
            if (novo) StartCoroutine(Tutorial(p));
            else StartCoroutine(Depois(3f, p, () =>
                Chat.Para(p, Chat.Cor(S.CorTexto, "Bem-vindo de volta, ") + NomeColorido(f) +
                    Chat.Cor(S.CorTexto, ". Digite ") + Chat.Cor(S.CorPrincipal, "/menu") + Chat.Cor(S.CorTexto, " pra ver tudo."))));
        }

        IEnumerator Tutorial(UnturnedPlayer p)
        {
            yield return new WaitForSeconds(4f);
            for (int i = 0; i < S.Tutorial.Count; i++)
            {
                if (p?.Player == null) yield break;
                Chat.Para(p, Chat.Cor(S.CorApagada, $"({i + 1}/{S.Tutorial.Count}) ") + Chat.Cor(S.CorTexto, S.Tutorial[i]));
                yield return new WaitForSeconds(6f);
            }
        }

        IEnumerator Depois(float s, UnturnedPlayer p, Action a)
        {
            yield return new WaitForSeconds(s);
            if (p?.Player != null) a();
        }

        void AoSair(UnturnedPlayer p)
        {
            var id = Id(p);
            Cofres.Fechar(p, false);
            teleportes.Remove(id);
            pedidosTpa.Remove(id);
            Stats.Salvar();
        }
    }
}
