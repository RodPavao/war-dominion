using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class InstallationResolutionPresenter : MonoBehaviour, IResolutionVisualPresenter
{
    private const float BadgeFadeDuration = .38f;
    private WarDominionUITheme theme;

    // ============================================================
    // 01. SINAL VISUAL REUTILIZÁVEL, DERIVADO DO EVENTO DE RESOLUTION
    // ============================================================

    public void Configure(WarDominionUITheme value) => theme = value;

    public bool Supports(ResolutionVisualEventType type) =>
        type == ResolutionVisualEventType.Installation;

    public IEnumerator Present(ResolutionVisualEvent visualEvent,
        ResolutionVisualPresentationContext context)
    {
        if (context?.Overlay == null || theme == null)
        {
            Debug.LogError("[InstallationResolution] Contexto visual indisponível.", this);
            yield break;
        }

        if (!WDWeaponInstallationMapPresenter.TryBeginInstallationFade(
                visualEvent.InstallationId, visualEvent.ContentId,
                visualEvent.DestinationTerritoryId,
                out string installationId))
        {
            Debug.LogWarning("[InstallationResolution] Badge territorial não encontrado " +
                $"para '{visualEvent.ContentId}' em " +
                $"'{visualEvent.DestinationTerritoryId}'.", this);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < BadgeFadeDuration)
        {
            elapsed += context.PlaybackDeltaTime;
            float progress = Mathf.Clamp01(elapsed / BadgeFadeDuration);
            float eased = progress * progress * (3f - 2f * progress);
            WDWeaponInstallationMapPresenter.SetInstallationFadeAlpha(
                installationId, eased);
            yield return null;
        }
        WDWeaponInstallationMapPresenter.SetInstallationFadeAlpha(installationId, 1f);
    }
}
