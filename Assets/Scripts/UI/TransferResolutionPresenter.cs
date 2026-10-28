using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public readonly struct WDTransferResolutionLabStyle
{
    public readonly bool Enabled;
    public readonly float TransitionDuration, ReactionDuration;
    public readonly WDTransferOwnershipBadgeStyle Badge;

    public WDTransferResolutionLabStyle(bool enabled, float transition,
        float reaction, WDTransferOwnershipBadgeStyle badge)
    {
        Enabled = enabled;
        TransitionDuration = Mathf.Max(.05f, transition);
        ReactionDuration = Mathf.Max(.05f, reaction);
        Badge = badge;
    }
}

[DisallowMultipleComponent]
public sealed class TransferResolutionPresenter : MonoBehaviour,
    IResolutionVisualPresenter, IResolutionVisualCleanup
{
    // ============================================================
    // 01. CONFIGURAÇÃO E SUPORTE
    // ============================================================

    private WarDominionUITheme theme;
    private GameObject activeRoot;
    private WDTransferResolutionLabStyle labStyle;
    private WDTransferOwnershipBadgeView activeBadge;
    private Vector2 activeBadgePosition;
    private Color activeBadgeColor;
    private WDGameplayGlassStyle glassStyle;

    public void Configure(WarDominionUITheme newTheme) => theme = newTheme;

    public void ConfigureLabStyle(WDTransferResolutionLabStyle value)
    {
        labStyle = value;
        ApplyActiveBadgeStyle();
    }

    public void ConfigureLabGlassStyle(WDGameplayGlassStyle value)
    {
        glassStyle = value;
        if (activeBadge != null)
            activeBadge.ConfigureGlassStyle(value);
        ApplyActiveBadgeStyle();
    }

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
        if (labStyle.Enabled)
        {
            yield return PresentLabBadge(visualEvent, territory, position,
                previousColor, newColor, context);
            yield break;
        }
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

    // 02.1. LABORATÓRIO — reutiliza o badge procedural já existente, sem
    // texto e sem o losango provisório da apresentação histórica.
    private IEnumerator PresentLabBadge(ResolutionVisualEvent visualEvent,
        TerritorioClique territory, Vector2 position, Color previousColor,
        Color newColor, ResolutionVisualPresentationContext context)
    {
        RectTransform root = ResolutionVisualMapUtility.CreateRoot(
            "TransferControlResolution_Lab", context.Overlay);
        activeRoot = root.gameObject;
        var badgeObject = new GameObject("TransferOwnershipBadge",
            typeof(RectTransform), typeof(CanvasGroup));
        badgeObject.transform.SetParent(root, false);
        activeBadge = badgeObject.AddComponent<WDTransferOwnershipBadgeView>();
        activeBadge.Build(theme, labStyle.Enabled);
        activeBadge.ConfigureGlassStyle(glassStyle);
        activeBadgePosition = position;
        activeBadgeColor = previousColor;
        ApplyActiveBadgeStyle();
        CanvasGroup group = badgeObject.GetComponent<CanvasGroup>();

        float elapsed = 0f;
        bool applied = false;
        while (elapsed < labStyle.TransitionDuration)
        {
            elapsed += context.PlaybackDeltaTime;
            float progress = Mathf.Clamp01(elapsed / labStyle.TransitionDuration);
            if (!applied && progress >= .5f)
            {
                applied = true;
                territory.AplicarEstadoVisualResolucao(
                    visualEvent.DestinationAfter, visualEvent.NewOwner);
            }
            activeBadgeColor = Color.Lerp(previousColor, newColor,
                progress * progress * (3f - 2f * progress));
            ApplyActiveBadgeStyle();
            group.alpha = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(progress * 2f));
            yield return null;
        }
        if (!applied)
            territory.AplicarEstadoVisualResolucao(
                visualEvent.DestinationAfter, visualEvent.NewOwner);

        elapsed = 0f;
        while (elapsed < labStyle.ReactionDuration)
        {
            elapsed += context.PlaybackDeltaTime;
            float progress = Mathf.Clamp01(elapsed / labStyle.ReactionDuration);
            group.alpha = 1f - progress * progress * progress;
            yield return null;
        }
        Destroy(root.gameObject);
        activeRoot = null;
        activeBadge = null;
    }

    private void ApplyActiveBadgeStyle()
    {
        if (!labStyle.Enabled || activeBadge == null)
            return;
        activeBadge.ConfigureGlassStyle(glassStyle);
        activeBadge.Configure(activeBadgeColor, labStyle.Badge);
        activeBadge.SetPosition(activeBadgePosition);
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
        activeBadge = null;
    }
}
