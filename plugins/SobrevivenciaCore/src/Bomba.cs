using System;
using SDG.Unturned;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Bombas de gasolina do /montar (safe): tanque de combustível que nunca acaba.
    /// O jogador segura o galão, usa na bomba pra encher e depois usa no carro/heli.</summary>
    public partial class Plugin
    {
        int segundosBomba;

        float proxManterMontagens = 60f;

        /// <summary>Peças do /montar ficam "ativas" pra nunca sumirem pelo decay do Unturned (base abandonada).</summary>
        void ManterMontagens()
        {
            if (Time.realtimeSinceStartup < proxManterMontagens) return;
            proxManterMontagens = Time.realtimeSinceStartup + 1800f;
            PecaMontada(null); // carrega a lista
            if (pecasMontadas == null || pecasMontadas.Count == 0) return;
            var regioes = BarricadeManager.regions;
            if (regioes == null) return;
            int n = 0;
            foreach (var r in regioes)
                if (r != null)
                    foreach (var d in r.drops)
                    {
                        var dd = d.GetServersideData();
                        if (dd != null && pecasMontadas.Contains(dd.instanceID)) { dd.objActiveDate = Provider.time; n++; }
                    }
        }

        void TickBombas()
        {
            try { ManterMontagens(); } catch (System.Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
            if (++segundosBomba < 10) return;
            segundosBomba = 0;
            PecaMontada(null); // garante a lista de peças montadas carregada
            if (pecasMontadas == null || pecasMontadas.Count == 0 || BarricadeManager.regions == null) return;
            foreach (var reg in BarricadeManager.regions)
            {
                if (reg?.drops == null) continue;
                foreach (var d in reg.drops)
                {
                    if (!(d?.interactable is InteractableTank t) || t.source != ETankSource.FUEL || t.amount >= t.capacity) continue;
                    if (!pecasMontadas.Contains(d.instanceID)) continue;
                    t.ServerSetAmount(t.capacity);
                }
            }
        }
    }
}
