using TMPro;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(GameManager))]
public sealed class UICompositionRoot : MonoBehaviour
{
    // ============================================================
    // 01. COMPOSIÇÃO E CICLO DE VIDA
    // ============================================================

    private const string CaminhoTema = "UI/WarDominionUITheme";
    public const string ChaveHUDNovo = "WarDominion.UI.HUDNovoAtivo";
    public static bool IsNewHudSelected() => PlayerPrefs.GetInt(ChaveHUDNovo, 1) == 1;
    private MatchUIPresenter presenter;
    private RectTransform safeArea;
    private RoundAnnouncementView roundAnnouncement;
    private ResolutionSequenceController resolutionSequence;
    private AttackResolutionPresenter attackResolutionPresenter;
    private RegionConquestResolutionPresenter regionConquestResolutionPresenter;
    private ReinforcementResolutionPresenter reinforcementResolutionPresenter;
    private TransferResolutionPresenter transferResolutionPresenter;
    private WeaponResolutionPresenter weaponResolutionPresenter;
    private ResolutionVisualStateCoordinator resolutionVisualStateCoordinator;
    private PreparedActionsOverlayController preparedActionsOverlay;
    private WarDominionMatchHUD matchHUD;
    private WDWeaponMatchUIController weaponUI;
    private WDWeaponInstallationMapPresenter weaponInstallationPresenter;
    private WDMatchInformationalOverlayController informationalOverlay;
    private WDInitialTerritorySelectionView initialTerritorySelectionView;
    private WDMatchResultView resultView;
    private WDLocalMatchResultService matchResults;
    private string presentedResultId;
    private bool possuiFaseAnterior;
    private GameManager.FaseTurno faseAnterior;
    private string ultimoCicloResolucaoAnunciado = string.Empty;
    private Rect ultimaAreaSegura;
    private Vector2Int ultimaResolucao;
    private string activeChatMatchId;

    private void Awake()
    {
        GarantirSistemaInputUI();
        GarantirEstrutura();
        Inicializar(GetComponent<MatchUIPresenter>());
    }

    private void OnDestroy()
    {
        if (!string.IsNullOrEmpty(activeChatMatchId))
            WDChatRuntime.Current?.EndMatch(activeChatMatchId);
        DesconectarPresenter();
    }

    public void Inicializar(MatchUIPresenter novoPresenter)
    {
        GarantirEstrutura();
        matchHUD?.BindCommands(novoPresenter?.Comandos);

        if (presenter == novoPresenter)
        {
            presenter?.SolicitarSnapshotAtual();
            return;
        }

        DesconectarPresenter();
        presenter = novoPresenter;

        if (presenter == null)
            return;

        presenter.EstadoAlterado += AoEstadoAlterado;
        presenter.SolicitarSnapshotAtual();
    }

    private void DesconectarPresenter()
    {
        if (presenter != null)
            presenter.EstadoAlterado -= AoEstadoAlterado;

        presenter = null;
    }

    // ============================================================
    // 02. EVENTSYSTEM ÚNICO PARA A INTERFACE DA PARTIDA
    // ============================================================

    private static void GarantirSistemaInputUI()
    {
        EventSystem[] sistemas = FindObjectsByType<EventSystem>(
            FindObjectsInactive.Exclude);

        EventSystem principal = EventSystem.current;
        if (principal == null && sistemas.Length > 0)
            principal = sistemas[0];

        if (principal == null)
        {
            var objeto = new GameObject("EventSystem", typeof(EventSystem));
            principal = objeto.GetComponent<EventSystem>();
            objeto.AddComponent<InputSystemUIInputModule>();
            return;
        }

        if (principal.GetComponent<BaseInputModule>() == null)
            principal.gameObject.AddComponent<InputSystemUIInputModule>();

        foreach (EventSystem sistema in sistemas)
        {
            if (sistema != null && sistema != principal)
            {
                sistema.enabled = false;
                Debug.LogWarning(
                    "UI | EventSystem duplicado desativado durante a partida.",
                    sistema);
            }
        }
    }

    // ============================================================
    // 03. CANVAS E HIERARQUIA REUTILIZÁVEL
    // ============================================================

    private void GarantirEstrutura()
    {
        if (roundAnnouncement != null)
            return;

        WarDominionUITheme tema = Resources.Load<WarDominionUITheme>(CaminhoTema);
        if (tema == null)
        {
            Debug.LogError($"Tema da UI não encontrado em Resources/{CaminhoTema}.", this);
            return;
        }

        Transform existente = transform.Find("UIRoot");
        GameObject raiz = existente != null
            ? existente.gameObject
            : new GameObject(
                "UIRoot",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

        RectTransform raizRect = raiz.GetComponent<RectTransform>();
        raizRect.SetParent(transform, false);
        raizRect.localScale = Vector3.one;

        Canvas canvas = raiz.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = raiz.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        safeArea = CriarCamada("SafeArea", raizRect);
        CriarCamada("LeftDock", safeArea);
        CriarCamada("RightDock", safeArea);
        CriarCamada("BottomActionBar", safeArea);
        RectTransform mapOverlayLayer = CriarCamada("MapOverlayLayer", raizRect);
        RectTransform notificationLayer = CriarCamada("NotificationLayer", raizRect);
        RectTransform modalLayer = CriarCamada("ModalLayer", raizRect);
        RectTransform regionInfoOverlay = CriarCamada("RegionInfoOverlay", safeArea);

        RectTransform anuncioRect = CriarCamada("RoundAnnouncement", notificationLayer);
        roundAnnouncement = anuncioRect.gameObject.AddComponent<RoundAnnouncementView>();
        roundAnnouncement.Construir(tema);

        attackResolutionPresenter =
            mapOverlayLayer.gameObject.AddComponent<AttackResolutionPresenter>();
        attackResolutionPresenter.Configure(tema);

        regionConquestResolutionPresenter =
            mapOverlayLayer.gameObject.AddComponent<RegionConquestResolutionPresenter>();

        reinforcementResolutionPresenter =
            mapOverlayLayer.gameObject.AddComponent<ReinforcementResolutionPresenter>();
        reinforcementResolutionPresenter.Configure(tema);

        transferResolutionPresenter =
            mapOverlayLayer.gameObject.AddComponent<TransferResolutionPresenter>();
        transferResolutionPresenter.Configure(tema);

        weaponResolutionPresenter =
            mapOverlayLayer.gameObject.AddComponent<WeaponResolutionPresenter>();
        weaponResolutionPresenter.Configure(tema);

        resolutionVisualStateCoordinator =
            mapOverlayLayer.gameObject.AddComponent<ResolutionVisualStateCoordinator>();

        resolutionSequence =
            mapOverlayLayer.gameObject.AddComponent<ResolutionSequenceController>();
        resolutionSequence.Configure(mapOverlayLayer, roundAnnouncement);

        preparedActionsOverlay =
            mapOverlayLayer.gameObject.AddComponent<PreparedActionsOverlayController>();
        preparedActionsOverlay.Configure(
            GetComponent<MatchUIPresenter>(), mapOverlayLayer, tema);

        RectTransform finalHUD = CriarCamada("FinalMatchHUD", safeArea);
        weaponUI = GetComponent<WDWeaponMatchUIController>() ??
            gameObject.AddComponent<WDWeaponMatchUIController>();
        weaponUI.Configure(GetComponent<GameManager>());
        weaponInstallationPresenter =
            mapOverlayLayer.gameObject.AddComponent<WDWeaponInstallationMapPresenter>();
        weaponInstallationPresenter.Configure(mapOverlayLayer, tema, weaponUI);
        informationalOverlay = mapOverlayLayer.gameObject.AddComponent<WDMatchInformationalOverlayController>();
        informationalOverlay.Configure(mapOverlayLayer, tema, regionInfoOverlay);
        RectTransform chatLayer = CriarCamada("SharedChatHub", modalLayer);
        WDChatRepository chatRepository = EnsureChatRuntime();
        WDChatHubPanel chatPanel = chatLayer.gameObject.AddComponent<WDChatHubPanel>();
        chatPanel.Build(tema, chatRepository, chatRepository.Social, chatRepository.Profiles);
        WDChatChannel matchChat = CreateMatchChat(chatRepository, GetComponent<GameManager>()?.MatchSetupAtual);
        matchHUD = finalHUD.gameObject.AddComponent<WarDominionMatchHUD>();
        matchHUD.Build(tema, GetComponent<MatchUIPresenter>()?.Comandos, weaponUI,
            resolutionSequence, informationalOverlay,
            () => chatPanel.OpenLocal(chatPanel.HideFromNavigation, matchChat),
            chatRepository, matchChat?.Id);

        RectTransform initialSelectionRect = CriarCamada("InitialTerritorySelection", modalLayer);
        initialTerritorySelectionView =
            initialSelectionRect.gameObject.AddComponent<WDInitialTerritorySelectionView>();
        initialTerritorySelectionView.Build(tema, GetComponent<GameManager>());
        RectTransform resultRect = CriarCamada("MatchResult", modalLayer);
        resultView = resultRect.gameObject.AddComponent<WDMatchResultView>();
        resultView.Build(tema);
        DefinirHUDNovoAtivo(IsNewHudSelected());
        AtualizarSafeArea(true);
    }

    // ============================================================
    // 04. CHAT COMPARTILHADO E CONTEXTO TEMPORÁRIO DA PARTIDA
    // ============================================================

    private static WDChatRepository EnsureChatRuntime()
    {
        if (WDChatRuntime.Current != null)
            return WDChatRuntime.Current;
        WarDominionHomeData homeData = Resources.Load<WarDominionHomeData>(
            "UI/Home/WarDominionHomeMockData");
        var profiles = new WDProfileRepository(homeData);
        var social = new WDSocialRepository(profiles);
        var repository = new WDChatRepository(profiles, social);
        WDChatRuntime.Initialize(repository);
        return repository;
    }

    private WDChatChannel CreateMatchChat(WDChatRepository repository, WDMatchSetup setup)
    {
        if (setup == null)
            return null;
        activeChatMatchId = setup.DeterministicSeed.ToString();
        WDMatchParticipant local = setup.Participants.FirstOrDefault(participant =>
            participant.Kind == WDMatchParticipantKind.Local) ?? setup.Participants.FirstOrDefault();
        if (local == null)
            return null;
        return repository.CreateMatchTeamChannel(activeChatMatchId, local.PlayerId,
            setup.Participants.Select(participant => (participant.PlayerId, participant.TeamIndex)));
    }

    private static RectTransform CriarCamada(string nome, Transform pai)
    {
        Transform existente = pai.Find(nome);
        RectTransform rect;

        if (existente != null)
        {
            rect = existente as RectTransform;
        }
        else
        {
            var objeto = new GameObject(nome, typeof(RectTransform));
            rect = objeto.GetComponent<RectTransform>();
            rect.SetParent(pai, false);
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    // ============================================================
    // 03. SAFE AREA E RESPONSIVIDADE BÁSICA
    // ============================================================

    private void Update()
    {
        AtualizarSafeArea(false);
    }

    private void AtualizarSafeArea(bool forcar)
    {
        if (safeArea == null || Screen.width <= 0 || Screen.height <= 0)
            return;

        Rect area = Screen.safeArea;
        var resolucao = new Vector2Int(Screen.width, Screen.height);

        if (!forcar && area == ultimaAreaSegura && resolucao == ultimaResolucao)
            return;

        ultimaAreaSegura = area;
        ultimaResolucao = resolucao;
        safeArea.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
        safeArea.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
        safeArea.offsetMin = Vector2.zero;
        safeArea.offsetMax = Vector2.zero;
    }

    // ============================================================
    // 04. LIGAÇÃO DA VIEW AO ESTADO
    // ============================================================

    private void AoEstadoAlterado(MatchUIState state)
    {
        if (state == null)
            return;

        matchHUD?.BindCommands(presenter?.Comandos);

        matchHUD?.Present(state);
        informationalOverlay?.Present(state);
        PresentMatchResult(state);

        bool entrouEmResolucao =
            possuiFaseAnterior &&
            faseAnterior != GameManager.FaseTurno.Resolucao &&
            state.Fase == GameManager.FaseTurno.Resolucao;

        faseAnterior = state.Fase;
        possuiFaseAnterior = true;

        if (!entrouEmResolucao)
            return;

        string cicloResolucao = state.EmMorteSubita
            ? $"morte-subita:{state.RoundMorteSubita}"
            : $"round:{state.Round}";

        if (cicloResolucao == ultimoCicloResolucaoAnunciado)
            return;

        ultimoCicloResolucaoAnunciado = cicloResolucao;
        roundAnnouncement?.ExibirResolucao(state);
    }

    // 04. O resultado fechado do GameManager é publicado uma vez e apenas apresentado aqui.
    private void PresentMatchResult(MatchUIState state)
    {
        if (state.Resultado?.Encerrada != true || resultView == null ||
            WDMatchSetupContext.Current == null) return;
        WDMatchSetup setup = WDMatchSetupContext.Current;
        if (presentedResultId == setup.MatchId) return;
        var counts = state.Jogadores.ToDictionary(item => (int)item.Jogador - 1,
            item => item.TerritoriosControlados);
        WDMatchCompletion outcome = WDMatchCompletion.Create(setup,
            GetComponent<GameManager>()?.ResultadoPartidaAtual, counts, DateTime.UtcNow.Ticks);
        if (outcome == null) return;
        matchResults ??= new WDLocalMatchResultService(
            new WDPlayerPrefsLocalMatchProgressStore());
        matchResults.TryApply(outcome);
        resultView.Present(outcome, matchResults.Progress.trophies);
        presentedResultId = outcome.ResultId;
    }

    public void ExibirInicioRound(int round, System.Action concluir)
    {
        GarantirEstrutura();
        if (roundAnnouncement == null)
        {
            concluir?.Invoke();
            return;
        }
        void AoConcluir()
        {
            roundAnnouncement.Completed -= AoConcluir;
            concluir?.Invoke();
        }
        roundAnnouncement.Completed += AoConcluir;
        roundAnnouncement.ExibirInicioRound(round);
    }

    // ============================================================
    // 05. COMPARAÇÃO CONTROLADA ENTRE HUD FINAL E LEGADO
    // ============================================================

    public void DefinirHUDNovoAtivo(bool ativo)
    {
        PlayerPrefs.SetInt(ChaveHUDNovo, ativo ? 1 : 0);
        PlayerPrefs.Save();
        ApplyHudMode(matchHUD != null ? matchHUD.gameObject : null,
            GetComponent<HUDPreparacao>(), ativo);
    }

    public static void ApplyHudMode(GameObject newHud, Behaviour legacyHud, bool useNewHud)
    {
        // 02. Uma seleção controla ambas as composições, inclusive no modo de desenvolvimento.
        if (legacyHud != null)
            legacyHud.enabled = !useNewHud;
        if (newHud != null)
            newHud.SetActive(useNewHud);
    }
}

public sealed class WDMatchExitView : MonoBehaviour
{
    // ============================================================
    // 06. SAÍDA PROVISÓRIA COM CONFIRMAÇÃO
    // ============================================================

    private WarDominionUITheme theme;
    private GameObject confirmation;

    public void Build(WarDominionUITheme newTheme)
    {
        theme = newTheme;

        RectTransform exitRect = WDUIFactory.Rect("ExitMatch", transform);
        exitRect.anchorMin = exitRect.anchorMax = new Vector2(1f, 0f);
        exitRect.pivot = new Vector2(1f, 0f);
        exitRect.anchoredPosition = new Vector2(-24f, 24f);
        exitRect.sizeDelta = new Vector2(170f, 48f);
        WDUIPremiumButton exit = exitRect.gameObject.AddComponent<WDUIPremiumButton>();
        exit.Build(theme, "SAIR DA PARTIDA", ShowConfirmation);

        BuildConfirmation();
    }

    private void BuildConfirmation()
    {
        RectTransform blocker = WDUIFactory.Rect("ExitConfirmation", transform);
        blocker.anchorMin = Vector2.zero;
        blocker.anchorMax = Vector2.one;
        blocker.offsetMin = blocker.offsetMax = Vector2.zero;
        Image blockerImage = blocker.gameObject.AddComponent<Image>();
        blockerImage.color = new Color(0f, 0f, 0f, 0.82f);
        blockerImage.raycastTarget = true;
        confirmation = blocker.gameObject;

        RectTransform panel = WDUIFactory.Rect("Panel", blocker);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(480f, 220f);
        panel.gameObject.AddComponent<WDUIPanel>().Build(theme, true);

        TextMeshProUGUI title = WDUIFactory.Text(
            "Title", panel, theme, theme.TypeTitle,
            theme.TextoPrincipal, TextAlignmentOptions.Center);
        title.text = "SAIR DA PARTIDA?";
        title.rectTransform.anchorMin = new Vector2(0.08f, 0.62f);
        title.rectTransform.anchorMax = new Vector2(0.92f, 0.90f);
        title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;

        TextMeshProUGUI body = WDUIFactory.Text(
            "Body", panel, theme, theme.TypeBody,
            theme.TextoSecundario, TextAlignmentOptions.Center);
        body.text = "Retornar à Home? Não há penalidade nesta etapa local.";
        body.rectTransform.anchorMin = new Vector2(0.08f, 0.42f);
        body.rectTransform.anchorMax = new Vector2(0.92f, 0.62f);
        body.rectTransform.offsetMin = body.rectTransform.offsetMax = Vector2.zero;

        BuildAction(panel, "CONFIRMAR", new Vector2(0.08f, 0.10f),
            WDMatchSetupContext.ExitToHome);
        BuildAction(panel, "CANCELAR", new Vector2(0.52f, 0.10f),
            HideConfirmation);
        confirmation.SetActive(false);
    }

    private void BuildAction(
        Transform parent, string label, Vector2 anchor,
        UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = WDUIFactory.Rect(label, parent);
        rect.anchorMin = anchor;
        rect.anchorMax = new Vector2(anchor.x + 0.40f, anchor.y + 0.22f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        WDUIPremiumButton button = rect.gameObject.AddComponent<WDUIPremiumButton>();
        button.Build(theme, label, action);
    }

    private void ShowConfirmation() => confirmation.SetActive(true);
    private void HideConfirmation() => confirmation.SetActive(false);
}
