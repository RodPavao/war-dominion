using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WarDominionMatchHUD : MonoBehaviour
{
    // ============================================================
    // 01. ESTADO E REFERÊNCIAS DA COMPOSIÇÃO
    // ============================================================

    private const int EnergiaMock = 100;
    private static readonly int[] CustosDeckMock = { 15, 20, 25, 30, 35, 40, 45, 50 };
    private WarDominionUITheme theme;
    private IMatchUICommands commands;
    private MatchUIState state;
    private TextMeshProUGUI energyText, troopsText, timerText, roundText, phaseText, attackAmountText;
    private RectTransform playersContent, actionsContent, actionsDrawer, actionsDismissLayer, attackBar, popupLayer;
    private TextMeshProUGUI socialEmojiToast;
    private WDUIPremiumButton transferButton, confirmTransferButton, finishRoundButton,
        decreaseAttackButton, increaseAttackButton, sendAttackButton;
    private WDUIPremiumButton socialEmojiFilterButton;
    private readonly List<WDUIPremiumButton> deckButtons = new List<WDUIPremiumButton>();
    private bool actionsOpen;
    private bool hideSocialEmojis;
    private int selectedCard = -1;
    private Coroutine drawerAnimation;
    private Coroutine emojiToastAnimation;
    private string actionsContentKey = string.Empty;
    private string playersContentKey = string.Empty;
    private int lastPresentedRound = -1;

    // ============================================================
    // 02. COMPOSIÇÃO LATERAL, SEM PAINEL INFERIOR PERMANENTE
    // ============================================================

    public void Build(WarDominionUITheme newTheme, IMatchUICommands newCommands)
    {
        theme = newTheme;
        commands = newCommands;
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
        roundText = CreateText(rail, "Round", theme.TypeTitle, theme.TextoPrincipal,
            new Vector2(16f, -98f), new Vector2(268f, 32f));
        phaseText = CreateText(rail, "Phase", theme.TypeMicro, theme.Acento,
            new Vector2(16f, -128f), new Vector2(268f, 22f));
        CreateText(rail, "DeckTitle", theme.TypeSection, theme.TextoSecundario,
            new Vector2(16f, -164f), new Vector2(268f, 24f)).text = "DECK  8 SLOTS";
        RectTransform deck = WDUIFactory.Rect("Deck", rail);
        AnchorTop(deck, 16f, 194f, 268f, 356f);
        GridLayoutGroup grid = deck.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(128f, 80f); grid.spacing = new Vector2(8f, 8f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 2;
        for (int i = 0; i < 8; i++)
        {
            int slot = i;
            WDUIPremiumButton card = CreateButton(deck,
                $"CARTA {i + 1}\nE {CustosDeckMock[i]}  CD --", () => ToggleCard(slot));
            card.GetComponent<LayoutElement>().preferredHeight = 80f;
            deckButtons.Add(card);
        }
        transferButton = CreateCompactControl(rail, "TransferControl", "< >  TRANSFERIR",
            new Vector2(16f, -570f), ToggleTransfer);
        confirmTransferButton = CreateCompactControl(rail, "ConfirmTransferControl",
            "CONFIRMAR TRANSFERÊNCIA", new Vector2(16f, -626f),
            ConfirmTransferRecipient);
        CreateCompactControl(rail, "ActionsControl", "=  AÇÕES",
            new Vector2(16f, -682f), ToggleActionsDrawer);
    }

    private void BuildRightRail(RectTransform rail)
    {
        RectTransform tools = WDUIFactory.Rect("UtilityControls", rail);
        AnchorTop(tools, 14f, 14f, 272f, 48f);
        HorizontalLayoutGroup row = tools.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 6f; row.childControlWidth = true; row.childForceExpandWidth = true;
        CreateButton(tools, "CHAT", ShowChatPopup);
        socialEmojiFilterButton = CreateButton(tools, "EMOJI", ToggleSocialEmojiFilter);
        CreateButton(tools, "INFO", ShowMatchInfo);
        CreateButton(tools, "MENU", ShowMatchSettings);
        CreateText(rail, "PlayersTitle", theme.TypeSection, theme.TextoSecundario,
            new Vector2(16f, -84f), new Vector2(268f, 24f)).text = "ORDEM DO ROUND";
        playersContent = WDUIFactory.Rect("Players", rail);
        AnchorTop(playersContent, 14f, 116f, 272f, 560f);
        VerticalLayoutGroup list = playersContent.gameObject.AddComponent<VerticalLayoutGroup>();
        list.spacing = 8f; list.childControlHeight = true; list.childForceExpandHeight = false;
        finishRoundButton = CreateCompactControl(rail, "FinishRound", ">>  FINALIZAR ROUND",
            new Vector2(16f, -704f), FinishRound);
    }

    // ============================================================
    // 03. APRESENTAÇÃO DO ESTADO AUTORITATIVO
    // ============================================================

    public void Present(MatchUIState newState)
    {
        if (newState == null || theme == null) return;
        bool newRound = lastPresentedRound >= 0 && lastPresentedRound != newState.Round;
        lastPresentedRound = newState.Round;
        state = newState;
        if (newRound || state.Fase != GameManager.FaseTurno.Preparacao || state.AcoesEnviadas)
            CloseActionsDrawerImmediately();
        energyText.text = $"E\n{EnergiaMock}";
        troopsText.text = $"T\n{state.ReforcosDisponiveis}";
        int seconds = Mathf.CeilToInt(state.TempoRestante);
        timerText.text = $"@\n{seconds / 60:00}:{seconds % 60:00}";
        roundText.text = state.EmMorteSubita ? $"MORTE SÚBITA {state.RoundMorteSubita}" : $"ROUND {state.Round:00}";
        phaseText.text = state.EmSelecaoInicial
            ? state.FeedbackSelecaoInicial
            : state.Fase.ToString().ToUpperInvariant();
        transferButton.SetInteractable(state.PodeEditarPreparacao && state.TransferenciaDisponivel && !state.TransferenciaUsada);
        transferButton.SetSelected(state.EmModoTransferencia);
        transferButton.SetLabel(state.EmModoTransferencia ? "X  CANCELAR TRANSFERIR" : "< >  TRANSFERIR");
        bool canConfirmTransfer = state.EmModoTransferencia &&
            !string.IsNullOrEmpty(state.TerritorioTransferenciaId) &&
            !state.TransferenciaUsada;
        confirmTransferButton.gameObject.SetActive(canConfirmTransfer);
        confirmTransferButton.SetInteractable(canConfirmTransfer);
        confirmTransferButton.SetLabel(
            state.DestinatariosTransferencia.Count > 1 &&
            state.DestinatarioTransferenciaSelecionado == TerritorioClique.Dono.Neutro
                ? "ESCOLHER ALIADO"
                : "CONFIRMAR TRANSFERÊNCIA");
        finishRoundButton.SetInteractable(
            state.AcoesEnviadas
                ? state.PodeCancelarEnvio
                : state.PodeEnviarPreparacao && state.ReforcosDisponiveis == 0);
        finishRoundButton.SetLabel(state.AcoesEnviadas
            ? "X  DESFAZER ENVIO"
            : ">>  FINALIZAR ROUND");
        PresentSocialEmojiFilter();
        PresentDeck();
        string newPlayersKey = BuildPlayersContentKey();
        if (newPlayersKey != playersContentKey)
            RebuildPlayers(newPlayersKey);
        PresentAttackBar();
        if (actionsOpen && BuildActionsContentKey() != actionsContentKey) RebuildActions();
    }

    private void PresentDeck()
    {
        for (int i = 0; i < deckButtons.Count; i++)
        {
            if (state != null && state.UsaRandomCards)
            {
                string cardId = i < state.CartasJogadorLocal.Count
                    ? state.CartasJogadorLocal[i]
                    : string.Empty;
                bool occupied = !string.IsNullOrEmpty(cardId);
                deckButtons[i].SetInteractable(occupied);
                deckButtons[i].SetSelected(false);
                deckButtons[i].SetLabel(occupied ? cardId : "VAZIO");
                continue;
            }

            bool selected = selectedCard == i;
            deckButtons[i].SetInteractable(CustosDeckMock[i] <= EnergiaMock);
            deckButtons[i].SetSelected(false);
            deckButtons[i].SetLabel(selected
                ? $"X  CARTA {i + 1}\nE {CustosDeckMock[i]}  CD --"
                : $"CARTA {i + 1}\nE {CustosDeckMock[i]}  CD --");
            Image background = deckButtons[i].GetComponent<Image>();
            TextMeshProUGUI label = deckButtons[i].GetComponentInChildren<TextMeshProUGUI>();
            if (selected && background != null)
                background.color = new Color(0.01f, 0.01f, 0.012f, 0.98f);
            if (label != null)
                label.color = selected ? theme.Danger : theme.TextoPrincipal;
        }
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
        foreach (MatchUIReinforcementState reinforcement in state.Distribuicoes)
        {
            int id = reinforcement.Id;
            AddActionItem($"REFORÇO  +{reinforcement.Quantidade}\n{reinforcement.TerritorioId}",
                () => commands?.RemoverDistribuicaoReforco(id));
        }
        foreach (MatchUIActionState action in state.Acoes)
        {
            int position = action.Posicao;
            AddActionItem($"{action.TipoEsperado}  x{action.Quantidade}\n{action.OrigemId} > {action.DestinoId}",
                () => commands?.RemoverAcao(position));
        }
        if (state.Transferencia != null)
            AddActionItem($"TRANSFERÊNCIA\n{state.Transferencia.TerritorioId}", () => commands?.RemoverTransferencia());
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
                if (player.EhJogadorLocal) button.onClick.AddListener(ShowEmojiDrawer);
                else button.onClick.AddListener(ShowTacticalPopup);
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

    private void ShowChatPopup() => ShowPopup("CHAT / MENSAGENS",
        "TIME   CLÃ\n\nHistórico local provisório\n\n[ campo de mensagem ]   ENVIAR\n\nEMOJIS   |   SUPORTE");
    private void ShowMatchSettings() => ShowPopup("CONFIGURAÇÕES DA PARTIDA",
        "CONFIGURAÇÕES\n\nMANUAL", "ABANDONAR PARTIDA", WDMatchSetupContext.ExitToHome);
    private void ShowEmojiDrawer() => ShowPopup("EMOJIS RÁPIDOS",
        "Envio social local provisório.", "ENVIAR  :) ", SendLocalTestEmoji);
    private void ShowTacticalPopup() => ShowPopup("COMUNICAÇÃO TÁTICA",
        "Selecione um comando local provisório.", "ATACAR   DEFENDER   TRANSFERIR", ClosePopup);

    // ============================================================
    // 08. SELEÇÃO REUTILIZÁVEL DO DESTINATÁRIO DA TRANSFERÊNCIA
    // ============================================================

    private void ConfirmTransferRecipient()
    {
        if (state == null || state.DestinatariosTransferencia.Count == 0)
            return;

        if (state.DestinatariosTransferencia.Count == 1)
        {
            commands?.ConfirmarTransferencia();
            return;
        }

        if (state.DestinatarioTransferenciaSelecionado !=
            TerritorioClique.Dono.Neutro)
        {
            commands?.ConfirmarTransferencia();
            return;
        }

        ShowTransferRecipientPopup();
    }

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
            theme.Danger, ClosePopup);

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
        string body = $"ROUND {state?.Round ?? 0}\n\n";
        if (state != null)
            foreach (MatchUIPlayerState player in state.Jogadores)
            {
                int totalTroops = 0;
                foreach (MatchUITerritoryState territory in state.Territorios)
                    if (territory.Proprietario == player.Jogador)
                        totalTroops += territory.Tropas;
                string team = state.UsaEquipes ? $"  |  {player.Equipe}" : string.Empty;
                body += $"{player.Nome}{team}\nMapa {player.TerritoriosControlados}   " +
                    $"Tropas {totalTroops}   Disponíveis {(player.EhJogadorLocal ? state.ReforcosDisponiveis : 0)}   " +
                    $"Energia {EnergiaMock}\nDeck: {(player.EhJogadorLocal ? "ATUAL" : "OCULTO")}\n\n";
            }
        ShowPopup("INFORMAÇÕES DA PARTIDA", body);
    }

    private void ShowPopup(string title, string body, string actionLabel = null, UnityEngine.Events.UnityAction action = null)
    {
        ClearChildren(popupLayer); popupLayer.gameObject.SetActive(true);
        Image blocker = popupLayer.gameObject.GetComponent<Image>() ?? popupLayer.gameObject.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0.66f); blocker.raycastTarget = true;
        RectTransform panel = CreateNeutralPanel("Popup", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(620f, 560f), popupLayer);
        CreateText(panel, "Title", theme.TypeTitle, theme.TextoPrincipal,
            new Vector2(24f, -24f), new Vector2(520f, 42f)).text = title;
        CreateMiniButton(panel, "X", new Vector2(-54f, -20f), theme.Danger, ClosePopup);
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
        PresentSocialEmojiFilter();
    }

    private void PresentSocialEmojiFilter()
    {
        if (socialEmojiFilterButton == null)
            return;

        socialEmojiFilterButton.SetSelected(hideSocialEmojis);
        socialEmojiFilterButton.SetLabel(hideSocialEmojis ? "X EMOJI" : "EMOJI");
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
        if (state != null && state.UsaRandomCards)
        {
            commands?.UsarCarta(slot);
            return;
        }

        selectedCard = selectedCard == slot ? -1 : slot;
        PresentDeck();
    }
    private void ToggleTransfer()
    {
        if (state == null) return;
        if (state.EmModoTransferencia) commands?.SelecionarModoAcao();
        else commands?.SelecionarModoTransferencia();
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
