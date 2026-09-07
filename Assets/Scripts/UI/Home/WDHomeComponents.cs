using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

public static class WDHomeUIFactory
{
    // ============================================================
    // 01. PRIMITIVAS DE LAYOUT DA HOME
    // ============================================================

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    public static Image Image(string name, Transform parent, Color color, bool raycast = false)
    {
        RectTransform rect = Rect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    public static TextMeshProUGUI Text(
        string name, Transform parent, WarDominionUITheme theme, string content,
        float size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        RectTransform rect = Rect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (theme != null)
            text.font = theme.FonteInterface;
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    public static void Stretch(RectTransform rect, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    public static void VerticalLayout(GameObject target, float spacing, RectOffset padding)
    {
        VerticalLayoutGroup layout = target.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.padding = padding;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
    }
}

[DisallowMultipleComponent]
public class WDHomeCard : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    // ============================================================
    // 02. CARD REUTILIZÁVEL COM ESTADOS INTERATIVOS
    // ============================================================

    protected Image surface;
    protected Image accent;
    protected Button button;
    protected WarDominionUITheme theme;
    private Vector3 targetScale = Vector3.one;

    public virtual void Build(
        WarDominionUITheme newTheme, string icon, string title, string subtitle,
        UnityAction action, float height = 96f)
    {
        theme = newTheme;
        surface = gameObject.AddComponent<Image>();
        surface.color = theme.SurfaceGlass;
        surface.raycastTarget = true;
        Outline outline = gameObject.AddComponent<Outline>();
        outline.effectColor = theme.BorderNeutral;
        outline.effectDistance = Vector2.one;

        button = gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        if (action != null)
            button.onClick.AddListener(action);

        LayoutElement element = gameObject.AddComponent<LayoutElement>();
        element.minHeight = height;
        element.preferredHeight = height;

        accent = WDHomeUIFactory.Image("Accent", transform, theme.Acento);
        RectTransform accentRect = accent.rectTransform;
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.sizeDelta = new Vector2(4f, 0f);
        accentRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI iconText = WDHomeUIFactory.Text(
            "Icon", transform, theme, icon, 24f, theme.Acento, TextAlignmentOptions.Center);
        RectTransform iconRect = iconText.rectTransform;
        iconRect.anchorMin = new Vector2(0f, 0f);
        iconRect.anchorMax = new Vector2(0f, 1f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.sizeDelta = new Vector2(54f, 0f);
        iconRect.anchoredPosition = new Vector2(8f, 0f);

        TextMeshProUGUI titleText = WDHomeUIFactory.Text(
            "Title", transform, theme, title, theme.TypeSection,
            theme.TextoPrincipal, TextAlignmentOptions.Left);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 0.48f);
        titleRect.anchorMax = new Vector2(1f, 0.94f);
        titleRect.offsetMin = new Vector2(64f, 0f);
        titleRect.offsetMax = new Vector2(-14f, 0f);
        titleText.fontStyle = FontStyles.Bold;

        TextMeshProUGUI subtitleText = WDHomeUIFactory.Text(
            "Subtitle", transform, theme, subtitle, theme.TypeMicro,
            theme.TextoSecundario, TextAlignmentOptions.Left);
        RectTransform subtitleRect = subtitleText.rectTransform;
        subtitleRect.anchorMin = new Vector2(0f, 0.08f);
        subtitleRect.anchorMax = new Vector2(1f, 0.48f);
        subtitleRect.offsetMin = new Vector2(64f, 0f);
        subtitleRect.offsetMax = new Vector2(-14f, 0f);
    }

    private void Update()
    {
        transform.localScale = Vector3.Lerp(
            transform.localScale, targetScale,
            Time.unscaledDeltaTime / Mathf.Max(0.01f, theme != null ? theme.MicroAnimationDuration : 0.14f));
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (surface != null) surface.color = theme.BackgroundElevated;
        if (accent != null) accent.color = Color.Lerp(theme.Acento, Color.white, 0.18f);
        targetScale = Vector3.one * 1.012f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (surface != null) surface.color = theme.SurfaceGlass;
        if (accent != null) accent.color = theme.Acento;
        targetScale = Vector3.one;
    }

    public void OnPointerDown(PointerEventData eventData) => targetScale = Vector3.one * 0.985f;
    public void OnPointerUp(PointerEventData eventData) => targetScale = Vector3.one * 1.012f;
}

[DisallowMultipleComponent]
public sealed class WDHomePlayCard : WDHomeCard
{
    // ============================================================
    // 03. CARD PROTAGONISTA DE PLAY
    // ============================================================

    public override void Build(
        WarDominionUITheme newTheme, string icon, string title, string subtitle,
        UnityAction action, float height = 270f)
    {
        base.Build(newTheme, icon, title, subtitle, action, height);
        surface.color = Color.Lerp(theme.BackgroundElevated, theme.Acento, 0.18f);
        Outline glow = gameObject.AddComponent<Outline>();
        Color glowColor = theme.Acento;
        glowColor.a = 0.62f;
        glow.effectColor = glowColor;
        glow.effectDistance = new Vector2(3f, -3f);

        TextMeshProUGUI titleText = transform.Find("Title")?.GetComponent<TextMeshProUGUI>();
        if (titleText != null)
        {
            titleText.fontSize = 42f;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.rectTransform.offsetMin = new Vector2(20f, 0f);
            titleText.rectTransform.offsetMax = new Vector2(-20f, 0f);
        }

        TextMeshProUGUI subtitleText = transform.Find("Subtitle")?.GetComponent<TextMeshProUGUI>();
        if (subtitleText != null)
        {
            subtitleText.fontSize = 14f;
            subtitleText.alignment = TextAlignmentOptions.Center;
            subtitleText.rectTransform.offsetMin = new Vector2(20f, 0f);
            subtitleText.rectTransform.offsetMax = new Vector2(-20f, 0f);
        }
    }
}

[DisallowMultipleComponent]
public sealed class WDHomeNotificationBadge : MonoBehaviour
{
    // ============================================================
    // 04. BADGE NUMÉRICO
    // ============================================================

    public void Build(WarDominionUITheme theme, int count)
    {
        Image image = gameObject.AddComponent<Image>();
        image.color = theme.Danger;
        image.raycastTarget = false;
        TextMeshProUGUI label = WDHomeUIFactory.Text(
            "Count", transform, theme, count.ToString(), theme.TypeMicro,
            Color.white, TextAlignmentOptions.Center);
        WDHomeUIFactory.Stretch(label.rectTransform);
    }
}

[DisallowMultipleComponent]
public sealed class WDHomeModal : MonoBehaviour
{
    // ============================================================
    // 05. MODAL ÚNICO COM BLOQUEIO E ANIMAÇÃO SIMPLES
    // ============================================================

    private CanvasGroup canvasGroup;
    private RectTransform panel;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI bodyLabel;
    private RectTransform choiceRow;
    private WDUIPremiumButton[] choiceButtons;
    private UnityAction<int> choiceAction;
    private UnityAction defaultCloseAction;
    private UnityAction contextualCloseAction;
    private Coroutine transition;

    public bool IsOpen => gameObject.activeSelf;

    public void Build(WarDominionUITheme theme, UnityAction closeAction)
    {
        defaultCloseAction = closeAction;
        Image blocker = gameObject.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0.76f);
        blocker.raycastTarget = true;
        Button blockerButton = gameObject.AddComponent<Button>();
        blockerButton.transition = Selectable.Transition.None;
        blockerButton.onClick.AddListener(RequestClose);

        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        panel = WDHomeUIFactory.Rect("Panel", transform);
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(720f, 560f);
        Image panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = theme.BackgroundElevated;
        panelImage.raycastTarget = true;
        Outline outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = theme.Acento;
        outline.effectDistance = new Vector2(2f, -2f);

        titleLabel = WDHomeUIFactory.Text(
            "Title", panel, theme, string.Empty, theme.TypeTitle,
            theme.TextoPrincipal, TextAlignmentOptions.Left);
        RectTransform titleRect = titleLabel.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(34f, -88f);
        titleRect.offsetMax = new Vector2(-90f, -26f);
        titleLabel.fontStyle = FontStyles.Bold;

        bodyLabel = WDHomeUIFactory.Text(
            "Body", panel, theme, string.Empty, theme.TypeBody,
            theme.TextoSecundario, TextAlignmentOptions.TopLeft);
        RectTransform bodyRect = bodyLabel.rectTransform;
        bodyRect.anchorMin = Vector2.zero;
        bodyRect.anchorMax = Vector2.one;
        bodyRect.offsetMin = new Vector2(34f, 104f);
        bodyRect.offsetMax = new Vector2(-34f, -106f);

        RectTransform close = WDHomeUIFactory.Rect("Close", panel);
        close.anchorMin = close.anchorMax = new Vector2(1f, 1f);
        close.pivot = new Vector2(1f, 1f);
        close.anchoredPosition = new Vector2(-22f, -22f);
        close.sizeDelta = new Vector2(52f, 52f);
        WDUIPremiumButton closeButton = close.gameObject.AddComponent<WDUIPremiumButton>();
        closeButton.Build(theme, "X", RequestClose);

        choiceRow = WDHomeUIFactory.Rect("Choices", panel);
        choiceRow.anchorMin = new Vector2(0f, 0f);
        choiceRow.anchorMax = new Vector2(1f, 0f);
        choiceRow.pivot = new Vector2(0.5f, 0f);
        choiceRow.offsetMin = new Vector2(34f, 28f);
        choiceRow.offsetMax = new Vector2(-34f, 88f);
        HorizontalLayoutGroup choicesLayout = choiceRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        choicesLayout.spacing = 12f;
        choicesLayout.childControlWidth = true;
        choicesLayout.childControlHeight = true;
        choicesLayout.childForceExpandWidth = true;
        choicesLayout.childForceExpandHeight = true;
        choiceButtons = new WDUIPremiumButton[3];
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            int index = i;
            RectTransform choice = WDHomeUIFactory.Rect($"Choice_{i + 1}", choiceRow);
            choiceButtons[i] = choice.gameObject.AddComponent<WDUIPremiumButton>();
            choiceButtons[i].Build(theme, string.Empty, () => choiceAction?.Invoke(index));
        }
        choiceRow.gameObject.SetActive(false);

        gameObject.SetActive(false);
    }

    public void Open(string title, string body)
    {
        titleLabel.text = title;
        bodyLabel.text = body;
        choiceRow.gameObject.SetActive(false);
        choiceAction = null;
        contextualCloseAction = null;
        BeginOpen();
    }

    public void OpenChoices(
        string title, string body, string[] choices, UnityAction<int> action,
        UnityAction closeAction = null)
    {
        titleLabel.text = title;
        bodyLabel.text = body;
        choiceAction = action;
        contextualCloseAction = closeAction;
        choiceRow.gameObject.SetActive(true);
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            string label = choices != null && i < choices.Length ? choices[i] : string.Empty;
            choiceButtons[i].SetLabel(label);
            choiceButtons[i].gameObject.SetActive(!string.IsNullOrEmpty(label));
        }
        BeginOpen();
    }

    public void RequestClose()
    {
        UnityAction action = contextualCloseAction ?? defaultCloseAction;
        action?.Invoke();
    }

    public void SetBody(string body)
    {
        if (bodyLabel != null)
            bodyLabel.text = body;
    }

    private void BeginOpen()
    {
        gameObject.SetActive(true);
        if (transition != null) StopCoroutine(transition);
        transition = StartCoroutine(Animate(true));
    }

    public void Close()
    {
        if (!gameObject.activeSelf) return;
        contextualCloseAction = null;
        if (transition != null) StopCoroutine(transition);
        transition = StartCoroutine(Animate(false));
    }

    private IEnumerator Animate(bool opening)
    {
        float duration = 0.16f;
        float elapsed = 0f;
        float fromAlpha = opening ? 0f : canvasGroup.alpha;
        float toAlpha = opening ? 1f : 0f;
        Vector3 fromScale = opening ? Vector3.one * 0.94f : panel.localScale;
        Vector3 toScale = opening ? Vector3.one : Vector3.one * 0.96f;
        canvasGroup.blocksRaycasts = true;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            canvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, t);
            panel.localScale = Vector3.Lerp(fromScale, toScale, t);
            yield return null;
        }

        canvasGroup.alpha = toAlpha;
        panel.localScale = toScale;
        if (!opening)
            gameObject.SetActive(false);
        transition = null;
    }
}
