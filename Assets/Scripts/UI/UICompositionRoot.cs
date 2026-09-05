using TMPro;
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
    private MatchUIPresenter presenter;
    private RectTransform safeArea;
    private RoundAnnouncementView roundAnnouncement;
    private ResolutionSequenceController resolutionSequence;
    private AttackResolutionPresenter attackResolutionPresenter;
    private ReinforcementResolutionPresenter reinforcementResolutionPresenter;
    private TransferResolutionPresenter transferResolutionPresenter;
    private ResolutionVisualStateCoordinator resolutionVisualStateCoordinator;
    private PreparedActionsOverlayController preparedActionsOverlay;
    private WarDominionMatchHUD matchHUD;
    private WDInitialTerritorySelectionView initialTerritorySelectionView;
    private bool possuiFaseAnterior;
    private GameManager.FaseTurno faseAnterior;
    private string ultimoCicloResolucaoAnunciado = string.Empty;
    private Rect ultimaAreaSegura;
    private Vector2Int ultimaResolucao;

    private void Awake()
    {
        GarantirSistemaInputUI();
        GarantirEstrutura();
        Inicializar(GetComponent<MatchUIPresenter>());
    }

    private void OnDestroy()
    {
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

        RectTransform anuncioRect = CriarCamada("RoundAnnouncement", notificationLayer);
        roundAnnouncement = anuncioRect.gameObject.AddComponent<RoundAnnouncementView>();
        roundAnnouncement.Construir(tema);

        attackResolutionPresenter =
            mapOverlayLayer.gameObject.AddComponent<AttackResolutionPresenter>();
        attackResolutionPresenter.Configure(tema);

        reinforcementResolutionPresenter =
            mapOverlayLayer.gameObject.AddComponent<ReinforcementResolutionPresenter>();
        reinforcementResolutionPresenter.Configure(tema);

        transferResolutionPresenter =
            mapOverlayLayer.gameObject.AddComponent<TransferResolutionPresenter>();
        transferResolutionPresenter.Configure(tema);

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
        matchHUD = finalHUD.gameObject.AddComponent<WarDominionMatchHUD>();
        matchHUD.Build(tema, GetComponent<MatchUIPresenter>()?.Comandos);

        RectTransform initialSelectionRect = CriarCamada("InitialTerritorySelection", modalLayer);
        initialTerritorySelectionView =
            initialSelectionRect.gameObject.AddComponent<WDInitialTerritorySelectionView>();
        initialTerritorySelectionView.Build(tema, GetComponent<GameManager>());
        DefinirHUDNovoAtivo(PlayerPrefs.GetInt(ChaveHUDNovo, 1) == 1);
        AtualizarSafeArea(true);
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
        if (matchHUD != null)
            matchHUD.gameObject.SetActive(ativo);
        HUDPreparacao legado = GetComponent<HUDPreparacao>();
        if (legado != null)
            legado.enabled = !ativo;
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
