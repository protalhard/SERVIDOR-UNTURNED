using System;
using SDG.Unturned;

namespace SobrevivenciaCore
{
    /// <summary>Bombas de gasolina do /montar (safe): tanque de combustível que nunca acaba.
    /// O jogador segura o galão, usa na bomba pra encher e depois usa no carro/heli.</summary>
    public partial class Plugin
    {
        int segundosBomba;

        void TickBombas()
        {
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
