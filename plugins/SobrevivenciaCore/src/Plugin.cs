using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Rocket.Core.Plugins;
using Rocket.Unturned;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Events;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using UnityEngine;

namespace SobrevivenciaCore
{
    public partial class Plugin : RocketPlugin<Config>
    {
        public static Plugin Instancia;
        public Banco Banco;
        readonly Dictionary<ulong, string> zonaAtual = new Dictionary<ulong, string>();

        protected override void Load()
        {
            Instancia = this;
            Banco = new Banco(Directory);
            IniciarSistemas();
            LigarSafeZone();
            MigrarLojas();
            CarregarStaff();
            LigarMenu();
            LigarLojas();
            UnturnedPlayerEvents.OnPlayerDeath += AoMorrer;
            UnturnedPlayerEvents.OnPlayerUpdateStat += AoMudarStat;
            DamageTool.damagePlayerRequested += AoDanoNoJogador;
            U.Events.OnPlayerConnected += AoConectar;
            U.Events.OnPlayerDisconnected += AoDesconectar;
            UnturnedPlayerEvents.OnPlayerRevive += AoRenascer;
            ChatManager.onChatted += AoFalar;
            InvokeRepeating(nameof(ChecarZonas), 1f, 1f);
            Rocket.Core.Logging.Logger.Log($"SobrevivenciaCore carregado: {Configuration.Instance.Lojas.Count} lojas, " +
                $"{Configuration.Instance.Atms.Count} ATMs, {Configuration.Instance.ZonasSeguras.Count} safe zones.");
        }

        protected override void Unload()
        {
            CancelInvoke(nameof(ChecarZonas));
            DesligarMenu();
            DesligarLojas();
            UnturnedPlayerEvents.OnPlayerDeath -= AoMorrer;
            UnturnedPlayerEvents.OnPlayerUpdateStat -= AoMudarStat;
            DesligarSafeZone();
            PararSistemas();
            DamageTool.damagePlayerRequested -= AoDanoNoJogador;
            U.Events.OnPlayerConnected -= AoConectar;
            U.Events.OnPlayerDisconnected -= AoDesconectar;
            UnturnedPlayerEvents.OnPlayerRevive -= AoRenascer;
            ChatManager.onChatted -= AoFalar;
            Instancia = null;
        }

        public Color Cor => UnturnedChat.GetColorFromName(Configuration.Instance.CorMensagem, Color.yellow);

        public void Msg(UnturnedPlayer p, string texto) => Chat.Info(p, Chat.Limpar(texto));
        public void Erro(UnturnedPlayer p, string texto) => Chat.Erro(p, Chat.Limpar(texto));

        // ---------- Zonas ----------

        static float DistanciaPlana(Vector3 a, float x, float z)
        {
            var dx = a.x - x; var dz = a.z - z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public Ponto ZonaSeguraEm(Vector3 pos)
            => Configuration.Instance.ZonasSeguras.FirstOrDefault(z => DistanciaPlana(pos, z.X, z.Z) <= z.Raio);

        public Ponto AtmPerto(Vector3 pos)
            => (S.AtmSoNaSafe && ZonaSeguraEm(pos) == null) ? null
             : Configuration.Instance.Atms.FirstOrDefault(a => Vector3.Distance(pos, new Vector3(a.X, a.Y, a.Z)) <= a.Raio);

        public Loja LojaPerto(Vector3 pos)
            => Configuration.Instance.Lojas
                .Where(l => l.Definido && Vector3.Distance(pos, new Vector3(l.X, l.Y, l.Z)) <= l.Raio)
                .OrderBy(l => Vector3.Distance(pos, new Vector3(l.X, l.Y, l.Z)))
                .FirstOrDefault();

        void ChecarZonas()
        {
            try { TickSistemas(); }
            catch (System.Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
            try { TickMenus(); }
            catch (System.Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
            if (!Configuration.Instance.AvisarEntradaSafe) return;
            foreach (var sp in Provider.clients)
            {
                if (sp?.player == null) continue;
                var id = sp.playerID.steamID.m_SteamID;
                var zona = ZonaSeguraEm(sp.player.transform.position)?.Nome;
                zonaAtual.TryGetValue(id, out var antes);
                if (zona == antes) continue;
                zonaAtual[id] = zona;
                var p = UnturnedPlayer.FromSteamPlayer(sp);
                if (zona != null)
                    Chat.Ok(p, $"Voce entrou na SAFE ZONE ({Chat.Limpar(zona)}). PvP desligado aqui.");
                else
                    Chat.Erro(p, "Voce saiu da safe zone. PvP LIBERADO! Cuidado.");
            }
        }

        void AoDanoNoJogador(ref DamagePlayerParameters par, ref bool permitir)
        {
            if (!permitir || par.player == null) return;
            if (EhImortal(par.player)) { permitir = false; return; }
            var vitimaNaSafe = ZonaSeguraEm(par.player.transform.position) != null;

            if (par.cause == EDeathCause.ZOMBIE || par.cause == EDeathCause.ANIMAL)
            {
                if (vitimaNaSafe && Configuration.Instance.BloquearZumbisNaSafe) permitir = false;
                return;
            }

            if (par.killer == CSteamID.Nil || par.killer == par.player.channel.owner.playerID.steamID) return;
            var atacante = PlayerTool.getPlayer(par.killer);
            if (atacante == null) return;

            var atacanteNaSafe = ZonaSeguraEm(atacante.transform.position) != null;
            if (vitimaNaSafe || atacanteNaSafe) { permitir = false; return; }
            MarcarCombate(par.player);
            MarcarCombate(atacante);
        }

        // ---------- Kit inicial ----------

        void AoConectar(UnturnedPlayer p)
        {
            var id = p.CSteamID.m_SteamID;
            if (!Banco.Existe(id) && Configuration.Instance.SaldoInicialBanco > 0)
                Banco.Somar(id, Configuration.Instance.SaldoInicialBanco);
            BoasVindas(p);
            BuscarAvatar(id);
            StartCoroutine(Depois(3f, p, () => MostrarHud(p)));
            if (Banco.PrimeiroKit(id))
                StartCoroutine(DarKitDepois(p, "Bem-vindo! Voce recebeu o kit inicial."));
        }

        void AoDesconectar(UnturnedPlayer p)
        {
            zonaAtual.Remove(p.CSteamID.m_SteamID);
            AoSair(p);
        }

        void AoRenascer(UnturnedPlayer p, Vector3 pos, byte angulo)
        {
            StartCoroutine(Depois(1f, p, () => MostrarHud(p)));
            if (Configuration.Instance.KitAoRenascer)
                StartCoroutine(DarKitDepois(p, "Voce renasceu com o kit inicial."));
        }

        IEnumerator DarKitDepois(UnturnedPlayer p, string msg)
        {
            yield return new WaitForSeconds(2f);
            if (p?.Player == null || p.Dead) yield break;
            DarKit(p);
            Msg(p, msg);
        }

        public void DarKit(UnturnedPlayer p)
        {
            foreach (var k in Configuration.Instance.KitInicial)
                Itens.Dar(p, k.Id, k.Quantidade);
            if (Configuration.Instance.DinheiroKitInicial > 0)
                Dinheiro.Dar(p, Configuration.Instance.DinheiroKitInicial);
        }
    }
}
