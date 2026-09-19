using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WarDominionMatchHUD : MonoBehaviour
{
    // ============================================================
    // 01. ESTADO E REFERÊNCIAS DA COMPOSIÇÃO
    // ============================================================

    private WarDominionUITheme theme;
    private IMatchUICommands commands;
    private WDWeaponMatchUIController weaponUI;
    private ResolutionSequenceController resolutionSequence;
    private WDMatchInformationalOverlayController informationalOverlay;
    private MatchUIState state;
    private TextMeshProUGUI energyText, troopsText, timerText, roundText, phaseText, attackAmountText;
    private RectTransform playersContent, actionsContent, actionsDrawer, actionsDismissLayer, attackBar, popupLayer;
    private TextMeshProUGUI socialEmojiToast;
    private WDUIPremiumButton transferButton, confirmTransferButton, finishRoundButton,
        decreaseAttackButton, increaseAttackButton, sendAttackButton, actionsButton;
    private WDUIPremiumButton regionsButton;
    private WDUIPremiumButton speedButton;
    private readonly List<WDUIPremiumButton> deckButtons = new List<WDUIPremiumButton>();
    private bool actionsOpen;
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
        RectTransform left = CreateNeutralPanel("LeftCommandRail", Vector2.zero,
            new Vector2(0f, 1f), Vector2.zero, new Vector2(300f, 0f));
        RectTransform right = CreateNeutralPanel("RightCommandRail", new Vector2(1f, 0f),
            Vector2.one, Vector2.zero, new Vector2(300f, 0f));
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

    private void BuildLeftRail(RectTransform rail)
    {
        RectTransform indicators = WDUIFactory.Rect("RoundResources", rail);
        AnchorTop(indicators, 14f, 14f, 272f, 72f);
        HorizontalLayoutGroup row = indicators.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 6f; row.childControlWidth = true; row.childForceExpandWidth = true;
        energyText = CreateIndicator(indicators, "ENERGIA", "E");
        troopsText = CreateIndicator(indicators, "TROPAS", "T");
        timerText = CreateIndicator(indicators, "TEMPO", "@");
        RectTransform navigation = WDUIFactory.Rect("PrimaryNavigation", rail);
        AnchorTop(navigation, 14f, 94f, 272f, 48f);
        HorizontalLayoutGroup navigationRow = navigation.gameObject.AddComponent<HorizontalLayoutGroup>();
        navigationRow.spacing = 6f; navigationRow.childControlWidth = true;
        navigationRow.childForceExpandWidth = true;
        CreateButton(navigation, "MENU", ShowMatchInfo);
        regionsButton = CreateButton(navigation, "REGIÕES", ToggleRegions);
        roundText = CreateText(rail, "Round", theme.TypeTitle, theme.TextoPrincipal,
            new Vector2(16f, -150f), new Vector2(268f, 32f));
        phaseText = CreateText(rail, "Phase", theme.TypeMicro, theme.Acento,
            new Vector2(16f, -180f), new Vector2(268f, 22f));
        CreateText(rail, "DeckTitle", theme.TypeSection, theme.TextoSecundario,
            new Vector2(16f, -210f), new Vector2(268f, 24f)).text = "DECK  8 SLOTS";
        RectTransform deck = WDUIFactory.Rect("Deck", rail);
        AnchorTop(deck, 16f, 240f, 268f, 356f);
        GridLayoutGroup grid = deck.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(128f, 80f); grid.spacing = new Vector2(8f, 8f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 2;
        for (int i = 0; i < 8; i++)
        {
            int slot = i;
            WDUIPremiumButton card = CreateButton(deck,
                $"SLOT {i + 1}", () => ToggleCard(slot));
            card.SetHoverFeedbackEnabled(false);
            card.GetComponent<LayoutElement>().preferredHeight = 80f;
            deckButtons.Add(card);
        }
        transferButton = CreateCompactControl(rail, "TransferControl", "< >  TRANSFERIR",
            new Vector2(16f, -616f), ToggleTransfer);
        confirmTransferButton = CreateCompactControl(rail, "ConfirmTransferControl",
            "CONFIRMAR TRANSFERÊNCIA", new Vector2(16f, -672f),
            ConfirmTransferRecipient);
        actionsButton = CreateCompactControl(rail, "ActionsControl", "=  AÇÕES",
            new Vector2(16f, -672f), ToggleActionsDrawer);
    }

    private void BuildRightRail(RectTransform rail)
    {
        RectTransform tools = WDUIFactory.Rect("UtilityControls", rail);
        AnchorTop(tools, 14f, 14f, 272f, 48f);
        HorizontalLayoutGroup row = tools.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 6f; row.childControlWidth = true; row.childForceExpandWidth = true;
        WDUIPremiumButton chatButton = CreateButton(tools, "MENSAGENS", ShowMessagesPopup);
        RectTransform unread = WDUIFactory.Rect("UnreadIndicator", chatButton.transform);
        unread.anchorMin = unread.anchorMax = new Vector2(1f, 1f);
        unread.pivot = new Vector2(1f, 1f);
        unread.anchoredPosition = new Vector2(-4f, -4f);
        unread.sizeDelta = new Vector2(14f, 14f);
        Image unreadImage = unread.gameObject.AddComponent<Image>();
        unreadImage.color = theme.Danger;
        unreadImage.raycastTarget = false;
        chatUnreadIndicator = unread.gameObject;
        RefreshChatAttention();
        CreateButton(tools, "CONFIGURAÇÕES", ShowMatchSettings);
        CreateText(rail, "PlayersTitle", theme.TypeSection, theme.TextoSecundario,
            new Vector2(16f, -84f), new Vector2(268f, 24f)).text = "ORDEM DO ROUND";
        playersContent = WDUIFactory.Rect("Players", rail);
        AnchorTop(playersContent, 14f, 116f, 272f, 560f);
        VerticalLayoutGroup list = playersContent.gameObject.AddComponent<VerticalLayoutGroup>();
        list.spacing = 8f; list.childControlHeight = true; list.childForceExpandHeight = false;
        finishRoundButton = CreateCompactControl(rail, "FinishRound", ">>  FINALIZAR ROUND",
            new Vector2(16f, -704f), FinishRound);
        speedButton = CreateCompactControl(rail, "ResolutionSpeed", "NORMAL",
            new Vector2(16f, -704f), ToggleResolutionSpeed);
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
        energyText.text = $"E\n{weaponUI?.CurrentEnergy ?? 0}";
        troopsText.text = $"T\n{state.ReforcosDisponiveis}";
        int seconds = Mathf.CeilToInt(state.TempoRestante);
        timerText.text = $"@\n{seconds / 60:00}:{seconds % 60:00}";
        roundText.text = state.EmMorteSubita ? $"MORTE SÚBITA {state.RoundMorteSubita}" : $"ROUND {state.Round:00}";
        phaseText.text = state.EmSelecaoInicial
            ? state.FeedbackSelecaoInicial
            : state.Fase.ToString().ToUpperInvariant();
        transferButton.SetInteractable(state.PodeEditarPreparacao && state.TransferenciaDisponivel && !state.TransferenciaUsada);
        transferButton.gameObject.SetActive(state.UsaEquipes);
        SetLeftRailControlPosition(actionsButton, ActionsControlTop(state.UsaEquipes));
        transferButton.SetSelected(state.EmModoTransferencia);
        transferButton.SetLabel(state.EmModoTransferencia ? "X  CANCELAR TRANSFERIR" : "< >  TRANSFERIR");
        confirmTransferButton.gameObject.SetActive(false);
        finishRoundButton.SetInteractable(
            state.AcoesEnviadas
                ? state.PodeCancelarEnvio
                : state.PodeEnviarPreparacao && state.ReforcosDisponiveis == 0);
        finishRoundButton.SetLabel(state.AcoesEnviadas
            ? "AÇÕES ENVIADAS"
            : ">>  FINALIZAR ROUND");
        finishRoundButton.gameObject.SetActive(state.Fase == GameManager.FaseTurno.Preparacao);
        speedButton.gameObject.SetActive(state.Fase == GameManager.FaseTurno.Resolucao);
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
                $"{(selected ? "X  " : string.Empty)}{card.DisplayName}\nN{card.Level}  {status}");
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

    public static float ActionsControlTop(bool usesTeams) => usesTeams ? -672f : -616f;

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
            new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(560f, 72f));
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

        actionsDrawer = CreateNeutralPanel("ActionsDrawer", Vector2.zero, new Vector2(0f, 1f),
            new Vector2(300f, 0f), new Vector2(390f, 0f));
        CreateText(actionsDrawer, "Title", theme.TypeTitle, theme.TextoPrincipal,
            new Vector2(18f, -18f), new Vector2(250f, 34f)).text = "AÇÕES";
        CreateCompactControl(actionsDrawer, "Close", "X", new Vector2(326f, -14f), ToggleActionsDrawer, 48f);
        actionsContent = WDUIFactory.Rect("Items", actionsDrawer);
        Stretch(actionsContent, 14f, 14f, 70f, 14f);
        VerticalLayoutGroup list = actionsContent.gameObject.AddComponent<VerticalLayoutGroup>();
        list.spacing = 8f; list.childControlHeight = true; list.childForceExpandHeight = false;
        actionsDrawer.anchoredPosition = new Vector2(-90f, 0f);
        actionsDrawer.gameObject.SetActive(false);
    }

    private void RebuildActions()
    {
        ClearChildren(actionsContent);
        actionsContentKey = BuildActionsContentKey();
        bool readOnly = state.Fase == GameManager.FaseTurno.Resolucao;
        foreach (MatchUIReinforcementState reinforcement in state.Distribuicoes)
        {
            int id = reinforcement.Id;
            AddActionItem($"REFORÇO  +{reinforcement.Quantidade}\n{reinforcement.TerritorioId}",
                readOnly ? null : () => commands?.RemoverDistribuicaoReforco(id));
        }
        foreach (MatchUIActionState action in state.Acoes)
        {
            int position = action.Posicao;
            AddActionItem($"{action.TipoEsperado}  x{action.Quantidade}\n{action.OrigemId} > {action.DestinoId}",
                readOnly ? null : () => commands?.RemoverAcao(position));
        }
        if (state.Transferencia != null)
            AddActionItem($"TRANSFERÊNCIA\n{state.Transferencia.TerritorioId}",
                readOnly ? null : () => commands?.RemoverTransferencia());
        if (weaponUI != null)
        {
            foreach (WDWeaponAction action in weaponUI.GetProgrammedActions())
            {
                WDWeaponAction captured = action;
                bool irreversible = weaponUI.IsIrreversible(action.ActionId);
                string result = readOnly ? $"  |  {weaponUI.GetResolutionStatus(action.ActionId)}" : string.Empty;
                AddActionItem($"ARMA  {action.WeaponId}{result}\n{action.OriginTerritoryId} > {action.TargetTerritoryId}",
                    irreversible || state.Fase == GameManager.FaseTurno.Resolucao
                        ? null : () => weaponUI.CancelAction(captured.ActionId));
                if (!readOnly && action.WeaponId == WDTacticalWeaponIds.Radar && !irreversible)
                    AddActionItem("USAR RADAR\nSelecionar território", () =>
                        weaponUI.SelectProgrammedRadar(captured.ActionId));
            }
            foreach (WDWeaponHudInstallationState installation in weaponUI.GetVisibleInstallations()
                .Where(item => item.CanOperate))
            {
                string id = installation.InstallationId;
                AddActionItem($"{installation.DisplayName}\n{installation.TerritoryId}  CARGAS {installation.Charges}",
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

    private void AddActionItem(string summary, UnityEngine.Events.UnityAction remove)
    {
        RectTransform item = WDUIFactory.Rect("Action", actionsContent);
        item.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;
        Image background = item.gameObject.AddComponent<Image>();
        background.color = new Color(0.025f, 0.025f, 0.03f, 0.96f);
        TextMeshProUGUI text = CreateText(item, "Summary", theme.TypeMicro, theme.TextoPrincipal,
            new Vector2(10f, -8f), new Vector2(230f, 48f));
        text.text = summary; text.textWrappingMode = TextWrappingModes.Normal;
        if (remove != null)
            CreateMiniButton(item, "X", new Vector2(-42f, -10f), theme.Danger, remove);
    }

    // ============================================================
    // 06. JOGADORES E INTERAÇÕES CONTEXTUAIS
    // ============================================================

    private void RebuildPlayers(string contentKey)
    {
        ClearChildren(playersContent);
        playersContentKey = contentKey;
        foreach (MatchUIPlayerState player in state.Jogadores)
        {
            RectTransform item = WDUIFactory.Rect("Player_" + player.Jogador, playersContent);
            bool compact = state.Jogadores.Count > 4;
            item.gameObject.AddComponent<LayoutElement>().preferredHeight = compact ? 64f : 82f;
            Image background = item.gameObject.AddComponent<Image>();
            background.color = new Color(0.02f, 0.02f, 0.025f, 0.96f);
            Image marker = WDUIFactory.Rect("Color", item).gameObject.AddComponent<Image>();
            marker.color = player.Cor; marker.raycastTarget = false;
            RectTransform markerRect = marker.rectTransform;
            markerRect.anchorMin = new Vector2(0f, 0f); markerRect.anchorMax = new Vector2(0f, 1f);
            markerRect.sizeDelta = new Vector2(7f, 0f); markerRect.anchoredPosition = new Vector2(3.5f, 0f);
            float nameY = state.UsaEquipes ? (compact ? -22f : -28f) : -12f;
            if (state.UsaEquipes)
            {
                TextMeshProUGUI team = CreateText(item, "Team", theme.TypeMicro,
                    theme.TextoSecundario, new Vector2(18f, compact ? -3f : -7f), new Vector2(230f, 18f));
                team.text = player.Equipe.ToString().ToUpperInvariant();
            }
            TextMeshProUGUI name = CreateText(item, "Name", theme.TypeBody, theme.TextoPrincipal,
                new Vector2(18f, nameY), new Vector2(230f, 24f));
            name.text = player.Nome + (player.EhJogadorLocal ? "  VOCÊ" : string.Empty);
            TextMeshProUGUI meta = CreateText(item, "Meta", theme.TypeMicro, theme.TextoSecundario,
                new Vector2(18f, compact ? -39f : -46f), new Vector2(230f, 20f));
            meta.text = $"{player.TerritoriosControlados} TERRITÓRIOS";
            if (player.EhJogadorLocal ||
                state.UsaEquipes && EquipesJogadores.SaoAliados(state.JogadorLocal, player.Jogador))
            {
                Button button = item.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.targetGraphic = background;
                background.raycastTarget = true;
                TerritorioClique.Dono captured = player.Jogador;
                if (player.EhJogadorLocal) button.onClick.AddListener(ShowEmojiDrawer);
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
    private void ShowMatchSettings() => ShowPopup("CONFIGURAÇÕES DA PARTIDA",
        "CONFIGURAÇÕES\n\nMANUAL", "ABANDONAR PARTIDA", WDMatchSetupContext.ExitToHome);
    private void ShowEmojiDrawer()
    {
        if (state?.Fase != GameManager.FaseTurno.Resolucao)
            return;
        ClearChildren(popupLayer);
        popupLayer.gameObject.SetActive(true);
        RectTransform panel = CreateNeutralPanel("EmojiDrawer", new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 360f), popupLayer);
        CreateText(panel, "Title", theme.TypeTitle, theme.TextoPrincipal,
            new Vector2(24f, -24f), new Vector2(450f, 42f)).text = "EMOJIS RÁPIDOS";
        CreateMiniButton(panel, "X", new Vector2(-54f, -20f), theme.Danger, ClosePopup);
        CreateText(panel, "State", theme.TypeBody, theme.TextoSecundario,
            new Vector2(24f, -92f), new Vector2(500f, 40f)).text = hideSocialEmojis
                ? "Emojis recebidos: OCULTOS" : "Emojis recebidos: VISÍVEIS";
        RectTransform actions = WDUIFactory.Rect("EmojiActions", panel);
        actions.anchorMin = new Vector2(0.08f, 0.12f);
        actions.anchorMax = new Vector2(0.92f, 0.60f);
        actions.offsetMin = actions.offsetMax = Vector2.zero;
        VerticalLayoutGroup layout = actions.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f; layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        WDUIPremiumButton send = CreateButton(actions, ":)  ENVIAR EMOJI", SendLocalTestEmoji);
        send.GetComponent<LayoutElement>().preferredHeight = 56f;
        WDUIPremiumButton filter = CreateButton(actions,
            hideSocialEmojis ? "HABILITAR EMOJIS RECEBIDOS" : "OCULTAR EMOJIS RECEBIDOS",
            ToggleSocialEmojiFilter);
        filter.GetComponent<LayoutElement>().preferredHeight = 56f;
    }

    private void ShowTacticalPopup(TerritorioClique.Dono ally)
    {
        if (state?.Fase != GameManager.FaseTurno.Preparacao)
            return;
        if (informationalOverlay?.IsSelectingTacticalTerritory == true)
        {
            informationalOverlay.CancelTacticalSelection();
            return;
        }
        ClearChildren(popupLayer);
        popupLayer.gameObject.SetActive(true);
        Image blocker = popupLayer.gameObject.GetComponent<Image>() ??
            popupLayer.gameObject.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0.18f);
        blocker.raycastTarget = true;
        Button dismiss = popupLayer.gameObject.GetComponent<Button>() ??
            popupLayer.gameObject.AddComponent<Button>();
        dismiss.transition = Selectable.Transition.None;
        dismiss.onClick.RemoveAllListeners();
        dismiss.onClick.AddListener(() =>
        {
            informationalOverlay?.CancelTacticalSelection();
            ClosePopup();
        });
        RectTransform panel = CreateNeutralPanel("TacticalRadial", new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f), new Vector2(-312f, 60f), new Vector2(480f, 420f), popupLayer);
        CreateText(panel, "Title", theme.TypeSection, theme.TextoPrincipal,
            new Vector2(90f, -20f), new Vector2(300f, 36f)).text = "COMUNICAÇÃO TÁTICA";
        string[] labels = { "!  ATACAR", "#  DEFENDER", ">  TRANSFERIR", "?  ATENÇÃO", "+  FOCO" };
        for (int i = 0; i < labels.Length; i++)
        {
            int captured = i;
            float angle = Mathf.Lerp(120f, 240f, i / 4f) * Mathf.Deg2Rad;
            Vector2 position = new Vector2(220f + Mathf.Cos(angle) * 150f,
                -205f - Mathf.Sin(angle) * 150f);
            CreateCompactControl(panel, "Tactical_" + i, labels[i], position,
                () => { informationalOverlay?.BeginTacticalPing(ally,
                    (WDTacticalPingKind)captured); ClosePopup(); }, 180f);
        }
        CreateMiniButton(panel, "X", new Vector2(-54f, -20f), theme.Danger, ClosePopup);
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
        blocker.color = new Color(0f, 0f, 0f, 0.66f);
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
        blocker.color = new Color(0f, 0f, 0f, 0.66f);
        blocker.raycastTarget = true;
        RectTransform panel = CreateNeutralPanel("PlayersPopup", new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180f, 700f), popupLayer);
        CreateText(panel, "Title", theme.TypeTitle, theme.TextoPrincipal,
            new Vector2(24f, -20f), new Vector2(1040f, 42f)).text =
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
        card.gameObject.AddComponent<LayoutElement>().preferredHeight = 118f;
        Image background = card.gameObject.AddComponent<Image>();
        background.color = new Color(0.02f, 0.02f, 0.025f, 0.98f);
        Image avatar = WDUIFactory.Rect("Avatar", card).gameObject.AddComponent<Image>();
        avatar.color = player.Cor;
        avatar.rectTransform.anchorMin = avatar.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        avatar.rectTransform.pivot = new Vector2(0f, 0.5f);
        avatar.rectTransform.anchoredPosition = new Vector2(14f, 0f);
        avatar.rectTransform.sizeDelta = new Vector2(72f, 72f);
        string team = state.UsaEquipes ? $"  |  {player.Equipe}" : string.Empty;
        CreateText(card, "Identity", theme.TypeBody, theme.TextoPrincipal,
            new Vector2(100f, -10f), new Vector2(250f, 42f)).text =
            $"{player.Nome}{team}\nPATENTE  --";
        int totalTroops = state.Territorios
            .Where(item => item.Proprietario == player.Jogador).Sum(item => item.Tropas);
        int available = player.EhJogadorLocal ? state.ReforcosDisponiveis : 0;
        string playerId = state == null ? string.Empty :
            weaponUI?.PlayerIdForOwner(player.Jogador) ?? string.Empty;
        CreateText(card, "Indicators", theme.TypeMicro, theme.TextoSecundario,
            new Vector2(360f, -10f), new Vector2(780f, 24f)).text =
            $"TERRITÓRIOS {player.TerritoriosControlados}   TROPAS {totalTroops}   " +
            $"DISPONÍVEIS {available}   ENERGY {weaponUI?.GetPlayerEnergy(playerId) ?? 0}";
        RectTransform deck = WDUIFactory.Rect("Deck", card);
        deck.anchorMin = deck.anchorMax = new Vector2(0f, 1f);
        deck.pivot = new Vector2(0f, 1f); deck.anchoredPosition = new Vector2(360f, -42f);
        deck.sizeDelta = new Vector2(790f, 62f);
        GridLayoutGroup grid = deck.gameObject.AddComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 8;
        grid.cellSize = new Vector2(91f, 54f); grid.spacing = new Vector2(7f, 0f);
        IReadOnlyList<string> cards = weaponUI?.GetDeckCardLabels(playerId) ??
            System.Array.Empty<string>();
        for (int i = 0; i < 8; i++)
        {
            RectTransform slot = WDUIFactory.Rect("Card_" + (i + 1), deck);
            Image slotBackground = slot.gameObject.AddComponent<Image>();
            slotBackground.color = new Color(0.045f, 0.045f, 0.055f, 1f);
            TextMeshProUGUI label = WDUIFactory.Text("Label", slot, theme, theme.TypeMicro,
                theme.TextoPrincipal, TextAlignmentOptions.Center);
            Stretch(label.rectTransform, 3f, 3f, 3f, 3f);
            label.text = i < cards.Count ? cards[i] : "--";
            label.textWrappingMode = TextWrappingModes.Normal;
        }
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
        blocker.color = new Color(0f, 0f, 0f, 0.66f); blocker.raycastTarget = true;
        RectTransform panel = CreateNeutralPanel("Popup", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(620f, 560f), popupLayer);
        CreateText(panel, "Title", theme.TypeTitle, theme.TextoPrincipal,
            new Vector2(24f, -24f), new Vector2(520f, 42f)).text = title;
        CreateMiniButton(panel, "X", new Vector2(-54f, -20f), theme.Danger,
            closeAction ?? ClosePopup);
        TextMeshProUGUI bodyText = CreateText(panel, "Body", theme.TypeBody, theme.TextoSecundario,
            new Vector2(24f, -90f), new Vector2(572f, 380f));
        bodyText.text = body; bodyText.textWrappingMode = TextWrappingModes.Normal;
        if (!string.IsNullOrEmpty(actionLabel))
        {
            RectTransform actionRect = WDUIFactory.Rect("PopupAction", panel);
            actionRect.anchorMin = actionRect.anchorMax = new Vector2(0.5f, 0f);
            actionRect.pivot = new Vector2(0.5f, 0f); actionRect.anchoredPosition = new Vector2(0f, 24f);
            actionRect.sizeDelta = new Vector2(300f, 52f);
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
        blocker.color = new Color(0f, 0f, 0f, 0.66f); blocker.raycastTarget = true;
        RectTransform panel = CreateNeutralPanel("TransportPopup", new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 520f), popupLayer);
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
        blocker.color = new Color(0f, 0f, 0f, 0.66f); blocker.raycastTarget = true;
        RectTransform panel = CreateNeutralPanel("WeaponPlayerPopup", new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 620f), popupLayer);
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
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 420f), popupLayer);
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
        speedButton.SetLabel(resolutionSequence.PlaybackSpeed > 1f ? "ACELERADO >>" : "NORMAL");
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
        actionsDismissLayer.gameObject.SetActive(false);
        actionsDrawer.anchoredPosition = new Vector2(-90f, 0f);
        actionsDrawer.gameObject.SetActive(false);
    }

    private IEnumerator AnimateActionsDrawer(bool opening)
    {
        actionsDismissLayer.gameObject.SetActive(opening || actionsOpen);
        actionsDrawer.gameObject.SetActive(true);
        float start = actionsDrawer.anchoredPosition.x;
        float target = opening ? 300f : -90f;
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
            actionsDrawer.gameObject.SetActive(false);
            actionsDismissLayer.gameObject.SetActive(false);
        }
        drawerAnimation = null;
    }
    private void FinishRound()
    {
        if (state == null) return;
        if (state.AcoesEnviadas) commands?.CancelarEnvio(); else commands?.EnviarAcoes();
    }

    // ============================================================
    // 10. FÁBRICA VISUAL PROVISÓRIA
    // ============================================================

    private TextMeshProUGUI CreateIndicator(Transform parent, string label, string icon)
    {
        RectTransform rect = WDUIFactory.Rect(label, parent);
        Image background = rect.gameObject.AddComponent<Image>();
        background.color = new Color(0.025f, 0.025f, 0.03f, 0.95f);
        TextMeshProUGUI text = WDUIFactory.Text("Value", rect, theme, theme.TypeMicro,
            theme.TextoPrincipal, TextAlignmentOptions.Center);
        Stretch(text.rectTransform); text.text = icon + "\n0"; return text;
    }

    private WDUIPremiumButton CreateCompactControl(Transform parent, string name, string label,
        Vector2 position, UnityEngine.Events.UnityAction action, float width = 268f)
    {
        RectTransform rect = WDUIFactory.Rect(name, parent);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f); rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position; rect.sizeDelta = new Vector2(width, 46f);
        WDUIPremiumButton button = rect.gameObject.AddComponent<WDUIPremiumButton>();
        button.Build(theme, label, action); return button;
    }

    private void CreateMiniButton(Transform parent, string label, Vector2 position,
        Color color, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = WDUIFactory.Rect(label, parent);
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f); rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = position; rect.sizeDelta = new Vector2(label == "X" ? 36f : 78f, 36f);
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

    private RectTransform CreateNeutralPanel(string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 position, Vector2 size, Transform parent = null)
    {
        RectTransform rect = WDUIFactory.Rect(name, parent ?? transform);
        rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(anchorMin.x, anchorMax.y); rect.anchoredPosition = position; rect.sizeDelta = size;
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.006f, 0.006f, 0.009f, 0.95f); image.raycastTarget = false;
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
