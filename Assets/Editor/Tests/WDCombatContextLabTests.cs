using NUnit.Framework;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class WDCombatContextLabTests
{
#pragma warning disable CS0649
    [System.Serializable]
    private sealed class PresetProbe
    {
        public float confirmedBodyThickness;
        public float confirmedCurveHeight;
        public float confirmedCurvePosition;
        public float confirmedOriginExtension;
        public float confirmedDestinationExtension;
        public string confirmedCardBackground;
        public List<PresetFieldProbe> fields;
    }
    [System.Serializable]
    private sealed class PresetFieldProbe
    {
        public string name, type, text;
        public float number, x, y;
        public int integer;
        public bool boolean;
    }
#pragma warning restore CS0649

    // ============================================================
    // 01. CONTRATO DO LABORATÓRIO — cenários mínimos e caminho isolado
    // ============================================================
    [Test]
    public void ContextLabDefinesAllRequiredScenarios()
    {
        Assert.That(WDCombatContextLabController.ScenarioLabels.Count, Is.EqualTo(8));
        Assert.That(WDCombatContextLabController.ScenarioLabels,
            Does.Contain("ATAQUE CONQUISTADO"));
        Assert.That(WDCombatContextLabController.ScenarioLabels,
            Does.Contain("TRANSFERÊNCIA DE PROPRIEDADE"));
    }

    [Test]
    public void ContextLabUsesDedicatedDevelopmentScene()
    {
        Assert.That(WDCombatContextLabAuthoring.ScenePath,
            Is.EqualTo("Assets/Scenes/Development/WDCombatContextLab.unity"));
    }

    // ============================================================
    // 02. PRESET E DROPDOWN — persistência local e hit-area coincidente
    // ============================================================
    [Test]
    public void CapturedVisualPresetContainsValidEditableValues()
    {
        const string path = "UserSettings/WDCombatContextLabVisualPreset.json";
        Assert.That(File.Exists(path), Is.True);
        PresetProbe preset = JsonUtility.FromJson<PresetProbe>(
            File.ReadAllText(path));
        Assert.That(preset.confirmedBodyThickness, Is.GreaterThan(0f));
        Assert.That(float.IsFinite(preset.confirmedCurveHeight), Is.True);
        Assert.That(preset.confirmedCurvePosition, Is.InRange(0f, 1f));
        Assert.That(float.IsFinite(preset.confirmedOriginExtension), Is.True);
        Assert.That(float.IsFinite(preset.confirmedDestinationExtension), Is.True);
        Assert.That(preset.confirmedCardBackground,
            Does.Match("^#?[0-9A-Fa-f]{8}$"));
        Assert.That(preset.fields, Has.Count.EqualTo(99));
        Assert.That(preset.fields.Select(field => field.name).Distinct().Count(),
            Is.EqualTo(99));
        Assert.That(preset.fields.Any(field =>
            field.name == "resolutionArrowDuration" &&
            Mathf.Approximately(field.number, .48f)), Is.True);
        Assert.That(preset.fields.Any(field =>
            field.name == "conquestRevealDuration" &&
            Mathf.Approximately(field.number, .58f)), Is.True);
        Assert.That(preset.fields.Any(field =>
            field.name == "reinforcementRiseDistance" &&
            Mathf.Approximately(field.number, 24f)), Is.True);
    }

    [Test]
    public void CompletePresetRestoresDistanceScenarioColorEnumAndVectorFields()
    {
        var go = new GameObject("PresetTarget");
        try
        {
            WDCombatContextLabController controller =
                go.AddComponent<WDCombatContextLabController>();
            SerializedObject serialized = new(controller);
            int count = WDCombatContextLabAuthoring.ApplyVisualPresetForTests(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(count, Is.EqualTo(99));
            Assert.That(serialized.FindProperty("confirmedOriginExtension").floatValue,
                Is.EqualTo(-5f).Within(.001f));
            Assert.That(serialized.FindProperty("confirmedDestinationExtension").floatValue,
                Is.EqualTo(-.9f).Within(.001f));
            Assert.That(serialized.FindProperty("confirmedCurvePosition").floatValue,
                Is.EqualTo(.379f).Within(.001f));
            Assert.That(serialized.FindProperty("conqueredTravelEase").enumValueIndex,
                Is.EqualTo((int)WDAttackTravelEase.EaseOutCubic));
            Assert.That(serialized.FindProperty("handoffBadgeSize").vector2Value,
                Is.EqualTo(new Vector2(34f, 30f)));
            Assert.That(serialized.FindProperty("resolutionArrowDuration").floatValue,
                Is.EqualTo(.48f).Within(.001f));
            Assert.That(serialized.FindProperty("conquestRevealDuration").floatValue,
                Is.EqualTo(.58f).Within(.001f));
            Assert.That(serialized.FindProperty("reinforcementRiseDistance").floatValue,
                Is.EqualTo(24f).Within(.001f));
            Assert.That(serialized.FindProperty("reinforcementTextColor").colorValue,
                Is.EqualTo(new Color32(16, 24, 32, 255)));
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void GlassPremiumControlsExpandPersistenceWithoutChangingLegacyPreset()
    {
        var go = new GameObject("GlassPresetTarget");
        try
        {
            var controller = go.AddComponent<WDCombatContextLabController>();
            var serialized = new SerializedObject(controller);
            Assert.That(WDCombatContextLabAuthoring
                .CountPersistibleFieldsForTests(serialized), Is.EqualTo(111));
            Assert.That(serialized.FindProperty("visualFinish").enumValueIndex,
                Is.EqualTo((int)WDGameplayVisualFinish.ApprovedClassic));
            Assert.That(serialized.FindProperty("counterFinish").enumValueIndex,
                Is.EqualTo((int)WDGameplayVisualOverride.UseGlobal));
            Assert.That(serialized.FindProperty("glassVariant").enumValueIndex,
                Is.EqualTo((int)WDGameplayGlassVariant.Current));
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void GlassVariantsPreserveCurrentAndProfileManualControls()
    {
        var current = new WDGameplayGlassStyle(true, .8f, .5f, .4f, .6f,
            .2f, WDGameplayGlassVariant.Current);
        var clean = new WDGameplayGlassStyle(true, .8f, .5f, .4f, .6f,
            .2f, WDGameplayGlassVariant.Clean);
        var enhanced = new WDGameplayGlassStyle(true, .8f, .5f, .4f, .6f,
            .2f, WDGameplayGlassVariant.Enhanced);
        Assert.That(current.ProfiledGlassIntensity, Is.EqualTo(.8f).Within(.001f));
        Assert.That(current.ProfiledGradientStrength, Is.EqualTo(.5f).Within(.001f));
        Assert.That(current.ProfiledReflectionIntensity, Is.EqualTo(.4f).Within(.001f));
        Assert.That(clean.ProfiledBevelStrength,
            Is.LessThan(current.ProfiledBevelStrength));
        Assert.That(clean.ProfiledReflectionIntensity,
            Is.LessThan(current.ProfiledReflectionIntensity));
        Assert.That(enhanced.ProfiledGradientStrength,
            Is.GreaterThan(current.ProfiledGradientStrength));
        Assert.That(enhanced.ProfiledReflectionIntensity,
            Is.GreaterThan(current.ProfiledReflectionIntensity));
    }

    [Test]
    public void CasualtyBadgeUsesOwnerWhoActuallySufferedLosses()
    {
        ResolutionVisualEvent visualEvent = ResolutionVisualEvent.Attack(
            TerritorioClique.Dono.Jogador3, "Origin", "Destination", 5,
            8, 3, 4, 1, false, TerritorioClique.Dono.Jogador4,
            TerritorioClique.Dono.Jogador4);
        Assert.That(AttackResolutionPresenter.CasualtyOwnerFor(
            visualEvent, true), Is.EqualTo(TerritorioClique.Dono.Jogador3));
        Assert.That(AttackResolutionPresenter.CasualtyOwnerFor(
            visualEvent, false), Is.EqualTo(TerritorioClique.Dono.Jogador4));
    }

    [TestCase(16f, 52f)]
    [TestCase(16f, 16f)]
    public void DropdownLabelsStretchInsideTheirClickableRows(
        float left, float right)
    {
        var go = new GameObject("DropdownLabel", typeof(RectTransform));
        try
        {
            RectTransform rect = go.GetComponent<RectTransform>();
            WDCombatContextLabAuthoring.StretchDropdownLabel(rect, left, right);
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(rect.offsetMin, Is.EqualTo(new Vector2(left, 0f)));
            Assert.That(rect.offsetMax, Is.EqualTo(new Vector2(-right, 0f)));
            Assert.That(rect.anchoredPosition, Is.EqualTo(Vector2.zero));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void LabDropdownRebuildsItsTransientFadeRunner()
    {
        var go = new GameObject("LabDropdown", typeof(RectTransform),
            typeof(WDCombatContextLabDropdown));
        try
        {
            WDCombatContextLabDropdown dropdown =
                go.GetComponent<WDCombatContextLabDropdown>();
            typeof(WDCombatContextLabDropdown).GetMethod("Start",
                BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(dropdown, null);
            FieldInfo runner = typeof(TMP_Dropdown).GetField("m_AlphaTweenRunner",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(runner, Is.Not.Null);
            Assert.That(runner.GetValue(dropdown), Is.Not.Null,
                "O fade do TMP precisa de um runner válido antes de abrir a lista.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void LabSceneSerializesOneInteractiveDropdownWithSupportedUiStack()
    {
        string scene = File.ReadAllText(WDCombatContextLabAuthoring.ScenePath);
        const string dropdownGuid = "e136b5bf243c4314a8724a9c48d02df8";
        int component = scene.IndexOf(dropdownGuid,
            System.StringComparison.Ordinal);
        Assert.That(component, Is.GreaterThanOrEqualTo(0));
        Assert.That(scene.IndexOf(dropdownGuid, component + dropdownGuid.Length,
            System.StringComparison.Ordinal), Is.EqualTo(-1));

        int end = scene.IndexOf("--- !u!", component,
            System.StringComparison.Ordinal);
        string dropdownBlock = scene.Substring(component,
            (end < 0 ? scene.Length : end) - component);
        Assert.That(dropdownBlock, Does.Contain("m_Enabled: 1"));
        Assert.That(dropdownBlock, Does.Contain("m_Interactable: 1"));
        Assert.That(dropdownBlock, Does.Not.Contain("m_TargetGraphic: {fileID: 0}"));
        Assert.That(dropdownBlock, Does.Not.Contain("m_Template: {fileID: 0}"));
        Match template = Regex.Match(dropdownBlock,
            @"m_Template: \{fileID: (?<id>\d+)\}");
        Match itemText = Regex.Match(dropdownBlock,
            @"m_ItemText: \{fileID: (?<id>\d+)\}");
        Assert.That(template.Success, Is.True);
        Assert.That(itemText.Success, Is.True);
        Assert.That(scene, Does.Contain(
            "--- !u!224 &" + template.Groups["id"].Value));
        Assert.That(scene, Does.Contain(
            "--- !u!114 &" + itemText.Groups["id"].Value));
        Assert.That(scene, Does.Contain("m_Name: Template"));
        Assert.That(scene, Does.Contain("m_Name: Viewport"));
        Assert.That(scene, Does.Contain("m_Name: Content"));
        Assert.That(scene, Does.Contain("m_Name: Item"));
        Assert.That(scene, Does.Contain(
            "UnityEngine.UI::UnityEngine.UI.Toggle"));
        Assert.That(scene, Does.Not.Contain("--- !u!115"));
        Assert.That(scene, Does.Not.Contain(
            "m_Script: {fileID: 1813165631}"));
        Assert.That(scene, Does.Contain("UnityEngine.UI::UnityEngine.UI.GraphicRaycaster"));
        Assert.That(scene, Does.Contain(
            "Unity.InputSystem::UnityEngine.InputSystem.UI.InputSystemUIInputModule"));
        Assert.That(scene, Does.Contain(
            "UnityEngine.UI::UnityEngine.EventSystems.EventSystem"));
    }

    // ============================================================
    // 03. CONTROLES DE CENÁRIO — isolamento e badge sem texto/losango
    // ============================================================
    [Test]
    public void OptionalScenarioStylesRemainDisabledUnlessLabConfiguresThem()
    {
        var attack = default(WDAttackResolutionLabStyle);
        var reinforcement = default(WDReinforcementResolutionLabStyle);
        var transfer = default(WDTransferResolutionLabStyle);
        Assert.That(attack.Enabled, Is.False);
        Assert.That(reinforcement.Enabled, Is.False);
        Assert.That(transfer.Enabled, Is.False);
    }

    [Test]
    public void OwnershipTransferBadgeUsesCycleOnlyAndAppliesLabGeometry()
    {
        WarDominionUITheme theme = Resources.Load<WarDominionUITheme>(
            "UI/WarDominionUITheme");
        var go = new GameObject("Badge", typeof(RectTransform));
        try
        {
            WDTransferOwnershipBadgeView badge =
                go.AddComponent<WDTransferOwnershipBadgeView>();
            badge.Build(theme, true);
            var style = new WDTransferOwnershipBadgeStyle(true,
                new Vector2(72f, 54f), new Vector2(3f, -61f), 4.5f,
                .82f, .7f);
            badge.Configure(Color.cyan, style);
            badge.SetPosition(new Vector2(100f, 80f));

            RectTransform rect = go.GetComponent<RectTransform>();
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(72f, 54f)));
            Assert.That(rect.anchoredPosition,
                Is.EqualTo(new Vector2(103f, 19f)));
            Assert.That(go.GetComponentsInChildren<TMPro.TMP_Text>(true),
                Is.Empty);
            Assert.That(go.transform.Find("TransferCycle"), Is.Not.Null);
            Assert.That(go.transform.Find("TransferCycleOutline"), Is.Not.Null);
            Assert.That(go.transform.Find("TransferCycleHighlight"), Is.Not.Null);
            Assert.That(go.transform.Find("Diamond"), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void OwnershipTransferPremiumFinishRemainsOptInForOfficialFallback()
    {
        WarDominionUITheme theme = Resources.Load<WarDominionUITheme>(
            "UI/WarDominionUITheme");
        var go = new GameObject("OfficialFallback", typeof(RectTransform));
        try
        {
            WDTransferOwnershipBadgeView badge =
                go.AddComponent<WDTransferOwnershipBadgeView>();
            badge.Build(theme);
            badge.Configure(Color.red,
                WDTransferOwnershipBadgeStyle.PreparationDefault);

            Assert.That(go.transform.Find("TransferCycle"), Is.Not.Null);
            Assert.That(go.transform.Find("TransferCycleOutline"), Is.Not.Null);
            Assert.That(go.transform.Find("TransferCycleHighlight"), Is.Null);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [TestCase(5f, 9f, 3.78f)]
    [TestCase(12f, 20f, 5.04f)]
    public void TransferCycleThicknessIsClampedProportionallyWithoutCrossing(
        float radius, float requested, float expected)
    {
        Assert.That(WDTransferCycleGraphic.ClampThickness(radius, requested),
            Is.EqualTo(expected).Within(.001f));
    }

    [Test]
    public void ThickTransferCycleUsesTwoContinuousNonDegenerateStrips()
    {
        var go = new GameObject("TransferCycle", typeof(RectTransform),
            typeof(CanvasRenderer));
        var helper = new VertexHelper();
        try
        {
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(58f, 46f);
            WDTransferCycleGraphic graphic =
                go.AddComponent<WDTransferCycleGraphic>();
            graphic.Configure(Color.cyan, 9f, 1f);
            graphic.PopulateGeometry(helper);

            Assert.That(helper.currentVertCount, Is.EqualTo(106));
            Assert.That(helper.currentIndexCount, Is.EqualTo(294));
            var vertex = new UIVertex();
            for (int i = 0; i < helper.currentVertCount; i++)
            {
                helper.PopulateUIVertex(ref vertex, i);
                Assert.That(float.IsFinite(vertex.position.x), Is.True);
                Assert.That(float.IsFinite(vertex.position.y), Is.True);
            }

            var bodyLeft = new UIVertex();
            var bodyRight = new UIVertex();
            var headTip = new UIVertex();
            var headBaseLeft = new UIVertex();
            var headBaseRight = new UIVertex();
            helper.PopulateUIVertex(ref bodyLeft, 48);
            helper.PopulateUIVertex(ref bodyRight, 49);
            helper.PopulateUIVertex(ref headTip, 50);
            helper.PopulateUIVertex(ref headBaseLeft, 51);
            helper.PopulateUIVertex(ref headBaseRight, 52);
            Vector3 bodyCenter = (bodyLeft.position + bodyRight.position) * .5f;
            Vector3 headBaseCenter =
                (headBaseLeft.position + headBaseRight.position) * .5f;
            Assert.That(Vector3.Distance(bodyCenter, headBaseCenter),
                Is.LessThan(.001f));
            Assert.That(Vector3.Distance(bodyCenter, headTip.position),
                Is.GreaterThan(1f));
        }
        finally
        {
            helper.Dispose();
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void OwnershipTransferBadgeUsesTwoStraightOpposedArrows()
    {
        var go = new GameObject("TransferStraight", typeof(RectTransform),
            typeof(CanvasRenderer));
        var helper = new VertexHelper();
        try
        {
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(58f, 46f);
            WDTransferCycleGraphic graphic =
                go.AddComponent<WDTransferCycleGraphic>();
            graphic.Configure(Color.cyan, 6f, 1f, true);
            graphic.PopulateGeometry(helper);
            Assert.That(helper.currentVertCount, Is.EqualTo(14));
            Assert.That(helper.currentIndexCount, Is.EqualTo(18));
        }
        finally
        {
            helper.Dispose();
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void TransferPreparationPersistsOnlyWhileStateRemainsPrepared()
    {
        Assert.That(PreparedActionsOverlayController.ShouldKeepOwnershipTransfer(
            GameManager.FaseTurno.Preparacao, "Brazil"), Is.True);
        Assert.That(PreparedActionsOverlayController.ShouldKeepOwnershipTransfer(
            GameManager.FaseTurno.Preparacao, string.Empty), Is.False);
        Assert.That(PreparedActionsOverlayController.ShouldKeepOwnershipTransfer(
            GameManager.FaseTurno.Resolucao, "Brazil"), Is.False);
    }

    [Test]
    public void ReinforcementGraphiteStylePreservesReadableOpacityAndFadeControls()
    {
        Color graphite = new(16f / 255f, 24f / 255f, 32f / 255f, 1f);
        var style = new WDReinforcementResolutionLabStyle(true, .18f, .54f,
            .28f, .72f, 58f, .32f, 42f, 21f, graphite, .96f, .18f, .58f);
        Assert.That(style.TextColor, Is.EqualTo(graphite));
        Assert.That(style.TextOpacity, Is.EqualTo(.96f));
        Assert.That(style.ExitDuration, Is.EqualTo(.28f));
        Assert.That(style.EntryDuration + style.ReactionDuration +
            style.ExitDuration, Is.EqualTo(1f).Within(.001f));
        Assert.That(style.RiseDistance, Is.EqualTo(24f));
    }

    [Test]
    public void ReinforcementModesKeepIndependentEventAmounts()
    {
        ResolutionVisualEvent individual =
            WDCombatContextLabController.BuildReinforcementEvent(
                "Brazil", WDCombatContextLabController.ReinforcementMode.IndividualTouch,
                12, 5);
        ResolutionVisualEvent batch =
            WDCombatContextLabController.BuildReinforcementEvent(
                "Brazil", WDCombatContextLabController.ReinforcementMode.HoldAllAvailable,
                12, 5);

        Assert.That(individual.Amount, Is.EqualTo(1));
        Assert.That(individual.DestinationAfter, Is.EqualTo(13));
        Assert.That(batch.Amount, Is.EqualTo(5));
        Assert.That(batch.DestinationAfter, Is.EqualTo(17));
    }

    [Test]
    public void ResolutionArrowKeepsPreparationAmountBadgeHidden()
    {
        WarDominionUITheme theme = Resources.Load<WarDominionUITheme>(
            "UI/WarDominionUITheme");
        var go = new GameObject("ResolutionArrow", typeof(RectTransform));
        try
        {
            PreparedActionArrowView view =
                go.AddComponent<PreparedActionArrowView>();
            view.Build(theme);
            view.SetColorAndAmount(Color.cyan, 0);
            view.SetMode(WDPreparedActionVisualMode.ConfirmedAttack);

            Transform amount = go.transform.Find("TroopAmount");
            Assert.That(amount, Is.Not.Null);
            Assert.That(amount.gameObject.activeSelf, Is.False,
                "Na resolucao, a quantidade pertence ao contador viajante.");
        }
        finally { Object.DestroyImmediate(go); }
    }

    // ============================================================
    // 04. RITMO DE COMBATE — easing e contato antes do defensor
    // ============================================================
    [TestCase(WDAttackTravelEase.Linear)]
    [TestCase(WDAttackTravelEase.SmoothStep)]
    [TestCase(WDAttackTravelEase.EaseOutCubic)]
    [TestCase(WDAttackTravelEase.EaseInOutCubic)]
    public void TravelEasePreservesExactEndpoints(WDAttackTravelEase ease)
    {
        Assert.That(AttackResolutionPresenter.EvaluateTravelEase(0f, ease),
            Is.Zero.Within(.0001f));
        Assert.That(AttackResolutionPresenter.EvaluateTravelEase(1f, ease),
            Is.EqualTo(1f).Within(.0001f));
    }

    [Test]
    public void ContactPointStopsBeforeDefenderWithoutChangingLogicalEndpoint()
    {
        Vector3 origin = new(-2f, 1f, 0f);
        Vector3 defender = new(8f, 5f, 0f);
        Vector3 contact = AttackResolutionPresenter.ContactPoint(
            origin, defender, .62f);
        Assert.That(Vector3.Distance(contact, defender),
            Is.EqualTo(.62f).Within(.0001f));
        Assert.That(defender, Is.EqualTo(new Vector3(8f, 5f, 0f)));
    }

    [Test]
    public void CombatRhythmStyleClampsPressureBeforeRuntimeUse()
    {
        var style = new WDAttackResolutionLabStyle(true, .55f, .18f, 1.1f,
            1f, .65f, 42f, 34f, .76f, .30f,
            WDAttackTravelEase.EaseOutCubic, .22f, -.5f, .62f);
        Assert.That(style.ContactHoldDuration, Is.EqualTo(.22f));
        Assert.That(style.ContactPressureIntensity, Is.Zero);
        Assert.That(style.ContactSeparation, Is.EqualTo(.62f));
    }

    [Test]
    public void CombatRhythmDefaultsToFirmShortContact()
    {
        var style = new WDAttackResolutionLabStyle(true, .55f, .18f, 1.1f,
            1f, .65f, 42f, 34f, .76f, .30f);
        Assert.That(style.ContactHoldDuration, Is.EqualTo(.12f));
        Assert.That(style.ContactSeparation, Is.EqualTo(.62f));
    }

    [Test]
    public void CasualtyBadgesUseOnlyResolvedCombatValues()
    {
        ResolutionVisualEvent conquered = ResolutionVisualEvent.Attack(
            TerritorioClique.Dono.Jogador1, "A", "B", 7, 12, 5, 4, 3,
            true, TerritorioClique.Dono.Jogador2,
            TerritorioClique.Dono.Jogador1);
        ResolutionVisualEvent defended = ResolutionVisualEvent.Attack(
            TerritorioClique.Dono.Jogador1, "A", "B", 5, 12, 7, 9, 5,
            false, TerritorioClique.Dono.Jogador2,
            TerritorioClique.Dono.Jogador2);
        Assert.That(AttackResolutionPresenter.CalculateCasualties(conquered),
            Is.EqualTo((4, 4)));
        Assert.That(AttackResolutionPresenter.CalculateCasualties(defended),
            Is.EqualTo((5, 4)));
    }

    // ============================================================
    // 05. CENÁRIOS SINTÉTICOS — resultado, perdas e persistência local
    // ============================================================
    [Test]
    public void SyntheticAttackScenariosAreMathematicallyCoherentAndUnequivocal()
    {
        ResolutionVisualEvent conquered =
            WDCombatContextLabController.BuildConqueredAttackEvent("A", "B");
        ResolutionVisualEvent defended =
            WDCombatContextLabController.BuildDefendedAttackEvent("A", "B");

        Assert.That(conquered.Conquered, Is.True);
        Assert.That(conquered.Result,
            Is.EqualTo(ResolutionVisualResult.TerritoryConquered));
        Assert.That(conquered.NewOwner, Is.EqualTo(conquered.Player));
        Assert.That(defended.Conquered, Is.False);
        Assert.That(defended.Result,
            Is.EqualTo(ResolutionVisualResult.AttackDefended));
        Assert.That(defended.NewOwner, Is.EqualTo(defended.PreviousOwner));
        Assert.That(WDCombatContextLabController.HasCoherentCombatSnapshot(
            conquered), Is.True);
        Assert.That(WDCombatContextLabController.HasCoherentCombatSnapshot(
            defended), Is.True);
        Assert.That(AttackResolutionPresenter.CalculateCasualties(conquered),
            Is.EqualTo((6, 6)));
        Assert.That(AttackResolutionPresenter.CalculateCasualties(defended),
            Is.EqualTo((7, 3)));
    }

    [Test]
    public void AlternatingAttackOutcomesDoesNotRetainPreviousOwner()
    {
        var destinationObject = new GameObject("Destination");
        try
        {
            destinationObject.AddComponent<TerritorioTropas>();
            TerritorioClique destination =
                destinationObject.AddComponent<TerritorioClique>();
            WDCombatContextLabController.ApplySyntheticOutcomeToTerritories(
                WDCombatContextLabController.BuildConqueredAttackEvent("A", "B"),
                null, destination);
            Assert.That(destination.dono,
                Is.EqualTo(TerritorioClique.Dono.Jogador1));

            WDCombatContextLabController.ApplySyntheticOutcomeToTerritories(
                WDCombatContextLabController.BuildDefendedAttackEvent("A", "B"),
                null, destination);
            Assert.That(destination.dono,
                Is.EqualTo(TerritorioClique.Dono.Jogador2));
            Assert.That(destination.Tropas, Is.EqualTo(6));
        }
        finally { Object.DestroyImmediate(destinationObject); }
    }

    [Test]
    public void ResolvedOwnershipTransferUpdatesLogicalClone()
    {
        var destinationObject = new GameObject("Handoff");
        try
        {
            destinationObject.AddComponent<TerritorioTropas>();
            TerritorioClique destination =
                destinationObject.AddComponent<TerritorioClique>();
            ResolutionVisualEvent handoff = ResolutionVisualEvent.TerritoryHandoff(
                TerritorioClique.Dono.Jogador1,
                TerritorioClique.Dono.Jogador3, "Handoff", 8, 8);
            WDCombatContextLabController.ApplySyntheticOutcomeToTerritories(
                handoff, null, destination);
            Assert.That(destination.dono,
                Is.EqualTo(TerritorioClique.Dono.Jogador3));
            Assert.That(destination.Tropas, Is.EqualTo(8));
        }
        finally { Object.DestroyImmediate(destinationObject); }
    }

    [TestCase("Brazil", -3.3034f, -1.8843f, -2.3114886f, -.8062347f)]
    [TestCase("Argentina", -3.2134f, -3.2043f, -2.8533697f, -2.2422838f)]
    [TestCase("Peru", -3.4334f, -1.7543f, -2.871789f, -1.2759417f)]
    [TestCase("Venezuela", -3.7534f, -.8843001f, -3.4071684f, -.25515276f)]
    public void LabCountersPreserveTerritoryWorldTransformAndCanonicalArtPosition(
        string id, float territoryX, float territoryY,
        float counterArtX, float counterArtY)
    {
        var artObject = new GameObject("ClassicArt");
        var sourceParent = new GameObject("Territorios");
        var sourceObject = new GameObject(id);
        var labParent = new GameObject("01_RealTerritoryStage");
        var territoryObject = new GameObject(id + "_ContextLab");
        var counterObject = new GameObject("ContadorModerno");
        try
        {
            artObject.transform.position = new Vector3(-.0234f, .0257f, 0f);
            sourceParent.transform.position = new Vector3(-.57622f, .12875f, 0f);
            sourceObject.transform.SetParent(sourceParent.transform, false);
            sourceObject.transform.localPosition =
                new Vector3(territoryX, territoryY, 0f) -
                sourceParent.transform.position;
            WDCombatContextLabAuthoring.ApplySourceWorldTransform(
                territoryObject.transform, labParent.transform,
                sourceObject.transform.position, sourceObject.transform.rotation,
                sourceObject.transform.lossyScale);
            counterObject.transform.SetParent(territoryObject.transform, false);
            TerritorioClique territory =
                territoryObject.AddComponent<TerritorioClique>();
            var data = new DefinicaoTerritorioMapa
            {
                id = id,
                possuiPosicaoContador = true,
                posicaoContador = new Vector2(counterArtX, counterArtY)
            };
            Vector3 expectedWorld = artObject.transform.TransformPoint(
                data.posicaoContador);

            Assert.That(WDCombatContextLabAuthoring.ApplyCanonicalCounterPosition(
                territory, artObject.transform, data), Is.True);
            Assert.That(Vector3.Distance(territoryObject.transform.position,
                sourceObject.transform.position), Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(counterObject.transform.position,
                expectedWorld), Is.LessThan(.0001f));

            Vector3 firstPosition = counterObject.transform.position;
            Assert.That(WDCombatContextLabAuthoring.ApplyCanonicalCounterPosition(
                territory, artObject.transform, data), Is.True);
            Assert.That(Vector3.Distance(counterObject.transform.position,
                firstPosition),
                Is.LessThan(.0001f));
        }
        finally
        {
            Object.DestroyImmediate(counterObject);
            Object.DestroyImmediate(territoryObject);
            Object.DestroyImmediate(labParent);
            Object.DestroyImmediate(sourceObject);
            Object.DestroyImmediate(sourceParent);
            Object.DestroyImmediate(artObject);
        }
    }

    [Test]
    public void SelectionGlowSettlesAtConfiguredPersistentIntensity()
    {
        Assert.That(TerritorioVisual.EvaluateSelectionPulse(0f, .2f, .8f),
            Is.EqualTo(.8f).Within(.001f));
        Assert.That(TerritorioVisual.EvaluateSelectionPulse(.04f, .2f, .8f),
            Is.GreaterThan(.8f));
        Assert.That(TerritorioVisual.EvaluateSelectionPulse(.19f, .2f, .8f),
            Is.GreaterThanOrEqualTo(.8f));
        Assert.That(TerritorioVisual.EvaluateSelectionPulse(.2f, .2f, .8f),
            Is.EqualTo(.8f).Within(.001f));
        Assert.That(TerritorioVisual.EvaluateSelectionPulse(1f, .2f, .8f),
            Is.EqualTo(.8f).Within(.001f));
    }

    [Test]
    public void ResolutionArrowRevealPreservesStartAndReachesFullDestination()
    {
        var go = new GameObject("ResolutionRoute", typeof(RectTransform),
            typeof(CanvasRenderer));
        try
        {
            WDPreparedRouteGraphic route = go.AddComponent<WDPreparedRouteGraphic>();
            route.ConfigureGeometryExplicitCurve(Vector2.zero,
                new Vector2(120f, 40f), 0f, 0f, 18f, .42f);
            Vector2 fullEnd = route.VisualEnd;

            route.SetRevealProgress(.5f);
            Assert.That(route.VisualStart, Is.EqualTo(Vector2.zero));
            Assert.That(Vector2.Distance(route.VisualEnd, route.VisualStart),
                Is.GreaterThan(1f));
            Assert.That(Vector2.Distance(route.VisualEnd, fullEnd),
                Is.GreaterThan(1f));

            route.SetRevealProgress(1f);
            Assert.That(Vector2.Distance(route.VisualEnd, fullEnd),
                Is.LessThan(.001f));
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void ResolutionArrowOccludesGeometryInsideTravellingCounterBounds()
    {
        var go = new GameObject("OccludedRoute", typeof(RectTransform),
            typeof(CanvasRenderer));
        var complete = new VertexHelper();
        var occluded = new VertexHelper();
        try
        {
            WDPreparedRouteGraphic route = go.AddComponent<WDPreparedRouteGraphic>();
            route.Mode = WDPreparedActionVisualMode.ConfirmedAttack;
            route.ConfigureConfirmedAttackStyle(new WDConfirmedAttackStyle(
                Color.white, 1f, 12f, 2f, Color.black, 1f, .2f,
                18f, 24f, Color.black, Color.white, 1f, .1f, 8f,
                54f, 34f, Color.white, 18f, true, 3f, .4f,
                .2f, .2f, 1f));
            route.ConfigureGeometryExplicitCurve(Vector2.zero,
                new Vector2(180f, 0f), 0f, 0f, 0f, .5f);
            route.PopulateGeometry(complete);

            route.ConfigureOcclusion(new Vector2(90f, 0f), 18f, true);
            route.PopulateGeometry(occluded);

            Assert.That(complete.currentIndexCount, Is.GreaterThan(0));
            Assert.That(occluded.currentIndexCount,
                Is.LessThan(complete.currentIndexCount));
            Assert.That(occluded.currentIndexCount, Is.GreaterThan(0),
                "A oclusao deve esconder somente o trecho sob o contador.");
        }
        finally
        {
            complete.Dispose();
            occluded.Dispose();
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void CounterOcclusionUsesVisibleRadiusInsteadOfBoundsDiagonal()
    {
        float radius = AttackResolutionPresenter.CalculateCounterOcclusionRadius(
            Vector2.zero, new Vector2(-20f, 0f), new Vector2(20f, 0f),
            new Vector2(0f, -20f), new Vector2(0f, 20f));
        Assert.That(radius, Is.EqualTo(20f).Within(.001f));
        Assert.That(radius, Is.LessThan(Mathf.Sqrt(20f * 20f + 20f * 20f)),
            "Os cantos transparentes nao podem ampliar a exclusao.");
    }

    [Test]
    public void ResolutionTipUsesCurrentCounterPositionAndLocalTangent()
    {
        Vector2 center = new(120f, 45f);
        Vector2 tip = AttackResolutionPresenter.CalculateTrailingArrowTip(
            center, new Vector2(3f, 4f), 20f);
        Assert.That(Vector2.Distance(tip, center),
            Is.EqualTo(20f).Within(.001f));
        Assert.That(Vector2.Dot(center - tip, new Vector2(3f, 4f).normalized),
            Is.EqualTo(20f).Within(.001f));
    }

    [Test]
    public void ResolutionTipRemainsInsideTravellingCounterSilhouette()
    {
        Vector2 center = new(120f, 45f);
        Vector2 tip = AttackResolutionPresenter.CalculateOccludedArrowTip(
            center);

        Assert.That(tip, Is.EqualTo(center));
        Assert.That(Vector2.Distance(tip, center), Is.Zero.Within(.001f),
            "A ponta nao pode receber deslocamento positivo alem do contador.");
    }

    [Test]
    public void DirectResolutionGeometryKeepsCompleteTipAtRequestedPosition()
    {
        var go = new GameObject("DirectResolutionRoute", typeof(RectTransform),
            typeof(CanvasRenderer));
        var geometry = new VertexHelper();
        try
        {
            WDPreparedRouteGraphic route = go.AddComponent<WDPreparedRouteGraphic>();
            route.Mode = WDPreparedActionVisualMode.ConfirmedAttack;
            route.ConfigureConfirmedAttackStyle(new WDConfirmedAttackStyle(
                Color.white, 1f, 12f, 2f, Color.black, 1f, .2f,
                18f, 24f, Color.black, Color.white, 1f, .1f, 8f,
                54f, 34f, Color.white, 18f, true, 3f, .4f,
                .2f, .2f, 1f));
            Vector2 requestedTip = new(150f, 35f);
            route.ConfigureDirectGeometry(Vector2.zero,
                new Vector2(70f, 24f), requestedTip);
            route.ConfigureOcclusion(default, 0f, false);
            route.PopulateGeometry(geometry);

            Assert.That(Vector2.Distance(route.VisualEnd, requestedTip),
                Is.LessThan(.001f));
            Assert.That(geometry.currentIndexCount, Is.GreaterThan(0),
                "Corpo e ponta precisam existir no mesmo frame.");
        }
        finally
        {
            geometry.Dispose();
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void CounterAndArrowUseTheSameEasedTravelProgress()
    {
        Vector3 origin = new(0f, 0f, 0f);
        Vector3 control = new(5f, 4f, 0f);
        Vector3 destination = new(12f, 0f, 0f);
        float eased = AttackResolutionPresenter.EvaluateTravelEase(.45f,
            WDAttackTravelEase.EaseOutCubic);
        Vector3 tangent = AttackResolutionPresenter.QuadraticTangent(origin,
            control, destination, eased);

        Assert.That(eased, Is.InRange(0f, 1f));
        Assert.That(tangent.sqrMagnitude, Is.GreaterThan(.001f));
    }

    [Test]
    public void ResolutionArrowRetiresImmediatelyAtTravelBoundary()
    {
        var go = new GameObject("ResolutionArrow", typeof(RectTransform));
        PreparedActionArrowView arrow =
            go.AddComponent<PreparedActionArrowView>();
        Assert.That(arrow.gameObject.activeSelf, Is.True);

        arrow = AttackResolutionPresenter.RetireResolutionArrow(arrow);

        Assert.That(arrow, Is.Null);
        Assert.That(go == null, Is.True,
            "Em EditMode a autoridade deve destruir o objeto imediatamente.");
    }

    [Test]
    public void FinalSynchronizedFrameAppliesOnlyToLabAttacks()
    {
        ResolutionVisualEvent attack =
            WDCombatContextLabController.BuildDefendedAttackEvent("A", "B");
        ResolutionVisualEvent movement = ResolutionVisualEvent.Movement(
            TerritorioClique.Dono.Jogador1, "A", "B", 2, 5, 3, 4, 6,
            TerritorioClique.Dono.Jogador1,
            TerritorioClique.Dono.Jogador1);

        Assert.That(AttackResolutionPresenter.
            RequiresFinalSynchronizedAttackFrame(attack, true), Is.True);
        Assert.That(AttackResolutionPresenter.
            RequiresFinalSynchronizedAttackFrame(movement, true), Is.False);
        Assert.That(AttackResolutionPresenter.
            RequiresFinalSynchronizedAttackFrame(attack, false), Is.False);
    }

    [Test]
    public void ResolutionCleanupRemovesActiveArrowWithoutAccumulation()
    {
        var presenterObject = new GameObject("AttackPresenter");
        var arrowObject = new GameObject("ResolutionAttackArrow",
            typeof(RectTransform));
        try
        {
            AttackResolutionPresenter presenter =
                presenterObject.AddComponent<AttackResolutionPresenter>();
            PreparedActionArrowView arrow =
                arrowObject.AddComponent<PreparedActionArrowView>();
            typeof(AttackResolutionPresenter).GetField("activeResolutionArrow",
                BindingFlags.Instance | BindingFlags.NonPublic)?
                .SetValue(presenter, arrow);

            presenter.ResetTransientVisuals();

            Assert.That(arrowObject == null, Is.True);
            Assert.That(typeof(AttackResolutionPresenter).GetField(
                "activeResolutionArrow", BindingFlags.Instance |
                BindingFlags.NonPublic)?.GetValue(presenter), Is.Null);
        }
        finally
        {
            if (arrowObject != null) Object.DestroyImmediate(arrowObject);
            Object.DestroyImmediate(presenterObject);
        }
    }

    [Test]
    public void LabDirectionalRevealDoesNotScheduleSecondConquestReaction()
    {
        Assert.That(AttackResolutionPresenter.ShouldPlayPostConquestReaction(
            true, true), Is.False);
        Assert.That(AttackResolutionPresenter.ShouldPlayPostConquestReaction(
            true, false), Is.False);
        Assert.That(AttackResolutionPresenter.ShouldPlayPostConquestReaction(
            false, true), Is.True,
            "O fallback historico oficial deve permanecer inalterado.");
    }

    [Test]
    public void PremiumTravellingCounterCreatesRealRendererLayersWithoutDuplicateNumber()
    {
        GameObject copy = AttackResolutionPresenter.CriarRepresentacaoAnimada(
            null, 9, TerritorioClique.Dono.Jogador1);
        try
        {
            WDArmyCounterView view = copy.GetComponent<WDArmyCounterView>();
            Assert.That(view, Is.Not.Null);
            view.AplicarEstiloBadgeViajante(Color.cyan);

            foreach (string layerName in new[] {
                "TravellingShadow", "TravellingIdentity", "TravellingSilver" })
            {
                Transform layer = copy.transform.Find(
                    "ArmyCounterSpriteRoot/" + layerName);
                Assert.That(layer, Is.Not.Null, layerName);
                Assert.That(layer.GetComponent<SpriteRenderer>(), Is.Not.Null,
                    layerName + " precisa de SpriteRenderer real.");
            }
            Assert.That(copy.GetComponentsInChildren<TMPro.TextMeshPro>(true),
                Has.Length.EqualTo(1));
        }
        finally { Object.DestroyImmediate(copy); }
    }

    [Test]
    public void ContinuousSelectionDoesNotRestartPulseAndResetClearsIt()
    {
        var go = new GameObject("SelectionPulse", typeof(SpriteRenderer),
            typeof(TerritorioTropas), typeof(TerritorioClique));
        try
        {
            TerritorioVisual visual = go.GetComponent<TerritorioVisual>() ??
                go.AddComponent<TerritorioVisual>();
            visual.ConfigureLabFeedback(.8f, .25f, .4f, .5f);
            visual.DestacarSelecao();
            float firstEnd = visual.SelectionPulseEndTime;
            visual.DestacarSelecao();
            Assert.That(visual.SelectionPulseEndTime,
                Is.EqualTo(firstEnd).Within(.0001f));
            Assert.That(visual.SelectionRaised, Is.True);

            visual.ResetTransientFeedback();
            Assert.That(visual.SelectionRaised, Is.False);
            Assert.That(float.IsNegativeInfinity(visual.SelectionPulseEndTime),
                Is.True);
        }
        finally { Object.DestroyImmediate(go); }
    }
}
