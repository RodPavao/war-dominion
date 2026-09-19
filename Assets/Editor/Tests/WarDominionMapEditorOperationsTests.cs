using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public sealed class WarDominionMapEditorOperationsTests
{
    private readonly List<Object> disposable = new List<Object>();

    // ============================================================
    // 01. CONEXÕES BIDIRECIONAIS, REMOÇÃO E DUPLICAÇÃO
    // ============================================================

    [Test]
    public void AddConnectionCreatesOneBidirectionalRecord()
    {
        DefinicaoMapa map = CreateMap("map", true, 1);
        Assert.That(WarDominionMapEditorOperations.AddBidirectionalConnection(
            map, "a", "b", TipoConexaoMapa.Especial), Is.True);
        Assert.That(map.Conexoes, Has.Count.EqualTo(1));
        Assert.That(map.Conexoes[0].bidirecional, Is.True);
        Assert.That(WarDominionMapEditorOperations.ContainsConnection(map, "b", "a"), Is.True);
    }

    [Test]
    public void DuplicateConnectionIsNotAddedAndRemovalWorksFromEitherDirection()
    {
        DefinicaoMapa map = CreateMap("map", true, 1);
        WarDominionMapEditorOperations.AddBidirectionalConnection(map, "a", "b",
            TipoConexaoMapa.Terrestre);
        Assert.That(WarDominionMapEditorOperations.AddBidirectionalConnection(
            map, "b", "a", TipoConexaoMapa.Terrestre), Is.False);
        Assert.That(map.Conexoes, Has.Count.EqualTo(1));
        Assert.That(WarDominionMapEditorOperations.RemoveConnection(map, "b", "a"), Is.True);
        Assert.That(map.Conexoes, Is.Empty);
    }

    // ============================================================
    // 02. VALIDAÇÃO PURA E TROCA SEGURA DE ARTE
    // ============================================================

    [Test]
    public void ValidationDetectsMissingRegionIsolatedTerritoryAndDuplicateMapId()
    {
        DefinicaoMapa first = CreateMap("duplicate", true, 1);
        DefinicaoMapa second = CreateMap("duplicate", true, 1);
        first.Territorios[0].regiaoId = string.Empty;
        WDMapEditorValidationReport report = WarDominionMapEditorOperations.Validate(
            first, new[] { first, second });
        string[] codes = report.Issues.Select(issue => issue.Codigo).ToArray();
        Assert.That(codes, Does.Contain("TERRITORIO_SEM_REGIAO"));
        Assert.That(codes, Does.Contain("TERRITORIO_ISOLADO"));
        Assert.That(codes, Does.Contain("MAPA_ID_DUPLICADO"));
    }

    [Test]
    public void ValidationDetectsMissingArtAndRegionBonus()
    {
        DefinicaoMapa map = CreateMap("map", false, 0);
        string[] codes = WarDominionMapEditorOperations.Validate(map, new[] { map })
            .Issues.Select(issue => issue.Codigo).ToArray();
        Assert.That(codes, Does.Contain("ARTE_AUSENTE"));
        Assert.That(codes, Does.Contain("REGIAO_SEM_BONUS"));
    }

    [Test]
    public void ReplacingArtPreservesTerritoriesRegionsAndConnections()
    {
        DefinicaoMapa map = CreateMap("map", true, 1);
        WarDominionMapEditorOperations.AddBidirectionalConnection(map, "a", "b",
            TipoConexaoMapa.Terrestre);
        int territories = map.Territorios.Count;
        int regions = map.Regioes.Count;
        int connections = map.Conexoes.Count;
        Sprite replacement = CreateSprite();
        Assert.That(WarDominionMapEditorOperations.ReplaceMainArt(map, replacement), Is.True);
        Assert.That(map.ArteBase, Is.SameAs(replacement));
        Assert.That(map.Territorios, Has.Count.EqualTo(territories));
        Assert.That(map.Regioes, Has.Count.EqualTo(regions));
        Assert.That(map.Conexoes, Has.Count.EqualTo(connections));
    }

    // ============================================================
    // 03. ROUND-TRIP ESPACIAL E SELEÇÃO MÚLTIPLA
    // ============================================================

    [TestCase(1365f, 1024f, 100f, 50f)]
    [TestCase(2048f, 1024f, -350f, 210f)]
    [TestCase(900f, 1600f, 42f, -515f)]
    public void ViewportTransformRoundTripPreservesMapPosition(float width,
        float height, float x, float y)
    {
        var bounds = new Bounds(new Vector3(37f, -21f), new Vector3(width, height));
        var transform = new WDMapViewportTransform(new Rect(120f, 80f, 777f, 431f), bounds);
        Vector2 original = new Vector2(x, y);
        Vector2 roundTrip = transform.ViewportToMap(transform.MapToViewport(original));
        Assert.That(roundTrip.x, Is.EqualTo(original.x).Within(0.001f));
        Assert.That(roundTrip.y, Is.EqualTo(original.y).Within(0.001f));
    }

    [Test]
    public void SelectionSupportsSimpleClickControlToggleAndExactPair()
    {
        var selection = new WDMapEditorSelection();
        selection.Click(2, false);
        Assert.That(selection.Indices, Is.EqualTo(new[] { 2 }));
        selection.Click(5, true);
        Assert.That(selection.HasPair, Is.True);
        selection.Click(5, true);
        Assert.That(selection.Indices, Is.EqualTo(new[] { 2 }));
        selection.Click(7, true);
        selection.Click(9, true);
        Assert.That(selection.Indices.Count, Is.EqualTo(3));
        Assert.That(selection.HasPair, Is.False);
    }

    [Test]
    public void RegionCurationSearchMatchesDisplayNameAndIdWithoutMutation()
    {
        DefinicaoMapa map = CreateMap("map", true, 1);
        string[] before = map.Territorios.Select(item => item.regiaoId).ToArray();

        Assert.That(WarDominionMapEditorOperations.SearchTerritories(map, "A")
            .Select(item => item.id), Does.Contain("a"));
        Assert.That(WarDominionMapEditorOperations.SearchTerritories(map, "b")
            .Select(item => item.nomeExibido), Does.Contain("B"));
        Assert.That(map.Territorios.Select(item => item.regiaoId), Is.EqualTo(before));
    }

    [Test]
    public void MapEditorExposesNamesTab()
    {
        System.Type tab = typeof(WarDominionMapEditorWindow).GetNestedType("Tab",
            System.Reflection.BindingFlags.NonPublic);
        Assert.That(tab, Is.Not.Null);
        Assert.That(System.Enum.GetNames(tab), Does.Contain("Nomes"));
    }

    [Test]
    public void TerritoryNameManualPositionUsesCanonicalDataAndSupportsUndo()
    {
        DefinicaoMapa map = CreateMap("map", true, 1);
        Vector2 position = new Vector2(1.25f, -0.75f);

        Assert.That(WarDominionMapEditorOperations.SetTerritoryNamePosition(
            map, "a", position), Is.True);
        Assert.That(map.Territorios[0].possuiPosicaoNomeManual, Is.True);
        Assert.That(map.Territorios[0].posicaoNomeManual, Is.EqualTo(position));

        Undo.PerformUndo();
        Assert.That(map.Territorios[0].possuiPosicaoNomeManual, Is.False);
    }

    [Test]
    public void TerritoryNameIndividualAndGlobalResetOnlyRemoveCanonicalOverrides()
    {
        DefinicaoMapa map = CreateMap("map", true, 1);
        WarDominionMapEditorOperations.SetTerritoryNamePosition(map, "a",
            new Vector2(1f, 2f));
        WarDominionMapEditorOperations.SetTerritoryNamePosition(map, "b",
            new Vector2(3f, 4f));

        Assert.That(WarDominionMapEditorOperations.ResetTerritoryNamePosition(
            map, "a"), Is.True);
        Assert.That(map.Territorios[0].possuiPosicaoNomeManual, Is.False);
        Assert.That(map.Territorios[1].possuiPosicaoNomeManual, Is.True);
        Assert.That(WarDominionMapEditorOperations.ResetAllTerritoryNamePositions(map),
            Is.EqualTo(1));
        Assert.That(map.Territorios.All(item =>
            !item.possuiPosicaoNomeManual), Is.True);
    }

    [Test]
    public void RegionCurationTerritoryIdResolvesTheCorrectTerritory()
    {
        DefinicaoMapa map = CreateMap("map", true, 1);

        int index = WarDominionMapEditorWindow.ResolveTerritoryIndex(map, "b");

        Assert.That(index, Is.EqualTo(1));
        Assert.That(map.Territorios[index].nomeExibido, Is.EqualTo("B"));
    }

    [Test]
    public void RegionCurationMoveUpdatesBothSidesAndRemovesDuplicatesAtomically()
    {
        DefinicaoMapa map = CreateMapWithSecondRegion();
        map.Regioes[0].territorioIds.Add("a");

        Assert.That(WarDominionMapEditorOperations.MoveTerritoryToRegion(
            map, "a", "second"), Is.True);
        Assert.That(map.Territorios.Single(item => item.id == "a").regiaoId,
            Is.EqualTo("second"));
        Assert.That(map.Regioes[0].territorioIds, Does.Not.Contain("a"));
        Assert.That(map.Regioes[1].territorioIds.Count(id => id == "a"), Is.EqualTo(1));
        Assert.That(WarDominionMapEditorOperations.DiagnoseRegionAssociation(map, "a")
            .State, Is.EqualTo(WarDominionMapEditorOperations.RegionAssociationState.Consistent));
    }

    [Test]
    public void RegionCurationRemovalLeavesTerritoryConsistentlyUnassigned()
    {
        DefinicaoMapa map = CreateMap("map", true, 1);

        Assert.That(WarDominionMapEditorOperations.RemoveTerritoryFromRegion(
            map, "a"), Is.True);
        Assert.That(map.Territorios[0].regiaoId, Is.Empty);
        Assert.That(map.Regioes[0].territorioIds, Does.Not.Contain("a"));
        Assert.That(WarDominionMapEditorOperations.DiagnoseRegionAssociation(map, "a")
            .State, Is.EqualTo(WarDominionMapEditorOperations.RegionAssociationState.Unassigned));
    }

    [Test]
    public void RegionCurationAddsExistingUnassignedTerritoryConsistently()
    {
        DefinicaoMapa map = CreateMapWithSecondRegion();
        WarDominionMapEditorOperations.RemoveTerritoryFromRegion(map, "a");

        Assert.That(WarDominionMapEditorOperations.MoveTerritoryToRegion(
            map, "a", "second"), Is.True);
        Assert.That(map.Territorios[0].regiaoId, Is.EqualTo("second"));
        Assert.That(map.Regioes[1].territorioIds, Does.Contain("a"));
    }

    [Test]
    public void RegionCurationMoveSupportsUndoAndRedoWithoutDivergence()
    {
        DefinicaoMapa map = CreateMapWithSecondRegion();
        Assert.That(WarDominionMapEditorOperations.MoveTerritoryToRegion(
            map, "a", "second"), Is.True);

        Undo.PerformUndo();
        Assert.That(map.Territorios[0].regiaoId, Is.EqualTo("r"));
        Assert.That(map.Regioes[0].territorioIds, Does.Contain("a"));
        Assert.That(map.Regioes[1].territorioIds, Does.Not.Contain("a"));

        Undo.PerformRedo();
        Assert.That(map.Territorios[0].regiaoId, Is.EqualTo("second"));
        Assert.That(map.Regioes[0].territorioIds, Does.Not.Contain("a"));
        Assert.That(map.Regioes[1].territorioIds, Does.Contain("a"));
    }

    [Test]
    public void RegionCurationDetectsBothKindsOfBidirectionalInconsistency()
    {
        DefinicaoMapa map = CreateMapWithSecondRegion();
        map.Regioes[0].territorioIds.Remove("a");
        Assert.That(WarDominionMapEditorOperations.DiagnoseRegionAssociation(map, "a")
            .State, Is.EqualTo(WarDominionMapEditorOperations.RegionAssociationState.RegionMissingTerritory));

        map.Regioes[1].territorioIds.Add("a");
        Assert.That(WarDominionMapEditorOperations.DiagnoseRegionAssociation(map, "a")
            .State, Is.EqualTo(WarDominionMapEditorOperations.RegionAssociationState.RegionContainsTerritoryWithDifferentOwner));
    }

    [Test]
    public void RegionPreviewHitTestUsesCanonicalPolygonAndDoesNotMutateMap()
    {
        DefinicaoMapa map = CreateMap("promoted", false, 1);
        Sprite art = CreateSprite(1920, 1080);
        map.ConfigurarNoEditor(map.MapaId, map.NomeInterno, map.NomeExibido,
            map.Descricao, map.TemaId, art, map.CentroCamera, map.TamanhoOrtografico,
            map.Territorios.ToList(), map.Regioes.ToList(), map.Conexoes.ToList());
        var snapshot = new WDMapSpatialSnapshot();
        snapshot.SetPolygonsForTests("a", new[]
        {
            (IReadOnlyList<Vector2>)new[]
            {
                new Vector2(-2f, -2f), new Vector2(2f, -2f),
                new Vector2(2f, 2f), new Vector2(-2f, 2f), new Vector2(-2f, -2f)
            }
        });
        string[] before = map.Territorios.Select(item => item.regiaoId).ToArray();

        var context = new WDMapActivePreviewContext(map, snapshot);
        Assert.That(context.TryHitTerritory(Vector2.zero, out int selected), Is.True);
        Assert.That(selected, Is.EqualTo(0));
        Assert.That(context.TryHitTerritory(new Vector2(3f, 0f), out _), Is.False);
        Assert.That(map.Territorios.Select(item => item.regiaoId), Is.EqualTo(before));
    }

    [Test]
    public void ActivePreviewContextUsesCurrentPromotedArtAndArtLocalPositions()
    {
        DefinicaoMapa map = CreateMap("promoted", false, 1);
        Sprite promotedArt = CreateSprite(1920, 1080);
        map.ConfigurarNoEditor(map.MapaId, map.NomeInterno, map.NomeExibido,
            map.Descricao, map.TemaId, promotedArt, map.CentroCamera,
            map.TamanhoOrtografico, map.Territorios.ToList(), map.Regioes.ToList(),
            map.Conexoes.ToList());
        Vector2 stored = new Vector2(4.25f, -1.75f);

        WDMapActivePreviewContext context = WDMapActivePreviewContext.Resolve(map);

        Assert.That(context.Art, Is.SameAs(promotedArt));
        Assert.That(context.UsesArtLocalTerritoryPositions, Is.True);
        Assert.That(context.ToArtLocal("a", stored), Is.EqualTo(stored));
        Assert.That(context.ToStoredPosition("a", stored), Is.EqualTo(stored));
    }

    [Test]
    public void ActivePreviewContextPreservesLegacyMapMode()
    {
        DefinicaoMapa map = CreateMap("legacy", true, 1);

        WDMapActivePreviewContext context = WDMapActivePreviewContext.Resolve(map);

        Assert.That(context.Art, Is.SameAs(map.ArteBase));
        Assert.That(context.UsesArtLocalTerritoryPositions, Is.False);
    }

    [Test]
    public void ReplacingActiveArtInvalidatesPreviewContextIdentity()
    {
        DefinicaoMapa map = CreateMap("map", true, 1);
        WDMapActivePreviewContext context = WDMapActivePreviewContext.Resolve(map);
        Sprite replacement = CreateSprite(1920, 1080);

        WarDominionMapEditorOperations.ReplaceMainArt(map, replacement);

        Assert.That(context.Matches(map), Is.False);
        Assert.That(WDMapActivePreviewContext.Resolve(map).Art,
            Is.SameAs(replacement));
    }

    [Test]
    public void PreviewContextRemainsValidWithoutRelevantAssetChange()
    {
        DefinicaoMapa map = CreateMap("stable", true, 1);
        WDMapActivePreviewContext context = WDMapActivePreviewContext.Resolve(map);

        Assert.That(context.Matches(map), Is.True);
        Assert.That(context.RelevantAssetsUnchanged(map), Is.True);
    }

    [Test]
    public void PromotedPreviewDoesNotRequireOpeningSceneSnapshot()
    {
        DefinicaoMapa promoted = CreateMap("promoted", false, 1);
        Sprite promotedArt = CreateSprite(1920, 1080);
        promoted.ConfigurarNoEditor(promoted.MapaId, promoted.NomeInterno,
            promoted.NomeExibido, promoted.Descricao, promoted.TemaId, promotedArt,
            promoted.CentroCamera, promoted.TamanhoOrtografico,
            promoted.Territorios.ToList(), promoted.Regioes.ToList(),
            promoted.Conexoes.ToList());
        DefinicaoMapa legacy = CreateMap("legacy", true, 1);

        Assert.That(WDMapActivePreviewContext.RequiresSceneSnapshot(promoted),
            Is.False);
        Assert.That(WDMapActivePreviewContext.RequiresSceneSnapshot(legacy),
            Is.True);
    }

    [Test]
    public void ResolvingPreviewContextDoesNotMutateMapData()
    {
        DefinicaoMapa map = CreateMap("map", true, 1);
        Vector2[] counters = map.Territorios.Select(item => item.posicaoContador).ToArray();
        int connections = map.Conexoes.Count;
        int regions = map.Regioes.Count;

        WDMapActivePreviewContext.Resolve(map);

        Assert.That(map.Territorios.Select(item => item.posicaoContador).ToArray(),
            Is.EqualTo(counters));
        Assert.That(map.Conexoes.Count, Is.EqualTo(connections));
        Assert.That(map.Regioes.Count, Is.EqualTo(regions));
    }

    // ============================================================
    // 04. LABELS DE REGIÃO: AUTO, OVERRIDE, RESET E OVERLAP
    // ============================================================

    [Test]
    public void RegionAnchorUsesCentralTerritoryInsteadOfEmptyAverage()
    {
        Vector3[] positions = { new Vector3(-10f, 0f), new Vector3(1f, 1f), new Vector3(10f, 0f) };
        Assert.That(WDMatchInformationalOverlayController.TryCalculateRegionAnchor(
            positions, out Vector3 anchor), Is.True);
        Assert.That(anchor, Is.EqualTo(new Vector3(1f, 1f)));
    }

    [Test]
    public void RegionLabelManualOffsetCanBeSetAndReset()
    {
        DefinicaoMapa map = CreateMap("map", true, 1);
        DefinicaoRegiaoMapa region = map.Regioes[0];
        Assert.That(region.possuiDeslocamentoLabelManual, Is.False);
        Assert.That(WarDominionMapEditorOperations.SetRegionLabelOffset(
            map, region, new Vector2(3f, -2f)), Is.True);
        Assert.That(region.possuiDeslocamentoLabelManual, Is.True);
        Assert.That(region.deslocamentoLabelManual, Is.EqualTo(new Vector2(3f, -2f)));
        Assert.That(WarDominionMapEditorOperations.ResetRegionLabelOffset(map, region), Is.True);
        Assert.That(region.possuiDeslocamentoLabelManual, Is.False);
        Assert.That(region.deslocamentoLabelManual, Is.EqualTo(Vector2.zero));
    }

    [Test]
    public void RegionLabelPlacementAvoidsAnExistingBadgeDeterministically()
    {
        var viewport = new Rect(-300f, -200f, 600f, 400f);
        var size = new Vector2(120f, 36f);
        var occupied = new[] { new Rect(-60f, -18f, 120f, 36f) };
        Vector2 result = WDMatchInformationalOverlayController.ResolveRegionLabelPosition(
            Vector2.zero, viewport, size, occupied);
        Assert.That(result, Is.Not.EqualTo(Vector2.zero));
        Assert.That(new Rect(result - size * 0.5f, size).Overlaps(occupied[0]), Is.False);
    }

    [TestCase("Assets/Mapas/Classic/Arte/WD_Map_ClassicMap_Background_16x9_v01.png")]
    [TestCase("Assets/Mapas/Pearl Harbor/Arte/WD_Map_PearlHarbor_Background_16x9_v01.png")]
    [TestCase("Assets/Mapas/Batalha do Riachuelo/Arte/WD_Map_Riachuelo_Background_16x9_v01.png")]
    [TestCase("Assets/Mapas/Peloponeso/Arte/WD_Map_Peloponeso_Background_16x9_v01.png")]
    [TestCase("Assets/Mapas/Dark World/Arte/WD_Map_DarkWorld_Background_16x9_v01.png")]
    public void WdBackgroundSpritesCoverTheFullTextureCanvas(string assetPath)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        Assert.That(texture, Is.Not.Null, assetPath);
        Assert.That(texture.width, Is.EqualTo(1920), assetPath);
        Assert.That(texture.height, Is.EqualTo(1080), assetPath);

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        Assert.That(importer, Is.Not.Null, assetPath);
        Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple), assetPath);

        var factories = new SpriteDataProviderFactories();
        factories.Init();
        ISpriteEditorDataProvider dataProvider =
            factories.GetSpriteEditorDataProviderFromObject(importer);
        Assert.That(dataProvider, Is.Not.Null, assetPath);
        dataProvider.InitSpriteEditorDataProvider();

        SpriteRect[] spriteRects = dataProvider.GetSpriteRects();
        Assert.That(spriteRects, Has.Length.EqualTo(1), assetPath);
        foreach (SpriteRect spriteRect in spriteRects)
        {
            Assert.That(spriteRect.rect.x, Is.EqualTo(0f).Within(0.01f), spriteRect.name);
            Assert.That(spriteRect.rect.y, Is.EqualTo(0f).Within(0.01f), spriteRect.name);
            Assert.That(spriteRect.rect.width,
                Is.EqualTo(texture.width).Within(0.01f), spriteRect.name);
            Assert.That(spriteRect.rect.height,
                Is.EqualTo(texture.height).Within(0.01f), spriteRect.name);
        }
    }

    // ============================================================
    // 05. RECALIBRAÇÃO 16:9: TRANSFORM, BUSCA, CONFIANÇA E ACEITE
    // ============================================================

    [Test]
    public void MigrationTransformAppliesScaleRotationAndOffsetDeterministically()
    {
        var transform = new WDMapMigrationTransform(new Vector2(2f, 3f),
            new Vector2(5f, -2f), 0f);
        Assert.That(transform.TransformPoint(new Vector2(4f, 2f)),
            Is.EqualTo(new Vector2(13f, 4f)));
    }

    [Test]
    public void MigrationConfidenceUsesStableThresholds()
    {
        Assert.That(WarDominionMapMigrationFitter.Classify(0.72f),
            Is.EqualTo(WDMapMigrationConfidence.High));
        Assert.That(WarDominionMapMigrationFitter.Classify(0.52f),
            Is.EqualTo(WDMapMigrationConfidence.Medium));
        Assert.That(WarDominionMapMigrationFitter.Classify(0.32f),
            Is.EqualTo(WDMapMigrationConfidence.Low));
        Assert.That(WarDominionMapMigrationFitter.Classify(0.31f),
            Is.EqualTo(WDMapMigrationConfidence.Unresolved));
    }

    [Test]
    public void MigrationFitFindsNearbyBoundaryWithoutLeavingSearchLimits()
    {
        bool[,] mask = CreateSquareMask(48, 48, 12, 12, 18);
        float[,] edges = CreateTransformedBoundaryEdges(mask, 72, 72, 8, -4);
        WDMapMigrationFitResult result = WarDominionMapMigrationFitter.Fit(
            "territory", "Territory", mask, edges,
            new WDMapMigrationTransform(Vector2.one, Vector2.zero));
        Assert.That(result.CandidateTransform.OffsetPixels.x, Is.InRange(-16f, 16f));
        Assert.That(result.CandidateTransform.OffsetPixels.y, Is.InRange(-16f, 16f));
        Assert.That(result.Score, Is.GreaterThan(0.7f));
    }

    [Test]
    public void MigrationFitRejectsAnEdgeMapWithoutLocalCorrespondence()
    {
        bool[,] mask = CreateSquareMask(48, 48, 12, 12, 18);
        var edges = new float[72, 72];
        WDMapMigrationFitResult result = WarDominionMapMigrationFitter.Fit(
            "territory", "Territory", mask, edges,
            new WDMapMigrationTransform(Vector2.one, Vector2.zero));
        Assert.That(result.Confidence, Is.EqualTo(WDMapMigrationConfidence.Unresolved));
        Assert.That(result.Score, Is.LessThan(0.32f));
    }

    [Test]
    public void MigrationBatchAcceptIncludesOnlyHighConfidence()
    {
        WDMapMigrationTransform transform = new WDMapMigrationTransform(
            Vector2.one, Vector2.zero);
        var high = new WDMapMigrationFitResult("high", "High", transform, transform,
            0.8f, WDMapMigrationConfidence.High, string.Empty);
        var medium = new WDMapMigrationFitResult("medium", "Medium", transform, transform,
            0.6f, WDMapMigrationConfidence.Medium, string.Empty);
        var analysis = new WDMapMigrationAnalysis(new[] { high, medium });
        Assert.That(analysis.AcceptAllHigh(), Is.EqualTo(1));
        Assert.That(high.Accepted, Is.True);
        Assert.That(medium.Accepted, Is.False);
    }

    [Test]
    public void MigrationResetRestoresOriginalReviewState()
    {
        WDMapMigrationTransform transform = new WDMapMigrationTransform(
            Vector2.one, Vector2.zero);
        var fitted = new WDMapMigrationTransform(new Vector2(1.03f, 0.97f),
            new Vector2(8f, -4f));
        var result = new WDMapMigrationFitResult("a", "A", transform, fitted,
            0.8f, WDMapMigrationConfidence.High, string.Empty) { Accepted = true };
        var analysis = new WDMapMigrationAnalysis(new[] { result });
        result.AdjustCandidate(new WDMapMigrationTransform(Vector2.one,
            new Vector2(50f, 30f)));
        Assert.That(result.Accepted, Is.False);
        Assert.That(result.Rejected, Is.False);
        result.ResetCandidate();
        analysis.Reset(result);
        Assert.That(result.Accepted, Is.False);
        Assert.That(result.Rejected, Is.False);
        Assert.That(result.CandidateTransform.Scale, Is.EqualTo(fitted.Scale));
        Assert.That(result.CandidateTransform.OffsetPixels,
            Is.EqualTo(fitted.OffsetPixels));
    }

    [Test]
    public void ArgentinaMigrationBoundaryIsClosedAndContiguous()
    {
        IReadOnlyList<Vector2> boundary =
            WarDominionMapMigrationFitter.GetPreviewBoundary("Argentina",
                new WDMapMigrationTransform(Vector2.one, Vector2.zero));

        Assert.That(boundary.Count, Is.GreaterThan(3));
        Assert.That(boundary[boundary.Count - 1], Is.EqualTo(boundary[0]));
        for (int index = 1; index < boundary.Count; index++)
            Assert.That(Vector2.Distance(boundary[index - 1], boundary[index]),
                Is.LessThanOrEqualTo(32f), $"Salto no segmento {index - 1} → {index}");
    }

    [Test]
    public void MigrationDragConvertsScreenDeltaAndPreservesScaleAndRotation()
    {
        Vector2 normal = WDMapMigrationDragMath.ScreenDeltaToProposalDelta(
            new Vector2(96f, 54f), new Vector2(960f, 540f),
            new Vector2(1920f, 1080f), 1f);
        Vector2 fine = WDMapMigrationDragMath.ScreenDeltaToProposalDelta(
            new Vector2(96f, 54f), new Vector2(960f, 540f),
            new Vector2(1920f, 1080f), 0.25f);
        var original = new WDMapMigrationTransform(new Vector2(1.1f, 0.9f),
            new Vector2(10f, 20f), 3f);
        WDMapMigrationTransform moved = WDMapMigrationDragMath.TranslateOffset(
            original, original.OffsetPixels + normal);

        Assert.That(normal, Is.EqualTo(new Vector2(192f, 108f)));
        Assert.That(fine, Is.EqualTo(new Vector2(48f, 27f)));
        Assert.That(moved.Scale, Is.EqualTo(original.Scale));
        Assert.That(moved.RotationDegrees, Is.EqualTo(original.RotationDegrees));
        Assert.That(moved.OffsetPixels, Is.EqualTo(new Vector2(202f, 128f)));
    }

    [Test]
    public void MigrationDragHitTestUsesPolygonInsteadOfBoundingBox()
    {
        var concave = new List<Vector2>
        {
            new Vector2(0f, 0f), new Vector2(10f, 0f),
            new Vector2(10f, 4f), new Vector2(4f, 4f),
            new Vector2(4f, 10f), new Vector2(0f, 10f),
            new Vector2(0f, 0f)
        };
        var transform = new WDMapMigrationTransform(Vector2.one,
            new Vector2(20f, 30f));

        Assert.That(WDMapMigrationDragMath.ContainsTransformedPoint(concave,
            transform, new Vector2(22f, 38f)), Is.True);
        Assert.That(WDMapMigrationDragMath.ContainsTransformedPoint(concave,
            transform, new Vector2(28f, 38f)), Is.False);
    }

    [Test]
    public void MigrationCalibrationPersistsByMapAndTerritoryWithoutMutatingResult()
    {
        WDMapMigrationCalibrationStore source =
            ScriptableObject.CreateInstance<WDMapMigrationCalibrationStore>();
        WDMapMigrationCalibrationStore reloaded =
            ScriptableObject.CreateInstance<WDMapMigrationCalibrationStore>();
        disposable.Add(source);
        disposable.Add(reloaded);
        var original = new WDMapMigrationTransform(Vector2.one,
            new Vector2(10f, 20f));
        var candidate = new WDMapMigrationTransform(new Vector2(1.1f, 0.9f),
            new Vector2(30f, 40f), 2f);
        var result = new WDMapMigrationFitResult("argentina", "Argentina", original,
            candidate, 0.8f, WDMapMigrationConfidence.High, string.Empty)
            { Accepted = true };

        source.Upsert("classic", result.TerritoryId, result.CandidateTransform,
            result.InitialCandidateTransform);
        EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(source), reloaded);

        Assert.That(reloaded.TryGet("classic", "argentina", out var record), Is.True);
        Assert.That(record.CandidateTransform.OffsetPixels,
            Is.EqualTo(candidate.OffsetPixels));
        Assert.That(record.state,
            Is.EqualTo(WDMapMigrationPersistenceState.SavedForMigration));
        Assert.That(result.Accepted, Is.True, "Salvar não substitui aprovação humana.");
        Assert.That(result.CandidateTransform.OffsetPixels,
            Is.EqualTo(candidate.OffsetPixels), "Persistência não muta a proposta.");
    }

    [Test]
    public void MigrationBatchIncludesOnlyApprovedCalibrationsNotAlreadySaved()
    {
        WDMapMigrationCalibrationStore store =
            ScriptableObject.CreateInstance<WDMapMigrationCalibrationStore>();
        disposable.Add(store);
        var transform = new WDMapMigrationTransform(Vector2.one, Vector2.zero);
        var saved = new WDMapMigrationFitResult("saved", "Saved", transform, transform,
            0.8f, WDMapMigrationConfidence.High, string.Empty) { Accepted = true };
        var approved = new WDMapMigrationFitResult("approved", "Approved", transform,
            transform, 0.8f, WDMapMigrationConfidence.High, string.Empty)
            { Accepted = true };
        var pending = new WDMapMigrationFitResult("pending", "Pending", transform,
            transform, 0.8f, WDMapMigrationConfidence.High, string.Empty);
        store.Upsert("classic", saved.TerritoryId, saved.CandidateTransform,
            saved.InitialCandidateTransform);

        IReadOnlyList<WDMapMigrationFitResult> batch =
            WDMapMigrationCalibrationRepository.SelectApprovedUnsaved(store, "classic",
                new[] { saved, approved, pending });

        Assert.That(batch.Select(item => item.TerritoryId).ToArray(),
            Is.EqualTo(new[] { "approved" }));
    }

    [Test]
    public void MigrationPreviewLighteningPreservesDarkLineAgainstLiftedInterior()
    {
        Color32 interior = WDMapMigrationPreviewLightening.LightenPixel(
            new Color32(0, 0, 0, 255), 0.25f);
        Color32 line = WDMapMigrationPreviewLightening.LightenPixel(
            new Color32(15, 15, 15, 255), 0.25f);
        Color32 ocean = new Color32(10, 70, 100, 255);

        Assert.That(interior.r, Is.GreaterThan(line.r));
        Assert.That(line, Is.EqualTo(new Color32(15, 15, 15, 255)));
        Assert.That(WDMapMigrationPreviewLightening.LightenPixel(ocean, 0.25f),
            Is.EqualTo(ocean));
    }

    [Test]
    public void MigrationPreviewLighteningRespondsMonotonicallyToTuningLevels()
    {
        var territory = new Color32(0, 0, 0, 255);
        var line = new Color32(15, 15, 15, 255);

        Color32 off = WDMapMigrationPreviewLightening.LightenPixel(territory, 0f);
        Color32 at25 = WDMapMigrationPreviewLightening.LightenPixel(territory, 0.25f);
        Color32 at50 = WDMapMigrationPreviewLightening.LightenPixel(territory, 0.5f);
        Color32 at75 = WDMapMigrationPreviewLightening.LightenPixel(territory, 0.75f);

        Assert.That(off.r, Is.EqualTo(0));
        Assert.That(at25.r, Is.GreaterThan(off.r));
        Assert.That(at50.r, Is.GreaterThan(at25.r));
        Assert.That(at75.r, Is.GreaterThan(at50.r));
        Assert.That(WDMapMigrationPreviewLightening.LightenPixel(line, 0.75f),
            Is.EqualTo(line));
    }

    [Test]
    public void MigrationProgressSeparatesSavedApprovedReviewAndPending()
    {
        WDMapMigrationCalibrationStore store =
            ScriptableObject.CreateInstance<WDMapMigrationCalibrationStore>();
        disposable.Add(store);
        var transform = new WDMapMigrationTransform(Vector2.one, Vector2.zero);
        var saved = new WDMapMigrationFitResult("saved", "Saved", transform,
            transform, 0.8f, WDMapMigrationConfidence.High, string.Empty);
        var approved = new WDMapMigrationFitResult("approved", "Approved", transform,
            transform, 0.8f, WDMapMigrationConfidence.High, string.Empty)
            { Accepted = true };
        var review = new WDMapMigrationFitResult("review", "Review", transform,
            transform, 0.5f, WDMapMigrationConfidence.Medium, string.Empty);
        var pending = new WDMapMigrationFitResult("pending", "Pending", transform,
            transform, 0.2f, WDMapMigrationConfidence.Low, string.Empty);
        store.Upsert("classic", saved.TerritoryId, saved.CandidateTransform,
            saved.InitialCandidateTransform);

        WDMapMigrationProgress progress = WDMapMigrationProgress.Calculate("classic",
            new[] { saved, approved, review, pending }, store,
            new HashSet<string> { review.TerritoryId });

        Assert.That(progress.Saved, Is.EqualTo(1));
        Assert.That(progress.ApprovedUnsaved, Is.EqualTo(1));
        Assert.That(progress.Review, Is.EqualTo(1));
        Assert.That(progress.Pending, Is.EqualTo(1));
    }

    [Test]
    public void TerritoryMaskSeparatorKeepsClosedRegionsIndependent()
    {
        Color32[] pixels = CreateMaskPixels(7, 3, (x, _) => x != 3);
        WDMaskAuditResult result = WDMapTerritoryMaskAuditor.Analyze(pixels, 7, 3, 2);

        Assert.That(result.Components, Has.Count.EqualTo(2));
    }

    [Test]
    public void TerritoryMaskGapProducesDetectableMerge()
    {
        Color32[] pixels = CreateMaskPixels(7, 3, (x, y) => x != 3 || y == 1);
        WDMaskAuditResult result = WDMapTerritoryMaskAuditor.Analyze(pixels, 7, 3, 2);

        Assert.That(result.Components, Has.Count.EqualTo(1));
        Assert.That(result.PotentialMerge, Is.True);
    }

    [Test]
    public void TerritoryMaskReportsSmallComponentsWithoutJoiningThem()
    {
        Color32[] pixels = CreateMaskPixels(30, 30, (x, y) =>
            (x < 20 && y < 20) || (x == 29 && y == 29));
        WDMaskAuditResult result = WDMapTerritoryMaskAuditor.Analyze(pixels, 30, 30, 1);

        Assert.That(result.Components, Has.Count.EqualTo(2));
        Assert.That(result.SmallComponents, Is.EqualTo(2));
    }

    [Test]
    public void TerritoryMaskMultipartGeometryRemainsSeparateAndDeterministic()
    {
        Color32[] pixels = CreateMaskPixels(60, 20, (x, y) =>
            (x < 25 && y < 20) || (x >= 35 && y < 20));

        WDMaskAuditResult first = WDMapTerritoryMaskAuditor.Analyze(pixels, 60, 20, 1);
        WDMaskAuditResult second = WDMapTerritoryMaskAuditor.Analyze(pixels, 60, 20, 1);

        Assert.That(first.Components, Has.Count.EqualTo(2));
        Assert.That(second.Components.Select(item => item.Bounds).ToArray(),
            Is.EqualTo(first.Components.Select(item => item.Bounds).ToArray()));
    }

    [Test]
    public void TerritoryMaskWrongResolutionIsRejected()
    {
        Color32[] pixels = CreateMaskPixels(20, 20, (_, __) => true);
        WDMaskAuditResult result = WDMapTerritoryMaskAuditor.Analyze(pixels, 20, 20, 1);

        Assert.That(result.ResolutionValid, Is.False);
        Assert.That(result.Classification, Is.EqualTo(WDMaskAuditClassification.Red));
    }

    [Test]
    public void TerritoryMaskAlphaValidationIgnoresBackgroundButRejectsTransparentFill()
    {
        Color32[] valid = CreateMaskPixels(30, 30, (x, y) => x < 25 && y < 25);
        valid[valid.Length - 1] = new Color32(2, 4, 8, 0);
        Color32[] invalid = (Color32[])valid.Clone();
        invalid[0] = new Color32(241, 226, 197, 128);

        WDMaskAuditResult validResult = WDMapTerritoryMaskAuditor.Analyze(valid,
            30, 30, 1);
        WDMaskAuditResult invalidResult = WDMapTerritoryMaskAuditor.Analyze(invalid,
            30, 30, 1);

        Assert.That(validResult.AlphaConsistent, Is.True);
        Assert.That(invalidResult.AlphaConsistent, Is.False);
    }

    // ============================================================
    // 06. BOUNDARIES MASK AUTO: EXTRAÇÃO, ASSOCIAÇÃO E PERSISTÊNCIA
    // ============================================================

    [Test]
    public void MaskAutoContourIsOrderedClosedAndPreservesConcavity()
    {
        Color32[] pixels = CreateMaskPixels(20, 20, (x, y) =>
            x >= 2 && y >= 2 && ((x <= 6 && y <= 16) || (x <= 16 && y <= 6)));
        WDMapMaskRegion region = WDMapTerritoryBoundaryGenerator.Extract(pixels,
            20, 20).Single();

        Assert.That(region.Boundary.Count, Is.GreaterThan(4));
        Assert.That(region.Boundary[region.Boundary.Count - 1],
            Is.EqualTo(region.Boundary[0]));
        Assert.That(HasSelfIntersection(region.Boundary), Is.False);
        Assert.That(WDMapTerritoryBoundaryGenerator.PointInPolygon(region.Boundary,
            new Vector2(4f, 6f)), Is.True);
        Assert.That(WDMapTerritoryBoundaryGenerator.PointInPolygon(region.Boundary,
            new Vector2(14f, 6f)), Is.False, "A concavidade não pode ser preenchida.");
    }

    [Test]
    public void MaskAutoSimplificationIsDeterministic()
    {
        Color32[] pixels = CreateMaskPixels(30, 30, (x, y) =>
            x >= 3 && x <= 25 && y >= 4 && y <= 24 && !(x > 15 && y > 14));
        Vector2[] first = WDMapTerritoryBoundaryGenerator.Extract(pixels, 30, 30)
            .Single().Boundary.ToArray();
        Vector2[] second = WDMapTerritoryBoundaryGenerator.Extract(pixels, 30, 30)
            .Single().Boundary.ToArray();

        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void MaskAutoSupportsMultipleComponentsForOneTerritoryId()
    {
        Color32[] pixels = CreateMaskPixels(60, 20, (x, y) =>
            (x >= 2 && x <= 21 && y >= 2 && y <= 17) ||
            (x >= 38 && x <= 57 && y >= 2 && y <= 17));
        IReadOnlyList<WDMapMaskRegion> regions =
            WDMapTerritoryBoundaryGenerator.Extract(pixels, 60, 20);
        var anchors = new Dictionary<string, Vector2> { ["islands"] = new Vector2(12f, 10f) };
        var semanticGuide = new Dictionary<string, IReadOnlyList<Vector2>>
        {
            ["islands"] = regions.Select(item => item.Centroid).ToArray()
        };
        IReadOnlyList<WDMapBoundaryAssociation> associations =
            WDMapTerritoryBoundaryGenerator.AssociateWithSemanticGuide(regions, anchors,
                new Vector2(60f, 20f), semanticGuide);

        Assert.That(associations.Count(item => item.TerritoryId == "islands"),
            Is.EqualTo(2));
        Assert.That(associations.Select(item => item.Region.Index).Distinct().Count(),
            Is.EqualTo(2));
    }

    [Test]
    public void MaskAutoDoesNotAttachUnspecifiedExcessComponentToNearestTerritory()
    {
        Color32[] pixels = CreateMaskPixels(60, 20, (x, y) =>
            (x >= 2 && x <= 21 && y >= 2 && y <= 17) ||
            (x >= 38 && x <= 57 && y >= 2 && y <= 17));
        IReadOnlyList<WDMapMaskRegion> regions =
            WDMapTerritoryBoundaryGenerator.Extract(pixels, 60, 20);
        var anchors = new Dictionary<string, Vector2> { ["single"] = new Vector2(12f, 10f) };

        IReadOnlyList<WDMapBoundaryAssociation> associations =
            WDMapTerritoryBoundaryGenerator.Associate(regions, anchors,
                new Vector2(60f, 20f));

        Assert.That(associations.Count, Is.EqualTo(1));
        Assert.That(associations[0].TerritoryId, Is.EqualTo("single"));
    }

    [Test]
    public void MaskAutoGlobalAssignmentMaximizesWholeBijection()
    {
        var scores = new float[,]
        {
            { 9f, 8f },
            { 8f, 0f }
        };

        int[] assignment = WDMapTerritoryBoundaryGenerator.SolveMaximumAssignment(
            scores, out float total);

        Assert.That(assignment, Is.EqualTo(new[] { 1, 0 }));
        Assert.That(total, Is.EqualTo(16f));
        Assert.That(assignment.Distinct().Count(), Is.EqualTo(2));
    }

    [Test]
    public void MaskAutoHistoricalOverlapOutweighsCentroidSignal()
    {
        float overlapping = WDMapTerritoryBoundaryGenerator.ComposeCompatibilityScore(
            0.2f, false, 1f, 0, 0);
        float centroidOnly = WDMapTerritoryBoundaryGenerator.ComposeCompatibilityScore(
            0f, true, 0f, 0, 0);

        Assert.That(overlapping, Is.GreaterThan(centroidOnly));
    }

    [Test]
    public void MaskAutoTopologyBreaksOtherwiseEqualGeometry()
    {
        float withoutNeighbour =
            WDMapTerritoryBoundaryGenerator.ComposeCompatibilityScore(
                0f, false, 0.1f, 0, 0);
        float withNeighbour = WDMapTerritoryBoundaryGenerator.ComposeCompatibilityScore(
            0f, false, 0.1f, 1, 0);

        Assert.That(withNeighbour, Is.GreaterThan(withoutNeighbour));
    }

    [Test]
    public void MaskAutoTrueGlobalTieRemainsAmbiguous()
    {
        Assert.That(WDMapTerritoryBoundaryGenerator.ClassifyGlobalEvidence(
            0f, 0.8f, 4), Is.EqualTo(WDMapBoundaryConfidence.Ambiguous));
    }

    [Test]
    public void MaskAutoHumanAssignmentsResolveOnlyExplicitPermutation()
    {
        Color32[] pixels = CreateMaskPixels(80, 20, (x, y) =>
            (x >= 2 && x <= 17 && y >= 2 && y <= 17) ||
            (x >= 22 && x <= 37 && y >= 2 && y <= 17) ||
            (x >= 42 && x <= 57 && y >= 2 && y <= 17));
        WDMapMaskRegion[] regions = WDMapTerritoryBoundaryGenerator.Extract(pixels,
            80, 20).ToArray();
        var associations = new[]
        {
            new WDMapBoundaryAssociation("a", regions[0],
                WDMapBoundaryConfidence.Ambiguous, 0f),
            new WDMapBoundaryAssociation("b", regions[1],
                WDMapBoundaryConfidence.Ambiguous, 0f),
            new WDMapBoundaryAssociation("untouched", regions[2],
                WDMapBoundaryConfidence.High, 3f)
        };

        IReadOnlyList<WDMapBoundaryAssociation> resolved =
            WDMapTerritoryBoundaryGenerator.ApplyHumanAssignments(associations,
                new Dictionary<string, int>
                {
                    ["a"] = regions[1].Index,
                    ["b"] = regions[0].Index
                });

        Assert.That(resolved.Single(item => item.TerritoryId == "a").Region.Index,
            Is.EqualTo(regions[1].Index));
        Assert.That(resolved.Single(item => item.TerritoryId == "b").Region.Index,
            Is.EqualTo(regions[0].Index));
        Assert.That(resolved.Single(item => item.TerritoryId == "untouched").Region,
            Is.SameAs(regions[2]));
        Assert.That(resolved.Count(item => item.Confidence ==
            WDMapBoundaryConfidence.Ambiguous), Is.Zero);
    }

    [Test]
    public void MaskAutoHumanAssignmentsPreserveCoexistingMultipartTerritories()
    {
        Color32[] pixels = CreateMaskPixels(130, 20, (x, y) =>
            Enumerable.Range(0, 6).Any(index => x >= 2 + index * 21 &&
                x <= 17 + index * 21 && y >= 2 && y <= 17));
        WDMapMaskRegion[] regions = WDMapTerritoryBoundaryGenerator.Extract(pixels,
            130, 20).ToArray();
        var associations = new[]
        {
            new WDMapBoundaryAssociation("Indonesia", regions[0],
                WDMapBoundaryConfidence.High, 3f),
            new WDMapBoundaryAssociation("Indonesia", regions[1],
                WDMapBoundaryConfidence.High, 3f),
            new WDMapBoundaryAssociation("UnitedKingdom", regions[2],
                WDMapBoundaryConfidence.High, 3f),
            new WDMapBoundaryAssociation("UnitedKingdom", regions[3],
                WDMapBoundaryConfidence.High, 3f),
            new WDMapBoundaryAssociation("a", regions[4],
                WDMapBoundaryConfidence.Ambiguous, 0f),
            new WDMapBoundaryAssociation("b", regions[5],
                WDMapBoundaryConfidence.Ambiguous, 0f)
        };

        IReadOnlyList<WDMapBoundaryAssociation> resolved =
            WDMapTerritoryBoundaryGenerator.ApplyHumanAssignments(associations,
                new Dictionary<string, int>
                {
                    ["a"] = regions[5].Index,
                    ["b"] = regions[4].Index
                });

        Assert.That(resolved.Count, Is.EqualTo(6));
        Assert.That(resolved.Select(item => item.TerritoryId).Distinct().Count(),
            Is.EqualTo(4));
        Assert.That(resolved.Count(item => item.TerritoryId == "Indonesia"),
            Is.EqualTo(2));
        Assert.That(resolved.Count(item => item.TerritoryId == "UnitedKingdom"),
            Is.EqualTo(2));
        Assert.That(resolved.Select(item => item.Region.Index).Distinct().Count(),
            Is.EqualTo(6));
    }

    [Test]
    public void MaskAutoFiltersOnePixelNoise()
    {
        Color32[] pixels = CreateMaskPixels(40, 40, (x, y) =>
            (x < 30 && y < 30) || (x == 39 && y == 39));
        IReadOnlyList<WDMapMaskRegion> regions =
            WDMapTerritoryBoundaryGenerator.Extract(pixels, 40, 40);

        Assert.That(regions.Count, Is.EqualTo(2));
        Assert.That(regions.Count(item =>
            WDMapTerritoryBoundaryGenerator.IsValidTerritoryComponent("pearl_harbor", item)),
            Is.EqualTo(1));
    }

    [Test]
    public void MaskAutoPeloponneseFilterRejectsSparseDecorButKeepsCompactTerritory()
    {
        Color32[] pixels = CreateMaskPixels(220, 160, (x, y) =>
            (x >= 5 && x < 45 && y >= 5 && y < 45) ||
            (x >= 80 && x < 200 && y >= 90 && y < 94) ||
            (x >= 80 && x < 84 && y >= 90 && y < 150));
        WDMapMaskRegion[] regions = WDMapTerritoryBoundaryGenerator.Extract(pixels,
            220, 160).ToArray();
        WDMapMaskRegion compact = regions.Single(item => item.Density > 0.9f);
        WDMapMaskRegion sparse = regions.Single(item => item.Density < 0.3f);

        Assert.That(WDMapTerritoryBoundaryGenerator.IsValidTerritoryComponent(
            "peloponeso", compact), Is.True);
        Assert.That(WDMapTerritoryBoundaryGenerator.IsValidTerritoryComponent(
            "peloponeso", sparse), Is.False);
    }

    [Test]
    public void MaskAutoContainmentBeatsCloserCentroid()
    {
        Color32[] pixels = CreateMaskPixels(100, 30, (x, y) =>
            (x >= 2 && x <= 60 && y >= 2 && y <= 27 && !(x > 20 && x < 55 && y > 8 && y < 22)) ||
            (x >= 70 && x <= 90 && y >= 5 && y <= 25));
        IReadOnlyList<WDMapMaskRegion> regions =
            WDMapTerritoryBoundaryGenerator.Extract(pixels, 100, 30);
        var anchors = new Dictionary<string, Vector2> { ["a"] = new Vector2(8f, 15f) };
        WDMapBoundaryAssociation association = WDMapTerritoryBoundaryGenerator.Associate(
            regions, anchors, new Vector2(100f, 30f)).First();

        Assert.That(WDMapTerritoryBoundaryGenerator.PointInPolygon(
            association.Region.Boundary, anchors["a"]), Is.True);
    }

    [Test]
    public void MaskAutoAmbiguityIsNotResolvedArbitrarily()
    {
        Color32[] pixels = CreateMaskPixels(80, 20, (x, y) =>
            (x >= 5 && x < 25 && y >= 2 && y < 18) ||
            (x >= 55 && x < 75 && y >= 2 && y < 18));
        IReadOnlyList<WDMapMaskRegion> regions =
            WDMapTerritoryBoundaryGenerator.Extract(pixels, 80, 20);
        var anchors = new Dictionary<string, Vector2> { ["center"] = new Vector2(40f, 10f) };
        WDMapBoundaryAssociation association = WDMapTerritoryBoundaryGenerator.Associate(
            regions, anchors, new Vector2(80f, 20f)).Single();

        Assert.That(association.Confidence, Is.EqualTo(WDMapBoundaryConfidence.Ambiguous));
    }

    [Test]
    public void MaskAutoBoundaryStorePersistsMultiplePathsAndMetadata()
    {
        WDMapMigrationBoundaryStore source =
            ScriptableObject.CreateInstance<WDMapMigrationBoundaryStore>();
        WDMapMigrationBoundaryStore reloaded =
            ScriptableObject.CreateInstance<WDMapMigrationBoundaryStore>();
        disposable.Add(source); disposable.Add(reloaded);
        source.ReplaceMap("map", new[] { new WDMapMigrationBoundaryRecord
        {
            mapId = "map", territoryId = "islands", origin = "MASK_AUTO",
            confidence = WDMapBoundaryConfidence.High,
            reviewState = WDMapBoundaryReviewState.AutoNotReviewed,
            maskHash = "abc", algorithmVersion = 1,
            paths = new List<WDMapMigrationBoundaryPath>
            {
                new WDMapMigrationBoundaryPath { points = new List<Vector2>
                    { Vector2.zero, Vector2.right, Vector2.zero } },
                new WDMapMigrationBoundaryPath { points = new List<Vector2>
                    { Vector2.up, Vector2.one, Vector2.up } }
            }
        }});
        EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(source), reloaded);

        Assert.That(reloaded.TryGet("map", "islands", out var record), Is.True);
        Assert.That(record.paths, Has.Count.EqualTo(2));
        Assert.That(record.maskHash, Is.EqualTo("abc"));
        Assert.That(record.origin, Is.EqualTo("MASK_AUTO"));
    }

    [Test]
    public void MaskAutoBoundaryStoreReplacementIsAtomicWhenInputEnumerationFails()
    {
        WDMapMigrationBoundaryStore store =
            ScriptableObject.CreateInstance<WDMapMigrationBoundaryStore>();
        disposable.Add(store);
        store.ReplaceMap("classic", new[] { BoundaryRecord("classic", "original") });

        Assert.Throws<System.InvalidOperationException>(() =>
            store.ReplaceMap("classic", FailingBoundaryRecords()));

        Assert.That(store.ForMap("classic").Single().territoryId,
            Is.EqualTo("original"));
    }

    [Test]
    public void MaskAutoReplaceMapDoesNotTouchOtherMapsOrRuntimeObjects()
    {
        WDMapMigrationBoundaryStore store =
            ScriptableObject.CreateInstance<WDMapMigrationBoundaryStore>();
        disposable.Add(store);
        store.ReplaceMap("first", new[] { BoundaryRecord("first", "a") });
        store.ReplaceMap("second", new[] { BoundaryRecord("second", "b") });
        store.ReplaceMap("first", new[] { BoundaryRecord("first", "c") });

        Assert.That(store.ForMap("first").Single().territoryId, Is.EqualTo("c"));
        Assert.That(store.ForMap("second").Single().territoryId, Is.EqualTo("b"));
        Assert.That(typeof(WDMapMigrationBoundaryStore).Assembly.GetName().Name,
            Is.EqualTo("Assembly-CSharp-Editor"));
    }

    [Test]
    public void MaskAutoExtractionUsesStableComponentOrdering()
    {
        Color32[] pixels = CreateMaskPixels(50, 50, (x, y) =>
            (x < 20 && y < 20) || (x >= 30 && y >= 30));
        RectInt[] first = WDMapTerritoryBoundaryGenerator.Extract(pixels, 50, 50)
            .Select(item => item.Bounds).ToArray();
        RectInt[] second = WDMapTerritoryBoundaryGenerator.Extract(pixels, 50, 50)
            .Select(item => item.Bounds).ToArray();

        Assert.That(second, Is.EqualTo(first));
    }

    [TearDown]
    public void TearDown()
    {
        foreach (Object item in disposable)
            if (item != null) Object.DestroyImmediate(item);
        disposable.Clear();
    }

    private DefinicaoMapa CreateMap(string id, bool withArt, int bonus)
    {
        DefinicaoMapa map = ScriptableObject.CreateInstance<DefinicaoMapa>();
        disposable.Add(map);
        Sprite art = withArt ? CreateSprite() : null;
        var territories = new List<DefinicaoTerritorioMapa>
        {
            new DefinicaoTerritorioMapa { id = "a", nomeExibido = "A", regiaoId = "r" },
            new DefinicaoTerritorioMapa { id = "b", nomeExibido = "B", regiaoId = "r" }
        };
        var regions = new List<DefinicaoRegiaoMapa>
        {
            new DefinicaoRegiaoMapa
            {
                id = "r", nomeExibido = "Region", bonus = bonus,
                territorioIds = new List<string> { "a", "b" }
            }
        };
        map.ConfigurarNoEditor(id, id, id, string.Empty, "default", art,
            Vector2.zero, 5f, territories, regions, new List<DefinicaoConexaoMapa>());
        return map;
    }

    private DefinicaoMapa CreateMapWithSecondRegion()
    {
        DefinicaoMapa map = CreateMap("map", true, 1);
        var regions = map.Regioes.ToList();
        regions.Add(new DefinicaoRegiaoMapa
        {
            id = "second", nomeExibido = "Second", bonus = 1,
            territorioIds = new List<string>()
        });
        map.ConfigurarNoEditor(map.MapaId, map.NomeInterno, map.NomeExibido,
            map.Descricao, map.TemaId, map.ArteBase, map.CentroCamera,
            map.TamanhoOrtografico, map.Territorios.ToList(), regions,
            map.Conexoes.ToList());
        return map;
    }

    private Sprite CreateSprite()
    {
        Texture2D texture = new Texture2D(4, 4);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4),
            new Vector2(0.5f, 0.5f), 1f);
        disposable.Add(sprite);
        disposable.Add(texture);
        return sprite;
    }

    private Sprite CreateSprite(int width, int height)
    {
        Texture2D texture = new Texture2D(width, height);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f), 100f);
        disposable.Add(sprite);
        disposable.Add(texture);
        return sprite;
    }

    private static bool[,] CreateSquareMask(int width, int height, int x, int y, int size)
    {
        var mask = new bool[height, width];
        for (int row = y; row < y + size; row++)
        for (int column = x; column < x + size; column++)
            mask[row, column] = true;
        return mask;
    }

    private static float[,] CreateTransformedBoundaryEdges(bool[,] mask, int width,
        int height, int offsetX, int offsetY)
    {
        var edges = new float[height, width];
        for (int y = 1; y < mask.GetLength(0) - 1; y++)
        for (int x = 1; x < mask.GetLength(1) - 1; x++)
            if (mask[y, x] && (!mask[y - 1, x] || !mask[y + 1, x] ||
                !mask[y, x - 1] || !mask[y, x + 1]))
                edges[y + offsetY, x + offsetX] = 1f;
        return edges;
    }

    private static Color32[] CreateMaskPixels(int width, int height,
        System.Func<int, int, bool> isTerritory)
    {
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            pixels[y * width + x] = isTerritory(x, y)
                ? WDMapTerritoryMaskAuditor.CanonicalFill
                : new Color32(0, 0, 0, 255);
        return pixels;
    }

    private static WDMapMigrationBoundaryRecord BoundaryRecord(string mapId,
        string territoryId) => new WDMapMigrationBoundaryRecord
        { mapId = mapId, territoryId = territoryId };

    private static IEnumerable<WDMapMigrationBoundaryRecord> FailingBoundaryRecords()
    {
        yield return BoundaryRecord("classic", "partial");
        throw new System.InvalidOperationException(
            "Falha sintética durante materialização.");
    }

    private static bool HasSelfIntersection(IReadOnlyList<Vector2> polygon)
    {
        int segments = polygon.Count - 1;
        for (int first = 0; first < segments; first++)
        for (int second = first + 1; second < segments; second++)
        {
            if (second == first + 1 || (first == 0 && second == segments - 1))
                continue;
            if (SegmentsIntersect(polygon[first], polygon[first + 1], polygon[second],
                    polygon[second + 1])) return true;
        }
        return false;
    }

    private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        float abC = Cross2(b - a, c - a);
        float abD = Cross2(b - a, d - a);
        float cdA = Cross2(d - c, a - c);
        float cdB = Cross2(d - c, b - c);
        return abC * abD < 0f && cdA * cdB < 0f;
    }

    private static float Cross2(Vector2 first, Vector2 second) =>
        first.x * second.y - first.y * second.x;
}
