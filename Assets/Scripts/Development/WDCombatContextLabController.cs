using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Harness de desenvolvimento que aciona os presenters reais da partida sobre
/// clones isolados de territórios da cena oficial.
/// </summary>
[DisallowMultipleComponent]
public sealed class WDCombatContextLabController : MonoBehaviour
{
    private sealed class PreparationBinding
    {
        public PreparedActionArrowView View;
        public TerritorioClique From;
        public TerritorioClique To;
        public WDPreparedActionVisualMode Mode;
    }
    public enum Scenario
    {
        TerritorySelection,
        PossibleDestinations,
        ConfirmedAttack,
        ConqueredAttack,
        DefendedAttack,
        AlliedMovement,
        Reinforcement,
        OwnershipTransfer
    }
    public enum ReinforcementMode { IndividualTouch, HoldAllAvailable }

    [Header("Territórios reais clonados de WarGame")]
    [SerializeField] private TerritorioClique origin;
    [SerializeField] private TerritorioClique enemy;
    [SerializeField] private TerritorioClique ally;
    [SerializeField] private TerritorioClique handoff;
    [Header("Composição real da partida")]
    [SerializeField] private Camera mapCamera;
    [SerializeField] private RectTransform mapOverlay;
    [SerializeField] private WarDominionUITheme theme;
    [Header("Controles exclusivos do laboratório")]
    [SerializeField] private TMP_Dropdown scenarioDropdown;
    [SerializeField] private TMP_Text speedLabel;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private Button startButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button stopButton;
    [SerializeField] private Button speedButton;
    [Header("Indicadores de possibilidades — somente laboratório")]
    [SerializeField, Range(0f, 1f)] private float indicatorOpacity =
        WDPreparedIndicatorStyle.LabApprovedIndicatorOpacity;
    [SerializeField, Range(4f, 18f)] private float chevronArmLength =
        WDPreparedIndicatorStyle.LabApprovedChevronArmLength;
    [SerializeField, Range(1f, 8f)] private float chevronThickness =
        WDPreparedIndicatorStyle.LabApprovedChevronThickness;
    [SerializeField, Range(1f, 8f)] private float dotRadius =
        WDPreparedIndicatorStyle.LabApprovedDotRadius;
    [SerializeField, Range(0f, 2f)] private float glowIntensity =
        WDPreparedIndicatorStyle.LabApprovedGlowIntensity;
    [SerializeField, Range(0f, 2f)] private float contrast =
        WDPreparedIndicatorStyle.LabApprovedContrast;
    [SerializeField, Range(0f, 3f)] private float animationSpeedMultiplier =
        WDPreparedIndicatorStyle.LabApprovedAnimationSpeedMultiplier;

    [Header("Ataque confirmado — somente laboratório")]
    [Tooltip("Multiplicador cromático aplicado à cor/skin da origem.")]
    [SerializeField] private Color confirmedArrowTint = Color.white;
    [SerializeField, Range(0f, 1f)] private float confirmedArrowOpacity = .96f;
    [SerializeField, Range(.5f, 20f)] private float confirmedBodyThickness = 12.7f;
    [SerializeField, Range(0f, 8f)] private float confirmedOutlineThickness = 1.25f;
    [SerializeField] private Color confirmedOutlineColor = Color.black;
    [SerializeField, Range(0f, 4f)] private float confirmedOuterGlow;
    [SerializeField, Range(0f, 2f)] private float confirmedReflection;
    [SerializeField, Range(4f, 50f)] private float confirmedHeadLength = 18f;
    [SerializeField, Range(4f, 50f)] private float confirmedHeadWidth = 24.1f;
    [Space(4f)]
    [SerializeField] private Color confirmedCardBackground =
        new(6f / 255f, 11f / 255f, 17f / 255f, 245f / 255f);
    [SerializeField] private Color confirmedCardBorder = Color.white;
    [SerializeField, Range(0f, 8f)] private float confirmedCardBorderThickness = 1.93f;
    [SerializeField, Range(0f, 1f)] private float confirmedCardTopReflection;
    [SerializeField, Range(0f, 30f)] private float confirmedCardCornerRadius = 10f;
    [SerializeField, Range(24f, 180f)] private float confirmedCardWidth = 39.7f;
    [SerializeField, Range(20f, 100f)] private float confirmedCardHeight = 36.1f;
    [SerializeField] private Color confirmedNumberColor = Color.white;
    [SerializeField, Range(8f, 48f)] private float confirmedNumberSize = 19.3f;
    [Space(4f)]
    [SerializeField] private bool confirmedAdaptiveOutline = true;
    [SerializeField, Range(0f, 12f)] private float confirmedShadowThickness;
    [SerializeField, Range(0f, 1f)] private float confirmedShadowIntensity;
    [SerializeField, Range(0f, 2f)] private float confirmedSpecularIntensity;
    [SerializeField, Range(0f, 8f)] private float confirmedReliefIntensity = 3.74f;
    [SerializeField, Range(0f, 1f)] private float confirmedSurfaceOpacity = 1f;
    [SerializeField, Range(0f, 40f)] private float confirmedOriginMargin = 5f;
    [SerializeField, Range(0f, 40f)] private float confirmedDestinationMargin = 5f;
    [SerializeField, Range(-120f, 120f)] private float confirmedCurveHeight = 10f;
    [SerializeField, Range(-40f, 40f)] private float confirmedOriginExtension = -5f;
    [SerializeField, Range(-40f, 40f)] private float confirmedDestinationExtension = -.9f;
    [SerializeField, Range(0f, 1f)] private float confirmedCurvePosition = .379f;

    [Header("Ataque conquistado — somente laboratório")]
    [SerializeField, InspectorName("Travel Duration"), Range(.1f, 3f)]
    private float conqueredTravelDuration = .55f;
    [SerializeField, InspectorName("Travel Ease")]
    private WDAttackTravelEase conqueredTravelEase = WDAttackTravelEase.EaseOutCubic;
    [SerializeField, InspectorName("Contact Hold Duration"), Range(0f, 1f)]
    private float conqueredContactHoldDuration = .22f;
    [SerializeField, InspectorName("Contact Pressure Intensity"), Range(0f, .3f)]
    private float conqueredContactPressureIntensity = .04f;
    [SerializeField, InspectorName("Contact Separation"), Range(.1f, 2f)]
    private float conqueredContactSeparation = .238f;
    [SerializeField, InspectorName("Result Reveal Duration"), Range(0f, 2f)]
    private float conqueredResultDuration = .30f;
    [SerializeField, Range(-.5f, .5f)] private float conqueredCurveFactor = .06f;
    [SerializeField, Range(0f, 3f)] private float conqueredCurveHeight = 1.1f;
    [SerializeField, Range(.5f, 2f)] private float conqueredCounterScale = 1f;
    [SerializeField, Range(.1f, 2f)] private float conqueredImpactDuration = .65f;
    [SerializeField, Range(8f, 100f)] private float conqueredImpactSize = 42f;
    [SerializeField, Range(0f, 120f)] private float conqueredImpactSpread = 34f;
    [SerializeField, Range(0f, 1f)] private float conqueredImpactOpacity = .76f;

    [Header("Ataque defendido — somente laboratório")]
    [SerializeField, InspectorName("Travel Duration"), Range(.1f, 3f)]
    private float defendedTravelDuration = .55f;
    [SerializeField, InspectorName("Travel Ease")]
    private WDAttackTravelEase defendedTravelEase = WDAttackTravelEase.EaseOutCubic;
    [SerializeField, InspectorName("Contact Hold Duration"), Range(0f, 1f)]
    private float defendedContactHoldDuration = .22f;
    [SerializeField, InspectorName("Contact Pressure Intensity"), Range(0f, .3f)]
    private float defendedContactPressureIntensity = .04f;
    [SerializeField, InspectorName("Contact Separation"), Range(.1f, 2f)]
    private float defendedContactSeparation = .238f;
    [SerializeField, InspectorName("Result Reveal Duration"), Range(0f, 2f)]
    private float defendedResultDuration = .30f;
    [SerializeField, Range(-.5f, .5f)] private float defendedCurveFactor = .06f;
    [SerializeField, Range(0f, 3f)] private float defendedCurveHeight = 1.1f;
    [SerializeField, Range(.5f, 2f)] private float defendedCounterScale = 1f;
    [SerializeField, Range(.1f, 2f)] private float defendedImpactDuration = .65f;
    [SerializeField, Range(8f, 100f)] private float defendedImpactSize = 42f;
    [SerializeField, Range(0f, 120f)] private float defendedImpactSpread = 34f;
    [SerializeField, Range(0f, 1f)] private float defendedImpactOpacity = .76f;

    [Header("Resolução visual — somente laboratório")]
    [SerializeField, Range(.05f, 2f)] private float resolutionArrowDuration = .48f;
    [SerializeField, Range(.1f, 3f)] private float resolutionArrowSpeed = 1f;
    [SerializeField, Range(-.5f, .5f)] private float resolutionArrowSyncOffset;
    [SerializeField, Range(.05f, 2f)] private float conquestRevealDuration = .58f;
    [SerializeField, Range(0f, 1f)] private float conquestRevealIntensity = 1f;

    [Header("Movimento aliado — somente laboratório")]
    [SerializeField, Range(.1f, 3f)] private float movementTravelDuration = .55f;
    [SerializeField, Range(-.5f, .5f)] private float movementCurveFactor = .06f;
    [SerializeField, Range(0f, 3f)] private float movementCurveHeight = 1.1f;
    [SerializeField, Range(.5f, 2f)] private float movementCounterScale = 1f;
    [SerializeField, Range(0f, 2f)] private float movementSettleDuration = .8f;

    [Header("Reforço recebido — somente laboratório")]
    [SerializeField, Range(.05f, 2f)] private float reinforcementEntry = .18f;
    [SerializeField, InspectorName("Text Hold Duration"), Range(0f, 2f)]
    private float reinforcementReaction = .54f;
    [SerializeField, InspectorName("Fade Duration"), Range(.05f, 2f)]
    private float reinforcementExit = .28f;
    [SerializeField, Range(.25f, 2f)] private float reinforcementInitialScale = .72f;
    [SerializeField, Range(8f, 140f)] private float reinforcementPulseSize = 58f;
    [SerializeField, Range(0f, 1f)] private float reinforcementPulseOpacity = .32f;
    [SerializeField, Range(-100f, 100f)] private float reinforcementTextOffset = 42f;
    [SerializeField, Range(8f, 60f)] private float reinforcementTextSize = 21f;
    [SerializeField] private Color reinforcementTextColor =
        new(16f / 255f, 24f / 255f, 32f / 255f, 1f);
    [SerializeField, Range(0f, 1f)] private float reinforcementTextOpacity = .96f;
    [SerializeField, Range(0f, 1f)] private float reinforcementOutlineWidth = .18f;
    [SerializeField, Range(0f, 1f)] private float reinforcementOutlineOpacity = .58f;
    [SerializeField, InspectorName("Vertical Rise Distance"), Range(0f, 80f)]
    private float reinforcementRiseDistance = 24f;
    [SerializeField] private ReinforcementMode reinforcementMode =
        ReinforcementMode.HoldAllAvailable;

    [Header("Glow territorial — somente laboratório")]
    [SerializeField, Range(0f, 1f)] private float selectionGlowIntensity = .42f;
    [SerializeField, Range(.02f, 1f)] private float selectionGlowDuration = .10f;
    [SerializeField, Range(0f, 1f)] private float reinforcementGlowIntensity = .38f;
    [SerializeField, Range(.05f, 2f)] private float reinforcementGlowDuration = .45f;

    [Header("Transferência de propriedade — somente laboratório")]
    [SerializeField, Range(.05f, 2f)] private float handoffTransitionDuration = .55f;
    [SerializeField, Range(.05f, 2f)] private float handoffReactionDuration = .42f;
    [SerializeField] private Vector2 handoffBadgeSize = new(58f, 46f);
    [SerializeField] private Vector2 handoffBadgeOffset = new(0f, -52f);
    [SerializeField, Range(.5f, 10f)] private float handoffArrowThickness =
        WDTransferCycleGraphic.EmphasizedBadgeThickness;
    [SerializeField, Range(0f, 1f)] private float handoffArrowOpacity = 1f;
    [SerializeField, Range(0f, 1f)] private float handoffSurfaceOpacity = .96f;
    [SerializeField, Tooltip("Mantém o badge até INTERROMPER/REINICIAR, simulando a preparação oficial.")]
    private bool handoffPersistentPreparation = true;

    [Header("Glass Premium 2.0 — somente laboratório")]
    [SerializeField] private WDGameplayVisualFinish visualFinish =
        WDGameplayVisualFinish.ApprovedClassic;
    [SerializeField] private WDGameplayGlassVariant glassVariant =
        WDGameplayGlassVariant.Current;
    [SerializeField] private WDGameplayVisualOverride counterFinish;
    [SerializeField] private WDGameplayVisualOverride casualtyFinish;
    [SerializeField] private WDGameplayVisualOverride transferFinish;
    [SerializeField] private WDGameplayVisualOverride attackBadgeFinish;
    [SerializeField] private WDGameplayVisualOverride reinforcementFinish;
    [SerializeField, Range(0f, 1f)] private float glassIntensity = .78f;
    [SerializeField, Range(0f, 1f)] private float glassGradientStrength = .55f;
    [SerializeField, Range(0f, 1f)] private float glassReflectionIntensity = .35f;
    [SerializeField, Range(0f, 1f)] private float glassBevelStrength = .45f;
    [SerializeField, Range(0f, 1f)] private float glassGlowIntensity = .18f;

    private ResolutionSequenceController sequence;
    private AttackResolutionPresenter attackPresenter;
    private ResolutionVisualEvent activeResolutionEvent;
    private ReinforcementResolutionPresenter reinforcementPresenter;
    private TransferResolutionPresenter transferPresenter;
    private readonly List<PreparationBinding> preparationViews = new();
    private bool initialized;
    private WDTransferOwnershipBadgeView preparedTransferBadge;
    private Scenario activeScenario;
    private WDPreparedIndicatorStyle appliedIndicatorStyle;
    private bool hasAppliedIndicatorStyle;
    private WDConfirmedAttackStyle appliedConfirmedAttackStyle;
    private bool hasAppliedConfirmedAttackStyle;
    private float appliedOriginMargin;
    private float appliedDestinationMargin;
    private float appliedCurveHeight;
    private float appliedOriginExtension;
    private float appliedDestinationExtension;
    private float appliedCurvePosition;
    private bool individualReinforcementSessionActive;
    private int pendingIndividualReinforcements;

    public static IReadOnlyList<string> ScenarioLabels { get; } = new[]
    {
        "SELEÇÃO DE TERRITÓRIO",
        "POSSÍVEIS: ATAQUE + MOVIMENTO",
        "ATAQUE CONFIRMADO",
        "ATAQUE CONQUISTADO",
        "ATAQUE DEFENDIDO",
        "MOVIMENTO ALIADO",
        "REFORÇO RECEBIDO",
        "TRANSFERÊNCIA DE PROPRIEDADE"
    };

    public bool IsReady => initialized && sequence != null;
    public WDPreparedIndicatorStyle CurrentIndicatorStyle =>
        BuildIndicatorStyle();
    public WDConfirmedAttackStyle CurrentConfirmedAttackStyle =>
        BuildConfirmedAttackStyle();

    // ============================================================
    // 01. INICIALIZAÇÃO — presenters oficiais, sem regra de gameplay paralela
    // ============================================================
    private void Awake()
    {
        if (theme == null)
            theme = Resources.Load<WarDominionUITheme>("UI/WarDominionUITheme");
        if (mapCamera != null && Camera.main == null)
            mapCamera.tag = "MainCamera";

        ConfigureRealPresentationStack();
        ConfigureControls();
        RestoreBaseline();
        initialized = ValidateReferences();
        SetStatus(initialized
            ? "Laboratório pronto. Escolha um cenário e pressione INICIAR."
            : "Laboratório incompleto: regenere a cena pelo menu War Dominion.");
    }

    private void OnDisable() => StopAndRestore();

    // ============================================================
    // 01.1. AJUSTE AO VIVO — propaga mudanças do Inspector sem regenerar cena
    // ============================================================
    private void Update()
    {
        ApplyScenarioLabStyles();
        ApplyTerritoryLabFeedback();
        ApplyCounterGlassStyles();
        UpdatePreparedTransferBadge();
        WDPreparedIndicatorStyle current = BuildIndicatorStyle();
        WDConfirmedAttackStyle confirmed = BuildConfirmedAttackStyle();
        bool indicatorsChanged = !hasAppliedIndicatorStyle ||
            !SameStyle(current, appliedIndicatorStyle);
        bool confirmedChanged = !hasAppliedConfirmedAttackStyle ||
            !SameStyle(confirmed, appliedConfirmedAttackStyle);
        if (indicatorsChanged || confirmedChanged)
        {
            appliedIndicatorStyle = current;
            hasAppliedIndicatorStyle = true;
            appliedConfirmedAttackStyle = confirmed;
            hasAppliedConfirmedAttackStyle = true;
            foreach (PreparationBinding binding in preparationViews)
                if (binding.View != null)
                {
                    if (indicatorsChanged)
                        binding.View.ConfigureIndicatorStyle(current);
                    if (confirmedChanged)
                        binding.View.ConfigureConfirmedAttackStyle(confirmed);
                    binding.View.ConfigureLabGlassStyle(
                        BuildGlassStyle(attackBadgeFinish));
                }
        }
        foreach (PreparationBinding binding in preparationViews)
            binding.View?.ConfigureLabGlassStyle(
                BuildGlassStyle(attackBadgeFinish));
        if (!Mathf.Approximately(appliedOriginMargin, confirmedOriginMargin) ||
            !Mathf.Approximately(appliedDestinationMargin,
                confirmedDestinationMargin) ||
            !Mathf.Approximately(appliedCurveHeight, confirmedCurveHeight) ||
            !Mathf.Approximately(appliedOriginExtension,
                confirmedOriginExtension) ||
            !Mathf.Approximately(appliedDestinationExtension,
                confirmedDestinationExtension) ||
            !Mathf.Approximately(appliedCurvePosition,
                confirmedCurvePosition))
        {
            appliedOriginMargin = confirmedOriginMargin;
            appliedDestinationMargin = confirmedDestinationMargin;
            appliedCurveHeight = confirmedCurveHeight;
            appliedOriginExtension = confirmedOriginExtension;
            appliedDestinationExtension = confirmedDestinationExtension;
            appliedCurvePosition = confirmedCurvePosition;
            foreach (PreparationBinding binding in preparationViews)
                ApplyPreparationGeometry(binding);
        }
    }

    private void ConfigureRealPresentationStack()
    {
        if (mapOverlay == null)
            return;

        attackPresenter = GetOrAdd<AttackResolutionPresenter>(mapOverlay.gameObject);
        reinforcementPresenter =
            GetOrAdd<ReinforcementResolutionPresenter>(mapOverlay.gameObject);
        transferPresenter = GetOrAdd<TransferResolutionPresenter>(mapOverlay.gameObject);
        attackPresenter.Configure(theme);
        reinforcementPresenter.Configure(theme);
        transferPresenter.Configure(theme);
        ApplyScenarioLabStyles();

        sequence = GetOrAdd<ResolutionSequenceController>(mapOverlay.gameObject);
        sequence.Configure(mapOverlay, null);
        sequence.SequenceCompleted -= OnSequenceCompleted;
        sequence.SequenceCompleted += OnSequenceCompleted;
    }

    private void ConfigureControls()
    {
        if (scenarioDropdown != null)
        {
            scenarioDropdown.ClearOptions();
            scenarioDropdown.AddOptions(new List<string>(ScenarioLabels));
        }
        startButton?.onClick.AddListener(StartSelectedScenario);
        resetButton?.onClick.AddListener(ResetLab);
        stopButton?.onClick.AddListener(StopAndRestore);
        speedButton?.onClick.AddListener(ToggleSpeed);
        UpdateSpeedLabel();
    }

    // ============================================================
    // 02. CENÁRIOS — snapshots determinísticos consumidos pelos sistemas reais
    // ============================================================
    public void StartSelectedScenario()
    {
        if (!IsReady)
            return;

        Scenario scenario = (Scenario)Mathf.Clamp(
            scenarioDropdown != null ? scenarioDropdown.value : 0,
            0, ScenarioLabels.Count - 1);
        bool continueIndividual = scenario == Scenario.Reinforcement &&
            reinforcementMode == ReinforcementMode.IndividualTouch &&
            individualReinforcementSessionActive;
        if (continueIndividual && sequence.IsRunning)
        {
            pendingIndividualReinforcements++;
            SetStatus($"Reforço +1 aguardando ({pendingIndividualReinforcements}).");
            return;
        }
        if (!continueIndividual)
            StopAndRestore();
        activeScenario = scenario;
        SetStatus("Executando: " + ScenarioLabels[(int)scenario]);

        switch (scenario)
        {
            case Scenario.TerritorySelection:
                origin.DestacarSelecao();
                break;
            case Scenario.PossibleDestinations:
                origin.DestacarSelecao();
                CreatePreparationView(origin, enemy,
                    WDPreparedActionVisualMode.PreviewChevrons, 0);
                CreatePreparationView(origin, ally,
                    WDPreparedActionVisualMode.PreviewDots, 0);
                break;
            case Scenario.ConfirmedAttack:
                CreatePreparationView(origin, enemy,
                    WDPreparedActionVisualMode.ConfirmedAttack, 7);
                break;
            case Scenario.ConqueredAttack:
                PlaySyntheticAttack(BuildConqueredAttackEvent(
                    origin.idTerritorio, enemy.idTerritorio));
                break;
            case Scenario.DefendedAttack:
                PlaySyntheticAttack(BuildDefendedAttackEvent(
                    origin.idTerritorio, enemy.idTerritorio));
                break;
            case Scenario.AlliedMovement:
                sequence.Play(new[] { ResolutionVisualEvent.Movement(
                    TerritorioClique.Dono.Jogador1, origin.idTerritorio,
                    ally.idTerritorio, 4, 12, 8, 6, 10,
                    TerritorioClique.Dono.Jogador1,
                    TerritorioClique.Dono.Jogador1) });
                break;
            case Scenario.Reinforcement:
                individualReinforcementSessionActive = reinforcementMode ==
                    ReinforcementMode.IndividualTouch;
                PlaySyntheticReinforcement(reinforcementMode);
                break;
            case Scenario.OwnershipTransfer:
                if (handoffPersistentPreparation)
                    ShowPreparedTransferBadge();
                else
                {
                    activeResolutionEvent = ResolutionVisualEvent.TerritoryHandoff(
                        TerritorioClique.Dono.Jogador1,
                        TerritorioClique.Dono.Jogador3,
                        handoff.idTerritorio, 8, 8);
                    sequence.Play(new[] { activeResolutionEvent });
                }
                break;
        }
    }

    // ============================================================
    // 02.1. SNAPSHOTS SINTÉTICOS — resultados completos e coerentes
    // ============================================================
    public static ResolutionVisualEvent BuildConqueredAttackEvent(
        string originId, string destinationId) => ResolutionVisualEvent.Attack(
        TerritorioClique.Dono.Jogador1, originId, destinationId,
        9, 12, 3, 6, 3, true,
        TerritorioClique.Dono.Jogador2, TerritorioClique.Dono.Jogador1);

    public static ResolutionVisualEvent BuildDefendedAttackEvent(
        string originId, string destinationId) => ResolutionVisualEvent.Attack(
        TerritorioClique.Dono.Jogador1, originId, destinationId,
        7, 12, 5, 9, 6, false,
        TerritorioClique.Dono.Jogador2, TerritorioClique.Dono.Jogador2);

    public static bool HasCoherentCombatSnapshot(ResolutionVisualEvent value)
    {
        if (value == null || value.Type != ResolutionVisualEventType.Attack ||
            value.Amount <= 0 || value.OriginAfter != value.OriginBefore - value.Amount)
            return false;
        (int attackerLosses, int defenderLosses) =
            AttackResolutionPresenter.CalculateCasualties(value);
        if (attackerLosses < 0 || attackerLosses > value.Amount ||
            defenderLosses < 0 || defenderLosses > value.DestinationBefore)
            return false;
        return value.Conquered
            ? value.NewOwner == value.Player &&
                value.DestinationAfter == value.Amount - attackerLosses &&
                defenderLosses == value.DestinationBefore
            : value.NewOwner == value.PreviousOwner &&
                attackerLosses == value.Amount &&
                value.DestinationAfter == value.DestinationBefore - defenderLosses;
    }

    // 02.1.1. Cada acionamento individual produz seu próprio evento +1. O modo
    // em lote continua emitindo um único evento com todo o saldo demonstrativo.
    public static ResolutionVisualEvent BuildReinforcementEvent(string territoryId,
        ReinforcementMode mode, int before, int available = 5)
    {
        int amount = mode == ReinforcementMode.IndividualTouch
            ? 1 : Mathf.Max(0, available);
        return ResolutionVisualEvent.Reinforcement(
            TerritorioClique.Dono.Jogador1, territoryId, amount,
            Mathf.Max(0, before), Mathf.Max(0, before) + amount);
    }

    private void PlaySyntheticReinforcement(ReinforcementMode mode)
    {
        ResolutionVisualEvent visualEvent = BuildReinforcementEvent(
            origin.idTerritorio, mode, origin.Tropas, 5);
        activeResolutionEvent = visualEvent;
        sequence.Play(new[] { visualEvent });
    }

    private void PlaySyntheticAttack(ResolutionVisualEvent visualEvent)
    {
        if (!HasCoherentCombatSnapshot(visualEvent))
            throw new System.InvalidOperationException(
                "O cenário sintético de combate possui resultados incoerentes.");
        RestoreTerritory(origin, visualEvent.Player, visualEvent.OriginBefore);
        RestoreTerritory(enemy, visualEvent.PreviousOwner,
            visualEvent.DestinationBefore);
        activeResolutionEvent = visualEvent;
        sequence.Play(new[] { visualEvent });
    }

    public void ToggleSpeed()
    {
        if (sequence == null)
            return;
        sequence.TogglePlaybackSpeed();
        UpdateSpeedLabel();
    }

    public void ResetLab() => StopAndRestore();

    public void StopAndRestore()
    {
        activeResolutionEvent = null;
        pendingIndividualReinforcements = 0;
        individualReinforcementSessionActive = false;
        ClearPreparationViews();
        ClearPreparedTransferBadge();
        if (sequence != null)
        {
            float speed = sequence.PlaybackSpeed;
            sequence.enabled = false;
            sequence.enabled = true;
            sequence.Configure(mapOverlay, null);
            sequence.PlaybackSpeed = speed;
        }
        RestoreBaseline();
        SetStatus("Interrompido e restaurado ao estado inicial.");
    }

    // ============================================================
    // 03. PREPARAÇÃO — usa a mesma view, tema e projeção da partida
    // ============================================================
    private void CreatePreparationView(TerritorioClique from,
        TerritorioClique to, WDPreparedActionVisualMode mode, int amount)
    {
        if (from == null || to == null || mapOverlay == null || mapCamera == null)
            return;

        GameObject route = new("ContextLab_PreparedRoute", typeof(RectTransform));
        route.transform.SetParent(mapOverlay, false);
        PreparedActionArrowView view = route.AddComponent<PreparedActionArrowView>();
        view.Build(theme);
        view.ConfigureIndicatorStyle(BuildIndicatorStyle());
        view.ConfigureConfirmedAttackStyle(BuildConfirmedAttackStyle());
        view.ConfigureLabGlassStyle(BuildGlassStyle(attackBadgeFinish));
        view.SetColorAndAmount(PaletaJogadores.ObterCorAtiva(
            TerritorioClique.Dono.Jogador1), amount);
        view.SetMode(mode);
        var binding = new PreparationBinding
        {
            View = view,
            From = from,
            To = to,
            Mode = mode
        };
        preparationViews.Add(binding);
        ApplyPreparationGeometry(binding);
    }

    private void ApplyPreparationGeometry(PreparationBinding binding)
    {
        if (binding?.View == null || binding.From == null || binding.To == null ||
            mapOverlay == null || mapCamera == null)
            return;
        if (!MapOverlayProjection.TryWorldToOverlay(mapOverlay,
                ResolutionVisualMapUtility.GetActionWorldAnchor(binding.From),
                mapCamera, out Vector2 start) ||
            !MapOverlayProjection.TryWorldToOverlay(mapOverlay,
                ResolutionVisualMapUtility.GetActionWorldAnchor(binding.To),
                mapCamera, out Vector2 end))
            return;
        if (binding.Mode == WDPreparedActionVisualMode.ConfirmedAttack)
        {
            float originGap = SafeVisualGap(confirmedOriginMargin,
                confirmedOriginExtension);
            float destinationGap = SafeVisualGap(confirmedDestinationMargin,
                confirmedDestinationExtension);
            float originInset = CounterVisibleInset(binding.From, start, end,
                true);
            float destinationInset = CounterVisibleInset(binding.To, start, end,
                false);
            binding.View.SetGeometry(start, end,
                originInset + originGap,
                destinationInset + destinationGap, confirmedCurveHeight,
                confirmedCurvePosition);
        }
        else
        {
            binding.View.SetGeometry(start, end);
        }
    }

    // 03.1. SILHUETA VISÍVEL DO CONTADOR — o bounds retangular inclui padding
    // transparente. A forma física gerada pelo importador acompanha os pixels
    // visíveis e fornece o ponto exato onde a trajetória encontra o aro.
    private float CounterVisibleInset(TerritorioClique territory,
        Vector2 logicalStart, Vector2 logicalEnd, bool origin)
    {
        WDArmyCounterView counter = territory != null
            ? territory.GetComponentInChildren<WDArmyCounterView>(true)
            : null;
        Renderer visual = counter != null ? counter.BodyRenderer : null;
        Collider2D fallbackCollider = counter != null
            ? counter.GetComponent<Collider2D>() : null;
        if ((visual == null && fallbackCollider == null) || mapOverlay == null ||
            mapCamera == null)
            return theme != null ? theme.PreparedArrowEndpointInset : 0f;

        if (visual is SpriteRenderer spriteRenderer &&
            spriteRenderer.sprite != null)
        {
            Sprite sprite = spriteRenderer.sprite;
            var localShape = new List<Vector2>();
            float closestVisibleInset = float.PositiveInfinity;
            bool foundVisibleBoundary = false;
            for (int shapeIndex = 0;
                shapeIndex < sprite.GetPhysicsShapeCount(); shapeIndex++)
            {
                localShape.Clear();
                sprite.GetPhysicsShape(shapeIndex, localShape);
                var projectedShape = new List<Vector2>(localShape.Count);
                foreach (Vector2 point in localShape)
                {
                    Vector3 world = spriteRenderer.transform.TransformPoint(point);
                    if (MapOverlayProjection.TryWorldToOverlay(mapOverlay, world,
                            mapCamera, out Vector2 projected))
                        projectedShape.Add(projected);
                }
                if (TrySegmentBoundaryInset(logicalStart, logicalEnd,
                        projectedShape, origin, out float visibleInset))
                {
                    closestVisibleInset = Mathf.Min(closestVisibleInset,
                        visibleInset);
                    foundVisibleBoundary = true;
                }
            }
            if (foundVisibleBoundary)
                return closestVisibleInset;
        }

        // 03.1.1. FALLBACK — mantém compatibilidade com sprites sem physics
        // shape, mas usa o centro efetivo projetado em vez de presumir que o
        // bounds esteja centrado na âncora lógica.
        Bounds bounds = visual != null ? visual.bounds : fallbackCollider.bounds;
        var corners = new List<Vector2>(4);
        foreach (Vector3 world in new[] {
            new Vector3(bounds.min.x, bounds.min.y, bounds.center.z),
            new Vector3(bounds.max.x, bounds.min.y, bounds.center.z),
            new Vector3(bounds.max.x, bounds.max.y, bounds.center.z),
            new Vector3(bounds.min.x, bounds.max.y, bounds.center.z) })
        {
            if (MapOverlayProjection.TryWorldToOverlay(mapOverlay, world,
                    mapCamera, out Vector2 projected))
                corners.Add(projected);
        }
        if (TrySegmentBoundaryInset(logicalStart, logicalEnd, corners, origin,
                out float boundsInset))
            return boundsInset;
        return theme != null ? theme.PreparedArrowEndpointInset : 0f;
    }

    // 03.1.2. INTERSEÇÃO SEGMENTO/SILHUETA — retorna distância ao primeiro
    // contorno na origem ou ao último contorno antes do destino.
    public static bool TrySegmentBoundaryInset(Vector2 start, Vector2 end,
        IReadOnlyList<Vector2> polygon, bool origin, out float inset)
    {
        inset = 0f;
        if (polygon == null || polygon.Count < 2)
            return false;
        Vector2 route = end - start;
        float length = route.magnitude;
        if (length < .0001f)
            return false;
        bool found = false;
        float selected = origin ? float.PositiveInfinity : float.NegativeInfinity;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 edgeStart = polygon[i];
            Vector2 edge = polygon[(i + 1) % polygon.Count] - edgeStart;
            float denominator = Cross(route, edge);
            if (Mathf.Abs(denominator) < .000001f)
                continue;
            Vector2 offset = edgeStart - start;
            float t = Cross(offset, edge) / denominator;
            float u = Cross(offset, route) / denominator;
            if (t < -.0001f || t > 1.0001f || u < -.0001f || u > 1.0001f)
                continue;
            selected = origin ? Mathf.Min(selected, t) : Mathf.Max(selected, t);
            found = true;
        }
        if (!found)
            return false;
        inset = origin ? Mathf.Clamp01(selected) * length
            : (1f - Mathf.Clamp01(selected)) * length;
        return true;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    public static float RectRadialExtent(Vector2 halfExtents,
        Vector2 direction)
    {
        direction = direction.sqrMagnitude > .0001f
            ? direction.normalized : Vector2.right;
        float x = Mathf.Abs(direction.x) > .0001f
            ? halfExtents.x / Mathf.Abs(direction.x) : float.PositiveInfinity;
        float y = Mathf.Abs(direction.y) > .0001f
            ? halfExtents.y / Mathf.Abs(direction.y) : float.PositiveInfinity;
        return Mathf.Max(0f, Mathf.Min(x, y));
    }

    public static float SafeVisualGap(float margin, float extension) =>
        Mathf.Max(0f, Mathf.Max(0f, margin) - extension);

    private void ClearPreparationViews()
    {
        foreach (PreparationBinding binding in preparationViews)
            if (binding.View != null)
                Destroy(binding.View.gameObject);
        preparationViews.Clear();
    }

    // 03.2. TRANSFERÊNCIA PREPARADA — usa o mesmo badge da partida e só é
    // removida por cancelamento (INTERROMPER/REINICIAR) ou troca para resolução.
    private void ShowPreparedTransferBadge()
    {
        if (preparedTransferBadge != null || mapOverlay == null)
            return;
        var badgeObject = new GameObject("ContextLab_PreparedOwnershipTransfer",
            typeof(RectTransform));
        badgeObject.transform.SetParent(mapOverlay, false);
        preparedTransferBadge =
            badgeObject.AddComponent<WDTransferOwnershipBadgeView>();
        preparedTransferBadge.Build(theme, true);
        preparedTransferBadge.ConfigureGlassStyle(
            BuildGlassStyle(transferFinish));
        badgeObject.transform.SetAsLastSibling();
        UpdatePreparedTransferBadge();
        SetStatus("Transferência preparada: INTERROMPER cancela; REINICIAR encerra a preparação.");
    }

    private void UpdatePreparedTransferBadge()
    {
        if (preparedTransferBadge == null || handoff == null ||
            mapOverlay == null || mapCamera == null)
            return;
        var style = new WDTransferOwnershipBadgeStyle(true, handoffBadgeSize,
            handoffBadgeOffset, handoffArrowThickness,
            handoffArrowOpacity, handoffSurfaceOpacity);
        preparedTransferBadge.ConfigureGlassStyle(
            BuildGlassStyle(transferFinish));
        preparedTransferBadge.Configure(PaletaJogadores.ObterCorAtiva(
            TerritorioClique.Dono.Jogador1), style);
        if (MapOverlayProjection.TryWorldToOverlay(mapOverlay,
                ResolutionVisualMapUtility.GetActionWorldAnchor(handoff),
                mapCamera, out Vector2 position))
            preparedTransferBadge.SetPosition(position);
    }

    private void ClearPreparedTransferBadge()
    {
        if (preparedTransferBadge != null)
            Destroy(preparedTransferBadge.gameObject);
        preparedTransferBadge = null;
    }

    // ============================================================
    // 04. RESTAURAÇÃO — somente clones locais recebem estado determinístico
    // ============================================================
    private void RestoreBaseline()
    {
        RestoreTerritory(origin, TerritorioClique.Dono.Jogador1, 12);
        RestoreTerritory(enemy, TerritorioClique.Dono.Jogador2, 9);
        RestoreTerritory(ally, TerritorioClique.Dono.Jogador1, 6);
        RestoreTerritory(handoff, TerritorioClique.Dono.Jogador1, 8);
    }

    private static void RestoreTerritory(TerritorioClique territory,
        TerritorioClique.Dono owner, int troops)
    {
        if (territory == null)
            return;
        territory.GetComponent<TerritorioVisual>()?.ResetTransientFeedback();
        territory.GetComponentInChildren<ContadorTropas>(true)?
            .ResetTransientFeedback();
        territory.RemoverDestaqueSelecao();
        territory.DefinirDono(owner);
        territory.DefinirTropas(troops);
        territory.AplicarEstadoVisualResolucao(troops, owner);
    }

    private bool ValidateReferences() => origin != null && enemy != null &&
        ally != null && handoff != null && mapCamera != null &&
        mapOverlay != null && theme != null && scenarioDropdown != null &&
        sequence != null;

    private void OnSequenceCompleted()
    {
        // 04.1. CONSOLIDAÇÃO LOCAL — a sequência restaura o snapshot lógico
        // oficial ao terminar. Como o laboratório não executa resolvedores,
        // consolida depois disso somente o evento sintético que acabou de exibir.
        ApplySyntheticOutcome(activeResolutionEvent);
        activeResolutionEvent = null;
        if (activeScenario == Scenario.Reinforcement &&
            reinforcementMode == ReinforcementMode.IndividualTouch &&
            individualReinforcementSessionActive &&
            pendingIndividualReinforcements > 0)
        {
            pendingIndividualReinforcements--;
            PlaySyntheticReinforcement(ReinforcementMode.IndividualTouch);
            return;
        }
        SetStatus("Cenário concluído.");
    }

    private static void ApplySyntheticOutcome(ResolutionVisualEvent value)
    {
        if (value == null)
            return;
        TerritorioClique originTerritory = null;
        TerritorioClique destination = null;
        if (!string.IsNullOrEmpty(value.OriginTerritoryId) &&
            ResolutionVisualMapUtility.TryGetTerritory(
                value.OriginTerritoryId, out TerritorioClique resolvedOrigin))
            originTerritory = resolvedOrigin;
        if (!string.IsNullOrEmpty(value.DestinationTerritoryId) &&
            ResolutionVisualMapUtility.TryGetTerritory(
                value.DestinationTerritoryId, out TerritorioClique resolvedDestination))
            destination = resolvedDestination;
        ApplySyntheticOutcomeToTerritories(value, originTerritory, destination);
    }

    public static void ApplySyntheticOutcomeToTerritories(
        ResolutionVisualEvent value, TerritorioClique originTerritory,
        TerritorioClique destination)
    {
        if (value == null)
            return;
        if (originTerritory != null)
            RestoreTerritory(originTerritory, value.Player, value.OriginAfter);
        if (destination != null)
            RestoreTerritory(destination, value.NewOwner,
                value.DestinationAfter);
    }

    private void UpdateSpeedLabel()
    {
        if (speedLabel != null)
            speedLabel.text = sequence != null && sequence.IsAccelerated
                ? "VELOCIDADE 2×" : "VELOCIDADE 1×";
    }

    private void SetStatus(string value)
    {
        if (statusLabel != null)
            statusLabel.text = value;
    }

    private WDPreparedIndicatorStyle BuildIndicatorStyle() => new(
        indicatorOpacity, chevronArmLength, chevronThickness, dotRadius,
        glowIntensity, contrast, animationSpeedMultiplier);

    private WDConfirmedAttackStyle BuildConfirmedAttackStyle() => new(
        confirmedArrowTint, confirmedArrowOpacity, confirmedBodyThickness,
        confirmedOutlineThickness, confirmedOutlineColor, confirmedOuterGlow,
        confirmedReflection, confirmedHeadLength, confirmedHeadWidth,
        confirmedCardBackground, confirmedCardBorder,
        confirmedCardBorderThickness, confirmedCardTopReflection,
        confirmedCardCornerRadius, confirmedCardWidth, confirmedCardHeight,
        confirmedNumberColor, confirmedNumberSize, confirmedAdaptiveOutline,
        confirmedShadowThickness, confirmedShadowIntensity,
        confirmedSpecularIntensity, confirmedReliefIntensity,
        confirmedSurfaceOpacity);

    // ============================================================
    // 06. CENÁRIOS DE RESOLUÇÃO — overrides exclusivos e imediatos do lab
    // ============================================================
    private void ApplyScenarioLabStyles()
    {
        attackPresenter?.ConfigureLabStyles(
            BuildAttackStyle(true), BuildAttackStyle(false),
            new WDAttackResolutionLabStyle(true, movementTravelDuration,
                movementCurveFactor, movementCurveHeight, movementCounterScale,
                .65f, 42f, 34f, .76f, movementSettleDuration));
        attackPresenter?.ConfigureResolutionOverlayLabStyle(
            new WDResolutionOverlayLabStyle(true, BuildConfirmedAttackStyle(),
                resolutionArrowDuration, resolutionArrowSpeed,
                resolutionArrowSyncOffset, conquestRevealDuration,
                conquestRevealIntensity));
        attackPresenter?.ConfigureLabGlassStyles(
            BuildGlassStyle(casualtyFinish), BuildGlassStyle(counterFinish));
        reinforcementPresenter?.ConfigureLabStyle(
            new WDReinforcementResolutionLabStyle(true, reinforcementEntry,
                reinforcementReaction, reinforcementExit,
                reinforcementInitialScale, reinforcementPulseSize,
                reinforcementPulseOpacity, reinforcementTextOffset,
                reinforcementTextSize, reinforcementTextColor,
                reinforcementTextOpacity, reinforcementOutlineWidth,
                reinforcementOutlineOpacity, reinforcementRiseDistance));
        reinforcementPresenter?.ConfigureLabGlassStyle(
            BuildGlassStyle(reinforcementFinish));
        transferPresenter?.ConfigureLabStyle(new WDTransferResolutionLabStyle(
            true, handoffTransitionDuration, handoffReactionDuration,
            new WDTransferOwnershipBadgeStyle(true, handoffBadgeSize,
                handoffBadgeOffset, handoffArrowThickness,
                handoffArrowOpacity, handoffSurfaceOpacity)));
        transferPresenter?.ConfigureLabGlassStyle(
            BuildGlassStyle(transferFinish));
    }

    // 06.1. SELETORES GLASS — o global pode ser substituído por componente.
    private WDGameplayGlassStyle BuildGlassStyle(
        WDGameplayVisualOverride component)
    {
        bool enabled = component == WDGameplayVisualOverride.GlassPremium ||
            component == WDGameplayVisualOverride.UseGlobal &&
            visualFinish == WDGameplayVisualFinish.GlassPremium;
        return new WDGameplayGlassStyle(enabled, glassIntensity,
            glassGradientStrength, glassReflectionIntensity,
            glassBevelStrength, glassGlowIntensity, glassVariant);
    }

    private void ApplyCounterGlassStyles()
    {
        WDGameplayGlassStyle style = BuildGlassStyle(counterFinish);
        foreach (TerritorioClique territory in new[] { origin, enemy, ally, handoff })
        {
            if (territory == null)
                continue;
            Color identity = PaletaJogadores.ObterCorAtiva(territory.dono);
            territory.GetComponentInChildren<WDArmyCounterView>(true)?
                .ConfigurarGlassLaboratorio(identity, style);
        }
    }

    private void ApplyTerritoryLabFeedback()
    {
        foreach (TerritorioClique territory in new[] { origin, enemy, ally, handoff })
            territory?.GetComponent<TerritorioVisual>()?.ConfigureLabFeedback(
                selectionGlowIntensity, selectionGlowDuration,
                reinforcementGlowIntensity, reinforcementGlowDuration);
    }

    private WDAttackResolutionLabStyle BuildAttackStyle(bool conquered) =>
        conquered
            ? new WDAttackResolutionLabStyle(true, conqueredTravelDuration,
                conqueredCurveFactor, conqueredCurveHeight,
                conqueredCounterScale, conqueredImpactDuration,
                conqueredImpactSize, conqueredImpactSpread,
                conqueredImpactOpacity, conqueredResultDuration,
                conqueredTravelEase, conqueredContactHoldDuration,
                conqueredContactPressureIntensity,
                conqueredContactSeparation)
            : new WDAttackResolutionLabStyle(true, defendedTravelDuration,
                defendedCurveFactor, defendedCurveHeight,
                defendedCounterScale, defendedImpactDuration,
                defendedImpactSize, defendedImpactSpread,
                defendedImpactOpacity, defendedResultDuration,
                defendedTravelEase, defendedContactHoldDuration,
                defendedContactPressureIntensity,
                defendedContactSeparation);

    private static bool SameStyle(WDPreparedIndicatorStyle a,
        WDPreparedIndicatorStyle b) =>
        Mathf.Approximately(a.IndicatorOpacity, b.IndicatorOpacity) &&
        Mathf.Approximately(a.ChevronArmLength, b.ChevronArmLength) &&
        Mathf.Approximately(a.ChevronThickness, b.ChevronThickness) &&
        Mathf.Approximately(a.DotRadius, b.DotRadius) &&
        Mathf.Approximately(a.GlowIntensity, b.GlowIntensity) &&
        Mathf.Approximately(a.Contrast, b.Contrast) &&
        Mathf.Approximately(a.AnimationSpeedMultiplier,
            b.AnimationSpeedMultiplier);

    private static bool SameStyle(WDConfirmedAttackStyle a,
        WDConfirmedAttackStyle b) =>
        a.ArrowTint == b.ArrowTint &&
        Mathf.Approximately(a.ArrowOpacity, b.ArrowOpacity) &&
        Mathf.Approximately(a.BodyThickness, b.BodyThickness) &&
        Mathf.Approximately(a.OutlineThickness, b.OutlineThickness) &&
        a.OutlineColor == b.OutlineColor &&
        Mathf.Approximately(a.OuterGlow, b.OuterGlow) &&
        Mathf.Approximately(a.Reflection, b.Reflection) &&
        Mathf.Approximately(a.HeadLength, b.HeadLength) &&
        Mathf.Approximately(a.HeadWidth, b.HeadWidth) &&
        a.CardBackground == b.CardBackground && a.CardBorder == b.CardBorder &&
        Mathf.Approximately(a.CardBorderThickness, b.CardBorderThickness) &&
        Mathf.Approximately(a.CardTopReflection, b.CardTopReflection) &&
        Mathf.Approximately(a.CardCornerRadius, b.CardCornerRadius) &&
        Mathf.Approximately(a.CardWidth, b.CardWidth) &&
        Mathf.Approximately(a.CardHeight, b.CardHeight) &&
        a.NumberColor == b.NumberColor &&
        Mathf.Approximately(a.NumberSize, b.NumberSize) &&
        a.AdaptiveOutline == b.AdaptiveOutline &&
        Mathf.Approximately(a.ShadowThickness, b.ShadowThickness) &&
        Mathf.Approximately(a.ShadowIntensity, b.ShadowIntensity) &&
        Mathf.Approximately(a.SpecularIntensity, b.SpecularIntensity) &&
        Mathf.Approximately(a.ReliefIntensity, b.ReliefIntensity) &&
        Mathf.Approximately(a.SurfaceOpacity, b.SurfaceOpacity);

    private static T GetOrAdd<T>(GameObject target) where T : Component =>
        target.GetComponent<T>() ?? target.AddComponent<T>();
}
