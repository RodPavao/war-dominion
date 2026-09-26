using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WarDominionMatchHUD : MonoBehaviour, IWDMatchUIPresentation
{
    private static readonly Color FigmaGold = WDMatchVisualStyle.BorderMatchGold;
    private const float LeftRailWidth = 273f;
    private const float LeftContentWidth = 227f;
    private const float LeftContentX = (LeftRailWidth - LeftContentWidth) * 0.5f;
    // ============================================================
    // 01. ESTADO E REFERÊNCIAS DA COMPOSIÇÃO
    // ============================================================

    private WarDominionUITheme theme;
    private IMatchUICommands commands;
    private WDWeaponMatchUIController weaponUI;
    private ResolutionSequenceController resolutionSequence;
    private WDMatchInformationalOverlayController informationalOverlay;
    private MatchUIState state;
    private TextMeshProUGUI energyText, troopsText, timerText, roundText, attackAmountText;
    private RectTransform playersContent, actionsContent, actionsDrawer, supportHub, actionsDismissLayer, attackBar, popupLayer;
    private TextMeshProUGUI socialEmojiToast;
    private WDUIPremiumButton transferButton, confirmTransferButton, finishRoundButton,
        decreaseAttackButton, increaseAttackButton, sendAttackButton, actionsButton;
    private WDUIPremiumButton regionsButton;
    private WDUIPremiumButton supportButton;
    private WDUIPremiumButton speedButton;
    private readonly List<WDUIPremiumButton> deckButtons = new List<WDUIPremiumButton>();
    private readonly Dictionary<TerritorioClique.Dono, RectTransform> playerAnchors =
        new Dictionary<TerritorioClique.Dono, RectTransform>();
    private bool actionsOpen;
    private bool supportHubOpen;
    private bool hideSocialEmojis;
    private Action openChatHub;
    private WDChatRepository chatRepository;
    private string matchChatChannelId;
    private GameObject chatUnreadIndicator;
    private Coroutine drawerAnimation;
    private Coroutine emojiToastAnimation;
    private string actionsContentKey = string.Empty;
    private string playersContentKey = string.Empty;
    private int lastPresentedRound = -1;
    private GameManager.FaseTurno? lastPresentedPhase;

    // ============================================================
    // 02. COMPOSIÇÃO LATERAL, SEM PAINEL INFERIOR PERMANENTE
    // ============================================================

    public void Build(WarDominionUITheme newTheme, IMatchUICommands newCommands,
        WDWeaponMatchUIController newWeaponUI,
        ResolutionSequenceController newResolutionSequence,
        WDMatchInformationalOverlayController newInformationalOverlay,
        Action chatHubAction = null, WDChatRepository sharedChatRepository = null,
        string sharedMatchChannelId = null)
    {
        theme = newTheme;
        commands = newCommands;
        weaponUI = newWeaponUI;
        resolutionSequence = newResolutionSequence;
        if (resolutionSequence != null)
        {
            WDSettingsData preferences = WDSettingsRepository.LoadPersisted();
            resolutionSequence.PlaybackSpeed = preferences.ResolutionSpeed ==
                WDResolutionPresentationSpeed.Accelerated ? 2f : 1f;
        }
        informationalOverlay = newInformationalOverlay;
        openChatHub = chatHubAction;
        chatRepository = sharedChatRepository;
        matchChatChannelId = sharedMatchChannelId;
        if (chatRepository != null)
            chatRepository.Changed += RefreshChatAttention;
        // 02.1 — Geometria canônica do frame Figma 132:937, base 1920×1080.
        RectTransform left = CreateNeutralPanel("LeftCommandRail", new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(12f, -185f), new Vector2(273f, 874f));
        RectTransform right = CreateNeutralPanel("RightCommandRail", Vector2.one,
            Vector2.one, new Vector2(-9f, -329f), new Vector2(199f, 292f));
        BuildTopResources();
        BuildUtilityControls();
        BuildLeftRail(left);
        BuildRightRail(right);
        BuildAttackBar();
        BuildActionsDrawer();
        BuildSocialEmojiToast();
        popupLayer = WDUIFactory.Rect("ContextPopupLayer", transform);
        Stretch(popupLayer);
        popupLayer.gameObject.SetActive(false);
    }

    public void BindCommands(IMatchUICommands newCommands) => commands = newCommands;

    public void BindUtilities(IWDMatchUtilityCommands utilities)
    {
        // Compatibilidade de compilação da presentation aposentada.
        // A autoridade V2 não registra esta view.
    }

    private void BuildLeftRail(RectTransform rail)
    {
        // 02.2. Todos os blocos do rail compartilham o mesmo eixo central.
        RectTransform navigation = WDUIFactory.Rect("PrimaryNavigation", rail);
        AnchorTop(navigation, LeftContentX, 27f, LeftContentWidth, 40f);
        HorizontalLayoutGroup navigationRow = navigation.gameObject.AddComponent<HorizontalLayoutGroup>();
        navigationRow.spacing = 16f; navigationRow.childControlWidth = true;
        navigationRow.childForceExpandWidth = true;
        CreateIconButton(navigation, "Menu", "UI/Match/FigmaCurrent/Menu", ShowMatchInfo);
        regionsButton = CreateIconButton(navigation, "Regions", "UI/Match/FigmaCurrent/Regions", ToggleRegions);
        speedButton = CreateButton(navigation, ">>", ToggleResolutionSpeed);
        speedButton.SetPersistentColors(Color.clear, Color.clear);
        roundText = CreateText(rail, "Round", 36f, FigmaGold,
            new Vector2(30f, -75f), new Vector2(213f, 44f));
        RectTransform deck = WDUIFactory.Rect("Deck", rail);
        AnchorTop(deck, LeftContentX, 112f, LeftContentWidth, 511f);
        GridLayoutGroup grid = deck.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(109f, 121f); grid.spacing = new Vector2(9f, 9f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 2;
        for (int i = 0; i < 8; i++)
        {
            int slot = i;
            WDUIPremiumButton card = CreateButton(deck,
                $"SLOT {i + 1}", () => ToggleCard(slot));
            card.SetHoverFeedbackEnabled(false);
            card.SetPersistentColors(new Color(11f / 255f, 14f / 255f, 18f / 255f, 0.4f),
                new Color(FigmaGold.r, FigmaGold.g, FigmaGold.b, 0.22f));
            card.GetComponent<LayoutElement>().preferredHeight = 121f;
            TextMeshProUGUI cardLabel = card.GetComponentInChildren<TextMeshProUGUI>();
            if (cardLabel != null)
            {
                cardLabel.fontSize = 15f;
                cardLabel.textWrappingMode = TextWrappingModes.Normal;
            }
            deckButtons.Add(card);
        }
        supportButton = CreateCompactControl(rail, "SupportControl", "APOIO",
            new Vector2(LeftContentX, -689f), ToggleSupportHub, LeftContentWidth, 43f);
        transferButton = CreateCompactControl(rail, "TransferControl", "TRANSFERÊNCIA",
            new Vector2(LeftContentX, -746f), ToggleTransfer, LeftContentWidth, 43f);
        confirmTransferButton = CreateCompactControl(rail, "ConfirmTransferControl",
            "CONFIRMAR TRANSFERÊNCIA", new Vector2(LeftContentX, -803f), ConfirmTransferRecipient, LeftContentWidth, 43f);
        actionsButton = CreateCompactControl(rail, "ActionsControl", "AÇÕES",
            new Vector2(LeftContentX, -803f), ToggleActionsDrawer, LeftContentWidth, 43f);
    }

    private void BuildRightRail(RectTransform rail)
    {
        playersContent = WDUIFactory.Rect("Players", rail);
        AnchorTop(playersContent, 12f, 23f, 175f, 250f);
        VerticalLayoutGroup list = playersContent.gameObject.AddComponent<VerticalLayoutGroup>();
        list.spacing = 8f; list.childControlHeight = true; list.childForceExpandHeight = false;
        // 02.3. Finalização e aceleração são controles semanticamente distintos.
        finishRoundButton = CreateCompactControl(rail, "FinishRound", "ENVIAR JOGADAS",
            new Vector2(-99f, -622f), FinishRound, 230f, 69f);
    }

    // 02.2. Recursos e utilidades pertencem ao Safe Area Root, independentes
    // das colunas laterais. A largura extra reposiciona sem alterar seu tamanho.
    private void BuildTopResources()
    {
        RectTransform resources = CreateNeutralPanel("RoundResources", new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(69f, -12f), new Vector2(430f, 60f));
        HorizontalLayoutGroup row = resources.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 8f;
        row.padding = new RectOffset(22, 22, 0, 0);
        row.childControlWidth = true;
        row.childForceExpandWidth = true;
        energyText = CreateResourceIndicator(resources, "Energy", "UI/Match/FigmaCurrent/Energy");
        troopsText = CreateResourceIndicator(resources, "Troops", "UI/Match/FigmaCurrent/Troops");
        timerText = CreateResourceIndicator(resources, "Time", "UI/Match/FigmaCurrent/Time");
    }

    private void BuildUtilityControls()
    {
        RectTransform tools = WDUIFactory.Rect("UtilityControls", transform);
        tools.anchorMin = tools.anchorMax = Vector2.one;
        tools.pivot = Vector2.one;
        tools.anchoredPosition = new Vector2(-55f, -19f);
        tools.sizeDelta = new Vector2(140f, 60f);
        HorizontalLayoutGroup row = tools.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 8f;
        row.childControlWidth = true;
        row.childForceExpandWidth = true;
        WDUIPremiumButton chatButton = CreateIconButton(tools, "Chat",
            "UI/Match/FigmaCurrent/Chat", ShowMessagesPopup, 40f);
        RectTransform unread = WDUIFactory.Rect("UnreadIndicator", chatButton.transform);
        unread.anchorMin = unread.anchorMax = Vector2.one;
        unread.pivot = Vector2.one;
        unread.anchoredPosition = new Vector2(-3f, -3f);
        unread.sizeDelta = new Vector2(10f, 10f);
        Image unreadImage = unread.gameObject.AddComponent<Image>();
        unreadImage.color = theme.Danger;
        unreadImage.raycastTarget = false;
        chatUnreadIndicator = unread.gameObject;
        RefreshChatAttention();
        CreateIconButton(tools, "Settings", "UI/Match/FigmaCurrent/Settings",
            ShowMatchSettings, 40f);
    }

    // ============================================================
    // 03. APRESENTAÇÃO DO ESTADO AUTORITATIVO
    // ============================================================

    public void Present(MatchUIState newState)
    {
        if (newState == null || theme == null) return;
        bool newRound = lastPresentedRound >= 0 && lastPresentedRound != newState.Round;
        bool enteredResolution = lastPresentedPhase.HasValue &&
            lastPresentedPhase.Value != GameManager.FaseTurno.Resolucao &&
            newState.Fase == GameManager.FaseTurno.Resolucao;
        lastPresentedRound = newState.Round;
        lastPresentedPhase = newState.Fase;
        state = newState;
        if (newRound || enteredResolution || state.AcoesEnviadas &&
            state.Fase == GameManager.FaseTurno.Preparacao)
            CloseActionsDrawerImmediately();
        energyText.text = $"ENERGIA  {weaponUI?.CurrentEnergy ?? 0}";
        troopsText.text = $"TROPAS  {state.ReforcosDisponiveis}";
        int seconds = Mathf.CeilToInt(state.TempoRestante);
        timerText.text = $"TEMPO  {seconds / 60:00}:{seconds % 60:00}";
        roundText.text = state.EmMorteSubita ? $"MORTE SÚBITA {state.RoundMorteSubita}" : $"ROUND {state.Round}";
        transferButton.SetInteractable(state.PodeEditarPreparacao && state.TransferenciaDisponivel && !state.TransferenciaUsada);
        transferButton.gameObject.SetActive(state.UsaEquipes);
        SetLeftRailControlPosition(actionsButton, ActionsControlTop(state.UsaEquipes));
        actionsButton.SetInteractable(state.PodeEditarPreparacao);
        transferButton.SetSelected(state.EmModoTransferencia);
        transferButton.SetLabel(state.EmModoTransferencia ? "CANCELAR TRANSFERÊNCIA" : "TRANSFERÊNCIA");
        confirmTransferButton.gameObject.SetActive(false);
        finishRoundButton.SetInteractable(
            state.AcoesEnviadas
                ? state.PodeCancelarEnvio
                : state.PodeEnviarPreparacao && state.ReforcosDisponiveis == 0);
        finishRoundButton.SetLabel(state.AcoesEnviadas
            ? "AÇÕES ENVIADAS"
            : "ENVIAR JOGADAS");
        finishRoundButton.gameObject.SetActive(state.Fase == GameManager.FaseTurno.Preparacao);
        speedButton.gameObject.SetActive(state.Fase == GameManager.FaseTurno.Resolucao);
        supportButton.gameObject.SetActive(state.Fase == GameManager.FaseTurno.Preparacao);
        if (state.Fase != GameManager.FaseTurno.Preparacao && supportHubOpen)
        {
            supportHubOpen = false;
            supportHub.gameObject.SetActive(false);
        }
        regionsButton?.SetSelected(informationalOverlay?.RegionsActive == true);
        PresentDeck();
        string newPlayersKey = BuildPlayersContentKey();
        if (newPlayersKey != playersContentKey)
            RebuildPlayers(newPlayersKey);
        PresentAttackBar();
        if (actionsOpen && BuildActionsContentKey() != actionsContentKey) RebuildActions();
        PresentWeaponModalIfNeeded();
    }

    private void PresentDeck()
    {
        IReadOnlyList<WDWeaponHudCardState> cards = weaponUI?.GetCards() ??
            System.Array.Empty<WDWeaponHudCardState>();
        for (int i = 0; i < deckButtons.Count; i++)
        {
            WDWeaponHudCardState card = i < cards.Count ? cards[i] : null;
            bool selected = card?.Selected == true;
            bool available = IsCardInteractable(card, state.PodeEditarPreparacao);
            deckButtons[i].SetInteractable(
                available || selected || ShouldAcceptInsufficientEnergyClick(card, state.PodeEditarPreparacao));
            deckButtons[i].SetSelected(selected);
            string status = FormatCardStatus(card, weaponUI?.CurrentEnergy ?? 0);
            deckButtons[i].SetLabel(card == null ? "VAZIO" :
                $"{(selected ? "X " : string.Empty)}{card.DisplayName}\nN{card.Level}  {CompactCardStatus(card, weaponUI?.CurrentEnergy ?? 0)}");
            Image background = deckButtons[i].GetComponent<Image>();
            TextMeshProUGUI label = deckButtons[i].GetComponentInChildren<TextMeshProUGUI>();
            if (selected && background != null)
                background.color = new Color(0.01f, 0.01f, 0.012f, 0.98f);
            if (label != null)
                label.color = selected ? theme.Danger : theme.TextoPrincipal;
        }
    }

    public static bool IsCardInteractable(WDWeaponHudCardState card, bool canEdit) =>
        canEdit && card != null &&
        card.AvailabilityReason == WDWeaponAvailabilityReason.Available;

    public static bool ShouldAcceptInsufficientEnergyClick(
        WDWeaponHudCardState card, bool canEdit) =>
        canEdit && card != null &&
        card.AvailabilityReason == WDWeaponAvailabilityReason.InsufficientEnergy;

    public static string FormatCardStatus(WDWeaponHudCardState card, int currentEnergy)
    {
        if (card == null)
            return "VAZIO";
        if (card.Programmed)
            return $"PROGRAMADA  CD {Mathf.Max(1, card.CooldownRemaining)}";
        if (card.AvailabilityReason == WDWeaponAvailabilityReason.InitialRoundLock)
            return $"BLOQUEADA {card.CooldownRemaining}  |  E {card.EnergyCost}";
        if (card.AvailabilityReason == WDWeaponAvailabilityReason.Cooldown)
            return $"CD {card.CooldownRemaining}  |  E {card.EnergyCost}";
        if (card.AvailabilityReason == WDWeaponAvailabilityReason.InsufficientEnergy)
            return $"ENERGIA {currentEnergy}/{card.EnergyCost}";
        return $"E {card.EnergyCost}";
    }

    private static string CompactCardStatus(WDWeaponHudCardState card, int currentEnergy)
    {
        if (card == null) return string.Empty;
        if (card.Programmed) return "PRONTA";
        if (card.AvailabilityReason == WDWeaponAvailabilityReason.InitialRoundLock ||
            card.AvailabilityReason == WDWeaponAvailabilityReason.Cooldown)
            return $"CD {card.CooldownRemaining}";
        return $"E {currentEnergy}/{card.EnergyCost}";
    }

    public static float ActionsControlTop(bool usesTeams) => -803f;

    private static void SetLeftRailControlPosition(
        WDUIPremiumButton control, float top)
    {
        if (control == null)
            return;
        RectTransform rect = control.GetComponent<RectTransform>();
        if (rect != null)
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, top);
    }

    // ============================================================
    // 04. ATAQUE CONTEXTUAL: QUANTIDADE E ENVIO INDIVIDUAL
    // ============================================================

    private void BuildAttackBar()
    {
        attackBar = CreateNeutralPanel("AttackContextBar", new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(470f, 60f));
        HorizontalLayoutGroup row = attackBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.padding = new RectOffset(10, 10, 10, 10); row.spacing = 8f;
        row.childControlWidth = true; row.childForceExpandWidth = true;
        decreaseAttackButton = CreateButton(attackBar, "-", () => commands?.DiminuirQuantidadeAcao());
        attackAmountText = WDUIFactory.Text("AttackAmount", attackBar, theme, theme.TypeNumber,
            theme.TextoPrincipal, TextAlignmentOptions.Center);
        attackAmountText.gameObject.AddComponent<LayoutElement>().preferredWidth = 190f;
        increaseAttackButton = CreateButton(attackBar, "+", () => commands?.AumentarQuantidadeAcao());
        sendAttackButton = CreateButton(attackBar, "SEND", () => commands?.ConfirmarAcao());
        attackBar.gameObject.SetActive(false);
    }

    private void PresentAttackBar()
    {
        bool visible = state.PodeConfirmarAcao && state.ReforcosDisponiveis == 0 && !string.IsNullOrEmpty(state.DestinoSelecionadoId);
        attackBar.gameObject.SetActive(visible);
        if (!visible) return;
        attackAmountText.text = $"ATAQUE  {state.QuantidadeAcaoSelecionada}";
        decreaseAttackButton.SetInteractable(state.QuantidadeAcaoSelecionada > 1);
        increaseAttackButton.SetInteractable(true);
        sendAttackButton.SetInteractable(true);
    }

    // ============================================================
    // 05. DRAWER DE AÇÕES PREPARADAS
    // ============================================================

    private void BuildActionsDrawer()
    {
        actionsDismissLayer = WDUIFactory.Rect("ActionsDismissLayer", transform);
        Stretch(actionsDismissLayer);
        Image dismissImage = actionsDismissLayer.gameObject.AddComponent<Image>();
        dismissImage.color = Color.clear;
        dismissImage.raycastTarget = true;
        Button dismissButton = actionsDismissLayer.gameObject.AddComponent<Button>();
        dismissButton.transition = Selectable.Transition.None;
        dismissButton.onClick.AddListener(CloseActionsDrawer);
        actionsDismissLayer.gameObject.SetActive(false);

        actionsDrawer = CreateNeutralPanel("ActionsDrawer", new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(-396f, -205f), new Vector2(396f, 854f));
        CreateText(actionsDrawer, "Title", theme.TypeTitle, theme.TextoPrincipal,
            new Vector2(24f, -24f), new Vector2(284f, 36f)).text = "AÇÕES PROGRAMADAS";
        CreateCompactControl(actionsDrawer, "SupportToggle", ">", new Vector2(312f, -18f), ToggleSupportHub, 32f);
        CreateCompactControl(actionsDrawer, "Close", "‹", new Vector2(350f, -18f), ToggleActionsDrawer, 32f);
        actionsContent = WDUIFactory.Rect("Items", actionsDrawer);
        Stretch(actionsContent, 24f, 24f, 90f, 24f);
        VerticalLayoutGroup list = actionsContent.gameObject.AddComponent<VerticalLayoutGroup>();
        list.spacing = 10f; list.childControlHeight = true; list.childForceExpandHeight = false;
        actionsDrawer.anchoredPosition = new Vector2(-396f, -205f);
        actionsDrawer.gameObject.SetActive(false);

        supportHub = CreateNeutralPanel("SupportHub", new Vector2(0f, 1f),
            new Vector2(0f, 1f), Vector2.zero, Vector2.zero, transform);
        supportHub.anchorMin = supportHub.anchorMax = new Vector2(0f, 1f);
        supportHub.pivot = new Vector2(0f, 1f);
        supportHub.anchoredPosition = new Vector2(257f, -844f);
        supportHub.sizeDelta = new Vector2(348f, 210f);
        Image supportRoot = supportHub.GetComponent<Image>();
        if (supportRoot != null) supportRoot.color = Color.clear;
        Transform supportBorder = supportHub.Find("Border");
        if (supportBorder != null) supportBorder.gameObject.SetActive(false);
        CreateNeutralPanel("SupportPanel", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(48f, 0f), new Vector2(300f, 210f), supportHub);
        CreateText(supportHub, "Title", theme.TypeSection, theme.Acento,
            new Vector2(65f, -14f), new Vector2(218f, 34f)).text = "APOIO";
        CreateCompactControl(supportHub, "CollapseSupport", "‹", new Vector2(291f, -6f),
            ToggleSupportHub, 44f);
        supportHub.gameObject.SetActive(false);
    }

    private void ToggleSupportHub()
    {
        if (state?.Fase != GameManager.FaseTurno.Preparacao)
            return;
        supportHubOpen = !supportHubOpen;
        RebuildSupportHub();
        supportHub.gameObject.SetActive(supportHubOpen);
        if (supportHubOpen) supportHub.SetAsLastSibling();
    }

    private void RebuildSupportHub()
    {
        if (supportHub == null)
            return;
        for (int i = supportHub.childCount - 1; i >= 0; i--)
            if (supportHub.GetChild(i).name.StartsWith("SupportSlot_"))
                Destroy(supportHub.GetChild(i).gameObject);
        IReadOnlyList<WDWeaponHudCardState> cards = weaponUI?.GetSupportCards() ??
            Array.Empty<WDWeaponHudCardState>();
        for (int i = 0; i < 2; i++)
        {
            WDWeaponHudCardState card = i < cards.Count ? cards[i] : null;
            int slot = i;
            RectTransform item = WDUIFactory.Rect("SupportSlot_" + i, supportHub);
            item.anchorMin = item.anchorMax = new Vector2(0f, 1f);
            item.pivot = new Vector2(0f, 1f);
            item.anchoredPosition = new Vector2(65f + i * 138f, -63f);
            item.sizeDelta = new Vector2(126f, 121f);
            WDUIPremiumButton button = item.gameObject.AddComponent<WDUIPremiumButton>();
            button.Build(theme, card == null ? "SUPPORT SLOT\nVAZIO" :
                $"{card.DisplayName}\nN{card.Level}  {FormatCardStatus(card, weaponUI.CurrentEnergy)}",
                card == null ? null : () => ToggleSupportCard(slot));
            button.SetHoverFeedbackEnabled(false);
            button.SetPersistentColors(new Color(11f / 255f, 14f / 255f, 18f / 255f, 0.4f),
                new Color(FigmaGold.r, FigmaGold.g, FigmaGold.b, 0.22f));
            button.SetInteractable(card != null &&
                (IsCardInteractable(card, state?.PodeEditarPreparacao == true) || card.Selected ||
                 ShouldAcceptInsufficientEnergyClick(card, state?.PodeEditarPreparacao == true)));
            button.SetSelected(card?.Selected == true);
            RectTransform accent = WDUIFactory.Rect("Accent", item);
            accent.anchorMin = accent.anchorMax = new Vector2(0f, 1f);
            accent.pivot = new Vector2(0f, 1f);
            accent.anchoredPosition = new Vector2(11f, -11f);
            accent.sizeDelta = new Vector2(26f, 3f);
            Image accentImage = accent.gameObject.AddComponent<Image>();
            accentImage.color = new Color(56f / 255f, 181f / 255f, 204f / 255f);
            accentImage.raycastTarget = false;
        }
    }

    private void ToggleSupportCard(int slot)
    {
        IReadOnlyList<WDWeaponHudCardState> cards = weaponUI?.GetSupportCards();
        if (cards == null || slot < 0 || slot >= cards.Count)
            return;
        WDWeaponHudCardState card = cards[slot];
        if (card.Selected) weaponUI.CancelSelection();
        else if (weaponUI.SelectWeapon(card.WeaponId)) PresentWeaponSelectionPopupIfNeeded();
        RebuildSupportHub();
        PresentDeck();
    }

    private void RebuildActions()
    {
        ClearChildren(actionsContent);
        actionsContentKey = BuildActionsContentKey();
        bool readOnly = state.Fase == GameManager.FaseTurno.Resolucao;
        foreach (MatchUIReinforcementState reinforcement in state.Distribuicoes)
        {
            int id = reinforcement.Id;
            AddActionItem("REFORÇO", "+" + reinforcement.Quantidade,
                reinforcement.TerritorioId, string.Empty,
                readOnly ? null : () => commands?.RemoverDistribuicaoReforco(id));
        }
        foreach (MatchUIActionState action in state.Acoes)
        {
            int position = action.Posicao;
            AddActionItem(action.TipoEsperado.ToString().ToUpperInvariant(),
                "×" + action.Quantidade, action.OrigemId, action.DestinoId,
                readOnly ? null : () => commands?.RemoverAcao(position));
        }
        if (state.Transferencia != null)
            AddActionItem("TRANSFERÊNCIA", string.Empty,
                state.Transferencia.TerritorioId, string.Empty,
                readOnly ? null : () => commands?.RemoverTransferencia());
        if (weaponUI != null)
        {
            foreach (WDWeaponAction action in weaponUI.GetProgrammedActions())
            {
                WDWeaponAction captured = action;
                bool irreversible = weaponUI.IsIrreversible(action.ActionId);
                string result = readOnly ? $"  |  {weaponUI.GetResolutionStatus(action.ActionId)}" : string.Empty;
                AddActionItem("ARMA", action.WeaponId + result,
                    action.OriginTerritoryId, action.TargetTerritoryId,
                    irreversible || state.Fase == GameManager.FaseTurno.Resolucao
                        ? null : () => weaponUI.CancelAction(captured.ActionId));
                if (!readOnly && action.WeaponId == WDTacticalWeaponIds.Radar && !irreversible)
                    AddActionItem("USAR RADAR", string.Empty,
                        "Selecionar território", string.Empty, () =>
                        weaponUI.SelectProgrammedRadar(captured.ActionId));
            }
            foreach (WDWeaponHudInstallationState installation in weaponUI.GetVisibleInstallations()
                .Where(item => item.CanOperate))
            {
                string id = installation.InstallationId;
                AddActionItem(installation.DisplayName, "CARGAS " + installation.Charges,
                    installation.TerritoryId, string.Empty,
                    readOnly ? null : () => weaponUI.SelectInstallation(id));
            }
        }
        if (actionsContent.childCount == 0)
        {
            TextMeshProUGUI empty = WDUIFactory.Text("Empty", actionsContent, theme, theme.TypeBody,
                theme.TextoSecundario, TextAlignmentOptions.Center);
            empty.text = "Nenhuma ação preparada";
            empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 54f;
        }
    }

    private string BuildActionsContentKey()
    {
        if (state == null) return string.Empty;
        string key = $"{state.Distribuicoes.Count}|{state.Acoes.Count}|{state.Transferencia?.TerritorioId}";
        foreach (MatchUIReinforcementState item in state.Distribuicoes)
            key += $"|R:{item.Id}:{item.Quantidade}:{item.TerritorioId}";
        foreach (MatchUIActionState item in state.Acoes)
            key += $"|A:{item.Posicao}:{item.Quantidade}:{item.OrigemId}:{item.DestinoId}";
        if (weaponUI != null)
            foreach (WDWeaponAction item in weaponUI.GetProgrammedActions())
                key += $"|W:{item.ActionId}:{item.WeaponId}:{item.TargetTerritoryId}:" +
                    weaponUI.GetResolutionStatus(item.ActionId);
        return key;
    }

    private void AddActionItem(string actionType, string amount, string origin,
        string destination, UnityEngine.Events.UnityAction remove)
    {
        // 05.1. Item reutilizável: tipo/quantidade, rota e cancelamento.
        RectTransform item = WDUIFactory.Rect("Action", actionsContent);
        item.gameObject.AddComponent<LayoutElement>().preferredHeight = 76f;
        Image background = item.gameObject.AddComponent<Image>();
        background.color = WDMatchVisualStyle.SurfaceMatchControl;
        WDHomeUIFactory.ApplyRounded(background, 8);
        WDHomeUIFactory.AddRoundedBorder(item, WDMatchVisualStyle.BorderMatchGoldSoft, 8);
        TextMeshProUGUI heading = CreateText(item, "ActionType", theme.TypeBody,
            theme.TextoPrincipal, new Vector2(14f, -8f), new Vector2(250f, 26f));
        heading.text = string.IsNullOrWhiteSpace(amount) ? actionType : actionType + "  " + amount;
        TextMeshProUGUI route = CreateText(item, "Route", theme.TypeMicro,
            theme.TextoSecundario, new Vector2(14f, -37f), new Vector2(286f, 25f));
        route.text = string.IsNullOrWhiteSpace(destination)
            ? origin : origin + "  →  " + destination;
        if (remove != null)
            CreateMiniButton(item, "×", new Vector2(-10f, -16f), theme.Danger, remove);
    }

    // ============================================================
    // 06. JOGADORES E INTERAÇÕES CONTEXTUAIS
    // ============================================================

    private void RebuildPlayers(string contentKey)
    {
        ClearChildren(playersContent);
        playerAnchors.Clear();
        playersContentKey = contentKey;
        foreach (MatchUIPlayerState player in state.Jogadores)
        {
            RectTransform item = WDUIFactory.Rect("Player_" + player.Jogador, playersContent);
            bool compact = state.Jogadores.Count > 4;
            item.gameObject.AddComponent<LayoutElement>().preferredHeight = compact ? 58f : 65f;
            Image background = item.gameObject.AddComponent<Image>();
            background.color = Color.clear;
            playerAnchors[player.Jogador] = item;
            CreatePlayerAvatar(item, player, new Vector2(8f, 0f), new Vector2(42f, 48f));
            float nameY = state.UsaEquipes ? (compact ? -18f : -20f) : -18f;
            if (state.UsaEquipes)
            {
                TextMeshProUGUI team = CreateText(item, "Team", 10f,
                    theme.TextoSecundario, new Vector2(58f, compact ? -2f : -4f), new Vector2(108f, 15f));
                team.text = player.Equipe.ToString().ToUpperInvariant();
            }
            TextMeshProUGUI name = CreateText(item, "Name", theme.TypeBody, theme.TextoPrincipal,
                new Vector2(58f, nameY), new Vector2(112f, 28f));
            name.fontSize = 17f;
            name.enableAutoSizing = true;
            name.fontSizeMin = 12f;
            name.fontSizeMax = 17f;
            name.overflowMode = TextOverflowModes.Ellipsis;
            name.color = FigmaGold;
            name.text = player.Nome + (player.EhJogadorLocal ? "  VOCÊ" : string.Empty);
            TextMeshProUGUI meta = CreateText(item, "Meta", theme.TypeMicro, theme.TextoSecundario,
                new Vector2(58f, compact ? -39f : -43f), new Vector2(112f, 17f));
            meta.text = $"{player.TerritoriosControlados} TERRITÓRIOS";
            bool localEmoji = player.EhJogadorLocal &&
                state.Fase == GameManager.FaseTurno.Resolucao;
            bool eligibleTacticalAlly = !player.EhJogadorLocal &&
                state.Fase == GameManager.FaseTurno.Preparacao && state.UsaEquipes &&
                EquipesJogadores.SaoAliados(state.JogadorLocal, player.Jogador);
            if (localEmoji || eligibleTacticalAlly)
            {
                Button button = item.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.targetGraphic = background;
                background.raycastTarget = true;
                TerritorioClique.Dono captured = player.Jogador;
                if (localEmoji) button.onClick.AddListener(ShowEmojiDrawer);
                else button.onClick.AddListener(() => ShowTacticalPopup(captured));
            }
        }
    }

    private string BuildPlayersContentKey()
    {
        if (state == null)
            return string.Empty;

        string key = state.JogadorLocal + "|" + state.UsaEquipes;
        foreach (MatchUIPlayerState player in state.Jogadores)
        {
            key += $"|{player.Jogador}:{player.Nome}:{player.Equipe}:" +
                $"{player.TerritoriosControlados}:{player.EhJogadorLocal}:{player.Cor}";
        }
        return key;
    }

    // ============================================================
    // 07. POPUPS LOCAIS PROVISÓRIOS
    // ============================================================

    private void ShowMessagesPopup()
    {
        if (openChatHub != null) openChatHub();
        else ShowPopup("MENSAGENS", "Chat local temporariamente indisponível.");
    }

    private void RefreshChatAttention()
    {
        if (chatUnreadIndicator != null)
            chatUnreadIndicator.SetActive(chatRepository?.HasExternalAttention(matchChatChannelId) == true);
    }

    private void OnDestroy()
    {
        if (chatRepository != null)
            chatRepository.Changed -= RefreshChatAttention;
    }
    private void ShowMatchSettings()
    {
        ClearChildren(popupLayer);
        popupLayer.gameObject.SetActive(true);
        Image blocker = popupLayer.gameObject.GetComponent<Image>() ??
            popupLayer.gameObject.AddComponent<Image>();
        blocker.color = WDMatchVisualStyle.BackdropMatch;
        blocker.raycastTarget = true;
        RectTransform panel = CreateNeutralPanel("MatchSettingsPopup",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(640f, 430f), popupLayer);
        panel.GetComponent<Image>().color = WDMatchVisualStyle.SurfaceMatchModal;
        CreateText(panel, "Title", 28f, theme.TextoPrincipal,
            new Vector2(28f, -28f), new Vector2(520f, 42f)).text =
            "CONFIGURAÇÕES DA PARTIDA";
        CreateMiniButton(panel, "X", new Vector2(-24f, -24f), theme.Danger, ClosePopup);
        CreateSettingsRow(panel, "CONFIGURAÇÕES", "AJUSTES GLOBAIS", -108f);
        CreateSettingsRow(panel, "MANUAL", "REGRAS E REFERÊNCIA", -184f);
        RectTransform abandon = WDUIFactory.Rect("LeaveMatch", panel);
        abandon.anchorMin = abandon.anchorMax = new Vector2(0.5f, 0f);
        abandon.pivot = new Vector2(0.5f, 0f);
        abandon.anchoredPosition = new Vector2(0f, 32f);
        abandon.sizeDelta = new Vector2(310f, 52f);
        WDUIPremiumButton leaveButton = abandon.gameObject.AddComponent<WDUIPremiumButton>();
        leaveButton.Build(theme, "ABANDONAR PARTIDA", WDMatchSetupContext.ExitToHome);
        leaveButton.SetPersistentColors(new Color(0.24f, 0.05f, 0.05f, 0.96f),
            new Color(0.42f, 0.08f, 0.08f, 1f));
    }

    private void CreateSettingsRow(RectTransform panel, string title, string value, float top)
    {
        RectTransform row = WDUIFactory.Rect(title, panel);
        row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
        row.pivot = new Vector2(0f, 1f);
        row.anchoredPosition = new Vector2(28f, top);
        row.sizeDelta = new Vector2(584f, 60f);
        Image surface = row.gameObject.AddComponent<Image>();
        surface.color = WDMatchVisualStyle.SurfaceMatchControl;
        WDHomeUIFactory.ApplyRounded(surface, 8);
        CreateText(row, "Name", 16f, theme.TextoPrincipal,
            new Vector2(16f, -10f), new Vector2(220f, 40f)).text = title;
        TextMeshProUGUI valueText = CreateText(row, "Value", 14f, theme.TextoSecundario,
            new Vector2(250f, -10f), new Vector2(318f, 40f));
        valueText.alignment = TextAlignmentOptions.MidlineRight;
        valueText.text = value;
    }
    private void ShowEmojiDrawer()
    {
        if (state?.Fase != GameManager.FaseTurno.Resolucao)
            return;
        string[] emojis = { "😎", "😢", "😘", "✌", "😁", "😂", "👍" };
        BuildRadialHub("EmojiHub", emojis, new Vector2(-320f, 176f), index =>
        {
            TryShowSocialEmoji(emojis[index], WDSocialEmojiSource.Player);
            ClosePopup();
        }, 176f);
        RectTransform filter = WDUIFactory.Rect("EmojiFilter", popupLayer);
        filter.anchorMin = filter.anchorMax = new Vector2(1f, 0.5f);
        filter.pivot = new Vector2(0.5f, 0.5f);
        filter.anchoredPosition = new Vector2(-360f, -130f);
        filter.sizeDelta = new Vector2(100f, 52f);
        filter.gameObject.AddComponent<WDUIPremiumButton>().Build(theme,
            hideSocialEmojis ? "MOSTRAR\nEMOJIS" : "OCULTAR\nEMOJIS", ToggleSocialEmojiFilter);
    }

    private void ShowTacticalPopup(TerritorioClique.Dono ally)
    {
        if (state?.Fase != GameManager.FaseTurno.Preparacao || !state.UsaEquipes ||
            ally == state.JogadorLocal ||
            !EquipesJogadores.SaoAliados(state.JogadorLocal, ally))
            return;
        if (informationalOverlay?.IsSelectingTacticalTerritory == true)
        {
            informationalOverlay.CancelTacticalSelection();
            return;
        }
        string[] labels = { "ATACAR", "DEFENDER", "TRANSFERIR", "ATENÇÃO", "FOCO" };
        // 06.1. A camada inativa não possui geometria de canvas confiável.
        // Ativá-la antes da conversão restaura o hub no avatar realmente tocado.
        popupLayer.gameObject.SetActive(true);
        popupLayer.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        Vector2 origin = ResolvePlayerHubOrigin(ally);
        BuildRadialHub("TacticalHub", labels, origin, index =>
        {
            informationalOverlay?.BeginTacticalPing(ally, (WDTacticalPingKind)index);
            ClosePopup();
        }, 174f);
    }

    private Vector2 ResolvePlayerHubOrigin(TerritorioClique.Dono player)
    {
        if (!playerAnchors.TryGetValue(player, out RectTransform source))
            return new Vector2(-320f, 31f);
        Bounds relative = RectTransformUtility.CalculateRelativeRectTransformBounds(
            popupLayer, source);
        Vector2 center = relative.center;
        float halfWidth = popupLayer.rect.width * 0.5f;
        float halfHeight = popupLayer.rect.height * 0.5f;
        center.x = Mathf.Clamp(center.x - 220f, -halfWidth + 280f, halfWidth - 280f);
        center.y = Mathf.Clamp(center.y, -halfHeight + 325f, halfHeight - 325f);
        return center;
    }

    private void BuildRadialHub(string name, string[] labels, Vector2 origin,
        System.Action<int> onSelect, float radius)
    {
        ClearChildren(popupLayer);
        popupLayer.gameObject.SetActive(true);
        Image blocker = popupLayer.gameObject.GetComponent<Image>() ??
            popupLayer.gameObject.AddComponent<Image>();
        blocker.color = Color.clear;
        blocker.raycastTarget = true;
        Button dismiss = popupLayer.gameObject.GetComponent<Button>() ??
            popupLayer.gameObject.AddComponent<Button>();
        dismiss.transition = Selectable.Transition.None;
        dismiss.onClick.RemoveAllListeners();
        dismiss.onClick.AddListener(ClosePopup);

        RectTransform hub = WDUIFactory.Rect(name, popupLayer);
        hub.anchorMin = hub.anchorMax = new Vector2(1f, 0.5f);
        hub.pivot = new Vector2(0.5f, 0.5f);
        hub.anchoredPosition = origin;
        hub.sizeDelta = new Vector2(560f, 650f);

        for (int index = 0; index < labels.Length; index++)
        {
            float progress = labels.Length == 1 ? 0.5f : index / (labels.Length - 1f);
            float angle = Mathf.Lerp(115f, 245f, progress);
            float radians = angle * Mathf.Deg2Rad;
            RectTransform segment = WDUIFactory.Rect($"Segment_{index + 1}", hub);
            segment.anchorMin = segment.anchorMax = new Vector2(0.5f, 0.5f);
            segment.pivot = new Vector2(0.5f, 0.5f);
            segment.anchoredPosition = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
            segment.sizeDelta = new Vector2(175f, 132f);
            segment.localEulerAngles = new Vector3(0f, 0f, angle - 180f);
            Image segmentImage = segment.gameObject.AddComponent<Image>();
            string segmentSet = name == "EmojiHub" ? "EmojiSegment" : "TacticalSegment";
            segmentImage.sprite = CreateRuntimeSprite(Resources.Load<Texture2D>(
                $"UI/Match/FigmaCurrent/Hubs/{segmentSet}{index + 1}"));
            segmentImage.preserveAspect = true;
            segmentImage.color = Color.white;

            Button button = segment.gameObject.AddComponent<Button>();
            button.targetGraphic = segmentImage;
            int captured = index;
            button.onClick.AddListener(() => onSelect?.Invoke(captured));

            if (name == "EmojiHub")
            {
                RectTransform emoji = WDUIFactory.Rect("Emoji", segment);
                emoji.anchorMin = emoji.anchorMax = new Vector2(0.5f, 0.5f);
                emoji.pivot = new Vector2(0.5f, 0.5f);
                emoji.sizeDelta = new Vector2(48f, 48f);
                emoji.localEulerAngles = new Vector3(0f, 0f, 180f - angle);
                Image emojiImage = emoji.gameObject.AddComponent<Image>();
                emojiImage.sprite = CreateRuntimeSprite(Resources.Load<Texture2D>(
                    $"UI/Match/FigmaCurrent/Hubs/Emoji{index + 1}"));
                emojiImage.preserveAspect = true;
                emojiImage.raycastTarget = false;
            }
            else
            {
                TextMeshProUGUI label = WDUIFactory.Text("Label", segment, theme,
                    15f, theme.TextoPrincipal, TextAlignmentOptions.Center);
                RectTransform labelRect = label.rectTransform;
                labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
                labelRect.pivot = new Vector2(0.5f, 0.5f);
                labelRect.anchoredPosition = Vector2.zero;
                labelRect.sizeDelta = new Vector2(112f, 66f);
                labelRect.localEulerAngles = new Vector3(0f, 0f, 180f - angle);
                label.text = labels[index];
                label.raycastTarget = false;
            }
        }

        RectTransform avatar = WDUIFactory.Rect("PlayerAvatar", hub);
        avatar.anchorMin = avatar.anchorMax = new Vector2(0.5f, 0.5f);
        avatar.pivot = new Vector2(0.5f, 0.5f);
        avatar.sizeDelta = new Vector2(201f, 201f);
        Image avatarImage = avatar.gameObject.AddComponent<Image>();
        avatarImage.sprite = CreateRuntimeSprite(Resources.Load<Texture2D>(
            "UI/Match/FigmaCurrent/Hubs/HubAvatar"));
        avatarImage.preserveAspect = true;
        avatarImage.raycastTarget = false;
    }

    private static Sprite CreateRuntimeSprite(Texture2D texture)
    {
        return texture == null ? null : Sprite.Create(texture,
            new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    // ============================================================
    // 08. SELEÇÃO REUTILIZÁVEL DO DESTINATÁRIO DA TRANSFERÊNCIA
    // ============================================================

    private void ConfirmTransferRecipient() => ShowTransferRecipientPopup();

    private void ShowTransferRecipientPopup()
    {
        ClearChildren(popupLayer);
        popupLayer.gameObject.SetActive(true);
        Image blocker = popupLayer.gameObject.GetComponent<Image>() ??
            popupLayer.gameObject.AddComponent<Image>();
        blocker.color = WDMatchVisualStyle.BackdropMatch;
        blocker.raycastTarget = true;

        RectTransform panel = CreateNeutralPanel(
            "TransferRecipientPopup", new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(620f, 420f), popupLayer);
        CreateText(panel, "Title", theme.TypeTitle, theme.TextoPrincipal,
            new Vector2(24f, -24f), new Vector2(520f, 42f)).text =
            "TRANSFERIR PARA QUAL ALIADO?";
        CreateMiniButton(panel, "X", new Vector2(-54f, -20f),
            theme.Danger, CancelTransferPopup);

        RectTransform options = WDUIFactory.Rect("Recipients", panel);
        options.anchorMin = new Vector2(0.08f, 0.14f);
        options.anchorMax = new Vector2(0.92f, 0.72f);
        options.offsetMin = options.offsetMax = Vector2.zero;
        VerticalLayoutGroup layout = options.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        foreach (TerritorioClique.Dono recipient in state.DestinatariosTransferencia)
        {
            bool found = TryFindPlayer(recipient, out MatchUIPlayerState player);
            string label = found
                ? $"{player.Nome}  |  {player.Equipe}"
                : recipient.ToString();
            TerritorioClique.Dono captured = recipient;
            WDUIPremiumButton button = CreateButton(options, label, () =>
            {
                commands?.SelecionarDestinatarioTransferencia(captured);
                ClosePopup();
            });
            button.GetComponent<LayoutElement>().preferredHeight = 64f;
            if (found)
                button.SetPersistentColors(
                    Color.Lerp(player.Cor, Color.black, 0.45f),
                    Color.Lerp(player.Cor, Color.white, 0.12f));
        }
    }

    private bool TryFindPlayer(
        TerritorioClique.Dono owner,
        out MatchUIPlayerState result)
    {
        if (state != null)
            foreach (MatchUIPlayerState player in state.Jogadores)
                if (player.Jogador == owner)
                {
                    result = player;
                    return true;
                }

        result = default;
        return false;
    }

    private void ShowMatchInfo()
    {
        ClearChildren(popupLayer);
        popupLayer.gameObject.SetActive(true);
        Image blocker = popupLayer.gameObject.GetComponent<Image>() ??
            popupLayer.gameObject.AddComponent<Image>();
        blocker.color = WDMatchVisualStyle.BackdropMatch;
        blocker.raycastTarget = true;
        RectTransform panel = CreateNeutralPanel("PlayersPopup", new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 620f), popupLayer);
        panel.GetComponent<Image>().color = WDMatchVisualStyle.SurfaceMatchModal;
        CreateText(panel, "Title", theme.TypeTitle, theme.TextoPrincipal,
            new Vector2(24f, -20f), new Vector2(820f, 42f)).text =
            $"JOGADORES  |  ROUND {state?.Round ?? 0}";
        CreateMiniButton(panel, "X", new Vector2(-54f, -20f), theme.Danger, ClosePopup);
        RectTransform content = WDUIFactory.Rect("PlayerCards", panel);
        Stretch(content, 24f, 24f, 82f, 24f);
        VerticalLayoutGroup list = content.gameObject.AddComponent<VerticalLayoutGroup>();
        list.spacing = 10f; list.childControlHeight = true; list.childForceExpandHeight = false;
        if (state == null) return;
        foreach (MatchUIPlayerState player in state.Jogadores)
            CreatePlayerInformationCard(content, player);
    }

    private void CreatePlayerInformationCard(Transform parent, MatchUIPlayerState player)
    {
        RectTransform card = WDUIFactory.Rect("PlayerInfo_" + player.Jogador, parent);
        card.gameObject.AddComponent<LayoutElement>().preferredHeight = 108f;
        Image background = card.gameObject.AddComponent<Image>();
        background.color = WDMatchVisualStyle.SurfaceMatchElevated;
        WDHomeUIFactory.ApplyRounded(background, 10);
        WDHomeUIFactory.AddRoundedBorder(card, WDMatchVisualStyle.BorderMatchGoldSoft, 10);

        // 08.1. O mesmo placeholder de avatar abastece HUD e PlayerRoundCard.
        CreatePlayerAvatar(card, player, new Vector2(14f, 0f), new Vector2(64f, 64f));
        string team = state.UsaEquipes ? $"  |  {player.Equipe}" : string.Empty;
        CreateText(card, "Identity", theme.TypeBody, theme.TextoPrincipal,
            new Vector2(92f, -10f), new Vector2(226f, 42f)).text =
            $"{player.Nome}{team}\nPATENTE  --";
        int totalTroops = state.Territorios
            .Where(item => item.Proprietario == player.Jogador).Sum(item => item.Tropas);
        int available = player.EhJogadorLocal ? state.ReforcosDisponiveis : 0;
        string playerId = state == null ? string.Empty :
            weaponUI?.PlayerIdForOwner(player.Jogador) ?? string.Empty;

        // 08.2. Stats são itens reutilizáveis e independentes; recebem ícone
        // oficial assim que os vetores isolados forem exportados do Figma.
        CreatePlayerStat(card, "Territories", "TERRITÓRIOS", player.TerritoriosControlados,
            new Vector2(330f, -8f));
        CreatePlayerStat(card, "Troops", "TROPAS", totalTroops,
            new Vector2(438f, -8f));
        CreatePlayerStat(card, "Available", "DISPONÍVEIS", available,
            new Vector2(526f, -8f));
        CreatePlayerStat(card, "Energy", "ENERGIA", weaponUI?.GetPlayerEnergy(playerId) ?? 0,
            new Vector2(638f, -8f));

        // 08.3. Oito slots compactos preservam a leitura do deck sem caixas pesadas.
        RectTransform deck = WDUIFactory.Rect("Deck", card);
        deck.anchorMin = deck.anchorMax = new Vector2(0f, 1f);
        deck.pivot = new Vector2(0f, 1f); deck.anchoredPosition = new Vector2(330f, -48f);
        deck.sizeDelta = new Vector2(576f, 46f);
        GridLayoutGroup grid = deck.gameObject.AddComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 8;
        grid.cellSize = new Vector2(66f, 42f); grid.spacing = new Vector2(6f, 0f);
        IReadOnlyList<string> cards = weaponUI?.GetDeckCardLabels(playerId) ??
            System.Array.Empty<string>();
        for (int i = 0; i < 8; i++)
        {
            RectTransform slot = WDUIFactory.Rect("Card_" + (i + 1), deck);
            Image slotBackground = slot.gameObject.AddComponent<Image>();
            slotBackground.color = WDMatchVisualStyle.SurfaceMatchControl;
            WDHomeUIFactory.ApplyRounded(slotBackground, 6);
            WDHomeUIFactory.AddRoundedBorder(slot, WDMatchVisualStyle.BorderMatchGoldSoft, 6);
            TextMeshProUGUI label = WDUIFactory.Text("Label", slot, theme, theme.TypeMicro,
                theme.TextoPrincipal, TextAlignmentOptions.Center);
            Stretch(label.rectTransform, 3f, 3f, 3f, 3f);
            label.text = i < cards.Count ? cards[i] : "--";
            label.textWrappingMode = TextWrappingModes.Normal;
        }
    }

    private Image CreatePlayerAvatar(Transform parent, MatchUIPlayerState player,
        Vector2 position, Vector2 size)
    {
        RectTransform avatarRect = WDUIFactory.Rect("Avatar", parent);
        avatarRect.anchorMin = avatarRect.anchorMax = new Vector2(0f, 0.5f);
        avatarRect.pivot = new Vector2(0f, 0.5f);
        avatarRect.anchoredPosition = position;
        avatarRect.sizeDelta = size;
        Image avatar = avatarRect.gameObject.AddComponent<Image>();
        int playerIndex = Mathf.Clamp((int)player.Jogador, 1, 4);
        avatar.sprite = CreateRuntimeSprite(Resources.Load<Texture2D>(
            "UI/Match/FigmaCurrent/Player" + playerIndex));
        avatar.color = Color.white;
        avatar.preserveAspect = true;
        avatar.raycastTarget = false;
        return avatar;
    }

    private void CreatePlayerStat(Transform parent, string name, string caption,
        int value, Vector2 position)
    {
        RectTransform stat = WDUIFactory.Rect(name, parent);
        stat.anchorMin = stat.anchorMax = new Vector2(0f, 1f);
        stat.pivot = new Vector2(0f, 1f);
        stat.anchoredPosition = position;
        stat.sizeDelta = new Vector2(100f, 34f);
        TextMeshProUGUI captionText = WDUIFactory.Text("Caption", stat, theme,
            9f, theme.TextoSecundario, TextAlignmentOptions.Left);
        captionText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        captionText.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        captionText.rectTransform.pivot = new Vector2(0f, 0.5f);
        captionText.rectTransform.anchoredPosition = Vector2.zero;
        captionText.rectTransform.sizeDelta = new Vector2(74f, 22f);
        captionText.text = caption;
        TextMeshProUGUI valueText = WDUIFactory.Text("Value", stat, theme,
            16f, theme.TextoPrincipal, TextAlignmentOptions.Right);
        valueText.rectTransform.anchorMin = new Vector2(1f, 0.5f);
        valueText.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        valueText.rectTransform.pivot = new Vector2(1f, 0.5f);
        valueText.rectTransform.anchoredPosition = Vector2.zero;
        valueText.rectTransform.sizeDelta = new Vector2(24f, 24f);
        valueText.text = value.ToString();
    }

    private void ToggleRegions()
    {
        informationalOverlay?.ToggleRegions();
        regionsButton?.SetSelected(informationalOverlay?.RegionsActive == true);
    }

    private void ShowPopup(string title, string body, string actionLabel = null,
        UnityEngine.Events.UnityAction action = null,
        UnityEngine.Events.UnityAction closeAction = null)
    {
        ClearChildren(popupLayer); popupLayer.gameObject.SetActive(true);
        Image blocker = popupLayer.gameObject.GetComponent<Image>() ?? popupLayer.gameObject.AddComponent<Image>();
        blocker.color = WDMatchVisualStyle.BackdropMatch; blocker.raycastTarget = true;
        RectTransform panel = CreateNeutralPanel("Popup", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(500f, 320f), popupLayer);
        CreateText(panel, "Title", theme.TypeTitle, theme.TextoPrincipal,
            new Vector2(24f, -24f), new Vector2(400f, 42f)).text = title;
        CreateMiniButton(panel, "X", new Vector2(-54f, -20f), theme.Danger,
            closeAction ?? ClosePopup);
        TextMeshProUGUI bodyText = CreateText(panel, "Body", theme.TypeBody, theme.TextoSecundario,
            new Vector2(24f, -82f), new Vector2(452f, 150f));
        bodyText.text = body; bodyText.textWrappingMode = TextWrappingModes.Normal;
        if (!string.IsNullOrEmpty(actionLabel))
        {
            RectTransform actionRect = WDUIFactory.Rect("PopupAction", panel);
            actionRect.anchorMin = actionRect.anchorMax = new Vector2(0.5f, 0f);
            actionRect.pivot = new Vector2(0.5f, 0f); actionRect.anchoredPosition = new Vector2(0f, 24f);
            actionRect.sizeDelta = new Vector2(280f, 48f);
            actionRect.gameObject.AddComponent<WDUIPremiumButton>().Build(theme, actionLabel, action);
        }
    }

    private void ClosePopup() => popupLayer.gameObject.SetActive(false);

    public void CancelWeaponSelectionAndClosePopup()
    {
        weaponUI?.CancelSelection();
        ClosePopup();
    }

    private void CancelTransferPopup()
    {
        commands?.SelecionarModoAcao();
        ClosePopup();
    }

    // ============================================================
    // 08. FILTRO LOCAL DE RECEPÇÃO DE EMOJIS SOCIAIS
    // ============================================================

    public bool TryShowSocialEmoji(string emoji, WDSocialEmojiSource source)
    {
        if (hideSocialEmojis || string.IsNullOrWhiteSpace(emoji))
            return false;

        if (emojiToastAnimation != null)
            StopCoroutine(emojiToastAnimation);

        socialEmojiToast.text = source == WDSocialEmojiSource.Spectator
            ? $"ESPECTADOR  {emoji}"
            : emoji;
        socialEmojiToast.gameObject.SetActive(true);
        emojiToastAnimation = StartCoroutine(HideSocialEmojiToast());
        return true;
    }

    private void ToggleSocialEmojiFilter()
    {
        hideSocialEmojis = !hideSocialEmojis;
        ClosePopup();
    }

    private void SendLocalTestEmoji()
    {
        ClosePopup();
        TryShowSocialEmoji(":)", WDSocialEmojiSource.Player);
    }

    private void BuildSocialEmojiToast()
    {
        socialEmojiToast = WDUIFactory.Text(
            "SocialEmojiToast", transform, theme, theme.TypeTitle,
            theme.TextoPrincipal, TextAlignmentOptions.Center);
        RectTransform rect = socialEmojiToast.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.82f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(320f, 72f);
        socialEmojiToast.gameObject.SetActive(false);
    }

    private IEnumerator HideSocialEmojiToast()
    {
        yield return new WaitForSecondsRealtime(2f);
        socialEmojiToast.gameObject.SetActive(false);
        emojiToastAnimation = null;
    }

    // ============================================================
    // 09. COMANDOS LOCAIS E AUTORITATIVOS
    // ============================================================

    private void ToggleCard(int slot)
    {
        IReadOnlyList<WDWeaponHudCardState> cards = weaponUI?.GetCards();
        if (cards == null || slot < 0 || slot >= cards.Count)
            return;
        if (cards[slot].Selected)
            weaponUI.CancelSelection();
        else if (weaponUI.SelectWeapon(cards[slot].WeaponId))
            PresentWeaponSelectionPopupIfNeeded();
        else if (cards[slot].AvailabilityReason ==
            WDWeaponAvailabilityReason.InsufficientEnergy)
            deckButtons[slot].PlayDeniedFeedback();
        PresentDeck();
    }

    private void PresentWeaponSelectionPopupIfNeeded()
    {
        if (weaponUI.Stage == WDWeaponHudSelectionStage.TargetPlayer)
            ShowWeaponPlayerPopup();
        else if (weaponUI.Stage == WDWeaponHudSelectionStage.Parameters)
            ShowTransportPopup();
        else if (weaponUI.Stage == WDWeaponHudSelectionStage.RadarConfirmation)
            ShowRadarConfirmation();
    }

    private void PresentWeaponModalIfNeeded()
    {
        if (weaponUI == null || popupLayer.gameObject.activeSelf)
            return;
        PresentWeaponSelectionPopupIfNeeded();
    }

    private void ShowTransportPopup()
    {
        ClearChildren(popupLayer); popupLayer.gameObject.SetActive(true);
        Image blocker = popupLayer.gameObject.GetComponent<Image>() ?? popupLayer.gameObject.AddComponent<Image>();
        blocker.color = WDMatchVisualStyle.BackdropMatch; blocker.raycastTarget = true;
        RectTransform panel = CreateNeutralPanel("TransportPopup", new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 460f), popupLayer);
        CreateText(panel, "Title", theme.TypeTitle, theme.TextoPrincipal,
            new Vector2(24f, -24f), new Vector2(640f, 42f)).text = "TRANSPORTE AÉREO";
        CreateMiniButton(panel, "X", new Vector2(-54f, -20f), theme.Danger,
            CancelWeaponSelectionAndClosePopup);
        RectTransform options = WDUIFactory.Rect("Options", panel);
        Stretch(options, 24f, 24f, 100f, 36f);
        VerticalLayoutGroup layout = options.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f; layout.childControlHeight = true; layout.childForceExpandHeight = false;
        RectTransform amount = WDUIFactory.Rect("Amount", options);
        amount.gameObject.AddComponent<LayoutElement>().preferredHeight = 58f;
        HorizontalLayoutGroup amountLayout = amount.gameObject.AddComponent<HorizontalLayoutGroup>();
        amountLayout.spacing = 8f; amountLayout.childControlWidth = true;
        amountLayout.childForceExpandWidth = true;
        CreateButton(amount, "-", () => { weaponUI.ChangeTransportTroops(-1); ShowTransportPopup(); });
        CreateButton(amount, $"TROPAS  {weaponUI.TransportTroops} / {weaponUI.MaximumTransportTroops}", null);
        CreateButton(amount, "+", () => { weaponUI.ChangeTransportTroops(1); ShowTransportPopup(); });
        CreateButton(options, $"CONFIRMAR {weaponUI.TransportTroops} — SEM INSTALAÇÃO", () =>
        {
            weaponUI.ConfirmTransport(weaponUI.TransportTroops, string.Empty); ClosePopup();
        }).GetComponent<LayoutElement>().preferredHeight = 58f;
        foreach (WDWeaponHudInstallationState installation in weaponUI.GetVisibleInstallations()
            .Where(item => item.OwnerPlayerId == weaponUI.LocalPlayerId &&
                item.TerritoryId == weaponUI.CurrentOriginId &&
                (item.WeaponId == WDSupportWeaponIds.FlakTower ||
                 item.WeaponId == WDStrategicWeaponIds.EnergyPlant ||
                 item.WeaponId == WDStrategicWeaponIds.Artillery ||
                 item.WeaponId == WDStrategicWeaponIds.Rockets ||
                 item.WeaponId == WDSpecialWeaponIds.Drone ||
                 item.WeaponId == WDTacticalWeaponIds.Radar)))
        {
            string id = installation.InstallationId;
            CreateButton(options, $"{weaponUI.TransportTroops} TROPAS + {installation.DisplayName}", () =>
            {
                weaponUI.ConfirmTransport(weaponUI.TransportTroops, id); ClosePopup();
            }).GetComponent<LayoutElement>().preferredHeight = 58f;
        }
    }

    private void ShowRadarConfirmation()
    {
        ShowPopup("RADAR — USO IRREVERSÍVEL",
            "A varredura revela somente a existência de cobertura Flak neste território.",
            "CONFIRMAR", () => { weaponUI.ConfirmRadarScan(); ClosePopup(); },
            CancelWeaponSelectionAndClosePopup);
    }

    private void ShowWeaponPlayerPopup()
    {
        ClearChildren(popupLayer); popupLayer.gameObject.SetActive(true);
        Image blocker = popupLayer.gameObject.GetComponent<Image>() ?? popupLayer.gameObject.AddComponent<Image>();
        blocker.color = WDMatchVisualStyle.BackdropMatch; blocker.raycastTarget = true;
        RectTransform panel = CreateNeutralPanel("WeaponPlayerPopup", new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(780f, 540f), popupLayer);
        CreateText(panel, "Title", theme.TypeTitle, theme.TextoPrincipal,
            new Vector2(24f, -24f), new Vector2(780f, 42f)).text = "SELECIONE O ADVERSÁRIO";
        CreateMiniButton(panel, "X", new Vector2(-54f, -20f), theme.Danger,
            CancelWeaponSelectionAndClosePopup);
        RectTransform options = WDUIFactory.Rect("Options", panel);
        Stretch(options, 24f, 24f, 90f, 24f);
        VerticalLayoutGroup layout = options.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f; layout.childControlHeight = true; layout.childForceExpandHeight = false;
        foreach (WDMatchParticipant opponent in weaponUI.GetOpponents())
        {
            string id = opponent.PlayerId;
            WDUIPremiumButton button = CreateButton(options,
                $"{opponent.Nickname}\n{weaponUI.DescribeDeck(id)}", () =>
                {
                    weaponUI.SelectTargetPlayer(id);
                    if (weaponUI.Stage == WDWeaponHudSelectionStage.TargetWeapon)
                        ShowInfiltratorWeaponPopup(id);
                    else ClosePopup();
                });
            button.GetComponent<LayoutElement>().preferredHeight = 82f;
        }
    }

    private void ShowInfiltratorWeaponPopup(string playerId)
    {
        ClearChildren(popupLayer);
        RectTransform panel = CreateNeutralPanel("InfiltratorPopup", new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980f, 380f), popupLayer);
        CreateText(panel, "Title", theme.TypeTitle, theme.TextoPrincipal,
            new Vector2(24f, -24f), new Vector2(980f, 42f)).text = "INFILTRADO — ESCOLHA UMA ARMA";
        CreateMiniButton(panel, "X", new Vector2(-54f, -20f), theme.Danger,
            CancelWeaponSelectionAndClosePopup);
        RectTransform options = WDUIFactory.Rect("Weapons", panel);
        Stretch(options, 24f, 24f, 100f, 40f);
        GridLayoutGroup grid = options.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(245f, 82f); grid.spacing = new Vector2(10f, 10f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 4;
        foreach (WDInfiltratorWeaponTarget target in weaponUI.GetInfiltratorWeapons(playerId))
        {
            string weaponId = target.WeaponId;
            CreateButton(options, $"{target.DisplayName}\nN{target.Level}", () =>
            {
                weaponUI.SelectInfiltratorWeapon(weaponId); ClosePopup();
            });
        }
    }

    private void ToggleResolutionSpeed()
    {
        if (resolutionSequence == null) return;
        resolutionSequence.PlaybackSpeed = resolutionSequence.PlaybackSpeed > 1f ? 1f : 2f;
        speedButton.SetLabel(resolutionSequence.PlaybackSpeed > 1f ? ">>" : ">");
    }
    private void ToggleTransfer()
    {
        if (state == null) return;
        if (state.EmModoTransferencia) commands?.SelecionarModoAcao();
        else
        {
            commands?.SelecionarModoTransferencia();
            if (state.DestinatariosTransferencia.Count > 1)
                ShowTransferRecipientPopup();
        }
    }
    private void ToggleActionsDrawer()
    {
        // 09. O estado real da preparação bloqueia antes de ativar Canvas/animação.
        if (!actionsOpen && (state == null || !state.PodeEditarPreparacao))
            return;
        actionsOpen = !actionsOpen;
        if (drawerAnimation != null) StopCoroutine(drawerAnimation);
        if (actionsOpen && state != null) RebuildActions();
        drawerAnimation = StartCoroutine(AnimateActionsDrawer(actionsOpen));
    }

    private void CloseActionsDrawer()
    {
        if (!actionsOpen)
            return;

        actionsOpen = false;
        if (drawerAnimation != null)
            StopCoroutine(drawerAnimation);
        drawerAnimation = StartCoroutine(AnimateActionsDrawer(false));
    }

    private void CloseActionsDrawerImmediately()
    {
        if (!actionsOpen && !actionsDrawer.gameObject.activeSelf) return;
        if (drawerAnimation != null)
        {
            StopCoroutine(drawerAnimation);
            drawerAnimation = null;
        }
        actionsOpen = false;
        supportHubOpen = false;
        supportHub?.gameObject.SetActive(false);
        actionsDismissLayer.gameObject.SetActive(false);
        actionsDrawer.anchoredPosition = new Vector2(-396f, -205f);
        actionsDrawer.gameObject.SetActive(false);
    }

    private IEnumerator AnimateActionsDrawer(bool opening)
    {
        actionsDismissLayer.gameObject.SetActive(opening || actionsOpen);
        actionsDrawer.gameObject.SetActive(true);
        float start = actionsDrawer.anchoredPosition.x;
        float target = opening ? 295f : -396f;
        float elapsed = 0f;
        float duration = theme != null ? theme.ActionsDrawerDuration : 0.20f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            Vector2 position = actionsDrawer.anchoredPosition;
            position.x = Mathf.Lerp(start, target, progress);
            actionsDrawer.anchoredPosition = position;
            yield return null;
        }
        if (!opening)
        {
            supportHubOpen = false;
            supportHub?.gameObject.SetActive(false);
            actionsDrawer.gameObject.SetActive(false);
            actionsDismissLayer.gameObject.SetActive(false);
        }
        drawerAnimation = null;
    }

    public void ShowInstallationHub(WDWeaponHudInstallationState installation)
    {
        if (installation == null)
            return;
        ClearChildren(popupLayer);
        popupLayer.gameObject.SetActive(true);
        Image blocker = popupLayer.gameObject.GetComponent<Image>() ??
            popupLayer.gameObject.AddComponent<Image>();
        blocker.color = Color.clear;
        blocker.raycastTarget = true;
        Button dismiss = popupLayer.gameObject.GetComponent<Button>() ??
            popupLayer.gameObject.AddComponent<Button>();
        dismiss.transition = Selectable.Transition.None;
        dismiss.onClick.RemoveAllListeners();
        dismiss.onClick.AddListener(ClosePopup);

        RectTransform hub = WDUIFactory.Rect("InstallationHub", popupLayer);
        hub.anchorMin = hub.anchorMax = new Vector2(0.5f, 0.5f);
        hub.pivot = new Vector2(0.5f, 0.5f);
        hub.sizeDelta = new Vector2(333.9f, 414.2f);
        Image hubImage = hub.gameObject.AddComponent<Image>();
        hubImage.sprite = CreateRuntimeSprite(Resources.Load<Texture2D>(InstallationAssetPath(installation.WeaponId)));
        hubImage.preserveAspect = true;
        hubImage.raycastTarget = true;

        Button operate = hub.gameObject.AddComponent<Button>();
        operate.targetGraphic = hubImage;
        operate.transition = Selectable.Transition.ColorTint;
        operate.interactable = installation.CanOperate;
        if (installation.CanOperate)
            operate.onClick.AddListener(() =>
            {
                weaponUI.SelectInstallation(installation.InstallationId);
                ClosePopup();
            });
    }

    private static string InstallationAssetPath(string weaponId)
    {
        string asset = weaponId switch
        {
            WDSupportWeaponIds.Bunker => "Bunker",
            WDSupportWeaponIds.Minefield => "Minefield",
            WDSupportWeaponIds.FlakTower => "FlakTower",
            WDStrategicWeaponIds.EnergyPlant => "PowerPlant",
            WDStrategicWeaponIds.Artillery => "Artillery",
            WDStrategicWeaponIds.Rockets => "Rockets",
            WDSpecialWeaponIds.Drone => "Drone",
            _ => "Bunker"
        };
        return "UI/Match/Figma/Installations/" + asset;
    }
    private void FinishRound()
    {
        if (state == null) return;
        if (state.AcoesEnviadas) commands?.CancelarEnvio(); else commands?.EnviarAcoes();
    }

    // ============================================================
    // 10. FÁBRICA VISUAL PROVISÓRIA
    // ============================================================

    private TextMeshProUGUI CreateIndicator(Transform parent, string label)
    {
        RectTransform rect = WDUIFactory.Rect(label, parent);
        Image background = rect.gameObject.AddComponent<Image>();
        background.color = Color.clear;
        background.raycastTarget = false;
        TextMeshProUGUI text = WDUIFactory.Text("Value", rect, theme, 15f,
            theme.TextoPrincipal, TextAlignmentOptions.Center);
        Stretch(text.rectTransform); text.text = label + "  0"; return text;
    }

    private TextMeshProUGUI CreateResourceIndicator(Transform parent, string name,
        string resourcePath)
    {
        RectTransform item = WDUIFactory.Rect(name, parent);
        HorizontalLayoutGroup row = item.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 6f;
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = false;
        row.childControlHeight = false;

        RectTransform icon = WDUIFactory.Rect("FigmaIcon", item);
        icon.sizeDelta = new Vector2(18f, 18f);
        Image image = icon.gameObject.AddComponent<Image>();
        image.sprite = CreateRuntimeSprite(Resources.Load<Texture2D>(resourcePath));
        image.preserveAspect = true;
        image.raycastTarget = false;

        TextMeshProUGUI text = WDUIFactory.Text("Value", item, theme, 15f,
            theme.TextoPrincipal, TextAlignmentOptions.Center);
        text.rectTransform.sizeDelta = new Vector2(100f, 28f);
        text.text = name.ToUpperInvariant() + "  0";
        text.raycastTarget = false;
        return text;
    }

    private WDUIPremiumButton CreateCompactControl(Transform parent, string name, string label,
        Vector2 position, UnityEngine.Events.UnityAction action, float width = 268f,
        float height = 46f)
    {
        RectTransform rect = WDUIFactory.Rect(name, parent);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f); rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position; rect.sizeDelta = new Vector2(width, height);
        WDUIPremiumButton button = rect.gameObject.AddComponent<WDUIPremiumButton>();
        button.Build(theme, label, action);
        button.SetPersistentColors(WDMatchVisualStyle.SurfaceMatchControl,
            new Color(WDMatchVisualStyle.BorderMatchGold.r,
                WDMatchVisualStyle.BorderMatchGold.g,
                WDMatchVisualStyle.BorderMatchGold.b, 0.28f));
        Image surface = rect.GetComponent<Image>();
        WDHomeUIFactory.ApplyRounded(surface, 6);
        return button;
    }

    private void CreateMiniButton(Transform parent, string label, Vector2 position,
        Color color, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = WDUIFactory.Rect(label, parent);
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f); rect.pivot = new Vector2(1f, 1f);
        bool compactDismiss = label == "X" || label == "×";
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(compactDismiss ? 44f : 78f, 44f);
        WDUIPremiumButton button = rect.gameObject.AddComponent<WDUIPremiumButton>();
        button.Build(theme, label, action);
        button.SetPersistentColors(color, Color.Lerp(color, Color.white, 0.16f));
    }

    private WDUIPremiumButton CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = WDUIFactory.Rect(label.Replace("\n", "_"), parent);
        WDUIPremiumButton button = rect.gameObject.AddComponent<WDUIPremiumButton>();
        button.Build(theme, label, action); return button;
    }

    private WDUIPremiumButton CreateIconButton(Transform parent, string name,
        string resourcePath, UnityEngine.Events.UnityAction action, float iconSize = 40f)
    {
        WDUIPremiumButton button = CreateButton(parent, string.Empty, action);
        button.gameObject.name = name;
        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
            return button;
        RectTransform icon = WDUIFactory.Rect("FigmaIcon", button.transform);
        icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
        icon.pivot = new Vector2(0.5f, 0.5f);
        icon.sizeDelta = new Vector2(iconSize, iconSize);
        Image image = icon.gameObject.AddComponent<Image>();
        image.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f), 100f);
        image.preserveAspect = true;
        image.raycastTarget = false;
        return button;
    }

    private RectTransform CreateNeutralPanel(string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 position, Vector2 size, Transform parent = null)
    {
        RectTransform rect = WDUIFactory.Rect(name, parent ?? transform);
        rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(anchorMin.x, anchorMax.y); rect.anchoredPosition = position; rect.sizeDelta = size;
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = WDMatchVisualStyle.SurfaceMatchBase;
        image.raycastTarget = false;
        WDHomeUIFactory.ApplyRounded(image, 12);
        WDHomeUIFactory.AddRoundedBorder(rect, WDMatchVisualStyle.BorderMatchGold, 12);
        return rect;
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, float size, Color color,
        Vector2 position, Vector2 dimensions)
    {
        TextMeshProUGUI text = WDUIFactory.Text(name, parent, theme, size, color);
        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f); rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position; rect.sizeDelta = dimensions; return text;
    }

    private static void AnchorTop(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f); rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
    }

    private static void Stretch(RectTransform rect, float left = 0f, float right = 0f,
        float top = 0f, float bottom = 0f)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top);
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--) Destroy(parent.GetChild(i).gameObject);
    }
}

// ============================================================
// 11. ORIGENS FUTURAS COMPATÍVEIS DO EMOJI SOCIAL
// ============================================================

public enum WDSocialEmojiSource
{
    Player,
    Spectator
}
