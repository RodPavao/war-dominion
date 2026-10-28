using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public readonly struct WDReinforcementResolutionLabStyle
{
    public readonly bool Enabled;
    public readonly float EntryDuration, ReactionDuration, ExitDuration;
    public readonly float InitialScale, PulseSize, PulseOpacity;
    public readonly float IndicatorOffset, TextSize;
    public readonly Color TextColor;
    public readonly float TextOpacity, OutlineWidth, OutlineOpacity;
    public readonly float RiseDistance;

    public WDReinforcementResolutionLabStyle(bool enabled, float entry,
        float reaction, float exit, float initialScale, float pulseSize,
        float pulseOpacity, float indicatorOffset, float textSize,
        Color textColor, float textOpacity, float outlineWidth,
        float outlineOpacity, float riseDistance = 24f)
    {
        Enabled = enabled;
        EntryDuration = Mathf.Max(.05f, entry);
        ReactionDuration = Mathf.Max(0f, reaction);
        ExitDuration = Mathf.Max(.05f, exit);
        InitialScale = Mathf.Clamp(initialScale, .25f, 2f);
        PulseSize = Mathf.Max(8f, pulseSize);
        PulseOpacity = Mathf.Clamp01(pulseOpacity);
        IndicatorOffset = indicatorOffset;
        TextSize = Mathf.Max(8f, textSize);
        TextColor = textColor;
        TextOpacity = Mathf.Clamp01(textOpacity);
        OutlineWidth = Mathf.Clamp(outlineWidth, 0f, 1f);
        OutlineOpacity = Mathf.Clamp01(outlineOpacity);
        RiseDistance = Mathf.Max(0f, riseDistance);
    }
}

[DisallowMultipleComponent]
public sealed class ReinforcementResolutionPresenter : MonoBehaviour,
    IResolutionVisualPresenter, IResolutionVisualCleanup
{
    // ============================================================
    // 01. CONFIGURAÇÃO E SUPORTE
    // ============================================================

    private WarDominionUITheme theme;
    private GameObject activeRoot;
    private WDReinforcementResolutionLabStyle labStyle;
    private Image activePulse;
    private TextMeshProUGUI activeIndicator;
    private Vector2 activePosition;
    private float activeRiseProgress;
    private WDGameplayGlassStyle glassStyle;
    private Image activeGlassSurface;
    private Image activeGlassReflection;
    private Color activeIdentityColor = Color.white;

    public void Configure(WarDominionUITheme newTheme) => theme = newTheme;

    public void ConfigureLabStyle(WDReinforcementResolutionLabStyle value)
    {
        labStyle = value;
        ApplyActiveLabStyle();
    }

    // 01.1. ACABAMENTO GLASS — não altera duração, valor ou trajetória.
    public void ConfigureLabGlassStyle(WDGameplayGlassStyle value)
    {
        glassStyle = value;
        ApplyActiveLabStyle();
    }

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
        activeIdentityColor = color;
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
        indicator.fontStyle = FontStyles.Bold;
        indicator.rectTransform.anchoredPosition =
            position + Vector2.up * theme.ReinforcementIndicatorOffset;
        activePulse = pulse;
        activeIndicator = indicator;
        activePosition = position;
        activeRiseProgress = 0f;
        EnsureGlassSurface(root);
        ApplyActiveLabStyle();

        // 02.1. No laboratório, cada acionamento precisa refletir a tropa no
        // contador imediatamente; a partida oficial mantém o marco histórico
        // depois da entrada porque labStyle permanece desabilitado nela.
        if (labStyle.Enabled)
            territory.AplicarEstadoVisualResolucao(
                visualEvent.DestinationAfter, visualEvent.NewOwner);
        yield return AnimateEntry(indicator, pulseRect, context);
        if (!labStyle.Enabled)
            territory.AplicarEstadoVisualResolucao(
                visualEvent.DestinationAfter, visualEvent.NewOwner);
        if (theme.PremiumGameplayFeedbackEnabled)
            territory.DestacarReforco();
        yield return AnimateExit(indicator, pulse, context);
        Destroy(root.gameObject);
        activeRoot = null;
        activePulse = null;
        activeIndicator = null;
        activeGlassSurface = null;
        activeGlassReflection = null;
        activeRiseProgress = 0f;
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
        float duration = labStyle.Enabled ? labStyle.EntryDuration
            : theme.ReinforcementEntryDuration;
        while (elapsed < duration)
        {
            elapsed += context.PlaybackDeltaTime;
            duration = labStyle.Enabled ? labStyle.EntryDuration
                : theme.ReinforcementEntryDuration;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = theme.PremiumGameplayFeedbackEnabled
                ? 1f - Mathf.Pow(1f - progress, 3f) : progress;
            activeRiseProgress = eased * .25f;
            ApplyActiveIndicatorPosition();
            indicator.alpha = eased * (labStyle.Enabled
                ? labStyle.TextOpacity : 1f);
            indicator.transform.localScale = Vector3.one * Mathf.Lerp(
                labStyle.Enabled ? labStyle.InitialScale
                    : theme.ReinforcementInitialScale, 1f, eased);
            pulse.localScale = Vector3.one * Mathf.Lerp(0.65f,
                theme.PremiumGameplayFeedbackEnabled && theme.ReducedGameplayMotion
                    ? 1.02f : 1.1f, eased);
            yield return null;
        }

        float holdElapsed = 0f;
        float holdDuration = labStyle.Enabled ? labStyle.ReactionDuration
            : theme.ReinforcementReactionDuration;
        while (holdElapsed < holdDuration)
        {
            holdElapsed += context.PlaybackDeltaTime;
            holdDuration = labStyle.Enabled ? labStyle.ReactionDuration
                : theme.ReinforcementReactionDuration;
            float holdProgress = holdDuration <= 0f ? 1f
                : Mathf.Clamp01(holdElapsed / holdDuration);
            activeRiseProgress = Mathf.Lerp(.25f, .55f,
                holdProgress * holdProgress * (3f - 2f * holdProgress));
            ApplyActiveIndicatorPosition();
            yield return null;
        }
    }

    private IEnumerator AnimateExit(
        TextMeshProUGUI indicator,
        Image pulse,
        ResolutionVisualPresentationContext context)
    {
        float elapsed = 0f;
        float duration = labStyle.Enabled ? labStyle.ExitDuration
            : theme.ReinforcementExitDuration;
        while (elapsed < duration)
        {
            elapsed += context.PlaybackDeltaTime;
            duration = labStyle.Enabled ? labStyle.ExitDuration
                : theme.ReinforcementExitDuration;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = theme.PremiumGameplayFeedbackEnabled
                ? progress * progress * progress : progress;
            activeRiseProgress = Mathf.Lerp(.55f, 1f,
                progress * progress * (3f - 2f * progress));
            ApplyActiveIndicatorPosition();
            indicator.alpha = (1f - eased) * (labStyle.Enabled
                ? labStyle.TextOpacity : 1f);
            SetGlassAlpha((1f - eased) * (labStyle.Enabled
                ? labStyle.TextOpacity : 1f));
            Color pulseColor = pulse.color;
            pulseColor.a = (1f - eased) * (labStyle.Enabled
                ? labStyle.PulseOpacity : theme.ReinforcementPulseOpacity);
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
        activePulse = null;
        activeIndicator = null;
        activeGlassSurface = null;
        activeGlassReflection = null;
        activeRiseProgress = 0f;
    }

    // 05. AJUSTE AO VIVO — somente objetos temporários já criados no lab.
    private void ApplyActiveLabStyle()
    {
        if (!labStyle.Enabled)
            return;
        if (activePulse != null)
        {
            activePulse.rectTransform.sizeDelta = Vector2.one * labStyle.PulseSize;
            Color color = activePulse.color;
            color.a = labStyle.PulseOpacity;
            activePulse.color = color;
        }
        if (activeIndicator != null)
        {
            activeIndicator.fontSize = labStyle.TextSize;
            Color textColor = labStyle.TextColor;
            textColor.a = 1f;
            activeIndicator.color = textColor;
            activeIndicator.alpha = labStyle.TextOpacity;
            // 05.1. O contorno adapta-se à luminância do texto: grafite recebe
            // separação clara e textos claros recebem uma sombra escura.
            float luminance = textColor.r * .2126f + textColor.g * .7152f +
                textColor.b * .0722f;
            Color outline = luminance < .45f
                ? new Color(.86f, .91f, .94f, labStyle.OutlineOpacity)
                : new Color(.02f, .03f, .04f, labStyle.OutlineOpacity);
            activeIndicator.outlineColor = outline;
            activeIndicator.outlineWidth = labStyle.OutlineWidth;
            ApplyActiveIndicatorPosition();
        }
        if (activeGlassSurface != null)
        {
            activeGlassSurface.gameObject.SetActive(glassStyle.Enabled);
            Color surface = Color.Lerp(new Color(.025f, .04f, .055f),
                activeIdentityColor, .10f * glassStyle.ProfiledGradientStrength);
            surface.a = Mathf.Lerp(.74f, .92f, glassStyle.ProfiledGlassIntensity);
            activeGlassSurface.color = surface;
            Outline border = activeGlassSurface.GetComponent<Outline>();
            if (border != null)
            {
                border.effectColor = new Color(.84f, .88f, .92f,
                    Mathf.Lerp(.54f, .9f, glassStyle.ProfiledBevelStrength));
                border.effectDistance = Vector2.one * Mathf.Lerp(.7f, 1.4f,
                    glassStyle.ProfiledBevelStrength);
            }
        }
        if (activeGlassReflection != null)
        {
            activeGlassReflection.color = new Color(1f, 1f, 1f,
                .12f * glassStyle.ProfiledReflectionIntensity);
            activeGlassReflection.gameObject.SetActive(glassStyle.Enabled &&
                glassStyle.ProfiledReflectionIntensity > .001f);
        }
    }

    // 05.2. A posicao ao vivo conserva o progresso da animacao. Assim, uma
    // atualizacao do Inspector nao devolve o texto ao ponto inicial a cada frame.
    private void ApplyActiveIndicatorPosition()
    {
        if (activeIndicator == null || !labStyle.Enabled)
            return;
        activeIndicator.rectTransform.anchoredPosition = activePosition +
            Vector2.up * (labStyle.IndicatorOffset +
                labStyle.RiseDistance * activeRiseProgress);
        if (activeGlassSurface != null)
            activeGlassSurface.rectTransform.anchoredPosition =
                activeIndicator.rectTransform.anchoredPosition;
    }

    private void EnsureGlassSurface(RectTransform root)
    {
        if (activeGlassSurface != null)
            return;
        var surfaceObject = new GameObject("ReinforcementGlass",
            typeof(RectTransform), typeof(Image), typeof(Outline));
        RectTransform rect = surfaceObject.GetComponent<RectTransform>();
        rect.SetParent(root, false);
        rect.sizeDelta = new Vector2(48f, 30f);
        activeGlassSurface = surfaceObject.GetComponent<Image>();
        activeGlassSurface.raycastTarget = false;
        WDHomeUIFactory.ApplyRounded(activeGlassSurface, 13);
        var reflectionObject = new GameObject("Reflection",
            typeof(RectTransform), typeof(Image));
        RectTransform reflection = reflectionObject.GetComponent<RectTransform>();
        reflection.SetParent(rect, false);
        reflection.anchorMin = new Vector2(.16f, .58f);
        reflection.anchorMax = new Vector2(.84f, .84f);
        reflection.offsetMin = reflection.offsetMax = Vector2.zero;
        activeGlassReflection = reflectionObject.GetComponent<Image>();
        activeGlassReflection.raycastTarget = false;
        WDHomeUIFactory.ApplyRounded(activeGlassReflection, 7);
        activeGlassSurface.transform.SetSiblingIndex(
            Mathf.Max(0, activeIndicator.transform.GetSiblingIndex()));
        activeIndicator.transform.SetAsLastSibling();
    }

    private void SetGlassAlpha(float value)
    {
        if (activeGlassSurface == null || !glassStyle.Enabled)
            return;
        Color surface = activeGlassSurface.color;
        surface.a = Mathf.Lerp(.74f, .92f, glassStyle.ProfiledGlassIntensity) *
            Mathf.Clamp01(value);
        activeGlassSurface.color = surface;
        if (activeGlassReflection != null)
        {
            Color reflection = activeGlassReflection.color;
            reflection.a = .12f * glassStyle.ProfiledReflectionIntensity *
                Mathf.Clamp01(value);
            activeGlassReflection.color = reflection;
        }
    }
}
