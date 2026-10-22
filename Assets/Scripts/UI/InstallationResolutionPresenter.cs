using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class InstallationResolutionPresenter : MonoBehaviour,
    IResolutionVisualPresenter, IResolutionVisualCleanup
{
    private const float BadgeFadeDuration = .38f;
    private WarDominionUITheme theme;
    private string activeInstallationId;

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
        activeInstallationId = installationId;

        float elapsed = 0f;
        while (elapsed < BadgeFadeDuration)
        {
            elapsed += context.PlaybackDeltaTime;
            float progress = Mathf.Clamp01(elapsed / BadgeFadeDuration);
            // 02. Fase 7.3A: a instalação usa o mesmo easing natural das
            // demais resoluções, mantendo a duração e o estado final anteriores.
            float eased = theme.PremiumGameplayFeedbackEnabled
                ? 1f - Mathf.Pow(1f - progress, 3f)
                : progress * progress * (3f - 2f * progress);
            WDWeaponInstallationMapPresenter.SetInstallationFadeAlpha(
                installationId, eased);
            yield return null;
        }
        WDWeaponInstallationMapPresenter.SetInstallationFadeAlpha(installationId, 1f);
        activeInstallationId = null;
    }

    public void ResetTransientVisuals()
    {
        if (!string.IsNullOrEmpty(activeInstallationId))
            WDWeaponInstallationMapPresenter.SetInstallationFadeAlpha(
                activeInstallationId, 1f);
        activeInstallationId = null;
    }

    private void OnDisable() => ResetTransientVisuals();
}
