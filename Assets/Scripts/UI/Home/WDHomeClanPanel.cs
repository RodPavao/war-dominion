using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum WDClanTab { Discover, Overview, Members, Trades, Log }

[DisallowMultipleComponent]
public sealed class WDHomeClanPanel : MonoBehaviour
{
    // ============================================================
    // 01. AUTORIDADES COMPARTILHADAS E ESTADO DE NAVEGAÇÃO
    // ============================================================
    private sealed class ViewState
    {
        public WDClanTab Tab;
        public string Query;
        public string SelectedClanId;
    }

    private WarDominionUITheme theme;
    private WDClanRepository clans;
    private WDProfileRepository profiles;
    private WDSocialRepository social;
    private WDNavigationStack navigation;
    private Action<string> openProfile;
    private Action<string> openChat;
    private Action<string, string, Action> confirm;
    private RectTransform content;
    private TMP_InputField searchInput;
    private TMP_InputField nameInput;
    private TMP_InputField tagInput;
    private TMP_InputField descriptionInput;
    private TMP_InputField quantityInput;
    private string selectedWeaponId;
    private string feedback;
    private WDClanAdmission createAdmission = WDClanAdmission.Open;
    private string createName = string.Empty;
    private string createTag = string.Empty;
    private string createDescription = string.Empty;

    public bool IsOpen => gameObject.activeSelf;
    public WDClanTab CurrentTab { get; private set; }
    public string SearchQuery { get; private set; } = string.Empty;
    public string SelectedClanId { get; private set; }
    public WDClanRepository Repository => clans;

    public void Build(WarDominionUITheme newTheme, WDClanRepository clanRepository,
        WDProfileRepository profileRepository, WDSocialRepository socialRepository,
        WDNavigationStack navigationStack, Action<string> profileAction,
        Action<string> chatAction, Action<string, string, Action> confirmAction)
    {
        theme = newTheme ?? throw new ArgumentNullException(nameof(newTheme));
        clans = clanRepository ?? throw new ArgumentNullException(nameof(clanRepository));
        selectedWeaponId = clans.Weapons.FirstOrDefault()?.Id;
        profiles = profileRepository ?? throw new ArgumentNullException(nameof(profileRepository));
        social = socialRepository ?? throw new ArgumentNullException(nameof(socialRepository));
        navigation = navigationStack ?? throw new ArgumentNullException(nameof(navigationStack));
        openProfile = profileAction;
        openChat = chatAction;
        confirm = confirmAction;
        Image backdrop = gameObject.AddComponent<Image>();
        backdrop.color = new Color(0.005f, 0.015f, 0.025f, 0.96f);
        backdrop.raycastTarget = true;
        content = WDHomeUIFactory.Rect("ClanContent", transform);
        WDHomeUIFactory.Stretch(content, 48f, 38f, 48f, 38f);
        clans.Changed += RefreshIfOpen;
        gameObject.SetActive(false);
    }

    public void Open()
    {
        CurrentTab = clans.LocalClan == null ? WDClanTab.Discover : WDClanTab.Overview;
        SearchQuery = string.Empty;
        SelectedClanId = null;
        navigation.Push(new WDNavigationContext("clan", Restore,
            () => new ViewState { Tab = CurrentTab, Query = SearchQuery,
                SelectedClanId = SelectedClanId }));
    }

    public void HideFromNavigation() => gameObject.SetActive(false);

    private void OnDestroy()
    {
        if (clans != null)
            clans.Changed -= RefreshIfOpen;
    }

    public bool TryHandleLocalBack()
    {
        if (!string.IsNullOrEmpty(SelectedClanId))
        { SelectedClanId = null; Render(); return true; }
        return false;
    }

    private void Restore(object state)
    {
        if (state is ViewState saved)
        { CurrentTab = saved.Tab; SearchQuery = saved.Query;
          SelectedClanId = saved.SelectedClanId; }
        transform.SetAsLastSibling();
        Render();
    }

    // ============================================================
    // 02. DESCOBERTA, BUSCA, INFORMAÇÕES E CRIAÇÃO
    // ============================================================
    private void Render()
    {
        gameObject.SetActive(true);
        Clear(content);
        AddButton(content, "Back", "< VOLTAR", () => navigation.TryBack(),
            new Vector2(0.01f, 0.91f), new Vector2(0.14f, 0.99f));
        AddText(content, "Title", "CLÃ", 30f, theme.TextoPrincipal,
            new Vector2(0.40f, 0.91f), new Vector2(0.60f, 0.99f), TextAlignmentOptions.Center);
        RectTransform surface = AddSurface(content, "Surface",
            new Vector2(0.02f, 0.03f), new Vector2(0.98f, 0.89f));
        if (clans.Availability != WDClanAvailability.Available)
        {
            AddText(surface, "Availability", AvailabilityText(), 22f, theme.TextoSecundario,
                new Vector2(0.15f, 0.38f), new Vector2(0.85f, 0.63f), TextAlignmentOptions.Center);
            if (clans.Availability == WDClanAvailability.Error)
                AddButton(surface, "Retry", "TENTAR NOVAMENTE", () => clans.SetAvailability(WDClanAvailability.Available),
                    new Vector2(0.4f, 0.24f), new Vector2(0.6f, 0.33f));
            return;
        }
        WDClanData myClan = clans.LocalClan;
        if (myClan == null)
        {
            CurrentTab = WDClanTab.Discover;
            RenderDiscover(surface);
        }
        else
        {
            if (CurrentTab == WDClanTab.Discover) CurrentTab = WDClanTab.Overview;
            RenderMine(surface, myClan);
        }
        if (!string.IsNullOrEmpty(feedback))
            AddText(surface, "Feedback", feedback, 15f, theme.Acento,
                new Vector2(0.03f, 0.01f), new Vector2(0.97f, 0.06f));
    }

    private void RenderDiscover(RectTransform surface)
    {
        AddText(surface, "DiscoverTitle", "SEM CLÃ · PROCURE OU CRIE UM CLÃ", 22f,
            theme.TextoPrincipal, new Vector2(0.03f, 0.90f), new Vector2(0.85f, 0.98f));
        searchInput = AddInput(surface, "Search", "Nome ou sigla", SearchQuery,
            new Vector2(0.03f, 0.82f), new Vector2(0.65f, 0.89f));
        AddButton(surface, "SearchButton", "BUSCAR", () =>
        { SearchQuery = searchInput.text.Trim(); Render(); },
            new Vector2(0.67f, 0.82f), new Vector2(0.81f, 0.89f));
        AddButton(surface, "ClearSearch", "LIMPAR", () =>
        { SearchQuery = string.Empty; Render(); },
            new Vector2(0.83f, 0.82f), new Vector2(0.97f, 0.89f));

        if (!string.IsNullOrEmpty(SelectedClanId))
        {
            RenderClanInfo(surface, clans.Find(SelectedClanId));
            return;
        }
        var results = clans.Search(SearchQuery);
        if (results.Count == 0)
            AddText(surface, "Empty", "NENHUM CLÃ ENCONTRADO", 20f, theme.TextoSecundario,
                new Vector2(0.08f, 0.56f), new Vector2(0.92f, 0.74f), TextAlignmentOptions.Center);
        for (int i = 0; i < results.Count && i < 7; i++)
        {
            WDClanData clan = results[i];
            float top = 0.77f - i * 0.085f;
            AddText(surface, $"Clan_{clan.Id}",
                $"[{clan.Tag}] {clan.Name} · {clan.Members.Count} MEMBROS · " +
                (clan.Admission == WDClanAdmission.Open ? "ABERTO" : "POR APROVAÇÃO"),
                17f, theme.TextoPrincipal, new Vector2(0.04f, top - 0.07f),
                new Vector2(0.78f, top));
            AddButton(surface, $"Info_{clan.Id}", "INFORMAÇÕES", () =>
            { SelectedClanId = clan.Id; Render(); },
                new Vector2(0.80f, top - 0.07f), new Vector2(0.96f, top));
        }
        AddText(surface, "CreateTitle", $"CRIAR CLÃ · {clans.CreationPolicy.CoinCost} MOEDAS",
            18f, theme.Acento, new Vector2(0.04f, 0.12f), new Vector2(0.37f, 0.19f));
        descriptionInput = AddInput(surface, "CreateDescription", "Descrição", createDescription,
            new Vector2(0.39f, 0.20f), new Vector2(0.77f, 0.27f));
        descriptionInput.onValueChanged.AddListener(value => createDescription = value);
        AddButton(surface, "CreateAdmission", createAdmission == WDClanAdmission.Open
            ? "ABERTO" : "POR APROVAÇÃO", () =>
            { createAdmission = createAdmission == WDClanAdmission.Open
                ? WDClanAdmission.Approval : WDClanAdmission.Open; Render(); },
            new Vector2(0.79f, 0.20f), new Vector2(0.97f, 0.27f));
        nameInput = AddInput(surface, "CreateName", "Nome", createName,
            new Vector2(0.39f, 0.10f), new Vector2(0.61f, 0.18f));
        nameInput.onValueChanged.AddListener(value => createName = value);
        tagInput = AddInput(surface, "CreateTag", "Sigla", createTag,
            new Vector2(0.63f, 0.10f), new Vector2(0.77f, 0.18f));
        tagInput.onValueChanged.AddListener(value => createTag = value);
        AddButton(surface, "CreateClan", "CRIAR GRATUITAMENTE", () =>
            Apply(clans.Create(clans.LocalPlayerId, nameInput.text, tagInput.text,
                descriptionInput.text, createAdmission, out _)),
            new Vector2(0.79f, 0.10f), new Vector2(0.97f, 0.18f));
    }

    private void RenderClanInfo(RectTransform surface, WDClanData clan)
    {
        if (clan == null) { SelectedClanId = null; Render(); return; }
        AddText(surface, "ClanInfo", $"[{clan.Tag}] {clan.Name}\n{clan.Description}\n" +
            $"MEMBROS {clan.Members.Count} · " +
            (clan.Admission == WDClanAdmission.Open ? "ABERTO" : "POR APROVAÇÃO"),
            20f, theme.TextoPrincipal, new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.73f));
        AddButton(surface, "CloseInfo", "VOLTAR À LISTA", () =>
        { SelectedClanId = null; Render(); },
            new Vector2(0.10f, 0.23f), new Vector2(0.38f, 0.32f));
        bool pending = clan.JoinRequests.Any(item => item.PlayerId == clans.LocalPlayerId);
        WDUIPremiumButton join = AddButton(surface, "JoinClan",
            pending ? "SOLICITAÇÃO ENVIADA" : clan.Admission == WDClanAdmission.Open
                ? "ENTRAR" : "SOLICITAR ENTRADA",
            () => Apply(clans.Join(clan.Id, clans.LocalPlayerId)),
            new Vector2(0.60f, 0.23f), new Vector2(0.90f, 0.32f));
        join.SetInteractable(!pending);
    }

    // ============================================================
    // 03. MEU CLÃ, MEMBROS E ADMINISTRAÇÃO CONTEXTUAL
    // ============================================================
    private void RenderMine(RectTransform surface, WDClanData clan)
    {
        WDClanRole role = clan.Member(clans.LocalPlayerId).Role;
        string[] labels = { "MEU CLÃ", "MEMBROS", "TROCAS", "REGISTRO" };
        WDClanTab[] tabs = { WDClanTab.Overview, WDClanTab.Members, WDClanTab.Trades, WDClanTab.Log };
        for (int i = 0; i < tabs.Length; i++)
        {
            WDClanTab tab = tabs[i];
            AddButton(surface, $"Tab_{tab}", labels[i], () =>
            { CurrentTab = tab; Render(); },
                new Vector2(0.03f + i * 0.245f, 0.88f),
                new Vector2(0.26f + i * 0.245f, 0.97f), CurrentTab == tab);
        }
        switch (CurrentTab)
        {
            case WDClanTab.Overview: RenderOverview(surface, clan, role); break;
            case WDClanTab.Members: RenderMembers(surface, clan, role); break;
            case WDClanTab.Trades: RenderTrades(surface, clan); break;
            case WDClanTab.Log: RenderLog(surface, clan); break;
        }
    }

    private void RenderOverview(RectTransform surface, WDClanData clan, WDClanRole role)
    {
        AddText(surface, "ClanSummary", $"[{clan.Tag}] {clan.Name}\n{clan.Description}\n" +
            $"{clan.Members.Count} MEMBROS · COMANDANTE {NameOf(clan.CommanderId)}\n" +
            $"ADMISSÃO: {(clan.Admission == WDClanAdmission.Open ? "ABERTO" : "POR APROVAÇÃO")}",
            20f, theme.TextoPrincipal, new Vector2(0.04f, 0.58f), new Vector2(0.78f, 0.84f));
        if (role == WDClanRole.Commander)
        {
            AddButton(surface, "Admission", "ALTERAR ADMISSÃO", () => Apply(clans.SetAdmission(
                clan.Id, clans.LocalPlayerId, clan.Admission == WDClanAdmission.Open
                    ? WDClanAdmission.Approval : WDClanAdmission.Open)),
                new Vector2(0.72f, 0.73f), new Vector2(0.96f, 0.82f));
            nameInput = AddInput(surface, "EditName", "Nome", clan.Name,
                new Vector2(0.04f, 0.47f), new Vector2(0.30f, 0.54f));
            tagInput = AddInput(surface, "EditTag", "Sigla", clan.Tag,
                new Vector2(0.32f, 0.47f), new Vector2(0.49f, 0.54f));
            descriptionInput = AddInput(surface, "EditDescription", "Descrição", clan.Description,
                new Vector2(0.51f, 0.47f), new Vector2(0.77f, 0.54f));
            AddButton(surface, "SaveInfo", "SALVAR DADOS", () => Apply(clans.Edit(clan.Id,
                clans.LocalPlayerId, nameInput.text, tagInput.text,
                descriptionInput.text, clan.EmblemId)),
                new Vector2(0.79f, 0.47f), new Vector2(0.96f, 0.54f));
            RenderAdministrativeRequests(surface, clan);
            AddButton(surface, "DeleteClan", "EXCLUIR CLÃ", () => ConfirmDelete(clan),
                new Vector2(0.78f, 0.06f), new Vector2(0.96f, 0.14f));
        }
        else AddButton(surface, "LeaveClan", "SAIR DO CLÃ", () => ConfirmAction(
            "SAIR DO CLÃ", "Você deixará de ser membro e perderá acesso ao Chat do Clã.",
            () => Apply(clans.Leave(clans.LocalPlayerId))),
            new Vector2(0.75f, 0.07f), new Vector2(0.96f, 0.15f));
    }

    private void RenderAdministrativeRequests(RectTransform surface, WDClanData clan)
    {
        int i = 0;
        foreach (WDClanJoinRequest request in clan.JoinRequests.Take(2))
        {
            float y = 0.36f - i * 0.09f;
            AddText(surface, $"Join_{request.Id}", $"ENTRADA: {NameOf(request.PlayerId)}",
                15f, theme.TextoPrincipal, new Vector2(0.04f, y), new Vector2(0.58f, y + 0.07f));
            AddButton(surface, $"Accept_{request.Id}", "ACEITAR", () =>
                Apply(clans.DecideJoin(clan.Id, clans.LocalPlayerId, request.Id, true)),
                new Vector2(0.60f, y), new Vector2(0.76f, y + 0.07f));
            AddButton(surface, $"Reject_{request.Id}", "REJEITAR", () =>
                Apply(clans.DecideJoin(clan.Id, clans.LocalPlayerId, request.Id, false)),
                new Vector2(0.78f, y), new Vector2(0.95f, y + 0.07f));
            i++;
        }
        foreach (WDClanKickRequest request in clan.KickRequests.Take(2))
        {
            float y = 0.18f - i * 0.07f;
            AddText(surface, $"Kick_{request.Id}", $"EXPULSÃO: {NameOf(request.TargetPlayerId)}",
                15f, theme.TextoPrincipal, new Vector2(0.04f, y), new Vector2(0.58f, y + 0.07f));
            AddButton(surface, $"ApproveKick_{request.Id}", "APROVAR", () =>
                Apply(clans.DecideKick(clan.Id, clans.LocalPlayerId, request.Id, true)),
                new Vector2(0.60f, y), new Vector2(0.76f, y + 0.07f));
            AddButton(surface, $"RejectKick_{request.Id}", "REJEITAR", () =>
                Apply(clans.DecideKick(clan.Id, clans.LocalPlayerId, request.Id, false)),
                new Vector2(0.78f, y), new Vector2(0.95f, y + 0.07f));
            i++;
        }
    }

    private void RenderMembers(RectTransform surface, WDClanData clan, WDClanRole role)
    {
        AddText(surface, "MembersTitle", "MEMBROS DO CLÃ", 20f, theme.Acento,
            new Vector2(0.04f, 0.78f), new Vector2(0.95f, 0.86f));
        int i = 0;
        foreach (WDClanMember member in clan.Members.Take(8))
        {
            float top = 0.76f - i * 0.085f;
            AddText(surface, $"Member_{member.PlayerId}",
                $"{NameOf(member.PlayerId)} · {member.Role} · {social.PresenceOf(member.PlayerId)}",
                15f, theme.TextoPrincipal, new Vector2(0.04f, top - 0.07f),
                new Vector2(0.40f, top));
            AddButton(surface, $"Profile_{member.PlayerId}", "PERFIL", () =>
                openProfile?.Invoke(member.PlayerId), new Vector2(0.42f, top - 0.07f),
                new Vector2(0.52f, top));
            if (member.PlayerId != clans.LocalPlayerId)
            {
                AddButton(surface, $"Chat_{member.PlayerId}", "CHAT", () =>
                    openChat?.Invoke(member.PlayerId), new Vector2(0.53f, top - 0.07f),
                    new Vector2(0.62f, top));
                RenderMemberActions(surface, clan, role, member, top);
            }
            i++;
        }
    }

    private void RenderMemberActions(RectTransform surface, WDClanData clan,
        WDClanRole localRole, WDClanMember member, float top)
    {
        if (localRole == WDClanRole.Commander)
        {
            if (member.Role == WDClanRole.Member)
                AddButton(surface, $"Promote_{member.PlayerId}", "PROMOVER", () => ConfirmAction(
                    "PROMOVER", $"Promover {NameOf(member.PlayerId)} a Oficial?",
                    () => Apply(clans.Promote(clan.Id, clans.LocalPlayerId, member.PlayerId))),
                    new Vector2(0.63f, top - 0.07f), new Vector2(0.74f, top));
            if (member.Role == WDClanRole.Officer)
                AddButton(surface, $"Demote_{member.PlayerId}", "REBAIXAR", () => ConfirmAction(
                    "REBAIXAR", $"Rebaixar {NameOf(member.PlayerId)} a Membro?",
                    () => Apply(clans.Demote(clan.Id, clans.LocalPlayerId, member.PlayerId))),
                    new Vector2(0.63f, top - 0.07f), new Vector2(0.74f, top));
            AddButton(surface, $"Transfer_{member.PlayerId}", "COMANDO", () => ConfirmAction(
                "TRANSFERIR COMANDO", $"Transferir o comando para {NameOf(member.PlayerId)}?",
                () => Apply(clans.TransferCommand(clan.Id, clans.LocalPlayerId, member.PlayerId))),
                new Vector2(0.75f, top - 0.07f), new Vector2(0.85f, top));
            AddButton(surface, $"Kick_{member.PlayerId}", "EXPULSAR", () => ConfirmAction(
                "EXPULSAR", $"Expulsar {NameOf(member.PlayerId)} do Clã?",
                () => Apply(clans.Kick(clan.Id, clans.LocalPlayerId, member.PlayerId))),
                new Vector2(0.86f, top - 0.07f), new Vector2(0.98f, top));
        }
        else if (localRole == WDClanRole.Officer && member.Role == WDClanRole.Member)
            AddButton(surface, $"RequestKick_{member.PlayerId}", "SOLICITAR EXPULSÃO", () =>
                ConfirmAction("SOLICITAR EXPULSÃO", "O Comandante decidirá a solicitação.",
                    () => Apply(clans.RequestKick(clan.Id, clans.LocalPlayerId, member.PlayerId))),
                new Vector2(0.65f, top - 0.07f), new Vector2(0.97f, top));
    }

    // ============================================================
    // 04. TROCAS DE TOKENS E REGISTRO
    // ============================================================
    private void RenderTrades(RectTransform surface, WDClanData clan)
    {
        AddText(surface, "TradesTitle", "TROCAS · PEDIDO E DOAÇÃO DE TOKENS DE ARMA",
            20f, theme.Acento, new Vector2(0.04f, 0.78f), new Vector2(0.95f, 0.86f));
        WDWeaponDefinition[] weapons = clans.Weapons.Take(6).ToArray();
        for (int i = 0; i < weapons.Length; i++)
        {
            WDWeaponDefinition weapon = weapons[i];
            AddButton(surface, $"Weapon_{weapon.Id}", weapon.DisplayName,
                () => { selectedWeaponId = weapon.Id; Render(); },
                new Vector2(0.04f + i * 0.15f, 0.67f),
                new Vector2(0.18f + i * 0.15f, 0.76f), selectedWeaponId == weapon.Id);
        }
        quantityInput = AddInput(surface, "Quantity", "Quantidade", "1",
            new Vector2(0.04f, 0.57f), new Vector2(0.25f, 0.65f));
        AddButton(surface, "RequestTokens", "PEDIR TOKENS", () =>
        {
            if (!int.TryParse(quantityInput.text, out int quantity))
            { Apply(WDClanResult.Invalid); return; }
            Apply(clans.RequestTokens(clans.LocalPlayerId, selectedWeaponId, quantity, out _));
        }, new Vector2(0.27f, 0.57f), new Vector2(0.48f, 0.65f));
        int row = 0;
        foreach (WDClanTokenRequest request in clan.TokenRequests.Where(item => !item.Fulfilled).Take(5))
        {
            float top = 0.54f - row * 0.09f;
            AddText(surface, $"TokenRequest_{request.Id}",
                $"{NameOf(request.PlayerId)} · {request.Quantity} TOKENS · " +
                (clans.Weapons.FirstOrDefault(item => item.Id == request.WeaponId)?.DisplayName ?? request.WeaponId),
                16f, theme.TextoPrincipal, new Vector2(0.04f, top - 0.08f),
                new Vector2(0.73f, top));
            if (request.PlayerId != clans.LocalPlayerId)
                AddButton(surface, $"Donate_{request.Id}", "DOAR", () => ConfirmAction(
                    "DOAR TOKENS", $"Doar {request.Quantity} tokens de {request.WeaponId}?",
                    () => Apply(clans.Donate(clans.LocalPlayerId, request.Id))),
                    new Vector2(0.76f, top - 0.08f), new Vector2(0.96f, top));
            row++;
        }
        if (row == 0)
            AddText(surface, "NoRequests", "NENHUM PEDIDO ATIVO", 18f,
                theme.TextoSecundario, new Vector2(0.3f, 0.28f), new Vector2(0.7f, 0.42f),
                TextAlignmentOptions.Center);
    }

    private void RenderLog(RectTransform surface, WDClanData clan)
    {
        AddText(surface, "LogTitle", "REGISTRO DO CLÃ", 20f, theme.Acento,
            new Vector2(0.04f, 0.78f), new Vector2(0.95f, 0.86f));
        int i = 0;
        foreach (WDClanLogEntry entry in clan.Log.Reverse().Take(8))
        {
            float top = 0.75f - i * 0.08f;
            AddText(surface, $"Log_{i}", $"{entry.At:dd/MM HH:mm} · {entry.Text}",
                16f, theme.TextoPrincipal, new Vector2(0.05f, top - 0.07f),
                new Vector2(0.94f, top));
            i++;
        }
    }

    private void ConfirmDelete(WDClanData clan) => ConfirmAction(
        "EXCLUIR CLÃ", $"Excluir definitivamente {clan.Name}? Todos perderão o acesso.",
        () => ConfirmAction("CONFIRMAÇÃO FINAL", $"Excluir {clan.Name} sem possibilidade de desfazer?",
            () => Apply(clans.Delete(clan.Id, clans.LocalPlayerId))));

    private void ConfirmAction(string title, string body, Action action) =>
        confirm?.Invoke(title, body, action);
    private void Apply(WDClanResult result)
    { feedback = result == WDClanResult.Success ? "AÇÃO CONCLUÍDA" : $"AÇÃO NÃO REALIZADA: {result}";
      Render(); }
    private void RefreshIfOpen() { if (IsOpen) Render(); }
    private string NameOf(string playerId) => profiles.Find(playerId)?.Nickname ?? playerId;
    private string AvailabilityText() => clans.Availability switch
    { WDClanAvailability.Loading => "CARREGANDO CLÃS...", WDClanAvailability.Error =>
      "ERRO AO CARREGAR CLÃS", WDClanAvailability.Offline => "CLÃS INDISPONÍVEIS OFFLINE",
      WDClanAvailability.Empty => "NENHUM CLÃ DISPONÍVEL", _ => string.Empty };

    // ============================================================
    // 05. ELEMENTOS REUTILIZÁVEIS DA APRESENTAÇÃO
    // ============================================================
    private RectTransform AddSurface(Transform parent, string name, Vector2 min, Vector2 max)
    {
        RectTransform rect = WDHomeUIFactory.Rect(name, parent);
        SetBounds(rect, min, max);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = theme.BackgroundElevated; image.raycastTarget = true;
        return rect;
    }

    private TextMeshProUGUI AddText(Transform parent, string name, string text, float size,
        Color color, Vector2 min, Vector2 max,
        TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        TextMeshProUGUI label = WDHomeUIFactory.Text(name, parent, theme, text, size, color, alignment);
        SetBounds(label.rectTransform, min, max);
        return label;
    }

    private WDUIPremiumButton AddButton(Transform parent, string name, string text,
        UnityEngine.Events.UnityAction action, Vector2 min, Vector2 max, bool selected = false)
    {
        RectTransform rect = WDHomeUIFactory.Rect(name, parent); SetBounds(rect, min, max);
        WDUIPremiumButton button = rect.gameObject.AddComponent<WDUIPremiumButton>();
        button.Build(theme, text, action); button.SetSelected(selected); return button;
    }

    private TMP_InputField AddInput(Transform parent, string name, string placeholder,
        string value, Vector2 min, Vector2 max)
    {
        RectTransform rect = WDHomeUIFactory.Rect(name, parent); SetBounds(rect, min, max);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = theme.BackgroundPrimary; image.raycastTarget = true;
        TMP_InputField input = rect.gameObject.AddComponent<TMP_InputField>();
        RectTransform viewport = WDHomeUIFactory.Rect("TextViewport", rect);
        WDHomeUIFactory.Stretch(viewport, 12f, 4f, 12f, 4f);
        TextMeshProUGUI text = AddText(viewport, "Text", value, 16f, theme.TextoPrincipal,
            Vector2.zero, Vector2.one);
        TextMeshProUGUI hint = AddText(viewport, "Placeholder", placeholder, 16f,
            theme.TextoSecundario, Vector2.zero, Vector2.one);
        input.textViewport = viewport; input.textComponent = text; input.placeholder = hint;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.text = value ?? string.Empty;
        return input;
    }

    private static void SetBounds(RectTransform rect, Vector2 min, Vector2 max)
    { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }

    private static void Clear(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            child.transform.SetParent(null, false);
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
    }
}
