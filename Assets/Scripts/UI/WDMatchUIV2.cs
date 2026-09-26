using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WDMatchUIV2 : MonoBehaviour, IWDMatchUIPresentation, IWDMatchChatPresentation
{
    // ============================================================
    // 01. REFERÊNCIAS E CONSTANTES VISUAIS
    // ============================================================

    private const float RailWidth = 273f;
    private const float RailHeight = 874f;
    private const float RailContentX = 18f;
    private const float RailContentWidth = 227f;
    private const float WeaponCardWidth = 109f;
    private const float WeaponCardHeight = 121f;
    private const float CompactButtonHeight = 43f;
    private const float ResourceIconSize = 24f;
    private const float SpeedIconSize = 40f;
    private const float MenuPlayerNameX = 126f;
    private const float MenuPlayerNameWidth = 220f;
    private const float MenuTerritoriesValueX = 360f;
    private const float MenuTroopsValueX = 400f;
    private const float MenuEnergyValueX = 440f;
    private const float MenuSlotsX = 500f;
    private const string MatchIconRoot = "UI/Match/Official/";
    private const string EnergyIconPath = MatchIconRoot + "Energy";
    private const string TroopsIconPath = MatchIconRoot + "Troops";
    private const string MovementsIconPath = MatchIconRoot + "Movements";
    private const string TimeIconPath = MatchIconRoot + "Time";
    private const string SpeedOffIconPath = MatchIconRoot + "SpeedOff";
    private const string SpeedOnIconPath = MatchIconRoot + "SpeedOn";
    private const string ChatIconPath = MatchIconRoot + "ChatNeutral";
    private const string SettingsIconPath = MatchIconRoot + "SettingsNeutral";
    private const string PlayerTerritoriesIconPath = MatchIconRoot + "PlayerStatTerritories";
    private const string PlayerTroopsIconPath = MatchIconRoot + "PlayerStatTroops";
    private const string PlayerEnergyIconPath = MatchIconRoot + "PlayerStatEnergy";
    private static readonly Color Gold = WDMatchVisualStyle.BorderMatchGold;
    private static readonly Color Surface = WDMatchVisualStyle.SurfaceMatchReadable;
    private static readonly Color ResourceSurface = new Color(6f / 255f, 9f / 255f,
        14f / 255f, WDMatchVisualStyle.SurfaceMatchReadable.a);
    private static readonly Color RailSurface = new Color(11f / 255f, 14f / 255f,
        18f / 255f, WDMatchVisualStyle.SurfaceMatchReadable.a);
    private static readonly Color CardSurface = new Color(6f / 255f, 9f / 255f,
        14f / 255f, WDMatchVisualStyle.SurfaceMatchReadable.a);

    private WarDominionUITheme theme;
    private IMatchUICommands commands;
    private IWDMatchUtilityCommands utilities;
    private WDWeaponMatchUIController weaponUI;
    private ResolutionSequenceController resolutionSequence;
    private MatchUIState state;

    private TextMeshProUGUI energyText;
    private TextMeshProUGUI contextualResourceText;
    private TextMeshProUGUI timerText;
    private TextMeshProUGUI roundText;
    private Image contextualResourceIcon;
    private Sprite troopsResourceSprite;
    private Sprite movementsResourceSprite;
    private TextMeshProUGUI movementRouteText;
    private TextMeshProUGUI movementAmountText;
    private RectTransform playersContent;
    private RectTransform movementContext;
    private RectTransform menuModalLayer;
    private WDMatchChatView chatView;
    private WDMatchSettingsView settingsView;
    private RectTransform menuPlayersContent;
    private TextMeshProUGUI menuTitle;
    private WDUIPremiumButton sendMovesButton;
    private WDUIPremiumButton transferButton;
    private WDUIPremiumButton speedButton;
    private WDUIPremiumButton menuButton;
    private WDUIPremiumButton regionsButton;
    private WDUIPremiumButton chatButton;
    private WDUIPremiumButton settingsButton;
    private Image speedIcon;
    private Sprite speedOffSprite;
    private Sprite speedOnSprite;
    private readonly List<WDUIPremiumButton> weaponCards = new();
    private string playersKey = string.Empty;
    private string menuPlayersKey = string.Empty;

    // ============================================================
    // 02. CONSTRUÇÃO DO HUD BASE SOBRE A SAFE AREA
    // ============================================================

    public void Build(WarDominionUITheme newTheme,
        IMatchUICommands newCommands,
        WDWeaponMatchUIController newWeaponUI,
        ResolutionSequenceController newResolutionSequence)
    {
        theme = newTheme;
        commands = newCommands;
        weaponUI = newWeaponUI;
        resolutionSequence = newResolutionSequence;

        RectTransform rect = transform as RectTransform;
        Stretch(rect);

        BuildResources();
        BuildUtilities();
        BuildLeftRail();
        BuildPlayers();
        BuildSendMoves();
        BuildMovementContext();
        BuildMenuModal();
        // 02.1. O Chat usa o mesmo Canvas/Safe Area e a autoridade de utilidades.
        RectTransform chatRect = WDUIFactory.Rect("MatchChat", transform);
        Stretch(chatRect);
        chatView = chatRect.gameObject.AddComponent<WDMatchChatView>();
        chatView.Build(new[] { chatButton.transform as RectTransform,
            menuButton.transform as RectTransform, regionsButton.transform as RectTransform,
            settingsButton.transform as RectTransform });
        // 02.2. Settings compartilha Canvas/Safe Area e a mesma autoridade principal.
        RectTransform settingsRect = WDUIFactory.Rect("MatchSettings", transform);
        Stretch(settingsRect);
        settingsView = settingsRect.gameObject.AddComponent<WDMatchSettingsView>();
        settingsView.Build(new[] { chatButton.transform as RectTransform,
            menuButton.transform as RectTransform, regionsButton.transform as RectTransform,
            settingsButton.transform as RectTransform });
    }

    public void BindSettings(WDSettingsRepository repository, Action exit) =>
        settingsView?.Bind(repository, exit);

    public void BindChat(WDChatRepository repository, WDChatChannel matchChannel) =>
        chatView?.BindRepository(repository, matchChannel);

    public void BindCommands(IMatchUICommands newCommands) => commands = newCommands;

    public void BindUtilities(IWDMatchUtilityCommands newUtilities)
    {
        if (utilities != null)
            utilities.ModuleRequested -= HandleUtilityModuleRequested;
        utilities = newUtilities;
        chatView?.BindUtilities(newUtilities);
        settingsView?.BindUtilities(newUtilities);
        if (utilities != null)
            utilities.ModuleRequested += HandleUtilityModuleRequested;
        PresentUtilitySelection();
    }

    private void OnDestroy()
    {
        if (utilities != null)
            utilities.ModuleRequested -= HandleUtilityModuleRequested;
    }

    private void BuildResources()
    {
        RectTransform panel = Panel("Resources", transform, new Vector2(0f, 1f),
            new Vector2(69f, -12f), new Vector2(240f, 60f), new Vector2(0f, 1f),
            ResourceSurface);
        SetBorderColor(panel, new Color(Gold.r, Gold.g, Gold.b, .75f));
        energyText = Resource(panel, "Energy", EnergyIconPath,
            new Vector2(26f, -18f), new Vector2(50f, -12f), 54f, out _);
        contextualResourceText = Resource(panel, "Contextual", TroopsIconPath,
            new Vector2(93f, -18f), new Vector2(123f, -12f), 48f,
            out contextualResourceIcon);
        troopsResourceSprite = contextualResourceIcon.sprite;
        movementsResourceSprite = LoadRuntimeSprite(MovementsIconPath);
        timerText = Resource(panel, "Time", TimeIconPath,
            new Vector2(156f, -18f), new Vector2(182f, -13f), 82f, out _);
    }

    private void BuildUtilities()
    {
        RectTransform tools = WDUIFactory.Rect("UtilityControls", transform);
        tools.anchorMin = tools.anchorMax = Vector2.one;
        tools.pivot = Vector2.one;
        tools.anchoredPosition = new Vector2(-54f, -24f);
        tools.sizeDelta = new Vector2(118f, 48f);
        Image surface = tools.gameObject.AddComponent<Image>();
        surface.color = RailSurface;
        surface.raycastTarget = false;
        WDHomeUIFactory.ApplyRounded(surface, 10);
        Image toolsBorder = WDHomeUIFactory.AddRoundedBorder(tools,
            new Color(Gold.r, Gold.g, Gold.b, .42f), 10);
        LayoutElement borderLayout = toolsBorder.gameObject.AddComponent<LayoutElement>();
        borderLayout.ignoreLayout = true;
        HorizontalLayoutGroup row = tools.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 8f;
        row.padding = new RectOffset(10, 4, 0, 0);
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = true;
        row.childForceExpandWidth = false;
        chatButton = IconButton(tools, "Chat", ChatIconPath,
            () => ToggleUtility(WDMatchUtilityModule.Chat), 24f);
        settingsButton = IconButton(tools, "Settings", SettingsIconPath,
            () => ToggleUtility(WDMatchUtilityModule.Settings), 24f);
        ConfigureTouchIcon(chatButton, 48f, Color.white);
        ConfigureTouchIcon(settingsButton, 48f, Color.white);
    }

    private void BuildLeftRail()
    {
        RectTransform rail = Panel("LeftCommandRail", transform,
            new Vector2(0f, 1f), new Vector2(12f, -185f),
            new Vector2(RailWidth, RailHeight), new Vector2(0f, 1f), RailSurface);
        SetBorderColor(rail, Gold);

        RectTransform navigation = WDUIFactory.Rect("PrimaryNavigation", rail);
        AnchorTop(navigation, 40f, 25f, 192f, 48f);
        menuButton = PositionedIconButton(navigation, "Menu",
            "UI/Match/FigmaCurrent/Menu", 0f, 40f,
            () => ToggleUtility(WDMatchUtilityModule.Menu));
        regionsButton = PositionedIconButton(navigation, "Regions",
            "UI/Match/FigmaCurrent/Regions", 72f, 40f,
            () => ToggleUtility(WDMatchUtilityModule.Regions));
        speedButton = PositionedIconButton(navigation, "Speed", SpeedOffIconPath,
            144f, SpeedIconSize, ToggleResolutionSpeed);
        speedIcon = speedButton.transform.Find("Icon")?.GetComponent<Image>();
        speedOffSprite = speedIcon != null ? speedIcon.sprite : null;
        speedOnSprite = LoadRuntimeSprite(SpeedOnIconPath);
        ConfigureBareControl(menuButton);
        ConfigureBareControl(regionsButton);
        ConfigureBareControl(speedButton);
        // A fase serializada nunca deve decidir um frame visual antes do primeiro
        // MatchUIState autoritativo publicado pelo presenter.
        speedButton.gameObject.SetActive(false);

        roundText = Text(rail, "Round", 34f, Gold,
            new Vector2(64f, -89f), new Vector2(146f, 43f),
            TextAlignmentOptions.Center);

        RectTransform gridRect = WDUIFactory.Rect("WeaponGrid", rail);
        AnchorTop(gridRect, RailContentX, 162f, RailContentWidth, 511f);
        GridLayoutGroup grid = gridRect.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(WeaponCardWidth, WeaponCardHeight);
        grid.spacing = new Vector2(9f, 9f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        for (int i = 0; i < 8; i++)
        {
            int slot = i;
            WDUIPremiumButton card = Button(gridRect, string.Empty,
                () => SelectWeapon(slot));
            card.gameObject.name = $"WeaponSlot{i + 1}";
            card.SetHoverFeedbackEnabled(false);
            card.SetPersistentColors(CardSurface,
                new Color(Gold.r, Gold.g, Gold.b, WDMatchVisualStyle.SurfaceMatchReadable.a));
            card.SetDisabledColor(CardSurface);
            SetBorderColor(card.transform as RectTransform,
                new Color(Gold.r, Gold.g, Gold.b, .70f));
            TextMeshProUGUI label = card.GetComponentInChildren<TextMeshProUGUI>();
            label.fontSize = 15f;
            label.textWrappingMode = TextWrappingModes.Normal;
            weaponCards.Add(card);
        }

        CompactButton(rail, "Support", "APOIO", new Vector2(RailContentX, -689f), null);
        transferButton = CompactButton(rail, "Transfer", "TRANSFERÊNCIA",
            new Vector2(RailContentX, -746f), ToggleTransfer);
        CompactButton(rail, "Actions", "AÇÕES", new Vector2(RailContentX, -803f),
            () => commands?.SelecionarModoAcao());
    }

    private void BuildPlayers()
    {
        // 02.3. Contraste é irmão anterior à lista: não participa do layout nem dos cliques.
        RectTransform contrast = WDUIFactory.Rect("PlayersContrast", transform);
        Image background = contrast.gameObject.AddComponent<Image>();
        background.color = WDMatchVisualStyle.SurfaceMatchReadable;
        background.raycastTarget = false;
        WDHomeUIFactory.ApplyRounded(background, 10);
        WDHomeUIFactory.AddRoundedBorder(contrast, new Color(Gold.r, Gold.g, Gold.b, .6f), 10);
        playersContent = WDUIFactory.Rect("Players", transform);
        playersContent.anchorMin = playersContent.anchorMax = Vector2.one;
        playersContent.pivot = Vector2.one;
        // Margem de 20 px para o painel + seus 8 px de padding, relativos à Safe Area.
        playersContent.anchoredPosition = new Vector2(-28f, -334f);
        playersContent.sizeDelta = new Vector2(154f, 282f);
        VerticalLayoutGroup list = playersContent.gameObject.AddComponent<VerticalLayoutGroup>();
        list.spacing = 22f;
        list.childControlHeight = true;
        list.childForceExpandHeight = false;
        contrast.gameObject.AddComponent<WDMatchPlayersContrast>().Bind(playersContent);
    }

    private void BuildSendMoves()
    {
        RectTransform rect = WDUIFactory.Rect("SendMoves", transform);
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-20f, 58f);
        rect.sizeDelta = new Vector2(230f, 69f);
        sendMovesButton = rect.gameObject.AddComponent<WDUIPremiumButton>();
        sendMovesButton.Build(theme, "ENVIAR JOGADAS", SendMoves);
        sendMovesButton.SetPersistentColors(Surface,
            new Color(Gold.r, Gold.g, Gold.b, WDMatchVisualStyle.SurfaceMatchReadable.a));
        sendMovesButton.SetDisabledColor(Surface);
        SetButtonRadius(sendMovesButton, 20);
        sendMovesButton.gameObject.SetActive(false);
    }

    private void BuildMovementContext()
    {
        // 02.1. A barra somente apresenta o recorte da seleção autoritativa. Os
        // botões encaminham comandos existentes e não calculam regras de jogo.
        movementContext = Panel("MovementContext", transform,
            new Vector2(.5f, 0f), new Vector2(0f, 24f),
            new Vector2(650f, 88f), new Vector2(.5f, 0f));
        movementRouteText = Text(movementContext, "Route", 16f,
            theme.TextoPrincipal, new Vector2(20f, -12f), new Vector2(250f, 62f),
            TextAlignmentOptions.Left);
        WDUIPremiumButton decrease = CompactButton(movementContext, "Decrease", "−",
            new Vector2(286f, -22f), () => commands?.DiminuirQuantidadeAcao());
        decrease.GetComponent<RectTransform>().sizeDelta = new Vector2(52f, 48f);
        movementAmountText = Text(movementContext, "Amount", 18f, Gold,
            new Vector2(346f, -22f), new Vector2(94f, 48f),
            TextAlignmentOptions.Center);
        WDUIPremiumButton increase = CompactButton(movementContext, "Increase", "+",
            new Vector2(448f, -22f), () => commands?.AumentarQuantidadeAcao());
        increase.GetComponent<RectTransform>().sizeDelta = new Vector2(52f, 48f);
        WDUIPremiumButton confirm = CompactButton(movementContext, "Confirm", "CONFIRMAR",
            new Vector2(508f, -22f), () => commands?.ConfirmarAcao());
        confirm.GetComponent<RectTransform>().sizeDelta = new Vector2(122f, 48f);
        movementContext.gameObject.SetActive(false);
    }

    // ============================================================
    // 02.2. MENU / JOGADORES CANÔNICO — FIGMA 110:120
    // ============================================================

    private void BuildMenuModal()
    {
        menuModalLayer = WDUIFactory.Rect("MenuPlayersModal", transform);
        Stretch(menuModalLayer);
        Image backdrop = menuModalLayer.gameObject.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, .55f);
        backdrop.raycastTarget = true;
        Button backdropButton = menuModalLayer.gameObject.AddComponent<Button>();
        backdropButton.transition = Selectable.Transition.None;
        backdropButton.onClick.AddListener(CloseMenuFromBackdrop);

        RectTransform panel = WDUIFactory.Rect("Panel", menuModalLayer);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
        panel.anchoredPosition = new Vector2(24.5f, -37.5f);
        panel.sizeDelta = new Vector2(991f, 557f);
        Image surface = panel.gameObject.AddComponent<Image>();
        surface.color = WDMatchVisualStyle.SurfaceMatchReadable;
        surface.raycastTarget = true;
        WDHomeUIFactory.ApplyRounded(surface, 20);
        WDHomeUIFactory.AddRoundedBorder(panel, Gold, 20);
        // O EventTrigger torna o painel o primeiro handler de clique da hierarquia.
        // Assim, o Button do backdrop só recebe interações fora do painel.
        panel.gameObject.AddComponent<EventTrigger>();

        menuTitle = Text(panel, "Title", 34f, Gold,
            new Vector2(190f, -28f), new Vector2(610f, 48f),
            TextAlignmentOptions.Center);
        menuPlayersContent = WDUIFactory.Rect("DynamicPlayers", panel);
        Stretch(menuPlayersContent);
        menuModalLayer.gameObject.SetActive(false);
    }

    // ============================================================
    // 03. RENDERIZAÇÃO DIRIGIDA PELO SNAPSHOT AUTORITATIVO
    // ============================================================

    public void Present(MatchUIState newState)
    {
        if (newState == null || theme == null)
            return;
        state = newState;
        energyText.text = $"{weaponUI?.CurrentEnergy ?? 0}";
        bool showTroops = ShouldShowTroopsResource(state.ReforcosDisponiveis);
        contextualResourceIcon.sprite = showTroops
            ? troopsResourceSprite
            : movementsResourceSprite;
        contextualResourceText.text = FormatContextualResource(
            state.ReforcosDisponiveis, state.AcoesPreparadas, state.LimiteAcoes);
        timerText.text = FormatTimerSeconds(state.TempoRestante);
        roundText.text = state.EmMorteSubita
            ? $"MORTE SÚBITA {state.RoundMorteSubita}"
            : $"ROUND {state.Round}";
        transferButton.gameObject.SetActive(state.UsaEquipes);
        transferButton.SetInteractable(state.PodeEditarPreparacao &&
            state.TransferenciaDisponivel && !state.TransferenciaUsada);
        speedButton.gameObject.SetActive(ShouldShowResolutionSpeed(state.Fase));
        PresentSpeedSelection(resolutionSequence?.IsAccelerated == true);
        sendMovesButton.gameObject.SetActive(ShouldShowSendMoves(state.Fase));
        sendMovesButton.SetInteractable(state.AcoesEnviadas
            ? state.PodeCancelarEnvio
            : state.PodeEnviarPreparacao && state.ReforcosDisponiveis == 0);
        sendMovesButton.SetLabel(state.AcoesEnviadas
            ? "AÇÕES ENVIADAS"
            : "ENVIAR JOGADAS");

        bool showMovement = ShouldShowMovementContext(state);
        movementContext.gameObject.SetActive(showMovement);
        if (showMovement)
        {
            movementRouteText.text = $"{state.TipoAcaoEsperado}\n" +
                $"{state.OrigemSelecionadaId} → {state.DestinoSelecionadoId}";
            movementAmountText.text = $"{state.QuantidadeAcaoSelecionada} / " +
                $"{state.QuantidadeMaximaAcao}";
        }

        PresentWeapons();
        string nextPlayersKey = PlayerContentKey(state.Jogadores);
        if (nextPlayersKey != playersKey)
            RebuildPlayers(nextPlayersKey);
        if (menuTitle != null)
            menuTitle.text = FormatMenuTitle(state.Round);
        if (menuModalLayer != null && menuModalLayer.gameObject.activeSelf)
            RebuildMenuPlayers();
    }

    private void PresentWeapons()
    {
        IReadOnlyList<WDWeaponHudCardState> cards = weaponUI?.GetCards() ??
            Array.Empty<WDWeaponHudCardState>();
        for (int i = 0; i < weaponCards.Count; i++)
        {
            WDWeaponHudCardState card = i < cards.Count ? cards[i] : null;
            weaponCards[i].SetLabel(card == null ? "VAZIO" :
                FormatWeaponCardLabel(card.DisplayName, card.Level, card.EnergyCost));
            weaponCards[i].SetInteractable(state.PodeEditarPreparacao && card != null &&
                (card.AvailabilityReason == WDWeaponAvailabilityReason.Available ||
                 card.AvailabilityReason == WDWeaponAvailabilityReason.InsufficientEnergy));
            weaponCards[i].SetSelected(card?.Selected == true);
        }
    }

    private void RebuildPlayers(string nextKey)
    {
        ClearChildren(playersContent);
        foreach (MatchUIPlayerState player in state.Jogadores)
        {
            RectTransform row = WDUIFactory.Rect($"Player-{player.Jogador}", playersContent);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 54f;

            // 03.1. O brilho pertence a duas bordas externas independentes. Elas
            // ficam atrás do slot e nunca processam o placeholder ou a silhueta.
            BuildAvatarGlow(row, player.Cor);
            RectTransform marker = WDUIFactory.Rect("AvatarSlot", row);
            marker.anchorMin = marker.anchorMax = new Vector2(0f, .5f);
            marker.pivot = new Vector2(0f, .5f);
            marker.anchoredPosition = Vector2.zero;
            marker.sizeDelta = new Vector2(54f, 54f);
            Image markerImage = marker.gameObject.AddComponent<Image>();
            markerImage.color = new Color(6f / 255f, 9f / 255f, 14f / 255f, .90f);
            markerImage.raycastTarget = false;
            WDHomeUIFactory.ApplyRounded(markerImage, 8);
            WDHomeUIFactory.AddRoundedBorder(marker, player.Cor, 8);
            BuildAvatarFallback(marker);

            string team = player.Equipe == EquipesJogadores.Equipe.Nenhuma
                ? "FFA"
                : player.Equipe.ToString().ToUpperInvariant();
            TextMeshProUGUI label = WDUIFactory.Text("Identity", row, theme, 17f,
                Gold, TextAlignmentOptions.Left);
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.offsetMin = new Vector2(68f, 0f);
            label.rectTransform.offsetMax = Vector2.zero;
            label.text = $"<size=10>{team}</size>\n{player.Nome.ToUpperInvariant()}";
            label.raycastTarget = false;
            Shadow shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, .85f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
        }
        playersKey = nextKey;
    }

    private void RebuildMenuPlayers()
    {
        if (state == null)
            return;
        string nextKey = BuildMenuPlayersKey();
        if (nextKey == menuPlayersKey)
            return;
        ClearChildren(menuPlayersContent);

        int rowCount = Mathf.Min(4, state.Jogadores.Count);
        for (int i = 0; i < rowCount; i++)
            BuildMenuPlayerRow(menuPlayersContent, state.Jogadores[i], i);
        menuPlayersKey = nextKey;
    }

    private string BuildMenuPlayersKey()
    {
        var key = new System.Text.StringBuilder(192);
        foreach (MatchUIPlayerState player in state.Jogadores)
        {
            int troops = 0;
            foreach (MatchUITerritoryState territory in state.Territorios)
                if (territory.Proprietario == player.Jogador)
                    troops += territory.Tropas;
            string playerId = weaponUI?.PlayerIdForOwner(player.Jogador) ?? string.Empty;
            key.Append((int)player.Jogador).Append(':').Append(player.Nome).Append(':')
                .Append((int)player.Equipe).Append(':').Append(player.Cor).Append(':')
                .Append(player.TerritoriosControlados).Append(':').Append(troops).Append(':')
                .Append(weaponUI?.GetPlayerEnergy(playerId) ?? 0).Append('|');
        }
        return key.ToString();
    }

    private void BuildMenuPlayerRow(Transform parent, MatchUIPlayerState player, int index)
    {
        float top = 149f + index * 59f;
        RectTransform row = WDUIFactory.Rect($"PlayerRow-{player.Jogador}", parent);
        AnchorTop(row, 0f, top, 991f, 52f);

        BuildMenuPlayerMarker(row, player.Cor);
        string role = player.Equipe == EquipesJogadores.Equipe.Nenhuma
            ? "FFA"
            : player.Equipe.ToString().ToUpperInvariant();
        TextMeshProUGUI roleText = Text(row, "Role", 10f,
            WDMatchVisualStyle.Muted, new Vector2(MenuPlayerNameX, -9f),
            new Vector2(MenuPlayerNameWidth, 16f), TextAlignmentOptions.Center);
        roleText.text = role;
        TextMeshProUGUI nameText = Text(row, "PlayerName", 22f, Gold,
            new Vector2(MenuPlayerNameX, -21f),
            new Vector2(MenuPlayerNameWidth, 28f),
            TextAlignmentOptions.Center);
        nameText.text = FormatMenuPlayerName(player.Nome);
        nameText.overflowMode = TextOverflowModes.Ellipsis;

        int troops = 0;
        foreach (MatchUITerritoryState territory in state.Territorios)
            if (territory.Proprietario == player.Jogador)
                troops += territory.Tropas;
        string playerId = weaponUI?.PlayerIdForOwner(player.Jogador) ?? string.Empty;
        int energy = weaponUI?.GetPlayerEnergy(playerId) ?? 0;
        AddMenuStatValue(row, "TerritoriesValue", player.TerritoriosControlados,
            MenuTerritoriesValueX);
        AddMenuStatValue(row, "TroopsValue", troops, MenuTroopsValueX);
        AddMenuStatValue(row, "EnergyValue", energy, MenuEnergyValueX);

        RectTransform slots = WDUIFactory.Rect("ReservedSlots", row);
        AnchorTop(slots, MenuSlotsX, 0f, 318f, 52f);
        HorizontalLayoutGroup slotLayout = slots.gameObject.AddComponent<HorizontalLayoutGroup>();
        slotLayout.spacing = 2f;
        slotLayout.childControlWidth = false;
        slotLayout.childControlHeight = false;
        slotLayout.childForceExpandWidth = false;
        slotLayout.childForceExpandHeight = false;
        for (int slotIndex = 0; slotIndex < 8; slotIndex++)
        {
            RectTransform slot = WDUIFactory.Rect($"Slot-{slotIndex + 1}", slots);
            slot.sizeDelta = new Vector2(38f, 52f);
            Image slotSurface = slot.gameObject.AddComponent<Image>();
            slotSurface.color = new Color(11f / 255f, 14f / 255f, 18f / 255f, .80f);
            slotSurface.raycastTarget = false;
            WDHomeUIFactory.ApplyRounded(slotSurface, 12);
            WDHomeUIFactory.AddRoundedBorder(slot, Gold, 12);
        }

        if (index == 0)
            BuildMenuStatIcons(parent);
    }

    private void BuildMenuStatIcons(Transform parent)
    {
        AddMenuStatIcon(parent, "TerritoriesIcon", PlayerTerritoriesIconPath,
            new Vector2(371f, -139f), new Vector2(17f, 20f));
        AddMenuStatIcon(parent, "TroopsIcon", PlayerTroopsIconPath,
            new Vector2(409f, -139f), new Vector2(20f, 22f));
        AddMenuStatIcon(parent, "EnergyIcon", PlayerEnergyIconPath,
            new Vector2(449f, -139f), new Vector2(20f, 22f));
    }

    private static void BuildMenuPlayerMarker(Transform parent, Color color)
    {
        RectTransform marker = WDUIFactory.Rect("PlayerMarker", parent);
        marker.anchorMin = marker.anchorMax = new Vector2(0f, 1f);
        marker.pivot = new Vector2(0f, 1f);
        marker.anchoredPosition = new Vector2(85f, 0f);
        marker.sizeDelta = new Vector2(42f, 48f);
        Image image = marker.gameObject.AddComponent<Image>();
        image.sprite = LoadRuntimeSprite(MatchIconRoot + "PlayerMarkerMask");
        image.color = new Color(color.r, color.g, color.b, .82f);
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private void AddMenuStatValue(Transform parent, string name, int value, float x)
    {
        TextMeshProUGUI text = Text(parent, name, 22f, Color.white,
            new Vector2(x, -21f), new Vector2(38f, 28f),
            TextAlignmentOptions.Center);
        text.text = Mathf.Max(0, value).ToString();
    }

    private static void AddMenuStatIcon(Transform parent, string name, string path,
        Vector2 position, Vector2 size)
    {
        RectTransform rect = WDUIFactory.Rect(name, parent);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = LoadRuntimeSprite(path);
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    public void ShowInstallationHub(WDWeaponHudInstallationState installation)
    {
        // O hub completo pertence ao próximo módulo; o binding permanece compatível.
    }

    // ============================================================
    // 04. REGRAS DE FASE E CALLBACKS FUNCIONAIS MÍNIMOS
    // ============================================================

    public static bool ShouldShowSendMoves(GameManager.FaseTurno phase) =>
        phase == GameManager.FaseTurno.Preparacao;

    public static bool ShouldShowResolutionSpeed(GameManager.FaseTurno phase) =>
        phase == GameManager.FaseTurno.Preparacao ||
        phase == GameManager.FaseTurno.Resolucao;

    public static bool ShouldShowMovementContext(MatchUIState value) =>
        value != null && value.Fase == GameManager.FaseTurno.Preparacao &&
        value.PodeEditarPreparacao && value.PodeConfirmarAcao &&
        value.QuantidadeMaximaAcao > 0;

    public static string FormatMovementAvailability(int prepared, int limit)
    {
        int safeLimit = Mathf.Max(0, limit);
        int safePrepared = Mathf.Clamp(prepared, 0, safeLimit);
        return $"MOVIMENTOS  {safePrepared}/{safeLimit} · {safeLimit - safePrepared} DISP.";
    }

    public static bool ShouldShowTroopsResource(int troopsRemaining) =>
        troopsRemaining > 0;

    public static string ContextualResourceIconPath(int troopsRemaining) =>
        ShouldShowTroopsResource(troopsRemaining)
            ? TroopsIconPath
            : MovementsIconPath;

    public static int RemainingMovements(int prepared, int limit)
    {
        int safeLimit = Mathf.Max(0, limit);
        return safeLimit - Mathf.Clamp(prepared, 0, safeLimit);
    }

    public static string FormatContextualResource(int troopsRemaining,
        int prepared, int limit) => troopsRemaining > 0
        ? Mathf.Max(0, troopsRemaining).ToString()
        : RemainingMovements(prepared, limit).ToString();

    public static string FormatTimerSeconds(float remainingSeconds) =>
        Mathf.Max(0, Mathf.CeilToInt(remainingSeconds)).ToString();

    public static string SpeedIconPath(bool accelerated) =>
        accelerated ? SpeedOnIconPath : SpeedOffIconPath;

    public static string FormatWeaponCardLabel(string displayName, int level,
        int energyCost) => $"{displayName}\nN{level} · ⚡ {energyCost}";

    public static bool ContainsDeckOneLabel(string label) =>
        !string.IsNullOrWhiteSpace(label) &&
        label.IndexOf("DECK 1", StringComparison.OrdinalIgnoreCase) >= 0;

    public static string FormatMenuTitle(int round) =>
        $"JOGADORES · ROUND {Mathf.Max(0, round)}";

    public static string FormatMenuPlayerName(string playerName) =>
        (playerName ?? string.Empty).ToUpperInvariant();

    public static IReadOnlyList<string> PlayerStatisticIconPaths => new[]
    {
        PlayerTerritoriesIconPath,
        PlayerTroopsIconPath,
        PlayerEnergyIconPath
    };

    public static Vector2 MenuPlayerNameBounds =>
        new Vector2(MenuPlayerNameX, MenuPlayerNameWidth);

    public static Vector4 MenuPlayerHorizontalLayout => new Vector4(
        MenuTerritoriesValueX, MenuTroopsValueX, MenuEnergyValueX, MenuSlotsX);

    private void SelectWeapon(int slot)
    {
        IReadOnlyList<WDWeaponHudCardState> cards = weaponUI?.GetCards() ??
            Array.Empty<WDWeaponHudCardState>();
        if (slot >= 0 && slot < cards.Count)
            weaponUI.SelectWeapon(cards[slot].WeaponId);
    }

    private void ToggleTransfer()
    {
        if (state == null)
            return;
        if (state.EmModoTransferencia)
            commands?.SelecionarModoAcao();
        else
            commands?.SelecionarModoTransferencia();
    }

    private void SendMoves()
    {
        if (state == null)
            return;
        if (state.AcoesEnviadas)
            commands?.CancelarEnvio();
        else
            commands?.EnviarAcoes();
    }

    private void ToggleResolutionSpeed()
    {
        if (resolutionSequence == null)
            return;
        resolutionSequence.TogglePlaybackSpeed();
        PresentSpeedSelection(resolutionSequence.IsAccelerated);
    }

    private void ToggleUtility(WDMatchUtilityModule module)
    {
        utilities?.Toggle(module);
        PresentUtilitySelection();
    }

    private void CloseMenuFromBackdrop()
    {
        if (utilities?.ActiveModule != WDMatchUtilityModule.Menu)
            return;
        utilities.CloseActive();
        PresentUtilitySelection();
    }

    private void PresentUtilitySelection()
    {
        WDMatchUtilityModule active = utilities?.ActiveModule ??
            WDMatchUtilityModule.None;
        menuButton?.SetSelected(active == WDMatchUtilityModule.Menu);
        regionsButton?.SetSelected(active == WDMatchUtilityModule.Regions);
        chatButton?.SetSelected(active == WDMatchUtilityModule.Chat);
        settingsButton?.SetSelected(active == WDMatchUtilityModule.Settings);
        chatView?.SetOpen(active == WDMatchUtilityModule.Chat);
        settingsView?.SetOpen(active == WDMatchUtilityModule.Settings);
        if (menuModalLayer != null)
        {
            bool showMenu = active == WDMatchUtilityModule.Menu;
            menuModalLayer.gameObject.SetActive(showMenu);
            if (showMenu)
                RebuildMenuPlayers();
        }
    }

    private void HandleUtilityModuleRequested(WDMatchUtilityModule module) =>
        PresentUtilitySelection();

    // ============================================================
    // 05. FÁBRICA VISUAL LOCAL E TOUCH SAFE
    // ============================================================

    private RectTransform Panel(string name, Transform parent, Vector2 anchor,
        Vector2 position, Vector2 size, Vector2 pivot, Color? surfaceColor = null)
    {
        RectTransform rect = WDUIFactory.Rect(name, parent);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = surfaceColor ?? Surface;
        image.raycastTarget = false;
        WDHomeUIFactory.ApplyRounded(image, 12);
        WDHomeUIFactory.AddRoundedBorder(rect, Gold, 12);
        return rect;
    }

    private TextMeshProUGUI Resource(Transform parent, string name, string path,
        Vector2 iconPosition, Vector2 valuePosition, float valueWidth,
        out Image iconImage)
    {
        RectTransform item = WDUIFactory.Rect(name, parent);
        Stretch(item);
        RectTransform icon = WDUIFactory.Rect("Icon", item);
        icon.anchorMin = icon.anchorMax = new Vector2(0f, 1f);
        icon.pivot = new Vector2(0f, 1f);
        icon.anchoredPosition = iconPosition;
        icon.sizeDelta = new Vector2(ResourceIconSize, ResourceIconSize);
        iconImage = icon.gameObject.AddComponent<Image>();
        iconImage.sprite = LoadRuntimeSprite(path);
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        TextMeshProUGUI value = WDUIFactory.Text("Value", item, theme, 19f,
            theme.TextoPrincipal, TextAlignmentOptions.Left);
        value.rectTransform.anchorMin = value.rectTransform.anchorMax = new Vector2(0f, 1f);
        value.rectTransform.pivot = new Vector2(0f, 1f);
        value.rectTransform.anchoredPosition = valuePosition;
        value.rectTransform.sizeDelta = new Vector2(valueWidth, 35f);
        value.alignment = TextAlignmentOptions.MidlineLeft;
        value.raycastTarget = false;
        return value;
    }

    private WDUIPremiumButton IconButton(Transform parent, string name,
        string path, UnityEngine.Events.UnityAction action, float size = 38f)
    {
        WDUIPremiumButton button = Button(parent, string.Empty, action);
        button.gameObject.name = name;
        RectTransform icon = WDUIFactory.Rect("Icon", button.transform);
        icon.anchorMin = icon.anchorMax = new Vector2(.5f, .5f);
        icon.pivot = new Vector2(.5f, .5f);
        icon.sizeDelta = new Vector2(size, size);
        Image image = icon.gameObject.AddComponent<Image>();
        image.sprite = LoadRuntimeSprite(path);
        image.preserveAspect = true;
        image.raycastTarget = false;
        return button;
    }

    private WDUIPremiumButton PositionedIconButton(Transform parent, string name,
        string path, float x, float glyphSize, UnityEngine.Events.UnityAction action)
    {
        WDUIPremiumButton button = IconButton(parent, name, path, action, glyphSize);
        RectTransform rect = button.transform as RectTransform;
        AnchorTop(rect, x, 0f, 48f, 44f);
        return button;
    }

    private static void ConfigureBareControl(WDUIPremiumButton button)
    {
        if (button == null)
            return;
        button.SetPersistentColors(Color.clear, new Color(Gold.r, Gold.g, Gold.b, .10f));
        button.SetLabelColor(Gold);
        Transform border = button.transform.Find("Border");
        if (border != null)
            border.gameObject.SetActive(false);
    }

    private static void ConfigureTouchIcon(WDUIPremiumButton button, float size, Color color)
    {
        RectTransform rect = button.transform as RectTransform;
        rect.sizeDelta = new Vector2(size, size);
        LayoutElement layout = button.GetComponent<LayoutElement>();
        layout.minWidth = size;
        layout.preferredWidth = size;
        layout.minHeight = size;
        layout.preferredHeight = size;
        ConfigureBareControl(button);
        Image[] images = button.GetComponentsInChildren<Image>(true);
        foreach (Image image in images)
            if (image.transform.name == "Icon")
                image.color = color;
    }

    private void PresentSpeedSelection(bool accelerated)
    {
        speedButton.SetSelected(false);
        if (speedIcon != null)
        {
            speedIcon.sprite = accelerated ? speedOnSprite : speedOffSprite;
            speedIcon.color = Color.white;
            speedIcon.rectTransform.localScale = Vector3.one;
        }
    }

    private WDUIPremiumButton CompactButton(Transform parent, string name,
        string label, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = WDUIFactory.Rect(name, parent);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(RailContentWidth, CompactButtonHeight);
        WDUIPremiumButton button = rect.gameObject.AddComponent<WDUIPremiumButton>();
        button.Build(theme, label, action);
        button.SetPersistentColors(Color.clear,
            new Color(Gold.r, Gold.g, Gold.b, .18f));
        return button;
    }

    private static void SetBorderColor(RectTransform target, Color color)
    {
        Image border = target?.Find("Border")?.GetComponent<Image>();
        if (border != null)
            border.color = color;
    }

    private static void SetButtonRadius(WDUIPremiumButton button, int radius)
    {
        if (button == null)
            return;
        RectTransform rect = button.transform as RectTransform;
        Image surface = button.GetComponent<Image>();
        if (surface != null)
            WDHomeUIFactory.ApplyRounded(surface, radius);
        Transform previousBorder = rect.Find("Border");
        if (previousBorder != null)
            previousBorder.gameObject.SetActive(false);
        WDHomeUIFactory.AddRoundedBorder(rect, Gold, radius);
    }

    private WDUIPremiumButton Button(Transform parent, string label,
        UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = WDUIFactory.Rect(string.IsNullOrEmpty(label) ? "Button" : label, parent);
        WDUIPremiumButton button = rect.gameObject.AddComponent<WDUIPremiumButton>();
        button.Build(theme, label, action);
        return button;
    }

    private TextMeshProUGUI Text(Transform parent, string name, float size,
        Color color, Vector2 position, Vector2 dimensions,
        TextAlignmentOptions alignment)
    {
        TextMeshProUGUI text = WDUIFactory.Text(name, parent, theme, size, color, alignment);
        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        return text;
    }

    private static string PlayerContentKey(IReadOnlyList<MatchUIPlayerState> players)
    {
        if (players == null)
            return string.Empty;
        var key = new System.Text.StringBuilder(players.Count * 24);
        foreach (MatchUIPlayerState player in players)
            key.Append((int)player.Jogador).Append(':').Append(player.Nome).Append(':')
                .Append((int)player.Equipe).Append(':').Append(player.Cor).Append('|');
        return key.ToString();
    }

    private static void BuildAvatarGlow(RectTransform row, Color playerColor)
    {
        AddAvatarGlowLayer(row, "AvatarGlowOuter", 62f,
            new Color(playerColor.r, playerColor.g, playerColor.b, .12f));
        AddAvatarGlowLayer(row, "AvatarGlowInner", 58f,
            new Color(playerColor.r, playerColor.g, playerColor.b, .22f));
    }

    private static void AddAvatarGlowLayer(RectTransform row, string name,
        float size, Color color)
    {
        RectTransform glow = WDUIFactory.Rect(name, row);
        glow.anchorMin = glow.anchorMax = new Vector2(0f, .5f);
        glow.pivot = new Vector2(.5f, .5f);
        glow.anchoredPosition = new Vector2(27f, 0f);
        glow.sizeDelta = new Vector2(size, size);
        Image border = WDHomeUIFactory.AddRoundedBorder(glow, color, 10);
        border.raycastTarget = false;
    }

    private static void BuildAvatarFallback(RectTransform slot)
    {
        Color neutral = new Color32(190, 198, 208, 255);
        RectTransform head = WDUIFactory.Rect("AvatarHead", slot);
        head.anchorMin = head.anchorMax = new Vector2(.5f, .5f);
        head.pivot = new Vector2(.5f, .5f);
        head.anchoredPosition = new Vector2(0f, 8f);
        head.sizeDelta = new Vector2(13f, 13f);
        Image headImage = head.gameObject.AddComponent<Image>();
        headImage.color = neutral;
        headImage.raycastTarget = false;
        WDHomeUIFactory.ApplyRounded(headImage, 7);

        RectTransform shoulders = WDUIFactory.Rect("AvatarShoulders", slot);
        shoulders.anchorMin = shoulders.anchorMax = new Vector2(.5f, .5f);
        shoulders.pivot = new Vector2(.5f, .5f);
        shoulders.anchoredPosition = new Vector2(0f, -9f);
        shoulders.sizeDelta = new Vector2(27f, 14f);
        Image bodyImage = shoulders.gameObject.AddComponent<Image>();
        bodyImage.color = neutral;
        bodyImage.raycastTarget = false;
        WDHomeUIFactory.ApplyRounded(bodyImage, 7);
    }

    private static Sprite LoadRuntimeSprite(string path) =>
        RuntimeSprite(Resources.Load<Texture2D>(path));

    private static Sprite RuntimeSprite(Texture2D texture) => texture == null ? null :
        Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(.5f, .5f), 100f);

    private static void AnchorTop(RectTransform rect, float x, float y,
        float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void Stretch(RectTransform rect, float left = 0f,
        float right = 0f, float top = 0f, float bottom = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }
}
