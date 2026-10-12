using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

public static class WDUIFactory
{
    private static TMP_FontAsset gabaritoSemiBold;

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
        text.font = ResolveInterfaceFont(theme);
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    // 01.1. A View da partida usa a fonte final do Figma sem depender de uma
    // troca manual no Inspector. O asset TMP é criado uma vez a partir do TTF.
    private static TMP_FontAsset ResolveInterfaceFont(WarDominionUITheme theme)
    {
        if (gabaritoSemiBold == null)
        {
            Font source = Resources.Load<Font>("UI/Fonts/Gabarito-SemiBold");
            if (source != null)
            {
                gabaritoSemiBold = TMP_FontAsset.CreateFontAsset(source);
                gabaritoSemiBold.name = "Gabarito SemiBold Runtime";
                gabaritoSemiBold.hideFlags = HideFlags.DontSave;
                ProtectInterfaceFontResources(null);
                TMPro_EventManager.TEXT_CHANGED_EVENT.Add(ProtectInterfaceFontResources);
                Application.quitting += ReleaseInterfaceFont;
#if UNITY_EDITOR
                UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ReleaseInterfaceFont;
#endif
            }
        }
        return gabaritoSemiBold != null ? gabaritoSemiBold : theme.FonteInterface;
    }

    // O cache possui fonte, material e todos os atlas dinâmicos, inclusive os
    // adicionados posteriormente pelo TMP. Nenhum deles pertence à cena da UI.
    private static void ProtectInterfaceFontResources(Object changed)
    {
        gabaritoSemiBold.material.hideFlags = HideFlags.DontSave;
        for (int i = 0; i < gabaritoSemiBold.atlasTextureCount; i++)
            gabaritoSemiBold.atlasTextures[i].hideFlags = HideFlags.DontSave;
    }

    private static void ReleaseInterfaceFont()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(ProtectInterfaceFontResources);
        Application.quitting -= ReleaseInterfaceFont;
#if UNITY_EDITOR
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ReleaseInterfaceFont;
#endif
        // TMP_FontAsset.OnDestroy libera o material e os atlas que ele possui.
        Object.DestroyImmediate(gabaritoSemiBold);
        gabaritoSemiBold = null;
    }
}

public static class WDMatchVisualStyle
{
    // 02. HIERARQUIA CANÔNICA DE SUPERFÍCIES DA PARTIDA
    public static readonly Color SurfaceMatchBase = new Color32(11, 14, 18, 224);
    public static readonly Color SurfaceMatchElevated = new Color32(11, 14, 18, 239);
    public static readonly Color SurfaceMatchModal = new Color32(11, 14, 18, 248);
    // 01.1. Corpo legível dos overlays V2; independente de backdrop e superfícies legadas.
    public static readonly Color SurfaceMatchReadable = new Color(11f / 255f, 14f / 255f, 18f / 255f, .95f);
    public static readonly Color SurfaceMatchControl = new Color32(18, 22, 28, 218);
    public static readonly Color BackdropMatch = new Color32(0, 0, 0, 184);
    public static readonly Color BorderMatchGold = new Color32(197, 155, 99, 255);
    public static readonly Color BorderMatchGoldSoft = new Color32(197, 155, 99, 122);
    public static readonly Color Surface = SurfaceMatchBase;
    public static readonly Color SurfaceElevated = SurfaceMatchElevated;
    public static readonly Color Gold = BorderMatchGold;
    public static readonly Color Muted = new Color32(142, 146, 154, 255);
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
        surface.color = elevated ? WDMatchVisualStyle.SurfaceMatchElevated :
            WDMatchVisualStyle.SurfaceMatchBase;
        surface.raycastTarget = false;
        WDHomeUIFactory.ApplyRounded(surface, 12);
        WDHomeUIFactory.AddRoundedBorder((RectTransform)transform,
            WDMatchVisualStyle.BorderMatchGold, 12);
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
    private bool useCustomLabelColor;
    private Color customLabelColor;
    private bool useCustomDisabledColor;
    private Color customDisabledColor;
    private Coroutine deniedFeedback;

    public void Build(WarDominionUITheme newTheme, string text, UnityAction action)
    {
        theme = newTheme;
        background = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        WDHomeUIFactory.ApplyRounded(background, 6);
        WDHomeUIFactory.AddRoundedBorder((RectTransform)transform, WDMatchVisualStyle.Gold, 6);
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
    public void SetLabelColor(Color color)
    {
        useCustomLabelColor = true;
        customLabelColor = color;
        AtualizarVisual();
    }
    public void SetDisabledColor(Color color)
    {
        useCustomDisabledColor = true;
        customDisabledColor = color;
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
        Color color = !enabled
            ? (useCustomDisabledColor ? customDisabledColor : theme.Disabled)
            : useCustomColors
            ? (hovered ? customHover : customNormal)
            : selected ? new Color(WDMatchVisualStyle.Gold.r, WDMatchVisualStyle.Gold.g,
                WDMatchVisualStyle.Gold.b, 0.28f)
            : hovered ? new Color(WDMatchVisualStyle.Gold.r, WDMatchVisualStyle.Gold.g,
                WDMatchVisualStyle.Gold.b, 0.16f)
            : WDMatchVisualStyle.SurfaceMatchControl;
        if (pressed && enabled) color = Color.Lerp(color, Color.black, 0.18f);
        background.color = color;
        transform.localScale = Vector3.one * (pressed && enabled ? 0.975f : 1f);
        if (label != null) label.color = useCustomLabelColor
            ? customLabelColor
            : enabled ? theme.TextoPrincipal : theme.TextoSecundario;
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
