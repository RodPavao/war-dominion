using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public enum WDFriendsTab { Friends, Requests, Blocked }

[DisallowMultipleComponent]
public sealed class WDHomeFriendsPanel : MonoBehaviour
{
    // ============================================================
    // 01. DEPENDÊNCIAS E ESTADO PRESERVÁVEL
    // ============================================================

    [Serializable]
    private sealed class ViewState
    {
        public WDFriendsTab Tab;
        public string Query;
    }

    private WarDominionUITheme theme;
    private WDSocialRepository social;
    private WDSocialRepository subscribedSocial;
    private WDNavigationStack navigation;
    private Action<string> openProfile;
    private Action<string> openChat;
    private Action<string, string, Action> confirm;
    private RectTransform content;
    private TMP_InputField searchInput;
    private GameObject addModal;
    private TMP_InputField addInput;
    private string openMenuPlayerId;

    public bool IsOpen => gameObject.activeSelf;
    public bool IsAddModalOpen => addModal != null;
    public WDFriendsTab CurrentTab { get; private set; }
    public string SearchQuery { get; private set; } = string.Empty;

    public void Build(WarDominionUITheme newTheme, WDSocialRepository socialRepository,
        WDNavigationStack navigationStack, Action<string> profileAction,
        Action<string> chatAction, Action<string, string, Action> confirmAction)
    {
        UnsubscribeFromSocial();
        theme = newTheme ?? throw new ArgumentNullException(nameof(newTheme));
        social = socialRepository ?? throw new ArgumentNullException(nameof(socialRepository));
        navigation = navigationStack ?? throw new ArgumentNullException(nameof(navigationStack));
        openProfile = profileAction;
        openChat = chatAction;
        confirm = confirmAction;
        Image blocker = gameObject.AddComponent<Image>();
        blocker.color = new Color(0.005f, 0.015f, 0.025f, 0.94f);
        blocker.raycastTarget = true;
        content = WDHomeUIFactory.Rect("FriendsContent", transform);
        WDHomeUIFactory.Stretch(content, 54f, 44f, 54f, 44f);
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (social == null || subscribedSocial == social)
            return;
        UnsubscribeFromSocial();
        social.Changed += RefreshIfOpen;
        subscribedSocial = social;
    }

    private void OnDisable() => UnsubscribeFromSocial();

    public void Open()
    {
        CurrentTab = WDFriendsTab.Friends;
        SearchQuery = string.Empty;
        navigation.Push(new WDNavigationContext("friends", Restore,
            () => new ViewState { Tab = CurrentTab, Query = SearchQuery }));
    }

    public void HideFromNavigation()
    {
        CloseAddModal();
        openMenuPlayerId = null;
        gameObject.SetActive(false);
    }

    private void OnDestroy() => UnsubscribeFromSocial();

    private void UnsubscribeFromSocial()
    {
        if (subscribedSocial == null)
            return;
        subscribedSocial.Changed -= RefreshIfOpen;
        subscribedSocial = null;
    }

    public bool TryHandleLocalBack()
    {
        if (IsAddModalOpen)
        {
            CloseAddModal();
            return true;
        }
        if (!string.IsNullOrEmpty(openMenuPlayerId))
        {
            openMenuPlayerId = null;
            Render();
            return true;
        }
        return false;
    }

    private void Restore(object state)
    {
        if (state is ViewState saved)
        {
            CurrentTab = saved.Tab;
            SearchQuery = saved.Query ?? string.Empty;
        }
        transform.SetAsLastSibling();
        Render();
    }

    // ============================================================
    // 02. HUB, ABAS E BUSCA LOCAL
    // ============================================================

    private void Render()
    {
        gameObject.SetActive(true);
        Clear(content);
        AddButton(content, "Back", "<  VOLTAR", () => navigation.TryBack(),
            new Vector2(0.01f, 0.90f), new Vector2(0.13f, 0.98f));
        AddText(content, "Title", "AMIGOS", 30f, theme.TextoPrincipal,
            new Vector2(0.38f, 0.90f), new Vector2(0.62f, 0.98f),
            TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;

        (WDFriendsTab Tab, string Label)[] tabs =
        {
            (WDFriendsTab.Friends, "AMIGOS"),
            (WDFriendsTab.Requests, $"SOLICITAÇÕES ({social.PendingReceivedCount})"),
            (WDFriendsTab.Blocked, "BLOQUEADOS")
        };
        for (int i = 0; i < tabs.Length; i++)
        {
            var tab = tabs[i];
            AddButton(content, $"Tab_{tab.Tab}", tab.Label, () =>
            {
                CurrentTab = tab.Tab;
                openMenuPlayerId = null;
                Render();
            }, new Vector2(0.28f + i * 0.16f, 0.82f),
               new Vector2(0.43f + i * 0.16f, 0.89f), CurrentTab == tab.Tab);
        }

        RectTransform body = Panel("FriendsBody", content,
            new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.80f));
        switch (CurrentTab)
        {
            case WDFriendsTab.Friends: RenderFriends(body); break;
            case WDFriendsTab.Requests: RenderRequests(body); break;
            default: RenderBlocked(body); break;
        }
    }

    private void RenderFriends(RectTransform body)
    {
        searchInput = AddInput(body, "FriendSearch", "BUSCAR NA LISTA...", SearchQuery,
            new Vector2(0.03f, 0.86f), new Vector2(0.62f, 0.96f));
        searchInput.onValueChanged.AddListener(value =>
        {
            SearchQuery = value;
            RenderFriendRows(body);
        });
        AddButton(body, "AddFriend", "+  ADICIONAR AMIGO", OpenAddModal,
            new Vector2(0.76f, 0.86f), new Vector2(0.97f, 0.96f), true);
        RenderFriendRows(body);
    }

    private void RenderFriendRows(RectTransform body)
    {
        Transform existing = body.Find("Rows");
        if (existing != null) DestroyNow(existing.gameObject);
        RectTransform rows = WDHomeUIFactory.Rect("Rows", body);
        rows.anchorMin = new Vector2(0.03f, 0.05f); rows.anchorMax = new Vector2(0.97f, 0.83f);
        rows.offsetMin = rows.offsetMax = Vector2.zero;
        IReadOnlyList<WDProfileData> friends = social.Friends(SearchQuery);
        if (friends.Count == 0)
        {
            AddText(rows, "Empty", "NENHUM AMIGO ENCONTRADO.", 19f, theme.TextoSecundario,
                new Vector2(0.1f, 0.35f), new Vector2(0.9f, 0.65f), TextAlignmentOptions.Center);
            return;
        }
        for (int i = 0; i < friends.Count; i++)
            AddFriendRow(rows, friends[i], i);
    }

    private void AddFriendRow(RectTransform parent, WDProfileData profile, int index)
    {
        float top = 0.98f - index * 0.18f;
        RectTransform row = Panel($"Friend_{profile.PlayerId}", parent,
            new Vector2(0f, top - 0.15f), new Vector2(1f, top));
        AddText(row, "Identity", $"{profile.Nickname}\n<size=72%>{PresenceLabel(social.PresenceOf(profile.PlayerId))} · {profile.PlayerId}</size>",
            17f, theme.TextoPrincipal, new Vector2(0.03f, 0.12f), new Vector2(0.55f, 0.88f));
        AddButton(row, "Chat", "CHAT", () => openChat?.Invoke(profile.PlayerId),
            new Vector2(0.62f, 0.22f), new Vector2(0.73f, 0.78f));
        AddButton(row, "Profile", "PERFIL", () => openProfile?.Invoke(profile.PlayerId),
            new Vector2(0.75f, 0.22f), new Vector2(0.87f, 0.78f));
        AddButton(row, "More", "...", () =>
        { openMenuPlayerId = openMenuPlayerId == profile.PlayerId ? null : profile.PlayerId; Render(); },
            new Vector2(0.89f, 0.22f), new Vector2(0.97f, 0.78f));
        if (openMenuPlayerId == profile.PlayerId)
        {
            RectTransform menu = Panel("ContextMenu", content,
                new Vector2(0.67f, 0.22f), new Vector2(0.91f, 0.52f));
            AddButton(menu, "View", "VER PERFIL", () => openProfile?.Invoke(profile.PlayerId),
                new Vector2(0.08f, 0.68f), new Vector2(0.92f, 0.92f));
            AddButton(menu, "Remove", "REMOVER AMIGO", () => ConfirmRemove(profile),
                new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.62f));
            AddButton(menu, "Block", "BLOQUEAR", () => ConfirmBlock(profile),
                new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.32f));
        }
    }

    private void RenderRequests(RectTransform body)
    {
        IReadOnlyList<WDProfileData> requests = social.ReceivedRequests();
        AddText(body, "Heading", "SOLICITAÇÕES DE AMIZADE RECEBIDAS", 17f,
            theme.TextoPrincipal, new Vector2(0.03f, 0.86f), new Vector2(0.97f, 0.96f));
        if (requests.Count == 0)
        {
            AddEmpty(body, "NENHUMA SOLICITAÇÃO PENDENTE.");
            return;
        }
        for (int i = 0; i < requests.Count; i++)
        {
            WDProfileData profile = requests[i];
            float top = 0.82f - i * 0.15f;
            RectTransform row = Panel($"Request_{profile.PlayerId}", body,
                new Vector2(0.03f, top - 0.12f), new Vector2(0.97f, top));
            AddText(row, "Identity", $"{profile.Nickname}\n<size=72%>{profile.PlayerId}</size>", 17f,
                theme.TextoPrincipal, new Vector2(0.03f, 0.1f), new Vector2(0.64f, 0.9f));
            AddButton(row, "Decline", "RECUSAR", () => social.DeclineReceived(profile.PlayerId),
                new Vector2(0.70f, 0.18f), new Vector2(0.82f, 0.82f));
            AddButton(row, "Accept", "ACEITAR", () => social.AcceptReceived(profile.PlayerId),
                new Vector2(0.84f, 0.18f), new Vector2(0.96f, 0.82f), true);
        }
    }

    private void RenderBlocked(RectTransform body)
    {
        IReadOnlyList<WDProfileData> blocked = social.BlockedPlayers();
        AddText(body, "Heading", "JOGADORES BLOQUEADOS", 17f,
            theme.TextoPrincipal, new Vector2(0.03f, 0.86f), new Vector2(0.97f, 0.96f));
        if (blocked.Count == 0)
        {
            AddEmpty(body, "NENHUM JOGADOR BLOQUEADO.");
            return;
        }
        for (int i = 0; i < blocked.Count; i++)
        {
            WDProfileData profile = blocked[i];
            float top = 0.82f - i * 0.15f;
            RectTransform row = Panel($"Blocked_{profile.PlayerId}", body,
                new Vector2(0.03f, top - 0.12f), new Vector2(0.97f, top));
            AddText(row, "Identity", $"{profile.Nickname}\n<size=72%>BLOQUEADO · {profile.PlayerId}</size>", 17f,
                theme.TextoPrincipal, new Vector2(0.03f, 0.1f), new Vector2(0.72f, 0.9f));
            AddButton(row, "Unblock", "DESBLOQUEAR", () => social.Unblock(profile.PlayerId),
                new Vector2(0.78f, 0.18f), new Vector2(0.96f, 0.82f));
        }
    }

    // ============================================================
    // 03. MODAL ADICIONAR AMIGO E ESTADOS DE ELEGIBILIDADE
    // ============================================================

    private void OpenAddModal()
    {
        RectTransform modalRect = WDHomeUIFactory.Rect("AddFriendModal", transform);
        WDHomeUIFactory.Stretch(modalRect);
        addModal = modalRect.gameObject;
        Image blocker = addModal.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0.72f); blocker.raycastTarget = true;
        Button outside = addModal.AddComponent<Button>();
        outside.transition = Selectable.Transition.None; outside.onClick.AddListener(CloseAddModal);
        RectTransform panel = Panel("Panel", addModal.transform,
            new Vector2(0.30f, 0.27f), new Vector2(0.70f, 0.73f));
        AddText(panel, "Title", "ADICIONAR AMIGO", 25f, theme.TextoPrincipal,
            new Vector2(0.07f, 0.78f), new Vector2(0.93f, 0.94f));
        AddText(panel, "Hint", "Busca local no catálogo social do protótipo por nome ou ID.", 14f,
            theme.TextoSecundario, new Vector2(0.07f, 0.68f), new Vector2(0.93f, 0.78f));
        addInput = AddInput(panel, "GlobalSearch", "NOME OU ID PÚBLICO", string.Empty,
            new Vector2(0.07f, 0.52f), new Vector2(0.72f, 0.65f));
        AddButton(panel, "Search", "BUSCAR", SearchCatalog,
            new Vector2(0.75f, 0.52f), new Vector2(0.93f, 0.65f), true);
        AddButton(panel, "Cancel", "CANCELAR", CloseAddModal,
            new Vector2(0.66f, 0.08f), new Vector2(0.93f, 0.21f));
    }

    private void SearchCatalog()
    {
        WDProfileData profile = social.FindCatalogResult(addInput.text);
        Transform old = addModal.transform.Find("Panel/Result");
        if (old != null) DestroyNow(old.gameObject);
        RectTransform panel = addModal.transform.Find("Panel") as RectTransform;
        RectTransform result = Panel("Result", panel, new Vector2(0.07f, 0.25f), new Vector2(0.93f, 0.47f));
        if (profile == null)
        {
            AddText(result, "NotFound", "JOGADOR NÃO ENCONTRADO.", 17f, theme.TextoSecundario,
                new Vector2(0.04f, 0.1f), new Vector2(0.96f, 0.9f), TextAlignmentOptions.Center);
            return;
        }
        WDAddFriendEligibility eligibility = social.Eligibility(profile);
        string label = EligibilityLabel(eligibility);
        AddText(result, "Identity", $"{profile.Nickname}\n<size=72%>{profile.PlayerId}</size>", 17f,
            theme.TextoPrincipal, new Vector2(0.04f, 0.12f), new Vector2(0.60f, 0.88f));
        AddButton(result, "Action", label, () =>
        {
            if (eligibility == WDAddFriendEligibility.Eligible)
                social.SendFriendRequest(profile.PlayerId);
            SearchCatalog();
        }, new Vector2(0.64f, 0.20f), new Vector2(0.96f, 0.80f),
            eligibility == WDAddFriendEligibility.Eligible);
    }

    private void CloseAddModal()
    {
        if (addModal != null) DestroyNow(addModal);
        addModal = null; addInput = null;
    }

    private void ConfirmRemove(WDProfileData profile) => confirm?.Invoke(
        "REMOVER AMIGO?", $"Remover {profile.Nickname} da sua lista de amigos?", () =>
        { social.RemoveFriend(profile.PlayerId); openMenuPlayerId = null; Render(); });
    private void ConfirmBlock(WDProfileData profile) => confirm?.Invoke(
        "BLOQUEAR JOGADOR?", $"Bloquear {profile.Nickname}? Amizade e solicitações serão removidas.", () =>
        { social.Block(profile.PlayerId); openMenuPlayerId = null; Render(); });

    // ============================================================
    // 04. PRIMITIVAS VISUAIS
    // ============================================================

    private TMP_InputField AddInput(Transform parent, string name, string placeholder,
        string value, Vector2 min, Vector2 max)
    {
        RectTransform field = Panel(name, parent, min, max);
        TMP_InputField input = field.gameObject.AddComponent<TMP_InputField>();
        RectTransform viewport = WDHomeUIFactory.Rect("TextViewport", field);
        WDHomeUIFactory.Stretch(viewport, 12f, 4f, 12f, 4f);
        TextMeshProUGUI text = AddText(viewport, "Text", value, 17f, theme.TextoPrincipal,
            Vector2.zero, Vector2.one);
        TextMeshProUGUI hint = AddText(viewport, "Placeholder", placeholder, 17f,
            theme.TextoSecundario, Vector2.zero, Vector2.one);
        input.textViewport = viewport; input.textComponent = text; input.placeholder = hint;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.text = value ?? string.Empty;
        return input;
    }

    private RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max)
    {
        RectTransform rect = WDHomeUIFactory.Rect(name, parent);
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = theme.BackgroundElevated; image.raycastTarget = true;
        Outline outline = rect.gameObject.AddComponent<Outline>(); outline.effectColor = theme.BorderNeutral;
        return rect;
    }

    private TextMeshProUGUI AddText(Transform parent, string name, string value, float size,
        Color color, Vector2 min, Vector2 max,
        TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        TextMeshProUGUI label = WDHomeUIFactory.Text(name, parent, theme, value, size, color, alignment);
        label.rectTransform.anchorMin = min; label.rectTransform.anchorMax = max;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        return label;
    }

    private WDUIPremiumButton AddButton(Transform parent, string name, string label,
        UnityAction action, Vector2 min, Vector2 max, bool selected = false)
    {
        RectTransform rect = WDHomeUIFactory.Rect(name, parent);
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        WDUIPremiumButton button = rect.gameObject.AddComponent<WDUIPremiumButton>();
        button.Build(theme, label, action); button.SetSelected(selected); return button;
    }

    private void AddEmpty(RectTransform body, string message) => AddText(body, "Empty", message,
        19f, theme.TextoSecundario, new Vector2(0.1f, 0.30f), new Vector2(0.9f, 0.65f),
        TextAlignmentOptions.Center);
    private void RefreshIfOpen() { if (IsOpen) Render(); }
    private static string PresenceLabel(WDSocialPresence value) => value switch
    { WDSocialPresence.Online => "ONLINE", WDSocialPresence.InMatch => "EM PARTIDA", _ => "OFFLINE" };
    private static string EligibilityLabel(WDAddFriendEligibility value) => value switch
    { WDAddFriendEligibility.You => "VOCÊ", WDAddFriendEligibility.AlreadyFriend => "JÁ É AMIGO",
      WDAddFriendEligibility.RequestSent => "SOLICITAÇÃO ENVIADA",
      WDAddFriendEligibility.Blocked => "BLOQUEADO", _ => "ADICIONAR" };
    private static void Clear(Transform parent)
    { for (int i = parent.childCount - 1; i >= 0; i--) DestroyNow(parent.GetChild(i).gameObject); }
    private static void DestroyNow(UnityEngine.Object target)
    { if (target == null) return; if (Application.isPlaying) Destroy(target); else DestroyImmediate(target); }
}
