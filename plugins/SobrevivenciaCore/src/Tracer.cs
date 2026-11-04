using SDG.Unturned;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Bala traçante: a cada tiro o plugin dispara o efeito DZ_Tracer (mod DZ Menu) saindo da arma na
    /// direção do tiro, pra quem está perto ver por onde a bala foi. Pente que já tem traçante próprio, e cano com
    /// silenciador/abafador, ficam como o jogo faz. Sem o efeito no mod, não faz nada.</summary>
    public partial class Plugin
    {
        bool tracerLigado;

        void LigarTracer()
        {
            if (tracerLigado) return;
            UseableGun.onBulletSpawned += AoTiro;
            tracerLigado = true;
        }

        void DesligarTracer()
        {
            if (!tracerLigado) return;
            UseableGun.onBulletSpawned -= AoTiro;
            tracerLigado = false;
        }

        EffectAsset TracerAsset => S.TracerEfeito == 0 ? null : Assets.find(EAssetType.EFFECT, S.TracerEfeito) as EffectAsset;

        void AoTiro(UseableGun arma, BulletInfo bala)
        {
            try
            {
                if (bala == null || bala.pellet > 0) return; // escopeta: só um rastro por tiro
                if (bala.magazineAsset != null && bala.magazineAsset.FindTracerEffectAsset() != null) return;
                if (bala.barrelAsset != null && (bala.barrelAsset.isBraked || bala.barrelAsset.isSilenced)) return;
                var asset = TracerAsset;
                if (asset == null) return;
                var dir = bala.GetDirection();
                if (dir.sqrMagnitude < 0.001f) return;
                var t = new TriggerEffectParameters(asset)
                {
                    position = bala.origin + dir.normalized * 0.6f,
                    relevantDistance = S.TracerDistancia,
                    reliable = false,
                };
                t.SetDirection(dir.normalized);
                EffectManager.triggerEffect(t);
            }
            catch { }
        }
    }
}
