using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public enum WDTournamentListTab { Active, ComingSoon, Closed }

public interface IWDTournamentConnectivity
{
    bool IsAvailable { get; }
}

public sealed class WDTournamentCenterCard
{
    public string Title { get; }
    public string Description { get; }
    public string ActionLabel { get; }
    public UnityAction Action { get; }
    public bool Selected { get; }
    public bool Enabled { get; }

    public WDTournamentCenterCard(string title, string description, string actionLabel,
        UnityAction action, bool selected = false, bool enabled = true)
    {
        Title = title;
        Description = description;
        ActionLabel = actionLabel;
        Action = action;
        Selected = selected;
        Enabled = enabled;
    }
}

// ============================================================
// 01. ÍCONE VETORIAL LOCAL: TROFÉU DE CONTORNO SEM ASSET DUPLICADO
// ============================================================

[RequireComponent(typeof(CanvasRenderer))]
public sealed class WDTournamentOutlineTrophyGraphic : MaskableGraphic
{
    [SerializeField, Min(.5f)] private float thickness = 1.6f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        Vector2 c = r.center;
        float unit = Mathf.Min(r.width, r.height) / 40f;
        Vector2[] cup =
        {
            c + new Vector2(-12f, 12f) * unit, c + new Vector2(12f, 12f) * unit,
            c + new Vector2(10f, 2f) * unit, c + new Vector2(5f, -5f) * unit,
            c + new Vector2(0f, -7f) * unit, c + new Vector2(-5f, -5f) * unit,
            c + new Vector2(-10f, 2f) * unit, c + new Vector2(-12f, 12f) * unit
        };
        AddPolyline(vh, cup, thickness * unit);
        AddPolyline(vh, new[] { c + new Vector2(-12f, 9f) * unit, c + new Vector2(-17f, 8f) * unit,
            c + new Vector2(-16f, 1f) * unit, c + new Vector2(-10f, -1f) * unit }, thickness * unit);
        AddPolyline(vh, new[] { c + new Vector2(12f, 9f) * unit, c + new Vector2(17f, 8f) * unit,
            c + new Vector2(16f, 1f) * unit, c + new Vector2(10f, -1f) * unit }, thickness * unit);
        AddLine(vh, c + new Vector2(0f, -7f) * unit, c + new Vector2(0f, -13f) * unit, thickness * unit);
        AddLine(vh, c + new Vector2(-6f, -13f) * unit, c + new Vector2(6f, -13f) * unit, thickness * unit);
    }

    private void AddPolyline(VertexHelper vh, IReadOnlyList<Vector2> points, float width)
    {
        for (int i = 1; i < points.Count; i++) AddLine(vh, points[i - 1], points[i], width);
    }

    private void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float width)
    {
        Vector2 normal = Vector2.Perpendicular((b - a).normalized) * width * .5f;
        int index = vh.currentVertCount;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = a - normal; vh.AddVert(vertex);
        vertex.position = a + normal; vh.AddVert(vertex);
        vertex.position = b + normal; vh.AddVert(vertex);
        vertex.position = b - normal; vh.AddVert(vertex);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }
}

[DisallowMultipleComponent]
public sealed class WDHomeTournamentsPanel : MonoBehaviour
{
    // ============================================================
    // 01. TOKENS LOCAIS EXTRAÍDOS DOS FRAMES 425:2 E 2111:575
    // ============================================================

    private static readonly Color Panel = new(7f / 255f, 15f / 255f, 26f / 255f, .88f);
    private static readonly Color MainListCardNavy = new(8f / 255f, 17f / 255f, 29f / 255f, .90f);
    private static readonly Color MainListCardNavySelected = new(10f / 255f, 25f / 255f, 38f / 255f, .94f);
    private static readonly Color MainTabsNavy = new(8f / 255f, 17f / 255f, 29f / 255f, .86f);
    private static readonly Color Gold = new(.773f, .608f, .388f, 1f);
    private static readonly Color Cyan = new(0f, .847f, 1f, 1f);
    private static readonly Color White = new(.95f, .96f, .98f, 1f);
    private static readonly Color Muted = new(.58f, .63f, .70f, 1f);
    private static readonly Color Green = new(.25f, .76f, .32f, 1f);
    private static readonly Color CenterCardNavy = new(8f / 255f, 17f / 255f, 29f / 255f, .96f);
    private static readonly Color CenterCardNavySelected = new(10f / 255f, 25f / 255f, 38f / 255f, .98f);

    private WarDominionUITheme theme;
    private WDTournamentRepository repository;
    private Func<WDArmoryProfile> walletProvider;
    private Action openNotifications;
    private Action<string> openDetails;
    private Action goBack;
    private IWDTournamentConnectivity connectivity;
    private Texture2D backgroundTexture;
    private Texture2D offlineArtwork;
    private TMP_FontAsset titleFont;
    private TMP_FontAsset bodyFont;
    private RectTransform frame;
    private RectTransform onlineRoot;
    private RectTransform offlineRoot;
    private RectTransform listContent;
    private RectTransform featuredRoot;
    private RectTransform centerOverlay;
    private RectTransform centerModal;
    private UnityAction centerBackAction;
    private WDUniversalHeaderContent header;
    private WDTournamentListTab selectedTab;
    private string selectedTournamentId;
    private string centerSectionTitle;

    public WDTournamentListTab SelectedTab => selectedTab;
    public bool ShowingOffline => offlineRoot != null && offlineRoot.gameObject.activeSelf;
    public bool CenterOpen => centerOverlay != null && centerOverlay.gameObject.activeSelf;

    // ============================================================
    // 02. CONSTRUÇÃO ÚNICA E DEPENDÊNCIAS INJETADAS
    // ============================================================

    public void Build(
        WarDominionUITheme newTheme, WDTournamentRepository newRepository,
        Func<WDArmoryProfile> newWalletProvider, Action newOpenNotifications,
        Action<string> newOpenDetails, Action newGoBack,
        Texture2D newBackgroundTexture, Texture2D newOfflineArtwork,
        TMP_FontAsset newTitleFont = null, TMP_FontAsset newBodyFont = null,
        IWDTournamentConnectivity newConnectivity = null)
    {
        theme = newTheme;
        repository = newRepository;
        walletProvider = newWalletProvider;
        openNotifications = newOpenNotifications;
        openDetails = newOpenDetails;
        goBack = newGoBack;
        backgroundTexture = newBackgroundTexture;
        offlineArtwork = newOfflineArtwork;
        titleFont = newTitleFont;
        bodyFont = newBodyFont;
        connectivity = newConnectivity;

        BuildBackground();
        frame = Rect("TournamentsResponsiveFrame", transform);
        WDHomeSecondaryScreenFrame.Configure(frame);
        header = WDUniversalHeaderFactory.BuildInset(frame, theme, "TORNEIOS",
            () => goBack?.Invoke(), openNotifications == null ? null : () => openNotifications.Invoke());
        BuildOnlineLayout();
        BuildOfflineLayout();
        gameObject.SetActive(false);
    }

    public void SetConnectivity(IWDTournamentConnectivity value)
    {
        connectivity = value;
        if (gameObject.activeSelf) Refresh();
    }

    public void Show()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Refresh();
    }

    public void Hide()
    {
        HideCenter();
        gameObject.SetActive(false);
    }

    // ============================================================
    // 03. CENTRO DE TORNEIO SOBRE A LISTAGEM, SEM TROCAR O FUNDO
    // ============================================================

    public void ShowCenter(string title, string subtitle, string summary,
        IReadOnlyList<WDTournamentCenterCard> cards, UnityAction closeAction,
        UnityAction backAction = null, string feedback = null,
        UnityAction primaryAction = null, string primaryLabel = null)
    {
        HideCenter();
        centerSectionTitle = title ?? string.Empty;
        centerBackAction = backAction;
        centerOverlay = Rect("TournamentCenterOverlay", transform);
        WDHomeUIFactory.Stretch(centerOverlay);
        Image veil = centerOverlay.gameObject.AddComponent<Image>();
        veil.color = new Color(.005f, .01f, .02f, .76f);
        veil.raycastTarget = true;

        centerModal = Rect("TournamentCenterModal", centerOverlay);
        centerModal.anchorMin = centerModal.anchorMax = centerModal.pivot = new Vector2(.5f, .5f);
        centerModal.sizeDelta = new Vector2(1320f, 780f);
        Surface(centerModal, new Color(5f / 255f, 12f / 255f, 22f / 255f, .985f),
            12, new Color(Gold.r, Gold.g, Gold.b, .46f));
        Image headerRule = WDHomeUIFactory.Image("HeaderRule", centerModal,
            new Color(Gold.r, Gold.g, Gold.b, .24f));
        headerRule.rectTransform.anchorMin = new Vector2(0f, 1f);
        headerRule.rectTransform.anchorMax = new Vector2(1f, 1f);
        headerRule.rectTransform.pivot = new Vector2(.5f, 1f);
        headerRule.rectTransform.anchoredPosition = new Vector2(0f, -132f);
        headerRule.rectTransform.sizeDelta = new Vector2(-72f, 1f);
        headerRule.raycastTarget = false;
        TextAt("Eyebrow", centerModal, "CENTRO DE TORNEIO", 12f, Gold,
            36f, 24f, 980f, 22f, TextAlignmentOptions.Left);
        TextAt("Title", centerModal, title, 30f, White,
            36f, 52f, 1040f, 42f, TextAlignmentOptions.Left);
        TextAt("Subtitle", centerModal, subtitle, 13f, Muted,
            36f, 98f, 890f, 24f, TextAlignmentOptions.Left);
        RectTransform stateBadge = TopRect("StateBadge", centerModal, 934f, 94f, 184f, 26f);
        Surface(stateBadge, new Color(.08f, .24f, .15f, .88f), 4,
            new Color(Green.r, Green.g, Green.b, .58f));
        Text("Label", stateBadge, "TORNEIO OFICIAL", 10f, Green, TextAlignmentOptions.Center);
        ButtonAt("Close", centerModal, "FECHAR", 1160f, 30f, 124f, 40f,
            closeAction, false);
        if (backAction != null)
            ButtonAt("Back", centerModal, "VOLTAR", 1010f, 30f, 132f, 40f,
                backAction, false);

        RectTransform summaryPanel = TopRect("Summary", centerModal, 36f, 154f, 1248f, 92f);
        Surface(summaryPanel, CenterCardNavy, 8,
            new Color(Cyan.r, Cyan.g, Cyan.b, .20f));
        TextAt("Copy", summaryPanel, summary ?? string.Empty, 13f, Muted,
            18f, 14f, 1212f, 64f, TextAlignmentOptions.TopLeft);

        RectTransform cardArea = TopRect("Cards", centerModal, 36f, 266f, 1248f, 430f);
        bool compactCards = cards != null && cards.Count > 6;
        if (cards != null && cards.Count > 0)
        {
            GridLayoutGroup grid = cardArea.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = compactCards ? new Vector2(400f, 120f) : new Vector2(400f, 188f);
            grid.spacing = new Vector2(24f, 22f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.UpperLeft;
            foreach (WDTournamentCenterCard card in cards) BuildCenterCard(cardArea, card, compactCards);
        }
        else
        {
            RectTransform notice = TopRect("DetailState", cardArea, 0f, 0f, 1248f, 350f);
            Surface(notice, new Color(8f / 255f, 17f / 255f, 29f / 255f, .92f), 10,
                new Color(Gold.r, Gold.g, Gold.b, .18f));
            RectTransform noticeIcon = TopRect("Icon", notice, 582f, 82f, 84f, 84f);
            WDTournamentOutlineTrophyGraphic trophy = noticeIcon.gameObject.AddComponent<WDTournamentOutlineTrophyGraphic>();
            trophy.color = new Color(Gold.r, Gold.g, Gold.b, .72f);
            trophy.raycastTarget = false;
            TextAt("Label", notice, "INFORMAÇÃO DO TORNEIO", 14f, White,
                160f, 190f, 928f, 26f, TextAlignmentOptions.Center);
            TextAt("Hint", notice, "USE VOLTAR PARA RETORNAR AO CENTRO DE TORNEIO", 11f, Muted,
                160f, 224f, 928f, 22f, TextAlignmentOptions.Center);
        }

        if (!string.IsNullOrWhiteSpace(feedback))
            TextAt("Feedback", centerModal, feedback, 12f, Gold,
                36f, 730f, 920f, 24f, TextAlignmentOptions.Left);
        if (primaryAction != null && !string.IsNullOrWhiteSpace(primaryLabel))
            ButtonAt("Primary", centerModal, primaryLabel, 1036f, 718f, 248f, 44f,
                primaryAction, true);
        centerOverlay.SetAsLastSibling();
    }

    public void ShowCenterNotice(string title, string body, UnityAction backAction,
        UnityAction closeAction)
    {
        ShowCenter(title, "TORNEIOS OFICIAIS", body,
            Array.Empty<WDTournamentCenterCard>(), closeAction, backAction);
    }

    public void HideCenter()
    {
        if (centerOverlay == null) return;
        centerOverlay.gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(centerOverlay.gameObject);
        else DestroyImmediate(centerOverlay.gameObject);
        centerOverlay = null;
        centerModal = null;
        centerBackAction = null;
        centerSectionTitle = null;
    }

    public bool TryHandleCenterBack()
    {
        if (!CenterOpen) return false;
        if (centerBackAction != null) centerBackAction.Invoke();
        else HideCenter();
        return true;
    }

    private void BuildCenterCard(RectTransform parent, WDTournamentCenterCard card, bool compact)
    {
        RectTransform rect = Rect(card.Title.Replace(" ", string.Empty), parent);
        Color accent = CenterCardAccent(card.Title);
        Color border = card.Selected ? Cyan : new Color(accent.r, accent.g, accent.b, .30f);
        Color fill = card.Selected ? CenterCardNavySelected : CenterCardNavy;
        Surface(rect, fill, 9, border);
        Image accentLine = WDHomeUIFactory.Image("Accent", rect,
            card.Enabled ? accent : new Color(Muted.r, Muted.g, Muted.b, .42f));
        accentLine.rectTransform.anchorMin = new Vector2(0f, 0f);
        accentLine.rectTransform.anchorMax = new Vector2(0f, 1f);
        accentLine.rectTransform.pivot = new Vector2(0f, .5f);
        accentLine.rectTransform.sizeDelta = new Vector2(3f, -2f);
        accentLine.raycastTarget = false;
        RectTransform icon = TopRect("Icon", rect, 18f, 18f, 44f, 44f);
        Surface(icon, new Color(accent.r, accent.g, accent.b, card.Enabled ? .09f : .035f), 6,
            new Color(accent.r, accent.g, accent.b, card.Enabled ? .40f : .14f));
        RectTransform iconArtwork = Rect("Artwork", icon);
        WDHomeUIFactory.Stretch(iconArtwork, 10f, 10f, 10f, 10f);
        if (card.Title != null && card.Title.Contains("VISÃO GERAL"))
        {
            WDTournamentOutlineTrophyGraphic trophy = iconArtwork.gameObject.AddComponent<WDTournamentOutlineTrophyGraphic>();
            trophy.color = card.Enabled ? accent : Muted;
            trophy.raycastTarget = false;
        }
        else
        {
            RawImage iconArt = iconArtwork.gameObject.AddComponent<RawImage>();
            iconArt.texture = Resources.Load<Texture2D>(CenterCardIcon(card.Title));
            iconArt.color = card.Title != null && card.Title.Contains("ASSISTIR")
                ? new Color(1f, 1f, 1f, card.Enabled ? .9f : .42f)
                : card.Enabled ? accent : Muted;
            iconArt.uvRect = new Rect(0f, 0f, 1f, 1f);
            if (card.Title != null && card.Title.Contains("ASSISTIR"))
                iconArt.rectTransform.localScale = new Vector3(1f, 1.16f, 1f);
            iconArt.raycastTarget = false;
        }
        TextAt("Title", rect, card.Title, 17f, card.Enabled ? White : Muted,
            76f, 18f, 306f, 28f, TextAlignmentOptions.Left);
        TextAt("Description", rect, card.Description ?? string.Empty, 12f, Muted,
            18f, 72f, 364f, compact ? 34f : 54f, TextAlignmentOptions.TopLeft);
        ButtonAt("ActionButton", rect, card.Enabled ? card.ActionLabel : "INDISPONÍVEL",
            18f, compact ? 94f : 134f, 364f, compact ? 34f : 38f,
            card.Enabled ? card.Action : null, card.Selected);
    }

    private static Color CenterCardAccent(string title)
    {
        if (title != null && (title.Contains("DECK") || title.Contains("PARTIDA"))) return Cyan;
        if (title != null && title.Contains("ASSISTIR")) return new Color(.48f, .53f, .61f, 1f);
        return Gold;
    }

    private string CenterCardIcon(string title)
    {
        if (!string.IsNullOrEmpty(centerSectionTitle) && centerSectionTitle.Contains("PARTICIPANTES"))
            return "UI/Home/FigmaPhase1A/Users";
        if (!string.IsNullOrEmpty(centerSectionTitle) && centerSectionTitle.Contains("CHAVEAMENTO"))
            return "UI/Home/PlayFinal/TwoVsTwo";
        if (title != null && title.Contains("PARTICIPANTES")) return "UI/Home/FigmaPhase1A/Users";
        if (title != null && title.Contains("DECK")) return "UI/Home/FigmaPhase1A/Shield";
        if (title != null && title.Contains("PARTIDA")) return "UI/Home/PlayFinal/PlayCrossedSwords";
        if (title != null && title.Contains("ASSISTIR")) return "UI/Match/FigmaCurrent/Hubs/HideEye";
        if (title != null && title.Contains("CHAVEAMENTO")) return "UI/Home/PlayFinal/TwoVsTwo";
        return "UI/Home/PlayFinal/TournamentTrophy";
    }

    private void BuildBackground()
    {
        Image blocker = gameObject.AddComponent<Image>();
        blocker.color = Color.black;
        blocker.raycastTarget = true;
        RectTransform background = Rect("TournamentBackground", transform);
        background.anchorMin = background.anchorMax = background.pivot = new Vector2(.5f, .5f);
        RawImage image = background.gameObject.AddComponent<RawImage>();
        image.texture = backgroundTexture;
        image.raycastTarget = false;
        AspectRatioFitter cover = background.gameObject.AddComponent<AspectRatioFitter>();
        cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        cover.aspectRatio = backgroundTexture != null
            ? (float)backgroundTexture.width / backgroundTexture.height : 16f / 9f;
        Image veil = WDHomeUIFactory.Image("TournamentVeil", transform, new Color(.015f, .025f, .04f, .34f));
        WDHomeUIFactory.Stretch(veil.rectTransform);
        veil.raycastTarget = false;
    }

    private void BuildOnlineLayout()
    {
        onlineRoot = Rect("OnlineState", frame);
        WDHomeUIFactory.Stretch(onlineRoot);
        RectTransform left = TopRect("TournamentListPanel", onlineRoot, 32f, 112f, 1120f, 944f);
        Surface(left, Panel, 12, new Color(Gold.r, Gold.g, Gold.b, .55f));
        RectTransform right = TopRect("FeaturedTournamentPanel", onlineRoot, 1168f, 112f, 720f, 944f);
        Surface(right, Panel, 12, new Color(Gold.r, Gold.g, Gold.b, .55f));
        BuildTabs(left);
        BuildList(left);
        featuredRoot = TopRect("FeaturedContent", right, 24f, 24f, 672f, 896f);
    }

    private void BuildTabs(RectTransform parent)
    {
        RectTransform tabs = TopRect("Tabs", parent, 24f, 24f, 1072f, 44f);
        Surface(tabs, MainTabsNavy, 6, new Color(1f, 1f, 1f, .08f));
        BuildTab(tabs, WDTournamentListTab.Active, "ATIVOS", 0);
        BuildTab(tabs, WDTournamentListTab.ComingSoon, "EM BREVE", 1);
        BuildTab(tabs, WDTournamentListTab.Closed, "ENCERRADOS", 2);
        for (int index = 1; index < 3; index++)
        {
            Image divider = WDHomeUIFactory.Image("Divider" + index, tabs,
                new Color(1f, 1f, 1f, .15f));
            divider.rectTransform.anchorMin = divider.rectTransform.anchorMax =
                new Vector2(index / 3f, .5f);
            divider.rectTransform.sizeDelta = new Vector2(1f, 28f);
            divider.raycastTarget = false;
        }
    }

    private void BuildTab(RectTransform parent, WDTournamentListTab tab, string label, int index)
    {
        RectTransform item = Rect("Tab" + tab, parent);
        item.anchorMin = new Vector2(index / 3f, 0f);
        item.anchorMax = new Vector2((index + 1f) / 3f, 1f);
        item.offsetMin = item.offsetMax = Vector2.zero;
        Button button = item.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => SelectTab(tab));
        Text("Label", item, label, 13f, Muted, TextAlignmentOptions.Center);
    }

    private void BuildList(RectTransform parent)
    {
        RectTransform viewport = TopRect("ListViewport", parent, 24f, 84f, 1080f, 836f);
        Image maskImage = viewport.gameObject.AddComponent<Image>();
        maskImage.color = new Color(0f, 0f, 0f, .001f);
        maskImage.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();
        listContent = Rect("ListContent", viewport);
        listContent.anchorMin = new Vector2(0f, 1f);
        listContent.anchorMax = new Vector2(1f, 1f);
        listContent.pivot = new Vector2(.5f, 1f);
        listContent.offsetMin = listContent.offsetMax = Vector2.zero;
        VerticalLayoutGroup layout = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.padding = new RectOffset(0, 24, 0, 0);
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fit = listContent.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect scroll = parent.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = listContent;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;
    }

    private void BuildOfflineLayout()
    {
        offlineRoot = Rect("OfflineState", frame);
        WDHomeUIFactory.Stretch(offlineRoot);
        RectTransform card = TopRect("OfflineCard", offlineRoot, 750f, 450f, 420f, 268f);
        Surface(card, new Color(7f / 255f, 15f / 255f, 26f / 255f, .97f), 12,
            new Color(Gold.r, Gold.g, Gold.b, .34f));
        RectTransform art = TopRect("OfflineArtwork", card, 16f, 16f, 388f, 148f);
        RawImage image = art.gameObject.AddComponent<RawImage>();
        image.texture = offlineArtwork;
        image.uvRect = new Rect(0f, .28f, 1f, .44f);
        image.raycastTarget = false;
        TextAt("Title", card, "SEM COMUNICAÇÃO", 17f, White, 16f, 177f, 388f, 24f,
            TextAlignmentOptions.Left);
        TextAt("Description", card, "ESTE RECURSO ESTÁ INDISPONÍVEL NO MOMENTO.", 12f, Muted,
            16f, 207f, 388f, 22f, TextAlignmentOptions.Left);
    }

    // ============================================================
    // 04. ESTADO, FILTRAGEM E SELEÇÃO DATA-DRIVEN
    // ============================================================

    public static IReadOnlyList<WDTournament> FilterTournaments(
        IEnumerable<WDTournament> source, WDTournamentListTab tab)
    {
        IEnumerable<WDTournament> items = source ?? Enumerable.Empty<WDTournament>();
        return tab switch
        {
            WDTournamentListTab.Active => items.Where(item =>
                item.Lifecycle == WDTournamentLifecycle.RegistrationOpen ||
                item.Lifecycle == WDTournamentLifecycle.InProgress).ToArray(),
            WDTournamentListTab.ComingSoon => items.Where(item =>
                item.Lifecycle == WDTournamentLifecycle.ComingSoon).ToArray(),
            _ => items.Where(item => item.Lifecycle == WDTournamentLifecycle.Closed).ToArray()
        };
    }

    public void SelectTab(WDTournamentListTab tab)
    {
        selectedTab = tab;
        RenderTabs();
        RenderList();
    }

    private void Refresh()
    {
        WDUniversalHeaderFactory.SetEconomy(header, walletProvider?.Invoke());
        bool online = connectivity == null || connectivity.IsAvailable;
        onlineRoot.gameObject.SetActive(online);
        offlineRoot.gameObject.SetActive(!online);
        if (!online) return;
        RenderTabs();
        RenderList();
    }

    private void RenderTabs()
    {
        foreach (WDTournamentListTab tab in Enum.GetValues(typeof(WDTournamentListTab)))
        {
            Transform target = onlineRoot.Find("TournamentListPanel/Tabs/Tab" + tab);
            if (target == null) continue;
            Image background = target.GetComponent<Image>() ?? target.gameObject.AddComponent<Image>();
            background.color = tab == selectedTab ? new Color(0f, .85f, 1f, .07f) : Color.clear;
            background.raycastTarget = false;
            TextMeshProUGUI label = target.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.color = tab == selectedTab ? Cyan : Muted;
            Transform old = target.Find("SelectionLine");
            if (old != null)
            {
                old.gameObject.SetActive(false);
                Destroy(old.gameObject);
            }
            if (tab == selectedTab)
            {
                Image line = WDHomeUIFactory.Image("SelectionLine", target, Cyan);
                line.rectTransform.anchorMin = new Vector2(0f, 0f);
                line.rectTransform.anchorMax = new Vector2(1f, 0f);
                line.rectTransform.pivot = new Vector2(.5f, 0f);
                line.rectTransform.sizeDelta = new Vector2(0f, 2f);
                line.raycastTarget = false;
            }
        }
    }

    private void RenderList()
    {
        Clear(listContent);
        IReadOnlyList<WDTournament> items = FilterTournaments(repository?.All, selectedTab);
        if (items.Count == 0)
        {
            TextMeshProUGUI empty = Text("Empty", listContent, "NENHUM TORNEIO NESTA CATEGORIA",
                14f, Muted, TextAlignmentOptions.Center);
            empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 120f;
            RenderFeatured(null);
            return;
        }
        if (string.IsNullOrEmpty(selectedTournamentId) || items.All(item => item.Id != selectedTournamentId))
            selectedTournamentId = items[0].Id;
        foreach (WDTournament item in items) BuildTournamentRow(item);
        RenderFeatured(items.FirstOrDefault(item => item.Id == selectedTournamentId) ?? items[0]);
    }

    private void BuildTournamentRow(WDTournament tournament)
    {
        bool selected = tournament.Id == selectedTournamentId;
        RectTransform row = Rect("Tournament_" + tournament.Id, listContent);
        LayoutElement size = row.gameObject.AddComponent<LayoutElement>();
        size.preferredHeight = 148f;
        Surface(row, selected ? MainListCardNavySelected : MainListCardNavy, 6,
            new Color(1f, 1f, 1f, .10f));
        Button hit = row.gameObject.AddComponent<Button>();
        hit.transition = Selectable.Transition.None;
        hit.onClick.AddListener(() => { selectedTournamentId = tournament.Id; RenderList(); });
        Image marker = WDHomeUIFactory.Image("SelectedMarker", row, selected ? Cyan : Color.clear);
        marker.rectTransform.anchorMin = new Vector2(0f, 0f);
        marker.rectTransform.anchorMax = new Vector2(0f, 1f);
        marker.rectTransform.sizeDelta = new Vector2(3f, 0f);
        marker.raycastTarget = false;
        RectTransform icon = TopRect("Icon", row, 16f, 20f, 56f, 56f);
        Surface(icon, new Color(0f, .85f, 1f, .08f), 5, new Color(Cyan.r, Cyan.g, Cyan.b, .35f));
        RectTransform iconArt = Rect("Artwork", icon);
        WDHomeUIFactory.Stretch(iconArt, 10f, 10f, 10f, 10f);
        WDTournamentOutlineTrophyGraphic trophy = iconArt.gameObject.AddComponent<WDTournamentOutlineTrophyGraphic>();
        trophy.color = tournament.Lifecycle == WDTournamentLifecycle.Closed ? Gold : Cyan;
        trophy.raycastTarget = false;
        TextAt("Name", row, tournament.Name, 17f, White, 84f, 18f, 650f, 28f,
            TextAlignmentOptions.Left);
        Color rowStateAccent = tournament.Lifecycle == WDTournamentLifecycle.Closed ? Gold : Green;
        RectTransform rowState = TopRect("Status", row, 84f, 48f, 172f, 22f);
        Surface(rowState, new Color(rowStateAccent.r, rowStateAccent.g, rowStateAccent.b, .09f), 4,
            new Color(rowStateAccent.r, rowStateAccent.g, rowStateAccent.b, .26f));
        Text("Label", rowState, LifecycleLabel(tournament.Lifecycle), 10f, rowStateAccent,
            TextAlignmentOptions.Center);
        TextAt("Metadata", row,
            $"FORMATO: {FormatLabel(tournament.Format)}  ·  PERÍODO: {Safe(tournament.Period)}",
            12f, Muted, 84f, 75f, 690f, 20f, TextAlignmentOptions.Left);
        TextAt("Description", row, tournament.Description ?? string.Empty, 12f, Muted,
            84f, 101f, 690f, 28f, TextAlignmentOptions.Left);
        ButtonAt("Details", row, tournament.Lifecycle == WDTournamentLifecycle.RegistrationOpen
            ? "PARTICIPAR" : "DETALHES", 920f, 48f, 120f, 44f,
            () => openDetails?.Invoke(tournament.Id), false);
    }

    private void RenderFeatured(WDTournament tournament)
    {
        Clear(featuredRoot);
        TextAt("Eyebrow", featuredRoot, "EM DESTAQUE", 12f, Gold, 0f, 0f, 640f, 22f,
            TextAlignmentOptions.Left);
        if (tournament == null)
        {
            TextAt("Empty", featuredRoot, "NENHUM TORNEIO DISPONÍVEL", 16f, Muted,
                0f, 96f, 640f, 30f, TextAlignmentOptions.Center);
            return;
        }
        RectTransform icon = TopRect("Trophy", featuredRoot, 8f, 48f, 72f, 72f);
        Surface(icon, new Color(1f, 1f, 1f, .035f), 6, new Color(Gold.r, Gold.g, Gold.b, .35f));
        RectTransform iconArt = Rect("Artwork", icon);
        WDHomeUIFactory.Stretch(iconArt, 14f, 14f, 14f, 14f);
        WDTournamentOutlineTrophyGraphic trophy = iconArt.gameObject.AddComponent<WDTournamentOutlineTrophyGraphic>();
        trophy.color = Gold;
        trophy.raycastTarget = false;
        TextAt("Name", featuredRoot, tournament.Name, 25f, White, 104f, 48f, 540f, 34f,
            TextAlignmentOptions.Left);
        TextAt("Season", featuredRoot, Safe(tournament.Period), 13f, Muted,
            104f, 86f, 540f, 22f, TextAlignmentOptions.Left);
        RectTransform state = TopRect("StateBadge", featuredRoot, 104f, 114f, 230f, 26f);
        Color stateAccent = tournament.Lifecycle == WDTournamentLifecycle.Closed ? Gold : Green;
        Surface(state, new Color(stateAccent.r, stateAccent.g, stateAccent.b, .10f), 4,
            new Color(stateAccent.r, stateAccent.g, stateAccent.b, .35f));
        Text("Label", state, LifecycleLabel(tournament.Lifecycle), 10f, stateAccent,
            TextAlignmentOptions.Center);
        FeaturedInfo("Period", "PERÍODO", Safe(tournament.Period), 164f);
        FeaturedInfo("Format", "FORMATO", FormatLabel(tournament.Format), 264f);
        FeaturedInfo("Capacity", "PARTICIPANTES / STATUS",
            $"{tournament.Entries.Count}/{tournament.Capacity} · {LifecycleLabel(tournament.Lifecycle)}", 364f);
        ButtonAt("PrimaryAction", featuredRoot,
            tournament.Lifecycle == WDTournamentLifecycle.RegistrationOpen ? "PARTICIPAR" : "VER DETALHES",
            8f, 820f, 656f, 52f, () => openDetails?.Invoke(tournament.Id), true);
    }

    private void FeaturedInfo(string name, string label, string value, float top)
    {
        RectTransform row = TopRect(name, featuredRoot, 8f, top, 656f, 72f);
        Image fill = row.gameObject.AddComponent<Image>();
        fill.color = new Color(5f / 255f, 12f / 255f, 22f / 255f, .72f);
        fill.raycastTarget = false;
        WDHomeUIFactory.ApplyRounded(fill, 6);
        TextAt("Label", row, label, 11f, Cyan, 12f, 10f, 620f, 18f, TextAlignmentOptions.Left);
        TextAt("Value", row, value, 13f, White, 12f, 35f, 620f, 22f, TextAlignmentOptions.Left);
    }

    // ============================================================
    // 05. PRIMITIVAS VISUAIS LOCAIS, SEM NOVO SISTEMA DE TEMA
    // ============================================================

    private static string Safe(string value) => string.IsNullOrWhiteSpace(value) ? "A DEFINIR" : value.ToUpperInvariant();
    private static string LifecycleLabel(WDTournamentLifecycle value) => value switch
    {
        WDTournamentLifecycle.RegistrationOpen => "INSCRIÇÕES ABERTAS",
        WDTournamentLifecycle.InProgress => "EM ANDAMENTO",
        WDTournamentLifecycle.ComingSoon => "EM BREVE",
        _ => "ENCERRADO"
    };
    private static string FormatLabel(WDTournamentFormat value) => value switch
    {
        WDTournamentFormat.OneVsOne => "1V1",
        WDTournamentFormat.TwoVsTwo => "2V2",
        _ => "3V3"
    };

    private void ButtonAt(string name, Transform parent, string label, float x, float y,
        float width, float height, UnityAction action, bool primary)
    {
        RectTransform rect = TopRect(name, parent, x, y, width, height);
        Color secondaryFill = new Color(11f / 255f, 20f / 255f, 32f / 255f, .96f);
        Color secondaryBorder = new Color(Gold.r, Gold.g, Gold.b, .30f);
        Surface(rect, primary ? Gold : secondaryFill, 6,
            primary ? new Color(Gold.r, Gold.g, Gold.b, .82f) : secondaryBorder);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        button.transition = Selectable.Transition.ColorTint;
        button.interactable = action != null;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = primary ? new Color(1f, .96f, .86f, 1f) : new Color(.78f, 1f, 1f, 1f);
        colors.pressedColor = primary ? new Color(.84f, .78f, .66f, 1f) : new Color(.55f, .88f, .92f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(.52f, .56f, .62f, .58f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = .08f;
        button.colors = colors;
        if (action != null) button.onClick.AddListener(action);
        TextMeshProUGUI labelText = Text("Label", rect, label, 13f,
            primary ? new Color(.04f, .05f, .07f, 1f) : White, TextAlignmentOptions.Center);
        labelText.font = WDHomeTypography.Load("Gabarito-Bold") ?? labelText.font;
    }

    private TextMeshProUGUI TextAt(string name, Transform parent, string value, float size,
        Color color, float x, float y, float width, float height, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI text = Text(name, parent, value, size, color, alignment);
        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return text;
    }

    private TextMeshProUGUI Text(string name, Transform parent, string value, float size,
        Color color, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI text = WDHomeUIFactory.Text(name, parent, theme, value, size, color, alignment);
        TMP_FontAsset selectedFont = size >= 17f
            ? titleFont ?? WDHomeTypography.Load("Gabarito-Bold")
            : size <= 11f ? WDHomeTypography.Load("Inter-Bold")
            : WDHomeTypography.Load("Inter-Regular") ?? bodyFont;
        if (selectedFont != null) text.font = selectedFont;
        text.fontStyle = FontStyles.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform Rect(string name, Transform parent) => WDHomeUIFactory.Rect(name, parent);

    private static RectTransform TopRect(string name, Transform parent, float x, float y,
        float width, float height)
    {
        RectTransform rect = Rect(name, parent);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static void Surface(RectTransform target, Color fill, int radius, Color border)
    {
        Image image = target.gameObject.AddComponent<Image>();
        image.color = fill;
        image.raycastTarget = true;
        WDHomeUIFactory.ApplyRounded(image, radius);
        if (border.a > 0f) WDHomeUIFactory.AddThinRoundedBorder(target, border, radius);
    }

    private static void Clear(Transform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            child.SetActive(false);
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
    }
}
