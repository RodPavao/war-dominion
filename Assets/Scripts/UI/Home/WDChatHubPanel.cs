using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WDChatHubPanel : MonoBehaviour
{
    // ============================================================
    // 01. APRESENTAÇÃO SOCIAL; REPOSITÓRIOS CONTINUAM SENDO A AUTORIDADE
    // ============================================================
    public static readonly Vector2 CanonicalSize = new(1920, 1080);
    // 01.1. Calibração local de leitura; sprites Noto seguem fontSize automaticamente.
    public const float MessageFontSize = WDChatVisualStyle.Message, PreviewFontSize = WDChatVisualStyle.Preview, NameFontSize = WDChatVisualStyle.Name;
    public const float HeadingFontSize = WDChatVisualStyle.Heading, StatusFontSize = WDChatVisualStyle.Status, TimestampFontSize = WDChatVisualStyle.Timestamp;
    private const float LeftWidth = 365f, CenterWidth = 1003f, RightWidth = 456f;
    private const float ColumnsHeight = 944f, HistoryHeight = 760f;
    private const float ColumnGap = 16f;
    private static readonly Color Cyan = new Color32(32, 199, 232, 255);
    private static readonly Color Gold = new Color32(197, 155, 99, 255);
    private static readonly Color Muted = new Color32(170, 185, 194, 255);
    private static readonly Color Green = new Color32(57, 217, 138, 255);
    private static readonly Color Border = new Color32(49, 81, 93, 255);
    private static readonly Color Side = new(14f / 255, 23f / 255, 29f / 255, .75f);
    private static readonly Color ActiveConversationSurface =
        new(17f / 255f, 24f / 255f, 32f / 255f, .82f);
    private const float ReadableSurfaceAlpha = WDChatVisualStyle.ReadableAlpha;
    private readonly Dictionary<string, string> drafts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Sprite> sprites = new();
    private readonly List<SocialRow> recentRows = new(), friendRows = new();
    private readonly List<MessageRow> messageRows = new();
    private readonly List<float> messageTops = new(), messageHeights = new();
    private WDChatRepository chat;
    private WDSocialRepository social;
    private WDProfileRepository profiles;
    private WDNavigationStack navigation;
    private Action<string> openProfile;
    private Action openFriends, closeLocal;
    private Action openNotifications;
    private Func<WDArmoryProfile> walletProvider;
    private WarDominionUITheme theme;
    private Action<string, string, Action> confirm;
    // ============================================================
    // 01. REFERÊNCIAS SERIALIZADAS DA VIEW V2
    // ============================================================
    [SerializeField] private RectTransform content, optionsLayer, optionsPopup, searchPanel;
    private WDHomeSecondaryScreenFrame responsiveFrame;
    [SerializeField] private RawImage backgroundArtwork;
    [SerializeField] private ScrollRect recentScroll, friendsScroll, messagesScroll;
    [SerializeField] private Button backButton, notificationButton, newConversationButton, viewAllFriendsButton;
    [SerializeField] private TextMeshProUGUI headerCoins, headerGems;
    [SerializeField] private Button optionsDismissButton, muteButton, findButton, cancelSearchButton;
    [SerializeField] private RectTransform friendsDiscord;
    [SerializeField] private TMP_InputField messageInput, searchInput;
    [SerializeField] private WDChatEmojiPicker emojiPicker;
    [SerializeField] private TextMeshProUGUI heading, status, friendsCount, empty, emptyRecents, emptyFriends, feedback, muteLabel, measure;
    [SerializeField] private Button clanButton, profileButton, optionsButton, sendButton, blockButton, emojiButton;
    [SerializeField] private GameObject onlineBadge, headerPresence, clanUnread;
    [SerializeField] private Image clanSelectionBorder;
    [SerializeField] private GameObject clanSelectionIndicator;
    [SerializeField] private RawImage headerAvatarImage;
    [SerializeField] private TextMeshProUGUI headerAvatarInitials;
    private WDChatChannel[] recentChannels = Array.Empty<WDChatChannel>();
    private WDProfileData[] onlineFriends = Array.Empty<WDProfileData>();
    private string selectedChannelId, renderedChannel;
    private int renderedCount = -1;
    private bool subscribed, refreshing, sending;
    private float lastResponsiveCenterWidth = -1f;
    private float lastResponsiveColumnHeight = -1f;

    public bool IsOpen => isActiveAndEnabled;
    public string SelectedChannelId => selectedChannelId;
    public WDChatChannel SelectedChannel => chat?.Channels.FirstOrDefault(c => c.Id == selectedChannelId && c.Type != WDChatChannelType.MatchTeam);
    public RectTransform Content => content;
    private bool IsVisible => IsOpen && (closeLocal != null || navigation == null || navigation.CurrentId == "chat");

    public void Build(WarDominionUITheme newTheme, WDChatRepository repository,
        WDSocialRepository socialRepository, WDProfileRepository profileRepository,
        WDNavigationStack navigationStack = null, Action<string> profileAction = null,
        Action<string, string, Action> confirmAction = null, Action friendsAction = null,
        Texture2D officialBackground = null, Func<WDArmoryProfile> economyProvider = null,
        Action notificationAction = null)
    {
        theme = newTheme;
        chat = repository ?? throw new ArgumentNullException(nameof(repository));
        social = socialRepository ?? throw new ArgumentNullException(nameof(socialRepository));
        profiles = profileRepository ?? throw new ArgumentNullException(nameof(profileRepository));
        navigation = navigationStack;
        openProfile = profileAction;
        confirm = confirmAction;
        openFriends = friendsAction;
        walletProvider = economyProvider;
        openNotifications = notificationAction;
        if (content != null)
        {
            // 01.1. Prefab V2: somente liga comportamento e dados à hierarquia serializada.
            responsiveFrame = WDHomeSecondaryScreenFrame.Configure(content);
            backgroundArtwork.texture = officialBackground;
            backgroundArtwork.color = officialBackground != null ? Color.white : Color.clear;
            RefreshHeaderEconomy();
            WireSerializedInteractions();
            FitSafeArea();
            gameObject.SetActive(false);
            return;
        }
        // 01.2. Arte oficial fornecida pela cena; fora do conteúdo/Safe Area.
        Image background = gameObject.AddComponent<Image>();
        background.color = new Color32(8, 13, 23, 255);
        background.raycastTarget = true;
        backgroundArtwork = Rect("BackgroundArtwork", transform, 0, 0, 0, 0).gameObject.AddComponent<RawImage>();
        WDHomeUIFactory.Stretch(backgroundArtwork.rectTransform);
        backgroundArtwork.texture = officialBackground;
        backgroundArtwork.color = officialBackground != null ? Color.white : Color.clear;
        backgroundArtwork.raycastTarget = false;
        content = Rect("ChatResponsiveShell", transform, 0, 0, 0, 0);
        responsiveFrame = WDHomeSecondaryScreenFrame.Configure(content);
        BuildHeader();
        // 01.3. Três superfícies independentes usam exatamente fill, outline e
        // opacidade do Arsenal V2; os gaps substituem as divisórias externas.
        RectTransform inner = Rect("ColumnsSurface", content, 32, 112, 1856, 944);
        // 01.4. Sem o veil ambiental do Arsenal, 97,3% produz a mesma
        // opacidade composta percebida, aplicada individualmente por painel.
        const float chatPanelAlpha = .973f;
        RectTransform left = WDUniversalPanelFactory.Build(inner, theme,
            "Channels", 0f, 0f, LeftWidth, ColumnsHeight, chatPanelAlpha);
        RectTransform center = WDUniversalPanelFactory.Build(inner, theme,
            "Conversation", LeftWidth + ColumnGap, 0f, CenterWidth, ColumnsHeight, chatPanelAlpha);
        RectTransform right = WDUniversalPanelFactory.Build(inner, theme,
            "OnlineFriends", LeftWidth + CenterWidth + ColumnGap * 2f, 0f,
            RightWidth, ColumnsHeight, chatPanelAlpha);
        BuildChannels(left);
        BuildConversation(center);
        BuildFriends(right);
        ConfigureResponsiveColumns(inner, left, center, right);
        WDUniversalPanelFactory.BringOutlineToFront(left);
        WDUniversalPanelFactory.BringOutlineToFront(center);
        WDUniversalPanelFactory.BringOutlineToFront(right);
        BuildLocalPopups();
        // O mesmo picker, catálogo, atlas e Recentes de sessão da partida.
        emojiPicker = emojiButton.gameObject.AddComponent<WDChatEmojiPicker>();
        emojiPicker.Build(messageInput, emojiButton, content);
        FitSafeArea();
        gameObject.SetActive(false);
    }

    // ============================================================
    // 02. MASTER 3072:117; HEADER INSET COMPARTILHADO E SEM BARRAS DECORATIVAS
    // ============================================================
    private void BuildHeader()
    {
        WDUniversalHeaderContent header = WDUniversalHeaderFactory.BuildInset(content,
            theme, "CHAT", Back, OpenNotifications);
        backButton = header.BackButton;
        notificationButton = header.NotificationButton;
        headerCoins = header.CoinValue;
        headerGems = header.GemValue;
    }

    private void BuildChannels(RectTransform parent)
    {
        clanButton = Button("Clan", parent, 16, 16, LeftWidth - 33, 44, () => Select(chat.ClanChannel));
        ChatIcon(clanButton.transform, "Clan", 12, 13, 18, 18);
        Label(clanButton.transform, "Label", "CLÃ", 42, 0, 240, 44, 14, Muted, true);
        clanUnread = Dot(clanButton.transform, "Unread", LeftWidth - 60, 18, 8, Cyan).gameObject;
        BuildConversationSelectionVisual(clanButton.transform, 44f,
            out clanSelectionBorder, out clanSelectionIndicator);
        clanSelectionBorder.gameObject.SetActive(false);
        clanSelectionIndicator.SetActive(false);
        Label(parent, "RecentTitle", "RECENTES", 16, 84, 230, 24, 12, Muted, true).characterSpacing = 1.8f;
        newConversationButton = Button("NewConversation", parent, LeftWidth - 60, 74, 44, 44, ToggleSearch);
        ChatIcon(newConversationButton.transform, "Plus", 14, 14, 16, 16);
        recentScroll = Scroll("Recents", parent, 16, 120, LeftWidth - 33, 530);
        recentScroll.onValueChanged.AddListener(_ => RenderRecentRows());
        emptyRecents = Label(recentScroll.viewport, "Empty", "Nenhuma conversa recente", 8, 8, LeftWidth - 49, 40, 12, Muted);
        Button support = Button("Support", parent, 16, ColumnsHeight - 60, 242, 44, null, Alpha(Side, ReadableSurfaceAlpha), Border);
        ChatIcon(support.transform, "Support", 13, 13, 18, 18);
        Label(support.transform, "Label", "SUPORTE", 43, 0, 180, 44, 14, Color.white, true);
    }

    private void BuildConversation(RectTransform parent)
    {
        RectTransform header = Surface("Header", parent, 0, 0, CenterWidth, 96, Alpha(Side, ReadableSurfaceAlpha));
        BuildAvatar(header, "Avatar", 20, 16, 48, out headerAvatarImage, out headerAvatarInitials);
        headerPresence = Dot(header, "Presence", 58, 54, 10, Green).gameObject;
        heading = Label(header, "Name", string.Empty, 80, 10, 610, 28, HeadingFontSize, Color.white, true);
        status = Label(header, "Status", string.Empty, 80, 40, 660, 22, StatusFontSize, Muted);
        onlineBadge = Surface("OnlineBadge", header, 220, 15, 64, 18, Alpha(Green, .2f), 9).gameObject;
        Label(onlineBadge.transform, "Label", "ONLINE", 4, 0, 56, 18, 12, Green, true, TextAlignmentOptions.Center);
        profileButton = Button("ViewProfile", header, CenterWidth - 202, 27, 132, 42, ViewProfile,
            new Color(17f / 255f, 24f / 255f, 32f / 255f, .96f), Alpha(Gold, .32f), 8);
        Label(profileButton.transform, "Label", "VER PERFIL", 0, 0, 132, 42, 14, Gold, true, TextAlignmentOptions.Center);
        optionsButton = Button("Options", header, CenterWidth - 62, 27, 42, 42, ToggleOptions,
            new Color(17f / 255f, 24f / 255f, 32f / 255f, .96f), Alpha(Gold, .32f), 8);
        ChatIcon(optionsButton.transform, "Options", 14, 19.5f, 13.5f, 3);
        Surface("Divider", parent, 0, 95, CenterWidth, 1, Alpha(Border, .8f));
        messagesScroll = Scroll("Messages", parent, 0, 96, CenterWidth, HistoryHeight);
        messagesScroll.onValueChanged.AddListener(_ => RenderMessageRows());
        empty = Label(messagesScroll.viewport, "Empty", string.Empty, 24, 220, CenterWidth - 48, 60, 14, Muted, false, TextAlignmentOptions.Center);
        empty.textWrappingMode = TextWrappingModes.Normal;
        RectTransform form = Surface("Composer", parent, 0, ColumnsHeight - 88, CenterWidth, 88, Alpha(Side, ReadableSurfaceAlpha));
        Surface("Divider", form, 0, 0, CenterWidth, 1, Alpha(Border, .8f));
        RectTransform field = Surface("Field", form, 16, 16, CenterWidth - 32, 56,
            new Color(10f / 255, 15f / 255, 19f / 255, ReadableSurfaceAlpha), 12, Border);
        Button attachment = Button("Attachment", field, 11, 6, 44, 44, null);
        ChatIcon(attachment.transform, "Plus", 12, 12, 20, 20);
        emojiButton = Button("Emoji", field, 60, 6, 44, 44, () => { });
        messageInput = Input("MessageInput", field, 112, 6, CenterWidth - 217, 44, "Digite uma mensagem...");
        messageInput.onSubmit.AddListener(_ => SendCurrent());
        messageInput.onValueChanged.AddListener(_ => { SaveDraft(); RefreshSendState(); });
        sendButton = Button("Send", field, CenterWidth - 94, 6, 44, 44, SendCurrent, Cyan);
        ChatIcon(sendButton.transform, "Send", 12, 12, 20, 20);
        // Medição usa exatamente a fonte/tamanho dos corpos, inclusive ascendente dos sprites.
        measure = Label(content, "MessageMeasure", string.Empty, 0, 0, 446, 24, MessageFontSize, Color.white);
        WDChatEmojiText.Apply(measure);
        measure.textWrappingMode = TextWrappingModes.Normal;
        measure.overflowMode = TextOverflowModes.Overflow;
        measure.gameObject.SetActive(false);
    }

    private void BuildFriends(RectTransform parent)
    {
        Label(parent, "Title", "AMIGOS ONLINE", 16, 16, 300, 18, 12, Muted, true).characterSpacing = 1.8f;
        friendsCount = Label(parent, "Count", string.Empty, 16, 35, 350, 20, StatusFontSize, Color.white);
        RectTransform badge = Surface("FriendsBadge", parent, RightWidth - 44, 19, 28, 28, Alpha(Green, .15f), 14);
        ChatIcon(badge, "Friends", 7, 7, 14, 14);
        Surface("Divider", parent, 16, 69, RightWidth - 32, 1, Alpha(Border, .7f));
        friendsScroll = Scroll("FriendsList", parent, 16, 86, RightWidth - 32, 158);
        friendsScroll.onValueChanged.AddListener(_ => RenderFriendRows());
        emptyFriends = Label(friendsScroll.viewport, "Empty", "Nenhum amigo online", 8, 8, RightWidth - 48, 40, 12, Muted);
        viewAllFriendsButton = Button("ViewAll", parent, 16, 260, RightWidth - 32, 40, ViewAllFriends, Alpha(Cyan, .1f), Alpha(Cyan, .4f));
        viewAllFriendsButton.interactable = openFriends != null;
        Label(viewAllFriendsButton.transform, "Label", "VER TODOS", 90, 0, 180, 40, 14, Cyan, true, TextAlignmentOptions.Center).characterSpacing = 1;
        ChatIcon(viewAllFriendsButton.transform, "ArrowRight", 270, 13, 14, 14);
        RectTransform discord = friendsDiscord = Surface("Discord", parent, 16, ColumnsHeight - 142, RightWidth - 32, 126, Color.white, 12);
        discord.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        RawImage artwork = Rect("Artwork", discord, 0, 0, RightWidth - 32, 126).gameObject.AddComponent<RawImage>();
        artwork.texture = Resources.Load<Texture2D>("UI/Match/Chat/Discord");
        artwork.raycastTarget = false;
        artwork.color = Alpha(Color.white, .9f);
        ApplyDiscordFraming(artwork, RightWidth - 32, 126);
        Button community = Button("DiscordCommunity", discord, 151, 76, RightWidth - 198, 40, null, Alpha(Cyan, .21f));
        Label(community.transform, "Label", "ENTRAR NA COMUNIDADE", 4, 0, RightWidth - 228, 40, 7,
            new Color32(7, 21, 27, 255), true, TextAlignmentOptions.Center);
        ChatIcon(community.transform, "External", RightWidth - 225, 12, 16, 16);
    }
    // ============================================================
    // 03. CICLO DE VIDA E PILHA EXISTENTE; DRAFT É ESTADO DA VIEW
    // ============================================================
    private void OnEnable() { Subscribe(); if (content != null && chat != null) Refresh(); }
    private void OnDisable() { SaveDraft(); CloseLocalPopups(); Unsubscribe(); }
    private void OnDestroy()
    {
        Unsubscribe();
        foreach (Sprite sprite in sprites.Values) DestroyOwned(sprite);
    }
    private void Subscribe()
    {
        if (subscribed || chat == null) return;
        chat.Changed += Refresh;
        social.Changed += Refresh;
        subscribed = true;
    }
    private void Unsubscribe()
    {
        if (!subscribed) return;
        chat.Changed -= Refresh;
        social.Changed -= Refresh;
        subscribed = false;
    }
    public void OpenFromHome(string privatePlayerId = null)
    {
        closeLocal = null;
        string preferred = string.IsNullOrEmpty(privatePlayerId) ? selectedChannelId : chat.OpenPrivate(privatePlayerId)?.Id;
        if (navigation == null) { Restore(preferred); return; }
        navigation.Push(new WDNavigationContext("chat", saved => Restore(saved as string ?? preferred),
            CaptureNavigationState, HideFromNavigation));
    }
    private object CaptureNavigationState()
    {
        // Push não chama Exit no contexto anterior. Ocultar aqui impede consumo
        // de unread enquanto Perfil/Amigos estão acima, preservando o estado local.
        SaveDraft();
        HideFromNavigation();
        return selectedChannelId;
    }
    public void OpenLocal(Action closeAction, WDChatChannel preferred = null)
    {
        closeLocal = closeAction;
        Restore(preferred?.Type == WDChatChannelType.MatchTeam ? null : preferred?.Id ?? selectedChannelId);
    }
    private void Restore(string id)
    {
        SaveDraft();
        selectedChannelId = chat.Channels.Any(c => c.Id == id && c.Type != WDChatChannelType.MatchTeam) ? id : null;
        messageInput.SetTextWithoutNotify(selectedChannelId != null && drafts.TryGetValue(selectedChannelId, out string draft) ? draft : string.Empty);
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        Subscribe();
        FitSafeArea();
        Refresh();
    }
    public void HideFromNavigation()
    {
        SaveDraft();
        CloseLocalPopups();
        Unsubscribe();
        gameObject.SetActive(false);
    }
    public bool TryHandleLocalBack()
    {
        if (emojiPicker?.IsOpen == true) { emojiPicker.Close(); return true; }
        if (searchPanel != null && searchPanel.gameObject.activeSelf) { searchPanel.gameObject.SetActive(false); return true; }
        if (optionsPopup != null && optionsPopup.gameObject.activeSelf) { CloseOptions(); return true; }
        if (closeLocal != null) { closeLocal(); return true; }
        return false;
    }
    private void Back() { if (!TryHandleLocalBack()) navigation?.TryBack(); }
    private void CloseLocalPopups()
    {
        emojiPicker?.Close();
        if (searchPanel != null) searchPanel.gameObject.SetActive(false);
        CloseOptions();
    }
    private void SaveDraft()
    {
        if (messageInput != null && selectedChannelId != null) drafts[selectedChannelId] = messageInput.text;
    }
    public void Select(WDChatChannel channel)
    {
        if (channel?.Type == WDChatChannelType.MatchTeam) return;
        SaveDraft();
        CloseLocalPopups();
        bool changed = selectedChannelId != channel?.Id;
        selectedChannelId = channel?.Id;
        messageInput.SetTextWithoutNotify(selectedChannelId != null && drafts.TryGetValue(selectedChannelId, out string draft) ? draft : string.Empty);
        if (changed) renderedCount = -1;
        Refresh();
    }

    // ============================================================
    // 04. REFRESH SEM RECRIAR COMPOSER; LEITURA APENAS DO CANAL VISÍVEL
    // ============================================================
    private void Refresh()
    {
        if (refreshing || content == null || messageInput == null || !IsVisible) return;
        refreshing = true;
        try
        {
            RefreshHeaderEconomy();
            WDChatChannel channel = SelectedChannel;
            bool accessible = channel?.CanAccess(chat.LocalPlayerId) == true;
            if (accessible && channel.IsUnread) chat.OpenContent(channel.Id);
            heading.text = channel?.DisplayName ?? "CHAT";
            string other = OtherParticipant(channel);
            ApplyAvatar(headerAvatarImage, headerAvatarInitials,
                profiles.Find(other) ?? profiles.LocalProfile, heading.text);
            bool online = other != null && social.PresenceOf(other) != WDSocialPresence.Offline;
            headerPresence.SetActive(online);
            onlineBadge.SetActive(online);
            ((RectTransform)onlineBadge.transform).anchoredPosition = new Vector2(80 + Mathf.Min(heading.preferredWidth, 610) + 8, -15);
            status.text = !accessible ? "Selecione uma conversa ou o seu clã." : other != null ? PresenceText(other) : "CLÃ";
            profileButton.interactable = accessible && other != null && openProfile != null;
            optionsButton.interactable = accessible;
            clanButton.interactable = chat.ClanChannel?.CanAccess(chat.LocalPlayerId) == true;
            bool clanSelected = channel?.Type == WDChatChannelType.Clan;
            clanButton.GetComponent<Image>().color = clanSelected
                ? ActiveConversationSurface : Color.clear;
            clanSelectionBorder.gameObject.SetActive(clanSelected);
            clanSelectionIndicator.SetActive(clanSelected);
            clanUnread.SetActive(chat.ClanChannel?.IsUnread == true);
            muteLabel.text = channel?.IsMuted == true ? "ATIVAR NOTIFICAÇÕES" : "SILENCIAR";
            blockButton.interactable = accessible && other != null && confirm != null;
            messageInput.interactable = accessible;
            emojiButton.interactable = accessible;
            if (!accessible) CloseLocalPopups();
            RefreshSendState();
            recentChannels = chat.RecentPrivate.Where(c => c.CanAccess(chat.LocalPlayerId)).ToArray();
            onlineFriends = social.Friends().Where(p => social.PresenceOf(p.PlayerId) != WDSocialPresence.Offline).ToArray();
            emptyRecents.gameObject.SetActive(recentChannels.Length == 0);
            emptyFriends.gameObject.SetActive(onlineFriends.Length == 0);
            friendsCount.text = onlineFriends.Length + (onlineFriends.Length == 1 ? " companheiro ativo" : " companheiros ativos");
            ResizeList(recentScroll, recentChannels.Length * 60);
            LayoutOnlineFriends();
            ResizeList(friendsScroll, onlineFriends.Length * 48);
            RenderRecentRows();
            RenderFriendRows();
            UpdateHistory(channel, accessible);
        }
        finally { refreshing = false; }
    }
    private void RefreshSendState()
    {
        if (sendButton != null) sendButton.interactable = IsVisible && SelectedChannel?.CanAccess(chat.LocalPlayerId) == true && !string.IsNullOrWhiteSpace(messageInput.text);
    }
    public void SendCurrent()
    {
        WDChatChannel channel = SelectedChannel;
        if (sending || !IsVisible || !messageInput.interactable || channel?.CanAccess(chat.LocalPlayerId) != true || string.IsNullOrWhiteSpace(messageInput.text)) return;
        sending = true;
        try
        {
            if (!chat.Send(channel.Id, chat.LocalPlayerId, messageInput.text)) return;
            drafts.Remove(channel.Id);
            messageInput.SetTextWithoutNotify(string.Empty);
            messagesScroll.StopMovement();
            messagesScroll.verticalNormalizedPosition = 0;
            RefreshSendState();
            RenderMessageRows();
        }
        finally { sending = false; }
    }

    // ============================================================
    // 05. LISTAS VIRTUALIZADAS: OBJETOS SÓ PARA AS LINHAS VISÍVEIS
    // ============================================================
    private sealed class SocialRow
    {
        public Button Button;
        public RawImage Avatar;
        public TextMeshProUGUI Initials;
        public TextMeshProUGUI Name, Preview;
        public GameObject Presence, Unread;
        public Image SelectionBorder;
        public GameObject SelectionIndicator;
    }
    private SocialRow GetSocialRow(List<SocialRow> pool, ScrollRect scroll, int index, bool friend)
    {
        if (index < pool.Count) return pool[index];
        float width = scroll.viewport.rect.width;
        Button row = Button(friend ? "Friend" : "Recent", scroll.content, 0, 0, width, friend ? 44 : 52, () => { });
        var item = new SocialRow { Button = row };
        BuildAvatar(row.transform, "Avatar", friend ? 0 : 8, friend ? 4 : 8, 36,
            out item.Avatar, out item.Initials);
        item.Presence = Dot(row.transform, "Presence", friend ? 28 : 36, friend ? 32 : 34, 8, Green).gameObject;
        float x = friend ? 44 : 52;
        item.Name = Label(row.transform, "Name", string.Empty, x, 5, width - x - 24, 20, NameFontSize, Color.white, true);
        item.Name.font = WDHomeTypography.Load("Inter-SemiBold");
        item.Preview = Label(row.transform, "Preview", string.Empty, x, 25, width - x - 24, friend ? 19 : 27, PreviewFontSize, Muted);
        WDChatEmojiText.Apply(item.Preview);
        item.Unread = Dot(row.transform, "Unread", width - 18, 20, 10, Cyan).gameObject;
        BuildConversationSelectionVisual(row.transform, friend ? 44f : 52f,
            out item.SelectionBorder, out item.SelectionIndicator);
        item.SelectionBorder.gameObject.SetActive(false);
        item.SelectionIndicator.SetActive(false);
        pool.Add(item);
        return item;
    }
    private void RenderRecentRows()
    {
        if (recentScroll == null) return;
        WDChatChannel activeChannel = SelectedChannel;
        int first = Mathf.Max(0, Mathf.FloorToInt(recentScroll.content.anchoredPosition.y / 60));
        int count = Mathf.Min(Mathf.CeilToInt(recentScroll.viewport.rect.height / 60) + 1, recentChannels.Length - first);
        for (int i = 0; i < count; i++)
        {
            WDChatChannel channel = recentChannels[first + i];
            SocialRow row = GetSocialRow(recentRows, recentScroll, i, false);
            row.Button.gameObject.SetActive(true);
            ((RectTransform)row.Button.transform).anchoredPosition = new Vector2(0, -(first + i) * 60);
            row.Button.onClick.RemoveAllListeners();
            row.Button.onClick.AddListener(() => Select(channel));
            // 05.1. O objeto resolvido pelo repositório é a autoridade. O fallback
            // ordinal sem distinção de caixa cobre IDs restaurados/normalizados.
            bool selected = ReferenceEquals(channel, activeChannel) ||
                string.Equals(channel.Id, activeChannel?.Id,
                    StringComparison.OrdinalIgnoreCase);
            row.Button.GetComponent<Image>().color = selected
                ? ActiveConversationSurface : Color.clear;
            row.SelectionBorder.gameObject.SetActive(selected);
            row.SelectionIndicator.SetActive(selected);
            if (selected)
            {
                // Linhas virtualizadas reutilizam a mesma árvore. Reafirmar a ordem
                // impede avatar, presença ou unread de cobrirem a marcação ativa.
                row.SelectionBorder.transform.SetAsLastSibling();
                row.SelectionIndicator.transform.SetAsLastSibling();
            }
            row.Name.text = channel.DisplayName;
            row.Preview.text = channel.Messages.LastOrDefault()?.Text ?? string.Empty;
            row.Preview.color = Muted;
            string other = OtherParticipant(channel);
            row.Presence.SetActive(other != null && social.PresenceOf(other) != WDSocialPresence.Offline);
            row.Unread.SetActive(channel.IsUnread);
            ApplyAvatar(row.Avatar, row.Initials, profiles.Find(other), channel.DisplayName);
        }
        HideUnused(recentRows, Mathf.Max(0, count));
    }
    private void RenderFriendRows()
    {
        if (friendsScroll == null) return;
        string selectedPlayerId = OtherParticipant(SelectedChannel);
        int first = Mathf.Max(0, Mathf.FloorToInt(friendsScroll.content.anchoredPosition.y / 48));
        int count = Mathf.Min(Mathf.CeilToInt(friendsScroll.viewport.rect.height / 48) + 1, onlineFriends.Length - first);
        for (int i = 0; i < count; i++)
        {
            WDProfileData profile = onlineFriends[first + i];
            SocialRow row = GetSocialRow(friendRows, friendsScroll, i, true);
            row.Button.gameObject.SetActive(true);
            ((RectTransform)row.Button.transform).anchoredPosition = new Vector2(0, -(first + i) * 48);
            row.Button.onClick.RemoveAllListeners();
            row.Button.onClick.AddListener(() => OpenPrivate(profile.PlayerId));
            // 05.2. A linha representa PlayerId; o canal selecionado representa
            // ChannelId. OtherParticipant faz a ligação sem criar estado paralelo.
            bool selected = string.Equals(profile.PlayerId, selectedPlayerId,
                StringComparison.OrdinalIgnoreCase);
            row.Button.GetComponent<Image>().color = selected
                ? ActiveConversationSurface : Color.clear;
            row.SelectionBorder.gameObject.SetActive(selected);
            row.SelectionIndicator.SetActive(selected);
            if (selected)
            {
                row.SelectionBorder.transform.SetAsLastSibling();
                row.SelectionIndicator.transform.SetAsLastSibling();
            }
            row.Name.text = profile.Nickname;
            row.Preview.text = PresenceText(profile.PlayerId);
            row.Preview.color = Green;
            row.Presence.SetActive(true);
            row.Unread.SetActive(false);
            ApplyAvatar(row.Avatar, row.Initials, profile, profile.Nickname);
        }
        HideUnused(friendRows, Mathf.Max(0, count));
    }
    private void LayoutOnlineFriends()
    {
        if (friendsScroll == null || viewAllFriendsButton == null || friendsDiscord == null) return;
        const float rowStep = 48f;
        const float gap = 16f;
        RectTransform list = friendsScroll.transform as RectTransform;
        RectTransform viewAll = viewAllFriendsButton.transform as RectTransform;
        float listTop = -list.anchoredPosition.y;
        float discordTop = -friendsDiscord.anchoredPosition.y;
        float maximumHeight = Mathf.Max(rowStep,
            discordTop - listTop - viewAll.rect.height - gap * 2f);
        float desiredHeight = Mathf.Max(rowStep, onlineFriends.Length * rowStep);
        float visibleHeight = Mathf.Min(desiredHeight, maximumHeight);
        ResizeTop(list, list.anchoredPosition.x, listTop, list.rect.width, visibleHeight);
        ResizeTop(viewAll, viewAll.anchoredPosition.x,
            listTop + visibleHeight + gap, viewAll.rect.width, viewAll.rect.height);
    }
    private static void HideUnused(List<SocialRow> pool, int used)
    { for (int i = used; i < pool.Count; i++) pool[i].Button.gameObject.SetActive(false); }
    private static void ResizeList(ScrollRect scroll, float height)
    {
        scroll.content.sizeDelta = new Vector2(0, Mathf.Max(scroll.viewport.rect.height, height));
        scroll.content.anchoredPosition = new Vector2(0, Mathf.Clamp(scroll.content.anchoredPosition.y, 0, scroll.content.rect.height - scroll.viewport.rect.height));
    }

    // ============================================================
    // 06. HISTÓRICO LITERAL COM MÉTRICAS E ATLAS COMPARTILHADOS
    // ============================================================
    private sealed class MessageRow
    {
        public RectTransform Root, Bubble;
        public Image Fill, Outline;
        public RawImage Avatar;
        public TextMeshProUGUI Initials;
        public TextMeshProUGUI Body, Timestamp;
    }
    private void UpdateHistory(WDChatChannel channel, bool accessible)
    {
        int count = accessible ? channel.Messages.Count : 0;
        empty.text = !accessible ? "Selecione uma conversa ou o seu clã." : count == 0 ? "Nenhuma mensagem nesta conversa." : string.Empty;
        empty.gameObject.SetActive(!accessible || count == 0);
        if (renderedChannel == selectedChannelId && renderedCount == count) return;
        bool bottom = renderedChannel != selectedChannelId || renderedCount < 0 || messagesScroll.verticalNormalizedPosition < .03f;
        float oldY = messagesScroll.content.anchoredPosition.y;
        messageTops.Clear();
        messageHeights.Clear();
        float y = 20;
        for (int i = 0; i < count; i++)
        {
            measure.text = channel.Messages[i].Text;
            float height = Mathf.Max(23, measure.GetPreferredValues(446, Mathf.Infinity).y) + 24;
            messageTops.Add(y);
            messageHeights.Add(height);
            y += height + 31;
        }
        float bottomSpace = Mathf.Max(0, HistoryHeight - y - 8);
        for (int i = 0; i < count; i++) messageTops[i] += bottomSpace;
        messagesScroll.content.sizeDelta = new Vector2(0, Mathf.Max(HistoryHeight, y + 8));
        float maximum = messagesScroll.content.rect.height - HistoryHeight;
        messagesScroll.content.anchoredPosition = new Vector2(0, bottom ? maximum : Mathf.Clamp(oldY, 0, maximum));
        messagesScroll.StopMovement();
        renderedChannel = selectedChannelId;
        renderedCount = count;
        RenderMessageRows();
    }
    private MessageRow GetMessageRow(int index)
    {
        if (index < messageRows.Count) return messageRows[index];
        var row = new MessageRow { Root = Rect("Message", messagesScroll.content, 0, 0, CenterWidth, 80) };
        row.Bubble = Surface("Bubble", row.Root, 68, 0, 480, 48, Side, 16);
        row.Fill = row.Bubble.GetComponent<Image>();
        row.Outline = WDHomeUIFactory.AddRoundedBorder(row.Bubble, Border, 16);
        row.Body = Label(row.Bubble, "Body", string.Empty, 17, 12, 446, 24, MessageFontSize, Color.white);
        WDChatEmojiText.Apply(row.Body);
        row.Body.textWrappingMode = TextWrappingModes.Normal;
        row.Body.overflowMode = TextOverflowModes.Overflow;
        row.Body.alignment = TextAlignmentOptions.TopLeft;
        row.Timestamp = Label(row.Root, "Timestamp", string.Empty, 68, 52, 480, 15, TimestampFontSize, Muted);
        BuildAvatar(row.Root, "Avatar", 24, 42, 32, out row.Avatar, out row.Initials);
        messageRows.Add(row);
        return row;
    }
    private void RenderMessageRows()
    {
        if (messagesScroll == null) return;
        WDChatChannel channel = SelectedChannel;
        int used = 0;
        if (channel?.CanAccess(chat.LocalPlayerId) == true && renderedChannel == selectedChannelId)
        {
            float top = messagesScroll.content.anchoredPosition.y;
            for (int i = 0; i < messageTops.Count && i < channel.Messages.Count; i++)
            {
                float y = messageTops[i], height = messageHeights[i];
                if (y + height + 19 < top || y > top + HistoryHeight) continue;
                WDChatMessage message = channel.Messages[i];
                MessageRow row = GetMessageRow(used++);
                row.Root.gameObject.SetActive(true);
                row.Root.anchoredPosition = new Vector2(0, -y);
                row.Root.sizeDelta = new Vector2(CenterWidth, height + 19);
                bool sent = string.Equals(message.SenderId, chat.LocalPlayerId, StringComparison.OrdinalIgnoreCase);
                bool system = message.Kind == WDChatMessageKind.System;
                float x = system ? (CenterWidth - 480) / 2 : sent ? CenterWidth - 504 : 68;
                row.Bubble.anchoredPosition = new Vector2(x, 0);
                row.Bubble.sizeDelta = new Vector2(480, height);
                row.Fill.color = sent
                    ? new Color(11f / 255f, 16f / 255f, 22f / 255f, .94f)
                    : new Color(17f / 255f, 24f / 255f, 32f / 255f, .78f);
                row.Outline.color = sent ? Alpha(Cyan, .36f) : Alpha(Gold, .18f);
                row.Body.text = message.Text;
                row.Body.rectTransform.sizeDelta = new Vector2(446, height - 24);
                row.Timestamp.rectTransform.anchoredPosition = new Vector2(x, -height - 4);
                string stamp = message.Timestamp.ToLocalTime().ToString("HH:mm");
                row.Timestamp.text = sent || system ? stamp : (profiles.Find(message.SenderId)?.Nickname ?? message.SenderId) + " · " + stamp;
                row.Timestamp.alignment = sent ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
                row.Avatar.gameObject.SetActive(!sent && !system);
                row.Avatar.rectTransform.anchoredPosition = new Vector2(24, -height + 6);
                row.Initials.transform.parent.parent.gameObject.SetActive(!sent && !system);
                if (!sent && !system)
                    ApplyAvatar(row.Avatar, row.Initials, profiles.Find(message.SenderId), message.SenderId);
            }
        }
        for (int i = used; i < messageRows.Count; i++) messageRows[i].Root.gameObject.SetActive(false);
    }
    // ============================================================
    // 07. AÇÕES SOCIAIS EXISTENTES E POPUPS LOCAIS
    // ============================================================
    private string OtherParticipant(WDChatChannel channel) => channel?.Type == WDChatChannelType.Private
        ? channel.Participants.FirstOrDefault(id => !id.Equals(chat.LocalPlayerId, StringComparison.OrdinalIgnoreCase)) : null;
    private string PresenceText(string id) => social.PresenceOf(id) switch
    { WDSocialPresence.Online => "ONLINE", WDSocialPresence.InMatch => "EM PARTIDA", _ => "OFFLINE" };
    private void OpenPrivate(string playerId)
    {
        WDChatChannel channel = chat.OpenPrivate(playerId);
        if (channel != null) Select(channel);
        else { searchPanel.gameObject.SetActive(true); feedback.text = "Conversa indisponível pelas permissões atuais."; }
    }
    private void ViewProfile()
    {
        string other = OtherParticipant(SelectedChannel);
        if (other == null || openProfile == null) return;
        CloseLocalPopups();
        SaveDraft();
        openProfile(other);
    }
    private void ViewAllFriends()
    {
        CloseLocalPopups();
        SaveDraft();
        openFriends?.Invoke();
    }
    private void ToggleOptions()
    {
        emojiPicker.Close();
        searchPanel.gameObject.SetActive(false);
        bool open = !optionsPopup.gameObject.activeSelf;
        optionsLayer?.gameObject.SetActive(open);
        optionsPopup.gameObject.SetActive(open);
        if (open)
        {
            PositionOptionsPopup();
            (optionsLayer != null ? optionsLayer : optionsPopup).SetAsLastSibling();
        }
    }
    private void ToggleSearch()
    {
        emojiPicker.Close();
        CloseOptions();
        searchPanel.gameObject.SetActive(!searchPanel.gameObject.activeSelf);
        if (searchPanel.gameObject.activeSelf)
        {
            searchPanel.SetAsLastSibling();
            feedback.text = string.Empty;
            searchInput.ActivateInputField();
        }
    }
    private void BuildLocalPopups()
    {
        // 07.1. A camada ocupa o Chat inteiro e fecha o menu somente fora do painel.
        optionsLayer = Rect("ConversationMenuLayer", content, 0, 0, 1920, 1080);
        WDHomeUIFactory.Stretch(optionsLayer);
        Image dismissGraphic = optionsLayer.gameObject.AddComponent<Image>();
        dismissGraphic.color = Color.clear;
        optionsDismissButton = optionsLayer.gameObject.AddComponent<Button>();
        optionsDismissButton.transition = Selectable.Transition.None;
        optionsDismissButton.targetGraphic = dismissGraphic;
        optionsDismissButton.onClick.AddListener(CloseOptions);
        optionsPopup = Surface("ConversationMenu", optionsLayer, 1123, 244, 270, 100, Alpha(Side, .98f), 12, Border);
        optionsPopup.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        muteButton = Button("Mute", optionsPopup, 4, 4, 262, 44, ToggleMute);
        muteLabel = Label(muteButton.transform, "Label", string.Empty, 12, 0, 238, 44, 14, Color.white);
        // O frame 630:2 mantém VER PERFIL no cabeçalho; o menu conserva ações complementares.
        blockButton = Button("Block", optionsPopup, 4, 52, 262, 44, BlockSelectedPlayer);
        Label(blockButton.transform, "Label", "BLOQUEAR JOGADOR", 12, 0, 238, 44, 14, Color.white);
        optionsLayer.gameObject.SetActive(false);
        optionsPopup.gameObject.SetActive(false);
        searchPanel = Surface("NewConversationPanel", content, 620, 290, 680, 180, Alpha(Side, .98f), 12, Border);
        Label(searchPanel, "Title", "NOVA CONVERSA", 16, 8, 580, 28, HeadingFontSize, Color.white, true);
        Surface("SearchField", searchPanel, 16, 48, 480, 48, Side, 12, Border);
        searchInput = Input("SearchInput", searchPanel, 28, 50, 456, 44, "Nickname ou ID exato");
        findButton = Button("Open", searchPanel, 512, 48, 152, 48, FindConversation, Alpha(Cyan, .15f), Cyan);
        Label(findButton.transform, "Label", "ABRIR", 0, 0, 152, 48, 14, Cyan, true, TextAlignmentOptions.Center);
        feedback = Label(searchPanel, "Feedback", string.Empty, 16, 100, 648, 28, StatusFontSize, Muted);
        cancelSearchButton = Button("Cancel", searchPanel, 512, 130, 152, 44, CloseSearch);
        Label(cancelSearchButton.transform, "Label", "CANCELAR", 0, 0, 152, 44, 14, Muted, true, TextAlignmentOptions.Center);
        searchPanel.gameObject.SetActive(false);
    }

    // 07.2. Resolve a posição pelo botão realmente renderizado. Assim o menu
    // acompanha o cabeçalho da conversa em Safe Area, Edge e ultrawide.
    private void PositionOptionsPopup()
    {
        if (optionsLayer == null || optionsPopup == null || optionsButton == null) return;
        RectTransform buttonRect = optionsButton.transform as RectTransform;
        if (buttonRect == null) return;
        Vector3[] corners = new Vector3[4];
        buttonRect.GetWorldCorners(corners);
        Vector3 topRight = optionsLayer.InverseTransformPoint(corners[2]);
        Vector3 bottomLeft = optionsLayer.InverseTransformPoint(corners[0]);
        Rect layerRect = optionsLayer.rect;
        float x = topRight.x - layerRect.xMin - optionsPopup.rect.width;
        float y = layerRect.yMax - bottomLeft.y + 8f;
        x = Mathf.Clamp(x, 32f, layerRect.width - 32f - optionsPopup.rect.width);
        y = Mathf.Clamp(y, 112f, layerRect.height - 32f - optionsPopup.rect.height);
        ResizeTop(optionsPopup, x, y, optionsPopup.rect.width, optionsPopup.rect.height);
    }

    // ============================================================
    // 07.1. BINDING PASSIVO DO PREFAB SERIALIZADO
    // ============================================================
    private void WireSerializedInteractions()
    {
        Bind(backButton, Back); Bind(clanButton, () => Select(chat.ClanChannel));
        Bind(notificationButton, OpenNotifications);
        Bind(newConversationButton, ToggleSearch); Bind(profileButton, ViewProfile);
        Bind(optionsButton, ToggleOptions);
        Bind(optionsDismissButton, CloseOptions);
        Bind(sendButton, SendCurrent); Bind(viewAllFriendsButton, ViewAllFriends);
        Bind(muteButton, ToggleMute); Bind(findButton, FindConversation);
        Bind(cancelSearchButton, CloseSearch);
        Bind(blockButton, BlockSelectedPlayer);
        recentScroll.onValueChanged.RemoveAllListeners();
        recentScroll.onValueChanged.AddListener(_ => RenderRecentRows());
        friendsScroll.onValueChanged.RemoveAllListeners();
        friendsScroll.onValueChanged.AddListener(_ => RenderFriendRows());
        messagesScroll.onValueChanged.RemoveAllListeners();
        messagesScroll.onValueChanged.AddListener(_ => RenderMessageRows());
        messageInput.onSubmit.RemoveAllListeners();
        messageInput.onSubmit.AddListener(_ => SendCurrent());
        // 07.2. O composer do frame é de uma linha; Enter dispara Submit e não insere quebra.
        messageInput.lineType = TMP_InputField.LineType.SingleLine;
        messageInput.onValueChanged.RemoveAllListeners();
        messageInput.onValueChanged.AddListener(_ => { SaveDraft(); RefreshSendState(); });
        viewAllFriendsButton.interactable = openFriends != null;
        // PersistTypography troca a fonte durante a autoria; reaplicar o pipeline oficial
        // garante que medição e renderização usem o mesmo atlas Noto Color.
        WDChatEmojiText.Apply(measure);
        emojiPicker?.BindSerialized(messageInput, emojiButton);
    }

    private void OpenNotifications() => openNotifications?.Invoke();

    private void RefreshHeaderEconomy()
    {
        if (headerCoins == null || headerGems == null) return;
        WDArmoryProfile wallet = walletProvider?.Invoke();
        headerCoins.text = WDNumberFormatter.Full(wallet?.Coins ?? 0);
        headerGems.text = WDNumberFormatter.Full(wallet?.Emeralds ?? 0);
        if (notificationButton != null) notificationButton.interactable = openNotifications != null;
    }

    private static void Bind(Button button, UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        if (action != null) button.onClick.AddListener(action);
    }

    private void ToggleMute()
    {
        WDChatChannel channel = SelectedChannel;
        CloseOptions();
        if (channel != null) chat.SetMuted(channel.Id, !channel.IsMuted);
    }

    private void CloseOptions()
    {
        if (optionsPopup != null) optionsPopup.gameObject.SetActive(false);
        if (optionsLayer != null) optionsLayer.gameObject.SetActive(false);
    }

    private void FindConversation()
    {
        WDProfileData result = social.FindCatalogResult(searchInput.text);
        if (result == null) feedback.text = "Jogador não encontrado.";
        else OpenPrivate(result.PlayerId);
    }

    private void CloseSearch() => searchPanel.gameObject.SetActive(false);

    private void BlockSelectedPlayer()
    {
        string other = OtherParticipant(SelectedChannel);
        if (other == null || confirm == null) return;
        CloseLocalPopups();
        confirm("BLOQUEAR JOGADOR?", "A conversa ficará indisponível.", () => social.Block(other));
    }

    // ============================================================
    // 08. COMPOSIÇÃO PROPORCIONAL DENTRO DA SAFE AREA REAL
    // ============================================================
    public static Rect FitCanonicalRect(Rect available)
    {
        float scale = Mathf.Max(.001f, Mathf.Min(1f,
            available.width / CanonicalSize.x, available.height / CanonicalSize.y));
        Vector2 size = CanonicalSize * scale;
        return new Rect(available.center - size / 2, size);
    }
    private void LateUpdate()
    {
        if (content == null) return;
        ApplyResponsiveConversation();
    }
    public void FitSafeArea()
    {
        if (content == null) return;
        RectTransform root = (RectTransform)transform;
        if (backgroundArtwork.texture != null)
            backgroundArtwork.uvRect = CoverUv(root.rect.size,
                new Vector2(backgroundArtwork.texture.width, backgroundArtwork.texture.height));
        responsiveFrame ??= WDHomeSecondaryScreenFrame.Configure(content);
        responsiveFrame.RefreshNow();
        Canvas.ForceUpdateCanvases();
        ApplyResponsiveConversation();
    }
    public void ApplySafeArea(Rect available)
    {
        if (content == null) return;
        // 08.1. A API pública permanece para testes e authoring, mas delega ao
        // mesmo contrato usado pelas demais telas secundárias.
        responsiveFrame ??= WDHomeSecondaryScreenFrame.Configure(content);
        responsiveFrame.ApplyAvailableRect(available);
        Canvas.ForceUpdateCanvases();
        ApplyResponsiveConversation();
    }

    private void ApplyResponsiveConversation()
    {
        RectTransform inner = content?.Find("ColumnsSurface") as RectTransform;
        RectTransform left = inner?.Find("Channels") as RectTransform;
        RectTransform center = inner?.Find("Conversation") as RectTransform;
        RectTransform right = inner?.Find("OnlineFriends") as RectTransform;
        if (center == null || left == null || right == null) return;
        float width = Mathf.Max(640f, center.rect.width);
        float height = Mathf.Max(640f, center.rect.height);
        if (Mathf.Abs(width - lastResponsiveCenterWidth) < .1f &&
            Mathf.Abs(height - lastResponsiveColumnHeight) < .1f) return;
        lastResponsiveCenterWidth = width;
        lastResponsiveColumnHeight = height;
        float historyHeight = Mathf.Max(456f, height - 184f);
        ResizeTop(center.Find("Header") as RectTransform, 0, 0, width, 96);
        RightTop(center.Find("Header/ViewProfile") as RectTransform, 70, 27, 132, 42);
        RightTop(center.Find("Header/Options") as RectTransform, 20, 27, 42, 42);
        ResizeTop(center.Find("Divider") as RectTransform, 0, 95, width, 1);
        ResizeTop(center.Find("Messages") as RectTransform, 0, 96, width, historyHeight);
        ResizeTop(center.Find("Composer") as RectTransform, 0, height - 88, width, 88);
        RectTransform field = center.Find("Composer/Field") as RectTransform;
        ResizeTop(field, 16, 16, width - 32, 56);
        ResizeTop(field?.Find("MessageInput") as RectTransform, 112, 6, width - 217, 44);
        RightTop(field?.Find("Send") as RectTransform, 14, 6, 44, 44);
        ResizeTop(left.Find("Support") as RectTransform, 16, height - 60, 242, 44);
        ResizeTop(friendsDiscord, 16, height - 142, right.rect.width - 32, 126);
        LayoutOnlineFriends();
    }

    // 08.1. O frame responsivo mantém laterais funcionais e entrega a variação
    // de largura à conversa. A altura acompanha Safe Area sem escala uniforme.
    private static void ConfigureResponsiveColumns(RectTransform inner,
        RectTransform left, RectTransform center, RectTransform right)
    {
        // 08.1. Header e conjunto de painéis compartilham X=32 e largura útil.
        // Os gaps são espaço externo real e continuam visíveis em toda resolução.
        StretchBody(inner, 32f, 32f, 112f, 24f);
        left.anchorMin = new Vector2(0f, 0f); left.anchorMax = new Vector2(0f, 1f);
        left.pivot = new Vector2(0f, .5f); left.offsetMin = Vector2.zero;
        left.offsetMax = new Vector2(LeftWidth, 0f);
        center.anchorMin = Vector2.zero; center.anchorMax = Vector2.one;
        center.pivot = new Vector2(.5f, .5f);
        center.offsetMin = new Vector2(LeftWidth + ColumnGap, 0f);
        center.offsetMax = new Vector2(-(RightWidth + ColumnGap), 0f);
        right.anchorMin = new Vector2(1f, 0f); right.anchorMax = Vector2.one;
        right.pivot = new Vector2(1f, .5f); right.offsetMin = new Vector2(-RightWidth, 0f);
        right.offsetMax = Vector2.zero;
    }

    // 08.2. O recurso oficial é um banner raster único. Um recorte focal desloca
    // o logotipo para cima sem deformá-lo e abre respiro para o botão separado.
    private static void ApplyDiscordFraming(RawImage artwork, float width, float height)
    {
        if (artwork?.texture == null) return;
        float textureAspect = (float)artwork.texture.width / artwork.texture.height;
        float viewportAspect = width / height;
        float ratio = textureAspect / viewportAspect;
        if (ratio < 1f)
        {
            float centeredY = (1f - ratio) * .5f;
            artwork.uvRect = new Rect(0f, Mathf.Max(0f, centeredY - .045f), 1f, ratio);
        }
        else
        {
            artwork.uvRect = new Rect((1f - 1f / ratio) * .5f, 0f, 1f / ratio, 1f);
        }
    }

    // 08.3. Cover centralizado: recorta UVs, nunca estica a arte ou cria barras.
    public static Rect CoverUv(Vector2 viewport, Vector2 texture)
    {
        if (viewport.x <= 0 || viewport.y <= 0 || texture.x <= 0 || texture.y <= 0)
            return new Rect(0, 0, 1, 1);
        float ratio = (viewport.x / viewport.y) / (texture.x / texture.y);
        return ratio > 1
            ? new Rect(0, (1 - 1 / ratio) * .5f, 1, 1 / ratio)
            : new Rect((1 - ratio) * .5f, 0, ratio, 1);
    }

    // ============================================================
    // 09. PRIMITIVAS UGUI E ASSETS OFICIAIS; NENHUMA FONTE TEMPORÁRIA
    // ============================================================
    private static Color Alpha(Color color, float alpha) => new(color.r, color.g, color.b, alpha);
    private static void StretchHorizontal(RectTransform rect, float left, float right, float height)
    { rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1); rect.offsetMin = new Vector2(left, -height); rect.offsetMax = new Vector2(-right, 0); }
    private static void StretchBody(RectTransform rect, float left, float right, float top, float bottom)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f); rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top); }
    private static void StretchCenterColumn(RectTransform rect, float left, float right, float height)
    { rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1); rect.offsetMin = new Vector2(left, -height); rect.offsetMax = new Vector2(-right, 0); }
    private static void CenterTop(RectTransform rect, float x, float y, float w, float h)
    { rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.pivot = new Vector2(.5f, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); }
    private static void RightTop(RectTransform rect, float right, float y, float w, float h)
    { if (rect == null) return; rect.anchorMin = rect.anchorMax = new Vector2(1, 1); rect.pivot = new Vector2(1, 1); rect.anchoredPosition = new Vector2(-right, -y); rect.sizeDelta = new Vector2(w, h); }
    private static void ResizeTop(RectTransform rect, float x, float y, float w, float h)
    { if (rect == null) return; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); }

    private static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
        return rect;
    }
    private static RectTransform Surface(string name, Transform parent, float x, float y, float w, float h, Color color, int radius = 0, Color? border = null)
    {
        RectTransform rect = Rect(name, parent, x, y, w, h);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        if (radius > 0) WDHomeUIFactory.ApplyRounded(image, radius);
        if (border.HasValue) WDHomeUIFactory.AddRoundedBorder(rect, border.Value, Mathf.Max(4, radius));
        return rect;
    }
    private static Button Button(string name, Transform parent, float x, float y, float w, float h, UnityAction action, Color? fill = null, Color? border = null, int radius = 16)
    {
        RectTransform rect = Surface(name, parent, x, y, w, h, fill ?? Color.clear, radius, border);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        button.transition = Selectable.Transition.None;
        button.interactable = action != null;
        if (action != null) button.onClick.AddListener(action);
        return button;
    }
    private static TextMeshProUGUI Label(Transform parent, string name, string value, float x, float y, float w, float h, float size, Color color, bool bold = false, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        TextMeshProUGUI text = Rect(name, parent, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = WDHomeTypography.Load(bold ? "Inter-Bold" : "Inter-Regular");
        text.fontSize = size;
        text.color = color;
        text.text = value;
        text.richText = false;
        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    // 09.1. Avatar oficial: máscara circular, imagem real quando disponível e
    // iniciais derivadas do perfil como fallback. O status permanece independente.
    private static void BuildAvatar(Transform parent, string name, float x, float y,
        float size, out RawImage portrait, out TextMeshProUGUI initials)
    {
        RectTransform root = Rect(name, parent, x, y, size, size);
        Image surface = root.gameObject.AddComponent<Image>();
        surface.color = new Color(17f / 255f, 24f / 255f, 32f / 255f, .96f);
        surface.raycastTarget = false;
        WDHomeUIFactory.ApplyRounded(surface, Mathf.RoundToInt(size * .5f));

        RectTransform clip = Rect("Clip", root, 3, 3, size - 6, size - 6);
        Image maskGraphic = clip.gameObject.AddComponent<Image>();
        maskGraphic.color = Color.white;
        maskGraphic.raycastTarget = false;
        WDHomeUIFactory.ApplyRounded(maskGraphic, Mathf.RoundToInt((size - 6) * .5f));
        Mask mask = clip.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        portrait = Rect("Portrait", clip, 0, 0, size - 6, size - 6)
            .gameObject.AddComponent<RawImage>();
        portrait.raycastTarget = false;
        initials = Label(clip, "Initials", string.Empty, 0, 0, size - 6, size - 6,
            size >= 48 ? 13 : 11, Color.white, false, TextAlignmentOptions.Center);
        initials.font = WDHomeTypography.Load("Inter-SemiBold");
        Image border = WDHomeUIFactory.AddThinRoundedBorder(root, Alpha(Gold, .32f),
            Mathf.RoundToInt(size * .5f));
        border.transform.SetAsLastSibling();
    }

    private static void ApplyAvatar(RawImage portrait, TMP_Text initials,
        WDProfileData profile, string fallbackName)
    {
        if (portrait == null || initials == null) return;
        Texture2D texture = profile != null ? WDProfileIdentityVisuals.ResolveAvatar(profile) : null;
        portrait.texture = texture;
        portrait.color = texture != null ? Color.white : Color.clear;
        if (texture != null)
            portrait.uvRect = CoverUv(portrait.rectTransform.rect.size,
                new Vector2(texture.width, texture.height));
        string name = profile?.Nickname ?? fallbackName ?? string.Empty;
        string[] parts = name.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
        initials.text = parts.Length > 1
            ? (parts[0][0].ToString() + parts[parts.Length - 1][0]).ToUpperInvariant()
            : name.Length >= 2 ? name.Substring(0, 2).ToUpperInvariant()
            : name.ToUpperInvariant();
        initials.gameObject.SetActive(texture == null);
    }

    // 09.2. A seleção visual projeta exclusivamente selectedChannelId. Borda e
    // marcador não recebem raycast e não criam uma segunda autoridade de estado.
    private static void BuildConversationSelectionVisual(Transform parent, float height,
        out Image border, out GameObject indicator)
    {
        RectTransform root = parent as RectTransform;
        border = WDHomeUIFactory.AddThinRoundedBorder(root, Alpha(Cyan, .58f), 12);
        border.name = "ActiveConversationBorder";
        RectTransform marker = Rect("ActiveConversationIndicator", parent,
            0f, 8f, 3f, Mathf.Max(20f, height - 16f));
        Image markerImage = marker.gameObject.AddComponent<Image>();
        markerImage.color = Alpha(Cyan, .9f);
        markerImage.raycastTarget = false;
        WDHomeUIFactory.ApplyRounded(markerImage, 2);
        indicator = marker.gameObject;
    }
    private Image ChatIcon(Transform parent, string name, float x, float y, float w, float h) => Icon(parent, "UI/Match/Chat/" + name, x, y, w, h);
    private Image Icon(Transform parent, string path, float x, float y, float w, float h)
    {
        Image image = Rect(path.Substring(path.LastIndexOf('/') + 1), parent, x, y, w, h).gameObject.AddComponent<Image>();
        image.sprite = GetSprite(path);
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }
    private Sprite GetSprite(string path)
    {
        if (sprites.TryGetValue(path, out Sprite sprite)) return sprite;
        sprite = Resources.Load<Sprite>(path);
        if (sprite != null) return sprite;
        Texture2D texture = Resources.Load<Texture2D>(path);
        if (texture == null) return null;
        sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f);
        sprite.hideFlags = HideFlags.DontSave;
        sprites.Add(path, sprite);
        return sprite;
    }
    private static RectTransform Dot(Transform parent, string name, float x, float y, float size, Color color) => Surface(name, parent, x, y, size, size, color, Mathf.RoundToInt(size / 2));
    private static TMP_InputField Input(string name, Transform parent, float x, float y, float w, float h, string placeholder)
    {
        RectTransform rect = Surface(name, parent, x, y, w, h, Color.clear);
        TMP_InputField input = rect.gameObject.AddComponent<TMP_InputField>();
        RectTransform viewport = Rect("Viewport", rect, 0, 0, w, h);
        viewport.gameObject.AddComponent<RectMask2D>();
        input.textViewport = viewport;
        input.textComponent = Label(viewport, "Text", string.Empty, 0, 0, w, h, MessageFontSize, Color.white);
        input.textComponent.overflowMode = TextOverflowModes.Overflow;
        input.placeholder = Label(viewport, "Placeholder", placeholder, 0, 0, w, h, 18, Muted);
        bool message = name == "MessageInput";
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = message
            ? WDUIContentContracts.ChatMessageMaxLength : WDUIContentContracts.SearchMaxLength;
        WDUITextPolicies.Apply(input.textComponent, message
            ? WDUITextPolicy.ScrollableBody : WDUITextPolicy.SingleLineEllipsis);
        input.richText = false;
        input.targetGraphic = rect.GetComponent<Image>();
        input.selectionColor = Alpha(Cyan, .3f);
        input.customCaretColor = true;
        input.caretColor = Cyan;
        return input;
    }
    private static ScrollRect Scroll(string name, Transform parent, float x, float y, float w, float h)
    {
        RectTransform viewport = Surface(name, parent, x, y, w, h, Color.clear);
        viewport.gameObject.AddComponent<RectMask2D>();
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = Rect("Content", viewport, 0, 0, w, h);
        scroll.content.anchorMax = new Vector2(1, 1);
        scroll.content.sizeDelta = new Vector2(0, h);
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28;
        return scroll;
    }
    private static void DestroyOwned(UnityEngine.Object target)
    { if (target != null) { if (Application.isPlaying) Destroy(target); else DestroyImmediate(target); } }
}
