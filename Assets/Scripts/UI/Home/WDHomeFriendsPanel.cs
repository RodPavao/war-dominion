using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public enum WDFriendsTab { Friends, Requests, Blocked }

[DisallowMultipleComponent]
public sealed class WDHomeFriendsPanel : MonoBehaviour
{
    [Serializable] private sealed class ViewState { public WDFriendsTab Tab; public string Query; }

    private static readonly Color Gold = Hex("C59B63");
    private static readonly Color White = Hex("F5F7FA");
    private static readonly Color Muted = Hex("8794A3");
    private static readonly Color Cyan = Hex("0DC2DB");
    private static readonly Color Red = Hex("A64A47");
    private static readonly Color Green = Hex("33D17A");
    private static readonly Color Panel = new(.035f, .055f, .075f, .90f);
    private static readonly Color RowA = new(.067f, .075f, .094f, .68f);
    private static readonly Color RowB = new(.055f, .075f, .102f, .68f);
    private static readonly Color Control = new(.086f, .094f, .122f, .72f);

    private WarDominionUITheme theme;
    private WDSocialRepository social, subscribedSocial;
    private WDNavigationStack navigation;
    private Action<string> openProfile, openChat;
    private Action<string, string, Action> confirm;
    private Action<bool> homeVisibility;
    private Texture2D background;
    private RectTransform shell, overlay, centerPanel;
    private TMP_InputField searchInput, addInput;
    private string openMenuPlayerId, modalResultPlayerId;
    private int statusFilter;
    private bool descending;

    public bool IsOpen => gameObject.activeSelf;
    public bool IsAddModalOpen => addInput != null;
    public WDFriendsTab CurrentTab { get; private set; }
    public string SearchQuery { get; private set; } = "";

    // 01. AUTORIDADES E LIFECYCLE
    public void Build(WarDominionUITheme t, WDSocialRepository repository,
        WDNavigationStack nav, Action<string> profile, Action<string> chat,
        Action<string, string, Action> confirmation, Texture2D backgroundTexture = null,
        Action<bool> home = null)
    {
        Unsubscribe();
        theme = t ?? throw new ArgumentNullException(nameof(t));
        social = repository ?? throw new ArgumentNullException(nameof(repository));
        navigation = nav ?? throw new ArgumentNullException(nameof(nav));
        openProfile = profile;
        openChat = chat;
        confirm = confirmation;
        background = backgroundTexture;
        homeVisibility = home;
        Image inputBlock = gameObject.AddComponent<Image>();
        inputBlock.color = Color.clear;
        inputBlock.raycastTarget = true;
        BuildShell();
        gameObject.SetActive(false);
    }

    private void BuildShell()
    {
        RectTransform scenery = R("FriendsFullscreenBackground", transform);
        Stretch(scenery);
        RawImage art = scenery.gameObject.AddComponent<RawImage>();
        art.texture = background;
        art.raycastTarget = false;
        AspectRatioFitter crop = scenery.gameObject.AddComponent<AspectRatioFitter>();
        crop.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        crop.aspectRatio = background != null ? (float)background.width / background.height : 16f / 9f;
        Surface("BackgroundVeil", transform, 0, 0, 0, 0, new Color(.012f, .02f, .035f, .66f));
        Stretch(transform.Find("BackgroundVeil") as RectTransform);
        shell = R("FriendsResponsiveShell", transform);
        Stretch(shell);
        overlay = R("FriendsOverlay", shell);
        Stretch(overlay);
        Canvas overlayCanvas = overlay.gameObject.AddComponent<Canvas>();
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = (rootCanvas != null ? rootCanvas.sortingOrder : 0) + 100;
        overlay.gameObject.AddComponent<GraphicRaycaster>();
        CanvasGroup overlayGroup = overlay.gameObject.AddComponent<CanvasGroup>();
        overlayGroup.interactable = true;
        overlayGroup.blocksRaycasts = true;
        overlay.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (social == null || subscribedSocial == social) return;
        Unsubscribe();
        social.Changed += Refresh;
        subscribedSocial = social;
    }

    private void OnDisable() => Unsubscribe();
    private void OnDestroy() => Unsubscribe();
    private void Unsubscribe()
    {
        if (subscribedSocial == null) return;
        subscribedSocial.Changed -= Refresh;
        subscribedSocial = null;
    }

    public void Open()
    {
        social.EnsureDevelopmentPreviewData();
        CurrentTab = WDFriendsTab.Friends;
        SearchQuery = "";
        navigation.Push(new WDNavigationContext("friends", Restore,
            () => new ViewState { Tab = CurrentTab, Query = SearchQuery }, HideFromNavigation));
    }

    public void HideFromNavigation()
    {
        CloseOverlay();
        openMenuPlayerId = null;
        gameObject.SetActive(false);
        homeVisibility?.Invoke(true);
    }

    public bool TryHandleLocalBack()
    {
        if (overlay.gameObject.activeSelf) { CloseOverlay(); return true; }
        if (!string.IsNullOrEmpty(openMenuPlayerId)) { openMenuPlayerId = null; Render(); return true; }
        return false;
    }

    private void Restore(object value)
    {
        if (value is ViewState state) { CurrentTab = state.Tab; SearchQuery = state.Query ?? ""; }
        homeVisibility?.Invoke(false);
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        Render();
    }

    private void Refresh() { if (IsOpen) Render(); }

    // 02. SHELL RESPONSIVO COMPARTILHADO — NODES 2998:137 / 3002:2 / 3002:230
    private void Render()
    {
        for (int i = shell.childCount - 1; i >= 0; i--)
        {
            Transform child = shell.GetChild(i);
            if (child != overlay) DestroyNow(child.gameObject);
        }

        RectTransform header = Box("FriendsHeader", shell, 40, 28, 1840, 132, Panel, 24,
            new Color(Gold.r, Gold.g, Gold.b, .20f));
        StretchHorizontal(header, 40, 40);
        WDHomeBackButtonFactory.BuildCanonical(header, () => navigation.TryBack());
        TextMeshProUGUI title = Text("Title", header, "AMIGOS", 34, White, 0, 13, 600, 54,
            TextAlignmentOptions.Center, "Gabarito-Black");
        CenterTop(title.rectTransform, 0, 13, 600, 54);
        RectTransform tabs = R("FriendsTabs", header);
        CenterTop(tabs, 0, 81, 724, 42);
        Tab(tabs, WDFriendsTab.Friends, "AMIGOS", 0, 210);
        Tab(tabs, WDFriendsTab.Requests, $"SOLICITAÇÕES ({social.PendingReceivedCount})", 226, 272);
        Tab(tabs, WDFriendsTab.Blocked, "BLOQUEADOS", 514, 210);

        BuildLeftRail();
        BuildRightRail();
        centerPanel = Box("FriendsMain", shell, 364, 180, 1132, 860, Panel, 24,
            new Color(Gold.r, Gold.g, Gold.b, .24f));
        StretchBetween(centerPanel, 364, 424, 180, 40);

        if (CurrentTab == WDFriendsTab.Friends) RenderFriends();
        else if (CurrentTab == WDFriendsTab.Requests) RenderRequests();
        else RenderBlocked();
        overlay.SetAsLastSibling();
    }

    private void Tab(Transform parent, WDFriendsTab tab, string label, float x, float width)
    {
        bool selected = CurrentTab == tab;
        RectTransform root = Box("Tab_" + tab, parent, x, 0, width, 42,
            selected ? new Color(Gold.r, Gold.g, Gold.b, .20f) : Control, 11,
            selected ? new Color(Gold.r, Gold.g, Gold.b, .72f) : new Color(1, 1, 1, .08f));
        Text("Label", root, label, 14, selected ? Gold : Muted, 0, 0, width, 42,
            TextAlignmentOptions.Center, "Inter-SemiBold");
        Button button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = root.GetComponent<Image>();
        button.onClick.AddListener(() =>
        {
            CurrentTab = tab;
            openMenuPlayerId = null;
            CloseOverlay();
            Render();
        });
    }

    // 03. COLUNA ESQUERDA — ESTATÍSTICAS E CTA
    private void BuildLeftRail()
    {
        RectTransform rail = R("FriendsLeftRail", shell);
        LeftStretchVertical(rail, 40, 180, 40, 300);
        RectTransform stats = Box("Statistics", rail, 0, 0, 300, 420, Panel, 24,
            new Color(Gold.r, Gold.g, Gold.b, .24f));
        IReadOnlyList<WDProfileData> friends = social.Friends();
        int online = friends.Count(p => social.PresenceOf(p.PlayerId) == WDSocialPresence.Online);
        int inMatch = friends.Count(p => social.PresenceOf(p.PlayerId) == WDSocialPresence.InMatch);
        int offline = friends.Count - online - inMatch;
        Text("Heading", stats, "AMIGOS", 22, White, 23, 16, 170, 34, TextAlignmentOptions.Left, "Gabarito-Black");
        Text("Count", stats, friends.Count.ToString(), 26, Gold, 222, 15, 53, 36, TextAlignmentOptions.Right, "Gabarito-Black");
        Text("Caption", stats, "SUA REDE ATUAL", 11, Muted, 23, 49, 220, 20, TextAlignmentOptions.Left, "Inter-Medium");
        Surface("Divider", stats, 23, 83, 252, 1, new Color(Gold.r, Gold.g, Gold.b, .24f));
        StatCard(stats, "Online", "ONLINE", "DISPONÍVEIS AGORA", online, 109, Green);
        StatCard(stats, "InMatch", "EM PARTIDA", "EM UMA BATALHA", inMatch, 197, Cyan);
        StatCard(stats, "Offline", "OFFLINE", "FORA DO JOGO", offline, 285, Muted);

        RectTransform play = Box("PlayTogether", rail, 0, 444, 300, 416, Panel, 24,
            new Color(Gold.r, Gold.g, Gold.b, .34f));
        RectTransform icon = Box("AddIcon", play, 91, 33, 116, 116,
            new Color(Gold.r, Gold.g, Gold.b, .10f), 58, new Color(Gold.r, Gold.g, Gold.b, .55f));
        Text("Glyph", icon, "+", 42, Gold, 0, 0, 116, 116, TextAlignmentOptions.Center, "Gabarito-Medium");
        Text("Title", play, "JOGUEM JUNTOS", 23, White, 24, 167, 252, 40, TextAlignmentOptions.Center, "Gabarito-Black");
        TextMeshProUGUI description = Text("Description", play,
            "Adicione aliados, forme seu\nesquadrão e entre em combate com\nquem você confia.", 14, Muted,
            30, 220, 240, 78, TextAlignmentOptions.Center, "Inter-Regular");
        description.textWrappingMode = TextWrappingModes.Normal;
        ActionButton(play, "AddFriend", "ADICIONAR AMIGO", 23, 321, 252, 56,
            Hex("14110D"), new Color(Gold.r, Gold.g, Gold.b, .92f), OpenAddModal, Hex("14110D"), 12);
    }

    private void StatCard(Transform parent, string name, string label, string caption, int count, float y, Color accent)
    {
        RectTransform card = Box(name, parent, 19, y, 260, 68, new Color(.086f, .094f, .122f, .62f), 14,
            new Color(accent.r, accent.g, accent.b, .16f));
        Surface("Dot", card, 17, 29, 9, 9, accent, 5);
        Text("Label", card, label, 14, White, 41, 10, 145, 24, TextAlignmentOptions.Left, "Inter-SemiBold");
        Text("Caption", card, caption, 10, Muted, 41, 32, 150, 18, TextAlignmentOptions.Left, "Inter-Medium");
        Text("Count", card, count.ToString(), 24, accent, 205, 14, 34, 40, TextAlignmentOptions.Right, "Gabarito-Black");
    }

    // 04. COLUNA DIREITA — RESUMO E JOGADORES RECENTES
    private void BuildRightRail()
    {
        RectTransform rail = R("FriendsRightRail", shell);
        RightStretchVertical(rail, 40, 180, 40, 360);
        RectTransform summary = Box("NetworkSummary", rail, 0, 0, 360, 352, Panel, 24,
            new Color(Gold.r, Gold.g, Gold.b, .24f));
        Text("Title", summary, "RESUMO DA REDE", 20, White, 23, 16, 300, 34, TextAlignmentOptions.Left, "Gabarito-Black");
        Text("Caption", summary, "ATIVIDADE DOS SEUS AMIGOS", 10, Muted, 23, 48, 300, 18, TextAlignmentOptions.Left, "Inter-Medium");
        BuildNetworkDonut(summary);

        RectTransform recent = Box("RecentPlayers", rail, 0, 376, 360, 484, Panel, 24,
            new Color(Gold.r, Gold.g, Gold.b, .24f));
        Text("Title", recent, "JOGADORES RECENTES", 20, White, 23, 16, 310, 34, TextAlignmentOptions.Left, "Gabarito-Black");
        Text("Caption", recent, "ÚLTIMOS ENCONTROS", 10, Muted, 23, 49, 280, 18, TextAlignmentOptions.Left, "Inter-Medium");
        IReadOnlyList<WDProfileData> players = social.RecentPlayers(4);
        for (int i = 0; i < players.Count; i++) RecentPlayerRow(recent, players[i], 86 + i * 74, i);
        ActionButton(recent, "All", "VER TODOS OS RECENTES", 20, 408, 320, 52,
            Gold, Control, () => { }, Gold, 10);
    }

    private void BuildNetworkDonut(RectTransform parent)
    {
        IReadOnlyList<WDProfileData> friends = social.Friends();
        int online = friends.Count(p => social.PresenceOf(p.PlayerId) == WDSocialPresence.Online);
        int match = friends.Count(p => social.PresenceOf(p.PlayerId) == WDSocialPresence.InMatch);
        int offline = Mathf.Max(0, friends.Count - online - match);
        RectTransform ring = R("Donut", parent); P(ring, 92, 85, 176, 176);
        AspectRatioFitter ratio = ring.gameObject.AddComponent<AspectRatioFitter>();
        ratio.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
        ratio.aspectRatio = 1f;
        float total = Mathf.Max(1, friends.Count);
        Image baseCircle = Circle("RingBase", ring, Control); Stretch(baseCircle.rectTransform);
        const float segmentGap = .012f;
        float onlineRatio = online / total;
        float matchRatio = match / total;
        float offlineRatio = offline / total;
        RingSlice(ring, "Online", Mathf.Max(0f, onlineRatio - segmentGap), 0f, Green);
        RingSlice(ring, "Match", Mathf.Max(0f, matchRatio - segmentGap), onlineRatio, Cyan);
        RingSlice(ring, "Offline", Mathf.Max(0f, offlineRatio - segmentGap), onlineRatio + matchRatio, Muted);
        Image center = Circle("Center", ring, Panel); P(center.rectTransform, 27, 27, 122, 122);
        Text("Count", ring, friends.Count.ToString(), 38, White, 28, 48, 120, 50, TextAlignmentOptions.Center, "Gabarito-Black");
        Text("Label", ring, "AMIGOS", 10, Muted, 28, 97, 120, 20, TextAlignmentOptions.Center, "Inter-Medium");
        NetworkLegend(parent, "Online", "ONLINE", online, 24, Green);
        NetworkLegend(parent, "Match", "EM PARTIDA", match, 132, Cyan);
        NetworkLegend(parent, "Offline", "OFFLINE", offline, 250, Muted);
    }

    private void RingSlice(Transform parent, string name, float amount, float offset, Color color)
    {
        RectTransform rt = R(name, parent); Stretch(rt);
        Image image = rt.gameObject.AddComponent<Image>();
        image.sprite = RuntimeWhiteSprite(); image.color = color; image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Radial360; image.fillOrigin = 2; image.fillClockwise = true;
        image.fillAmount = amount; rt.localEulerAngles = new Vector3(0, 0, -offset * 360f);
    }

    private void NetworkLegend(Transform parent, string name, string label, int value, float x, Color color)
    {
        Surface(name + "Dot", parent, x, 286, 9, 9, color, 5);
        Text(name + "Label", parent, label, 9, Muted, x + 15, 278, 84, 18, TextAlignmentOptions.Left, "Inter-Medium");
        Text(name + "Value", parent, value.ToString(), 17, color, x + 15, 298, 50, 24, TextAlignmentOptions.Left, "Gabarito-Black");
    }

    private void RecentPlayerRow(Transform parent, WDProfileData profile, float y, int index)
    {
        RectTransform row = Box("Recent_" + profile.PlayerId, parent, 20, y, 320, 62,
            index % 2 == 0 ? RowA : RowB, 12, new Color(1, 1, 1, .06f));
        Avatar(row, profile, 10, 7, 48, Gold);
        Text("Name", row, profile.Nickname.ToUpperInvariant(), 14, White, 66, 8, 184, 23, TextAlignmentOptions.Left, "Gabarito-Bold");
        Text("Seen", row, index == 2 ? "Encontrado há 1 h" : $"Partida há {12 + index * 26} min", 10, Muted,
            66, 31, 190, 18, TextAlignmentOptions.Left, "Inter-Regular");
        ActionButton(row, "Add", "+", 270, 12, 36, 38, Cyan, new Color(Cyan.r, Cyan.g, Cyan.b, .08f),
            () => { if (social.Eligibility(profile) == WDAddFriendEligibility.Eligible) social.SendFriendRequest(profile.PlayerId); }, Cyan, 9);
    }

    // 05. LISTA PRINCIPAL — NODE 2998:137
    private void RenderFriends()
    {
        IReadOnlyList<WDProfileData> list = FilteredFriends();
        Text("Heading", centerPanel, "LISTA DE AMIGOS", 20, White, 27, 16, 260, 34, TextAlignmentOptions.Left, "Gabarito-Black");
        Text("Count", centerPanel, $"{list.Count} JOGADORES", 11, Gold, 287, 19, 130, 28, TextAlignmentOptions.Left, "Inter-Medium");
        BuildSearchControls(list.Count);
        BuildFriendHeader();
        RectTransform viewport = R("RowsViewport", centerPanel); Stretch(viewport, 27, 183, 27, 24);
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform rows = R("Rows", viewport); Stretch(rows);
        if (list.Count == 0)
        {
            Text("Empty", rows, "NENHUM AMIGO ENCONTRADO.", 16, Muted, 0, 120, 600, 44,
                TextAlignmentOptions.Center, "Inter-SemiBold");
            return;
        }
        int visible = Mathf.Min(7, list.Count);
        for (int i = 0; i < visible; i++) FriendRow(rows, list[i], i);
        if (!string.IsNullOrEmpty(openMenuPlayerId)) OpenContextMenu(list);
    }

    private IReadOnlyList<WDProfileData> FilteredFriends()
    {
        IEnumerable<WDProfileData> source = social.Friends(SearchQuery);
        if (statusFilter > 0)
        {
            WDSocialPresence wanted = statusFilter == 1 ? WDSocialPresence.Online :
                statusFilter == 2 ? WDSocialPresence.InMatch : WDSocialPresence.Offline;
            source = source.Where(p => social.PresenceOf(p.PlayerId) == wanted);
        }
        source = descending ? source.OrderByDescending(p => p.Nickname, StringComparer.OrdinalIgnoreCase) :
            source.OrderBy(p => p.Nickname, StringComparer.OrdinalIgnoreCase);
        return source.ToArray();
    }

    private void BuildSearchControls(int count)
    {
        RectTransform search = Box("Search", centerPanel, 27, 67, 500, 48, new Color(.067f, .075f, .094f, .82f), 12,
            new Color(1, 1, 1, .10f));
        Image icon = Sprite("SearchIcon", search, "UI/Friends/Canonical/icon_search"); P(icon.rectTransform, 15, 14, 20, 20); icon.color = Muted;
        searchInput = Input(search, "Input", "BUSCAR POR NOME OU ID", SearchQuery, 49, 4, 430, 40);
        searchInput.onEndEdit.AddListener(value => { SearchQuery = value; Render(); });
        string[] labels = { "TODOS OS STATUS", "ONLINE", "EM PARTIDA", "OFFLINE" };
        ActionButton(centerPanel, "StatusFilter", labels[statusFilter], 547, 67, 260, 48, Muted, Control,
            () => { statusFilter = (statusFilter + 1) % labels.Length; Render(); }, Muted, 11);
        ActionButton(centerPanel, "Sort", descending ? "ORDENAR: Z–A" : "ORDENAR: A–Z", 827, 67, 276, 48,
            Muted, Control, () => { descending = !descending; Render(); }, Muted, 11);
        RightTop(centerPanel.Find("Sort") as RectTransform, 27, 67, 276, 48);
        RightTop(centerPanel.Find("StatusFilter") as RectTransform, 323, 67, 260, 48);
        StretchHorizontal(search, 27, 603);
    }

    private void BuildFriendHeader()
    {
        RectTransform header = Box("TableHeader", centerPanel, 27, 135, 1076, 36,
            new Color(.086f, .094f, .122f, .54f), 9, Color.clear);
        StretchHorizontal(header, 27, 27);
        HeaderText(header, "JOGADOR", 20, 0, 310);
        HeaderText(header, "DIVISÃO", 330, 0, 160);
        HeaderText(header, "STATUS", 500, 0, 170);
        HeaderText(header, "ATIVIDADE", 670, 0, 190);
        Text("Actions", header, "AÇÕES", 11, Muted, 876, 0, 174, 36, TextAlignmentOptions.Right, "Inter-SemiBold");
    }

    private void HeaderText(Transform parent, string value, float x, float y, float width) =>
        Text("Header_" + value, parent, value, 11, Muted, x, y, width, 36, TextAlignmentOptions.Left, "Inter-SemiBold");

    private void FriendRow(RectTransform rows, WDProfileData profile, int index)
    {
        RectTransform row = Box("Friend_" + profile.PlayerId, rows, 0, index * 88, 1076, 76,
            index % 2 == 0 ? RowA : RowB, 14, new Color(1, 1, 1, .06f));
        StretchHorizontal(row, 0, 0);
        WDSocialPresence presence = social.PresenceOf(profile.PlayerId);
        Color status = PresenceColor(presence);
        Avatar(row, profile, 15, 11, 52, status);
        Text("Name", row, profile.Nickname.ToUpperInvariant(), 17, White, 81, 8, 230, 26, TextAlignmentOptions.Left, "Gabarito-Bold");
        Text("Id", row, "ID #" + profile.PlayerId.Replace("WD-", ""), 10, Muted, 81, 34, 170, 18, TextAlignmentOptions.Left, "Inter-Regular");
        Text("Division", row, profile.Division.ToUpperInvariant(), 13, Gold, 329, 0, 155, 76, TextAlignmentOptions.Left, "Inter-SemiBold");
        Surface("PresenceDot", row, 499, 33, 9, 9, status, 5);
        Text("Presence", row, PresenceLabel(presence), 12, status, 517, 0, 145, 76, TextAlignmentOptions.Left, "Inter-SemiBold");
        Text("Activity", row, ActivityLabel(presence, index), 13, presence == WDSocialPresence.Offline ? Muted : White,
            669, 0, 180, 76, TextAlignmentOptions.Left, "Inter-Regular");
        string primary = presence == WDSocialPresence.InMatch ? "ASSISTIR" : "CHAT";
        Color primaryColor = presence == WDSocialPresence.Offline ? Hex("4F5966") : Cyan;
        ActionButton(row, "Primary", primary, 857, 20, 88, 34, primaryColor,
            presence == WDSocialPresence.Offline ? new Color(.086f, .094f, .122f, .22f) : new Color(Cyan.r, Cyan.g, Cyan.b, .12f),
            () => { if (presence != WDSocialPresence.Offline) openChat?.Invoke(profile.PlayerId); }, primaryColor, 9);
        ActionButton(row, "Profile", "PERFIL", 953, 20, 78, 34, Muted, Control, () => openProfile?.Invoke(profile.PlayerId), Muted, 9);
        ActionButton(row, "More", "•••", 1039, 20, 28, 34, Muted, Control,
            () => { openMenuPlayerId = openMenuPlayerId == profile.PlayerId ? null : profile.PlayerId; Render(); }, Muted, 9);
        RightTop(row.Find("More") as RectTransform, 9, 20, 28, 34);
        RightTop(row.Find("Profile") as RectTransform, 45, 20, 78, 34);
        RightTop(row.Find("Primary") as RectTransform, 131, 20, 88, 34);
    }

    // 06. SOLICITAÇÕES — NODE 3002:2
    private void RenderRequests()
    {
        IReadOnlyList<WDProfileData> list = social.ReceivedRequests();
        Text("Heading", centerPanel, "SOLICITAÇÕES RECEBIDAS", 20, White, 27, 16, 290, 34, TextAlignmentOptions.Left, "Gabarito-Black");
        Text("Count", centerPanel, $"{list.Count} REGISTROS", 11, Gold, 287, 19, 130, 28, TextAlignmentOptions.Left, "Inter-Medium");
        Text("Total", centerPanel, $"{list.Count} SOLICITAÇÕES RECEBIDAS", 14, Muted, 730, 16, 370, 34, TextAlignmentOptions.Right, "Inter-SemiBold");
        BuildRequestHeader();
        if (list.Count == 0) { Empty("NENHUMA SOLICITAÇÃO PENDENTE."); return; }
        int visible = Mathf.Min(3, list.Count);
        for (int i = 0; i < visible; i++) RequestRow(list[i], i);
        RectTransform help = Box("RequestsHelp", centerPanel, 27, 729, 1076, 104, RowA, 14, new Color(1, 1, 1, .06f));
        StretchHorizontal(help, 27, 27);
        Text("Title", help, "GERENCIE SUAS SOLICITAÇÕES", 19, White, 20, 14, 500, 30, TextAlignmentOptions.Left, "Gabarito-Black");
        TextMeshProUGUI body = Text("Body", help,
            "ACEITAR adiciona o jogador à sua lista. RECUSAR descarta a solicitação. PERFIL permite consultar o jogador.",
            13, Muted, 20, 48, 1000, 36, TextAlignmentOptions.Left, "Inter-Regular");
        body.textWrappingMode = TextWrappingModes.Normal;
    }

    private void BuildRequestHeader()
    {
        RectTransform header = Box("RequestHeader", centerPanel, 27, 75, 1076, 36, new Color(.086f, .094f, .122f, .54f), 9, Color.clear);
        StretchHorizontal(header, 27, 27);
        HeaderText(header, "JOGADOR", 20, 0, 380);
        HeaderText(header, "DIVISÃO", 419, 0, 180);
        Text("Actions", header, "AÇÕES", 11, Muted, 760, 0, 290, 36, TextAlignmentOptions.Right, "Inter-SemiBold");
    }

    private void RequestRow(WDProfileData profile, int index)
    {
        RectTransform row = Box("Request_" + profile.PlayerId, centerPanel, 27, 123 + index * 180, 1076, 164,
            index % 2 == 0 ? RowA : RowB, 14, new Color(1, 1, 1, .06f));
        StretchHorizontal(row, 27, 27);
        Avatar(row, profile, 23, 23, 76, Gold);
        Text("Name", row, profile.Nickname, 17, White, 119, 18, 280, 30, TextAlignmentOptions.Left, "Gabarito-Bold");
        Text("Id", row, "ID — não informado", 10, Muted, 119, 51, 250, 20, TextAlignmentOptions.Left, "Inter-Regular");
        Text("Division", row, profile.Division.ToUpperInvariant(), 13, Gold, 419, 20, 190, 30, TextAlignmentOptions.Left, "Inter-SemiBold");
        Text("State", row, "SOLICITAÇÃO DE AMIZADE RECEBIDA", 12, Gold, 119, 106, 470, 24, TextAlignmentOptions.Left, "Inter-SemiBold");
        ActionButton(row, "Accept", "ACEITAR", 725, 33, 112, 40, Cyan, new Color(Cyan.r, Cyan.g, Cyan.b, .12f),
            () => social.AcceptReceived(profile.PlayerId), Cyan, 9);
        ActionButton(row, "Decline", "RECUSAR", 845, 33, 112, 40, Red, new Color(Red.r, Red.g, Red.b, .12f),
            () => social.DeclineReceived(profile.PlayerId), Red, 9);
        ActionButton(row, "Profile", "PERFIL", 969, 33, 84, 40, Muted, Control,
            () => openProfile?.Invoke(profile.PlayerId), Muted, 9);
        RightTop(row.Find("Profile") as RectTransform, 23, 33, 84, 40);
        RightTop(row.Find("Decline") as RectTransform, 119, 33, 112, 40);
        RightTop(row.Find("Accept") as RectTransform, 239, 33, 112, 40);
    }

    // 07. BLOQUEADOS — NODE 3002:230
    private void RenderBlocked()
    {
        IReadOnlyList<WDProfileData> list = social.BlockedPlayers()
            .Where(profile => string.IsNullOrWhiteSpace(SearchQuery) ||
                profile.Nickname.IndexOf(SearchQuery, StringComparison.OrdinalIgnoreCase) >= 0 ||
                profile.PlayerId.IndexOf(SearchQuery, StringComparison.OrdinalIgnoreCase) >= 0)
            .OrderBy(profile => profile.Nickname, StringComparer.OrdinalIgnoreCase).ToArray();
        Text("Heading", centerPanel, "JOGADORES BLOQUEADOS", 20, White, 27, 16, 300, 34, TextAlignmentOptions.Left, "Gabarito-Black");
        Text("Count", centerPanel, $"{list.Count} JOGADORES", 11, Gold, 287, 19, 120, 28, TextAlignmentOptions.Left, "Inter-Medium");
        RectTransform search = Box("Search", centerPanel, 27, 67, 500, 48, new Color(.067f, .075f, .094f, .82f), 12, new Color(1, 1, 1, .1f));
        Image icon = Sprite("SearchIcon", search, "UI/Friends/Canonical/icon_search"); P(icon.rectTransform, 15, 14, 20, 20); icon.color = Muted;
        searchInput = Input(search, "Input", "BUSCAR POR NOME OU ID", SearchQuery, 49, 4, 430, 40);
        searchInput.onEndEdit.AddListener(value => { SearchQuery = value; Render(); });
        ActionButton(centerPanel, "BlockedCount", $"{list.Count} JOGADORES BLOQUEADOS", 547, 67, 260, 48, Muted, Control, () => { }, Muted, 11);
        ActionButton(centerPanel, "Sort", "ORDENAR: A–Z", 827, 67, 276, 48, Muted, Control, () => { }, Muted, 11);
        RightTop(centerPanel.Find("Sort") as RectTransform, 27, 67, 276, 48);
        RightTop(centerPanel.Find("BlockedCount") as RectTransform, 323, 67, 260, 48);
        StretchHorizontal(search, 27, 603);
        BuildBlockedHeader();
        if (list.Count == 0) { Empty("NENHUM JOGADOR BLOQUEADO."); return; }
        RectTransform viewport = R("BlockedViewport", centerPanel);
        P(viewport, 27, 183, 1076, 465);
        StretchHorizontal(viewport, 27, 27);
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = R("BlockedContent", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, Mathf.Max(465f, list.Count * 240f - 20f));
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 36f;
        for (int i = 0; i < list.Count; i++) BlockedRow(content, list[i], i);
        RectTransform help = Box("BlockedHelp", centerPanel, 27, 668, 1076, 164, RowA, 14, new Color(1, 1, 1, .06f));
        StretchHorizontal(help, 27, 27);
        Text("Title", help, "GERENCIE SEUS BLOQUEADOS", 19, White, 24, 18, 500, 30, TextAlignmentOptions.Left, "Gabarito-Black");
        TextMeshProUGUI body = Text("Body", help,
            "Os jogadores acima estão na sua lista de bloqueados. Use DESBLOQUEAR na linha correspondente para remover o bloqueio.",
            13, Muted, 24, 64, 750, 52, TextAlignmentOptions.Left, "Inter-Regular");
        body.textWrappingMode = TextWrappingModes.Normal;
        Text("Total", help, list.Count.ToString(), 38, Gold, 850, 28, 150, 48, TextAlignmentOptions.Center, "Gabarito-Black");
        Text("Label", help, "BLOQUEADOS", 10, Muted, 850, 80, 150, 20, TextAlignmentOptions.Center, "Inter-Medium");
    }

    private void BuildBlockedHeader()
    {
        RectTransform header = Box("BlockedHeader", centerPanel, 27, 135, 1076, 36, new Color(.086f, .094f, .122f, .54f), 9, Color.clear);
        StretchHorizontal(header, 27, 27);
        HeaderText(header, "JOGADOR", 20, 0, 390);
        HeaderText(header, "DIVISÃO", 419, 0, 160);
        HeaderText(header, "STATUS", 580, 0, 170);
        Text("Actions", header, "AÇÕES", 11, Muted, 780, 0, 270, 36, TextAlignmentOptions.Right, "Inter-SemiBold");
    }

    private void BlockedRow(Transform parent, WDProfileData profile, int index)
    {
        RectTransform row = Box("Blocked_" + profile.PlayerId, parent, 0, index * 240, 1076, 220,
            index % 2 == 0 ? RowA : RowB, 14, new Color(1, 1, 1, .06f));
        StretchHorizontal(row, 0, 0);
        Avatar(row, profile, 23, 32, 76, Red);
        Text("Name", row, profile.Nickname, 17, White, 119, 28, 280, 30, TextAlignmentOptions.Left, "Gabarito-Bold");
        Text("Id", row, "ID não informado", 10, Muted, 119, 68, 250, 20, TextAlignmentOptions.Left, "Inter-Regular");
        Text("Division", row, profile.Division.ToUpperInvariant(), 13, Muted, 419, 28, 170, 30, TextAlignmentOptions.Left, "Inter-SemiBold");
        Text("State", row, "BLOQUEADO", 12, Red, 580, 28, 180, 30, TextAlignmentOptions.Left, "Inter-SemiBold");
        Text("Description", row, "Jogador na sua lista de bloqueados", 13, Muted, 119, 145, 550, 28, TextAlignmentOptions.Left, "Inter-Regular");
        ActionButton(row, "Unblock", "DESBLOQUEAR", 830, 144, 223, 48, Gold, new Color(Gold.r, Gold.g, Gold.b, .08f),
            () => social.Unblock(profile.PlayerId), Gold, 10);
        RightTop(row.Find("Unblock") as RectTransform, 23, 144, 223, 48);
    }

    private void Empty(string value) => Text("Empty", centerPanel, value, 16, Muted, 0, 220, 700, 50,
        TextAlignmentOptions.Center, "Inter-SemiBold");

    // 08. MODAL ADICIONAR AMIGO — NODE 3002:458
    private void OpenAddModal()
    {
        CloseOverlay();
        overlay.SetAsLastSibling();
        overlay.gameObject.SetActive(true);
        RectTransform blocker = R("ModalBlocker", overlay); Stretch(blocker);
        Image dim = blocker.gameObject.AddComponent<Image>(); dim.color = new Color(0, 0, 0, .72f); dim.raycastTarget = true;
        Button outside = blocker.gameObject.AddComponent<Button>(); outside.targetGraphic = dim; outside.onClick.AddListener(CloseOverlay);
        RectTransform panel = Box("AddFriendModal", overlay, 0, 0, 760, 440, new Color(.035f, .055f, .075f, .98f), 20,
            new Color(Gold.r, Gold.g, Gold.b, .72f));
        Center(panel, 760, 440);
        panel.SetAsLastSibling();
        Text("Title", panel, "ADICIONAR AMIGO", 28, White, 32, 28, 650, 40, TextAlignmentOptions.Left, "Gabarito-Black");
        Text("Hint", panel, "Encontre um jogador pelo nome ou ID.", 14, Muted, 32, 75, 650, 24, TextAlignmentOptions.Left, "Inter-Regular");
        RectTransform field = Box("SearchField", panel, 32, 114, 582, 52, RowA, 12, new Color(1, 1, 1, .10f));
        addInput = Input(field, "Input", "NOME OU ID", "", 16, 6, 550, 40);
        ActionButton(panel, "Search", "BUSCAR", 626, 114, 102, 52, Hex("14110D"), new Color(Gold.r, Gold.g, Gold.b, .92f),
            SearchCatalog, Hex("14110D"), 12);
        ActionButton(panel, "Cancel", "CANCELAR", 578, 366, 150, 46, Muted, Control, CloseOverlay, Muted, 10);
    }

    private void SearchCatalog()
    {
        WDProfileData profile = social.FindCatalogResult(addInput.text);
        modalResultPlayerId = profile?.PlayerId;
        RectTransform panel = overlay.Find("AddFriendModal") as RectTransform;
        Transform old = panel?.Find("Result"); if (old != null) DestroyNow(old.gameObject);
        Transform oldLabel = panel?.Find("ResultLabel"); if (oldLabel != null) DestroyNow(oldLabel.gameObject);
        if (panel == null) return;
        Text("ResultLabel", panel, profile == null ? "RESULTADO" : "RESULTADO ENCONTRADO", 11,
            profile == null ? Muted : Cyan, 32, 190, 300, 20, TextAlignmentOptions.Left, "Inter-SemiBold");
        RectTransform result = Box("Result", panel, 32, 216, 696, 90, RowA, 12, new Color(1, 1, 1, .08f));
        if (profile == null)
        {
            Text("Missing", result, "JOGADOR NÃO ENCONTRADO.", 14, Muted, 20, 25, 656, 40, TextAlignmentOptions.Center, "Inter-SemiBold");
            return;
        }
        Avatar(result, profile, 16, 15, 60, Gold);
        Text("Name", result, profile.Nickname, 16, White, 90, 16, 340, 26, TextAlignmentOptions.Left, "Gabarito-Bold");
        Text("Id", result, "ID " + profile.PlayerId, 10, Muted, 90, 46, 300, 18, TextAlignmentOptions.Left, "Inter-Regular");
        WDAddFriendEligibility eligibility = social.Eligibility(profile);
        ActionButton(result, "Add", EligibilityLabel(eligibility), 570, 22, 110, 46,
            eligibility == WDAddFriendEligibility.Eligible ? Cyan : Muted,
            eligibility == WDAddFriendEligibility.Eligible ? new Color(Cyan.r, Cyan.g, Cyan.b, .12f) : Control,
            () => { if (eligibility == WDAddFriendEligibility.Eligible) social.SendFriendRequest(modalResultPlayerId); SearchCatalog(); },
            eligibility == WDAddFriendEligibility.Eligible ? Cyan : Muted, 9);
        RightTop(result.Find("Add") as RectTransform, 16, 22, 110, 46);
    }

    // 09. MENU CONTEXTUAL — NODE 3002:686
    private void OpenContextMenu(IReadOnlyList<WDProfileData> list)
    {
        WDProfileData profile = list.FirstOrDefault(p => p.PlayerId == openMenuPlayerId);
        int index = profile == null ? 0 : list.ToList().FindIndex(p => p.PlayerId == profile.PlayerId);
        if (profile == null) return;
        CloseOverlay();
        overlay.SetAsLastSibling();
        overlay.gameObject.SetActive(true);
        RectTransform dismiss = R("MenuDismiss", overlay); Stretch(dismiss);
        Image hit = dismiss.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, .001f); hit.raycastTarget = true;
        Button close = dismiss.gameObject.AddComponent<Button>(); close.targetGraphic = hit;
        close.onClick.AddListener(() => { openMenuPlayerId = null; CloseOverlay(); Render(); });

        RectTransform menu = Box("FriendContextMenu", overlay, 0, 0, 240, 154,
            new Color(.035f, .055f, .075f, .98f), 12, new Color(Gold.r, Gold.g, Gold.b, .70f));
        menu.anchorMin = menu.anchorMax = new Vector2(1, 1); menu.pivot = new Vector2(1, 1);
        menu.anchoredPosition = new Vector2(-452, -(429 + Mathf.Min(index, 4) * 88));
        menu.SetAsLastSibling();
        WDProfileData selected = profile;
        ActionButton(menu, "View", "VER PERFIL", 16, 16, 208, 34, White, Control,
            () => { CloseOverlay(); openProfile?.Invoke(selected.PlayerId); }, White, 9);
        ActionButton(menu, "Remove", "REMOVER AMIGO", 16, 60, 208, 34, Gold, new Color(Gold.r, Gold.g, Gold.b, .08f),
            () => ConfirmRemove(selected), Gold, 9);
        ActionButton(menu, "Block", "BLOQUEAR", 16, 104, 208, 34, Red, new Color(Red.r, Red.g, Red.b, .16f),
            () => ConfirmBlock(selected), Red, 9);
    }

    private void CloseOverlay()
    {
        if (overlay == null) return;
        Clear(overlay);
        overlay.gameObject.SetActive(false);
        addInput = null;
        modalResultPlayerId = null;
    }

    private void ConfirmRemove(WDProfileData profile) => confirm?.Invoke("REMOVER AMIGO?",
        $"Remover {profile.Nickname} da sua lista de amigos?", () =>
        { social.RemoveFriend(profile.PlayerId); openMenuPlayerId = null; CloseOverlay(); Render(); });

    private void ConfirmBlock(WDProfileData profile) => confirm?.Invoke("BLOQUEAR JOGADOR?",
        $"Bloquear {profile.Nickname}? Amizade e solicitações serão removidas.", () =>
        { social.Block(profile.PlayerId); openMenuPlayerId = null; CloseOverlay(); Render(); });

    // 10. PRIMITIVAS VISUAIS
    private void Avatar(Transform parent, WDProfileData profile, float x, float y, float size, Color accent)
    {
        RectTransform root = R("Avatar", parent);
        P(root, x, y, size, size);
        RectTransform clip = R("CircularClip", root);
        Stretch(clip, 3, 3, 3, 3);
        Image maskShape = clip.gameObject.AddComponent<Image>();
        maskShape.sprite = RuntimeWhiteSprite();
        maskShape.color = Hex("16181F");
        maskShape.preserveAspect = true;
        maskShape.raycastTarget = false;
        Mask mask = clip.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;
        RectTransform portrait = R("Portrait", clip);
        Stretch(portrait);
        RawImage image = portrait.gameObject.AddComponent<RawImage>();
        image.texture = WDProfileIdentityVisuals.ResolveAvatar(profile);
        image.raycastTarget = false;
        image.color = Color.white;
        image.uvRect = new Rect(0f, 0f, 1f, 1f);
        WDHomeUIFactory.AddThinRoundedBorder(root,
            new Color(accent.r, accent.g, accent.b, .72f), Mathf.RoundToInt(size / 2));
    }

    private TMP_InputField Input(Transform parent, string name, string placeholder, string value,
        float x, float y, float width, float height)
    {
        RectTransform root = R(name, parent); P(root, x, y, width, height);
        Image target = root.gameObject.AddComponent<Image>(); target.color = new Color(0, 0, 0, .001f);
        TMP_InputField input = root.gameObject.AddComponent<TMP_InputField>(); input.targetGraphic = target;
        RectTransform viewport = R("Viewport", root); Stretch(viewport);
        TextMeshProUGUI text = Text("Text", viewport, value, 13, White, 0, 0, width, height, TextAlignmentOptions.Left, "Inter-Regular");
        TextMeshProUGUI hint = Text("Placeholder", viewport, placeholder, 13, Muted, 0, 0, width, height, TextAlignmentOptions.Left, "Inter-Regular");
        input.textViewport = viewport; input.textComponent = text; input.placeholder = hint;
        input.lineType = TMP_InputField.LineType.SingleLine; input.characterLimit = WDUIContentContracts.SearchMaxLength;
        input.text = value ?? "";
        return input;
    }

    private void ActionButton(Transform parent, string name, string label, float x, float y, float width,
        float height, Color accent, Color backgroundColor, UnityAction action, Color textColor, int radius)
    {
        RectTransform root = Box(name, parent, x, y, width, height, backgroundColor, radius,
            new Color(accent.r, accent.g, accent.b, Mathf.Max(.08f, accent.a)));
        Text("Label", root, label, 12, textColor, 4, 2, width - 8, height - 4,
            TextAlignmentOptions.Center, "Inter-SemiBold");
        Button button = root.gameObject.AddComponent<Button>(); button.targetGraphic = root.GetComponent<Image>();
        button.onClick.AddListener(action);
    }

    private TextMeshProUGUI Text(string name, Transform parent, string value, float size, Color color,
        float x, float y, float width, float height, TextAlignmentOptions alignment, string font)
    {
        RectTransform root = R(name, parent); P(root, x, y, width, height);
        TextMeshProUGUI label = root.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = value; label.fontSize = size; label.color = color; label.alignment = alignment;
        label.font = WDHomeTypography.Load(font); label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    private static RectTransform Box(string name, Transform parent, float x, float y, float width,
        float height, Color color, int radius, Color border)
    {
        RectTransform root = R(name, parent); P(root, x, y, width, height);
        Image image = root.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = true;
        if (radius > 0) WDHomeUIFactory.ApplyRounded(image, radius);
        if (border.a > 0f) WDHomeUIFactory.AddThinRoundedBorder(root, border, Mathf.Max(4, radius));
        return root;
    }

    private static Image Surface(string name, Transform parent, float x, float y, float width,
        float height, Color color, int radius = 0)
    {
        RectTransform root = R(name, parent); P(root, x, y, width, height);
        Image image = root.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
        if (radius > 0) WDHomeUIFactory.ApplyRounded(image, radius);
        return image;
    }

    private static Image Sprite(string name, Transform parent, string path)
    {
        RectTransform root = R(name, parent);
        Image image = root.gameObject.AddComponent<Image>(); image.sprite = Resources.Load<Sprite>(path);
        image.preserveAspect = true; image.raycastTarget = false;
        return image;
    }

    private static Sprite runtimeCircleSprite;
    private static Sprite RuntimeWhiteSprite()
    {
        if (runtimeCircleSprite != null) return runtimeCircleSprite;
        const int size = 128;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            name = "WD_RuntimeCircle",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) * .5f;
        float radius = center - 1f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
            byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(radius + 1f - distance) * 255f);
            pixels[y * size + x] = new Color32(255, 255, 255, alpha);
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        runtimeCircleSprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f);
        runtimeCircleSprite.name = "WD_RuntimeCircleSprite";
        return runtimeCircleSprite;
    }

    private static Image Circle(string name, Transform parent, Color color)
    {
        RectTransform root = R(name, parent);
        Image image = root.gameObject.AddComponent<Image>();
        image.sprite = RuntimeWhiteSprite();
        image.color = color;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static string PresenceLabel(WDSocialPresence presence) => presence switch
    { WDSocialPresence.Online => "ONLINE", WDSocialPresence.InMatch => "EM PARTIDA", _ => "OFFLINE" };
    private static Color PresenceColor(WDSocialPresence presence) => presence switch
    { WDSocialPresence.Online => Green, WDSocialPresence.InMatch => Cyan, _ => Red };
    private static string ActivityLabel(WDSocialPresence presence, int index) => presence switch
    { WDSocialPresence.Online => index % 2 == 0 ? "No Menu Principal" : "No Arsenal",
      WDSocialPresence.InMatch => index % 2 == 0 ? "Batalha 3v3" : "Batalha 1v1",
      _ => $"Visto há {Mathf.Max(1, index)} horas" };
    private static string EligibilityLabel(WDAddFriendEligibility value) => value switch
    { WDAddFriendEligibility.Eligible => "ADICIONAR", WDAddFriendEligibility.AlreadyFriend => "JÁ É AMIGO",
      WDAddFriendEligibility.RequestSent => "SOLICITADO", WDAddFriendEligibility.Blocked => "BLOQUEADO", _ => "VOCÊ" };

    private static RectTransform R(string name, Transform parent)
    {
        GameObject go = new(name, typeof(RectTransform)); RectTransform result = (RectTransform)go.transform;
        result.SetParent(parent, false); return result;
    }

    private static void P(RectTransform root, float x, float y, float width, float height)
    {
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0, 1);
        root.anchoredPosition = new Vector2(x, -y); root.sizeDelta = new Vector2(width, height);
    }

    private static void Stretch(RectTransform root, float left = 0, float top = 0, float right = 0, float bottom = 0)
    {
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.pivot = new Vector2(.5f, .5f);
        root.offsetMin = new Vector2(left, bottom); root.offsetMax = new Vector2(-right, -top);
    }

    private static void StretchHorizontal(RectTransform root, float left, float right)
    {
        float top = -root.anchoredPosition.y;
        float height = root.sizeDelta.y;
        root.anchorMin = new Vector2(0, 1); root.anchorMax = Vector2.one; root.pivot = new Vector2(.5f, 1);
        root.offsetMin = new Vector2(left, -top - height); root.offsetMax = new Vector2(-right, -top);
    }

    private static void StretchBetween(RectTransform root, float left, float right, float top, float bottom)
    {
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.pivot = new Vector2(.5f, .5f);
        root.offsetMin = new Vector2(left, bottom); root.offsetMax = new Vector2(-right, -top);
    }

    private static void LeftStretchVertical(RectTransform root, float left, float top, float bottom, float width)
    {
        root.anchorMin = new Vector2(0, 0); root.anchorMax = new Vector2(0, 1); root.pivot = new Vector2(0, .5f);
        root.offsetMin = new Vector2(left, bottom); root.offsetMax = new Vector2(left + width, -top);
    }

    private static void RightStretchVertical(RectTransform root, float right, float top, float bottom, float width)
    {
        root.anchorMin = new Vector2(1, 0); root.anchorMax = Vector2.one; root.pivot = new Vector2(1, .5f);
        root.offsetMin = new Vector2(-right - width, bottom); root.offsetMax = new Vector2(-right, -top);
    }

    private static void CenterTop(RectTransform root, float x, float y, float width, float height)
    {
        root.anchorMin = root.anchorMax = new Vector2(.5f, 1); root.pivot = new Vector2(.5f, 1);
        root.anchoredPosition = new Vector2(x, -y); root.sizeDelta = new Vector2(width, height);
    }

    private static void Center(RectTransform root, float width, float height)
    {
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f);
        root.anchoredPosition = Vector2.zero; root.sizeDelta = new Vector2(width, height);
    }

    private static void RightTop(RectTransform root, float right, float top, float width, float height)
    {
        if (root == null) return;
        root.anchorMin = root.anchorMax = new Vector2(1, 1); root.pivot = new Vector2(1, 1);
        root.anchoredPosition = new Vector2(-right, -top); root.sizeDelta = new Vector2(width, height);
    }

    private static void Clear(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--) DestroyNow(root.GetChild(i).gameObject);
    }

    private static void DestroyNow(UnityEngine.Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
    }

    private static Color Hex(string value)
    {
        ColorUtility.TryParseHtmlString("#" + value, out Color color); return color;
    }
}
