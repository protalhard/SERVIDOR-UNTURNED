using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Safe zone estilo DayZ: protege carros e bases e não deixa construir dentro.
    /// Também escreve o status.json que o site e o bot do Discord leem.</summary>
    public partial class Plugin
    {
        int segundosStatus;

        void LigarSafeZone()
        {
            BarricadeManager.onDeployBarricadeRequested += AoColocarBarricada;
            StructureManager.onDeployStructureRequested += AoColocarEstrutura;
            BarricadeManager.onDamageBarricadeRequested += AoDanoBarricada;
            StructureManager.onDamageStructureRequested += AoDanoEstrutura;
            VehicleManager.onDamageVehicleRequested += AoDanoVeiculo;
            VehicleManager.onDamageTireRequested += AoDanoPneu;
            VehicleManager.onVehicleLockpicked += AoArrombarVeiculo;
            VehicleManager.onVehicleCarjacked += AoRoubarVeiculo;
            VehicleManager.onSiphonVehicleRequested += AoSifonar;
            VehicleManager.onEnterVehicleRequested += AoEntrarVeiculo;
            BarricadeManager.onSalvageBarricadeRequested += AoDesmontarPeca;
        }

        void DesligarSafeZone()
        {
            BarricadeManager.onDeployBarricadeRequested -= AoColocarBarricada;
            StructureManager.onDeployStructureRequested -= AoColocarEstrutura;
            BarricadeManager.onDamageBarricadeRequested -= AoDanoBarricada;
            StructureManager.onDamageStructureRequested -= AoDanoEstrutura;
            VehicleManager.onDamageVehicleRequested -= AoDanoVeiculo;
            VehicleManager.onDamageTireRequested -= AoDanoPneu;
            VehicleManager.onVehicleLockpicked -= AoArrombarVeiculo;
            VehicleManager.onVehicleCarjacked -= AoRoubarVeiculo;
            VehicleManager.onSiphonVehicleRequested -= AoSifonar;
            VehicleManager.onEnterVehicleRequested -= AoEntrarVeiculo;
            BarricadeManager.onSalvageBarricadeRequested -= AoDesmontarPeca;
        }

        bool NaSafe(Vector3 pos) => ZonaSeguraEm(pos) != null;

        /// <summary>Peça do /montar e NPC: só o dono do servidor (ou admin) tira com F. Se for o ATM, some do banco junto.
        /// (O jogo já não deixa quem não é dono da peça desmontar; isto é uma trava a mais.)</summary>
        void AoDesmontarPeca(CSteamID quem, byte x, byte y, ushort plant, ushort index, ref bool permitir)
        {
            if (!permitir || plant != ushort.MaxValue) return;
            if (!BarricadeManager.tryGetRegion(x, y, plant, out var reg) || index >= reg.drops.Count) return;
            var drop = reg.drops[index];
            var t = drop.model;
            if (!BarricadaEhNpc(t) && !PecaMontada(t)) return;
            var sp = PlayerTool.getSteamPlayer(quem);
            if (sp != null && (sp.isAdmin || EhDono(quem.m_SteamID)))
            {
                if (drop.asset?.id == 51249) TirarAtmsPerto(t.position, 4f);
                pecasMontadas = null;
                return;
            }
            permitir = false;
            if (sp?.player != null) Chat.Erro(UnturnedPlayer.FromSteamPlayer(sp), "Essa peca faz parte da base. So o dono do servidor tira.");
        }

        /// <summary>Tira do config os ATMs a menos de "raio" metros. Devolve quantos.</summary>
        public int TirarAtmsPerto(Vector3 pos, float raio)
        {
            var cfg = Configuration.Instance;
            int n = cfg.Atms.RemoveAll(a => (new Vector3(a.X, a.Y, a.Z) - pos).sqrMagnitude < raio * raio);
            if (n > 0) Configuration.Save();
            return n;
        }

        /// <summary>Tira a máquina de ATM (peça física) mais perto, até "raio" metros. Devolve true se tirou.</summary>
        public bool TirarMaquinaAtm(Vector3 pos, float raio)
        {
            BarricadeDrop melhor = null; float dMin = raio * raio;
            foreach (var r in BarricadeManager.regions)
                foreach (var d in r.drops)
                    if (d.asset?.id == 51249 && d.model != null && (d.model.position - pos).sqrMagnitude < dMin) { melhor = d; dMin = (d.model.position - pos).sqrMagnitude; }
            if (melhor == null || !BarricadeManager.tryGetRegion(melhor.model, out var x, out var y, out var plant, out _)) return false;
            BarricadeManager.destroyBarricade(melhor, x, y, plant);
            pecasMontadas = null;
            return true;
        }

        // ---------- entrar / sair da safe (contagem de saída + escudo na tela) ----------

        /// <summary>Escudo da safe no canto esquerdo (efeito DZ_Safe do mod; se ainda não existir, só usa o aviso do jogo).</summary>
        public const ushort SafeHudId = 51295;
        public const short SafeHudKey = 5128;
        readonly Dictionary<ulong, float> saiuSafeEm = new Dictionary<ulong, float>();
        readonly HashSet<ulong> escudoNaTela = new HashSet<ulong>();

        /// <summary>Dentro da safe ou ainda na contagem de saída.</summary>
        /// <summary>Marca a safe mais perto no mapa (tecla M) do jogador. Só se ele não tiver um marcador dele.</summary>
        /// <summary>Ao entrar: as safes o mapa do jogo já mostra, então marca o mercado negro mais perto (ou o caçador)
        /// e avisa no chat como marcar os dois. O mapa do California 2 não aceita ícone fixo do servidor.</summary>
        public void MarcarSafeNoMapa(UnturnedPlayer p)
        {
            if (p?.Player?.quests == null || p.Dead) return;
            var areas = AreasDoMapa();
            bool temNegro = areas.Any(a => a.Tipo == "negro"), temCacador = areas.Any(a => a.Tipo == "cacador");
            if (!temNegro && !temCacador) return;
            if (!p.Player.quests.isMarkerPlaced) MarcarArea(p, temNegro ? "negro" : "cacador");
            var partes = new List<string>();
            if (temNegro) partes.Add(Chat.Cor("#FF5A00", "/marcar negro") + " (mercado negro)");
            if (temCacador) partes.Add(Chat.Cor("#3DDC84", "/marcar cacador") + " (cacador)");
            Chat.Info(p, "Aperte M pra ver no mapa. Pra marcar: " + string.Join(" e ", partes) + ".");
        }

        public bool ProtegidoSafe(Player pl)
        {
            if (pl == null) return false;
            if (ZonaSeguraEm(pl.transform.position) != null) return true;
            return saiuSafeEm.TryGetValue(pl.channel.owner.playerID.steamID.m_SteamID, out var t)
                && Time.realtimeSinceStartup - t < S.SafeSaidaSegundos;
        }

        void Escudo(SteamPlayer sp, string texto)
        {
            var id = sp.playerID.steamID.m_SteamID;
            if (texto == null)
            {
                if (escudoNaTela.Remove(id)) EffectManager.askEffectClearByID(SafeHudId, sp.transportConnection);
                return;
            }
            if (!(Assets.find(EAssetType.EFFECT, SafeHudId) is EffectAsset efeito)) return;
            if (escudoNaTela.Add(id)) EffectManager.SendUIEffect(efeito, SafeHudKey, sp.transportConnection, true);
            EffectManager.sendUIEffectText(SafeHudKey, sp.transportConnection, true, "Safe_Texto", texto);
        }

        float proximaLimpezaArvores;

        /// <summary>Árvores que nascem de novo dentro das safes são cortadas (senão crescem no meio das cercas e NPCs).</summary>
        void LimparArvoresSafes()
        {
            var regs = new List<RegionCoordinate>();
            var arvores = new List<Transform>();
            foreach (var z in Configuration.Instance.ZonasSeguras ?? new List<Ponto>())
            {
                if (!z.Definido || z.Raio <= 0) continue;
                var c = new Vector3(z.X, z.Y, z.Z);
                regs.Clear(); arvores.Clear();
                Regions.getRegionsInRadius(c, z.Raio, regs);
                ResourceManager.getResourcesInRadius(c, z.Raio * z.Raio, regs, arvores);
                foreach (var a in arvores)
                    if (ResourceManager.tryGetRegion(a, out var x, out var y, out var i))
                        ResourceManager.ServerSetResourceDead(x, y, i, Vector3.zero);
            }
        }

        void TickSafe()
        {
            if (Time.realtimeSinceStartup >= proximaLimpezaArvores)
            {
                proximaLimpezaArvores = Time.realtimeSinceStartup + 60f;
                try { LimparArvoresSafes(); } catch (System.Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
            }
            bool avisar = Configuration.Instance.AvisarEntradaSafe;
            foreach (var sp in Provider.clients)
            {
                if (sp?.player == null) continue;
                var id = sp.playerID.steamID.m_SteamID;
                var zona = ZonaSeguraEm(sp.player.transform.position)?.Nome;
                zonaAtual.TryGetValue(id, out var antes);
                var p = UnturnedPlayer.FromSteamPlayer(sp);
                if (zona != antes)
                {
                    zonaAtual[id] = zona;
                    if (zona != null)
                    {
                        bool voltou = saiuSafeEm.Remove(id);
                        Escudo(sp, "SAFE ZONE");
                        if (!avisar) continue;
                        if (voltou && antes == null)
                        {
                            sp.player.ServerShowHint("<color=#55E07A><b>DE VOLTA NA SAFE ZONE</b></color>\nContagem cancelada.", 3f);
                            continue;
                        }
                        Chat.Ok(p, $"Voce entrou na SAFE ZONE ({Chat.Limpar(zona)}). PvP desligado aqui.");
                        Chat.Erro(p, "Regra da safe: proibido mexer nos carros e itens de outro jogador. Quem fizer leva ban.");
                        sp.player.ServerShowHint("<color=#55E07A><b>VOCE ENTROU NA SAFE ZONE</b></color>\nPvP desligado aqui.\n\n<color=#FF6A00><b>REGRA:</b></color> proibido mexer nos carros\ne itens de outro jogador.", S.SafeAvisoSegundos);
                        continue;
                    }
                    // saiu: começa a contagem (se estava na safe de verdade)
                    if (antes != null && S.SafeSaidaSegundos > 0) saiuSafeEm[id] = Time.realtimeSinceStartup;
                    else { PvpLiberado(sp, p, avisar); continue; }
                }
                if (zona != null || !saiuSafeEm.TryGetValue(id, out var t)) continue;
                int resta = S.SafeSaidaSegundos - Mathf.FloorToInt(Time.realtimeSinceStartup - t);
                if (resta > 0)
                {
                    Escudo(sp, $"SAINDO {resta}s");
                    if (avisar) sp.player.ServerShowHint($"<color=#FFB020><b>SAINDO DA SAFE ZONE</b></color>\nProtecao acaba em <b>{resta}s</b>\nVolte pra dentro pra cancelar.", 1.3f);
                }
                else
                {
                    saiuSafeEm.Remove(id);
                    PvpLiberado(sp, p, avisar);
                }
            }
        }

        void PvpLiberado(SteamPlayer sp, UnturnedPlayer p, bool avisar)
        {
            Escudo(sp, null);
            if (!avisar) return;
            Chat.Erro(p, "Voce saiu da safe zone. PvP LIBERADO! Cuidado.");
            sp.player.ServerShowHint("<color=#FF4040><b>VOCE SAIU DA SAFE ZONE</b></color>\nPvP liberado. Cuidado!", 4f);
        }

        // ---- regra da safe: ninguém mexe no carro dos outros ----
        static bool DonoDoVeiculo(InteractableVehicle v, Player p)
        {
            if (v.lockedOwner == CSteamID.Nil || v.lockedOwner == p.channel.owner.playerID.steamID) return true;
            var g = p.quests.groupID;
            return g != CSteamID.Nil && v.lockedGroup == g;
        }

        bool BloquearNaSafe(InteractableVehicle v, Player p)
        {
            if (!S.SafeProtegeVeiculos || v == null || p == null || !NaSafe(v.transform.position) || DonoDoVeiculo(v, p)) return false;
            if (p.channel.owner.isAdmin) return false;
            p.ServerShowHint("<color=#FF4040><b>PROIBIDO NA SAFE</b></color>\nNao mexa no carro de outro jogador.", 4f);
            return true;
        }

        void AoArrombarVeiculo(InteractableVehicle v, Player p, ref bool permitir) { if (permitir && BloquearNaSafe(v, p)) permitir = false; }
        void AoRoubarVeiculo(InteractableVehicle v, Player p, ref bool permitir, ref Vector3 forca, ref Vector3 torque) { if (permitir && BloquearNaSafe(v, p)) permitir = false; }
        void AoSifonar(InteractableVehicle v, Player p, ref bool permitir, ref ushort quanto) { if (permitir && BloquearNaSafe(v, p)) permitir = false; }
        void AoEntrarVeiculo(Player p, InteractableVehicle v, ref bool permitir) { if (permitir && BloquearNaSafe(v, p)) permitir = false; }

        bool PodeConstruirNaSafe(ulong dono)
        {
            var sp = PlayerTool.getSteamPlayer(dono);
            return sp != null && (sp.isAdmin || UnturnedPlayer.FromSteamPlayer(sp).IsAdmin);
        }

        void AvisarConstrucao(ulong dono)
        {
            var sp = PlayerTool.getSteamPlayer(dono);
            if (sp?.player != null) Chat.Erro(UnturnedPlayer.FromSteamPlayer(sp), "Nao pode construir dentro da safe zone.");
        }

        void AoColocarBarricada(Barricade b, ItemBarricadeAsset a, Transform hit, ref Vector3 ponto,
            ref float ax, ref float ay, ref float az, ref ulong dono, ref ulong grupo, ref bool permitir)
        {
            if (!permitir || !S.SafeSemConstrucao || hit != null && hit.GetComponentInParent<InteractableVehicle>() != null) return;
            if (NaSafe(ponto) && !PodeConstruirNaSafe(dono)) { permitir = false; AvisarConstrucao(dono); }
        }

        void AoColocarEstrutura(Structure s, ItemStructureAsset a, ref Vector3 ponto,
            ref float ax, ref float ay, ref float az, ref ulong dono, ref ulong grupo, ref bool permitir)
        {
            if (!permitir || !S.SafeSemConstrucao) return;
            if (NaSafe(ponto) && !PodeConstruirNaSafe(dono)) { permitir = false; AvisarConstrucao(dono); }
        }

        void AoDanoBarricada(CSteamID quem, Transform t, ref ushort dano, ref bool permitir, EDamageOrigin origem)
        {
            if (permitir && t != null && (BarricadaEhNpc(t) || PecaMontada(t))) { permitir = false; return; }
            if (permitir && S.SafeProtegeBases && t != null && NaSafe(t.position)) permitir = false;
            DanoBarricadaRaid(quem, t, ref permitir);
        }

        void AoDanoEstrutura(CSteamID quem, Transform t, ref ushort dano, ref bool permitir, EDamageOrigin origem)
        {
            if (permitir && S.SafeProtegeBases && t != null && NaSafe(t.position)) permitir = false;
            DanoEstruturaRaid(quem, t, ref permitir);
        }

        void AoDanoVeiculo(CSteamID quem, InteractableVehicle v, ref ushort dano, ref bool reparar, ref bool permitir, EDamageOrigin origem)
        {
            if (permitir && S.SafeProtegeVeiculos && v != null && NaSafe(v.transform.position)) permitir = false;
        }

        void AoDanoPneu(CSteamID quem, InteractableVehicle v, int pneu, ref bool permitir, EDamageOrigin origem)
        {
            if (permitir && S.SafeProtegeVeiculos && v != null && NaSafe(v.transform.position)) permitir = false;
        }

        // ---------- status.json (site e Discord) ----------

        void TickStatus()
        {
            if (--segundosStatus > 0) return;
            segundosStatus = 15;
            try
            {
                var sb = new StringBuilder();
                sb.Append("{\"online\":true");
                sb.Append(",\"nome\":").Append(Json(Provider.serverName));
                sb.Append(",\"mapa\":").Append(Json(Level.info?.name ?? ""));
                sb.Append(",\"max\":").Append(Provider.maxPlayers);
                sb.Append(",\"atualizado\":").Append(Estatisticas.Agora);
                sb.Append(",\"jogadores\":[");
                bool primeiro = true;
                foreach (var sp in Provider.clients)
                {
                    if (sp?.player == null) continue;
                    var f = Stats.Obter(sp.playerID.steamID.m_SteamID);
                    var pat = PatenteDe(f.Xp);
                    if (!primeiro) sb.Append(',');
                    primeiro = false;
                    sb.Append("{\"id\":\"").Append(f.Id).Append("\",\"nome\":").Append(Json(sp.playerID.characterName))
                      .Append(",\"patente\":").Append(Json(pat.Nome)).Append(",\"cor\":").Append(Json(pat.Cor))
                      .Append(",\"nivel\":").Append(f.Nivel).Append(",\"abates\":").Append(f.Abates).Append('}');
                }
                sb.Append("]}");
                var arq = Path.Combine(Directory, "status.json");
                File.WriteAllText(arq + ".tmp", sb.ToString());
                if (File.Exists(arq)) File.Delete(arq);
                File.Move(arq + ".tmp", arq);
            }
            catch (System.Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }

        static string Json(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (var c in s ?? "")
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
                else sb.Append(c);
            }
            return sb.Append('"').ToString();
        }
    }
}
