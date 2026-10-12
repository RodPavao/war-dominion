using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WDMatchChatView : MonoBehaviour
{
    // 01. Apresentação da partida; mensagens, permissões e presença são externas.
    public static readonly Vector2 CanonicalSize = new(1400f, 760f);
    private static readonly Color Cyan = new(32f / 255f, 199f / 255f, 232f / 255f);
    private static readonly Color Muted = new(170f / 255f, 185f / 255f, 194f / 255f);
    private static readonly Color Green = new(57f / 255f, 217f / 255f, 138f / 255f);
    private static readonly Color Border = new(49f / 255f, 81f / 255f, 93f / 255f);
    private static readonly Color Side = new(14f / 255f, 23f / 255f, 29f / 255f, .75f);
    private readonly Dictionary<string, string> drafts = new();
    private readonly Dictionary<string, Sprite> sprites = new();
    private WDChatRepository repository;
    private WDChatChannel matchChannel;
    private IWDMatchUtilityCommands utilities;
    private string selectedId;
    private bool subscribed, refreshing, showAllFriends;
    private RectTransform panel, backdrop, recentContent, friendsContent, messagesContent;
    private ScrollRect messagesScroll;
    private TMP_InputField composer, searchInput;
    private WDChatEmojiPicker emojiPicker;
    private TextMeshProUGUI title, subtitle, friendsCount, friendsTitle, empty, feedback, muteLabel;
    private TextMeshProUGUI viewAllLabel;
    private GameObject onlineBadge, headerPresence, optionsPopup, searchPanel;
    private Button matchButton, clanButton, sendButton, optionsButton;
    private Image matchSelection, clanSelection;
    private Image matchOutline, clanOutline;
    private GameObject matchDot;
    private string renderedChannel;
    private int renderedCount = -1;

    public bool IsOpen => gameObject.activeSelf;
    public WDChatChannel SelectedChannel => repository?.Channels.FirstOrDefault(c => c.Id == selectedId);
    public RectTransform Panel => panel;

    // 02. Estrutura canônica 119:338. Somente conteúdo do Chat é criado aqui.
    public void Build(RectTransform[] utilityTargets = null)
    {
        backdrop = Rect("Backdrop", transform, 0, 0, 1, 1);
        var backdropImage = backdrop.gameObject.AddComponent<Image>();
        backdropImage.color = new Color(0, 0, 0, .55f);
        backdropImage.raycastTarget = true;
        Button close = backdrop.gameObject.AddComponent<Button>();
        close.transition = Selectable.Transition.None;
        close.onClick.AddListener(CloseFromBackdrop);
        WDMatchChatBackdropFilter filter = backdrop.gameObject.AddComponent<WDMatchChatBackdropFilter>();
        filter.UtilityTargets = utilityTargets;

        panel = Surface("Panel", transform, 0, 0, 1400, 760,
            WDMatchVisualStyle.SurfaceMatchReadable, 20,
            new Color(197f / 255f, 155f / 255f, 99f / 255f));
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
        panel.anchoredPosition = new Vector2(0, -18);
        panel.gameObject.AddComponent<EventTrigger>();
        filter.Panel = panel;
        RectTransform clipped = Surface("Columns", panel, 1, 1, 1398, 723,
            new Color(10f / 255f, 15f / 255f, 19f / 255f, .8f), 12);
        clipped.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        RectTransform left = Surface("Left", clipped, 0, 0, 279, 723, Alpha(Side, WDChatVisualStyle.ReadableAlpha));
        RectTransform center = Surface("Conversation", clipped, 279, 0, 770, 723,
            new Color(10f / 255f, 15f / 255f, 19f / 255f, WDChatVisualStyle.ReadableAlpha));
        RectTransform right = Surface("Friends", clipped, 1049, 0, 349, 723, Alpha(Side, WDChatVisualStyle.ReadableAlpha));
        Surface("Divider", clipped, 278, 0, 1, 723, Alpha(Border, .8f));
        Surface("Divider", clipped, 1048, 0, 1, 723, Alpha(Border, .8f));
        BuildLeft(left);
        BuildConversation(center);
        BuildFriends(right);
        FitSafeArea();
        gameObject.SetActive(false);
    }

    private void BuildLeft(RectTransform parent)
    {
        matchButton = Button("Match", parent, 15, 9, 248, 44, () => Select(matchChannel));
        matchSelection = matchButton.GetComponent<Image>();
        matchOutline = WDHomeUIFactory.AddRoundedBorder(matchButton.transform as RectTransform, Alpha(Cyan, .6f), 16);
        matchDot = Dot(matchButton.transform, "Selected", 227, 18, 8, Cyan).gameObject;
        Icon(matchButton.transform, "Globe", 13, 13, 18, 18);
        Label(matchButton.transform, "Label", "PARTIDA", 43, 0, 175, 44, WDChatVisualStyle.Control, Color.white, true);
        clanButton = Button("Clan", parent, 15, 61, 248, 44, () => Select(repository?.ClanChannel));
        clanSelection = clanButton.GetComponent<Image>();
        clanOutline = WDHomeUIFactory.AddRoundedBorder(clanButton.transform as RectTransform, Color.clear, 16);
        Icon(clanButton.transform, "Clan", 13, 13, 18, 18);
        Label(clanButton.transform, "Label", "CLÃ", 43, 0, 175, 44, WDChatVisualStyle.Control, Muted, true);
        Label(parent, "RecentsTitle", "RECENTES", 15, 129, 190, 24, WDChatVisualStyle.Section, Muted, true).characterSpacing = 1.8f;
        Button add = Button("NewConversation", parent, 219, 119, 44, 44, ToggleSearch);
        Icon(add.transform, "Plus", 14, 14, 16, 16);
        Scroll("Recents", parent, 15, 165, 248, 476, out recentContent);
        Button support = Button("Support", parent, 15, 663, 242, 44, null,
            new Color(14f / 255f, 23f / 255f, 29f / 255f, WDChatVisualStyle.ReadableAlpha), Border);
        Icon(support.transform, "Support", 13, 13, 18, 18);
        Label(support.transform, "Label", "SUPORTE", 43, 0, 180, 44, WDChatVisualStyle.Control, Color.white, true);
    }

    private void BuildConversation(RectTransform parent)
    {
        RectTransform header = Surface("Header", parent, 0, 0, 769, 89, Alpha(Side, WDChatVisualStyle.ReadableAlpha));
        Icon(header, "HexCyan", 20, 17, 48, 48);
        headerPresence = Dot(header, "Presence", 59, 55, 10, Green).gameObject;
        title = Label(header, "Name", string.Empty, 80, 8, 460, 28, WDChatVisualStyle.Heading, Color.white, true);
        onlineBadge = Surface("OnlineBadge", header, 545, 13, 64, 18, Alpha(Green, .2f), 9).gameObject;
        Label(onlineBadge.transform, "Label", "ONLINE", 4, 0, 56, 18, WDChatVisualStyle.Section, Green, true,
            TextAlignmentOptions.Center);
        subtitle = Label(header, "Status", string.Empty, 80, 38, 560, 24, WDChatVisualStyle.Status, Muted);
        optionsButton = Button("Options", header, 707, 18, 44, 44,
            () => optionsPopup.SetActive(!optionsPopup.activeSelf), Alpha(Side, WDChatVisualStyle.ReadableAlpha), Border);
        Icon(optionsButton.transform, "Options", 15.25f, 20.5f, 13.5f, 3);
        Surface("Divider", parent, 0, 88, 769, 1, Alpha(Border, .8f));
        messagesScroll = Scroll("Messages", parent, 0, 89, 769, 546, out messagesContent);
        empty = Label(messagesScroll.viewport, "Empty", string.Empty, 24, 180, 721, 90, 14, Muted,
            false, TextAlignmentOptions.Center);
        empty.textWrappingMode = TextWrappingModes.Normal;
        RectTransform form = Surface("Composer", parent, 0, 635, 769, 88, Alpha(Side, WDChatVisualStyle.ReadableAlpha));
        Surface("Divider", form, 0, 0, 769, 1, Alpha(Border, .8f));
        RectTransform field = Surface("Field", form, 16, 16, 737, 56,
            new Color(10f / 255f, 15f / 255f, 19f / 255f, WDChatVisualStyle.ReadableAlpha), 12,
            Border);
        Button attachment = Button("Attachment", field, 11, 6, 44, 44, null);
        Icon(attachment.transform, "Plus", 12, 12, 20, 20);
        composer = Input("MessageInput", field, 112, 6, 552, 44, "Digite uma mensagem...");
        // 02.1. Picker reutilizável recebe só o campo e o painel delimitador.
        Button emojiButton = Button("Emoji", field, 60, 6, 44, 44, () => { });
        emojiPicker = emojiButton.gameObject.AddComponent<WDChatEmojiPicker>();
        emojiPicker.Build(composer, emojiButton, panel);
        composer.textComponent.fontSize = WDChatVisualStyle.Message;
        ((TMP_Text)composer.placeholder).fontSize = WDChatVisualStyle.Placeholder;
        composer.onSubmit.AddListener(_ => SendDraft());
        composer.onValueChanged.AddListener(_ => RefreshSendState());
        sendButton = Button("Send", field, 675, 6, 44, 44, SendDraft, Cyan);
        Icon(sendButton.transform, "Send", 12, 12, 20, 20);

        optionsPopup = Surface("ChannelOptions", parent, 481, 70, 270, 52, Side, 12, Border).gameObject;
        Button mute = Button("Mute", optionsPopup.transform, 4, 4, 262, 44, ToggleMute);
        muteLabel = Label(mute.transform, "Label", string.Empty, 12, 0, 238, 44, WDChatVisualStyle.Control, Color.white);
        optionsPopup.SetActive(false);
        BuildSearch(parent);
    }

    private void BuildFriends(RectTransform parent)
    {
        friendsTitle = Label(parent, "Title", "AMIGOS ONLINE", 16, 9, 250, 18, WDChatVisualStyle.Section, Muted, true);
        friendsTitle.characterSpacing = 1.8f;
        friendsCount = Label(parent, "Count", string.Empty, 16, 29, 280, 20, WDChatVisualStyle.Status, Color.white);
        RectTransform friendIcon = Surface("FriendIcon", parent, 304, 13, 28, 28, Alpha(Green, .1f), 14);
        Icon(friendIcon, "Friends", 7, 7, 14, 14);
        Surface("Divider", parent, 16, 57, 317, 1, Alpha(Border, .8f));
        Scroll("FriendsList", parent, 16, 83, 317, 158, out friendsContent);
        Button viewAll = Button("ViewAll", parent, 16, 253, 317, 44, ToggleAllFriends,
            Alpha(Cyan, .1f), Alpha(Cyan, .4f));
        viewAllLabel = Label(viewAll.transform, "Label", "VER TODOS", 70, 0, 152, 44, WDChatVisualStyle.Control, Cyan, true,
            TextAlignmentOptions.Center);
        viewAllLabel.characterSpacing = 1f;
        Icon(viewAll.transform, "ArrowRight", 191, 15, 14, 14);
        RectTransform discord = Surface("Discord", parent, 16, 582, 317, 126, Color.white, 12);
        discord.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        RawImage artwork = Rect("Artwork", discord, 0, 0, 317, 126).gameObject.AddComponent<RawImage>();
        Texture2D texture = Resources.Load<Texture2D>("UI/Match/Chat/Discord");
        artwork.texture = texture;
        artwork.raycastTarget = false;
        artwork.color = new Color(1, 1, 1, .9f);
        if (texture != null)
        {
            float visibleHeight = (float)texture.width / texture.height / (317f / 126f);
            artwork.uvRect = new Rect(0, (1 - visibleHeight) * .5f, 1, visibleHeight);
        }
        Button community = Button("DiscordCommunity", parent, 167, 659, 151, 40, null, Alpha(Cyan, .21f));
        Label(community.transform, "Label", "ENTRAR NA COMUNIDADE", 6, 0, 120, 40, 7,
            new Color(7f / 255f, 21f / 255f, 27f / 255f), true);
        Icon(community.transform, "External", 124, 12, 16, 16);
    }

    // 03. Inscrições de dados limitadas ao painel visível; nenhuma autoridade nova.
    public void BindRepository(WDChatRepository value, WDChatChannel preferred)
    {
        Unsubscribe();
        if (repository != value) { drafts.Clear(); selectedId = null; }
        repository = value;
        matchChannel = preferred;
        if (selectedId == null) selectedId = preferred?.Id;
        renderedCount = -1;
        if (isActiveAndEnabled) { Subscribe(); Refresh(); }
    }

    public void BindUtilities(IWDMatchUtilityCommands value) => utilities = value;

    public void SetOpen(bool open)
    {
        if (!open) emojiPicker?.Close();
        if (gameObject.activeSelf == open) return;
        gameObject.SetActive(open);
        if (open) { transform.SetAsLastSibling(); FitSafeArea(); }
    }

    private void OnEnable() { Subscribe(); if (panel != null) Refresh(); }
    private void OnDisable() { Unsubscribe(); if (optionsPopup != null) optionsPopup.SetActive(false); }
    private void OnDestroy()
    {
        Unsubscribe();
        foreach (Sprite sprite in sprites.Values)
            if (sprite != null)
            {
                if (Application.isPlaying) Destroy(sprite); else DestroyImmediate(sprite);
            }
    }

    private void Subscribe()
    {
        if (subscribed || repository == null) return;
        repository.Changed += Refresh;
        repository.Social.Changed += Refresh;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || repository == null) return;
        repository.Changed -= Refresh;
        repository.Social.Changed -= Refresh;
        subscribed = false;
    }

    public void CloseFromBackdrop()
    {
        if (utilities?.ActiveModule == WDMatchUtilityModule.Chat) utilities.CloseActive();
    }

    public void Select(WDChatChannel channel)
    {
        emojiPicker?.Close();
        if (selectedId != null) drafts[selectedId] = composer.text;
        selectedId = channel?.Id;
        composer.SetTextWithoutNotify(selectedId != null && drafts.TryGetValue(selectedId, out string draft)
            ? draft : string.Empty);
        optionsPopup.SetActive(false);
        searchPanel.SetActive(false);
        renderedCount = -1;
        Refresh();
    }

    private void Refresh()
    {
        if (refreshing || panel == null || !isActiveAndEnabled) return;
        refreshing = true;
        try
        {
            WDChatChannel channel = SelectedChannel;
            bool accessible = channel != null && repository != null && channel.CanAccess(repository.LocalPlayerId);
            if (accessible && channel.IsUnread) repository.OpenContent(channel.Id);
            title.text = channel?.DisplayName ?? "PARTIDA";
            string other = OtherParticipant(channel);
            bool online = other != null && repository.Social.PresenceOf(other) != WDSocialPresence.Offline;
            onlineBadge.SetActive(online);
            headerPresence.SetActive(online);
            float badgeX = 80 + Mathf.Min(title.preferredWidth, 450) + 8;
            onlineBadge.GetComponent<RectTransform>().anchoredPosition = new Vector2(badgeX, -13);
            subtitle.text = !accessible ? "Canal indisponível" : other != null ? PresenceText(other)
                : channel.Type == WDChatChannelType.MatchTeam ? "Equipe da partida" : "Clã";
            optionsButton.interactable = accessible;
            muteLabel.text = channel?.IsMuted == true ? "REATIVAR NOTIFICAÇÕES" : "SILENCIAR CONVERSA";
            // 03.1. Navegar para PARTIDA não exige permissão de envio. O canal
            // real (ou seu estado indisponível) continua sendo o recebido no binding.
            matchButton.interactable = repository != null;
            clanButton.interactable = repository?.ClanChannel?.CanAccess(repository.LocalPlayerId) == true;
            matchSelection.color = Alpha(Cyan, channel == matchChannel ? .15f : 0);
            clanSelection.color = Alpha(Cyan, channel?.Type == WDChatChannelType.Clan ? .15f : 0);
            matchOutline.color = Alpha(Cyan, channel == matchChannel ? .6f : 0);
            clanOutline.color = Alpha(Cyan, channel?.Type == WDChatChannelType.Clan ? .6f : 0);
            matchDot.SetActive(channel == matchChannel);
            composer.interactable = accessible;
            RefreshSendState();
            RenderRecents();
            RenderFriends();
            RenderMessages(channel, accessible);
        }
        finally { refreshing = false; }
    }

    // 04. Linhas e bubbles vinculadas ao histórico; drafts não são reconstruídos.
    private void RenderRecents()
    {
        Clear(recentContent);
        WDChatChannel[] channels = repository?.RecentPrivate.Where(c => c.CanAccess(repository.LocalPlayerId)).ToArray()
            ?? Array.Empty<WDChatChannel>();
        float y = 0;
        foreach (WDChatChannel channel in channels)
        {
            string other = OtherParticipant(channel);
            Button row = Button("Recent_" + channel.Id, recentContent, 0, y, 248, 52,
                () => Select(channel), channel.Id == selectedId ? WDChatVisualStyle.SelectedConversation : Color.clear);
            Icon(row.transform, channel.Id == selectedId ? "HexCyan" : "HexNeutral", 8, 8, 36, 36);
            if (other != null && repository.Social.PresenceOf(other) != WDSocialPresence.Offline)
                Dot(row.transform, "Online", 36, 34, 8, Green);
            Label(row.transform, "Name", channel.DisplayName, 52, 5, 168, 20, WDChatVisualStyle.Name, Color.white, true)
                .font = WDHomeTypography.Load("Inter-SemiBold");
            var preview = Label(row.transform, "Preview", channel.Messages.LastOrDefault()?.Text ?? string.Empty,
                52, 25, 168, 27, WDChatVisualStyle.Preview, Muted);
            WDChatEmojiText.Apply(preview);
            // Ellipsis considera altura também: usar o espaço restante da linha
            // permite a ascendente do sprite + descendente da fonte, sem truncar emojis.
            preview.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                ((RectTransform)row.transform).rect.height + preview.rectTransform.anchoredPosition.y);
            if (channel.IsUnread) Dot(row.transform, "Unread", 225, 21, 10, Cyan);
            y += 60;
        }
        if (channels.Length == 0) Label(recentContent, "Empty", "Nenhuma conversa recente", 8, 8, 228, 30, 12, Muted);
        recentContent.sizeDelta = new Vector2(0, Mathf.Max(60, y));
    }

    private void RenderFriends()
    {
        Clear(friendsContent);
        WDProfileData[] friends = repository?.Social.Friends().ToArray() ?? Array.Empty<WDProfileData>();
        WDProfileData[] online = friends.Where(p => repository.Social.PresenceOf(p.PlayerId) != WDSocialPresence.Offline).ToArray();
        friendsCount.text = online.Length + (online.Length == 1 ? " companheiro ativo" : " companheiros ativos");
        friendsTitle.text = showAllFriends ? "AMIGOS" : "AMIGOS ONLINE";
        viewAllLabel.text = showAllFriends ? "VER ONLINE" : "VER TODOS";
        WDProfileData[] visible = showAllFriends ? friends : online;
        float y = 0;
        foreach (WDProfileData profile in visible)
        {
            Button row = Button("Friend_" + profile.PlayerId, friendsContent, 0, y, 317, 44,
                () => OpenPrivate(profile.PlayerId));
            bool isOnline = repository.Social.PresenceOf(profile.PlayerId) != WDSocialPresence.Offline;
            Icon(row.transform, isOnline ? "HexCyan" : "HexNeutral", 0, 4, 36, 36);
            if (isOnline) Dot(row.transform, "Online", 28, 32, 8, Green);
            Label(row.transform, "Name", profile.Nickname, 44, 5, 260, 20, WDChatVisualStyle.Name, Color.white, true)
                .font = WDHomeTypography.Load("Inter-SemiBold");
            Label(row.transform, "Presence", PresenceText(profile.PlayerId), 44, 25, 260, 19, WDChatVisualStyle.Status,
                isOnline ? Green : Muted);
            y += 48;
        }
        if (visible.Length == 0) Label(friendsContent, "Empty", showAllFriends ? "Nenhum amigo" : "Nenhum amigo online",
            0, 10, 317, 30, 12, Muted);
        friendsContent.sizeDelta = new Vector2(0, Mathf.Max(48, y));
    }

    private void RenderMessages(WDChatChannel channel, bool accessible)
    {
        empty.text = !accessible ? channel == null || channel.Type == WDChatChannelType.MatchTeam
            ? "Chat de equipe indisponível nesta partida.\nSelecione uma conversa ou o seu clã."
            : "Conversa indisponível. Selecione outra conversa."
            : channel.Messages.Count == 0 ? "Nenhuma mensagem nesta conversa." : string.Empty;
        empty.gameObject.SetActive(!accessible || channel.Messages.Count == 0);
        int count = accessible ? channel.Messages.Count : 0;
        if (renderedChannel == selectedId && renderedCount == count) return;
        bool atBottom = renderedChannel != selectedId || renderedCount < 0 || messagesScroll.verticalNormalizedPosition < .03f;
        float previousY = messagesContent.anchoredPosition.y;
        Clear(messagesContent);
        float y = 20;
        var rows = new List<RectTransform>();
        if (accessible)
        foreach (WDChatMessage message in channel.Messages)
        {
            bool sent = string.Equals(message.SenderId, repository.LocalPlayerId, StringComparison.OrdinalIgnoreCase);
            bool system = message.Kind == WDChatMessageKind.System;
            RectTransform row = Rect("Message", messagesContent, 0, y, 769, 70);
            rows.Add(row);
            float x = sent ? 265 : 68;
            if (system) x = 144;
            RectTransform bubble = Surface("Bubble", row, x, 0, 480, 48,
                sent ? Alpha(Cyan, .15f) : Alpha(Side, .9f), 16, sent ? Alpha(Cyan, .6f) : Border);
            TextMeshProUGUI text = Label(bubble, "Body", message.Text, 17, 12, 446, 24, WDChatVisualStyle.Message, Color.white);
            WDChatEmojiText.Apply(text);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.TopLeft;
            float textHeight = Mathf.Max(23, text.GetPreferredValues(446, Mathf.Infinity).y);
            float bubbleHeight = textHeight + 24;
            text.rectTransform.sizeDelta = new Vector2(446, textHeight);
            bubble.sizeDelta = new Vector2(480, bubbleHeight);
            string sender = repository.Profiles.Find(message.SenderId)?.Nickname ?? message.SenderId;
            string stamp = message.Timestamp.ToLocalTime().ToString("HH:mm");
            string metadata = sent || system ? stamp : sender + " · " + stamp;
            Label(row, "Timestamp", metadata, x, bubbleHeight + 4, 480, 15, WDChatVisualStyle.Timestamp, Muted,
                false, sent ? TextAlignmentOptions.Right : TextAlignmentOptions.Left);
            if (!sent && !system) Icon(row, "HexNeutral", 24, bubbleHeight - 6, 30, 30);
            row.sizeDelta = new Vector2(769, bubbleHeight + 19);
            y += bubbleHeight + 31;
        }
        float height = Mathf.Max(546, y + 8);
        float bottomOffset = Mathf.Max(0, 546 - y - 8);
        foreach (RectTransform row in rows) row.anchoredPosition += Vector2.down * bottomOffset;
        messagesContent.sizeDelta = new Vector2(0, height);
        messagesContent.anchoredPosition = new Vector2(0, atBottom ? height - 546 : Mathf.Clamp(previousY, 0, height - 546));
        messagesScroll.StopMovement();
        renderedChannel = selectedId;
        renderedCount = count;
    }

    // 05. Ações locais encaminhadas à autoridade; nenhuma mensagem/URL simulada.
    private bool sendingDraft;
    public void SendDraft()
    {
        WDChatChannel channel = SelectedChannel;
        if (sendingDraft || !IsOpen || !composer.interactable || repository == null ||
            channel?.CanAccess(repository.LocalPlayerId) != true || string.IsNullOrWhiteSpace(composer.text)) return;
        sendingDraft = true;
        try
        {
            if (!repository.Send(channel.Id, repository.LocalPlayerId, composer.text)) return;
            composer.SetTextWithoutNotify(string.Empty);
            drafts.Remove(channel.Id);
            messagesScroll.verticalNormalizedPosition = 0;
            RefreshSendState();
        }
        finally { sendingDraft = false; }
    }

    private void RefreshSendState()
    {
        if (sendButton != null) sendButton.interactable = repository != null &&
            SelectedChannel?.CanAccess(repository.LocalPlayerId) == true && !string.IsNullOrWhiteSpace(composer.text);
    }

    private void ToggleMute()
    {
        WDChatChannel channel = SelectedChannel;
        if (channel != null) repository.SetMuted(channel.Id, !channel.IsMuted);
        optionsPopup.SetActive(false);
    }

    private void ToggleAllFriends() { showAllFriends = !showAllFriends; RenderFriends(); }

    private void OpenPrivate(string id)
    {
        WDChatChannel channel = repository?.OpenPrivate(id);
        if (channel != null) Select(channel);
        else { searchPanel.SetActive(true); feedback.text = "Conversa indisponível pelas permissões atuais."; }
    }

    private string OtherParticipant(WDChatChannel channel) => channel?.Type == WDChatChannelType.Private
        ? channel.Participants.FirstOrDefault(id => !id.Equals(repository.LocalPlayerId, StringComparison.OrdinalIgnoreCase)) : null;

    private string PresenceText(string id) => repository.Social.PresenceOf(id) switch
    {
        WDSocialPresence.Online => "ONLINE",
        WDSocialPresence.InMatch => "EM PARTIDA",
        _ => "OFFLINE"
    };

    private void BuildSearch(RectTransform parent)
    {
        searchPanel = Surface("NewConversationPanel", parent, 24, 110, 721, 152,
            new Color(10f / 255f, 15f / 255f, 19f / 255f, .98f), 12, Border).gameObject;
        Label(searchPanel.transform, "Title", "NOVA CONVERSA", 16, 8, 680, 28, WDChatVisualStyle.Heading, Color.white, true);
        RectTransform inputBox = Surface("Input", searchPanel.transform, 16, 48, 544, 48, Side, 12, Border);
        searchInput = Input("SearchInput", inputBox, 12, 2, 520, 44, "Nickname ou ID exato");
        Button find = Button("Open", searchPanel.transform, 576, 48, 129, 48, () =>
        {
            WDProfileData result = repository?.Social.FindCatalogResult(searchInput.text);
            if (result == null) feedback.text = "Jogador não encontrado.";
            else OpenPrivate(result.PlayerId);
        }, Alpha(Cyan, .15f), Cyan);
        Label(find.transform, "Label", "ABRIR", 0, 0, 129, 48, WDChatVisualStyle.Control, Cyan, true, TextAlignmentOptions.Center);
        feedback = Label(searchPanel.transform, "Feedback", string.Empty, 16, 105, 688, 36, WDChatVisualStyle.Status, Muted);
        searchPanel.SetActive(false);
    }

    private void ToggleSearch()
    {
        searchPanel.SetActive(!searchPanel.activeSelf);
        if (searchPanel.activeSelf) { feedback.text = string.Empty; searchInput.ActivateInputField(); }
    }

    // 06. Painel uniforme dentro da Safe Area; backdrop cobre o Canvas inteiro.
    public static float ScaleForSafeArea(Vector2 available) => Mathf.Max(.01f,
        Mathf.Min(1f, (available.x - 32) / CanonicalSize.x, (available.y - 68) / CanonicalSize.y));

    private void LateUpdate() => FitSafeArea();

    public void FitSafeArea()
    {
        if (panel == null) return;
        RectTransform area = transform as RectTransform;
        float scale = ScaleForSafeArea(area.rect.size);
        panel.localScale = Vector3.one * scale;
        panel.anchoredPosition = new Vector2(0, -18 * scale);
        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : area;
        var corners = new Vector3[4];
        canvasRect.GetWorldCorners(corners);
        Vector3 min = area.InverseTransformPoint(corners[0]);
        Vector3 max = area.InverseTransformPoint(corners[2]);
        backdrop.anchorMin = backdrop.anchorMax = new Vector2(.5f, .5f);
        backdrop.pivot = new Vector2(.5f, .5f);
        backdrop.anchoredPosition = (min + max) * .5f;
        backdrop.sizeDelta = max - min;
    }

    // 07. Primitivas locais. Glyphs são assets oficiais; texto de jogadores é literal.
    private static Color Alpha(Color color, float alpha) => new(color.r, color.g, color.b, alpha);

    private static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
        return rect;
    }

    private static RectTransform Surface(string name, Transform parent, float x, float y, float w, float h,
        Color color, int radius = 0, Color? border = null)
    {
        RectTransform rect = Rect(name, parent, x, y, w, h);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        if (radius > 0) WDHomeUIFactory.ApplyRounded(image, radius);
        if (border.HasValue) WDHomeUIFactory.AddRoundedBorder(rect, border.Value, Mathf.Max(4, radius));
        return rect;
    }

    private static Button Button(string name, Transform parent, float x, float y, float w, float h,
        UnityAction action, Color? fill = null, Color? border = null)
    {
        RectTransform rect = Surface(name, parent, x, y, w, h, fill ?? Color.clear, 16, border);
        Button button = rect.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = rect.GetComponent<Image>();
        button.interactable = action != null;
        if (action != null) button.onClick.AddListener(action);
        return button;
    }

    private static TextMeshProUGUI Label(Transform parent, string name, string value, float x, float y,
        float w, float h, float size, Color color, bool bold = false,
        TextAlignmentOptions alignment = TextAlignmentOptions.Left)
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

    private Image Icon(Transform parent, string asset, float x, float y, float w, float h)
    {
        Image image = Rect(asset, parent, x, y, w, h).gameObject.AddComponent<Image>();
        if (!sprites.TryGetValue(asset, out Sprite sprite))
        {
            Texture2D texture = Resources.Load<Texture2D>("UI/Match/Chat/" + asset);
            if (texture != null)
            {
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
                sprite.hideFlags = HideFlags.DontSave;
                sprites.Add(asset, sprite);
            }
        }
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform Dot(Transform parent, string name, float x, float y, float size, Color color) =>
        Surface(name, parent, x, y, size, size, color, Mathf.RoundToInt(size / 2));

    private static TMP_InputField Input(string name, Transform parent, float x, float y, float w, float h, string placeholder)
    {
        RectTransform rect = Surface(name, parent, x, y, w, h, Color.clear);
        TMP_InputField input = rect.gameObject.AddComponent<TMP_InputField>();
        RectTransform viewport = Rect("Viewport", rect, 0, 0, w, h);
        viewport.gameObject.AddComponent<RectMask2D>();
        input.textViewport = viewport;
        input.textComponent = Label(viewport, "Text", string.Empty, 0, 0, w, h, WDChatVisualStyle.Message, Color.white);
        input.textComponent.overflowMode = TextOverflowModes.Overflow;
        input.placeholder = Label(viewport, "Placeholder", placeholder, 0, 0, w, h, WDChatVisualStyle.Placeholder, Muted);
        bool message = name == "MessageInput";
        input.lineType = message
            ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
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

    private static ScrollRect Scroll(string name, Transform parent, float x, float y, float w, float h,
        out RectTransform content)
    {
        RectTransform viewport = Surface(name, parent, x, y, w, h, Color.clear);
        viewport.gameObject.AddComponent<RectMask2D>();
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        content = Rect("Content", viewport, 0, 0, w, h);
        content.anchorMax = new Vector2(1, 1);
        content.sizeDelta = new Vector2(0, h);
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28;
        return scroll;
    }

    private static void Clear(RectTransform content)
    {
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            GameObject child = content.GetChild(i).gameObject;
            child.SetActive(false);
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
    }
}
