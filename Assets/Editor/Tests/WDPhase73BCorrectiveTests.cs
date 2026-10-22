using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class WDPhase73BCorrectiveTests
{
    // ============================================================
    // 01. CLASSIFICAÇÃO SEMÂNTICA E REGRA AUTORITATIVA
    // ============================================================
    [TestCase("ATAQUE", MatchUIActionKind.Attack)]
    [TestCase("OCUPAÇÃO", MatchUIActionKind.Occupation)]
    [TestCase("MOVIMENTO", MatchUIActionKind.OwnMovement)]
    [TestCase("MOVIMENTO ALIADO", MatchUIActionKind.AlliedMovement)]
    public void LegacyLabelsMapToSemanticKinds(string label, MatchUIActionKind expected)
    {
        Assert.That(MatchUIActionKindUtility.FromLegacyLabel(label), Is.EqualTo(expected));
    }

    [TestCase(1, 2)]
    [TestCase(10, 11)]
    [TestCase(11, 13)]
    [TestCase(20, 23)]
    public void MinimumAttackUsesAuthoritativeRule(int defenders, int expected)
    {
        Assert.That(ResolvedorCombate.CalcularAtaqueMinimoBase(defenders), Is.EqualTo(expected));
    }

    [Test]
    public void PreviewAndConfirmedModesRemainDistinct()
    {
        Assert.That(WDPreparedActionVisualMode.PreviewChevrons,
            Is.Not.EqualTo(WDPreparedActionVisualMode.PreviewDots));
        Assert.That(WDPreparedActionVisualMode.ConfirmedAttack,
            Is.Not.EqualTo(WDPreparedActionVisualMode.ConfirmedMovement));
    }

    // ============================================================
    // 02. GEOMETRIA PROCEDURAL REAL, SEM DEPENDER DO PLAY MODE
    // ============================================================
    [TestCase(WDPreparedActionVisualMode.PreviewChevrons)]
    [TestCase(WDPreparedActionVisualMode.PreviewDots)]
    [TestCase(WDPreparedActionVisualMode.ConfirmedAttack)]
    [TestCase(WDPreparedActionVisualMode.ConfirmedMovement)]
    public void PreparedRouteModesGenerateVisibleTriangles(
        WDPreparedActionVisualMode mode)
    {
        var root = new GameObject("RouteTest", typeof(RectTransform),
            typeof(CanvasRenderer));
        try
        {
            WDPreparedRouteGraphic graphic =
                root.AddComponent<WDPreparedRouteGraphic>();
            graphic.Mode = mode;
            graphic.ActionColor = Color.cyan;
            graphic.ConfigureGeometry(new Vector2(-180f, -40f),
                new Vector2(220f, 90f), 20f, .12f, 80f);
            using var vertices = new VertexHelper();
            graphic.PopulateGeometry(vertices);
            Assert.That(vertices.currentVertCount, Is.GreaterThan(0));
            Assert.That(vertices.currentIndexCount, Is.GreaterThan(0));
            Assert.That(vertices.currentIndexCount % 3, Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // ============================================================
    // 02.1. RETROCOMPATIBILIDADE — ausência de configuração preserva o oficial
    // ============================================================
    [Test]
    public void PreparedIndicatorDefaultsMatchPreviousOfficialValues()
    {
        var root = new GameObject("RouteDefaults", typeof(RectTransform),
            typeof(CanvasRenderer));
        try
        {
            WDPreparedRouteGraphic graphic =
                root.AddComponent<WDPreparedRouteGraphic>();
            WDPreparedIndicatorStyle style = graphic.IndicatorStyle;
            Assert.That(style.IndicatorOpacity, Is.EqualTo(1f));
            Assert.That(style.ChevronArmLength, Is.EqualTo(9f));
            Assert.That(style.ChevronThickness, Is.EqualTo(4f));
            Assert.That(style.DotRadius, Is.EqualTo(3.4f));
            Assert.That(style.GlowIntensity, Is.EqualTo(1f));
            Assert.That(style.Contrast, Is.EqualTo(1f));
            Assert.That(style.AnimationSpeedMultiplier, Is.EqualTo(1f));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void PreparedIndicatorAcceptsOptionalLabStyle()
    {
        var root = new GameObject("RouteStyle", typeof(RectTransform),
            typeof(CanvasRenderer));
        try
        {
            WDPreparedRouteGraphic graphic =
                root.AddComponent<WDPreparedRouteGraphic>();
            var expected = new WDPreparedIndicatorStyle(
                .8f, 12f, 5f, 4.5f, 1.25f, 1.1f, .75f);
            graphic.ConfigureIndicatorStyle(expected);
            Assert.That(graphic.IndicatorStyle.IndicatorOpacity,
                Is.EqualTo(.8f));
            Assert.That(graphic.IndicatorStyle.ChevronArmLength,
                Is.EqualTo(12f));
            Assert.That(graphic.IndicatorStyle.DotRadius, Is.EqualTo(4.5f));
            Assert.That(graphic.IndicatorStyle.AnimationSpeedMultiplier,
                Is.EqualTo(.75f));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void LaboratoryApprovedIndicatorPresetKeepsExactApprovedValues()
    {
        WDPreparedIndicatorStyle style =
            WDPreparedIndicatorStyle.LaboratoryApproved;
        Assert.That(style.IndicatorOpacity, Is.EqualTo(1f));
        Assert.That(style.ChevronArmLength, Is.EqualTo(15.73f));
        Assert.That(style.ChevronThickness, Is.EqualTo(4.86f));
        Assert.That(style.DotRadius, Is.EqualTo(5.02f));
        Assert.That(style.GlowIntensity, Is.EqualTo(2f));
        Assert.That(style.Contrast, Is.EqualTo(2f));
        Assert.That(style.AnimationSpeedMultiplier, Is.EqualTo(1.39f));
    }

    // ============================================================
    // 02.2. ATAQUE CONFIRMADO — opt-in isolado e fallback oficial histórico
    // ============================================================
    [Test]
    public void ConfirmedAttackDefaultsPreserveLegacyRenderingPath()
    {
        var root = new GameObject("ConfirmedDefaults", typeof(RectTransform),
            typeof(CanvasRenderer));
        try
        {
            WDPreparedRouteGraphic graphic =
                root.AddComponent<WDPreparedRouteGraphic>();
            Assert.That(graphic.ConfirmedAttackStyle.PreserveLegacy, Is.True);
            Assert.That(graphic.ConfirmedAttackStyle.BodyThickness,
                Is.EqualTo(5f));
            Assert.That(graphic.ConfirmedAttackStyle.ArrowOpacity,
                Is.EqualTo(.96f));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void ConfirmedAttackAcceptsOptionalLaboratoryStyle()
    {
        var root = new GameObject("ConfirmedCustom", typeof(RectTransform),
            typeof(CanvasRenderer));
        try
        {
            WDPreparedRouteGraphic graphic =
                root.AddComponent<WDPreparedRouteGraphic>();
            var expected = new WDConfirmedAttackStyle(Color.white, .8f, 9f,
                2f, Color.black, 1f, .5f, 24f, 18f, Color.black,
                Color.cyan, 2f, .4f, 12f, 74f, 42f, Color.white, 22f,
                true, 3f, .4f, .3f, 2f, .9f);
            graphic.ConfigureConfirmedAttackStyle(expected);
            Assert.That(graphic.ConfirmedAttackStyle.PreserveLegacy, Is.False);
            Assert.That(graphic.ConfirmedAttackStyle.BodyThickness,
                Is.EqualTo(9f));
            Assert.That(graphic.ConfirmedAttackStyle.HeadLength,
                Is.EqualTo(24f));
            Assert.That(graphic.ConfirmedAttackStyle.CardWidth,
                Is.EqualTo(74f));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void ConfirmedBodyStopsAtArrowHeadBase()
    {
        var root = new GameObject("ConnectedArrow", typeof(RectTransform),
            typeof(CanvasRenderer));
        try
        {
            WDPreparedRouteGraphic graphic =
                root.AddComponent<WDPreparedRouteGraphic>();
            graphic.ConfigureConfirmedAttackStyle(CustomConfirmedStyle());
            graphic.ConfigureGeometry(Vector2.zero, new Vector2(300f, 80f),
                20f, 24f, .12f, 60f);
            Assert.That(Vector2.Distance(graphic.ConfirmedBodyEnd,
                graphic.VisualEnd), Is.GreaterThan(20f));
            Assert.That(graphic.ConfirmedBodyEnd,
                Is.Not.EqualTo(graphic.VisualEnd));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void ConfirmedShadowAddsIndependentContinuousLayer()
    {
        var root = new GameObject("ShadowArrow", typeof(RectTransform),
            typeof(CanvasRenderer));
        try
        {
            WDPreparedRouteGraphic graphic =
                root.AddComponent<WDPreparedRouteGraphic>();
            graphic.Mode = WDPreparedActionVisualMode.ConfirmedAttack;
            graphic.ConfigureGeometry(Vector2.zero, new Vector2(260f, 90f),
                18f, 22f, .1f, 50f);
            graphic.ConfigureConfirmedAttackStyle(CustomConfirmedStyle(0f, 0f));
            using var withoutShadow = new VertexHelper();
            graphic.PopulateGeometry(withoutShadow);
            graphic.ConfigureConfirmedAttackStyle(CustomConfirmedStyle(5f, .65f));
            using var withShadow = new VertexHelper();
            graphic.PopulateGeometry(withShadow);
            Assert.That(withShadow.currentVertCount,
                Is.GreaterThan(withoutShadow.currentVertCount));
            Assert.That(withShadow.currentIndexCount,
                Is.GreaterThan(withoutShadow.currentIndexCount));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [TestCase(30f, 20f, 1f, 0f, 30f)]
    [TestCase(30f, 20f, 0f, 1f, 20f)]
    [TestCase(30f, 20f, .7071068f, .7071068f, 28.28427f)]
    public void CounterExtentRespectsHorizontalVerticalAndDiagonalBounds(
        float halfWidth, float halfHeight, float x, float y, float expected)
    {
        Assert.That(WDCombatContextLabController.RectRadialExtent(
            new Vector2(halfWidth, halfHeight), new Vector2(x, y)),
            Is.EqualTo(expected).Within(.001f));
    }

    [Test]
    public void InsetsTrimShortDiagonalWithoutChangingVisualMidpointContract()
    {
        var root = new GameObject("ShortArrow", typeof(RectTransform),
            typeof(CanvasRenderer));
        try
        {
            WDPreparedRouteGraphic graphic =
                root.AddComponent<WDPreparedRouteGraphic>();
            graphic.ConfigureConfirmedAttackStyle(CustomConfirmedStyle());
            graphic.ConfigureGeometry(new Vector2(10f, 10f),
                new Vector2(90f, 70f), 18f, 22f, .12f, 40f);
            Vector2 expectedStart = new Vector2(10f, 10f) +
                new Vector2(80f, 60f).normalized * 18f;
            Vector2 expectedEnd = new Vector2(90f, 70f) -
                new Vector2(80f, 60f).normalized * 22f;
            Assert.That(Vector2.Distance(graphic.VisualStart, expectedStart),
                Is.LessThan(.001f));
            Assert.That(Vector2.Distance(graphic.VisualEnd, expectedEnd),
                Is.LessThan(.001f));
            Vector2 midpoint = graphic.Midpoint;
            graphic.ConfigureConfirmedAttackStyle(CustomConfirmedStyle(8f, .8f));
            Assert.That(Vector2.Distance(graphic.Midpoint, midpoint),
                Is.LessThan(.001f));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void ZeroCurveHeightProducesStraightRouteAtAnyCurvePosition()
    {
        var root = new GameObject("StraightArrow", typeof(RectTransform),
            typeof(CanvasRenderer));
        try
        {
            WDPreparedRouteGraphic graphic =
                root.AddComponent<WDPreparedRouteGraphic>();
            graphic.ConfigureGeometryExplicitCurve(new Vector2(-80f, -30f),
                new Vector2(240f, 90f), 20f, 25f, 0f, .72f);
            Vector2 axis = (graphic.VisualEnd - graphic.VisualStart).normalized;
            Vector2 toControl = graphic.ControlPoint - graphic.VisualStart;
            float cross = axis.x * toControl.y - axis.y * toControl.x;
            Assert.That(Mathf.Abs(cross), Is.LessThan(.001f));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [TestCase(5f, 0f, 5f)]
    [TestCase(5f, 3f, 2f)]
    [TestCase(5f, 20f, 0f)]
    [TestCase(5f, -4f, 9f)]
    public void ConfirmedExtensionsNeverCrossVisualCounterBoundary(
        float margin, float extension, float expected)
    {
        Assert.That(WDCombatContextLabController.SafeVisualGap(
            margin, extension), Is.EqualTo(expected));
    }

    // 08.1. EXTREMOS VISÍVEIS — usa a silhueta opaca, não o retângulo com
    // padding transparente, e mantém origem/destino exatamente na borda.
    [Test]
    public void VisibleCounterSilhouetteRemovesTransparentPaddingGap()
    {
        Vector2[] visibleOutline = {
            new(12f, 35f), new(28f, 15f), new(72f, 15f), new(88f, 35f),
            new(88f, 65f), new(72f, 85f), new(28f, 85f), new(12f, 65f)
        };
        Assert.That(WDCombatContextLabController.TrySegmentBoundaryInset(
            new Vector2(50f, 50f), new Vector2(200f, 50f), visibleOutline,
            true, out float inset), Is.True);
        Assert.That(inset, Is.EqualTo(38f).Within(.001f));
    }

    [Test]
    public void OriginAndDestinationEndpointsMeetOffCenterDiagonalSilhouettes()
    {
        Vector2[] origin = { new(-18f, -12f), new(22f, -12f),
            new(22f, 18f), new(-18f, 18f) };
        Vector2[] destination = { new(82f, 68f), new(126f, 68f),
            new(126f, 104f), new(82f, 104f) };
        Vector2 start = Vector2.zero;
        Vector2 end = new(100f, 80f);
        Assert.That(WDCombatContextLabController.TrySegmentBoundaryInset(
            start, end, origin, true, out float originInset), Is.True);
        Assert.That(WDCombatContextLabController.TrySegmentBoundaryInset(
            start, end, destination, false, out float destinationInset), Is.True);

        Vector2 axis = (end - start).normalized;
        Vector2 visibleStart = start + axis * originInset;
        Vector2 visibleEnd = end - axis * destinationInset;
        Assert.That(visibleStart.x, Is.EqualTo(22f).Within(.001f));
        Assert.That(visibleEnd.x, Is.EqualTo(85f).Within(.001f));
        Assert.That(visibleStart.y, Is.EqualTo(17.6f).Within(.001f));
        Assert.That(visibleEnd.y, Is.EqualTo(68f).Within(.001f));
    }

    [TestCase(90f, 18f)]
    [TestCase(280f, 48f)]
    [TestCase(720f, 90f)]
    public void ExplicitCurveSupportsShortMediumAndLongRoutes(
        float length, float requestedHeight)
    {
        var root = new GameObject("RouteLength", typeof(RectTransform),
            typeof(CanvasRenderer));
        try
        {
            WDPreparedRouteGraphic graphic =
                root.AddComponent<WDPreparedRouteGraphic>();
            graphic.ConfigureGeometryExplicitCurve(Vector2.zero,
                new Vector2(length, length * .25f), 10f, 12f,
                requestedHeight, .35f);
            Assert.That(graphic.VisualEnd.x, Is.GreaterThan(graphic.VisualStart.x));
            Assert.That(graphic.Midpoint.x,
                Is.InRange(graphic.VisualStart.x, graphic.VisualEnd.x));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static WDConfirmedAttackStyle CustomConfirmedStyle(
        float shadowThickness = 3f, float shadowIntensity = .5f) => new(
        Color.white, .9f, 11f, 2f, Color.black, 1f, .5f,
        24f, 22f, Color.black, Color.cyan, 2f, .4f, 12f,
        74f, 42f, Color.white, 22f, true, shadowThickness,
        shadowIntensity, .3f, 2f, .9f);

    [Test]
    public void TransferCycleUsesProceduralGraphicInsteadOfTextGlyphs()
    {
        var root = new GameObject("TransferTest", typeof(RectTransform));
        WarDominionUITheme theme = ScriptableObject.CreateInstance<WarDominionUITheme>();
        try
        {
            WDTransferOwnershipBadgeView badge =
                root.AddComponent<WDTransferOwnershipBadgeView>();
            badge.Build(theme);
            Assert.That(root.GetComponentInChildren<WDTransferCycleGraphic>(),
                Is.Not.Null);
            Assert.That(root.GetComponentInChildren<TMPro.TextMeshProUGUI>(),
                Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(theme);
        }
    }
}
