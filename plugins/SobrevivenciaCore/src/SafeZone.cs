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
        }

        void DesligarSafeZone()
        {
            BarricadeManager.onDeployBarricadeRequested -= AoColocarBarricada;
            StructureManager.onDeployStructureRequested -= AoColocarEstrutura;
            BarricadeManager.onDamageBarricadeRequested -= AoDanoBarricada;
            StructureManager.onDamageStructureRequested -= AoDanoEstrutura;
            VehicleManager.onDamageVehicleRequested -= AoDanoVeiculo;
            VehicleManager.onDamageTireRequested -= AoDanoPneu;
        }

        bool NaSafe(Vector3 pos) => ZonaSeguraEm(pos) != null;

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
        }

        void AoDanoEstrutura(CSteamID quem, Transform t, ref ushort dano, ref bool permitir, EDamageOrigin origem)
        {
            if (permitir && S.SafeProtegeBases && t != null && NaSafe(t.position)) permitir = false;
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
