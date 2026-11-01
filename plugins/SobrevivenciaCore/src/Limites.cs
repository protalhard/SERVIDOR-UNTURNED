using System.Linq;
using SDG.Unturned;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Limite de aeronaves soltas no mapa (helis e aviões que nasceram sozinhos, sem dono).
    /// Passou do limite: some a que estiver mais longe de qualquer jogador. Os comprados (trancados) não contam.</summary>
    public partial class Plugin
    {
        float proxLimiteVeiculos = 120f;

        void TickLimiteVeiculos()
        {
            if (S.LimiteAeronavesSoltas <= 0 || Time.realtimeSinceStartup < proxLimiteVeiculos) return;
            proxLimiteVeiculos = Time.realtimeSinceStartup + 60f;
            var soltas = VehicleManager.vehicles
                .Where(v => v != null && !v.isDead && !v.isExploded && v.asset != null && !v.isLocked && !v.anySeatsOccupied &&
                            (v.asset.engine == EEngine.HELICOPTER || v.asset.engine == EEngine.PLANE))
                .ToList();
            if (soltas.Count <= S.LimiteAeronavesSoltas) return;
            float Perto(InteractableVehicle v)
            {
                float m = float.MaxValue;
                foreach (var sp in Provider.clients)
                    if (sp?.player != null) m = Mathf.Min(m, (sp.player.transform.position - v.transform.position).sqrMagnitude);
                return m;
            }
            // tira as mais longe de gente (ninguém vê sumir), sem chegar a menos de 150 m de um jogador
            foreach (var v in soltas.OrderByDescending(Perto).Take(soltas.Count - S.LimiteAeronavesSoltas).ToList())
            {
                if (Perto(v) < 150f * 150f) continue;
                Rocket.Core.Logging.Logger.Log($"[LIMITE] tirou aeronave solta {v.asset.vehicleName} em {v.transform.position.x:0} {v.transform.position.z:0}");
                VehicleManager.askVehicleDestroy(v);
            }
        }
    }
}
