using System;
using System.Collections.Generic;
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
    ProvisionalEndpoint,
    CompetitiveFlow
}

[DisallowMultipleComponent]
public sealed class WDHomeMatchFlowPanel : MonoBehaviour
{
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
    private WDPreMatchFlowController preMatchFlow;
    private WDMatchmakingRequest matchmakingRequest;
    private Action returnBeforeMatchmaking;
    private bool useCompactCenteredOptions;
    private bool friendlyPlaceholderOpen;
    private WDHomeMatchFlowScreen currentScreen;
    private WDNavigationStack navigationStack;

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
        WDNavigationStack newNavigationStack = null)
    {
        theme = newTheme;
        config = newConfig;
        homeData = newHomeData;
        armoryCatalog = newArmoryCatalog;
        armoryRepository = newArmoryRepository;
        navigationStack = newNavigationStack;
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
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (preMatchFlow != null && preMatchFlow.State == WDPreMatchState.Searching)
        {
            preMatchFlow.Tick(Time.unscaledDeltaTime);
            feedbackLabel.text = $"TEMPO DE ESPERA: {FormatElapsed(preMatchFlow.WaitSeconds)}";
        }

        if (voteActive)
        {
            voteTimeRemaining = Mathf.Max(0f, voteTimeRemaining - Time.unscaledDeltaTime);
            feedbackLabel.text = $"TEMPO: {Mathf.CeilToInt(voteTimeRemaining):00}s";
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
        friendlyPlaceholderOpen = false;
        currentScreen = WDHomeMatchFlowScreen.None;
        backAction = null;
        gameObject.SetActive(false);
    }

    public void HandleEscape()
    {
        if (!TryHandleLocalBack())
            RequestNavigationBack();
    }

    public bool TryHandleLocalBack()
    {
        if (currentScreen != WDHomeMatchFlowScreen.CompetitiveFlow || backAction == null)
            return false;

        Back();
        return true;
    }

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
            string.Empty, true);
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
            RequestNavigationBack, string.Empty, true);
    }

    private void ShowOfficialTournaments()
    {
        ResetNavigationState(WDHomeMatchFlowScreen.OfficialTournaments);
        var options = new List<Option>
        {
            new("CAMPEONATO WAR DOMINION",
                "TEMPORADA ATUAL · INSCRIÇÕES FECHADAS · PERÍODO E FORMATO A DEFINIR",
                WDContentAvailability.Available, ShowSpectatorPlaceholder,
                actionLabel: "ASSISTIR"),
            new("DOMÍNIO GLOBAL",
                "ATIVO · TORNEIO ESTRATÉGICO OFICIAL · DADOS MOCK",
                WDContentAvailability.Available, ShowTournamentParticipationPlaceholder,
                actionLabel: "PARTICIPAR"),
            new("CONFRONTO DE EQUIPES",
                "ATIVO · FORMATO E PERÍODO A DEFINIR · DADOS MOCK",
                WDContentAvailability.Available, ShowTournamentParticipationPlaceholder,
                actionLabel: "PARTICIPAR"),
            new("BATTLE ROYALE",
                "FORMATO ESPECIAL · MAIS DETALHES SERÃO REVELADOS EM BREVE",
                WDContentAvailability.Available, ShowTournamentDetailsPlaceholder,
                actionLabel: "DETALHES")
        };

        ShowOptions("TORNEIOS", "ATIVOS   |   EM BREVE   |   ENCERRADOS",
            options, RequestNavigationBack,
            "DADOS PROVISÓRIOS · INSCRIÇÃO OFICIAL CUSTARÁ 10 ESMERALDAS");
    }

    private void ShowFriendlyHub()
    {
        ResetNavigationState(WDHomeMatchFlowScreen.FriendlyHub);
        friendlyPlaceholderOpen = true;
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
            options, RequestNavigationBack, string.Empty, true);
    }

    private void ShowFriendlyMatchesHub()
    {
        ResetNavigationState(WDHomeMatchFlowScreen.FriendlyMatchesHub);
        friendlyPlaceholderOpen = true;
        var options = new List<Option>
        {
            new("CRIAR PARTIDA", "CONFIGURE UMA PARTIDA AMISTOSA",
                WDContentAvailability.Available,
                () => NavigateToProvisionalEndpoint("friendly/create-match", "CRIAR PARTIDA"),
                actionLabel: "CRIAR"),
            new("SALAS CRIADAS", "ENCONTRE PARTIDAS CRIADAS POR OUTROS JOGADORES",
                WDContentAvailability.Available,
                () => NavigateToProvisionalEndpoint("friendly/match-rooms", "SALAS CRIADAS"),
                actionLabel: "ACESSAR")
        };
        ShowOptions("PARTIDAS AMISTOSAS", "Crie ou participe de uma sala amistosa",
            options, RequestNavigationBack, string.Empty, true);
    }

    private void ShowFriendlyTournamentsHub()
    {
        ResetNavigationState(WDHomeMatchFlowScreen.FriendlyTournamentsHub);
        friendlyPlaceholderOpen = true;
        var options = new List<Option>
        {
            new("CRIAR TORNEIO", "CONFIGURE UM NOVO TORNEIO AMISTOSO",
                WDContentAvailability.Available,
                () => NavigateToProvisionalEndpoint("friendly/create-tournament", "CRIAR TORNEIO"),
                actionLabel: "CRIAR"),
            new("SALAS DE TORNEIOS", "ENCONTRE TORNEIOS CRIADOS POR OUTROS JOGADORES",
                WDContentAvailability.Available,
                () => NavigateToProvisionalEndpoint("friendly/tournament-rooms", "SALAS DE TORNEIOS"),
                actionLabel: "ACESSAR")
        };
        ShowOptions("TORNEIOS AMISTOSOS", "Crie seu torneio ou entre em uma sala de torneio",
            options, RequestNavigationBack, string.Empty, true);
    }

    private void NavigateToProvisionalEndpoint(string id, string title)
    {
        NavigateTo($"play/{id}", () => ShowProvisionalEndpoint(title));
    }

    private void ShowProvisionalEndpoint(string title)
    {
        ResetNavigationState(WDHomeMatchFlowScreen.ProvisionalEndpoint);
        ShowStatus(title,
            "Endpoint provisório do protótipo. Nenhum matchmaking, sala ou partida foi iniciado.",
            RequestNavigationBack);
    }

    private void ShowTournamentParticipationPlaceholder() =>
        NavigateToProvisionalEndpoint("tournaments/participate", "PARTICIPAR");

    private void ShowTournamentDetailsPlaceholder() =>
        NavigateToProvisionalEndpoint("tournaments/details", "DETALHES DO TORNEIO");

    private void ShowSpectatorPlaceholder()
    {
        NavigateTo("play/tournaments/spectate", () =>
        {
            ResetNavigationState(WDHomeMatchFlowScreen.ProvisionalEndpoint);
            ShowStatus("ASSISTIR",
                "Modo Espectador ainda não implementado. Nenhuma partida foi carregada como jogador.",
                RequestNavigationBack);
        });
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
            restore();
        }));
    }

    private void RequestNavigationBack()
    {
        navigationStack?.TryBack();
    }

    private void ResetNavigationState(WDHomeMatchFlowScreen screen)
    {
        currentScreen = screen;
        friendlyPlaceholderOpen = screen == WDHomeMatchFlowScreen.FriendlyHub ||
            screen == WDHomeMatchFlowScreen.FriendlyMatchesHub ||
            screen == WDHomeMatchFlowScreen.FriendlyTournamentsHub;
        selectedMode = null;
        selectedSubmode = null;
    }

    private void SelectMode(WDMatchModeDefinition mode)
    {
        if (!WDContentAvailabilityRules.IsAvailable(mode.Availability))
            return;

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
            ShowModes, string.Empty);
    }

    private void SelectSubmode(WDMatchSubmodeDefinition submode)
    {
        if (!WDContentAvailabilityRules.IsAvailable(submode.Availability))
            return;

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
            ShowSubmodes, string.Empty);
    }

    private void SelectClanTeammate()
    {
        if (!homeData.HasClan)
        {
            feedbackLabel.text = "É necessário participar de um clã para convidar companheiros.";
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
        ShowConfirmation(
            "CONVIDAR AMIGO",
            "Fluxo provisório de convite. O grupo só entrará no matchmaking quando todas as vagas estiverem preenchidas.",
            "SIMULAR EQUIPE COMPLETA",
            () => BeginMatchmaking(
                new WDMatchmakingRequest(selectedMode, selectedSubmode, "friend_invite"),
                ShowTeamFormation),
            ShowTeamFormation);
    }

    // ============================================================
    // 04. MATCHMAKING LOCAL, CONTADOR E IDENTIDADE PROTEGIDA
    // ============================================================

    private void BeginMatchmaking(WDMatchmakingRequest request, Action onCancel)
    {
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
        selectedDeckIndex = homeData.DefaultDeckIndex;
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
        selectedDeckIndex = homeData.DefaultDeckIndex;

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

        bool usesProfileDeck = voteRequest != null &&
            voteRequest.DeckSelectionEnabled;
        for (int i = 0; usesProfileDeck && i < homeData.Decks.Count; i++)
        {
            int deckIndex = i;
            WDDeckProfile deck = homeData.Decks[i];
            bool isSelected = deckIndex == selectedDeckIndex;
            options.Add(new Option(
                deck.DisplayName,
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
        if (!voteActive || deckIndex < 0 || deckIndex >= homeData.Decks.Count)
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
        WDMatchSetup setup = WDMatchSetupFactory.Create(
            request, map, homeData, selectedDeckIndex, 0,
            armoryCatalog, armoryProfile);
        WDMatchSetupContext.Store(setup);
        GetComponentInParent<WarDominionHomeController>()?
            .PrepareForSceneTransition();
        if (!WDMatchSetupContext.TryLoadCurrentScene())
        {
            ShowStatus(
                "PARTIDA INDISPONÍVEL",
                "A cena do mapa não está configurada para carregamento.",
                selectedSubmode != null ? ShowSubmodes : ShowModes);
        }
    }

    private void ShowStatus(string title, string body, Action onBack)
    {
        voteActive = false;
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
        var options = new List<Option>
        {
            new(actionLabel, string.Empty, WDContentAvailability.Available, action)
        };
        ShowOptions(title, body, options, onBack, string.Empty);
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
        Action onBack, string feedback, bool compactCentered = false)
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
