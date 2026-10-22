using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum WDPreparedActionVisualMode
{
    PreviewChevrons,
    PreviewDots,
    ConfirmedAttack,
    ConfirmedMovement
}

// ============================================================
// 00. ESTILO OPCIONAL — defaults reproduzem exatamente o visual oficial
// ============================================================
public readonly struct WDPreparedIndicatorStyle
{
    public const float DefaultIndicatorOpacity = 1f;
    public const float DefaultChevronArmLength = 9f;
    public const float DefaultChevronThickness = 4f;
    public const float DefaultDotRadius = 3.4f;
    public const float DefaultGlowIntensity = 1f;
    public const float DefaultContrast = 1f;
    public const float DefaultAnimationSpeedMultiplier = 1f;

    // 00.1. Preset aprovado no laboratório. Permanece opt-in para que a
    // partida oficial só o adote em uma etapa futura explicitamente autorizada.
    public const float LabApprovedIndicatorOpacity = 1f;
    public const float LabApprovedChevronArmLength = 15.73f;
    public const float LabApprovedChevronThickness = 4.86f;
    public const float LabApprovedDotRadius = 5.02f;
    public const float LabApprovedGlowIntensity = 2f;
    public const float LabApprovedContrast = 2f;
    public const float LabApprovedAnimationSpeedMultiplier = 1.39f;

    public static WDPreparedIndicatorStyle Default => new(
        DefaultIndicatorOpacity, DefaultChevronArmLength,
        DefaultChevronThickness, DefaultDotRadius, DefaultGlowIntensity,
        DefaultContrast, DefaultAnimationSpeedMultiplier);

    public static WDPreparedIndicatorStyle LaboratoryApproved => new(
        LabApprovedIndicatorOpacity, LabApprovedChevronArmLength,
        LabApprovedChevronThickness, LabApprovedDotRadius,
        LabApprovedGlowIntensity, LabApprovedContrast,
        LabApprovedAnimationSpeedMultiplier);

    public float IndicatorOpacity { get; }
    public float ChevronArmLength { get; }
    public float ChevronThickness { get; }
    public float DotRadius { get; }
    public float GlowIntensity { get; }
    public float Contrast { get; }
    public float AnimationSpeedMultiplier { get; }

    public WDPreparedIndicatorStyle(float indicatorOpacity,
        float chevronArmLength, float chevronThickness, float dotRadius,
        float glowIntensity, float contrast, float animationSpeedMultiplier)
    {
        IndicatorOpacity = Mathf.Clamp01(indicatorOpacity);
        ChevronArmLength = Mathf.Max(1f, chevronArmLength);
        ChevronThickness = Mathf.Max(.5f, chevronThickness);
        DotRadius = Mathf.Max(.5f, dotRadius);
        GlowIntensity = Mathf.Max(0f, glowIntensity);
        Contrast = Mathf.Max(0f, contrast);
        AnimationSpeedMultiplier = Mathf.Max(0f, animationSpeedMultiplier);
    }
}

// ============================================================
// 00.2. ATAQUE CONFIRMADO OPCIONAL — defaults preservam o visual histórico
// ============================================================
public readonly struct WDConfirmedAttackStyle
{
    public static WDConfirmedAttackStyle Default => new(
        Color.white, .96f, 5f, 0f, Color.clear, 0f, 0f,
        18f, 16.3f, new Color(.025f, .045f, .065f, .96f),
        Color.white, 1.5f, 0f, 10f, 62f, 38f, Color.white, 19f,
        true, 0f, 0f, 0f, 0f, 1f, true);

    public Color ArrowTint { get; }
    public float ArrowOpacity { get; }
    public float BodyThickness { get; }
    public float OutlineThickness { get; }
    public Color OutlineColor { get; }
    public float OuterGlow { get; }
    public float Reflection { get; }
    public float HeadLength { get; }
    public float HeadWidth { get; }
    public Color CardBackground { get; }
    public Color CardBorder { get; }
    public float CardBorderThickness { get; }
    public float CardTopReflection { get; }
    public float CardCornerRadius { get; }
    public float CardWidth { get; }
    public float CardHeight { get; }
    public Color NumberColor { get; }
    public float NumberSize { get; }
    public bool AdaptiveOutline { get; }
    public float ShadowThickness { get; }
    public float ShadowIntensity { get; }
    public float SpecularIntensity { get; }
    public float ReliefIntensity { get; }
    public float SurfaceOpacity { get; }
    public bool PreserveLegacy { get; }

    public WDConfirmedAttackStyle(Color arrowTint, float arrowOpacity,
        float bodyThickness, float outlineThickness, Color outlineColor,
        float outerGlow, float reflection, float headLength, float headWidth,
        Color cardBackground, Color cardBorder, float cardBorderThickness,
        float cardTopReflection, float cardCornerRadius, float cardWidth,
        float cardHeight, Color numberColor, float numberSize,
        bool adaptiveOutline, float shadowThickness, float shadowIntensity,
        float specularIntensity, float reliefIntensity, float surfaceOpacity,
        bool preserveLegacy = false)
    {
        ArrowTint = arrowTint;
        ArrowOpacity = Mathf.Clamp01(arrowOpacity);
        BodyThickness = Mathf.Max(.5f, bodyThickness);
        OutlineThickness = Mathf.Max(0f, outlineThickness);
        OutlineColor = outlineColor;
        OuterGlow = Mathf.Max(0f, outerGlow);
        Reflection = Mathf.Max(0f, reflection);
        HeadLength = Mathf.Max(1f, headLength);
        HeadWidth = Mathf.Max(1f, headWidth);
        CardBackground = cardBackground;
        CardBorder = cardBorder;
        CardBorderThickness = Mathf.Max(0f, cardBorderThickness);
        CardTopReflection = Mathf.Clamp01(cardTopReflection);
        CardCornerRadius = Mathf.Max(0f, cardCornerRadius);
        CardWidth = Mathf.Max(16f, cardWidth);
        CardHeight = Mathf.Max(16f, cardHeight);
        NumberColor = numberColor;
        NumberSize = Mathf.Max(1f, numberSize);
        AdaptiveOutline = adaptiveOutline;
        ShadowThickness = Mathf.Max(0f, shadowThickness);
        ShadowIntensity = Mathf.Clamp01(shadowIntensity);
        SpecularIntensity = Mathf.Max(0f, specularIntensity);
        ReliefIntensity = Mathf.Max(0f, reliefIntensity);
        SurfaceOpacity = Mathf.Clamp01(surfaceOpacity);
        PreserveLegacy = preserveLegacy;
    }
}

[DisallowMultipleComponent]
public sealed class PreparedActionArrowView : MonoBehaviour
{
    private WDPreparedRouteGraphic route;
    private RectTransform amountCard;
    private TextMeshProUGUI amountLabel;
    private Image amountReflection;
    private WarDominionUITheme theme;
    private WDPreparedActionVisualMode mode;
    private int amount;
    private WDPreparedIndicatorStyle indicatorStyle =
        WDPreparedIndicatorStyle.Default;
    private float animationSpeedMultiplier =
        WDPreparedIndicatorStyle.DefaultAnimationSpeedMultiplier;
    private WDConfirmedAttackStyle confirmedAttackStyle =
        WDConfirmedAttackStyle.Default;
    private Color actionColor = Color.white;

    // ============================================================
    // 01. SUPERFÍCIE GRÁFICA INDEPENDENTE E DETERMINÍSTICA
    // ============================================================
    public void Build(WarDominionUITheme value)
    {
        theme = value;
        RectTransform root = (RectTransform)transform;
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;

        // 01.1. A malha não compartilha o RectTransform do controlador. Isso
        // garante CanvasRenderer, material UI e limites válidos em cada rebuild.
        var routeObject = new GameObject(
            "RouteGraphic", typeof(RectTransform), typeof(CanvasRenderer));
        RectTransform routeRect = routeObject.GetComponent<RectTransform>();
        routeRect.SetParent(root, false);
        routeRect.anchorMin = Vector2.zero;
        routeRect.anchorMax = Vector2.one;
        routeRect.offsetMin = routeRect.offsetMax = Vector2.zero;
        route = routeObject.AddComponent<WDPreparedRouteGraphic>();
        route.raycastTarget = false;
        route.maskable = false;
        route.material = Graphic.defaultGraphicMaterial;
        route.color = Color.white;
        route.ConfigureIndicatorStyle(indicatorStyle);

        amountCard = CreatePanel("TroopAmount", root, new Vector2(62f, 38f));
        amountLabel = CreateText("Value", amountCard, theme.FonteDisplay, 19f);
        amountReflection = CreateReflection(amountCard);
        SetMode(WDPreparedActionVisualMode.ConfirmedAttack);
    }

    public void SetMode(WDPreparedActionVisualMode value)
    {
        mode = value;
        route.Mode = value;
        amountCard.gameObject.SetActive(
            value == WDPreparedActionVisualMode.ConfirmedAttack && amount > 0);
    }

    public void SetPlanning(bool value) => SetMode(value
        ? WDPreparedActionVisualMode.PreviewChevrons
        : WDPreparedActionVisualMode.ConfirmedAttack);

    // 01.2. Configuração opcional: a partida oficial não chama este método e
    // continua usando rigorosamente os valores históricos declarados acima.
    public void ConfigureIndicatorStyle(WDPreparedIndicatorStyle style)
    {
        indicatorStyle = style;
        animationSpeedMultiplier = style.AnimationSpeedMultiplier;
        route?.ConfigureIndicatorStyle(style);
    }

    // 01.3. Configuração opt-in usada pelo laboratório. A view oficial mantém
    // WDConfirmedAttackStyle.Default até que uma etapa futura a autorize.
    public void ConfigureConfirmedAttackStyle(WDConfirmedAttackStyle style)
    {
        confirmedAttackStyle = style;
        route?.ConfigureConfirmedAttackStyle(style);
        ApplyConfirmedCardStyle();
    }

    public void SetColorAndAmount(Color color, int value)
    {
        actionColor = color;
        amount = Mathf.Max(0, value);
        route.ActionColor = color;
        amountLabel.text = amount.ToString();
        ApplyConfirmedCardStyle();
        amountCard.gameObject.SetActive(
            mode == WDPreparedActionVisualMode.ConfirmedAttack && amount > 0);
    }


    private void ApplyConfirmedCardStyle()
    {
        if (amountCard == null || amountLabel == null)
            return;
        amountCard.sizeDelta = new Vector2(confirmedAttackStyle.CardWidth,
            confirmedAttackStyle.CardHeight);
        Image surface = amountCard.GetComponent<Image>();
        if (confirmedAttackStyle.PreserveLegacy)
        {
            surface.color = new Color(.025f, .045f, .065f, .96f);
            WDHomeUIFactory.ApplyRounded(surface, 10);
            Outline legacyBorder = amountCard.GetComponent<Outline>() ??
                amountCard.gameObject.AddComponent<Outline>();
            legacyBorder.effectColor = new Color(
                actionColor.r, actionColor.g, actionColor.b, .95f);
            legacyBorder.effectDistance = new Vector2(1.5f, -1.5f);
            amountLabel.color = Color.white;
            amountLabel.fontSize = 19f;
            if (amountReflection != null)
                amountReflection.gameObject.SetActive(false);
            return;
        }
        Color background = confirmedAttackStyle.CardBackground;
        background.a *= confirmedAttackStyle.SurfaceOpacity;
        surface.color = background;
        WDHomeUIFactory.ApplyRounded(surface,
            Mathf.RoundToInt(confirmedAttackStyle.CardCornerRadius));

        Outline[] outlines = amountCard.GetComponents<Outline>();
        while (outlines.Length < 2)
        {
            amountCard.gameObject.AddComponent<Outline>();
            outlines = amountCard.GetComponents<Outline>();
        }
        Color borderColor = confirmedAttackStyle.AdaptiveOutline
            ? Color.Lerp(Color.black, actionColor, .7f)
            : confirmedAttackStyle.CardBorder;
        borderColor.a = confirmedAttackStyle.CardBorder.a;
        outlines[0].effectColor = borderColor;
        outlines[0].effectDistance = new Vector2(
            confirmedAttackStyle.CardBorderThickness,
            -confirmedAttackStyle.CardBorderThickness);
        outlines[1].effectColor = new Color(0f, 0f, 0f,
            confirmedAttackStyle.ShadowIntensity);
        outlines[1].effectDistance = new Vector2(
            confirmedAttackStyle.ShadowThickness,
            -confirmedAttackStyle.ShadowThickness);

        amountLabel.color = confirmedAttackStyle.NumberColor;
        amountLabel.fontSize = confirmedAttackStyle.NumberSize;
        if (amountReflection != null)
        {
            amountReflection.color = new Color(1f, 1f, 1f,
                confirmedAttackStyle.CardTopReflection);
            amountReflection.gameObject.SetActive(
                confirmedAttackStyle.CardTopReflection > .001f);
        }
    }

    public void SetGeometry(Vector2 origin, Vector2 destination)
    {
        route.ConfigureGeometry(origin, destination,
            theme.PreparedArrowEndpointInset,
            theme.PreparedArrowCurveFactor,
            theme.PreparedArrowMaxCurveHeight);
        amountCard.anchoredPosition = route.Midpoint;
        gameObject.SetActive(Vector2.Distance(origin, destination) >= 4f);
    }

    public void SetGeometry(Vector2 origin, Vector2 destination,
        float originInset, float destinationInset)
    {
        route.ConfigureGeometry(origin, destination, originInset,
            destinationInset, theme.PreparedArrowCurveFactor,
            theme.PreparedArrowMaxCurveHeight);
        amountCard.anchoredPosition = route.Midpoint;
        gameObject.SetActive(Vector2.Distance(origin, destination) >= 4f);
    }

    public void SetGeometry(Vector2 origin, Vector2 destination,
        float originInset, float destinationInset, float curveHeight,
        float curvePosition)
    {
        route.ConfigureGeometryExplicitCurve(origin, destination, originInset,
            destinationInset, curveHeight, curvePosition);
        amountCard.anchoredPosition = route.Midpoint;
        gameObject.SetActive(Vector2.Distance(origin, destination) >= 4f);
    }

    private void Update()
    {
        if (theme == null || route == null)
            return;
        route.Phase = Mathf.Repeat(route.Phase + Time.unscaledDeltaTime *
            (theme.ReducedGameplayMotion ? .32f : theme.PlanningBeamSpeed) *
            animationSpeedMultiplier, 1f);
    }

    private static RectTransform CreatePanel(
        string name, Transform parent, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;
        WDHomeUIFactory.ApplyRounded(image, 10);
        return rect;
    }

    private static TextMeshProUGUI CreateText(
        string name, Transform parent, TMP_FontAsset font, float size)
    {
        var go = new GameObject(name, typeof(RectTransform),
            typeof(TextMeshProUGUI));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static Image CreateReflection(Transform parent)
    {
        var go = new GameObject("TopReflection", typeof(RectTransform),
            typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(.12f, .58f);
        rect.anchorMax = new Vector2(.88f, .84f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;
        WDHomeUIFactory.ApplyRounded(image, 4);
        rect.SetAsFirstSibling();
        return image;
    }
}

// ============================================================
// 02. MESH CONTÍNUO: CHEVRONS, PONTOS E SETA CONFIRMADA
// ============================================================
public sealed class WDPreparedRouteGraphic : MaskableGraphic
{
    private Vector2 start;
    private Vector2 control;
    private Vector2 end;
    private WDPreparedActionVisualMode mode;
    private Color actionColor = Color.white;
    private float phase;
    private WDPreparedIndicatorStyle indicatorStyle =
        WDPreparedIndicatorStyle.Default;
    private WDConfirmedAttackStyle confirmedAttackStyle =
        WDConfirmedAttackStyle.Default;

    public Vector2 Midpoint => Bezier(start, control, end, .5f);
    public Vector2 VisualStart => start;
    public Vector2 VisualEnd => end;
    public Vector2 ControlPoint => control;
    public Vector2 ConfirmedBodyEnd => confirmedAttackStyle.PreserveLegacy
        ? end : ArrowBodyEnd();
    public WDPreparedActionVisualMode Mode
    {
        get => mode;
        set { mode = value; SetAllDirty(); }
    }
    public Color ActionColor
    {
        get => actionColor;
        set { actionColor = value; SetAllDirty(); }
    }
    public float Phase
    {
        get => phase;
        set { phase = value; SetVerticesDirty(); }
    }
    public WDPreparedIndicatorStyle IndicatorStyle => indicatorStyle;
    public WDConfirmedAttackStyle ConfirmedAttackStyle => confirmedAttackStyle;

    public void ConfigureIndicatorStyle(WDPreparedIndicatorStyle style)
    {
        indicatorStyle = style;
        SetVerticesDirty();
    }

    public void ConfigureConfirmedAttackStyle(WDConfirmedAttackStyle style)
    {
        confirmedAttackStyle = style;
        SetVerticesDirty();
    }

    public void ConfigureGeometry(Vector2 origin, Vector2 destination,
        float inset, float curveFactor, float maxCurve)
    {
        ConfigureGeometry(origin, destination, inset, inset, curveFactor,
            maxCurve);
    }

    // 02.2. EXTREMOS VISUAIS — permite ao laboratório descontar os limites
    // reais dos contadores sem alterar as âncoras lógicas nem a API oficial.
    public void ConfigureGeometry(Vector2 origin, Vector2 destination,
        float originInset, float destinationInset, float curveFactor,
        float maxCurve)
    {
        Vector2 delta = destination - origin;
        float length = delta.magnitude;
        if (length < 1f)
            return;
        Vector2 direction = delta / length;
        float safeOriginInset = Mathf.Min(Mathf.Max(0f, originInset), length * .4f);
        float safeDestinationInset = Mathf.Min(Mathf.Max(0f, destinationInset),
            length * .4f);
        start = origin + direction * safeOriginInset;
        end = destination - direction * safeDestinationInset;
        Vector2 normal = new(-direction.y, direction.x);
        control = (start + end) * .5f + normal *
            Mathf.Min(maxCurve, length * curveFactor);
        SetAllDirty();
    }

    public void ConfigureGeometryExplicitCurve(Vector2 origin, Vector2 destination,
        float originInset, float destinationInset, float curveHeight,
        float curvePosition)
    {
        Vector2 delta = destination - origin;
        float logicalLength = delta.magnitude;
        if (logicalLength < 1f)
            return;
        Vector2 direction = delta / logicalLength;
        float safeOriginInset = Mathf.Min(Mathf.Max(0f, originInset),
            logicalLength * .4f);
        float safeDestinationInset = Mathf.Min(Mathf.Max(0f, destinationInset),
            logicalLength * .4f);
        start = origin + direction * safeOriginInset;
        end = destination - direction * safeDestinationInset;
        float visualLength = Vector2.Distance(start, end);
        Vector2 normal = new(-direction.y, direction.x);
        float safeHeight = Mathf.Clamp(curveHeight,
            -visualLength * .4f, visualLength * .4f);
        control = Vector2.Lerp(start, end, Mathf.Clamp01(curvePosition)) +
            normal * safeHeight;
        SetAllDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh) =>
        PopulateGeometry(vh);

    // 02.1. Entrada pública somente de leitura para os testes de geometria.
    public void PopulateGeometry(VertexHelper vh)
    {
        vh.Clear();
        if (Vector2.Distance(start, end) < 2f)
            return;
        switch (mode)
        {
            case WDPreparedActionVisualMode.PreviewChevrons:
                DrawChevrons(vh);
                break;
            case WDPreparedActionVisualMode.PreviewDots:
                DrawDots(vh);
                break;
            case WDPreparedActionVisualMode.ConfirmedAttack:
                DrawArrow(vh);
                break;
            case WDPreparedActionVisualMode.ConfirmedMovement:
                DrawMovementBadge(vh);
                break;
        }
    }

    private void DrawChevrons(VertexHelper vh)
    {
        int count = Mathf.Clamp(Mathf.FloorToInt(ApproximateLength() / 42f), 3, 8);
        for (int i = 0; i < count; i++)
        {
            float t = (i + 1f) / (count + 1f);
            Vector2 point = Bezier(start, control, end, t);
            Vector2 back = -Tangent(start, control, end, t).normalized;
            Color c = Color.Lerp(actionColor, Color.white,
                Mathf.Clamp01(.12f * indicatorStyle.Contrast +
                    .48f * indicatorStyle.GlowIntensity * FlowGlow(t)));
            c.a = .94f * indicatorStyle.IndicatorOpacity;
            AddThickLine(vh, point + Rotate(back, 32f) *
                    indicatorStyle.ChevronArmLength,
                point, indicatorStyle.ChevronThickness, c);
            AddThickLine(vh, point,
                point + Rotate(back, -32f) * indicatorStyle.ChevronArmLength,
                indicatorStyle.ChevronThickness, c);
            AddDisc(vh, point, indicatorStyle.ChevronThickness * .5f, c, 8);
        }
    }

    private void DrawDots(VertexHelper vh)
    {
        int count = Mathf.Clamp(Mathf.FloorToInt(ApproximateLength() / 30f), 4, 11);
        for (int i = 0; i < count; i++)
        {
            float t = (i + 1f) / (count + 1f);
            Color c = Color.Lerp(actionColor, Color.white,
                Mathf.Clamp01(.1f * indicatorStyle.Contrast +
                    .45f * indicatorStyle.GlowIntensity * FlowGlow(t)));
            c.a = .92f * indicatorStyle.IndicatorOpacity;
            AddDisc(vh, Bezier(start, control, end, t),
                indicatorStyle.DotRadius, c, 10);
        }
    }

    private void DrawArrow(VertexHelper vh)
    {
        if (confirmedAttackStyle.PreserveLegacy)
        {
            Color legacyColor = actionColor;
            legacyColor.a = .96f;
            const int legacySegments = 28;
            for (int i = 0; i < legacySegments; i++)
                AddThickLine(vh,
                    Bezier(start, control, end, i / (float)legacySegments),
                    Bezier(start, control, end, (i + 1f) / legacySegments),
                    5f, legacyColor);
            Vector2 legacyForward = Tangent(start, control, end, 1f).normalized;
            float legacyHead = Mathf.Clamp(ApproximateLength() * .07f, 12f, 18f);
            AddTriangle(vh, end,
                end - Rotate(legacyForward, 27f) * legacyHead,
                end - Rotate(legacyForward, -27f) * legacyHead, legacyColor);
            return;
        }
        Color c = Multiply(actionColor, confirmedAttackStyle.ArrowTint);
        c.a = confirmedAttackStyle.ArrowOpacity *
            confirmedAttackStyle.SurfaceOpacity;
        float body = confirmedAttackStyle.BodyThickness;
        Color outline = confirmedAttackStyle.AdaptiveOutline
            ? Color.Lerp(Color.black, actionColor, .42f)
            : confirmedAttackStyle.OutlineColor;
        outline.a *= confirmedAttackStyle.ArrowOpacity;
        Vector2 forward = Tangent(start, control, end, 1f).normalized;
        float head = confirmedAttackStyle.HeadLength;
        float halfWidth = Mathf.Max(confirmedAttackStyle.HeadWidth * .5f,
            body * .5f);
        Vector2 normal = new(-forward.y, forward.x);
        float bodyEndT = FindTAtDistanceFromEnd(head);
        Vector2 bodyEnd = Bezier(start, control, end, bodyEndT);
        Vector2 baseTangent = Tangent(start, control, end, bodyEndT).normalized;
        Vector2 baseNormal = new(-baseTangent.y, baseTangent.x);

        // 02.3. CAMADAS CONTÍNUAS — uma faixa compartilhada por camada evita
        // sobreposição alfa nas junções e encerra o corpo na base da ponta.
        if (confirmedAttackStyle.ShadowThickness > 0f &&
            confirmedAttackStyle.ShadowIntensity > 0f)
        {
            Vector2 shadowOffset = new(0f,
                -Mathf.Max(1f, confirmedAttackStyle.ShadowThickness * .65f));
            Color shadow = new(0f, 0f, 0f,
                confirmedAttackStyle.ShadowIntensity);
            AddBezierStrip(vh, bodyEndT,
                body + confirmedAttackStyle.ShadowThickness * 2f,
                shadow, shadowOffset);
            AddArrowHead(vh, end + shadowOffset, bodyEnd + shadowOffset,
                baseNormal, halfWidth + confirmedAttackStyle.ShadowThickness,
                shadow);
        }
        if (confirmedAttackStyle.ReliefIntensity > 0f)
            AddBezierStrip(vh, bodyEndT,
                body + confirmedAttackStyle.ReliefIntensity,
                new Color(0f, 0f, 0f, Mathf.Clamp01(
                    confirmedAttackStyle.ReliefIntensity * .12f)),
                new Vector2(0f, -confirmedAttackStyle.ReliefIntensity));
        if (confirmedAttackStyle.OuterGlow > 0f)
        {
            Color glow = new(c.r, c.g, c.b,
                Mathf.Min(.28f, confirmedAttackStyle.OuterGlow * .11f));
            AddBezierStrip(vh, bodyEndT,
                body + confirmedAttackStyle.OuterGlow * 5f, glow, Vector2.zero);
            AddArrowHead(vh, end, bodyEnd, baseNormal,
                halfWidth + confirmedAttackStyle.OuterGlow * 2.5f, glow);
        }
        if (confirmedAttackStyle.OutlineThickness > 0f)
        {
            AddBezierStrip(vh, bodyEndT,
                body + confirmedAttackStyle.OutlineThickness * 2f,
                outline, Vector2.zero);
            AddArrowHead(vh, end, bodyEnd, baseNormal,
                halfWidth + confirmedAttackStyle.OutlineThickness, outline);
        }
        AddBezierStrip(vh, bodyEndT, body, c, Vector2.zero);
        AddArrowHead(vh, end, bodyEnd, baseNormal, halfWidth, c);

        float reflection = confirmedAttackStyle.Reflection +
            confirmedAttackStyle.SpecularIntensity;
        if (reflection > 0f)
            AddBezierStrip(vh, bodyEndT, Mathf.Max(.5f, body * .18f),
                new Color(1f, 1f, 1f, Mathf.Clamp01(reflection * .45f)),
                normal * body * .16f);
    }

    private Vector2 ArrowBodyEnd() => Bezier(start, control, end,
        FindTAtDistanceFromEnd(confirmedAttackStyle.HeadLength));

    private float FindTAtDistanceFromEnd(float distance)
    {
        const int samples = 64;
        float accumulated = 0f;
        Vector2 previous = end;
        for (int i = samples - 1; i >= 0; i--)
        {
            float t = i / (float)samples;
            Vector2 point = Bezier(start, control, end, t);
            accumulated += Vector2.Distance(previous, point);
            if (accumulated >= distance)
                return t;
            previous = point;
        }
        return 0f;
    }

    private void AddBezierStrip(VertexHelper vh, float endT, float width,
        Color color, Vector2 offset)
    {
        const int segments = 28;
        int first = vh.currentVertCount;
        for (int i = 0; i <= segments; i++)
        {
            float t = endT * i / segments;
            Vector2 point = Bezier(start, control, end, t) + offset;
            Vector2 tangent = Tangent(start, control, end, t).normalized;
            Vector2 side = new(-tangent.y, tangent.x);
            side *= width * .5f;
            vh.AddVert(point - side, color, Vector2.zero);
            vh.AddVert(point + side, color, Vector2.zero);
        }
        for (int i = 0; i < segments; i++)
        {
            int index = first + i * 2;
            vh.AddTriangle(index, index + 1, index + 3);
            vh.AddTriangle(index, index + 3, index + 2);
        }
    }

    private static void AddArrowHead(VertexHelper vh, Vector2 tip,
        Vector2 baseCenter, Vector2 baseNormal, float halfWidth, Color color)
    {
        AddTriangle(vh, tip, baseCenter + baseNormal * halfWidth,
            baseCenter - baseNormal * halfWidth, color);
    }

    private static Color Multiply(Color a, Color b) => new(
        a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);

    private void DrawMovementBadge(VertexHelper vh)
    {
        Vector2 center = Midpoint;
        AddDisc(vh, center, 25f, new Color(.025f, .06f, .08f, .94f), 24);
        Color c = actionColor;
        c.a = .95f;
        DrawArcArrow(vh, center, 18f, 25f, 155f, c);
        DrawArcArrow(vh, center, 18f, 205f, 335f, c);
    }

    private void DrawArcArrow(VertexHelper vh, Vector2 center, float radius,
        float from, float to, Color c)
    {
        const int steps = 10;
        Vector2 previous = center + Polar(radius, from);
        for (int i = 1; i <= steps; i++)
        {
            Vector2 next = center + Polar(radius,
                Mathf.Lerp(from, to, i / (float)steps));
            AddThickLine(vh, previous, next, 3f, c);
            previous = next;
        }
        Vector2 tangent = Polar(1f, to + 90f);
        AddTriangle(vh, previous,
            previous - Rotate(tangent, 28f) * 8f,
            previous - Rotate(tangent, -28f) * 8f, c);
    }

    private float FlowGlow(float t)
    {
        float distance = Mathf.Abs(Mathf.Repeat(t - phase + .5f, 1f) - .5f) * 2f;
        return Mathf.Pow(1f - Mathf.Clamp01(distance), 3f);
    }

    private float ApproximateLength()
    {
        float total = 0f;
        Vector2 previous = start;
        for (int i = 1; i <= 16; i++)
        {
            Vector2 next = Bezier(start, control, end, i / 16f);
            total += Vector2.Distance(previous, next);
            previous = next;
        }
        return total;
    }

    internal static void AddThickLine(VertexHelper vh,
        Vector2 a, Vector2 b, float width, Color c)
    {
        Vector2 d = b - a;
        if (d.sqrMagnitude < .01f)
            return;
        Vector2 n = new(-d.y, d.x);
        n.Normalize();
        n *= width * .5f;
        AddQuad(vh, a - n, a + n, b + n, b - n, c);
    }

    internal static void AddDisc(VertexHelper vh,
        Vector2 center, float radius, Color c, int sides)
    {
        int centerIndex = vh.currentVertCount;
        vh.AddVert(center, c, Vector2.zero);
        for (int i = 0; i <= sides; i++)
            vh.AddVert(center + Polar(radius, i * 360f / sides), c, Vector2.zero);
        for (int i = 0; i < sides; i++)
            vh.AddTriangle(centerIndex, centerIndex + i + 1,
                centerIndex + i + 2);
    }

    internal static void AddTriangle(VertexHelper vh,
        Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        int i = vh.currentVertCount;
        vh.AddVert(a, color, Vector2.zero);
        vh.AddVert(b, color, Vector2.zero);
        vh.AddVert(c, color, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2);
    }

    private static void AddQuad(VertexHelper vh,
        Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        int i = vh.currentVertCount;
        vh.AddVert(a, color, Vector2.zero);
        vh.AddVert(b, color, Vector2.zero);
        vh.AddVert(c, color, Vector2.zero);
        vh.AddVert(d, color, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i, i + 2, i + 3);
    }

    internal static Vector2 Polar(float radius, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
    }

    private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    private static Vector2 Tangent(Vector2 a, Vector2 b, Vector2 c, float t) =>
        2f * (1f - t) * (b - a) + 2f * t * (c - b);

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        return new Vector2(
            v.x * Mathf.Cos(radians) - v.y * Mathf.Sin(radians),
            v.x * Mathf.Sin(radians) + v.y * Mathf.Cos(radians));
    }
}

// ============================================================
// 03. ÍCONE PROCEDURAL DE TRANSFERÊNCIA SEM DEPENDÊNCIA DE FONTE
// ============================================================
public sealed class WDTransferCycleGraphic : MaskableGraphic
{
    private Color iconColor = Color.white;

    public void Configure(Color value)
    {
        iconColor = value;
        raycastTarget = false;
        maskable = false;
        material = Graphic.defaultGraphicMaterial;
        color = Color.white;
        SetAllDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        float radius = Mathf.Max(6f,
            Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .29f);
        DrawArc(vh, Vector2.zero, radius, 20f, 155f, iconColor);
        DrawArc(vh, Vector2.zero, radius, 200f, 335f, iconColor);
    }

    private static void DrawArc(VertexHelper vh, Vector2 center, float radius,
        float from, float to, Color color)
    {
        const int steps = 11;
        Vector2 previous = center + WDPreparedRouteGraphic.Polar(radius, from);
        for (int i = 1; i <= steps; i++)
        {
            Vector2 next = center + WDPreparedRouteGraphic.Polar(radius,
                Mathf.Lerp(from, to, i / (float)steps));
            WDPreparedRouteGraphic.AddThickLine(vh, previous, next, 3f, color);
            previous = next;
        }
        Vector2 tangent = WDPreparedRouteGraphic.Polar(1f, to + 90f);
        Vector2 left = Quaternion.Euler(0f, 0f, 28f) * -tangent;
        Vector2 right = Quaternion.Euler(0f, 0f, -28f) * -tangent;
        WDPreparedRouteGraphic.AddTriangle(vh, previous,
            previous + left * 7f, previous + right * 7f, color);
    }
}

// ============================================================
// 04. TRANSFERÊNCIA DE PROPRIEDADE: BOLHA ABAIXO DO CONTADOR
// ============================================================
public sealed class WDTransferOwnershipBadgeView : MonoBehaviour
{
    private RectTransform badge;
    private WDTransferCycleGraphic cycle;

    public void Build(WarDominionUITheme theme)
    {
        badge = (RectTransform)transform;
        badge.anchorMin = badge.anchorMax = badge.pivot = new Vector2(.5f, .5f);
        badge.sizeDelta = new Vector2(58f, 46f);
        Image image = gameObject.AddComponent<Image>();
        image.color = new Color(.025f, .065f, .085f, .96f);
        image.raycastTarget = false;
        WDHomeUIFactory.ApplyRounded(image, 22);

        var iconObject = new GameObject(
            "TransferCycle", typeof(RectTransform), typeof(CanvasRenderer));
        RectTransform icon = iconObject.GetComponent<RectTransform>();
        icon.SetParent(badge, false);
        icon.anchorMin = Vector2.zero;
        icon.anchorMax = Vector2.one;
        icon.offsetMin = icon.offsetMax = Vector2.zero;
        cycle = iconObject.AddComponent<WDTransferCycleGraphic>();
        cycle.Configure(theme.Acento);
    }

    public void SetPosition(Vector2 value) =>
        badge.anchoredPosition = value + new Vector2(0f, -52f);

    private void Update()
    {
        if (cycle != null)
            cycle.rectTransform.localEulerAngles = new Vector3(
                0f, 0f, Mathf.Sin(Time.unscaledTime * 2.4f) * 5f);
    }
}
