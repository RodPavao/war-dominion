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
    // 01. DEPENDÊNCIAS E CONTEXTO COMPARTILHADO
    // ============================================================

    private WarDominionUITheme theme;
    private WDChatRepository chat;
    private WDChatRepository subscribedChat;
    private WDSocialRepository social;
    private WDProfileRepository profiles;
    private WDNavigationStack navigation;
    private Action<string> openProfile;
    private Action<string, string, Action> confirm;
    private Action closeLocal;
    private RectTransform content;
    private TMP_InputField messageInput;
    private GameObject newConversationModal;
    private TMP_InputField searchInput;
    private TextMeshProUGUI searchFeedback;
    private string selectedChannelId;
    private bool menuOpen;

    public bool IsOpen => gameObject.activeSelf;
    public string SelectedChannelId => selectedChannelId;

    public void Build(WarDominionUITheme newTheme, WDChatRepository repository,
        WDSocialRepository socialRepository, WDProfileRepository profileRepository,
        WDNavigationStack navigationStack = null, Action<string> profileAction = null,
        Action<string, string, Action> confirmAction = null)
    {
        // 01. A inscrição ativa pertence ao painel, não ao repositório anterior.
        UnsubscribeFromChat();
        theme = newTheme;
        chat = repository;
        social = socialRepository;
        profiles = profileRepository;
        navigation = navigationStack;
        openProfile = profileAction;
        confirm = confirmAction;
        Image blocker = gameObject.AddComponent<Image>();
        blocker.color = new Color(0.005f, 0.015f, 0.025f, 0.94f);
        blocker.raycastTarget = true;
        content = WDHomeUIFactory.Rect("ChatContent", transform);
        WDHomeUIFactory.Stretch(content, 54f, 44f, 54f, 44f);
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        // 02. O painel oculto não precisa receber mudanças; Render lê o estado ao abrir.
        if (chat == null || subscribedChat == chat)
            return;
        UnsubscribeFromChat();
        chat.Changed += RefreshIfOpen;
        subscribedChat = chat;
    }

    private void OnDisable() => UnsubscribeFromChat();

    private void OnDestroy()
    {
        // 03. Segurança adicional para destruição após ativação.
        UnsubscribeFromChat();
    }

    private void UnsubscribeFromChat()
    {
        if (subscribedChat == null)
            return;
        subscribedChat.Changed -= RefreshIfOpen;
        subscribedChat = null;
    }

    public void OpenFromHome(string privatePlayerId = null)
    {
        closeLocal = null;
        if (!string.IsNullOrEmpty(privatePlayerId))
            selectedChannelId = chat.OpenPrivate(privatePlayerId)?.Id;
        navigation.Push(new WDNavigationContext("chat", Restore,
            () => selectedChannelId));
    }

    public void OpenLocal(Action closeAction, WDChatChannel preferred = null)
    {
        closeLocal = closeAction;
        selectedChannelId = preferred?.Id ?? selectedChannelId;
        transform.SetAsLastSibling();
        Render();
    }

    public void HideFromNavigation()
    {
        CloseNewConversation();
        gameObject.SetActive(false);
    }

    public bool TryHandleLocalBack()
    {
        if (newConversationModal != null) { CloseNewConversation(); return true; }
        if (menuOpen) { menuOpen = false; Render(); return true; }
        if (closeLocal != null) { closeLocal(); return true; }
        return false;
    }

    private void Restore(object state)
    {
        if (state is string saved) selectedChannelId = saved;
        transform.SetAsLastSibling();
        Render();
    }

    // ============================================================
    // 02. CANAIS, RECENTES E CONTEÚDO
    // ============================================================

    private void Render()
    {
        gameObject.SetActive(true);
        Clear(content);
        AddButton(content, "Back", "<  VOLTAR", Back,
            new Vector2(0.01f, 0.91f), new Vector2(0.13f, 0.99f));
        AddText(content, "Title", "CHAT", 30f, theme.TextoPrincipal,
            new Vector2(0.42f, 0.91f), new Vector2(0.58f, 0.99f),
            TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;

        RectTransform left = Panel("Channels", content, new Vector2(0.02f, 0.07f), new Vector2(0.23f, 0.89f));
        RectTransform center = Panel("Conversation", content, new Vector2(0.24f, 0.07f), new Vector2(0.75f, 0.89f));
        RectTransform right = Panel("OnlineFriends", content, new Vector2(0.76f, 0.07f), new Vector2(0.98f, 0.89f));
        RenderChannels(left);
        RenderConversation(center);
        RenderOnlineFriends(right);
    }

    private void RenderChannels(RectTransform parent)
    {
        float top = 0.94f;
        WDChatChannel match = FindAvailable(WDChatChannelType.MatchTeam);
        if (match != null)
        {
            AddChannelButton(parent, match, top); top -= 0.10f;
        }
        WDChatChannel clan = chat.ClanChannel;
        if (clan != null)
        {
            AddChannelButton(parent, clan, top); top -= 0.12f;
        }
        AddText(parent, "RecentTitle", "RECENTES", 15f, theme.TextoSecundario,
            new Vector2(0.06f, top - 0.06f), new Vector2(0.70f, top));
        AddButton(parent, "NewConversation", "+", OpenNewConversation,
            new Vector2(0.78f, top - 0.06f), new Vector2(0.94f, top));
        top -= 0.09f;
        IReadOnlyList<WDChatChannel> recents = chat.RecentPrivate;
        if (recents.Count == 0)
            AddText(parent, "NoRecent", "NENHUMA CONVERSA RECENTE.", 14f, theme.TextoSecundario,
                new Vector2(0.06f, top - 0.12f), new Vector2(0.94f, top));
        for (int i = 0; i < recents.Count && i < 6; i++)
        {
            AddChannelButton(parent, recents[i], top);
            top -= 0.10f;
        }
    }

    private void AddChannelButton(RectTransform parent, WDChatChannel channel, float top)
    {
        string marker = channel.IsUnread ? "● " : string.Empty;
        string muted = channel.IsMuted ? "  [SILENCIADO]" : string.Empty;
        AddButton(parent, $"Channel_{channel.Id}", marker + channel.DisplayName + muted, () =>
        {
            selectedChannelId = channel.Id;
            chat.OpenContent(channel.Id);
            Render();
        }, new Vector2(0.05f, top - 0.08f), new Vector2(0.95f, top),
            selectedChannelId == channel.Id);
    }

    private void RenderConversation(RectTransform parent)
    {
        WDChatChannel channel = FindSelected();
        if (channel == null)
        {
            AddText(parent, "Empty", "SELECIONE UMA CONVERSA OU CANAL.", 18f,
                theme.TextoSecundario, new Vector2(0.1f, 0.35f), new Vector2(0.9f, 0.65f),
                TextAlignmentOptions.Center);
            return;
        }
        AddText(parent, "Heading", channel.DisplayName, 22f, theme.TextoPrincipal,
            new Vector2(0.04f, 0.89f), new Vector2(0.72f, 0.97f));
        AddButton(parent, "Menu", "...", () => { menuOpen = !menuOpen; Render(); },
            new Vector2(0.88f, 0.89f), new Vector2(0.96f, 0.97f));
        string messages = channel.Messages.Count == 0 ? "CONVERSA VAZIA. ENVIE A PRIMEIRA MENSAGEM." :
            string.Join("\n\n", channel.Messages.Select(message => message.Kind == WDChatMessageKind.System
                ? $"[SISTEMA] {message.Text}"
                : $"{SenderName(message.SenderId)}  {message.Timestamp:HH:mm}\n{message.Text}"));
        AddText(parent, "Messages", messages, 16f, theme.TextoPrincipal,
            new Vector2(0.04f, 0.18f), new Vector2(0.96f, 0.86f));
        messageInput = AddInput(parent, "MessageInput", "DIGITE UMA MENSAGEM...", string.Empty,
            new Vector2(0.04f, 0.04f), new Vector2(0.82f, 0.14f));
        AddButton(parent, "Send", "ENVIAR", SendCurrent,
            new Vector2(0.84f, 0.04f), new Vector2(0.96f, 0.14f), true);
        if (menuOpen)
            RenderMenu(parent, channel);
    }

    private void RenderMenu(RectTransform parent, WDChatChannel channel)
    {
        RectTransform menu = Panel("ConversationMenu", parent,
            new Vector2(0.60f, 0.58f), new Vector2(0.96f, 0.88f));
        AddButton(menu, "Mute", channel.IsMuted ? "ATIVAR NOTIFICAÇÕES" : "SILENCIAR", () =>
        { chat.SetMuted(channel.Id, !channel.IsMuted); menuOpen = false; },
            new Vector2(0.06f, 0.68f), new Vector2(0.94f, 0.92f));
        if (channel.Type == WDChatChannelType.Private)
        {
            string other = OtherParticipant(channel);
            AddButton(menu, "Profile", "VER PERFIL", () => openProfile?.Invoke(other),
                new Vector2(0.06f, 0.38f), new Vector2(0.94f, 0.62f));
            AddButton(menu, "Block", "BLOQUEAR JOGADOR", () => confirm?.Invoke(
                "BLOQUEAR JOGADOR?", "A conversa ficará indisponível.", () => social.Block(other)),
                new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.32f));
        }
    }

    private void SendCurrent()
    {
        if (messageInput == null || string.IsNullOrWhiteSpace(selectedChannelId)) return;
        chat.Send(selectedChannelId, chat.LocalPlayerId, messageInput.text);
        Render();
    }

    private void RenderOnlineFriends(RectTransform parent)
    {
        AddText(parent, "Heading", "AMIGOS ONLINE\n<size=75%>PRESENÇA LOCAL/MOCK</size>", 17f,
            theme.TextoPrincipal, new Vector2(0.06f, 0.84f), new Vector2(0.94f, 0.96f));
        IReadOnlyList<WDProfileData> friends = social.Friends();
        int row = 0;
        foreach (WDProfileData friend in friends)
        {
            if (social.PresenceOf(friend.PlayerId) == WDSocialPresence.Offline) continue;
            float top = 0.80f - row++ * 0.10f;
            AddButton(parent, $"Online_{friend.PlayerId}", friend.Nickname, () =>
            {
                selectedChannelId = chat.OpenPrivate(friend.PlayerId)?.Id;
                Render();
            }, new Vector2(0.06f, top - 0.08f), new Vector2(0.94f, top));
        }
        if (row == 0)
            AddText(parent, "Empty", "NENHUM AMIGO ONLINE.", 14f, theme.TextoSecundario,
                new Vector2(0.08f, 0.55f), new Vector2(0.92f, 0.72f), TextAlignmentOptions.Center);
    }

    // ============================================================
    // 03. NOVA CONVERSA LOCAL
    // ============================================================

    private void OpenNewConversation()
    {
        RectTransform modalRect = WDHomeUIFactory.Rect("NewConversationModal", transform);
        WDHomeUIFactory.Stretch(modalRect);
        newConversationModal = modalRect.gameObject;
        Image blocker = newConversationModal.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0.72f); blocker.raycastTarget = true;
        RectTransform panel = Panel("Panel", newConversationModal.transform,
            new Vector2(0.32f, 0.34f), new Vector2(0.68f, 0.66f));
        AddText(panel, "Title", "NOVA CONVERSA", 24f, theme.TextoPrincipal,
            new Vector2(0.07f, 0.75f), new Vector2(0.93f, 0.93f));
        searchInput = AddInput(panel, "Search", "NOME OU ID PÚBLICO", string.Empty,
            new Vector2(0.07f, 0.46f), new Vector2(0.70f, 0.66f));
        AddButton(panel, "Open", "ABRIR", OpenSearchedConversation,
            new Vector2(0.73f, 0.46f), new Vector2(0.93f, 0.66f), true);
        AddButton(panel, "Cancel", "CANCELAR", CloseNewConversation,
            new Vector2(0.66f, 0.10f), new Vector2(0.93f, 0.28f));
        searchFeedback = AddText(panel, "Feedback", string.Empty, 13f, theme.Danger,
            new Vector2(0.07f, 0.29f), new Vector2(0.93f, 0.43f));
    }

    private void OpenSearchedConversation()
    {
        WDProfileData player = social.FindCatalogResult(searchInput.text);
        WDChatChannel channel = player == null ? null : chat.OpenPrivate(player.PlayerId);
        if (channel == null)
        {
            if (searchFeedback != null)
                searchFeedback.text = player == null
                    ? "JOGADOR NÃO ENCONTRADO."
                    : "CONVERSA INDISPONÍVEL PELAS REGRAS SOCIAIS/DE PRIVACIDADE.";
            return;
        }
        selectedChannelId = channel.Id;
        CloseNewConversation();
        Render();
    }

    private void CloseNewConversation()
    {
        if (newConversationModal != null) DestroyNow(newConversationModal);
        newConversationModal = null; searchInput = null; searchFeedback = null;
    }

    // ============================================================
    // 04. HELPERS VISUAIS E NAVEGAÇÃO
    // ============================================================

    private void Back()
    {
        if (closeLocal != null) closeLocal();
        else navigation?.TryBack();
    }
    private WDChatChannel FindSelected() => chat.Channels.FirstOrDefault(channel =>
        channel.Id == selectedChannelId && channel.IsAvailable);
    private WDChatChannel FindAvailable(WDChatChannelType type) => chat.Channels.FirstOrDefault(
        channel => channel.Type == type && channel.IsAvailable);
    private string OtherParticipant(WDChatChannel channel) => channel.Participants.First(id => id != chat.LocalPlayerId);
    private string SenderName(string id) => profiles.Find(id)?.Nickname ?? id;
    private void RefreshIfOpen() { if (IsOpen) Render(); }

    private TMP_InputField AddInput(Transform parent, string name, string placeholder,
        string value, Vector2 min, Vector2 max)
    {
        RectTransform field = Panel(name, parent, min, max);
        TMP_InputField input = field.gameObject.AddComponent<TMP_InputField>();
        RectTransform viewport = WDHomeUIFactory.Rect("TextViewport", field);
        WDHomeUIFactory.Stretch(viewport, 12f, 4f, 12f, 4f);
        TextMeshProUGUI text = AddText(viewport, "Text", value, 17f, theme.TextoPrincipal, Vector2.zero, Vector2.one);
        TextMeshProUGUI hint = AddText(viewport, "Placeholder", placeholder, 17f, theme.TextoSecundario, Vector2.zero, Vector2.one);
        input.textViewport = viewport; input.textComponent = text; input.placeholder = hint;
        input.lineType = TMP_InputField.LineType.SingleLine; input.text = value;
        return input;
    }
    private RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max)
    {
        RectTransform rect = WDHomeUIFactory.Rect(name, parent); rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Image image = rect.gameObject.AddComponent<Image>(); image.color = theme.BackgroundElevated; image.raycastTarget = true;
        Outline outline = rect.gameObject.AddComponent<Outline>(); outline.effectColor = theme.BorderNeutral; return rect;
    }
    private TextMeshProUGUI AddText(Transform parent, string name, string value, float size, Color color,
        Vector2 min, Vector2 max, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        TextMeshProUGUI label = WDHomeUIFactory.Text(name, parent, theme, value, size, color, alignment);
        label.rectTransform.anchorMin = min; label.rectTransform.anchorMax = max;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero; return label;
    }
    private WDUIPremiumButton AddButton(Transform parent, string name, string label, UnityAction action,
        Vector2 min, Vector2 max, bool selected = false)
    {
        RectTransform rect = WDHomeUIFactory.Rect(name, parent); rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        WDUIPremiumButton button = rect.gameObject.AddComponent<WDUIPremiumButton>();
        button.Build(theme, label, action); button.SetSelected(selected); return button;
    }
    private static void Clear(Transform parent)
    { for (int i = parent.childCount - 1; i >= 0; i--) DestroyNow(parent.GetChild(i).gameObject); }
    private static void DestroyNow(UnityEngine.Object target)
    { if (target == null) return; if (Application.isPlaying) Destroy(target); else DestroyImmediate(target); }
}
