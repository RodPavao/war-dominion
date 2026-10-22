using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TransferResolutionPresenter : MonoBehaviour,
    IResolutionVisualPresenter, IResolutionVisualCleanup
{
    // ============================================================
    // 01. CONFIGURAÇÃO E SUPORTE
    // ============================================================

    private WarDominionUITheme theme;
    private GameObject activeRoot;

    public void Configure(WarDominionUITheme newTheme) => theme = newTheme;

    public bool Supports(ResolutionVisualEventType type) =>
        type == ResolutionVisualEventType.Transfer;

    // ============================================================
    // 02. PASSAGEM AMIGÁVEL DE CONTROLE EM UM TERRITÓRIO
    // ============================================================

    public IEnumerator Present(
        ResolutionVisualEvent visualEvent,
        ResolutionVisualPresentationContext context)
    {
        if (theme == null || visualEvent == null || context == null ||
            !string.IsNullOrEmpty(visualEvent.OriginTerritoryId) ||
            !ResolutionVisualMapUtility.TryGetTerritory(
                visualEvent.DestinationTerritoryId, out TerritorioClique territory) ||
            !ResolutionVisualMapUtility.TryGetPosition(
                territory, context, out Vector2 position))
        {
            Debug.LogWarning(
                $"[ResolutionVisual] Passagem de controle inválida: {visualEvent}");
            yield break;
        }

        Color previousColor = context.GetPlayerColor(visualEvent.PreviousOwner);
        Color newColor = context.GetPlayerColor(visualEvent.NewOwner);
        RectTransform root = ResolutionVisualMapUtility.CreateRoot(
            "TransferControlResolution", context.Overlay);
        activeRoot = root.gameObject;
        Image pulse = ResolutionVisualMapUtility.CreateImage(
            "FriendlyControlPulse", root, previousColor,
            theme.TransferPulseOpacity);
        RectTransform pulseRect = (RectTransform)pulse.transform;
        pulseRect.anchoredPosition = position;
        pulseRect.sizeDelta = Vector2.one * theme.TransferPulseSize;
        pulseRect.localEulerAngles = new Vector3(0f, 0f, 45f);

        TextMeshProUGUI label = ResolutionVisualMapUtility.CreateText(
            "ProvisionalControlLabel", root, theme.FonteInterface,
            theme.TransferTextSize, newColor);
        label.text = "CONTROLE TRANSFERIDO";
        label.rectTransform.sizeDelta = new Vector2(260f, 40f);
        label.rectTransform.anchoredPosition =
            position + Vector2.up * theme.TransferTextOffset;
        label.alpha = 0f;

        yield return AnimateControlTransition(
            visualEvent, territory, pulse, label,
            previousColor, newColor, context);
        yield return AnimateReaction(pulse, label, context);
        Destroy(root.gameObject);
        activeRoot = null;
    }

    // ============================================================
    // 03. TROCA VISUAL NO MOMENTO CENTRAL, SEM MOVIMENTO DE TROPAS
    // ============================================================

    private IEnumerator AnimateControlTransition(
        ResolutionVisualEvent visualEvent,
        TerritorioClique territory,
        Image pulse,
        TextMeshProUGUI label,
        Color previousColor,
        Color newColor,
        ResolutionVisualPresentationContext context)
    {
        float elapsed = 0f;
        bool controlApplied = false;
        while (elapsed < theme.TransferTransitionDuration)
        {
            elapsed += context.PlaybackDeltaTime;
            float progress = Mathf.Clamp01(
                elapsed / theme.TransferTransitionDuration);

            if (!controlApplied && progress >= 0.5f)
            {
                controlApplied = true;
                territory.AplicarEstadoVisualResolucao(
                    visualEvent.DestinationAfter, visualEvent.NewOwner);
            }

            float eased = theme.PremiumGameplayFeedbackEnabled
                ? progress * progress * (3f - 2f * progress) : progress;
            pulse.color = Color.Lerp(previousColor, newColor, eased);
            Color pulseColor = pulse.color;
            pulseColor.a = theme.TransferPulseOpacity;
            pulse.color = pulseColor;
            pulse.transform.localScale = Vector3.one * Mathf.Lerp(
                0.72f, theme.PremiumGameplayFeedbackEnabled &&
                    theme.ReducedGameplayMotion ? 1.04f : 1.18f,
                Mathf.Sin(progress * Mathf.PI));
            label.alpha = Mathf.SmoothStep(0f, 1f, eased);
            yield return null;
        }

        if (!controlApplied)
        {
            territory.AplicarEstadoVisualResolucao(
                visualEvent.DestinationAfter, visualEvent.NewOwner);
        }
    }

    // ============================================================
    // 04. REAÇÃO FINAL DO CONTADOR E SAÍDA SUAVE
    // ============================================================

    private IEnumerator AnimateReaction(
        Image pulse,
        TextMeshProUGUI label,
        ResolutionVisualPresentationContext context)
    {
        float elapsed = 0f;
        while (elapsed < theme.TransferReactionDuration)
        {
            elapsed += context.PlaybackDeltaTime;
            float progress = Mathf.Clamp01(elapsed / theme.TransferReactionDuration);
            float eased = theme.PremiumGameplayFeedbackEnabled
                ? progress * progress * progress : progress;
            label.alpha = 1f - eased;
            Color color = pulse.color;
            color.a = theme.TransferPulseOpacity * (1f - eased);
            pulse.color = color;
            pulse.transform.localScale = Vector3.one * Mathf.Lerp(1f,
                theme.PremiumGameplayFeedbackEnabled && theme.ReducedGameplayMotion
                    ? 1.04f : 1.28f, eased);
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
