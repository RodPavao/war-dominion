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
    [SerializeField, Range(-120f, 120f)] private float confirmedCurveHeight = 19f;
    [SerializeField, Range(-40f, 40f)] private float confirmedOriginExtension = 15.2f;
    [SerializeField, Range(-40f, 40f)] private float confirmedDestinationExtension = 15.5f;
    [SerializeField, Range(0f, 1f)] private float confirmedCurvePosition = .399f;

    private ResolutionSequenceController sequence;
    private readonly List<PreparationBinding> preparationViews = new();
    private bool initialized;
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
                }
        }
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

        AttackResolutionPresenter attack = GetOrAdd<AttackResolutionPresenter>(mapOverlay.gameObject);
        ReinforcementResolutionPresenter reinforcement =
            GetOrAdd<ReinforcementResolutionPresenter>(mapOverlay.gameObject);
        TransferResolutionPresenter transfer = GetOrAdd<TransferResolutionPresenter>(mapOverlay.gameObject);
        attack.Configure(theme);
        reinforcement.Configure(theme);
        transfer.Configure(theme);

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

        StopAndRestore();
        Scenario scenario = (Scenario)Mathf.Clamp(
            scenarioDropdown != null ? scenarioDropdown.value : 0,
            0, ScenarioLabels.Count - 1);
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
                sequence.Play(new[] { ResolutionVisualEvent.Attack(
                    TerritorioClique.Dono.Jogador1, origin.idTerritorio,
                    enemy.idTerritorio, 7, 12, 5, 4, 3, true,
                    TerritorioClique.Dono.Jogador2,
                    TerritorioClique.Dono.Jogador1) });
                break;
            case Scenario.DefendedAttack:
                sequence.Play(new[] { ResolutionVisualEvent.Attack(
                    TerritorioClique.Dono.Jogador1, origin.idTerritorio,
                    enemy.idTerritorio, 5, 12, 7, 9, 5, false,
                    TerritorioClique.Dono.Jogador2,
                    TerritorioClique.Dono.Jogador2) });
                break;
            case Scenario.AlliedMovement:
                sequence.Play(new[] { ResolutionVisualEvent.Movement(
                    TerritorioClique.Dono.Jogador1, origin.idTerritorio,
                    ally.idTerritorio, 4, 12, 8, 6, 10,
                    TerritorioClique.Dono.Jogador1,
                    TerritorioClique.Dono.Jogador1) });
                break;
            case Scenario.Reinforcement:
                sequence.Play(new[] { ResolutionVisualEvent.Reinforcement(
                    TerritorioClique.Dono.Jogador1, origin.idTerritorio,
                    5, 12, 17) });
                break;
            case Scenario.OwnershipTransfer:
                sequence.Play(new[] { ResolutionVisualEvent.TerritoryHandoff(
                    TerritorioClique.Dono.Jogador1,
                    TerritorioClique.Dono.Jogador3,
                    handoff.idTerritorio, 8, 8) });
                break;
        }
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
        ClearPreparationViews();
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
        territory.RemoverDestaqueSelecao();
        territory.DefinirDono(owner);
        territory.DefinirTropas(troops);
        territory.AplicarEstadoVisualResolucao(troops, owner);
    }

    private bool ValidateReferences() => origin != null && enemy != null &&
        ally != null && handoff != null && mapCamera != null &&
        mapOverlay != null && theme != null && scenarioDropdown != null &&
        sequence != null;

    private void OnSequenceCompleted() => SetStatus("Cenário concluído.");

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
