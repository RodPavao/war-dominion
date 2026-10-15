using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WDHomeMissionsPanel : MonoBehaviour
{
    private const string MissionRoot = "UI/Missions/Canonical/";
    private const string BannerRoot = MissionRoot + "Banners/";
    private const float CardWidth = 436f;
    private const float CardHeight = 320f;
    private const float CardGap = 16f;
    private const float GridTop = 74f;

    private static readonly Color Gold = C("C59B63");
    private static readonly Color Text = C("F8FAFC");
    private static readonly Color Muted = C("64748B");
    private static readonly Color Navy = C("08111D");
    private static readonly Color Border = C("1E3A5F");

    private WarDominionUITheme theme;
    private WDMissionRepository repository;
    private WDNavigationStack navigation;
    private Action<bool> setHomeCompositionVisible;
    private Func<WDArmoryProfile> walletProvider;
    private Action openNotifications;
    private RectTransform content;
    private GameObject detail;

    public bool IsOpen => gameObject.activeSelf;
    public bool IsDetailOpen => detail != null;
    public string CurrentDetailMissionId { get; private set; }
    public WDMissionRepository Repository => repository;

    // ============================================================
    // 01. CONSTRUÇÃO E NAVEGAÇÃO
    // ============================================================

    public void Build(WarDominionUITheme newTheme, WDMissionRepository missionRepository,
        WDNavigationStack navigationStack)
        => Build(newTheme, missionRepository, navigationStack, null);

    public void Build(WarDominionUITheme newTheme, WDMissionRepository missionRepository,
        WDNavigationStack navigationStack, Action<bool> homeCompositionVisibility)
        => Build(newTheme, missionRepository, navigationStack,
            homeCompositionVisibility, null, null);

    public void Build(WarDominionUITheme newTheme, WDMissionRepository missionRepository,
        WDNavigationStack navigationStack, Action<bool> homeCompositionVisibility,
        Func<WDArmoryProfile> economyProvider, Action notificationAction)
    {
        theme = newTheme ?? throw new ArgumentNullException(nameof(newTheme));
        repository = missionRepository ?? throw new ArgumentNullException(nameof(missionRepository));
        navigation = navigationStack ?? throw new ArgumentNullException(nameof(navigationStack));
        setHomeCompositionVisible = homeCompositionVisibility;
        walletProvider = economyProvider;
        openNotifications = notificationAction;
        Image blocker = gameObject.AddComponent<Image>();
        blocker.color = Color.clear;
        blocker.raycastTarget = true;

        Texture2D backgroundTexture = Resources.Load<Texture2D>(MissionRoot + "MissionsBackground");
        RectTransform backgroundRoot = WDHomeUIFactory.Rect("MissionsFullscreenBackground", transform);
        WDHomeUIFactory.Stretch(backgroundRoot);
        RawImage background = backgroundRoot.gameObject.AddComponent<RawImage>();
        background.texture = backgroundTexture;
        background.raycastTarget = false;
        AspectRatioFitter crop = backgroundRoot.gameObject.AddComponent<AspectRatioFitter>();
        crop.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        crop.aspectRatio = backgroundTexture != null
            ? (float)backgroundTexture.width / backgroundTexture.height
            : 16f / 9f;

        Image scrim = WDHomeUIFactory.Image("MissionsReadabilityScrim", transform,
            new Color(.01f, .025f, .045f, .32f));
        WDHomeUIFactory.Stretch(scrim.rectTransform);
        scrim.raycastTarget = false;

        content = WDHomeUIFactory.Rect("MissionsContent", transform);
        WDHomeUIFactory.Stretch(content);
        gameObject.SetActive(false);
    }

    public void Open() => navigation.Push(new WDNavigationContext("missions", Restore));

    private void Restore(object _)
    {
        setHomeCompositionVisible?.Invoke(false);
        transform.SetAsLastSibling();
        Render();
    }

    public void HideFromNavigation()
    {
        CloseDetail();
        gameObject.SetActive(false);
        setHomeCompositionVisible?.Invoke(true);
    }

    public bool TryHandleLocalBack()
    {
        if (!IsDetailOpen) return false;
        CloseDetail();
        return true;
    }

    // ============================================================
    // 02. FRAME FIGMA 467:2 E RESUMO DA COLEÇÃO
    // ============================================================

    private void Render()
    {
        gameObject.SetActive(true);
        Clear(content);

        RectTransform frame = Rect("MissionsResponsiveShell", content, 0f, 0f, 0f, 0f);
        WDHomeSecondaryScreenFrame.Configure(frame);

        // 02.1. Missões compartilha o chrome completo e a economia do Arsenal.
        WDUniversalHeaderContent header = WDUniversalHeaderFactory.BuildInset(frame,
            theme, "MISSÕES", () => navigation.TryBack(),
            openNotifications == null ? null : () => openNotifications.Invoke());
        WDUniversalHeaderFactory.SetEconomy(header, walletProvider?.Invoke());

        RectTransform summary = Surface("SummaryBar", frame, 32f, 112f, 1856f, 72f,
            new Color(Navy.r, Navy.g, Navy.b, .94f));
        Rule(summary, 0f, 71f, 1856f, Gold, .24f);

        if (repository.State != WDMissionCollectionState.Ready || repository.TotalCount == 0)
        {
            string message = repository.State switch
            {
                WDMissionCollectionState.Loading => "CARREGANDO MISSÕES...",
                WDMissionCollectionState.Error => "NÃO FOI POSSÍVEL CARREGAR",
                WDMissionCollectionState.Unavailable => "MISSÕES INDISPONÍVEIS / OFFLINE",
                _ => "NENHUMA MISSÃO DISPONÍVEL"
            };
            Label(frame, "UniversalState", message, 20f, Muted,
                360f, 440f, 1200f, 100f, TextAlignmentOptions.Center, "Gabarito-Medium");
            return;
        }

        int discovered = repository.DiscoveredCount;
        Label(summary, "DiscoveriesCaption", "DESCOBERTAS", 12f, Gold,
            40f, 13f, 120f, 18f, TextAlignmentOptions.Left, "Inter-Bold", 1.44f);
        Label(summary, "DiscoveriesValue", $"{discovered} / {repository.TotalCount}", 18f, Text,
            40f, 32f, 120f, 26f, TextAlignmentOptions.Left, "Inter-Bold");
        RectTransform progress = Surface("ProgressTrack", summary, 176f, 31f, 460f, 10f,
            C("1E2D42"), 5);
        float ratio = repository.TotalCount == 0 ? 0f : (float)discovered / repository.TotalCount;
        RectTransform fill = Surface("ProgressFill", progress, 0f, 0f, 460f * ratio, 10f,
            Gold, 5);
        fill.GetComponent<Image>().raycastTarget = false;

        BuildCollection(frame, discovered);
    }

    // ============================================================
    // 03. COLEÇÃO DESCOBERTA E TEASER ÚNICO
    // ============================================================

    private void BuildCollection(RectTransform frame, int discovered)
    {
        const float viewportHeight = 898f;
        RectTransform viewport = Rect("CollectionViewport", frame, 32f, 200f, 1856f, viewportHeight - 18f);
        Image viewportHit = viewport.gameObject.AddComponent<Image>();
        viewportHit.color = Color.clear;
        viewportHit.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();

        WDMissionPresentation[] missions = repository.PresentAll()
            .Where(item => !item.IsHidden)
            .ToArray();
        int cardCount = missions.Length + (discovered < repository.TotalCount ? 1 : 0);
        int rowsCount = Mathf.Max(1, Mathf.CeilToInt(cardCount / 4f));

        RectTransform gridHost = WDHomeUIFactory.Rect("CanonicalGridHost", viewport);
        gridHost.anchorMin = gridHost.anchorMax = new Vector2(.5f, 1f);
        gridHost.pivot = new Vector2(.5f, 1f);
        gridHost.anchoredPosition = Vector2.zero;
        gridHost.sizeDelta = new Vector2(1856f, 0f);
        RectTransform grid = WDHomeUIFactory.Rect("CollectionGrid", gridHost);
        grid.anchorMin = new Vector2(0f, 1f);
        grid.anchorMax = new Vector2(1f, 1f);
        grid.pivot = new Vector2(.5f, 1f);
        grid.anchoredPosition = Vector2.zero;
        float naturalHeight = GridTop + rowsCount * CardHeight +
            Mathf.Max(0, rowsCount - 1) * CardGap + 60f;
        grid.sizeDelta = new Vector2(0f, Mathf.Max(viewportHeight, naturalHeight));

        for (int i = 0; i < missions.Length; i++) MissionCard(grid, missions[i], i);
        if (discovered < repository.TotalCount) SecretCard(grid, missions.Length);

        Scrollbar scrollbar = CreateScrollbar(frame, 1852f, 200f, 8f, viewportHeight - 18f);
        RightTop(scrollbar.transform as RectTransform, 60f, 200f, 8f, viewportHeight - 18f);
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = grid;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = true;
        scroll.decelerationRate = .135f;
        scroll.scrollSensitivity = 42f;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        scroll.verticalNormalizedPosition = 1f;

        Surface("RightBorder", frame, 1860f, 200f, 60f, viewportHeight - 18f,
            new Color(Navy.r, Navy.g, Navy.b, .60f));
        RightTop(frame.Find("RightBorder") as RectTransform, 0f, 200f, 60f, viewportHeight - 18f);
    }

    private static void StretchHorizontal(RectTransform rect, float left, float right)
    {
        rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, 1f); rect.offsetMin = new Vector2(left, rect.offsetMin.y);
        rect.offsetMax = new Vector2(-right, rect.offsetMax.y);
    }
    private static void CenterTop(RectTransform rect, float x, float y, float w, float h)
    { rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f); rect.pivot = new Vector2(.5f, 1f); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); }
    private static void RightTop(RectTransform rect, float right, float y, float w, float h)
    { rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f); rect.pivot = new Vector2(1f, 1f); rect.anchoredPosition = new Vector2(-right, -y); rect.sizeDelta = new Vector2(w, h); }

    private void MissionCard(RectTransform parent, WDMissionPresentation mission, int index)
    {
        Vector2 position = CardPosition(index);
        RectTransform card = Surface("Mission_" + mission.MissionId, parent,
            position.x, position.y, CardWidth, CardHeight,
            new Color(Navy.r, Navy.g, Navy.b, .86f), 14);
        Image cardSurface = card.GetComponent<Image>();
        cardSurface.raycastTarget = true;
        WDHomeUIFactory.AddThinRoundedBorder(card, Gold, 14);
        AddCardShadow(card);

        RawImage banner = Texture("CanonicalBanner", card,
            Resources.Load<Texture2D>(BannerRoot + mission.ArtKey));
        Place(banner.rectTransform, 0f, 0f, CardWidth, CardHeight);
        PreserveAspect(banner, AspectRatioFitter.AspectMode.EnvelopeParent);
        Button hit = card.gameObject.AddComponent<Button>();
        hit.targetGraphic = cardSurface;
        hit.transition = Selectable.Transition.None;
        hit.onClick.AddListener(() => OpenDetail(mission.MissionId));

        RectTransform count = Surface("CompletionCount", card,
            294f, 226f, 128f, 24f, new Color(Gold.r, Gold.g, Gold.b, .15f), 8);
        WDHomeUIFactory.AddThinRoundedBorder(count,
            new Color(Gold.r, Gold.g, Gold.b, .27f), 8);
        Label(count, "Label", $"CONQUISTADA ×{mission.CompletionCount}", 11f, Gold,
            6f, 0f, 116f, 24f, TextAlignmentOptions.Center, "Inter-Bold", .66f);
    }

    private void SecretCard(RectTransform parent, int index)
    {
        Vector2 position = CardPosition(index);
        RectTransform card = Surface("SecretMissionTeaser", parent,
            position.x, position.y, CardWidth, CardHeight,
            new Color(.024f, .047f, .086f, .80f), 14);
        WDHomeUIFactory.AddThinRoundedBorder(card, Border, 14);
        AddCardShadow(card);
        RectTransform art = Surface("SecretArt", card, 14f, 14f, 408f, 200f,
            new Color(.039f, .071f, .112f, .95f), 10);
        WDHomeUIFactory.AddThinRoundedBorder(art, Border, 10);
        RawImage lockIcon = Texture("Lock", art,
            WDProfileIdentityVisuals.ResolveLockIcon());
        Place(lockIcon.rectTransform, 190f, 58f, 28f, 28f);
        lockIcon.color = Muted;
        Label(art, "Question", "???", 18f, Muted, 114f, 94f, 180f, 28f,
            TextAlignmentOptions.Center, "Inter-ExtraBold", .36f);
        Label(art, "Secret", "MISSÃO SECRETA", 11f, Muted, 114f, 130f, 180f, 20f,
            TextAlignmentOptions.Center, "Inter-Bold", .66f);
        Label(art, "Undiscovered", "NÃO DESCOBERTA", 11f, C("334155"), 114f, 158f,
            180f, 20f, TextAlignmentOptions.Center, "Inter-Bold", .66f);
    }

    private static Vector2 CardPosition(int index)
    {
        int column = index % 4;
        int row = index / 4;
        return new Vector2(40f + column * (CardWidth + CardGap),
            GridTop + row * (CardHeight + CardGap));
    }


    // ============================================================
    // 04. DETALHE EXISTENTE, SEM REGRAS NOVAS
    // ============================================================

    private void OpenDetail(string missionId)
    {
        CloseDetail();
        WDMissionPresentation mission = repository.Present(missionId);
        if (mission == null || mission.IsHidden) return;
        CurrentDetailMissionId = missionId;
        RectTransform overlay = WDHomeUIFactory.Rect("MissionDetail", transform);
        WDHomeUIFactory.Stretch(overlay);
        detail = overlay.gameObject;
        Image veil = detail.AddComponent<Image>();
        veil.color = new Color(0f, 0f, 0f, .82f);
        veil.raycastTarget = true;
        RectTransform panel = Surface("Panel", overlay, 560f, 280f, 800f, 520f,
            new Color(Navy.r, Navy.g, Navy.b, .98f), 16);
        WDHomeUIFactory.AddThinRoundedBorder(panel, Gold, 16);
        Label(panel, "DetailTitle", mission.Title, 25f, Text,
            40f, 36f, 720f, 44f, TextAlignmentOptions.Center, "Gabarito-Bold");
        Label(panel, "DetailState", $"CONQUISTADA ×{mission.CompletionCount}", 16f, Gold,
            80f, 126f, 640f, 60f, TextAlignmentOptions.Center, "Inter-Bold");
        AddButton(panel, "CloseDetail", "VOLTAR À COLEÇÃO", CloseDetail,
            200f, 420f, 400f, 52f);
    }

    private void CloseDetail()
    {
        if (detail != null) DestroyNow(detail);
        detail = null;
        CurrentDetailMissionId = null;
    }

    // ============================================================
    // 05. PRIMITIVAS VISUAIS LOCAIS
    // ============================================================

    private Scrollbar CreateScrollbar(Transform parent, float x, float y,
        float width, float height)
    {
        RectTransform rail = Surface("ScrollbarRail", parent, x, y, width, height,
            new Color(.043f, .071f, .125f, .80f), 3);
        Scrollbar scrollbar = rail.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        RectTransform sliding = WDHomeUIFactory.Rect("SlidingArea", rail);
        WDHomeUIFactory.Stretch(sliding, 1f, 20f, 1f, 20f);
        RectTransform handle = Surface("Handle", sliding, 0f, 0f, 6f, 180f, Gold, 3);
        handle.anchorMin = Vector2.zero;
        handle.anchorMax = Vector2.one;
        handle.offsetMin = handle.offsetMax = Vector2.zero;
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handle.GetComponent<Image>();
        return scrollbar;
    }

    private void AddButton(Transform parent, string name, string value, Action action,
        float x, float y, float width, float height)
    {
        RectTransform rect = Surface(name, parent, x, y, width, height,
            new Color(.106f, .114f, .133f, .70f), 23);
        rect.GetComponent<Image>().raycastTarget = true;
        WDHomeUIFactory.AddThinRoundedBorder(rect, Gold, 23);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        button.onClick.AddListener(() => action());
        Label(rect, "Label", value, 16f, Gold, 8f, 0f, width - 16f, height,
            TextAlignmentOptions.Center, "Gabarito-Black", 1.5f);
    }

    private static RectTransform Surface(string name, Transform parent,
        float x, float y, float width, float height, Color color, int radius = 0)
    {
        RectTransform rect = Rect(name, parent, x, y, width, height);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        if (radius > 0) WDHomeUIFactory.ApplyRounded(image, radius);
        return rect;
    }

    private static RawImage Texture(string name, Transform parent, Texture2D texture)
    {
        RectTransform rect = WDHomeUIFactory.Rect(name, parent);
        RawImage image = rect.gameObject.AddComponent<RawImage>();
        image.texture = texture;
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI Label(Transform parent, string name, string value,
        float size, Color color, float x, float y, float width, float height,
        TextAlignmentOptions alignment, string font, float spacing = 0f)
    {
        RectTransform rect = Rect(name, parent, x, y, width, height);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = value;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.font = WDHomeTypography.Load(font);
        label.characterSpacing = spacing;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return label;
    }

    private static void Rule(Transform parent, float x, float y, float width,
        Color color, float alpha, float height = 1f)
    {
        color.a = alpha;
        Surface("Rule", parent, x, y, width, height, color);
    }

    private static void AddCardShadow(RectTransform card)
    {
        Shadow shadow = card.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, .31f);
        shadow.effectDistance = new Vector2(0f, -6f);
        shadow.useGraphicAlpha = true;
    }

    private static RectTransform Rect(string name, Transform parent,
        float x, float y, float width, float height)
    {
        RectTransform rect = WDHomeUIFactory.Rect(name, parent);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void PreserveAspect(RawImage image, AspectRatioFitter.AspectMode mode)
    {
        if (image?.texture == null) return;
        AspectRatioFitter aspect = image.gameObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = mode;
        aspect.aspectRatio = (float)image.texture.width / image.texture.height;
    }

    private static void Clear(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            DestroyNow(parent.GetChild(i).gameObject);
    }

    private static void DestroyNow(UnityEngine.Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(value);
        else UnityEngine.Object.DestroyImmediate(value);
    }

    private static Color C(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color color);
        return color;
    }
}
