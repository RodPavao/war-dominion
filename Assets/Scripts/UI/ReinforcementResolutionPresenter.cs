using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ReinforcementResolutionPresenter : MonoBehaviour,
    IResolutionVisualPresenter, IResolutionVisualCleanup
{
    // ============================================================
    // 01. CONFIGURAÇÃO E SUPORTE
    // ============================================================

    private WarDominionUITheme theme;
    private GameObject activeRoot;

    public void Configure(WarDominionUITheme newTheme) => theme = newTheme;

    public bool Supports(ResolutionVisualEventType type) =>
        type == ResolutionVisualEventType.Reinforcement;

    // ============================================================
    // 02. CHEGADA AMIGÁVEL E ATUALIZAÇÃO DO CONTADOR
    // ============================================================

    public IEnumerator Present(
        ResolutionVisualEvent visualEvent,
        ResolutionVisualPresentationContext context)
    {
        if (theme == null || visualEvent == null || context == null ||
            !ResolutionVisualMapUtility.TryGetTerritory(
                visualEvent.DestinationTerritoryId, out TerritorioClique territory) ||
            !ResolutionVisualMapUtility.TryGetPosition(territory, context, out Vector2 position))
        {
            Debug.LogWarning(
                $"[ResolutionVisual] Reforço inválido ou território ausente: {visualEvent}");
            yield break;
        }

        Color color = context.GetPlayerColor(visualEvent.Player);
        RectTransform root = ResolutionVisualMapUtility.CreateRoot(
            "ReinforcementResolution", context.Overlay);
        activeRoot = root.gameObject;
        Image pulse = ResolutionVisualMapUtility.CreateImage(
            "FriendlyPulse", root, color, theme.ReinforcementPulseOpacity);
        RectTransform pulseRect = (RectTransform)pulse.transform;
        pulseRect.anchoredPosition = position;
        pulseRect.sizeDelta = Vector2.one * theme.ReinforcementPulseSize;
        pulseRect.localEulerAngles = new Vector3(0f, 0f, 45f);

        TextMeshProUGUI indicator = ResolutionVisualMapUtility.CreateText(
            "Amount", root, theme.FonteInterface,
            theme.ReinforcementTextSize, color);
        indicator.text = $"+{visualEvent.Amount}";
        indicator.rectTransform.anchoredPosition =
            position + Vector2.up * theme.ReinforcementIndicatorOffset;

        yield return AnimateEntry(indicator, pulseRect, context);
        territory.AplicarEstadoVisualResolucao(
            visualEvent.DestinationAfter, visualEvent.NewOwner);
        if (theme.PremiumGameplayFeedbackEnabled)
            territory.DestacarReforco();
        yield return AnimateExit(indicator, pulse, context);
        Destroy(root.gameObject);
        activeRoot = null;
    }

    // ============================================================
    // 03. ESCALA E FADE CONTROLADOS PELO PLAYBACK
    // ============================================================

    private IEnumerator AnimateEntry(
        TextMeshProUGUI indicator,
        RectTransform pulse,
        ResolutionVisualPresentationContext context)
    {
        float elapsed = 0f;
        while (elapsed < theme.ReinforcementEntryDuration)
        {
            elapsed += context.PlaybackDeltaTime;
            float progress = Mathf.Clamp01(elapsed / theme.ReinforcementEntryDuration);
            float eased = theme.PremiumGameplayFeedbackEnabled
                ? 1f - Mathf.Pow(1f - progress, 3f) : progress;
            indicator.alpha = eased;
            indicator.transform.localScale = Vector3.one * Mathf.Lerp(
                theme.ReinforcementInitialScale, 1f, eased);
            pulse.localScale = Vector3.one * Mathf.Lerp(0.65f,
                theme.PremiumGameplayFeedbackEnabled && theme.ReducedGameplayMotion
                    ? 1.02f : 1.1f, eased);
            yield return null;
        }

        yield return context.Wait(theme.ReinforcementReactionDuration);
    }

    private IEnumerator AnimateExit(
        TextMeshProUGUI indicator,
        Image pulse,
        ResolutionVisualPresentationContext context)
    {
        float elapsed = 0f;
        while (elapsed < theme.ReinforcementExitDuration)
        {
            elapsed += context.PlaybackDeltaTime;
            float progress = Mathf.Clamp01(elapsed / theme.ReinforcementExitDuration);
            float eased = theme.PremiumGameplayFeedbackEnabled
                ? progress * progress * progress : progress;
            indicator.alpha = 1f - eased;
            Color pulseColor = pulse.color;
            pulseColor.a = (1f - eased) * theme.ReinforcementPulseOpacity;
            pulse.color = pulseColor;
            yield return null;
        }
    }

    private void OnDisable()
    {
        ResetTransientVisuals();
    }

    public void ResetTransientVisuals()
    {
        if (activeRoot != null)
            Destroy(activeRoot);
        activeRoot = null;
    }
}
