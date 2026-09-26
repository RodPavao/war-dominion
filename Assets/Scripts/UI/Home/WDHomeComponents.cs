using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

public static class WDHomeTypography
{
    private static readonly Dictionary<string, TMP_FontAsset> assets = new();

    // 01. Os arquivos estáticos oficiais ficam em Resources. O TMP cria um
    // Font Asset dinâmico por peso em runtime, sem alterar o fallback global.
    public static TMP_FontAsset Load(string familyAndWeight)
    {
        if (assets.TryGetValue(familyAndWeight, out TMP_FontAsset cached))
            return cached;
        Font source = Resources.Load<Font>("UI/Fonts/" + familyAndWeight);
        if (source == null)
        {
            Debug.LogError("Fonte oficial ausente: " + familyAndWeight);
            return null;
        }

        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(source);
        if (asset == null)
        {
            Debug.LogError("TMP Font Asset não pôde ser criado: " + familyAndWeight);
            return null;
        }
        asset.name = familyAndWeight + " TMP Dynamic";
        assets.Add(familyAndWeight, asset);
        return asset;
    }
}

public static class WDHomeMobileLayout
{
    // 01. A plataforma e a orientação definem o perfil ergonômico; a proporção
    // sozinha continua livre para o layout responsivo do Canvas e da Safe Area.
    public static bool IsMobileLandscape =>
        Application.isMobilePlatform && Screen.width > Screen.height;

    // 01.1. O baseline mobile usa largura em unidades do Canvas 1920x1080.
    // Tablets e Game View mantêm a composição canônica; telefones estreitos
    // cedem espaço antes de ampliar controles periféricos ou cards.
    private static float SafeWidthAtReferenceHeight => Screen.height > 0
        ? Screen.safeArea.width * 1080f / Screen.height : 1920f;

    private static float MobileWidthProgress => IsMobileLandscape
        ? Mathf.Clamp01((SafeWidthAtReferenceHeight - 1728f) / 672f) : 0f;

    // 01.2. Ganhos seletivos preservam o centro da HOME. As escalas recuam
    // progressivamente em landscape estreito e nunca dependem do modelo físico.
    public static float HomeLeftCardScale => Mathf.Lerp(1f, 1.40f, MobileWidthProgress);
    public static float HomeRightCardScale => Mathf.Lerp(1f, 1.47f, MobileWidthProgress);
    public static float HomeSecondaryCardScale => Mathf.Lerp(1f, 1.22f, MobileWidthProgress);
    public static float HomeAccountScale => Mathf.Lerp(1f, 1.31f, MobileWidthProgress);
    // 01.2.1. O mesmo ganho pertence ao grupo completo em HOME e PLAY.
    // A âncora direita do grupo mantém sino e Safe Area estáveis.
    public static float HeaderResourceScale => IsMobileLandscape
        ? Mathf.Lerp(1.25f, 1.62f, MobileWidthProgress) : 1f;
    public static void ApplyHeaderResourceScale(RectTransform group)
    {
        if (group != null)
            group.localScale = Vector3.one * HeaderResourceScale;
    }

    // 01.2.2. Redução compartilhada do protagonista PLAY (12%) em todos os
    // perfis; a calibração mobile aprovada continua aplicada sobre esta base.
    private const float HomePlayCanonicalScale = 1f;
    public static float HomePlayScale => HomePlayCanonicalScale *
        Mathf.Lerp(1f, 1.62f, MobileWidthProgress);
    public static float HomePlayVerticalOffset => Mathf.Lerp(-118f, -152f, MobileWidthProgress);
    public static float HomeSecondaryBottom => Mathf.Lerp(148f, 70f, MobileWidthProgress);
    public static float HomeRightSubtitleSize => Mathf.Lerp(13.82f, 15f, MobileWidthProgress);
    public static float HomeTrophiesSize => Mathf.Lerp(16f, 21f, MobileWidthProgress);
    public static float RightCardStep => Mathf.Lerp(130f, 120f, MobileWidthProgress);

    // 01.3. O Game View no Editor não entra em Application.isMobilePlatform.
    // No Player, a antiga elevação de 52 deslocava todos os painéis PLAY;
    // esta elevação moderada atua apenas sobre o painel, após a base Figma.
    public static float PlayMobileLift => IsMobileLandscape ? 12f : 0f;

    public static float PlayScale(Vector2 canonicalPanel) => IsMobileLandscape
        ? Mathf.Min(1.13f,
            Mathf.Max(1f, (SafeWidthAtReferenceHeight - 128f) / canonicalPanel.x),
            Mathf.Max(1f, (1080f * Screen.safeArea.height / Screen.height - 180f) /
                canonicalPanel.y)) : 1f;

    public static float SearchControlScale => IsMobileLandscape
        ? Mathf.Lerp(1f, 1.2f,
            Mathf.Clamp01((SafeWidthAtReferenceHeight - 1728f) / 672f)) : 1f;

    // 02. O controle padrão mantém pivô e origem superior esquerda em ambos os
    // perfis. Apenas sua área de toque, texto e padding interno mudam no mobile.
    public static void ApplyBackControl(RectTransform back, TMP_Text label)
    {
        if (back == null || label == null) return;
        bool mobile = IsMobileLandscape;
        Vector2 backSize = mobile ? new Vector2(230f, 74f) : new Vector2(140f, 46f);
        Vector2 labelSize = mobile
            ? new Vector2(216f, 64f) : new Vector2(132f, 40f);
        float fontSize = mobile ? 27f : 16f;
        float spacing = mobile ? 2.5f : 1.5f;
        if (back.sizeDelta != backSize) back.sizeDelta = backSize;
        if (label.rectTransform.sizeDelta != labelSize)
            label.rectTransform.sizeDelta = labelSize;
        if (!Mathf.Approximately(label.fontSize, fontSize)) label.fontSize = fontSize;
        if (!Mathf.Approximately(label.characterSpacing, spacing))
            label.characterSpacing = spacing;
    }

    // 03. As descrições recebem um ganho próprio de legibilidade, sem escalar
    // títulos, pictogramas ou a composição inteira dos cards.
    public static float DescriptionSize(bool hub) =>
        IsMobileLandscape ? hub ? 23f : 24f : hub ? 15f : 20f;

    public static float DescriptionWidth(bool hub, bool centerHubCard) =>
        IsMobileLandscape ? hub ? centerHubCard ? 430f : 340f : 345f : 290f;
}

public static class WDHomeUIFactory
{
    private static readonly Dictionary<int, Sprite> roundedSprites = new Dictionary<int, Sprite>();
    private static readonly Dictionary<int, Sprite> borderSprites = new Dictionary<int, Sprite>();

    // ============================================================
    // 00. SUPERFICIES ARREDONDADAS REUTILIZAVEIS DA HOME
    // ============================================================

    public static void ApplyRounded(Image image, int radius)
    {
        if (image == null) return;
        radius = Mathf.Clamp(radius, 4, 140);
        if (!roundedSprites.TryGetValue(radius, out Sprite sprite))
        {
            int size = radius * 2 + 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "WDHomeRounded_" + radius;
            texture.hideFlags = HideFlags.DontSave;
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Abs(x + 0.5f - half) - (half - radius));
                float dy = Mathf.Max(0f, Mathf.Abs(y + 0.5f - half) - (half - radius));
                byte alpha = (byte)Mathf.RoundToInt(
                    Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy)) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            float edge = radius + 1f;
            sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(edge, edge, edge, edge));
            sprite.hideFlags = HideFlags.DontSave;
            roundedSprites.Add(radius, sprite);
        }
        image.sprite = sprite;
        image.type = UnityEngine.UI.Image.Type.Sliced;
    }

    // 00.1. O contorno contém somente pixels da borda: não duplica o fill translúcido.
    public static Image AddRoundedBorder(RectTransform target, Color color, int radius)
    {
        radius = Mathf.Clamp(radius, 4, 140);
        if (!borderSprites.TryGetValue(radius, out Sprite sprite))
        {
            int size = radius * 2 + 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "WDHomeBorder_" + radius;
            texture.hideFlags = HideFlags.DontSave;
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float ax = Mathf.Abs(x + 0.5f - half);
                float ay = Mathf.Abs(y + 0.5f - half);
                float outerDx = Mathf.Max(0f, ax - (half - radius));
                float outerDy = Mathf.Max(0f, ay - (half - radius));
                float outer = Mathf.Clamp01(radius + 0.5f -
                    Mathf.Sqrt(outerDx * outerDx + outerDy * outerDy));
                float innerRadius = radius - 1.5f;
                float innerDx = Mathf.Max(0f, ax - (half - radius - 1.5f));
                float innerDy = Mathf.Max(0f, ay - (half - radius - 1.5f));
                float inner = Mathf.Clamp01(innerRadius + 0.5f -
                    Mathf.Sqrt(innerDx * innerDx + innerDy * innerDy));
                byte alpha = (byte)Mathf.RoundToInt(outer * (1f - inner) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            float edge = radius + 1f;
            sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(edge, edge, edge, edge));
            sprite.hideFlags = HideFlags.DontSave;
            borderSprites.Add(radius, sprite);
        }
        Image border = Image("Border", target, color);
        Stretch(border.rectTransform);
        border.sprite = sprite;
        border.type = UnityEngine.UI.Image.Type.Sliced;
        border.raycastTarget = false;
        return border;
    }

    // 00.2. A largura do recurso vem do texto efetivo e mantém ícone, espaçamento e padding.
    public static float FitResourcePill(TextMeshProUGUI value, float minimumWidth)
    {
        RectTransform pill = (RectTransform)value.transform.parent;
        RectTransform icon = pill.Find("Icon") as RectTransform;
        if (icon == null) return minimumWidth;
        const float left = 12f;
        const float spacing = 10f;
        const float right = 12f;
        float iconWidth = icon.rect.width > 0f ? icon.rect.width : icon.sizeDelta.x;
        float textWidth = Mathf.Ceil(value.GetPreferredValues(value.text).x) + 4f;
        float textLeft = left + iconWidth + spacing;
        float width = Mathf.Max(minimumWidth, textLeft + textWidth + right);
        pill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
        icon.pivot = new Vector2(0.5f, 0.5f);
        icon.anchoredPosition = new Vector2(left + iconWidth * 0.5f, 0f);
        value.textWrappingMode = TextWrappingModes.NoWrap;
        value.overflowMode = TextOverflowModes.Overflow;
        value.rectTransform.anchorMin = value.rectTransform.anchorMax =
            new Vector2(0f, 0.5f);
        value.rectTransform.pivot = new Vector2(0f, 0.5f);
        value.rectTransform.anchoredPosition = new Vector2(textLeft, 0f);
        value.rectTransform.sizeDelta = new Vector2(width - textLeft - right, 28f);
        return width;
    }

    // 00.3. Uma única autoridade posiciona os três irmãos a partir da borda direita.
    public static void LayoutResourceGroup(
        TextMeshProUGUI coins, TextMeshProUGUI emeralds,
        RectTransform notification, float coinMinimum, float emeraldMinimum,
        float spacing = 16f)
    {
        if (coins == null || emeralds == null || notification == null) return;
        float emeraldWidth = FitResourcePill(emeralds, emeraldMinimum);
        FitResourcePill(coins, coinMinimum);
        float notificationLeft = notification.anchoredPosition.x -
            notification.pivot.x * notification.rect.width;
        float centerY = notification.anchoredPosition.y +
            (0.5f - notification.pivot.y) * notification.rect.height;
        // Invariante para qualquer largura TMP: moedas | 16 | esmeraldas | 16 | sino.
        PositionPill((RectTransform)emeralds.transform.parent, notification,
            notificationLeft - spacing, centerY);
        PositionPill((RectTransform)coins.transform.parent, notification,
            notificationLeft - spacing - emeraldWidth - spacing, centerY);
    }

    private static void PositionPill(RectTransform pill, RectTransform notification,
        float rightEdge, float centerY)
    {
        pill.anchorMin = pill.anchorMax = notification.anchorMin;
        pill.pivot = new Vector2(1f, 0.5f);
        pill.anchoredPosition = new Vector2(rightEdge, centerY);
    }

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
    protected Color identityAccent;
    private Vector3 targetScale = Vector3.one;

    public virtual void Build(
        WarDominionUITheme newTheme, string icon, string title, string subtitle,
        UnityAction action, float height = 96f)
    {
        Build(newTheme, icon, title, subtitle, action, height, null, false);
    }

    public void Build(
        WarDominionUITheme newTheme, string icon, string title, string subtitle,
        UnityAction action, float height, Color? accentColor, bool centerContent)
    {
        theme = newTheme;
        identityAccent = accentColor ?? theme.Acento;
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

        accent = WDHomeUIFactory.Image("Accent", transform, identityAccent);
        RectTransform accentRect = accent.rectTransform;
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.sizeDelta = new Vector2(4f, 0f);
        accentRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI iconText = WDHomeUIFactory.Text(
            "Icon", transform, theme, icon, 24f, identityAccent, TextAlignmentOptions.Center);
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

        // 02.1. Cards centrais podem alinhar ícone e textos como um único grupo.
        if (centerContent)
        {
            accent.gameObject.SetActive(false);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.anchoredPosition = new Vector2(0f, -10f);
            iconRect.sizeDelta = new Vector2(42f, 42f);
            titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -53f);
            titleRect.sizeDelta = new Vector2(220f, 30f);
            titleText.alignment = TextAlignmentOptions.Center;
            subtitleRect.anchorMin = subtitleRect.anchorMax = new Vector2(0.5f, 1f);
            subtitleRect.pivot = new Vector2(0.5f, 1f);
            subtitleRect.anchoredPosition = new Vector2(0f, -86f);
            subtitleRect.sizeDelta = new Vector2(240f, 28f);
            subtitleText.alignment = TextAlignmentOptions.Center;
        }
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
        if (accent != null) accent.color = Color.Lerp(identityAccent, Color.white, 0.18f);
        targetScale = Vector3.one * 1.012f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (surface != null) surface.color = theme.SurfaceGlass;
        if (accent != null) accent.color = identityAccent;
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
        panelImage.color = WDMatchVisualStyle.SurfaceElevated;
        panelImage.raycastTarget = true;
        WDHomeUIFactory.ApplyRounded(panelImage, 16);
        WDHomeUIFactory.AddRoundedBorder(panel, WDMatchVisualStyle.Gold, 16);

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
