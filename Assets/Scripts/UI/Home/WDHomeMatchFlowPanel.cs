using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public enum WDHomeMatchFlowScreen
{
    None,
    PlayHub,
    OfficialMatches,
    OfficialTournaments,
    FriendlyHub,
    FriendlyMatchesHub,
    FriendlyTournamentsHub,
    FriendlyRoomBrowser,
    FriendlyMatchCreate,
    FriendlyTournamentCreate,
    FriendlyMatchRoom,
    FriendlyTournamentCenter,
    CompetitiveFlow
}

[DisallowMultipleComponent]
public sealed class WDHomeMatchFlowPanel : MonoBehaviour
{
    private TMP_FontAsset gabaritoBlack;
    private TMP_FontAsset gabaritoMedium;
    private TMP_FontAsset gabaritoBold;
    private TMP_FontAsset gabaritoSemiBold;
    private TMP_FontAsset gabaritoExtraBold;
    private TMP_FontAsset geistMedium;

    private sealed class Option
    {
        public string Title { get; }
        public string Description { get; }
        public WDContentAvailability Availability { get; }
        public UnityAction Action { get; }
        public Sprite Thumbnail { get; }
        public bool Selected { get; }
        public string ActionLabel { get; }

        public Option(
            string title, string description, WDContentAvailability availability,
            UnityAction action, Sprite thumbnail = null, bool selected = false,
            string actionLabel = null)
        {
            Title = title;
            Description = description;
            Availability = availability;
            Action = action;
            Thumbnail = thumbnail;
            Selected = selected;
            ActionLabel = actionLabel;
        }
    }

    // ============================================================
    // 01. REFERÊNCIAS E NAVEGAÇÃO DO FLUXO ÚNICO
    // ============================================================

    private WarDominionUITheme theme;
    private WarDominionMatchFlowConfig config;
    private WarDominionHomeData homeData;
    private WDArmoryCatalogConfig armoryCatalog;
    private IWDArmoryRepository armoryRepository;
    private readonly Dictionary<string, string> mapNames = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DefinicaoMapa> mapDefinitions = new(StringComparer.Ordinal);
    private RectTransform optionsRoot;
    private RectTransform legacyPanel;
    private RectTransform playVisualRoot;
    private RectTransform playSafeArea;
    private RectTransform playMainPanel;
    private RectTransform playCardsRoot;
    private RectTransform playBackRect;
    private readonly List<TextMeshProUGUI> playCardDescriptions = new();
    private TextMeshProUGUI playTitleLabel;
    private TextMeshProUGUI playCoinsLabel;
    private TextMeshProUGUI playEmeraldsLabel;
    private TextMeshProUGUI playFeedbackLabel;
    private Texture2D playBackgroundTexture;
    private Texture2D matchmakingBackgroundTexture;
    private RawImage flowBackgroundImage;
    private AspectRatioFitter flowBackgroundCover;
    private RectTransform matchmakingOverlay;
    private WDHomeTournamentsPanel tournamentsPanel;
    private Action openNotifications;
    private Vector2 playPanelSize;
    private Vector2 playPanelOffset;
    private float playCardScale = 1f;
    private HorizontalLayoutGroup optionsLayout;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI subtitleLabel;
    private TextMeshProUGUI feedbackLabel;
    private WDUIPremiumButton backButton;
    private WDMatchModeDefinition selectedMode;
    private WDMatchSubmodeDefinition selectedSubmode;
    private Action backAction;
    private Action returnAfterVoting;
    private WDMapVoteController mapVote;
    private WDMatchmakingRequest voteRequest;
    private float voteTimeRemaining;
    private bool voteActive;
    private int selectedDeckIndex;
    private bool matchTransitionStarted;
    private WDPreMatchFlowController preMatchFlow;
    private WDMatchmakingRequest matchmakingRequest;
    private Action returnBeforeMatchmaking;
    private bool useCompactCenteredOptions;
    private bool friendlyPlaceholderOpen;
    private WDHomeMatchFlowScreen currentScreen;
    private WDNavigationStack navigationStack;
    private WDTournamentRepository tournamentRepository;
    private WDSocialRepository socialRepository;
    private WDProfileRepository profileRepository;
    private Action<string> openPublicProfile;
    private string selectedTournamentId;
    private string selectedTournamentTab = "overview";
    private string friendlyMode = "2V2";
    private string friendlyCardMode = "NORMAL";
    private string friendlyMap = "CLASSIC";
    private bool friendlyPrivateRoom;
    private bool friendlyInviteOnly;
    private readonly HashSet<string> friendlyInvitedPlayers = new(StringComparer.OrdinalIgnoreCase);

    private const float VoteDurationSeconds = 15f;
    private const string LocalPlayerId = "local_player";

    public bool IsOpen => gameObject.activeSelf;
    public bool IsFriendlyPlaceholderOpen => friendlyPlaceholderOpen;
    public WDHomeMatchFlowScreen CurrentScreen => currentScreen;
    public bool IsMatchmakingActive =>
        preMatchFlow != null && preMatchFlow.State == WDPreMatchState.Searching;

    public void Build(
        WarDominionUITheme newTheme, WarDominionMatchFlowConfig newConfig,
        WarDominionHomeData newHomeData, WDArmoryCatalogConfig newArmoryCatalog,
        IWDArmoryRepository newArmoryRepository,
        WDNavigationStack newNavigationStack = null,
        WDProfileRepository newProfileRepository = null,
        Action<string> publicProfileAction = null,
        Texture2D newPlayBackgroundTexture = null,
        Action newOpenNotifications = null,
        Texture2D newMatchmakingBackgroundTexture = null,
        TMP_FontAsset newGabaritoBlack = null,
        TMP_FontAsset newGabaritoMedium = null,
        TMP_FontAsset newGabaritoBold = null,
        TMP_FontAsset newGabaritoExtraBold = null,
        TMP_FontAsset newGeistMedium = null,
        WDSocialRepository newSocialRepository = null,
        TMP_FontAsset newGabaritoSemiBold = null,
        Texture2D newTournamentBackgroundTexture = null,
        Texture2D newTournamentOfflineArtwork = null,
        IWDTournamentConnectivity tournamentConnectivity = null)
    {
        theme = newTheme;
        config = newConfig;
        homeData = newHomeData;
        armoryCatalog = newArmoryCatalog;
        armoryRepository = newArmoryRepository;
        navigationStack = newNavigationStack;
        profileRepository = newProfileRepository ?? new WDProfileRepository(newHomeData);
        openPublicProfile = publicProfileAction;
        playBackgroundTexture = newPlayBackgroundTexture;
        matchmakingBackgroundTexture = newMatchmakingBackgroundTexture;
        gabaritoBlack = newGabaritoBlack;
        gabaritoMedium = newGabaritoMedium;
        gabaritoBold = newGabaritoBold;
        gabaritoSemiBold = newGabaritoSemiBold;
        gabaritoExtraBold = newGabaritoExtraBold;
        geistMedium = newGeistMedium;
        socialRepository = newSocialRepository;
        openNotifications = newOpenNotifications;
        tournamentRepository = new WDTournamentRepository(profileRepository);
        if (navigationStack == null)
        {
            navigationStack = new WDNavigationStack();
            navigationStack.SetRoot(new WDNavigationContext(
                "home", _ => HideFromNavigation()));
        }
        LoadMapNames();

        Image blocker = gameObject.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0.82f);
        blocker.raycastTarget = true;

        RectTransform panel = WDHomeUIFactory.Rect("MatchFlowPanel", transform);
        legacyPanel = panel;
        panel.anchorMin = new Vector2(0.06f, 0.15f);
        panel.anchorMax = new Vector2(0.94f, 0.85f);
        panel.offsetMin = panel.offsetMax = Vector2.zero;
        Image surface = panel.gameObject.AddComponent<Image>();
        surface.color = theme.BackgroundElevated;
        surface.raycastTarget = true;
        Outline outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = theme.Acento;
        outline.effectDistance = new Vector2(2f, -2f);

        titleLabel = WDHomeUIFactory.Text(
            "Title", panel, theme, string.Empty, theme.TypeTitle,
            theme.TextoPrincipal, TextAlignmentOptions.Center);
        titleLabel.fontStyle = FontStyles.Bold;
        titleLabel.rectTransform.anchorMin = new Vector2(0f, 0.84f);
        titleLabel.rectTransform.anchorMax = new Vector2(1f, 0.98f);
        titleLabel.rectTransform.offsetMin = new Vector2(120f, 0f);
        titleLabel.rectTransform.offsetMax = new Vector2(-120f, 0f);

        subtitleLabel = WDHomeUIFactory.Text(
            "Subtitle", panel, theme, string.Empty, theme.TypeBody,
            theme.TextoSecundario, TextAlignmentOptions.Center);
        subtitleLabel.rectTransform.anchorMin = new Vector2(0f, 0.75f);
        subtitleLabel.rectTransform.anchorMax = new Vector2(1f, 0.84f);
        subtitleLabel.rectTransform.offsetMin = new Vector2(80f, 0f);
        subtitleLabel.rectTransform.offsetMax = new Vector2(-80f, 0f);

        optionsRoot = WDHomeUIFactory.Rect("Options", panel);
        optionsRoot.anchorMin = new Vector2(0.035f, 0.24f);
        optionsRoot.anchorMax = new Vector2(0.965f, 0.73f);
        optionsRoot.offsetMin = optionsRoot.offsetMax = Vector2.zero;
        optionsLayout = optionsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
        optionsLayout.spacing = 18f;
        optionsLayout.childControlWidth = true;
        optionsLayout.childControlHeight = true;
        optionsLayout.childForceExpandWidth = true;
        optionsLayout.childForceExpandHeight = true;

        feedbackLabel = WDHomeUIFactory.Text(
            "Feedback", panel, theme, string.Empty, theme.TypeBody,
            theme.Warning, TextAlignmentOptions.Center);
        feedbackLabel.rectTransform.anchorMin = new Vector2(0.08f, 0.11f);
        feedbackLabel.rectTransform.anchorMax = new Vector2(0.92f, 0.22f);
        feedbackLabel.rectTransform.offsetMin = feedbackLabel.rectTransform.offsetMax = Vector2.zero;

        backButton = BuildUtilityButton(panel, "Back", "VOLTAR", new Vector2(0f, 0f), Back);
        BuildUtilityButton(panel, "Close", "X", new Vector2(1f, 0f), RequestNavigationBack);
        if (playBackgroundTexture != null)
            BuildPlayVisual();
        // 01.1. A apresentação dedicada compartilha o domínio e a pilha de
        // navegação existentes; somente a camada visual de Torneios é isolada.
        RectTransform tournamentsLayer = WDHomeUIFactory.Rect("TournamentsLayer", transform);
        WDHomeUIFactory.Stretch(tournamentsLayer);
        tournamentsPanel = tournamentsLayer.gameObject.AddComponent<WDHomeTournamentsPanel>();
        tournamentsPanel.Build(theme, tournamentRepository,
            () => armoryRepository?.Load(armoryCatalog), openNotifications,
            OpenTournament, RequestNavigationBack,
            newTournamentBackgroundTexture != null ? newTournamentBackgroundTexture : playBackgroundTexture,
            newTournamentOfflineArtwork, gabaritoBold, geistMedium,
            tournamentConnectivity);
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (preMatchFlow != null && preMatchFlow.State == WDPreMatchState.Searching)
        {
            preMatchFlow.Tick(Time.unscaledDeltaTime);
            feedbackLabel.text = $"TEMPO DE ESPERA: {FormatElapsed(preMatchFlow.WaitSeconds)}";
            if (matchmakingOverlay != null && matchmakingOverlay.gameObject.activeSelf)
            {
                Transform timer = matchmakingOverlay.Find("SearchTimer");
                if (timer != null && timer.TryGetComponent(out TextMeshProUGUI label))
                    label.text = FormatElapsed(preMatchFlow.WaitSeconds);
            }
        }

        if (voteActive)
        {
            voteTimeRemaining = Mathf.Max(0f, voteTimeRemaining - Time.unscaledDeltaTime);
            feedbackLabel.text = $"TEMPO: {Mathf.CeilToInt(voteTimeRemaining):00}s";
            if (matchmakingOverlay != null && matchmakingOverlay.gameObject.activeSelf)
            {
                Transform countdown = matchmakingOverlay.Find("PrematchModal/VoteCountdown");
                if (countdown != null && countdown.TryGetComponent(out TextMeshProUGUI label))
                    label.text = $"00:{Mathf.CeilToInt(voteTimeRemaining):00}";
            }
            if (voteTimeRemaining <= 0f)
                FinishVoting();
        }
    }

    // ============================================================
    // 02. TELAS DATA-DRIVEN DE MODO E SUBMODO
    // ============================================================

    public void Open()
    {
        if (IsOpen)
            return;

        NavigateTo("play", RestorePlayHub);
    }

    public void Close()
    {
        if (navigationStack != null && navigationStack.CanGoBack)
        {
            navigationStack.ResetToRoot();
            return;
        }

        HideFromNavigation();
    }

    public void HideFromNavigation()
    {
        if (!matchTransitionStarted &&
            (preMatchFlow != null || voteActive || voteRequest != null || matchmakingRequest != null))
            WDMatchSetupContext.Clear();
        selectedMode = null;
        selectedSubmode = null;
        voteActive = false;
        mapVote = null;
        voteRequest = null;
        returnAfterVoting = null;
        preMatchFlow = null;
        matchmakingRequest = null;
        returnBeforeMatchmaking = null;
        selectedDeckIndex = 0;
        matchTransitionStarted = false;
        friendlyPlaceholderOpen = false;
        currentScreen = WDHomeMatchFlowScreen.None;
        backAction = null;
        tournamentsPanel?.Hide();
        gameObject.SetActive(false);
    }

    public void HandleEscape()
    {
        if (!TryHandleLocalBack())
            RequestNavigationBack();
    }

    public bool TryHandleLocalBack()
    {
        if (currentScreen == WDHomeMatchFlowScreen.OfficialTournaments &&
            tournamentsPanel != null && tournamentsPanel.TryHandleCenterBack())
            return true;
        if (IsFriendlyScreen(currentScreen) && matchmakingOverlay != null &&
            matchmakingOverlay.gameObject.activeSelf)
        {
            CloseFriendlyModal();
            return true;
        }
        if (currentScreen != WDHomeMatchFlowScreen.CompetitiveFlow || backAction == null)
            return false;

        Back();
        return true;
    }

    private static bool IsFriendlyScreen(WDHomeMatchFlowScreen screen) =>
        screen >= WDHomeMatchFlowScreen.FriendlyHub &&
        screen <= WDHomeMatchFlowScreen.FriendlyTournamentCenter;

    // ============================================================
    // 02. HUB PLAY E RAMOS DE PRIMEIRO NÍVEL
    // ============================================================

    private void ShowPlayHub()
    {
        ResetNavigationState(WDHomeMatchFlowScreen.PlayHub);
        var options = new List<Option>
        {
            new("TORNEIOS", "COMPETIÇÕES OFICIAIS DO WAR DOMINION",
                WDContentAvailability.Available,
                () => NavigateTo("play/tournaments", ShowOfficialTournaments),
                actionLabel: "ACESSAR"),
            new("PARTIDAS", "ESCOLHA SEU MODO DE BATALHA E ENTRE EM COMBATE",
                WDContentAvailability.Available,
                () => NavigateTo("play/matches", ShowModes),
                actionLabel: "ACESSAR"),
            new("AMISTOSOS", "PARTIDAS E TORNEIOS PERSONALIZADOS",
                WDContentAvailability.Available,
                () => NavigateTo("play/friendly", ShowFriendlyHub),
                actionLabel: "ACESSAR")
        };

        ShowOptions("PLAY", "Escolha como você quer jogar", options, RequestNavigationBack,
            string.Empty, true, true);
    }

    private void ShowModes()
    {
        ResetNavigationState(WDHomeMatchFlowScreen.OfficialMatches);
        var options = new List<Option>();
        foreach (WDMatchModeDefinition mode in config.Modes)
        {
            if (mode == null || !mode.ShowInStandardPlaySelection ||
                !WDContentAvailabilityRules.IsVisible(mode.Availability))
                continue;
            WDMatchModeDefinition captured = mode;
            options.Add(new Option(
                mode.DisplayName, mode.Description, mode.Availability,
                () => SelectMode(captured)));
        }

        ShowOptions(
            "PARTIDAS", "Escolha seu modo de batalha", options,
            RequestNavigationBack, string.Empty, true, true);
    }

    private void ShowOfficialTournaments()
    {
        ResetNavigationState(WDHomeMatchFlowScreen.OfficialTournaments);
        legacyPanel.gameObject.SetActive(false);
        playVisualRoot?.gameObject.SetActive(false);
        tournamentsPanel.Show();
    }

    // ============================================================
    // 03. CENTRAL E DETALHE DO TORNEIO OFICIAL
    // ============================================================

    private void OpenTournament(string tournamentId)
    {
        selectedTournamentId = tournamentId;
        selectedTournamentTab = "overview";
        ShowTournamentDetail();
    }

    private void ShowTournamentDetail()
    {
        WDTournament tournament = tournamentRepository.Find(selectedTournamentId);
        if (tournament == null)
        {
            ShowStatus("TORNEIO INDISPONÍVEL", "Os dados locais deste torneio não foram encontrados.",
                RequestNavigationBack);
            return;
        }
        if (selectedTournamentTab == "participants") ShowTournamentParticipants(tournament);
        else if (selectedTournamentTab == "bracket") ShowTournamentBracket(tournament);
        else ShowTournamentOverview(tournament);
    }

    private void ShowTournamentOverview(WDTournament tournament)
    {
        selectedTournamentTab = "overview";
        WDTournamentParticipation state = tournament.ParticipationOf(profileRepository.LocalProfile.PlayerId);
        WDTournamentMatch next = tournamentRepository.NextMatchFor(tournament.Id,
            profileRepository.LocalProfile.PlayerId);
        var cards = new List<WDTournamentCenterCard>
        {
            new("VISÃO GERAL", "FORMATO, REGRAS, SITUAÇÃO E PRÓXIMA PARTIDA", "ABRIR",
                () => { selectedTournamentTab = "overview"; ShowTournamentDetail(); }, true),
            new("PARTICIPANTES", "JOGADORES OU EQUIPES INSCRITAS", "ABRIR",
                () => { selectedTournamentTab = "participants"; ShowTournamentDetail(); }),
            new("CHAVEAMENTO", "MATA-MATA · ELIMINAÇÃO SIMPLES", "ABRIR",
                () => { selectedTournamentTab = "bracket"; ShowTournamentDetail(); }),
            new("ESCOLHER DECK", "SELEÇÃO DE DECK PARA A PRÓXIMA PARTIDA", "ESCOLHER",
                tournament.DeckSelectionIntentAvailable ? () => ShowTournamentDeckIntent(tournament) : null,
                enabled: tournament.DeckSelectionIntentAvailable),
            new("SUA PRÓXIMA PARTIDA", next == null ? "NENHUMA PARTIDA DISPONÍVEL" :
                $"{EntryName(tournament, next.FirstEntryId)} VS {EntryName(tournament, next.SecondEntryId)} · " +
                $"{(string.IsNullOrEmpty(next.EntryWindow) ? "JANELA A DEFINIR" : next.EntryWindow)}",
                "ENTRAR", next == null ? null : () => ShowTournamentMatchIntent(tournament), enabled: next != null),
            new("ASSISTIR", "SPECTATOR AINDA NÃO IMPLEMENTADO", "INDISPONÍVEL", null, enabled: false)
        };
        string summary = $"{FormatLabel(tournament.Format)} · {ModeLabel(tournament.CardMode)} · {tournament.MapRule} · " +
            $"{tournament.Entries.Count}/{tournament.Capacity} PARTICIPANTES/EQUIPES · {tournament.Period}\n" +
            $"REGRAS: {tournament.Rules}";
        UnityAction registration = tournament.Lifecycle == WDTournamentLifecycle.RegistrationOpen
            ? () => ConfirmTournamentRegistration(tournament) : null;
        tournamentsPanel.ShowCenter(tournament.Name,
            $"TORNEIO OFICIAL · {LifecycleLabel(tournament.Lifecycle)} · {ParticipationLabel(state)}",
            summary, cards, tournamentsPanel.HideCenter, null, null,
            registration, registration == null ? null : "CONFIRMAR INSCRIÇÃO");
    }

    private void ShowTournamentParticipants(WDTournament tournament)
    {
        selectedTournamentTab = "participants";
        var cards = new List<WDTournamentCenterCard>();
        foreach (WDTournamentEntry entry in tournament.Entries.Take(5))
        {
            string members = string.Join(" · ", entry.MemberPlayerIds.Select(id =>
                profileRepository.Find(id)?.Nickname ?? id));
            string firstPlayer = entry.MemberPlayerIds.FirstOrDefault();
            cards.Add(new WDTournamentCenterCard(entry.DisplayName, members,
                string.IsNullOrEmpty(firstPlayer) ? string.Empty : "VER PERFIL",
                string.IsNullOrEmpty(firstPlayer) ? null : () => openPublicProfile?.Invoke(firstPlayer),
                enabled: !string.IsNullOrEmpty(firstPlayer)));
        }
        tournamentsPanel.ShowCenter(tournament.Name,
            "PARTICIPANTES · SELECIONE UM JOGADOR PARA O PERFIL PÚBLICO",
            $"{tournament.Entries.Count}/{tournament.Capacity} PARTICIPANTES OU EQUIPES",
            cards, tournamentsPanel.HideCenter, () => ShowTournamentOverview(tournament),
            tournament.IsReadOnly ? "HISTÓRICO SOMENTE LEITURA" : string.Empty);
    }

    private void ShowTournamentBracket(WDTournament tournament)
    {
        selectedTournamentTab = "bracket";
        var cards = new List<WDTournamentCenterCard>();
        foreach (WDTournamentMatch match in tournament.Matches.Take(7))
        {
            string result = match.State == WDTournamentMatchState.Completed
                ? $"VENCEDOR: {EntryName(tournament, match.WinnerEntryId)} · {match.Score}"
                : MatchStateLabel(match.State);
            cards.Add(new WDTournamentCenterCard(
                $"RODADA {match.RoundIndex + 1} · JOGO {match.SlotIndex + 1}",
                $"{EntryName(tournament, match.FirstEntryId)} VS {EntryName(tournament, match.SecondEntryId)} · {result}",
                result, null, enabled: false));
        }
        tournamentsPanel.ShowCenter(tournament.Name,
            "CHAVEAMENTO DATA-DRIVEN · ELIMINAÇÃO SIMPLES",
            tournament.IsReadOnly ? "RESULTADO FINAL · SOMENTE LEITURA" :
                "PRAZO EXPIRADO NÃO ATRIBUI WO AUTOMATICAMENTE",
            cards, tournamentsPanel.HideCenter, () => ShowTournamentOverview(tournament));
    }

    private void ConfirmTournamentRegistration(WDTournament tournament)
    {
        string local = profileRepository.LocalProfile.PlayerId;
        WDTournamentRegistrationResult result = tournament.TeamSize == 1
            ? tournamentRepository.Register(tournament.Id, local)
            : WDTournamentRegistrationResult.IncompleteTeam;
        ShowTournamentDetail();
        string registrationFeedback = result == WDTournamentRegistrationResult.Success
            ? "INSCRIÇÃO CONFIRMADA." : result == WDTournamentRegistrationResult.IncompleteTeam
                ? "INSCRIÇÃO REQUER EQUIPE COMPLETA JÁ FORMADA. MATCHMAKING NÃO É USADO."
                : $"INSCRIÇÃO NÃO REALIZADA: {result}.";
        tournamentsPanel.ShowCenterNotice("INSCRIÇÃO", registrationFeedback,
            ShowTournamentDetail, tournamentsPanel.HideCenter);
    }

    private void ShowTournamentMatchIntent(WDTournament tournament)
    {
        tournamentsPanel.ShowCenterNotice("ENTRAR NA PARTIDA",
            tournament.DeckSelectionIntentAvailable
                ? "Intenção registrada. A seleção de deck usará a infraestrutura existente quando a integração oficial estiver disponível. Nenhuma partida foi iniciada."
                : "Este modo usa Random Cards. Nenhuma partida foi iniciada.",
            ShowTournamentDetail, tournamentsPanel.HideCenter);
    }

    private void ShowTournamentDeckIntent(WDTournament tournament)
    {
        tournamentsPanel.ShowCenterNotice("ESCOLHER DECK",
            "A intenção foi reconhecida. O torneio reutilizará a infraestrutura existente de decks quando o lock definitivo da partida for integrado. Nenhum deck ou gameplay foi alterado.",
            ShowTournamentDetail, tournamentsPanel.HideCenter);
    }

    private void ShowTournamentSpectatorIntent()
    {
        tournamentsPanel.ShowCenterNotice("ASSISTIR",
            "Spectator real não está disponível no protótipo. Nenhuma câmera, conexão ou partida foi criada.",
            ShowTournamentDetail, tournamentsPanel.HideCenter);
    }

    private string EntryName(WDTournament tournament, string entryId) =>
        tournament.Entries.FirstOrDefault(entry => entry.Id == entryId)?.DisplayName ?? "PENDENTE";
    private static string FormatLabel(WDTournamentFormat value) => value switch
    { WDTournamentFormat.OneVsOne => "1V1", WDTournamentFormat.TwoVsTwo => "2V2", _ => "3V3" };
    private static string ModeLabel(WDTournamentCardMode value) =>
        value == WDTournamentCardMode.Normal ? "NORMAL" : "RANDOM CARDS";
    private static string LifecycleLabel(WDTournamentLifecycle value) => value switch
    { WDTournamentLifecycle.ComingSoon => "EM BREVE", WDTournamentLifecycle.RegistrationOpen => "INSCRIÇÕES ABERTAS", WDTournamentLifecycle.InProgress => "EM ANDAMENTO", _ => "ENCERRADO" };
    private static string ParticipationLabel(WDTournamentParticipation value) => value switch
    { WDTournamentParticipation.NotRegistered => "NÃO INSCRITO", WDTournamentParticipation.Registered => "INSCRITO", WDTournamentParticipation.Competing => "EM DISPUTA", WDTournamentParticipation.Qualified => "CLASSIFICADO", WDTournamentParticipation.Eliminated => "ELIMINADO", _ => "CAMPEÃO" };
    private static string MatchStateLabel(WDTournamentMatchState value) => value switch
    { WDTournamentMatchState.Next => "PRÓXIMA", WDTournamentMatchState.InProgress => "EM ANDAMENTO", WDTournamentMatchState.Completed => "CONCLUÍDA", _ => "PENDENTE" };

    // ============================================================
    // 08. COMPOSICAO FIGMA 01B SOBRE O FLUXO PLAY EXISTENTE
    // ============================================================

    private static readonly Color PlayGold = new Color(0.773f, 0.608f, 0.388f, 1f);
    private static readonly Color PlayWhite = new Color(0.961f, 0.965f, 0.973f, 1f);
    private static readonly Color PlayMuted = new Color(0.604f, 0.616f, 0.639f, 1f);
    private static readonly Color PlayPanel = new Color(11f / 255f, 14f / 255f, 18f / 255f, 0.82f);
    private static readonly Color PlayCard = new Color(18f / 255f, 22f / 255f, 28f / 255f, 0.70f);
    // 08.1. Cabeçalho, conteúdo e ação das tabelas de AMISTOSOS usam a mesma grade.
    // As extremidades preservam 30 px de margem dentro das linhas de 1440 px.
    private static readonly float[] FriendlyTableColumnX =
        { -530f, -265f, -55f, 150f, 365f, 620f };
    private static readonly float[] FriendlyTableColumnWidth =
        { 320f, 190f, 120f, 140f, 210f, 140f };

    private void BuildPlayVisual()
    {
        playVisualRoot = WDHomeUIFactory.Rect("FigmaPlayVisual", transform);
        WDHomeUIFactory.Stretch(playVisualRoot);
        Image blocker = playVisualRoot.gameObject.AddComponent<Image>();
        blocker.color = Color.black;
        blocker.raycastTarget = true;

        RectTransform background = WDHomeUIFactory.Rect("PlayBackground", playVisualRoot);
        background.anchorMin = background.anchorMax = new Vector2(0.5f, 0.5f);
        background.pivot = new Vector2(0.5f, 0.5f);
        background.anchoredPosition = Vector2.zero;
        flowBackgroundImage = background.gameObject.AddComponent<RawImage>();
        flowBackgroundImage.texture = playBackgroundTexture;
        flowBackgroundImage.raycastTarget = false;
        flowBackgroundCover = background.gameObject.AddComponent<AspectRatioFitter>();
        flowBackgroundCover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        flowBackgroundCover.aspectRatio = (float)playBackgroundTexture.width / playBackgroundTexture.height;

        Image veil = WDHomeUIFactory.Image("PlayVeil", playVisualRoot,
            new Color(0.043f, 0.047f, 0.055f, 0.15f));
        WDHomeUIFactory.Stretch(veil.rectTransform);
        veil.raycastTarget = false;

        playSafeArea = WDHomeUIFactory.Rect("PlaySafeArea", playVisualRoot);
        WDHomeSecondaryScreenFrame.Configure(playSafeArea);
        BuildPlayHeader();

        playMainPanel = WDHomeUIFactory.Rect("PlayModePanel", playSafeArea);
        playMainPanel.anchorMin = playMainPanel.anchorMax = new Vector2(0.5f, 0.5f);
        playMainPanel.pivot = new Vector2(0.5f, 0.5f);
        Image surface = playMainPanel.gameObject.AddComponent<Image>();
        surface.color = PlayPanel;
        surface.raycastTarget = true;
        WDHomeUIFactory.ApplyRounded(surface, 28);
        AddPlayBorder(playMainPanel, PlayGold);

        playCardsRoot = WDHomeUIFactory.Rect("PlayCards", playMainPanel);
        WDHomeUIFactory.Stretch(playCardsRoot, 48f, 30f, 48f, 30f);
        HorizontalLayoutGroup cards = playCardsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
        cards.spacing = 28f;
        cards.childAlignment = TextAnchor.MiddleCenter;
        cards.childControlWidth = true;
        cards.childControlHeight = true;
        cards.childForceExpandWidth = true;
        cards.childForceExpandHeight = false;
        playFeedbackLabel = PlayText("PlayFeedback", playSafeArea, string.Empty,
            17f, PlayGold, new Vector2(0.5f, 0f), new Vector2(0f, 72f),
            new Vector2(1100f, 36f));
        matchmakingOverlay = WDHomeUIFactory.Rect("MatchmakingOverlay", playSafeArea);
        WDHomeUIFactory.Stretch(matchmakingOverlay);
        matchmakingOverlay.gameObject.SetActive(false);
        playVisualRoot.gameObject.SetActive(false);
    }

    private void BuildPlayHeader()
    {
        // 08.2. PLAY, Partidas, Amistosos e Torneios compartilham o mesmo
        // Header do Arsenal; cada fluxo altera somente título, ação e dados.
        WDUniversalHeaderContent header = WDUniversalHeaderFactory.BuildInset(
            playSafeArea, theme, string.Empty, Back,
            openNotifications == null ? null : () => openNotifications.Invoke());
        playTitleLabel = header.Title;
        playBackRect = header.BackButton.transform as RectTransform;
        playCoinsLabel = header.CoinValue;
        playEmeraldsLabel = header.GemValue;
    }

    private void RenderPlayVisual(string title, string subtitle, IReadOnlyList<Option> options)
    {
        bool primaryHub = currentScreen == WDHomeMatchFlowScreen.PlayHub;
        bool friendlyHub = currentScreen == WDHomeMatchFlowScreen.FriendlyHub ||
            currentScreen == WDHomeMatchFlowScreen.FriendlyMatchesHub ||
            currentScreen == WDHomeMatchFlowScreen.FriendlyTournamentsHub;
        bool hub = primaryHub || friendlyHub;
        bool matches = currentScreen == WDHomeMatchFlowScreen.OfficialMatches;
        bool team = options.Count > 0 && options[0].Title == "EQUIPE ALEATÓRIA";
        Vector2 canonicalPanelSize = friendlyHub ? new Vector2(960f, 500f) :
            hub ? new Vector2(1520f, 596f) :
            matches ? new Vector2(1696f, 516f) : new Vector2(1415f, 596f);
        playCardScale = WDHomeMobileLayout.PlayScale(canonicalPanelSize);
        playPanelSize = canonicalPanelSize * playCardScale;
        // 08.1. Os painéis canônicos do Figma têm centro em Y=540 (Hub=544).
        // O ganho ergonômico mobile continua sendo aplicado sobre essa base.
        playPanelOffset = team ? Vector2.zero :
            new Vector2(0f, (hub ? -4f : 0f) +
                WDHomeMobileLayout.PlayMobileLift);
        flowBackgroundImage.texture = playBackgroundTexture;
        flowBackgroundCover.aspectRatio = (float)playBackgroundTexture.width / playBackgroundTexture.height;
        matchmakingOverlay.gameObject.SetActive(false);
        playMainPanel.gameObject.SetActive(true);
        playCardsRoot.gameObject.SetActive(true);
        ClearFriendlyCanvas();
        playBackRect.gameObject.SetActive(backAction != null);
        float horizontalInset = (hub || matches ? 36f : 48f) * playCardScale;
        float verticalInset = (friendlyHub ? 30f : hub ? 38f : matches ? 28f : 34f) * playCardScale;
        playCardsRoot.offsetMin = new Vector2(horizontalInset, verticalInset);
        playCardsRoot.offsetMax = new Vector2(-horizontalInset, -verticalInset);
        HorizontalLayoutGroup cardLayout = playCardsRoot.GetComponent<HorizontalLayoutGroup>();
        cardLayout.spacing = (friendlyHub ? 34f : hub ? 48f : matches ? 28f : 94f) * playCardScale;
        cardLayout.childForceExpandWidth = hub || matches;
        playTitleLabel.text = team ? "SELEÇÃO DE EQUIPE" : title;
        playTitleLabel.gameObject.SetActive(true);
        playFeedbackLabel.text = string.Empty;
        WDArmoryProfile wallet = armoryRepository?.Load(armoryCatalog);
        // 08.3. Atualiza somente os dados das cápsulas universais. A geometria
        // permanece sob autoridade do header canônico (96 x 32 e gaps de 10 px).
        playCoinsLabel.text = WDNumberFormatter.Full(wallet?.Coins ?? 0);
        playEmeraldsLabel.text = WDNumberFormatter.Full(wallet?.Emeralds ?? 0);

        for (int i = playCardsRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = playCardsRoot.GetChild(i).gameObject;
            child.SetActive(false);
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
        playCardDescriptions.Clear();
        for (int i = 0; i < options.Count; i++)
            BuildPlayCard(options[i], hub, matches, team, i, friendlyHub);
        UpdatePlaySafeArea();
    }

    // ============================================================
    // 09. CARDS GUIADOS PELO CATALOGO E ADAPTACAO CONTINUA
    // ============================================================

    private void BuildPlayCard(Option option, bool hub, bool matches, bool team, int index,
        bool friendlyHub = false)
    {
        // 09.1. Uma escala limitada pelo painel e pela Safe Area calibra toda
        // a família de cards sem mudar conteúdo, destinos ou arte aprovada.
        float s = playCardScale;
        bool available = WDContentAvailabilityRules.IsAvailable(option.Availability);
        RectTransform card = WDHomeUIFactory.Rect(option.Title.Replace(" ", string.Empty), playCardsRoot);
        LayoutElement layout = card.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = (friendlyHub ? 410f : hub && index == 1 ? 520f : hub ? 480f :
            matches ? 460f : 527f) * s;
        layout.preferredWidth = (friendlyHub ? 410f : hub ? index == 1 ? 520f : 416f :
            matches ? 385f : 338f) * s;
        layout.flexibleWidth = hub ? index == 1 ? 1.25f : 1f :
            matches ? 1f : 0f;
        Color tint = PlayCard;
        PlaySurface(card, tint, 24,
            available ? PlayGold : new Color(0.52f, 0.47f, 0.4f, 0.72f));
        Button button = card.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.interactable = available && option.Action != null;
        if (button.interactable) button.onClick.AddListener(option.Action);

        string iconName = PlayCardIcon(option.Title, hub, matches, team);
        iconName = ResolvePlayIconState(iconName, option.Availability);
        if (!string.IsNullOrEmpty(iconName))
        {
            float iconY = (friendlyHub ? -92f : hub ? -150f : matches ? -105f : -130f) * s;
            float iconFrameSize = (friendlyHub ? 72f : hub ? index == 1 ? 120f : 100f : 96f) * s;
            RectTransform iconFrame = PlayRect("IconFrame", card,
                new Vector2(0.5f, 1f), new Vector2(0f, iconY),
                new Vector2(iconFrameSize, iconFrameSize),
                new Vector2(0.5f, 0.5f));
            if (hub)
                PlaySurface(iconFrame,
                    new Color(PlayGold.r, PlayGold.g, PlayGold.b, 0.2f),
                    48, PlayGold);
            float iconSize = (friendlyHub ? 34f : hub ? index == 1 ? 56f : index == 0 ? 66f : 64f :
                option.Title == "BATALHA 2V2" ? 128f : 96f) * s;
            PlayIcon(iconFrame, iconName, iconSize);
        }

        float titleSize = (friendlyHub ? 25f : hub ? index == 1 ? 40f : 34f :
            matches ? 48f : team ? 30f : index == 0 ? 48f : 40f) * s;
        float titleY = (friendlyHub ? -160f : hub ? -245f : matches ? -195f : team ? -235f : -230f) * s;
        TextMeshProUGUI name = PlayText("Title", card, option.Title, titleSize,
            available ? PlayWhite : new Color(0.76f, 0.76f, 0.76f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, titleY),
            new Vector2((hub || matches ? 350f : 310f) * s, 82f * s));
        name.fontStyle = FontStyles.Normal;
        name.enableAutoSizing = true;
        name.fontSizeMin = 21f * s;
        name.fontSizeMax = titleSize;
        float descriptionY = (friendlyHub ? -220f : hub ? -330f : matches ? -265f : -320f) * s;
        TextMeshProUGUI description = PlayText("Description", card,
            option.Description.ToUpperInvariant(), WDHomeMobileLayout.DescriptionSize(hub) * s,
            PlayMuted, new Vector2(0.5f, 1f), new Vector2(0f, descriptionY),
            new Vector2(WDHomeMobileLayout.DescriptionWidth(hub, index == 1) * s, 78f * s));
        description.font = hub ? geistMedium : gabaritoMedium;
        description.textWrappingMode = TextWrappingModes.Normal;
        description.overflowMode = TextOverflowModes.Ellipsis;
        playCardDescriptions.Add(description);

        if (option.Availability == WDContentAvailability.ComingSoon)
        {
            RectTransform lockHolder = PlayRect("Lock", card,
                new Vector2(0.5f, 1f),
                new Vector2(0f, (matches ? -350f : -440f) * s),
                new Vector2(54f * s, 54f * s));
            string lockAsset = option.Title == "BATALHA 3V3" ||
                option.Title == "DECKBUILDER"
                ? "PlayFinal/ThreeVsThreeLock"
                : matches ? "PlayModeLock" : "PlayDeckLock";
            PlayIcon(lockHolder, lockAsset, 54f * s);
            float availabilitySize = option.Title == "BATALHA 3V3" ? 24f :
                option.Title == "DECKBUILDER" ? 20f : 19f;
            float availabilityY = option.Title == "DECKBUILDER" ? -492f :
                matches ? -415f : -28f;
            Color availabilityColor = option.Title == "DECKBUILDER" ||
                option.Title == "BATALHA 3V3" ? PlayMuted : PlayGold;
            TextMeshProUGUI availability = PlayText("Availability", card, "EM BREVE", availabilitySize * s, availabilityColor,
                new Vector2(0.5f, 1f),
                new Vector2(0f, availabilityY * s),
                new Vector2(220f * s, 35f * s));
            if (option.Title == "BATALHA 3V3" && gabaritoSemiBold != null)
                availability.font = gabaritoSemiBold;
            else if (option.Title == "DECKBUILDER" && gabaritoMedium != null)
                availability.font = gabaritoMedium;
        }
        if (hub && !string.IsNullOrWhiteSpace(option.ActionLabel))
        {
            RectTransform action = PlayRect("Action", card,
                new Vector2(0.5f, 0f), new Vector2(0f, 65f * s),
                new Vector2(300f * s, 56f * s));
            PlaySurface(action, PlayGold, 12, PlayGold);
            Button actionButton = action.gameObject.AddComponent<Button>();
            actionButton.onClick.AddListener(option.Action);
            PlayText("Label", action, option.ActionLabel, 18f * s,
                new Color(0.043f, 0.047f, 0.055f, 1f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(290f * s, 46f * s));
        }
    }

    // Variantes exportadas do Figma: preservar a arte de cada estado sem tint.
    private static readonly IReadOnlyDictionary<string, string> AvailablePlayIcons =
        new Dictionary<string, string>
        {
            ["PlayFinal/ThreeVsThreeHelmet"] = "PlayFinal/ThreeVsThreeHelmetActive"
        };

    private static string ResolvePlayIconState(string asset, WDContentAvailability availability) =>
        asset != null && availability == WDContentAvailability.Available &&
        AvailablePlayIcons.TryGetValue(asset, out string active) ? active : asset;

    private static string PlayCardIcon(string title, bool hub, bool matches, bool team)
    {
        if (hub)
            return title == "TORNEIOS" ? "PlayFinal/TournamentTrophy" :
                title == "PARTIDAS" ? "PlayFinal/PlayCrossedSwords" :
                title == "AMISTOSOS" ? "PlayFinal/FriendliesGroupInvite" :
                title == "TORNEIOS AMISTOSOS" || title == "CRIAR TORNEIO" ? "PlayFinal/TournamentTrophy" :
                title == "CRIAR PARTIDA" ? "PlayFinal/PlayCrossedSwords" :
                title == "SALAS CRIADAS" || title == "SALAS DE TORNEIOS" ? "PlayFinal/FriendliesGroupInvite" : null;
        if (matches)
            return title == "BATALHA" ? "PlayFinal/FfaTarget" :
                title == "BATALHA 1V1" ? "PlayFinal/OneVsOneSwords" :
                title == "BATALHA 2V2" ? "PlayFinal/TwoVsTwo" :
                title == "BATALHA 3V3" ? "PlayFinal/ThreeVsThreeHelmet" : null;
        // Submodos e seleção de equipe não têm pictograma nas pranchas aprovadas.
        return null;
    }

    private void LateUpdate()
    {
        if (playVisualRoot != null && playVisualRoot.gameObject.activeSelf)
            UpdatePlaySafeArea();
    }

    private void UpdatePlaySafeArea()
    {
        if (playSafeArea == null || Screen.width <= 0 || Screen.height <= 0)
            return;
        // 09.1. PLAY e Torneios usam o mesmo frame externo das telas secundárias.
        // O conteúdo e o domínio permanecem independentes deste enquadramento.
        playSafeArea.GetComponent<WDHomeSecondaryScreenFrame>()?.RefreshNow();
        playMainPanel.sizeDelta = new Vector2(
            Mathf.Min(playPanelSize.x, Mathf.Max(0f, playSafeArea.rect.width - 128f)),
            Mathf.Min(playPanelSize.y, Mathf.Max(0f, playSafeArea.rect.height - 180f)));
        playMainPanel.anchoredPosition = playPanelOffset;
        // 09.2. O texto acompanha a largura real de cada card depois do layout.
        // A escala dos demais elementos e a composição 16:9 permanecem canônicas.
        bool hub = currentScreen == WDHomeMatchFlowScreen.PlayHub ||
            currentScreen == WDHomeMatchFlowScreen.FriendlyHub ||
            currentScreen == WDHomeMatchFlowScreen.FriendlyMatchesHub ||
            currentScreen == WDHomeMatchFlowScreen.FriendlyTournamentsHub;
        for (int i = 0; i < playCardDescriptions.Count; i++)
        {
            TextMeshProUGUI description = playCardDescriptions[i];
            if (description == null) continue;
            float fontSize = WDHomeMobileLayout.DescriptionSize(hub) * playCardScale;
            if (!Mathf.Approximately(description.fontSize, fontSize))
                description.fontSize = fontSize;
            float targetWidth = WDHomeMobileLayout.DescriptionWidth(hub, i == 1) * playCardScale;
            RectTransform card = description.transform.parent as RectTransform;
            float availableWidth = card != null && card.rect.width > 0f
                ? Mathf.Max(180f, card.rect.width - 48f * playCardScale) : targetWidth;
            float width = Mathf.Min(targetWidth, availableWidth);
            if (!Mathf.Approximately(description.rectTransform.rect.width, width))
                description.rectTransform.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Horizontal, width);
        }
    }

    private static RectTransform PlayRect(string name, Transform parent,
        Vector2 anchor, Vector2 offset, Vector2 size, Vector2? pivot = null)
    {
        RectTransform rect = WDHomeUIFactory.Rect(name, parent);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = offset;
        rect.sizeDelta = size;
        return rect;
    }

    private TextMeshProUGUI PlayText(string name, Transform parent, string value,
        float size, Color color, Vector2 anchor, Vector2 offset, Vector2 dimensions,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center,
        Vector2? pivot = null)
    {
        TextMeshProUGUI text = WDHomeUIFactory.Text(name, parent, theme,
            value, size, color, alignment);
        TMP_FontAsset intended = name == "SearchTitle" ? gabaritoExtraBold :
            name == "Description" ? geistMedium :
            name == "Value" ? gabaritoBold :
            name == "TimerCaption" || name == "Count" ? gabaritoMedium :
            gabaritoBlack;
        if (intended != null) text.font = intended;
        if (name == "Title" || name == "SearchTitle" || name == "Label")
            text.characterSpacing = name == "SearchTitle" ? 2f :
                name == "Label" && parent.name == "Back" ? 1.5f : 0f;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = offset;
        rect.sizeDelta = dimensions;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static void PlaySurface(RectTransform rect, Color fill, int radius,
        Color border)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = fill;
        WDHomeUIFactory.ApplyRounded(image, radius);
        AddPlayBorder(rect, border);
    }

    private static void AddPlayBorder(RectTransform rect, Color border)
    {
        Image surface = rect.GetComponent<Image>();
        int radius = surface != null && surface.sprite != null
            ? Mathf.RoundToInt((surface.sprite.rect.width - 4f) * 0.5f) : 12;
        WDHomeUIFactory.AddRoundedBorder(rect, border, radius);
    }

    private static void PlayIcon(Transform parent, string asset, float size,
        Color? tint = null)
    {
        string resourcePath = asset.StartsWith("PlayFinal/", StringComparison.Ordinal)
            ? "UI/Home/" + asset : "UI/Home/FigmaPhase1A/" + asset;
        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null) return;
        PlayIcon(parent, texture, asset, size, tint);
    }

    private static void PlayIcon(Transform parent, Texture2D texture, string objectName,
        float size, Color? tint = null)
    {
        if (texture == null) return;
        // 09.1. O pictograma mantém proporção nativa e nunca recebe upscale.
        float scale = Mathf.Min(1f, size / Mathf.Max(texture.width, texture.height));
        RectTransform icon = PlayRect(objectName, parent,
            new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(texture.width * scale, texture.height * scale));
        RawImage image = icon.gameObject.AddComponent<RawImage>();
        image.texture = texture;
        image.color = tint ?? Color.white;
        image.raycastTarget = false;
    }

    private void ShowFriendlyHub()
    {
        ResetNavigationState(WDHomeMatchFlowScreen.FriendlyHub);
        friendlyPlaceholderOpen = false;
        var options = new List<Option>
        {
            new("PARTIDAS", "CRIE UMA PARTIDA PERSONALIZADA OU ENTRE EM UMA SALA",
                WDContentAvailability.Available,
                () => NavigateTo("play/friendly/matches", ShowFriendlyMatchesHub),
                actionLabel: "ACESSAR"),
            new("TORNEIOS AMISTOSOS", "CRIE OU PARTICIPE DE TORNEIOS ORGANIZADOS POR JOGADORES",
                WDContentAvailability.Available,
                () => NavigateTo("play/friendly/tournaments", ShowFriendlyTournamentsHub),
                actionLabel: "ACESSAR")
        };
        ShowOptions("AMISTOSOS", "Partidas e torneios personalizados para jogar com seus amigos",
            options, RequestNavigationBack, string.Empty, true, true);
    }

    private void ShowFriendlyMatchesHub()
    {
        ResetNavigationState(WDHomeMatchFlowScreen.FriendlyMatchesHub);
        friendlyPlaceholderOpen = false;
        var options = new List<Option>
        {
            new("CRIAR PARTIDA", "CONFIGURE UMA PARTIDA AMISTOSA",
                WDContentAvailability.Available,
                () => NavigateTo("play/friendly/create-match", ShowFriendlyMatchCreate),
                actionLabel: "CRIAR"),
            new("SALAS CRIADAS", "ENCONTRE PARTIDAS CRIADAS POR OUTROS JOGADORES",
                WDContentAvailability.Available,
                () => NavigateTo("play/friendly/match-rooms", ShowFriendlyRoomBrowser),
                actionLabel: "ACESSAR")
        };
        ShowOptions("PARTIDAS AMISTOSAS", "Crie ou participe de uma sala amistosa",
            options, RequestNavigationBack, string.Empty, true, true);
    }

    private void ShowFriendlyTournamentsHub()
    {
        ResetNavigationState(WDHomeMatchFlowScreen.FriendlyTournamentsHub);
        friendlyPlaceholderOpen = false;
        var options = new List<Option>
        {
            new("CRIAR TORNEIO", "CONFIGURE UM NOVO TORNEIO AMISTOSO",
                WDContentAvailability.Available,
                () => NavigateTo("play/friendly/create-tournament", ShowFriendlyTournamentCreate),
                actionLabel: "CRIAR"),
            new("SALAS DE TORNEIOS", "ENCONTRE TORNEIOS CRIADOS POR OUTROS JOGADORES",
                WDContentAvailability.Available,
                () => NavigateTo("play/friendly/tournament-rooms", ShowFriendlyTournamentBrowser),
                actionLabel: "ACESSAR")
        };
        ShowOptions("TORNEIOS AMISTOSOS", "Crie seu torneio ou entre em uma sala de torneio",
            options, RequestNavigationBack, string.Empty, true, true);
    }

    // ============================================================
    // 10. AMISTOSOS — FLUXO LOCAL COESO SOBRE A AUTORIDADE PLAY
    // ============================================================

    private void ShowFriendlyRoomBrowser()
    {
        ResetNavigationState(WDHomeMatchFlowScreen.FriendlyRoomBrowser);
        RectTransform canvas = PrepareFriendlyScreen(new Vector2(1580f, 620f));
        FriendlyHeading(canvas, "SALAS CRIADAS", "ESCOLHA UMA SALA AMISTOSA DISPONÍVEL");
        FriendlyButton(canvas, "InvitePreview", "CONVITE RECEBIDO", new Vector2(1f, 1f),
            new Vector2(-170f, -42f), new Vector2(245f, 42f),
            () => ShowFriendlyIncomingInvite("BATALHA DA NOITE"));

        string[,] rows =
        {
            { "BATALHA DA NOITE", "PLAYER_4821", "2V2", "2 / 4", "ABERTA" },
            { "TREINO 2V2", "PLAYER_5834", "2V2", "2 / 4", "COM CONVITE" },
            { "DUELO CASUAL", "PLAYER_7714", "1V1", "1 / 2", "ABERTA" },
            { "GUERRA ENTRE AMIGOS", "CLÃ WD", "3V3", "4 / 6", "COM CONVITE" },
            { "SALA DO CLÃ", "PLAYER_3906", "2V2", "3 / 4", "ABERTA" }
        };
        FriendlyTableHeader(canvas,
            new[] { "SALA", "ANFITRIÃO", "MODO", "JOGADORES", "ACESSO", "AÇÃO" });
        for (int i = 0; i < rows.GetLength(0); i++)
        {
            float y = 105f - i * 82f;
            RectTransform row = FriendlyPanel(canvas, "Room" + i,
                new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(1440f, 68f), 10);
            for (int c = 0; c < 5; c++)
                FriendlyCopy(row, rows[i, c], new Vector2(0.5f, 0.5f),
                    new Vector2(FriendlyTableColumnX[c], 0f),
                    new Vector2(FriendlyTableColumnWidth[c], 34f),
                    c == 0 ? PlayWhite : PlayMuted, c == 0 ? 17f : 14f,
                    TextAlignmentOptions.Left);
            string roomName = rows[i, 0];
            FriendlyButton(row, "Join", "ENTRAR", new Vector2(0.5f, 0.5f),
                new Vector2(FriendlyTableColumnX[5], 0f),
                new Vector2(FriendlyTableColumnWidth[5], 38f),
                () => NavigateTo("play/friendly/room/" + roomName, ShowFriendlyMatchRoom));
        }
    }

    private void ShowFriendlyTournamentBrowser()
    {
        ResetNavigationState(WDHomeMatchFlowScreen.FriendlyRoomBrowser);
        RectTransform canvas = PrepareFriendlyScreen(new Vector2(1580f, 620f));
        FriendlyHeading(canvas, "SALAS DE TORNEIOS", "TORNEIOS AMISTOSOS DISPONÍVEIS");
        FriendlyTableHeader(canvas,
            new[] { "TORNEIO", "ORGANIZADOR", "FORMATO", "EQUIPES", "STATUS", "AÇÃO" });
        string[,] rows =
        {
            { "VANGUARD VS IRON WOLVES", "PLAYER_4821", "2V2", "4 / 8", "INSCRIÇÕES" },
            { "COPA DA NOITE", "PLAYER_7105", "1V1", "6 / 8", "INSCRIÇÕES" },
            { "FRONTEIRA CLASSIC", "PLAYER_2847", "3V3", "4 / 4", "EM ANDAMENTO" },
            { "DUELO DE CLÃS", "PLAYER_1112", "2V2", "8 / 8", "EM ANDAMENTO" }
        };
        for (int i = 0; i < rows.GetLength(0); i++)
        {
            float y = 105f - i * 92f;
            RectTransform row = FriendlyPanel(canvas, "Tournament" + i,
                new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(1440f, 76f), 10);
            for (int c = 0; c < 5; c++)
                FriendlyCopy(row, rows[i, c], new Vector2(0.5f, 0.5f),
                    new Vector2(FriendlyTableColumnX[c], 0f),
                    new Vector2(FriendlyTableColumnWidth[c], 36f),
                    c == 0 ? PlayWhite : PlayMuted, c == 0 ? 16f : 14f,
                    TextAlignmentOptions.Left);
            FriendlyButton(row, "Open", "ABRIR", new Vector2(0.5f, 0.5f),
                new Vector2(FriendlyTableColumnX[5], 0f),
                new Vector2(FriendlyTableColumnWidth[5], 38f),
                () => NavigateTo("play/friendly/tournament-center", ShowFriendlyTournamentCenter));
        }
    }

    private void ShowFriendlyMatchCreate()
    {
        ResetNavigationState(WDHomeMatchFlowScreen.FriendlyMatchCreate);
        RectTransform canvas = PrepareFriendlyScreen(new Vector2(1320f, 620f));
        FriendlyHeading(canvas, "CRIAR PARTIDA", "CONFIGURE SUA SALA AMISTOSA");
        RectTransform left = FriendlyPanel(canvas, "Rules", new Vector2(0.5f, 0.5f),
            new Vector2(-330f, -40f), new Vector2(570f, 430f), 16);
        RectTransform right = FriendlyPanel(canvas, "Access", new Vector2(0.5f, 0.5f),
            new Vector2(330f, -40f), new Vector2(570f, 430f), 16);
        FriendlySection(left, "MODO", 170f);
        FriendlyChoiceRow(left, new[] { "1V1", "2V2", "3V3" }, friendlyMode, 105f,
            value => { friendlyMode = value; ShowFriendlyMatchCreate(); });
        FriendlySection(left, "BARALHO", 25f);
        FriendlyChoiceRow(left, new[] { "NORMAL", "RANDOM CARDS" }, friendlyCardMode, -40f,
            value => { friendlyCardMode = value; ShowFriendlyMatchCreate(); });
        FriendlySection(left, "MAPA", -110f);
        FriendlyChoiceRow(left, new[] { "CLASSIC", "ALEATÓRIO" }, friendlyMap, -165f,
            value => { friendlyMap = value; ShowFriendlyMatchCreate(); });

        FriendlySection(right, "CONFIGURAÇÕES DA SALA", 170f);
        FriendlyToggle(right, "SALA PRIVADA", "EXIGE CONVITE PARA ENTRAR", 90f,
            friendlyPrivateRoom, () => { friendlyPrivateRoom = !friendlyPrivateRoom; ShowFriendlyMatchCreate(); });
        FriendlyToggle(right, "SOMENTE AMIGOS", "LIMITA A LISTAGEM AOS SEUS CONTATOS", 5f,
            friendlyInviteOnly, () => { friendlyInviteOnly = !friendlyInviteOnly; ShowFriendlyMatchCreate(); });
        FriendlyCopy(right, "A sala é local neste protótipo e não publica uma sessão de rede.",
            new Vector2(0.5f, 0f), new Vector2(0f, 95f), new Vector2(480f, 58f),
            PlayMuted, 13f);
        FriendlyButton(right, "Create", "+  CRIAR SALA", new Vector2(0.5f, 0f),
            new Vector2(0f, 34f), new Vector2(230f, 48f),
            () => NavigateTo("play/friendly/my-room", ShowFriendlyMatchRoom));
    }

    private void ShowFriendlyTournamentCreate()
    {
        ResetNavigationState(WDHomeMatchFlowScreen.FriendlyTournamentCreate);
        RectTransform canvas = PrepareFriendlyScreen(new Vector2(1320f, 620f));
        FriendlyHeading(canvas, "CRIAR TORNEIO", "CONFIGURE UM TORNEIO AMISTOSO LOCAL");
        RectTransform left = FriendlyPanel(canvas, "Format", new Vector2(0.5f, 0.5f),
            new Vector2(-330f, -40f), new Vector2(570f, 430f), 16);
        RectTransform right = FriendlyPanel(canvas, "TournamentRules", new Vector2(0.5f, 0.5f),
            new Vector2(330f, -40f), new Vector2(570f, 430f), 16);
        FriendlySection(left, "FORMATO", 170f);
        FriendlyChoiceRow(left, new[] { "1V1", "2V2", "3V3" }, friendlyMode, 105f,
            value => { friendlyMode = value; ShowFriendlyTournamentCreate(); });
        FriendlySection(left, "NÚMERO DE EQUIPES", 25f);
        FriendlyChoiceRow(left, new[] { "4", "8", "16" }, "8", -40f, _ => { });
        FriendlySection(left, "MAPA", -110f);
        FriendlyChoiceRow(left, new[] { "CLASSIC", "ALEATÓRIO" }, friendlyMap, -165f,
            value => { friendlyMap = value; ShowFriendlyTournamentCreate(); });
        FriendlySection(right, "REGRAS", 170f);
        FriendlyToggle(right, "INSCRIÇÃO PRIVADA", "APENAS PARTICIPANTES CONVIDADOS", 90f,
            friendlyInviteOnly, () => { friendlyInviteOnly = !friendlyInviteOnly; ShowFriendlyTournamentCreate(); });
        FriendlyToggle(right, "DECK LIVRE", "CADA EQUIPE ESCOLHE SEU DECK", 5f,
            friendlyCardMode == "NORMAL", () =>
            { friendlyCardMode = friendlyCardMode == "NORMAL" ? "RANDOM CARDS" : "NORMAL"; ShowFriendlyTournamentCreate(); });
        FriendlyCopy(right, "Eliminação simples · chave gerada localmente após a criação.",
            new Vector2(0.5f, 0f), new Vector2(0f, 95f), new Vector2(480f, 58f),
            PlayMuted, 13f);
        FriendlyButton(right, "CreateTournament", "+  CRIAR TORNEIO", new Vector2(0.5f, 0f),
            new Vector2(0f, 34f), new Vector2(260f, 48f),
            () => NavigateTo("play/friendly/tournament-center", ShowFriendlyTournamentCenter));
    }

    private void ShowFriendlyMatchRoom()
    {
        ResetNavigationState(WDHomeMatchFlowScreen.FriendlyMatchRoom);
        RectTransform canvas = PrepareFriendlyScreen(new Vector2(1480f, 650f));
        FriendlyHeading(canvas, "BATALHA DA NOITE", "SALA AMISTOSA · ESTADO LOCAL");
        RectTransform alpha = FriendlyTeam(canvas, "VANGUARD", new Vector2(-420f, -30f),
            new[] { profileRepository.LocalProfile.Nickname, "PLAYER_3906" });
        RectTransform bravo = FriendlyTeam(canvas, "SENTINEL", new Vector2(0f, -30f),
            new[] { "PLAYER_2847", "VAGA DISPONÍVEL" });
        RectTransform configPanel = FriendlyPanel(canvas, "Configuration",
            new Vector2(0.5f, 0.5f), new Vector2(455f, -15f), new Vector2(390f, 350f), 14);
        FriendlySection(configPanel, "CONFIGURAÇÃO", 140f);
        FriendlyKeyValue(configPanel, "MODO", friendlyMode, 78f);
        FriendlyKeyValue(configPanel, "BARALHO", friendlyCardMode, 25f);
        FriendlyKeyValue(configPanel, "MAPA", friendlyMap, -28f);
        FriendlyKeyValue(configPanel, "ACESSO", friendlyPrivateRoom ? "PRIVADO" : "ABERTO", -81f);
        FriendlyButton(configPanel, "Invite", "ESCOLHER AMIGO", new Vector2(0.5f, 0f),
            new Vector2(0f, 38f), new Vector2(300f, 42f), ShowFriendlyInviteSelector);
        FriendlyButton(canvas, "Deck", "ESCOLHER DECK", new Vector2(0.5f, 0f),
            new Vector2(-170f, 48f), new Vector2(260f, 48f), ShowFriendlyDeckSelector);
        FriendlyButton(canvas, "Start", "INICIAR PARTIDA", new Vector2(0.5f, 0f),
            new Vector2(170f, 48f), new Vector2(260f, 48f), () =>
            ShowFriendlyNotice("PARTIDA LOCAL PREPARADA",
                "A configuração foi preservada. Nenhum matchmaking ou gameplay foi iniciado."));
    }

    private void ShowFriendlyTournamentCenter()
    {
        ResetNavigationState(WDHomeMatchFlowScreen.FriendlyTournamentCenter);
        RectTransform canvas = PrepareFriendlyScreen(new Vector2(1540f, 650f));
        FriendlyHeading(canvas, "VANGUARD VS IRON WOLVES", "CENTRO DO TORNEIO AMISTOSO");
        RectTransform bracket = FriendlyPanel(canvas, "Bracket", new Vector2(0.5f, 0.5f),
            new Vector2(165f, -55f), new Vector2(1080f, 455f), 14);
        FriendlyCopy(bracket, "CHAVEAMENTO", new Vector2(0f, 1f), new Vector2(35f, -38f),
            new Vector2(260f, 35f), PlayGold, 18f, TextAlignmentOptions.Left, new Vector2(0f, 1f));
        string[] teams = { "VANGUARD", "NIGHT HAWKS", "BLACK LEGION", "GOLDEN GUARD" };
        for (int i = 0; i < teams.Length; i++)
        {
            RectTransform seed = FriendlyPanel(bracket, "Seed" + i, new Vector2(0f, 1f),
                new Vector2(165f, -105f - i * 78f), new Vector2(260f, 52f), 8);
            FriendlyCopy(seed, teams[i], new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(225f, 32f), PlayWhite, 14f);
        }
        RectTransform semiA = FriendlyPanel(bracket, "SemiA", new Vector2(0.5f, 1f),
            new Vector2(80f, -155f), new Vector2(220f, 52f), 8);
        FriendlyCopy(semiA, "VANGUARD", new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(190f, 32f), PlayWhite, 14f);
        RectTransform final = FriendlyPanel(bracket, "Final", new Vector2(1f, 0.5f),
            new Vector2(-145f, 0f), new Vector2(220f, 58f), 8);
        FriendlyCopy(final, "FINAL", new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(190f, 34f), PlayGold, 15f);
        RectTransform info = FriendlyPanel(canvas, "TournamentInfo", new Vector2(0f, 0.5f),
            new Vector2(205f, -55f), new Vector2(300f, 455f), 14, new Vector2(0.5f, 0.5f));
        FriendlySection(info, "INFORMAÇÕES", 175f);
        FriendlyKeyValue(info, "FORMATO", friendlyMode, 105f);
        FriendlyKeyValue(info, "MODO", friendlyCardMode, 45f);
        FriendlyKeyValue(info, "EQUIPES", "4 / 8", -15f);
        FriendlyKeyValue(info, "STATUS", "INSCRIÇÕES", -75f);
        FriendlyButton(info, "Registration", "ENTRAR NA DISPUTA", new Vector2(0.5f, 0f),
            new Vector2(0f, 42f), new Vector2(245f, 44f), () =>
            ShowFriendlyNotice("INSCRIÇÃO LOCAL", "Inscrição registrada apenas para demonstração desta sessão."));
    }

    // 10.1. Modais usam o overlay bloqueador já pertencente ao fluxo PLAY.
    private void ShowFriendlyInviteSelector()
    {
        RectTransform modal = BeginFriendlyModal("ESCOLHER AMIGO", new Vector2(540f, 570f));
        FriendlyCopy(modal, "SELECIONE QUEM RECEBERÁ O CONVITE", new Vector2(0.5f, 1f),
            new Vector2(0f, -72f), new Vector2(460f, 28f), PlayMuted, 13f);
        IReadOnlyList<WDProfileData> friends = socialRepository?.Friends() ?? Array.Empty<WDProfileData>();
        int count = Mathf.Min(5, friends.Count);
        for (int i = 0; i < count; i++)
        {
            WDProfileData friend = friends[i];
            float y = -125f - i * 72f;
            RectTransform row = FriendlyPanel(modal, "Friend" + i, new Vector2(0.5f, 1f),
                new Vector2(0f, y), new Vector2(470f, 58f), 9);
            string presence = socialRepository.PresenceOf(friend.PlayerId).ToString().ToUpperInvariant();
            FriendlyCopy(row, friend.Nickname + "  ·  " + presence, new Vector2(0f, 0.5f),
                new Vector2(22f, 0f), new Vector2(285f, 34f), PlayWhite, 14f,
                TextAlignmentOptions.Left, new Vector2(0f, 0.5f));
            string playerId = friend.PlayerId;
            FriendlyButton(row, "Invite", friendlyInvitedPlayers.Contains(playerId) ? "ENVIADO" : "CONVIDAR",
                new Vector2(1f, 0.5f), new Vector2(-68f, 0f), new Vector2(118f, 34f), () =>
                { friendlyInvitedPlayers.Add(playerId); ShowFriendlyInviteSelector(); });
        }
        if (count == 0)
            FriendlyCopy(modal, "NENHUM AMIGO DISPONÍVEL", new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(430f, 45f), PlayMuted, 15f);
        FriendlyModalClose(modal);
    }

    private void ShowFriendlyIncomingInvite(string roomName)
    {
        RectTransform modal = BeginFriendlyModal("CONVITE PARA PARTIDA", new Vector2(520f, 470f));
        FriendlyCopy(modal, "PLAYER_4821 CONVIDOU VOCÊ PARA UMA PARTIDA AMISTOSA",
            new Vector2(0.5f, 0.5f), new Vector2(0f, 75f), new Vector2(430f, 70f),
            PlayWhite, 16f);
        FriendlyCopy(modal, roomName + "\n" + friendlyMode + "  ·  " + friendlyCardMode + "  ·  " + friendlyMap,
            new Vector2(0.5f, 0.5f), new Vector2(0f, -15f), new Vector2(430f, 70f),
            PlayMuted, 14f);
        FriendlyButton(modal, "Decline", "RECUSAR", new Vector2(0f, 0f),
            new Vector2(145f, 42f), new Vector2(180f, 42f), CloseFriendlyModal,
            new Vector2(0.5f, 0f), false);
        FriendlyButton(modal, "Accept", "ENTRAR NA SALA", new Vector2(1f, 0f),
            new Vector2(-145f, 42f), new Vector2(210f, 42f), () =>
            { CloseFriendlyModal(); NavigateTo("play/friendly/invited-room", ShowFriendlyMatchRoom); });
    }

    private void ShowFriendlyDeckSelector()
    {
        RectTransform modal = BeginFriendlyModal("ESCOLHER DECK", new Vector2(760f, 480f));
        FriendlyCopy(modal, "SELECIONE O DECK PARA ESTA PARTIDA", new Vector2(0.5f, 1f),
            new Vector2(0f, -72f), new Vector2(660f, 28f), PlayMuted, 13f);
        for (int i = 0; i < 5; i++)
        {
            int deckIndex = i;
            RectTransform deck = FriendlyPanel(modal, "Deck" + i, new Vector2(0.5f, 0.5f),
                new Vector2(-272f + i * 136f, 10f), new Vector2(116f, 190f), 10,
                new Vector2(0.5f, 0.5f), i == selectedDeckIndex ? PlayGold : PlayMuted);
            FriendlyCopy(deck, "DECK " + (i + 1), new Vector2(0.5f, 1f), new Vector2(0f, -28f),
                new Vector2(100f, 28f), PlayWhite, 14f);
            // 10.2. Cada ponto representa um slot de arma: 8 slots em grade 2 x 4.
            FriendlyCopy(deck, "●  ●\n●  ●\n●  ●\n●  ●", new Vector2(0.5f, 0.5f), new Vector2(0f, -12f),
                new Vector2(90f, 122f), PlayMuted, 16f);
            deck.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            { selectedDeckIndex = deckIndex; ShowFriendlyDeckSelector(); });
        }
        FriendlyButton(modal, "ConfirmDeck", "CONFIRMAR DECK", new Vector2(1f, 0f),
            new Vector2(-145f, 42f), new Vector2(240f, 42f), CloseFriendlyModal);
        FriendlyModalClose(modal, "CANCELAR", new Vector2(-155f, 42f));
    }

    private void ShowFriendlyNotice(string title, string message)
    {
        RectTransform modal = BeginFriendlyModal(title, new Vector2(520f, 360f));
        FriendlyCopy(modal, message, new Vector2(0.5f, 0.5f), new Vector2(0f, 15f),
            new Vector2(430f, 100f), PlayMuted, 15f);
        FriendlyModalClose(modal, "FECHAR", Vector2.zero);
    }

    private RectTransform PrepareFriendlyScreen(Vector2 canonicalSize)
    {
        backAction = RequestNavigationBack;
        legacyPanel.gameObject.SetActive(false);
        playVisualRoot.gameObject.SetActive(true);
        flowBackgroundImage.texture = playBackgroundTexture;
        flowBackgroundCover.aspectRatio = (float)playBackgroundTexture.width / playBackgroundTexture.height;
        CloseFriendlyModal();
        playMainPanel.gameObject.SetActive(true);
        playBackRect.gameObject.SetActive(true);
        playTitleLabel.gameObject.SetActive(false);
        playFeedbackLabel.text = string.Empty;
        playCardScale = WDHomeMobileLayout.PlayScale(canonicalSize);
        playPanelSize = canonicalSize * playCardScale;
        playPanelOffset = new Vector2(0f, WDHomeMobileLayout.PlayMobileLift);
        playCardsRoot.gameObject.SetActive(false);
        ClearFriendlyCanvas();
        RectTransform canvas = WDHomeUIFactory.Rect("FriendlyCanvas", playMainPanel);
        WDHomeUIFactory.Stretch(canvas, 34f, 28f, 34f, 28f);
        UpdatePlaySafeArea();
        return canvas;
    }

    private void ClearFriendlyCanvas()
    {
        Transform existing = playMainPanel.Find("FriendlyCanvas");
        if (existing == null) return;
        existing.gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(existing.gameObject); else DestroyImmediate(existing.gameObject);
    }

    private void FriendlyHeading(RectTransform parent, string title, string subtitle)
    {
        FriendlyCopy(parent, title, new Vector2(0f, 1f), new Vector2(24f, -22f),
            new Vector2(760f, 42f), PlayWhite, 28f, TextAlignmentOptions.Left, new Vector2(0f, 1f));
        FriendlyCopy(parent, subtitle, new Vector2(0f, 1f), new Vector2(24f, -66f),
            new Vector2(900f, 25f), PlayMuted, 13f, TextAlignmentOptions.Left, new Vector2(0f, 1f));
    }

    private void FriendlyTableHeader(RectTransform parent, string[] labels)
    {
        int count = Mathf.Min(labels.Length, FriendlyTableColumnX.Length);
        for (int i = 0; i < count; i++)
            FriendlyCopy(parent, labels[i], new Vector2(0.5f, 0.5f),
                new Vector2(FriendlyTableColumnX[i], 170f),
                new Vector2(FriendlyTableColumnWidth[i], 25f), PlayMuted, 12f,
                i == 5 ? TextAlignmentOptions.Center : TextAlignmentOptions.Left);
    }

    private RectTransform FriendlyTeam(RectTransform parent, string title, Vector2 offset, string[] players)
    {
        RectTransform panel = FriendlyPanel(parent, title, new Vector2(0.5f, 0.5f),
            offset, new Vector2(370f, 350f), 14);
        FriendlySection(panel, title, 135f);
        for (int i = 0; i < players.Length; i++)
        {
            RectTransform row = FriendlyPanel(panel, "Player" + i, new Vector2(0.5f, 1f),
                new Vector2(0f, -100f - i * 85f), new Vector2(320f, 66f), 9);
            FriendlyCopy(row, players[i], new Vector2(0f, 0.5f), new Vector2(22f, 0f),
                new Vector2(240f, 34f), players[i] == "VAGA DISPONÍVEL" ? PlayMuted : PlayWhite,
                14f, TextAlignmentOptions.Left, new Vector2(0f, 0.5f));
        }
        return panel;
    }

    private RectTransform BeginFriendlyModal(string title, Vector2 size)
    {
        CloseFriendlyModal();
        matchmakingOverlay.gameObject.SetActive(true);
        Image dim = WDHomeUIFactory.Image("ModalBlocker", matchmakingOverlay, new Color(0f, 0f, 0f, 0.76f));
        WDHomeUIFactory.Stretch(dim.rectTransform);
        dim.raycastTarget = true;
        RectTransform modal = PlayRect("FriendlyModal", matchmakingOverlay,
            new Vector2(0.5f, 0.5f), Vector2.zero, size);
        PlaySurface(modal, new Color(11f / 255f, 14f / 255f, 18f / 255f, 0.985f),
            18, PlayGold);
        FriendlyCopy(modal, title, new Vector2(0f, 1f), new Vector2(30f, -28f),
            new Vector2(size.x - 100f, 38f), PlayWhite, 23f,
            TextAlignmentOptions.Left, new Vector2(0f, 1f));
        return modal;
    }

    private void FriendlyModalClose(RectTransform modal, string label = "FECHAR", Vector2? offset = null)
    {
        Vector2 position = offset ?? new Vector2(0f, 36f);
        FriendlyButton(modal, "Close", label, new Vector2(0.5f, 0f), position,
            new Vector2(190f, 40f), CloseFriendlyModal, new Vector2(0.5f, 0f), false);
    }

    private void CloseFriendlyModal()
    {
        if (matchmakingOverlay == null) return;
        for (int i = matchmakingOverlay.childCount - 1; i >= 0; i--)
        {
            GameObject child = matchmakingOverlay.GetChild(i).gameObject;
            child.SetActive(false);
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
        matchmakingOverlay.gameObject.SetActive(false);
    }

    private RectTransform FriendlyPanel(Transform parent, string name, Vector2 anchor,
        Vector2 offset, Vector2 size, int radius, Vector2? pivot = null, Color? border = null)
    {
        RectTransform panel = PlayRect(name, parent, anchor, offset, size, pivot);
        PlaySurface(panel, PlayCard, radius, border ?? new Color(PlayGold.r, PlayGold.g, PlayGold.b, 0.72f));
        return panel;
    }

    private TextMeshProUGUI FriendlyCopy(Transform parent, string value, Vector2 anchor,
        Vector2 offset, Vector2 size, Color color, float fontSize,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center, Vector2? pivot = null)
    {
        TextMeshProUGUI label = PlayText("FriendlyCopy", parent, value, fontSize, color,
            anchor, offset, size, alignment, pivot);
        if (gabaritoMedium != null) label.font = gabaritoMedium;
        label.textWrappingMode = TextWrappingModes.Normal;
        return label;
    }

    private void FriendlySection(Transform parent, string value, float y)
    {
        FriendlyCopy(parent, value, new Vector2(0f, 0.5f), new Vector2(28f, y),
            new Vector2(420f, 32f), PlayGold, 15f, TextAlignmentOptions.Left,
            new Vector2(0f, 0.5f));
    }

    private void FriendlyKeyValue(Transform parent, string key, string value, float y)
    {
        FriendlyCopy(parent, key, new Vector2(0f, 0.5f), new Vector2(28f, y),
            new Vector2(150f, 28f), PlayMuted, 13f, TextAlignmentOptions.Left, new Vector2(0f, 0.5f));
        FriendlyCopy(parent, value, new Vector2(1f, 0.5f), new Vector2(-28f, y),
            new Vector2(180f, 28f), PlayWhite, 13f, TextAlignmentOptions.Right, new Vector2(1f, 0.5f));
    }

    private void FriendlyChoiceRow(Transform parent, string[] values, string selected, float y,
        Action<string> choose)
    {
        float width = Mathf.Min(150f, 475f / values.Length);
        float total = values.Length * width + (values.Length - 1) * 12f;
        for (int i = 0; i < values.Length; i++)
        {
            string value = values[i];
            FriendlyButton(parent, "Choice" + value, value, new Vector2(0.5f, 0.5f),
                new Vector2(-total * 0.5f + width * 0.5f + i * (width + 12f), y),
                new Vector2(width, 42f), () => choose(value), null,
                value == selected);
        }
    }

    private void FriendlyToggle(Transform parent, string title, string description, float y,
        bool enabled, UnityAction toggle)
    {
        FriendlyCopy(parent, title, new Vector2(0f, 0.5f), new Vector2(28f, y),
            new Vector2(300f, 28f), PlayWhite, 14f, TextAlignmentOptions.Left, new Vector2(0f, 0.5f));
        FriendlyCopy(parent, description, new Vector2(0f, 0.5f), new Vector2(28f, y - 27f),
            new Vector2(380f, 22f), PlayMuted, 11f, TextAlignmentOptions.Left, new Vector2(0f, 0.5f));
        FriendlyButton(parent, "Toggle", enabled ? "SIM" : "NÃO", new Vector2(1f, 0.5f),
            new Vector2(-58f, y - 8f), new Vector2(86f, 38f), toggle, null, enabled);
    }

    private RectTransform FriendlyButton(Transform parent, string name, string label,
        Vector2 anchor, Vector2 offset, Vector2 size, UnityAction action,
        Vector2? pivot = null, bool primary = true)
    {
        if (WDHomeMobileLayout.IsMobileLandscape)
            size.y = Mathf.Max(size.y, 44f);
        RectTransform buttonRect = PlayRect(name, parent, anchor, offset, size, pivot);
        Color fill = primary ? PlayGold : new Color(0.10f, 0.12f, 0.15f, 0.95f);
        PlaySurface(buttonRect, fill, 9, primary ? PlayGold : PlayMuted);
        Button button = buttonRect.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        if (action != null) button.onClick.AddListener(action);
        FriendlyCopy(buttonRect, label, new Vector2(0.5f, 0.5f), Vector2.zero,
            size - new Vector2(12f, 6f), primary ? new Color(0.04f, 0.05f, 0.06f, 1f) : PlayWhite,
            13f);
        return buttonRect;
    }

    private void RestorePlayHub()
    {
        gameObject.SetActive(true);
        if (config == null)
        {
            ShowStatus(
                "CONFIGURAÇÃO INDISPONÍVEL",
                "O catálogo de modos não foi encontrado. Nenhum fluxo foi iniciado.",
                RequestNavigationBack);
            return;
        }

        ShowPlayHub();
    }

    private void NavigateTo(string id, Action restore)
    {
        navigationStack.Push(new WDNavigationContext(id, _ =>
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            restore();
        }));
    }

    private void RequestNavigationBack()
    {
        navigationStack?.TryBack();
    }

    private void ResetNavigationState(WDHomeMatchFlowScreen screen)
    {
        // 01.2. Toda transição desativa a apresentação dedicada; somente
        // ShowOfficialTournaments a reativa, inclusive após retornar do detalhe.
        tournamentsPanel?.Hide();
        currentScreen = screen;
        friendlyPlaceholderOpen = false;
        selectedMode = null;
        selectedSubmode = null;
    }

    private void SelectMode(WDMatchModeDefinition mode)
    {
        if (!WDContentAvailabilityRules.IsAvailable(mode.Availability))
            return;

        WDMatchSetupContext.Clear();
        currentScreen = WDHomeMatchFlowScreen.CompetitiveFlow;
        selectedMode = mode;
        switch (mode.Destination)
        {
            case WDMatchFlowDestination.SubmodeSelection:
                ShowSubmodes();
                break;
            case WDMatchFlowDestination.TeamFormation:
                ShowTeamFormation();
                break;
            default:
                BeginMatchmaking(
                    new WDMatchmakingRequest(mode, null, string.Empty),
                    ShowModes);
                break;
        }
    }

    private void ShowSubmodes()
    {
        var options = new List<Option>();
        foreach (WDMatchSubmodeDefinition submode in selectedMode.Submodes)
        {
            if (submode == null || !WDContentAvailabilityRules.IsVisible(submode.Availability))
                continue;
            WDMatchSubmodeDefinition captured = submode;
            options.Add(new Option(
                submode.DisplayName, submode.Description, submode.Availability,
                () => SelectSubmode(captured)));
        }

        ShowOptions(
            selectedMode.DisplayName, "Selecione o submodo", options,
            ShowModes, string.Empty, false, true);
    }

    private void SelectSubmode(WDMatchSubmodeDefinition submode)
    {
        if (!WDContentAvailabilityRules.IsAvailable(submode.Availability))
            return;

        WDMatchSetupContext.Clear();
        selectedSubmode = submode;
        if (submode.Destination == WDMatchFlowDestination.TeamFormation)
            ShowTeamFormation();
        else
            BeginMatchmaking(
                new WDMatchmakingRequest(selectedMode, submode, string.Empty),
                ShowSubmodes);
    }

    // ============================================================
    // 03. FORMAÇÃO DE EQUIPE E PLACEHOLDERS SOCIAIS
    // ============================================================

    private void ShowTeamFormation()
    {
        int companionCount = Mathf.Max(1, selectedMode.GroupSize - 1);
        string companions = companionCount == 1
            ? "um companheiro"
            : $"{companionCount} companheiros";
        var options = new List<Option>
        {
            new("EQUIPE ALEATÓRIA", $"Completar o grupo com {companions}", WDContentAvailability.Available,
                () => BeginMatchmaking(
                    new WDMatchmakingRequest(selectedMode, selectedSubmode, "random_teammate"),
                    ShowTeamFormation)),
            new("EQUIPE DO CLÃ", $"Preencher {companions} pelo clã", WDContentAvailability.Available, SelectClanTeammate),
            new("JOGAR COM AMIGOS", $"Convidar {companions} antes do matchmaking", WDContentAvailability.Available, ShowFriendInvite)
        };

        ShowOptions(
            $"{selectedMode.DisplayName} · {selectedSubmode.DisplayName}",
            "Como deseja formar seu time?", options,
            ShowSubmodes, string.Empty, false, true);
    }

    private void SelectClanTeammate()
    {
        if (string.IsNullOrWhiteSpace(profileRepository.LocalProfile.Clan))
        {
            feedbackLabel.text = "É necessário participar de um clã para convidar companheiros.";
            if (playFeedbackLabel != null)
                playFeedbackLabel.text = feedbackLabel.text;
            return;
        }

        ShowConfirmation(
            "CONVITE AO CLÃ",
            "Convite provisório enviado ao clã. Os aceites válidos preencherão as vagas disponíveis antes do matchmaking.",
            "SIMULAR EQUIPE COMPLETA",
            () => BeginMatchmaking(
                new WDMatchmakingRequest(selectedMode, selectedSubmode, "clan_teammate"),
                ShowTeamFormation),
            ShowTeamFormation);
    }

    private void ShowFriendInvite()
    {
        if (playVisualRoot != null && playVisualRoot.gameObject.activeSelf &&
            socialRepository != null)
        {
            RenderFriendSelector();
            return;
        }
        ShowConfirmation(
            "CONVIDAR AMIGO",
            "Fluxo provisório de convite. O grupo só entrará no matchmaking quando todas as vagas estiverem preenchidas.",
            "SIMULAR EQUIPE COMPLETA",
            () => BeginMatchmaking(
                new WDMatchmakingRequest(selectedMode, selectedSubmode, "friend_invite"),
                ShowTeamFormation),
            ShowTeamFormation);
    }

    // 13. O seletor usa os amigos locais já presentes no domínio social.
    private void RenderFriendSelector()
    {
        backAction = ShowTeamFormation;
        for (int i = matchmakingOverlay.childCount - 1; i >= 0; i--)
        {
            GameObject child = matchmakingOverlay.GetChild(i).gameObject;
            child.SetActive(false);
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
        matchmakingOverlay.gameObject.SetActive(true);
        Image dim = WDHomeUIFactory.Image("ModalBlocker", matchmakingOverlay,
            new Color(0f, 0f, 0f, 0.52f));
        WDHomeUIFactory.Stretch(dim.rectTransform);
        dim.raycastTarget = true;
        RectTransform modal = PlayRect("FriendSelector", matchmakingOverlay,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500f, 620f));
        PlaySurface(modal, new Color(11f / 255f, 14f / 255f, 18f / 255f, 0.8f),
            18, PlayGold);
        PlayText("Title", modal, "ESCOLHER AMIGO", 28f, PlayWhite,
            new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(450f, 45f));
        IReadOnlyList<WDProfileData> friends = socialRepository.Friends();
        int rowIndex = 0;
        foreach (WDProfileData friend in friends.Take(5))
        {
            WDProfileData chosen = friend;
            WDSocialPresence presence = socialRepository.PresenceOf(friend.PlayerId);
            RectTransform row = PlayRect("Friend" + rowIndex, modal,
                new Vector2(0.5f, 1f), new Vector2(0f, -145f - rowIndex * 85f),
                new Vector2(430f, 72f));
            PlaySurface(row, PlayCard, 12, PlayGold);
            PlayText("Identity", row, friend.Nickname + "  ·  " + presence.ToString().ToUpperInvariant(),
                17f, PlayWhite, new Vector2(0f, 0.5f), new Vector2(155f, 0f),
                new Vector2(285f, 45f), TextAlignmentOptions.Left,
                new Vector2(0.5f, 0.5f));
            RectTransform invite = PlayRect("Invite", row,
                new Vector2(1f, 0.5f), new Vector2(-60f, 0f),
                new Vector2(105f, 38f));
            PlaySurface(invite, PlayGold, 8, PlayGold);
            Button button = invite.gameObject.AddComponent<Button>();
            button.interactable = presence == WDSocialPresence.Online;
            button.onClick.AddListener(() => ShowOfficialFriendInvite(chosen));
            PlayText("Label", invite, "CONVIDAR", 14f,
                new Color(11f / 255f, 14f / 255f, 18f / 255f, 1f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 32f));
            rowIndex++;
        }
        if (rowIndex == 0)
            PlayText("Empty", modal, "NENHUM AMIGO ENCONTRADO", 18f, PlayMuted,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440f, 60f));
        RectTransform close = PlayRect("Close", modal,
            new Vector2(0.5f, 0f), new Vector2(0f, 35f), new Vector2(170f, 42f));
        PlaySurface(close, PlayCard, 8, PlayGold);
        close.gameObject.AddComponent<Button>().onClick.AddListener(ShowTeamFormation);
        PlayText("Label", close, "VOLTAR", 16f, PlayWhite,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160f, 36f));
    }

    private void ShowOfficialFriendInvite(WDProfileData friend)
    {
        ShowConfirmation("CONVITE PARA PARTIDA",
            friend.Nickname + " foi convidado(a) para " + selectedMode.DisplayName +
            ". A aceitação segue a simulação local existente.",
            "SIMULAR ACEITE", () => BeginMatchmaking(
                new WDMatchmakingRequest(selectedMode, selectedSubmode, "friend_invite"),
                ShowTeamFormation), RenderFriendSelector);
    }

    // ============================================================
    // 04. MATCHMAKING LOCAL, CONTADOR E IDENTIDADE PROTEGIDA
    // ============================================================

    private void BeginMatchmaking(WDMatchmakingRequest request, Action onCancel)
    {
        // 04. Toda tentativa local começa sem contexto ou temporizador anterior.
        WDMatchSetupContext.Clear();
        voteActive = false;
        mapVote = null;
        voteRequest = null;
        matchTransitionStarted = false;
        if (request == null || selectedMode == null ||
            !WDContentAvailabilityRules.IsAvailable(selectedMode.Availability) ||
            request.ModeId != selectedMode.Id ||
            (request.GroupSize > 1 && string.IsNullOrWhiteSpace(request.TeamFormation)) ||
            (selectedSubmode == null && !string.IsNullOrEmpty(request.SubmodeId)) ||
            (selectedSubmode != null &&
             (!WDContentAvailabilityRules.IsAvailable(selectedSubmode.Availability) ||
              !selectedMode.Submodes.Contains(selectedSubmode) ||
              request.SubmodeId != selectedSubmode.Id)))
        {
            ShowStatus("PARTIDA INDISPONÍVEL", "Modalidade indisponível para esta tentativa.", onCancel);
            return;
        }
        matchmakingRequest = request;
        returnBeforeMatchmaking = onCancel;
        preMatchFlow = new WDPreMatchFlowController();
        preMatchFlow.BeginSearch();

        string target = request.GroupSize > 1 ? "equipe adversária" : "adversário";
        var options = new List<Option>
        {
            new($"SIMULAR {target.ToUpperInvariant()} ENCONTRADO(A)",
                "Transição local provisória para validar o fluxo",
                WDContentAvailability.Available, MarkOpponentFound)
        };
        ShowOptions(
            $"PROCURANDO {target.ToUpperInvariant()}...",
            "A identidade dos oponentes permanece oculta até o início da partida.",
            options, CancelMatchmaking, "TEMPO DE ESPERA: 00:00");
    }

    private void CancelMatchmaking()
    {
        if (preMatchFlow == null || !preMatchFlow.Cancel())
            return;

        Action destination = returnBeforeMatchmaking;
        preMatchFlow = null;
        matchmakingRequest = null;
        returnBeforeMatchmaking = null;
        WDMatchSetupContext.Clear();
        destination?.Invoke();
    }

    private void MarkOpponentFound()
    {
        if (preMatchFlow == null || !preMatchFlow.MarkOpponentFound())
            return;

        ContinueAfterOpponentFound();
    }

    private void ContinueAfterOpponentFound()
    {
        WDMatchmakingRequest request = matchmakingRequest;
        preMatchFlow = null;
        matchmakingRequest = null;

        if (request.MapSelectionPolicy == WDMapSelectionPolicy.Fixed)
        {
            if (!config.IsMapAllowedForMode(request.ModeId, request.FixedMapId))
            {
                ShowStatus(
                    "MAPA INDISPONÍVEL",
                    "O mapa configurado para este modo está desabilitado ou incompatível.",
                    returnBeforeMatchmaking);
                return;
            }
            ShowFixedMapResult(request);
            return;
        }

        BeginVoting(request, null);
    }

    private void ShowFixedMapResult(WDMatchmakingRequest request)
    {
        selectedDeckIndex = armoryRepository?.Load(armoryCatalog)?.ActiveDeckIndex ??
            homeData.DefaultDeckIndex;
        CompleteMatchSetup(request, request.FixedMapId);
    }

    private static string FormatElapsed(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    // ============================================================
    // 05. VOTAÇÃO LOCAL DE MAPAS APÓS ADVERSÁRIO ENCONTRADO
    // ============================================================

    private void BeginVoting(WDMatchmakingRequest request, Action onBack)
    {
        mapVote = new WDMapVoteController();
        mapVote.SelectCandidates(config.GetEligibleMaps(
            request.ModeId, request.EligibleMaps));
        voteRequest = request;
        returnAfterVoting = onBack;
        selectedDeckIndex = armoryRepository?.Load(armoryCatalog)?.ActiveDeckIndex ??
            homeData.DefaultDeckIndex;

        if (mapVote.Candidates.Count == 0)
        {
            ShowStatus(
                "VOTAÇÃO INDISPONÍVEL",
                "Nenhum mapa elegível com peso válido foi configurado para este fluxo.",
                onBack);
            return;
        }

        voteTimeRemaining = VoteDurationSeconds;
        voteActive = true;
        ShowVotingOptions();
    }

    private void ShowVotingOptions()
    {
        string currentVote = mapVote.GetVote(LocalPlayerId);
        var options = new List<Option>();
        foreach (WDMapVoteCandidate candidate in mapVote.Candidates)
        {
            string mapId = candidate.MapId;
            bool isSelected = mapId == currentVote;
            string selected = isSelected ? "\nSELECIONADO" : string.Empty;
            options.Add(new Option(
                GetMapName(mapId),
                $"Peso {candidate.Weight:0.##}{selected}",
                WDContentAvailability.Available,
                () => ToggleLocalVote(mapId), GetMapThumbnail(mapId), isSelected));
        }

        bool usesProfileDeck = voteRequest != null && voteRequest.DeckSelectionEnabled;
        WDArmoryProfile armoryProfile = usesProfileDeck
            ? armoryRepository?.Load(armoryCatalog) : null;
        for (int i = 0; armoryProfile != null && i < armoryProfile.Decks.Count; i++)
        {
            int deckIndex = i;
            if (!armoryProfile.IsDeckUnlocked(i))
                continue;
            WDSavedWeaponDeck deck = armoryProfile.GetDeck(i);
            bool isSelected = deckIndex == selectedDeckIndex;
            options.Add(new Option(
                $"DECK {i + 1} · {deck.Name}",
                isSelected ? "DECK SELECIONADO" : "Usar nesta partida",
                WDContentAvailability.Available,
                () => SelectDeck(deckIndex), null, isSelected));
        }

        string availabilityNote = mapVote.Candidates.Count < WDMapVoteController.CandidateLimit
            ? $"Somente {mapVote.Candidates.Count} mapa(s) elegível(is) disponível(is)."
            : "Escolha um dos dois candidatos.";
        ShowOptions(
            "VOTAÇÃO DE MAPA", availabilityNote, options,
            null, $"TEMPO: {Mathf.CeilToInt(voteTimeRemaining):00}s");
    }

    private void ToggleLocalVote(string mapId)
    {
        if (!voteActive)
            return;

        if (mapVote.GetVote(LocalPlayerId) == mapId)
            mapVote.Abstain(LocalPlayerId);
        else if (!mapVote.SubmitVote(LocalPlayerId, mapId))
            return;

        ShowVotingOptions();
    }

    private void SelectDeck(int deckIndex)
    {
        WDArmoryProfile profile = armoryRepository?.Load(armoryCatalog);
        if (!voteActive || profile == null || !profile.IsDeckUnlocked(deckIndex))
            return;

        selectedDeckIndex = deckIndex;
        ShowVotingOptions();
    }

    private void FinishVoting()
    {
        if (!voteActive)
            return;

        voteActive = false;
        WDMapVoteResult result = mapVote.Resolve();
        CompleteMatchSetup(voteRequest, result.WinningMapId);
    }

    // ============================================================
    // 06. MATCH SETUP E TRANSIÇÃO PARA A CENA DO MAPA
    // ============================================================

    private void CompleteMatchSetup(WDMatchmakingRequest request, string mapId)
    {
        if (matchTransitionStarted)
            return;
        if (request == null || homeData == null ||
            armoryCatalog == null || armoryRepository == null ||
            config == null || !config.IsMapAllowedForMode(request.ModeId, mapId) ||
            (request.MapSelectionPolicy == WDMapSelectionPolicy.Vote &&
             (mapVote == null || !mapVote.Candidates.Any(item => item.MapId == mapId))))
        {
            WDMatchSetupContext.Clear();
            ShowStatus("PARTIDA INDISPONÍVEL", "Configuração de partida incompleta.",
                returnBeforeMatchmaking);
            return;
        }
        DefinicaoMapa map = GetMapDefinition(mapId);
        if (map == null)
        {
            ShowStatus(
                "PARTIDA INDISPONÍVEL",
                $"O mapa '{mapId}' não foi encontrado no catálogo.",
                returnBeforeMatchmaking);
            return;
        }

        WDArmoryProfile armoryProfile = armoryRepository?.Load(armoryCatalog);
        if (armoryProfile == null ||
            (!WDMatchSetup.IsRandomCardsRule(request.CardRuleId) &&
             !armoryProfile.IsDeckUnlocked(selectedDeckIndex)))
        {
            WDMatchSetupContext.Clear();
            ShowStatus("PARTIDA INDISPONÍVEL", "Deck selecionado não está disponível.",
                returnBeforeMatchmaking);
            return;
        }
        WDMatchSetup setup = WDMatchSetupFactory.Create(
            request, map, homeData, selectedDeckIndex, 0,
            armoryCatalog, armoryProfile);
        int effectiveDeckIndex = request.MapSelectionPolicy == WDMapSelectionPolicy.Fixed ||
            !request.DeckSelectionEnabled
            ? armoryProfile.ActiveDeckIndex : selectedDeckIndex;
        if (!WDMatchSetupFactory.IsReadyForEntry(setup, request, mapId,
            effectiveDeckIndex, armoryCatalog))
        {
            WDMatchSetupContext.Clear();
            ShowStatus("PARTIDA INDISPONÍVEL",
                "Deck ou participantes inválidos para iniciar a partida. Verifique o deck salvo no Arsenal.",
                returnBeforeMatchmaking);
            return;
        }
        matchTransitionStarted = true;
        WDMatchSetupContext.Store(setup);
        GetComponentInParent<WarDominionHomeController>()?
            .PrepareForSceneTransition();
        if (!WDMatchSetupContext.TryLoadCurrentScene())
        {
            matchTransitionStarted = false;
            WDMatchSetupContext.Clear();
            ShowStatus(
                "PARTIDA INDISPONÍVEL",
                "A cena do mapa não está configurada para carregamento.",
                selectedSubmode != null ? ShowSubmodes : ShowModes);
        }
    }

    private void ShowStatus(string title, string body, Action onBack)
    {
        voteActive = false;
        if (playVisualRoot != null)
        {
            playVisualRoot.gameObject.SetActive(false);
            legacyPanel.gameObject.SetActive(true);
        }
        ClearOptions();
        titleLabel.text = title;
        subtitleLabel.text = body;
        subtitleLabel.rectTransform.anchorMin = new Vector2(0.12f, 0.28f);
        subtitleLabel.rectTransform.anchorMax = new Vector2(0.88f, 0.72f);
        feedbackLabel.text = string.Empty;
        backAction = onBack;
        backButton.gameObject.SetActive(true);
    }

    private void ShowConfirmation(
        string title, string body, string actionLabel, UnityAction action,
        Action onBack)
    {
        if (playVisualRoot != null && playVisualRoot.gameObject.activeSelf &&
            matchmakingBackgroundTexture != null)
        {
            RenderInviteConfirmation(title, body, actionLabel, action, onBack);
            return;
        }
        var options = new List<Option>
        {
            new(actionLabel, string.Empty, WDContentAvailability.Available, action)
        };
        ShowOptions(title, body, options, onBack, string.Empty);
    }

    // 12. Convite é modal sobre a seleção de equipe e bloqueia todos os controles da base.
    private void RenderInviteConfirmation(string title, string body, string actionLabel,
        UnityAction action, Action onBack)
    {
        backAction = onBack;
        for (int i = matchmakingOverlay.childCount - 1; i >= 0; i--)
        {
            GameObject child = matchmakingOverlay.GetChild(i).gameObject;
            child.SetActive(false);
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
        matchmakingOverlay.gameObject.SetActive(true);
        Image dim = WDHomeUIFactory.Image("ModalBlocker", matchmakingOverlay,
            new Color(0f, 0f, 0f, 0.55f));
        WDHomeUIFactory.Stretch(dim.rectTransform);
        dim.raycastTarget = true;
        RectTransform modal = PlayRect("InviteModal", matchmakingOverlay,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(650f, 540f));
        PlaySurface(modal, new Color(11f / 255f, 14f / 255f, 18f / 255f, 0.8f),
            20, PlayGold);
        PlayText("Title", modal, title, 30f, PlayWhite,
            new Vector2(0.5f, 1f), new Vector2(0f, -85f), new Vector2(590f, 50f));
        PlayText("Description", modal, body, 18f, PlayMuted,
            new Vector2(0.5f, 0.5f), new Vector2(0f, 15f),
            new Vector2(560f, 150f));
        RectTransform dismiss = PlayRect("Dismiss", modal,
            new Vector2(0.5f, 0f), new Vector2(-145f, 70f),
            new Vector2(240f, 55f));
        PlaySurface(dismiss, PlayCard, 10, PlayGold);
        dismiss.gameObject.AddComponent<Button>().onClick.AddListener(() => onBack?.Invoke());
        PlayText("Label", dismiss, "RECUSAR", 17f, PlayWhite,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 45f));
        RectTransform accept = PlayRect("Accept", modal,
            new Vector2(0.5f, 0f), new Vector2(145f, 70f),
            new Vector2(240f, 55f));
        PlaySurface(accept, PlayGold, 10, PlayGold);
        accept.gameObject.AddComponent<Button>().onClick.AddListener(action);
        PlayText("Label", accept, actionLabel, 16f,
            new Color(11f / 255f, 14f / 255f, 18f / 255f, 1f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(225f, 48f));
    }

    // ============================================================
    // 07. NOMES, THUMBNAILS E CONSTRUÇÃO REUTILIZÁVEL
    // ============================================================

    private void LoadMapNames()
    {
        mapNames.Clear();
        mapDefinitions.Clear();
        CatalogoMapas catalog = Resources.Load<CatalogoMapas>("Mapas/CatalogoMapas");
        if (catalog == null)
            return;

        foreach (DefinicaoMapa map in catalog.Mapas)
        {
            if (map != null && !string.IsNullOrWhiteSpace(map.MapaId))
            {
                mapNames[map.MapaId] = map.NomeExibido;
                mapDefinitions[map.MapaId] = map;
            }
        }
    }

    private Sprite GetMapThumbnail(string mapId)
    {
        // 07.1. Previews são assets próprios e neutros para todo o catálogo.
        // O mapa jogável e seus territórios nunca são usados como tela de pintura.
        string thumbnail = mapId?.ToLowerInvariant() switch
        {
            "classic" => "Classic",
            "dark_world" => "DarkWorld",
            "pearl_harbor" => "PearlHarbor",
            "peloponeso" => "Peloponeso",
            "riachuelo" => "Riachuelo",
            _ => null
        };
        if (!string.IsNullOrEmpty(thumbnail))
        {
            Sprite preview = Resources.Load<Sprite>(
                "UI/Home/MapThumbnails/" + thumbnail);
            if (preview != null)
                return preview;
        }
        return mapDefinitions.TryGetValue(mapId ?? string.Empty, out DefinicaoMapa map)
            ? map.ArteBase
            : null;
    }

    private DefinicaoMapa GetMapDefinition(string mapId)
    {
        mapDefinitions.TryGetValue(mapId ?? string.Empty, out DefinicaoMapa map);
        return map;
    }

    private string GetMapName(string mapId)
    {
        return mapNames.TryGetValue(mapId ?? string.Empty, out string name) &&
            !string.IsNullOrWhiteSpace(name)
            ? name
            : mapId;
    }

    private void ShowOptions(
        string title, string subtitle, IReadOnlyList<Option> options,
        Action onBack, string feedback, bool compactCentered = false,
        bool figmaPlay = false)
    {
        ClearOptions();
        useCompactCenteredOptions = compactCentered;
        optionsLayout.childAlignment = compactCentered
            ? TextAnchor.MiddleCenter
            : TextAnchor.UpperLeft;
        optionsLayout.childForceExpandWidth = !compactCentered;
        titleLabel.text = title;
        subtitleLabel.text = subtitle;
        subtitleLabel.rectTransform.anchorMin = new Vector2(0f, 0.75f);
        subtitleLabel.rectTransform.anchorMax = new Vector2(1f, 0.84f);
        feedbackLabel.text = feedback;
        backAction = onBack;
        backButton.gameObject.SetActive(onBack != null);

        foreach (Option option in options)
            BuildOption(option);

        if (playVisualRoot != null)
        {
            bool searchVisual = preMatchFlow != null &&
                preMatchFlow.State == WDPreMatchState.Searching &&
                matchmakingBackgroundTexture != null;
            bool prematchVisual = voteActive && matchmakingBackgroundTexture != null;
            legacyPanel.gameObject.SetActive(!figmaPlay && !searchVisual && !prematchVisual);
            playVisualRoot.gameObject.SetActive(figmaPlay || searchVisual || prematchVisual);
            if (figmaPlay)
                RenderPlayVisual(title, subtitle, options);
            else if (searchVisual)
                RenderMatchmakingSearch(title, options);
            else if (prematchVisual)
                RenderPrematch(options);
        }
    }

    // 10. Busca usa o estado funcional existente e bloqueia a tela-base com um modal real.
    private void RenderMatchmakingSearch(string title, IReadOnlyList<Option> options)
    {
        // 10.1. O painel e os números principais conservam a escala aprovada;
        // somente legenda, estado e alvo de cancelamento ganham ergonomia mobile.
        float controlScale = WDHomeMobileLayout.SearchControlScale;
        flowBackgroundImage.texture = matchmakingBackgroundTexture;
        flowBackgroundCover.aspectRatio = (float)matchmakingBackgroundTexture.width /
            matchmakingBackgroundTexture.height;
        playMainPanel.gameObject.SetActive(false);
        playBackRect.gameObject.SetActive(false);
        playFeedbackLabel.text = string.Empty;
        for (int i = matchmakingOverlay.childCount - 1; i >= 0; i--)
        {
            GameObject child = matchmakingOverlay.GetChild(i).gameObject;
            child.SetActive(false);
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
        matchmakingOverlay.gameObject.SetActive(true);
        Image dim = WDHomeUIFactory.Image("ModalBlocker", matchmakingOverlay,
            new Color(0f, 0f, 0f, 0.34f));
        WDHomeUIFactory.Stretch(dim.rectTransform);
        dim.raycastTarget = true;
        RectTransform modal = PlayRect("SearchModal", matchmakingOverlay,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 570f));
        PlaySurface(modal, new Color(11f / 255f, 14f / 255f, 18f / 255f, 0.8f),
            22, PlayGold);
        PlayText("SearchTitle", modal, title.Replace("...", string.Empty), 42f,
            PlayWhite, new Vector2(0.5f, 1f), new Vector2(0f, -105f),
            new Vector2(780f, 65f));
        PlayText("SearchTimer", modal, "00:00", 42f, PlayWhite,
            new Vector2(0.5f, 1f), new Vector2(0f, -235f),
            new Vector2(300f, 60f));
        PlayText("TimerCaption", modal, "TEMPO DE BUSCA", 20f * controlScale, PlayMuted,
            new Vector2(0.5f, 1f), new Vector2(0f, -287f),
            new Vector2(400f, 35f * controlScale));
        // 10.1. A busca usa pictogramas locais; símbolos de fonte não são ícones.
        bool groupSearch = matchmakingRequest != null &&
            matchmakingRequest.MatchSize > 2;
        int playerIcons = groupSearch ? 4 : 2;
        for (int i = 0; i < playerIcons; i++)
        {
            float x = (i - (playerIcons - 1) * 0.5f) *
                (groupSearch ? 76f : 150f);
            RectTransform avatarIcon = PlayRect("PlayerIcon" + i, modal,
                new Vector2(0.5f, 1f), new Vector2(x, -365f),
                new Vector2(54f * controlScale, 54f * controlScale));
            PlayIcon(avatarIcon, "UserPlayNeutral", 38f * controlScale,
                i == playerIcons - 1 ? PlayMuted : PlayGold);
        }
        if (!groupSearch)
        {
            RectTransform swords = PlayRect("SearchSwords", modal,
                new Vector2(0.5f, 1f), new Vector2(0f, -365f),
                new Vector2(54f * controlScale, 54f * controlScale));
            PlayIcon(swords, "CrossedSwordsGlyph", 38f * controlScale);
        }
        PlayText("Count", modal, "BUSCA EM ANDAMENTO", 18f * controlScale, PlayWhite,
            new Vector2(0.5f, 1f),
            new Vector2(0f, -415f - 75f * (controlScale - 1f)),
            new Vector2(500f, 35f * controlScale));
        RectTransform cancel = PlayRect("CancelSearch", modal,
            new Vector2(0.5f, 0f), new Vector2(0f, 45f),
            new Vector2(280f * controlScale, 48f * controlScale));
        PlaySurface(cancel, new Color(0.15f, 0.15f, 0.15f, 0.8f), 14,
            new Color(1f, 1f, 1f, 0.15f));
        cancel.gameObject.AddComponent<Button>().onClick.AddListener(CancelMatchmaking);
        PlayText("Label", cancel, "CANCELAR BUSCA", 17f * controlScale, PlayWhite,
            new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(265f * controlScale, 42f * controlScale));
        // A simulação já existente continua acessível sem criar outra lógica de partida.
        if (options.Count > 0 && options[0].Action != null)
        {
            RectTransform simulate = PlayRect("LocalSimulation", matchmakingOverlay,
                new Vector2(1f, 0f), new Vector2(-32f, 24f),
                new Vector2(240f, 34f), new Vector2(1f, 0f));
            Image hitTarget = simulate.gameObject.AddComponent<Image>();
            hitTarget.color = new Color(0f, 0f, 0f, 0.01f);
            hitTarget.raycastTarget = true;
            simulate.gameObject.AddComponent<Button>().onClick.AddListener(options[0].Action);
            PlayText("Label", simulate, "SIMULAR ENCONTRO", 13f, PlayMuted,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(230f, 30f));
        }
        UpdatePlaySafeArea();
    }

    // 11. Votação e deck continuam ligados ao controlador e snapshot já existentes.
    private void RenderPrematch(IReadOnlyList<Option> options)
    {
        flowBackgroundImage.texture = matchmakingBackgroundTexture;
        flowBackgroundCover.aspectRatio = (float)matchmakingBackgroundTexture.width /
            matchmakingBackgroundTexture.height;
        playMainPanel.gameObject.SetActive(false);
        playBackRect.gameObject.SetActive(false);
        playFeedbackLabel.text = string.Empty;
        for (int i = matchmakingOverlay.childCount - 1; i >= 0; i--)
        {
            GameObject child = matchmakingOverlay.GetChild(i).gameObject;
            child.SetActive(false);
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
        matchmakingOverlay.gameObject.SetActive(true);
        Image dim = WDHomeUIFactory.Image("ModalBlocker", matchmakingOverlay,
            new Color(0f, 0f, 0f, 0.22f));
        WDHomeUIFactory.Stretch(dim.rectTransform);
        dim.raycastTarget = true;
        bool hasDecks = options.Any(option => option.Title.StartsWith("DECK ", StringComparison.Ordinal));
        RectTransform modal = PlayRect("PrematchModal", matchmakingOverlay,
            new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(hasDecks ? 1480f : 1350f, 595f));
        PlaySurface(modal, new Color(11f / 255f, 14f / 255f, 18f / 255f, 0.8f),
            24, PlayGold);
        PlayText("Title", modal, hasDecks ? "ESCOLHA O MAPA E O DECK" : "ESCOLHA O MAPA",
            36f, PlayWhite, new Vector2(0.5f, 1f), new Vector2(0f, -55f),
            new Vector2(1000f, 55f));
        var maps = options.Where(option =>
            !option.Title.StartsWith("DECK ", StringComparison.Ordinal)).Take(2).ToList();
        for (int i = 0; i < maps.Count; i++)
        {
            Option option = maps[i];
            float x = hasDecks ? -510f + 350f * i : -290f + 580f * i;
            RectTransform card = PlayRect("MapChoice" + i, modal,
                new Vector2(0.5f, 0.5f), new Vector2(x, 0f),
                new Vector2(hasDecks ? 310f : 500f, 325f));
            Image image = card.gameObject.AddComponent<Image>();
            image.sprite = option.Thumbnail;
            image.color = option.Thumbnail == null ? PlayCard : Color.white;
            image.preserveAspect = true;
            image.raycastTarget = true;
            card.gameObject.AddComponent<Button>().onClick.AddListener(option.Action);
            if (option.Selected)
                WDHomeUIFactory.AddRoundedBorder(card, PlayGold, 8);
            PlayText("Name", card, option.Title.ToUpperInvariant(), 20f, PlayWhite,
                new Vector2(0.5f, 0f), new Vector2(0f, -30f),
                new Vector2(card.sizeDelta.x, 32f));
        }
        if (hasDecks)
        {
            int deckIndex = 0;
            foreach (Option option in options.Where(item =>
                item.Title.StartsWith("DECK ", StringComparison.Ordinal)).Take(5))
            {
                RectTransform deck = PlayRect("DeckChoice" + deckIndex, modal,
                    new Vector2(1f, 1f), new Vector2(-215f, -155f - 73f * deckIndex),
                    new Vector2(260f, 52f), new Vector2(1f, 1f));
                PlaySurface(deck, option.Selected ?
                    new Color(PlayGold.r, PlayGold.g, PlayGold.b, 0.25f) :
                    new Color(11f / 255f, 14f / 255f, 18f / 255f, 0.45f),
                    6, PlayGold);
                deck.gameObject.AddComponent<Button>().onClick.AddListener(option.Action);
                PlayText("Name", deck, option.Title, 18f, PlayWhite,
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(250f, 42f));
                deckIndex++;
            }
        }
        PlayText("VoteCountdown", modal, $"00:{Mathf.CeilToInt(voteTimeRemaining):00}",
            20f, PlayWhite, new Vector2(0.5f, 0f), new Vector2(0f, 35f),
            new Vector2(170f, 32f));
        UpdatePlaySafeArea();
    }

    private void BuildOption(Option option)
    {
        RectTransform rect = WDHomeUIFactory.Rect(option.Title.Replace(" ", string.Empty), optionsRoot);
        if (useCompactCenteredOptions)
        {
            LayoutElement optionLayout = rect.gameObject.AddComponent<LayoutElement>();
            optionLayout.minWidth = 210f;
            optionLayout.preferredWidth = 260f;
            optionLayout.flexibleWidth = 0f;
        }
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = option.Availability == WDContentAvailability.Available
            ? theme.SurfaceGlass
            : Color.Lerp(theme.SurfaceGlass, theme.Disabled, 0.62f);
        Outline outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = option.Availability == WDContentAvailability.Available
            ? option.Selected ? theme.Acento : theme.BorderNeutral
            : theme.Disabled;
        outline.effectDistance = option.Selected ? new Vector2(3f, -3f) : new Vector2(1f, -1f);

        Button button = rect.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        button.interactable = option.Availability == WDContentAvailability.Available;
        if (button.interactable && option.Action != null)
            button.onClick.AddListener(option.Action);

        TextMeshProUGUI title = WDHomeUIFactory.Text(
            "Title", rect, theme, option.Title, theme.TypeSection,
            button.interactable ? theme.TextoPrincipal : theme.TextoSecundario,
            TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        title.rectTransform.anchorMin = new Vector2(0.06f, option.Thumbnail != null ? 0.15f : 0.50f);
        title.rectTransform.anchorMax = new Vector2(0.94f, option.Thumbnail != null ? 0.28f : 0.86f);
        title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;

        TextMeshProUGUI description = WDHomeUIFactory.Text(
            "Description", rect, theme, option.Description, theme.TypeMicro,
            theme.TextoSecundario, TextAlignmentOptions.Center);
        description.rectTransform.anchorMin = new Vector2(0.08f, 0.04f);
        description.rectTransform.anchorMax = new Vector2(0.92f, option.Thumbnail != null ? 0.20f : 0.52f);
        description.rectTransform.offsetMin = description.rectTransform.offsetMax = Vector2.zero;

        if (option.Thumbnail != null)
        {
            RectTransform thumbnailRect = WDHomeUIFactory.Rect("Thumbnail", rect);
            thumbnailRect.SetAsFirstSibling();
            thumbnailRect.anchorMin = new Vector2(0.05f, 0.30f);
            thumbnailRect.anchorMax = new Vector2(0.95f, 0.94f);
            thumbnailRect.offsetMin = thumbnailRect.offsetMax = Vector2.zero;
            thumbnailRect.gameObject.AddComponent<RectMask2D>();

            RectTransform mapArt = WDHomeUIFactory.Rect("MapArt", thumbnailRect);
            mapArt.anchorMin = Vector2.zero;
            mapArt.anchorMax = Vector2.one;
            mapArt.offsetMin = mapArt.offsetMax = Vector2.zero;
            mapArt.localScale = Vector3.one * theme.MapThumbnailInnerScale;
            Image thumbnail = mapArt.gameObject.AddComponent<Image>();
            thumbnail.sprite = option.Thumbnail;
            thumbnail.preserveAspect = true;
            thumbnail.raycastTarget = false;
        }

        if (!string.IsNullOrWhiteSpace(option.ActionLabel))
        {
            RectTransform actionRect = WDHomeUIFactory.Rect("Action", rect);
            actionRect.anchorMin = new Vector2(0.14f, 0.07f);
            actionRect.anchorMax = new Vector2(0.86f, 0.23f);
            actionRect.offsetMin = actionRect.offsetMax = Vector2.zero;
            WDUIPremiumButton actionButton =
                actionRect.gameObject.AddComponent<WDUIPremiumButton>();
            actionButton.Build(theme, option.ActionLabel, option.Action);
            description.rectTransform.anchorMin = new Vector2(0.08f, 0.27f);
        }

        if (option.Availability == WDContentAvailability.ComingSoon)
        {
            TextMeshProUGUI status = WDHomeUIFactory.Text(
                "Status", rect, theme, "COMING SOON", theme.TypeMicro,
                theme.Warning, TextAlignmentOptions.Center);
            status.rectTransform.anchorMin = new Vector2(0.05f, 0.87f);
            status.rectTransform.anchorMax = new Vector2(0.95f, 0.98f);
            status.rectTransform.offsetMin = status.rectTransform.offsetMax = Vector2.zero;
        }
    }

    private WDUIPremiumButton BuildUtilityButton(
        RectTransform panel, string name, string label, Vector2 anchor, UnityAction action)
    {
        RectTransform rect = WDHomeUIFactory.Rect(name, panel);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.sizeDelta = new Vector2(label == "X" ? 54f : 130f, 48f);
        rect.anchoredPosition = anchor.x < 0.5f ? new Vector2(28f, 24f) : new Vector2(-28f, 24f);
        WDUIPremiumButton button = rect.gameObject.AddComponent<WDUIPremiumButton>();
        button.Build(theme, label, action);
        return button;
    }

    private void ClearOptions()
    {
        for (int i = optionsRoot.childCount - 1; i >= 0; i--)
        {
            GameObject option = optionsRoot.GetChild(i).gameObject;
            option.SetActive(false);
            if (Application.isPlaying)
                Destroy(option);
            else
                DestroyImmediate(option);
        }
    }

    private void Back() => backAction?.Invoke();
}
