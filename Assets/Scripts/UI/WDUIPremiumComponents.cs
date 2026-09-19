using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

public static class WDUIFactory
{
    // ============================================================
    // 01. FÁBRICA NATIVA SUBSTITUÍVEL POR SPRITES DEFINITIVOS
    // ============================================================

    public static RectTransform Rect(string name, Transform parent)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    public static TextMeshProUGUI Text(
        string name,
        Transform parent,
        WarDominionUITheme theme,
        float size,
        Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        var gameObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>();
        text.font = theme.FonteInterface;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }
}

[DisallowMultipleComponent]
public sealed class WDUIPanel : MonoBehaviour
{
    // ============================================================
    // 02. SUPERFÍCIE E BORDA SEMÂNTICAS
    // ============================================================

    public void Build(WarDominionUITheme theme, bool elevated = false)
    {
        Image surface = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        surface.color = elevated ? theme.BackgroundElevated : theme.SurfaceGlass;
        surface.raycastTarget = false;
        Outline outline = gameObject.GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
        outline.effectColor = theme.BorderNeutral;
        outline.effectDistance = Vector2.one * theme.PanelBorderWidth;
        outline.useGraphicAlpha = true;
    }
}

[DisallowMultipleComponent]
public sealed class WDUIPremiumButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    // ============================================================
    // 03. ESTADOS INTERATIVOS DO BOTÃO FINAL
    // ============================================================

    private Button button;
    private Image background;
    private TextMeshProUGUI label;
    private WarDominionUITheme theme;
    private bool selected;
    private bool hovered;
    private bool pressed;
    private bool hoverFeedbackEnabled = true;
    private bool useCustomColors;
    private Color customNormal;
    private Color customHover;
    private Coroutine deniedFeedback;

    public void Build(WarDominionUITheme newTheme, string text, UnityAction action)
    {
        theme = newTheme;
        background = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        button = gameObject.GetComponent<Button>() ?? gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.RemoveAllListeners();
        if (action != null) button.onClick.AddListener(action);
        label = WDUIFactory.Text(
            "Label", transform, theme, theme.TypeButton,
            theme.TextoPrincipal, TextAlignmentOptions.Center);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        label.text = text;
        LayoutElement layout = gameObject.GetComponent<LayoutElement>() ??
            gameObject.AddComponent<LayoutElement>();
        layout.minHeight = theme.ButtonHeight;
        layout.preferredHeight = theme.ButtonHeight;
        AtualizarVisual();
    }

    public void SetLabel(string text) { if (label != null) label.text = text; }
    public void SetInteractable(bool value)
    {
        if (button != null) button.interactable = value;
        AtualizarVisual();
    }
    public void SetSelected(bool value) { selected = value; AtualizarVisual(); }
    public bool HoverFeedbackEnabled => hoverFeedbackEnabled;
    public void SetHoverFeedbackEnabled(bool value)
    {
        hoverFeedbackEnabled = value;
        if (!value)
            hovered = false;
        AtualizarVisual();
    }
    public void SetPersistentColors(Color normal, Color hover)
    {
        useCustomColors = true;
        customNormal = normal;
        customHover = hover;
        AtualizarVisual();
    }

    public void PlayDeniedFeedback()
    {
        if (!isActiveAndEnabled)
            return;
        if (deniedFeedback != null)
            StopCoroutine(deniedFeedback);
        deniedFeedback = StartCoroutine(AnimateDeniedFeedback());
    }

    private IEnumerator AnimateDeniedFeedback()
    {
        transform.localScale = Vector3.one * 0.96f;
        yield return new WaitForSecondsRealtime(0.08f);
        transform.localScale = Vector3.one;
        deniedFeedback = null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!hoverFeedbackEnabled)
            return;
        hovered = true;
        AtualizarVisual();
    }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; AtualizarVisual(); }
    public void OnPointerDown(PointerEventData eventData) { pressed = true; AtualizarVisual(); }
    public void OnPointerUp(PointerEventData eventData) { pressed = false; AtualizarVisual(); }

    private void AtualizarVisual()
    {
        if (background == null || theme == null) return;
        bool enabled = button == null || button.interactable;
        Color color = !enabled ? theme.Disabled : useCustomColors
            ? (hovered ? customHover : customNormal)
            : selected ? theme.Acento : hovered ? theme.BackgroundElevated : theme.SurfaceGlass;
        if (pressed && enabled) color = Color.Lerp(color, Color.black, 0.18f);
        background.color = color;
        transform.localScale = Vector3.one * (pressed && enabled ? 0.975f : 1f);
        if (label != null) label.color = enabled ? theme.TextoPrincipal : theme.TextoSecundario;
    }
}

[DisallowMultipleComponent]
public sealed class WDUIChip : MonoBehaviour
{
    private TextMeshProUGUI label;

    // ============================================================
    // 04. CHIP/BADGE COM COR SEMÂNTICA
    // ============================================================

    public void Build(WarDominionUITheme theme)
    {
        Image background = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        background.raycastTarget = false;
        label = WDUIFactory.Text(
            "Label", transform, theme, theme.TypeMicro,
            theme.TextoPrincipal, TextAlignmentOptions.Center);
        RectTransform rect = label.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    public void Present(string text, Color color)
    {
        if (label == null) return;
        label.text = text;
        Image background = GetComponent<Image>();
        color.a = 0.82f;
        background.color = color;
    }
}

[DisallowMultipleComponent]
public sealed class WDUITooltipFoundation : MonoBehaviour
{
    // ============================================================
    // 05. CONTRATO DE TOOLTIP PARA CONTEÚDO FUTURO
    // ============================================================

    public string Content { get; private set; } = string.Empty;
    public void SetContent(string content) => Content = content ?? string.Empty;
}
