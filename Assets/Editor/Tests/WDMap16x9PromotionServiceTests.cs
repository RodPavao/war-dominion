using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class WDMap16x9PromotionServiceTests
{
    [Test]
    public void ClassicValidAcceptsFortyTwoIdsFortyFourPathsAndCanonicalMultiparts()
    {
        string[] ids = Enumerable.Range(0, 38).Select(index => "T" + index)
            .Concat(new[] { "Indonesia", "UnitedKingdom", "WestUSA", "Japan" }).ToArray();
        WDMapMigrationBoundaryRecord[] records = ids.Select((id, index) =>
            Record(id, index * 3, id == "Indonesia" || id == "UnitedKingdom" ? 2 : 1)).ToArray();
        var report = Report();

        WDMap16x9PromotionPlanner.ValidateBoundaries(report, ids, records, 1920, 1080);

        Assert.That(report.TerritoryIds, Is.EqualTo(42));
        Assert.That(report.Paths, Is.EqualTo(44));
        Assert.That(report.MultipartTerritories, Is.EqualTo(2));
        Assert.That(report.Blockers, Is.Empty);
    }

    [Test]
    public void MissingTerritoryIdBlocksPromotion()
    {
        var report = Report(2, 1);
        WDMap16x9PromotionPlanner.ValidateBoundaries(report, new[] { "A", "B" },
            new[] { Record("A", 1) }, 1920, 1080);
        Assert.That(report.Blockers.Any(item => item.Contains("ausente: B")), Is.True);
    }

    [Test]
    public void UnknownTerritoryIdBlocksPromotion()
    {
        var report = Report(1, 1);
        WDMap16x9PromotionPlanner.ValidateBoundaries(report, new[] { "A" },
            new[] { Record("X", 1) }, 1920, 1080);
        Assert.That(report.Blockers.Any(item => item.Contains("desconhecido: X")), Is.True);
    }

    [Test]
    public void UnauthorizedMultipartBlocksPromotion()
    {
        var report = Report(1, 2);
        WDMap16x9PromotionPlanner.ValidateBoundaries(report, new[] { "A" },
            new[] { Record("A", 1, 2) }, 1920, 1080);
        Assert.That(report.Blockers.Any(item => item.Contains("Multipart inválido: A")),
            Is.True);
    }

    [Test]
    public void IndonesiaAndUnitedKingdomAcceptExactlyTwoPaths()
    {
        var report = Report(2, 4);
        WDMap16x9PromotionPlanner.ValidateBoundaries(report,
            new[] { "Indonesia", "UnitedKingdom" },
            new[] { Record("Indonesia", 1, 2), Record("UnitedKingdom", 3, 2) },
            1920, 1080);
        Assert.That(report.Blockers, Is.Empty);
    }

    [Test]
    public void DegeneratePathBlocksPromotion()
    {
        WDMapMigrationBoundaryRecord record = Record("A", 1);
        record.paths[0].points = new List<Vector2>
            { Vector2.zero, Vector2.right, new Vector2(2f, 0f) };
        var report = Report(1, 1);
        WDMap16x9PromotionPlanner.ValidateBoundaries(report, new[] { "A" },
            new[] { record }, 1920, 1080);
        Assert.That(report.Blockers.Any(item => item.Contains("Path degenerado")), Is.True);
    }

    [Test]
    public void CounterProjectionInsideBoundaryIsPreservable()
    {
        WDMapMigrationBoundaryRecord record = Record("A", 1);
        Vector2 projected = WDMap16x9PromotionPlanner.ProjectPixel(
            new Vector2(17.5f, 5f), new Vector2(50f, 50f),
            new Vector2(100f, 100f));
        Assert.That(projected, Is.EqualTo(new Vector2(35f, 10f)));
        Assert.That(WDMap16x9PromotionPlanner.Contains(record.paths, projected, out _),
            Is.True);
    }

    [Test]
    public void CounterOutsideBoundaryUsesInteriorFallback()
    {
        WDMapMigrationBoundaryRecord record = Record("A", 1);
        Vector2 fallback = WDMap16x9PromotionPlanner.FindInteriorPoint(record.paths,
            out int pathIndex);
        Assert.That(WDMap16x9PromotionPlanner.Contains(record.paths, fallback, out _),
            Is.True);
        Assert.That(pathIndex, Is.Zero);
    }

    [Test]
    public void PlanningDoesNotMutateBoundarySource()
    {
        WDMapMigrationBoundaryRecord record = Record("A", 1);
        Vector2[] before = record.paths[0].points.ToArray();
        var report = Report(1, 1);
        WDMap16x9PromotionPlanner.ValidateBoundaries(report, new[] { "A" },
            new[] { record }, 1920, 1080);
        CollectionAssert.AreEqual(before, record.paths[0].points);
    }

    [Test]
    public void LogicalSnapshotHashIsDeterministicAndOrderSensitive()
    {
        string first = WDMap16x9PromotionPlanner.StableHash(new[] { "A", "B" });
        Assert.That(WDMap16x9PromotionPlanner.StableHash(new[] { "A", "B" }),
            Is.EqualTo(first));
        Assert.That(WDMap16x9PromotionPlanner.StableHash(new[] { "B", "A" }),
            Is.Not.EqualTo(first));
    }

    [Test]
    public void CameraPlanContainsFullCanvasWithoutDistortion()
    {
        Assert.That(WDMap16x9PromotionPlanner.OrthographicSizeToContain(
            new Vector2(19.2f, 10.8f), 16f / 9f), Is.EqualTo(5.4f).Within(0.0001f));
        Assert.That(WDMap16x9PromotionPlanner.OrthographicSizeToContain(
            new Vector2(19.2f, 10.8f), 4f / 3f), Is.EqualTo(7.2f).Within(0.0001f));
    }

    [Test]
    public void MarkdownExplicitlyStatesThatNoPromotionWasApplied()
    {
        var report = Report();
        report.Status = WDMap16x9PromotionStatus.Ready;
        string markdown = WDMap16x9PromotionService.ToMarkdown(report);
        StringAssert.Contains("Nenhuma promoção foi aplicada", markdown);
        StringAssert.Contains("READY FOR PROMOTION", markdown);
    }

    [Test]
    public void ClassicDryRunDoesNotModifySceneDefinitionBoundaryStoreOrBackground()
    {
        string[] paths =
        {
            WDMap16x9PromotionService.ClassicScenePath,
            WDMap16x9PromotionService.ClassicMapAssetPath,
            WDMapMigrationBoundaryRepository.AssetPath,
            WDMap16x9PromotionService.ClassicTargetArtPath
        };
        string[] before = paths.Select(path => path.ReadAllTextSafeHash()).ToArray();

        WDMap16x9PromotionReport report =
            WDMap16x9PromotionService.AnalyzeClassic(false);

        string[] after = paths.Select(path => path.ReadAllTextSafeHash()).ToArray();
        CollectionAssert.AreEqual(before, after);
        Assert.That(report.SourceSceneIsDirtyAfterAnalysis,
            Is.EqualTo(report.SourceSceneWasDirty));
    }

    [Test]
    public void PromotionRequiresReadyDryRunAndValidBackup()
    {
        var report = Report();
        report.Status = WDMap16x9PromotionStatus.Ready;
        Assert.That(WDMap16x9PromotionApplier.CanApply(report, true), Is.True);
        Assert.That(WDMap16x9PromotionApplier.CanApply(report, false), Is.False);
        report.Status = WDMap16x9PromotionStatus.Blocked;
        Assert.That(WDMap16x9PromotionApplier.CanApply(report, true), Is.False);
    }

    [Test]
    public void PromotionBackupMatchesAuthorizedPrePromotionHashes()
    {
        Assert.That(WDMap16x9PromotionApplier.BackupIsValid(out string reason),
            Is.True, reason);
    }

    [Test]
    public void BoundaryTopLeftCoordinatesConvertToUnityBottomLeftAndRoundTrip()
    {
        Vector2 boundary = new Vector2(641f, 217f);
        Vector2 unity = WDMap16x9PromotionPlanner.BoundaryToUnityPixel(boundary, 1080f);
        Assert.That(unity, Is.EqualTo(new Vector2(641f, 863f)));
        Assert.That(WDMap16x9PromotionPlanner.UnityToBoundaryPixel(unity, 1080f),
            Is.EqualTo(boundary));
    }

    [Test]
    public void AlaskaInteriorPointSurvivesBoundarySpriteLocalRoundTrip()
    {
        WDMapMigrationBoundaryStore store = WDMapMigrationBoundaryRepository.Load();
        Assert.That(store.TryGet("classic", "Alaska", out var alaska), Is.True);
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(
            WDMap16x9PromotionService.ClassicTargetArtPath).OfType<Sprite>().First();
        Vector2 interior = WDMap16x9PromotionPlanner.FindInteriorPoint(alaska.paths,
            out _);
        Vector2 local = WDMap16x9PromotionPlanner.BoundaryPixelToSpriteLocal(
            interior, sprite);
        Vector2 roundTrip = WDMap16x9PromotionPlanner.SpriteLocalToBoundaryPixel(
            local, sprite);
        Assert.That(roundTrip.x, Is.EqualTo(interior.x).Within(0.001f));
        Assert.That(roundTrip.y, Is.EqualTo(interior.y).Within(0.001f));
        Assert.That(WDMap16x9PromotionPlanner.Contains(alaska.paths, roundTrip, out _),
            Is.True);
    }

    [Test]
    public void FullCanvasMapUsesWholeCameraViewport()
    {
        Assert.That(LayoutPartida.CalcularViewportMapa(true, 0.15f, 0.70f),
            Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
        Assert.That(LayoutPartida.CalcularViewportMapa(false, 0.15f, 0.70f),
            Is.EqualTo(new Rect(0.15f, 0f, 0.70f, 1f)));
    }

    [Test]
    public void PromotedTerritoryUsesWhiteMaskAndSolidGameplayTint()
    {
        Color32 inside = WDMap16x9PromotionPlanner.BuildTerritoryOverlayPixel(true);
        Color32 outside = WDMap16x9PromotionPlanner.BuildTerritoryOverlayPixel(false);
        Color tint = TerritorioVisual.CorTerritorialParaRepresentacao(Color.black, true);

        Assert.That(inside, Is.EqualTo(new Color32(255, 255, 255, 255)));
        Assert.That(outside.a, Is.Zero);
        Assert.That(tint.a, Is.EqualTo(TerritorioVisual.OpacidadeOverlayMapaIntegral));
        Assert.That(tint.a, Is.EqualTo(1f));
    }

    [Test]
    public void RuntimeCounterProjectionMatchesArtLocalEditorConvention()
    {
        var art = new GameObject("Art");
        var territory = new GameObject("Territory");
        try
        {
            art.transform.SetPositionAndRotation(new Vector3(3f, -2f, 0f),
                Quaternion.Euler(0f, 0f, 17f));
            art.transform.localScale = new Vector3(1.2f, 0.8f, 1f);
            territory.transform.SetPositionAndRotation(new Vector3(-4f, 5f, 0f),
                Quaternion.Euler(0f, 0f, -9f));
            territory.transform.localScale = new Vector3(0.7f, 1.1f, 1f);
            Vector2 savedArtLocal = new Vector2(2.25f, -1.75f);

            Vector2 territoryLocal = MapaAtivo.ConverterPosicaoArteParaLocalTerritorio(
                art.transform, territory.transform, savedArtLocal);
            Vector3 resultingWorld = territory.transform.TransformPoint(territoryLocal);
            Vector3 expectedWorld = art.transform.TransformPoint(savedArtLocal);

            Assert.That(resultingWorld.x, Is.EqualTo(expectedWorld.x).Within(0.0001f));
            Assert.That(resultingWorld.y, Is.EqualTo(expectedWorld.y).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(art);
            UnityEngine.Object.DestroyImmediate(territory);
        }
    }

    [Test]
    public void RuntimeTerritoryNameOverlayCreatesRenderableLabelsFromRealTerritories()
    {
        var host = new GameObject("OverlayHost");
        var territoryObject = new GameObject("Territory");
        var cameraObject = new GameObject("Camera");
        int previous = PlayerPrefs.GetInt(MapaAtivo.ChaveMostrarNomesTerritorios, 0);
        try
        {
            PlayerPrefs.SetInt(MapaAtivo.ChaveMostrarNomesTerritorios, 1);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            TerritorioClique territory = territoryObject.AddComponent<TerritorioClique>();
            territory.idTerritorio = "Alaska";
            territoryObject.transform.position = new Vector3(1f, -1f, 0f);
            BoxCollider2D collider = territoryObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(2f, 1f);
            SpriteRenderer territoryRenderer =
                territoryObject.AddComponent<SpriteRenderer>();
            territoryRenderer.sortingOrder = 7;
            WDTerritoryNameRuntimeOverlay overlay =
                host.AddComponent<WDTerritoryNameRuntimeOverlay>();

            overlay.Rebuild(new[] { territory }, item => "Alaska", camera, 10f);
            TextMeshPro label = overlay.Labels["Alaska"];
            Material privateMaterial = label.fontSharedMaterial;
            MeshRenderer renderer = label.GetComponent<MeshRenderer>();
            Vector3 viewport = camera.WorldToViewportPoint(label.transform.position);

            Assert.That(label.text, Is.EqualTo("Alaska"));
            Assert.That(label.color, Is.EqualTo(Color.white));
            Assert.That(label.fontStyle & FontStyles.Bold, Is.EqualTo(FontStyles.Bold));
            Assert.That(privateMaterial, Is.Not.Null);
            Assert.That(privateMaterial.GetColor(ShaderUtilities.ID_OutlineColor),
                Is.EqualTo(Color.black));
            Assert.That(privateMaterial.GetFloat(ShaderUtilities.ID_OutlineWidth),
                Is.EqualTo(WDTerritoryNameRuntimeOverlay.OutlineWidth)
                    .Within(0.0001f));
            Assert.That(overlay.LabelCount, Is.EqualTo(1));
            Assert.That(overlay.RootActive, Is.True);
            Assert.That(label.gameObject.activeSelf, Is.True);
            Assert.That(label.gameObject.activeInHierarchy, Is.True);
            Assert.That(label.enabled, Is.True);
            Assert.That(label.alpha, Is.GreaterThan(0f));
            Assert.That(label.fontSize,
                Is.EqualTo(WDTerritoryNameRuntimeOverlay.FontSize));
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.enabled, Is.True);
            Assert.That(label.gameObject.layer,
                Is.EqualTo(territoryObject.layer));
            Assert.That(renderer.sortingLayerID,
                Is.EqualTo(territoryRenderer.sortingLayerID));
            Assert.That(renderer.sortingOrder, Is.EqualTo(
                territoryRenderer.sortingOrder + WDTerritoryNameRuntimeOverlay.SortingOffset));
            Assert.That(label.transform.position.x,
                Is.EqualTo(collider.bounds.center.x).Within(0.0001f));
            Assert.That(label.transform.position.y,
                Is.EqualTo(collider.bounds.center.y).Within(0.0001f));
            Assert.That(label.transform.position.z,
                Is.LessThan(territoryObject.transform.position.z));
            Assert.That(label.transform.localScale,
                Is.EqualTo(Vector3.one * WDTerritoryNameRuntimeOverlay.WorldScale));
            Assert.That((camera.cullingMask & (1 << label.gameObject.layer)) != 0,
                Is.True);
            Assert.That(viewport.z, Is.GreaterThan(0f));
            Assert.That(viewport.x, Is.InRange(0f, 1f));
            Assert.That(viewport.y, Is.InRange(0f, 1f));
            Assert.That(label.textInfo.characterCount, Is.GreaterThan(0));
        }
        finally
        {
            PlayerPrefs.SetInt(MapaAtivo.ChaveMostrarNomesTerritorios, previous);
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(territoryObject);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void RuntimeTerritoryNamePreferenceIntegrationTogglesWithoutRecreatingLabels()
    {
        var host = new GameObject("OverlayHost");
        var territoryObject = new GameObject("Territory");
        var cameraObject = new GameObject("Camera");
        int previous = PlayerPrefs.GetInt(MapaAtivo.ChaveMostrarNomesTerritorios, 0);
        try
        {
            MapaAtivo.DefinirNomesTerritoriaisHabilitados(true);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7.2f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            TerritorioClique territory = territoryObject.AddComponent<TerritorioClique>();
            territory.idTerritorio = "Alaska";
            territoryObject.AddComponent<BoxCollider2D>();
            WDTerritoryNameRuntimeOverlay overlay =
                host.AddComponent<WDTerritoryNameRuntimeOverlay>();
            overlay.Rebuild(new[] { territory, territory }, item => "Alaska",
                camera, 10f);
            TextMeshPro label = overlay.Labels["Alaska"];
            EntityId labelId = label.GetEntityId();

            Assert.That(overlay.LabelCount, Is.EqualTo(1));
            Assert.That(overlay.RootActive, Is.True);

            camera.orthographicSize = 7.21f;
            overlay.RefreshVisibility();
            Assert.That(overlay.RootActive, Is.False);
            Assert.That(overlay.Labels["Alaska"].GetEntityId(), Is.EqualTo(labelId));

            camera.orthographicSize = 5f;
            MapaAtivo.DefinirNomesTerritoriaisHabilitados(false);
            overlay.RefreshVisibility();
            Assert.That(overlay.RootActive, Is.False);
            Assert.That(overlay.Labels["Alaska"].GetEntityId(), Is.EqualTo(labelId));

            MapaAtivo.DefinirNomesTerritoriaisHabilitados(true);
            overlay.RefreshVisibility();
            Assert.That(overlay.RootActive, Is.True);
            Assert.That(overlay.Labels["Alaska"].GetEntityId(), Is.EqualTo(labelId));
        }
        finally
        {
            PlayerPrefs.SetInt(MapaAtivo.ChaveMostrarNomesTerritorios, previous);
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(territoryObject);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void RuntimeTerritoryNameOverlayRecoversLayoutCameraAfterEarlyRebuild()
    {
        var host = new GameObject("OverlayHost");
        var territoryObject = new GameObject("Territory");
        var layoutObject = new GameObject("LayoutAuthority");
        var cameraObject = new GameObject("RuntimeMapCamera");
        LayoutPartida previousLayout = LayoutPartida.instance;
        int previousPreference = PlayerPrefs.GetInt(
            MapaAtivo.ChaveMostrarNomesTerritorios, 0);
        try
        {
            layoutObject.SetActive(false);
            LayoutPartida layout = layoutObject.AddComponent<LayoutPartida>();
            LayoutPartida.instance = layout;
            MapaAtivo.DefinirNomesTerritoriaisHabilitados(true);

            TerritorioClique territory =
                territoryObject.AddComponent<TerritorioClique>();
            territory.idTerritorio = "Alaska";
            territoryObject.AddComponent<BoxCollider2D>();
            WDTerritoryNameRuntimeOverlay overlay =
                host.AddComponent<WDTerritoryNameRuntimeOverlay>();
            overlay.Rebuild(new[] { territory }, item => "Alaska", null, 5.4f);
            TextMeshPro label = overlay.Labels["Alaska"];
            EntityId labelId = label.GetEntityId();

            Assert.That(LayoutPartida.CameraRuntime, Is.Null);
            Assert.That(overlay.RootActive, Is.False);

            Camera runtimeCamera = cameraObject.AddComponent<Camera>();
            runtimeCamera.orthographic = true;
            runtimeCamera.orthographicSize = 3.5f;
            typeof(LayoutPartida).GetField("cameraPrincipal",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(layout, runtimeCamera);

            overlay.RefreshVisibility();

            Assert.That(LayoutPartida.CameraRuntime, Is.SameAs(runtimeCamera));
            Assert.That(overlay.RootActive, Is.True);
            Assert.That(overlay.LabelCount, Is.EqualTo(1));
            Assert.That(overlay.Labels["Alaska"].GetEntityId(),
                Is.EqualTo(labelId));
        }
        finally
        {
            LayoutPartida.instance = previousLayout;
            PlayerPrefs.SetInt(MapaAtivo.ChaveMostrarNomesTerritorios,
                previousPreference);
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(territoryObject);
            UnityEngine.Object.DestroyImmediate(layoutObject);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void PromotedManualTerritoryNameUsesArtLocalCoordinatesOnlyOnce()
    {
        var art = new GameObject("Art");
        var territory = new GameObject("Territory");
        try
        {
            art.transform.position = new Vector3(4f, -3f, 0f);
            territory.transform.position = new Vector3(-7f, 6f, 0f);
            Vector2 saved = new Vector2(2f, 1f);

            Vector3 result = MapaAtivo.ConverterPosicaoNomeManualParaMundo(
                art.transform, territory.transform, saved, true);

            Assert.That(result, Is.EqualTo(art.transform.TransformPoint(saved)));
            Assert.That(result, Is.Not.EqualTo(
                territory.transform.TransformPoint(saved)));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(art);
            UnityEngine.Object.DestroyImmediate(territory);
        }
    }

    [Test]
    public void RuntimeTerritoryNameManualOverrideFallsBackToColliderCenterWhenReset()
    {
        var territoryObject = new GameObject("Territory");
        try
        {
            territoryObject.transform.position = new Vector3(3f, 4f, 0f);
            TerritorioClique territory =
                territoryObject.AddComponent<TerritorioClique>();
            territory.idTerritorio = "Alaska";
            BoxCollider2D collider = territoryObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(4f, 2f);
            var definition = new DefinicaoTerritorioMapa
            {
                id = "Alaska",
                possuiPosicaoNomeManual = true,
                posicaoNomeManual = new Vector2(0.75f, -0.25f)
            };

            Vector3 manual = WDTerritoryNameRuntimeOverlay.ResolveWorldAnchor(
                territory, definition, null, false);
            Assert.That(manual.x, Is.EqualTo(3.75f).Within(0.0001f));
            Assert.That(manual.y, Is.EqualTo(3.75f).Within(0.0001f));

            definition.possuiPosicaoNomeManual = false;
            Vector3 automatic = WDTerritoryNameRuntimeOverlay.ResolveWorldAnchor(
                territory, definition, null, false);
            Assert.That(automatic.x,
                Is.EqualTo(collider.bounds.center.x).Within(0.0001f));
            Assert.That(automatic.y,
                Is.EqualTo(collider.bounds.center.y).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(territoryObject);
        }
    }

    [Test]
    public void PromotedManualTerritoryNameChoosesTheCoordinateSpaceInsideItsTerritory()
    {
        var art = new GameObject("Art");
        var territory = new GameObject("Territory");
        try
        {
            art.transform.position = new Vector3(20f, 20f, 0f);
            territory.transform.position = new Vector3(-4f, 3f, 0f);
            PolygonCollider2D collider = territory.AddComponent<PolygonCollider2D>();
            collider.points = new[]
            {
                new Vector2(-1f, -1f), new Vector2(1f, -1f),
                new Vector2(1f, 1f), new Vector2(-1f, 1f)
            };

            Vector3 result = MapaAtivo.ResolverPosicaoNomeManualParaMundo(
                art.transform, territory.transform, new Vector2(0.2f, -0.1f), true);

            Assert.That(result, Is.EqualTo(
                territory.transform.TransformPoint(new Vector2(0.2f, -0.1f))));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(art);
            UnityEngine.Object.DestroyImmediate(territory);
        }
    }

    [Test]
    public void TerritoryNameZoomUsesTheSharedVisibilityThreshold()
    {
        var cameraObject = new GameObject("Camera");
        DefinicaoMapa map = ScriptableObject.CreateInstance<DefinicaoMapa>();
        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            map.ConfigurarNoEditor("test", "test", "test", string.Empty,
                string.Empty, null, Vector2.zero, 10f,
                new List<DefinicaoTerritorioMapa>(),
                new List<DefinicaoRegiaoMapa>(),
                new List<DefinicaoConexaoMapa>());
            camera.orthographicSize = 7.2f;
            Assert.That(MapaAtivo.ZoomPermiteExibirNomes(camera, map), Is.True);
            camera.orthographicSize = 7.21f;
            Assert.That(MapaAtivo.ZoomPermiteExibirNomes(camera, map), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(map);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void TerritoryNameZoomUsesActualFittedCameraSizeAndWheelIsDeterministic()
    {
        var cameraObject = new GameObject("Camera");
        DefinicaoMapa map = ScriptableObject.CreateInstance<DefinicaoMapa>();
        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            map.ConfigurarNoEditor("test", "test", "test", string.Empty,
                string.Empty, null, Vector2.zero, 99f,
                new List<DefinicaoTerritorioMapa>(),
                new List<DefinicaoRegiaoMapa>(),
                new List<DefinicaoConexaoMapa>());

            const float fitted = 10f;
            camera.orthographicSize = 7.21f;
            Assert.That(MapaAtivo.ZoomPermiteExibirNomes(camera, map, fitted),
                Is.False);
            camera.orthographicSize = InputPartida.CalcularTamanhoZoom(
                camera.orthographicSize, 120f, fitted);
            Assert.That(camera.orthographicSize, Is.LessThan(7.21f));
            Assert.That(MapaAtivo.ZoomPermiteExibirNomes(camera, map, fitted),
                Is.True);
            Assert.That(InputPartida.CalcularTamanhoZoom(fitted, -120f, fitted),
                Is.EqualTo(fitted));
            float afterSixSteps = fitted;
            for (int step = 0; step < 6; step++)
                afterSixSteps = InputPartida.CalcularTamanhoZoom(
                    afterSixSteps, 120f, fitted);
            Assert.That(afterSixSteps,
                Is.EqualTo(fitted * Mathf.Exp(-6f *
                    InputPartida.SensibilidadeZoomPorPasso)).Within(0.0001f));
            Assert.That(afterSixSteps,
                Is.LessThanOrEqualTo(fitted *
                    MapaAtivo.ProporcaoZoomParaExibirNomes));
            Assert.That(InputPartida.CalcularTamanhoZoom(0.1f, 120f, fitted),
                Is.EqualTo(fitted * 0.25f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(map);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void RegionManualOffsetUsesArtVectorWithoutApplyingTranslation()
    {
        var art = new GameObject("Art");
        try
        {
            art.transform.SetPositionAndRotation(new Vector3(20f, -30f, 0f),
                Quaternion.Euler(0f, 0f, 90f));
            art.transform.localScale = new Vector3(2f, 3f, 1f);

            Vector3 result = MapaAtivo.ConverterDeslocamentoArteParaMundo(
                art.transform, new Vector2(1f, 2f));

            Assert.That(result.x, Is.EqualTo(-6f).Within(0.0001f));
            Assert.That(result.y, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(result, Is.Not.EqualTo(
                art.transform.TransformPoint(new Vector2(1f, 2f))));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(art);
        }
    }

    [Test]
    public void RegionRuntimeAndEditorUseSameArtLocalAnchorAndManualPosition()
    {
        Vector3[] countersArtLocal =
        {
            new Vector3(-2f, 1f), new Vector3(2f, 1f),
            new Vector3(0f, -2f), Vector3.zero
        };
        Vector2 manualOffset = new Vector2(0.4f, -0.25f);

        Assert.That(WDMatchInformationalOverlayController.TryCalculateRegionPosition(
            countersArtLocal, true, manualOffset, out Vector3 position), Is.True);
        Assert.That(position, Is.EqualTo(new Vector3(0.4f, -0.25f, 0f)));

        Rect viewport = new Rect(-500f, -300f, 1000f, 600f);
        Vector2 size = new Vector2(100f, 30f);
        var occupied = new[] { new Rect(position.x - 50f, position.y - 15f, 100f, 30f) };
        Vector2 manual = WDMatchInformationalOverlayController
            .ResolveRegionLabelPositionForMode(position, viewport, size,
                occupied, false);
        Vector2 automatic = WDMatchInformationalOverlayController
            .ResolveRegionLabelPositionForMode(position, viewport, size,
                occupied, true);

        Assert.That(manual, Is.EqualTo((Vector2)position));
        Assert.That(automatic, Is.Not.EqualTo((Vector2)position));
    }

    [TestCase(false, false, false)]
    [TestCase(false, true, true)]
    [TestCase(true, true, false)]
    public void EditorialPreviewWarningOnlyAppliesToDirectMapPlay(
        bool hasSetup, bool isMapScene, bool expected)
    {
        Assert.That(DefinicaoMapaClassicEditor.DeveAvisarPreviewEditorial(
            hasSetup, isMapScene), Is.EqualTo(expected));
    }

    [Test]
    public void RiachueloRegionCurationRemainsCanonicalAndBidirectional()
    {
        DefinicaoMapa map = AssetDatabase.LoadAssetAtPath<DefinicaoMapa>(
            "Assets/Resources/Mapas/Riachuelo.asset");
        var expected = new Dictionary<string, string[]>
        {
            ["entre_rios"] = new[] { "Feliciano", "Federal", "LaPaz", "Federacion", "Parana", "Villaguay", "Concordia" },
            ["corrientes"] = new[] { "Mercedes", "Goya", "Lavalle", "PasoDeLosLibres", "Sauce", "CuruzuCuatia", "Esquina", "MonteCaseros" },
            ["chaco"] = new[] { "PalomeraIsland", "OHiggins", "SanLorenzo", "Pilcomayo", "Formosa", "GeneralDiaz" },
            ["santa_fe"] = new[] { "GeneralSanMartin", "Pilagas", "PrimeroDeMayo", "Laishi", "SanFernando" },
            ["neembucu"] = new[] { "Villalbin", "Desmochados", "SantaFe", "SanCristobal", "NueveDeJulio", "Vera" },
            ["formosa"] = new[] { "Pirane", "SanJavier", "Humaita", "PasoDeLaPatria", "MayorMartinez" }
        };

        Assert.That(map, Is.Not.Null);
        Assert.That(map.Regioes.Count, Is.EqualTo(expected.Count));
        foreach (DefinicaoRegiaoMapa region in map.Regioes)
        {
            Assert.That(expected.ContainsKey(region.id), Is.True, region.id);
            Assert.That(region.territorioIds, Is.EqualTo(expected[region.id]), region.id);
            Assert.That(region.possuiDeslocamentoLabelManual, Is.True, region.id);
            Assert.That(float.IsFinite(region.deslocamentoLabelManual.x), Is.True,
                region.id);
            Assert.That(float.IsFinite(region.deslocamentoLabelManual.y), Is.True,
                region.id);
            Assert.That(region.deslocamentoLabelManual, Is.Not.EqualTo(Vector2.zero),
                region.id);
            foreach (string territoryId in region.territorioIds)
                Assert.That(map.Territorios.Single(item => item.id == territoryId).regiaoId,
                    Is.EqualTo(region.id), territoryId);
        }
        Assert.That(expected.Values.SelectMany(items => items).Distinct().Count(),
            Is.EqualTo(37));
    }

    [TestCase("Assets/Resources/Mapas/Classic.asset")]
    [TestCase("Assets/Resources/Mapas/PearlHarbor.asset")]
    [TestCase("Assets/Resources/Mapas/Riachuelo.asset")]
    [TestCase("Assets/Resources/Mapas/Peloponeso.asset")]
    [TestCase("Assets/Resources/Mapas/DarkWorld.asset")]
    public void NeutralTerritoryVisualInitializesSolidAndNeverKeepsSerializedBlack(
        string mapPath)
    {
        var territory = new GameObject("Territory");
        try
        {
            DefinicaoMapa map = AssetDatabase.LoadAssetAtPath<DefinicaoMapa>(mapPath);
            Assert.That(LayoutPartida.EhArteIntegral16x9(map), Is.True, mapPath);
            SpriteRenderer renderer = territory.AddComponent<SpriteRenderer>();
            renderer.color = Color.black;
            territory.AddComponent<TerritorioClique>();
            TerritorioVisual visual = territory.AddComponent<TerritorioVisual>();

            // AddComponent em um fixture EditMode não reproduz de forma
            // autoritativa o lifecycle Awake/Start do Play Mode. AtualizarCor é
            // a mesma autoridade final chamada por TerritorioClique.Start.
            visual.AtualizarCor();

            Assert.That(renderer.color.a, Is.EqualTo(1f));
            Assert.That(renderer.color, Is.Not.EqualTo(Color.black));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(territory);
        }
    }

    [Test]
    public void RegionMemberLookupLocatesCanonicalTerritoryWithoutMutatingMap()
    {
        DefinicaoMapa map = AssetDatabase.LoadAssetAtPath<DefinicaoMapa>(
            "Assets/Resources/Mapas/PearlHarbor.asset");
        int countBefore = map.Territorios.Count;

        int index = WarDominionMapEditorWindow.ResolveTerritoryIndex(
            map, "PhilipCollins");

        Assert.That(index, Is.GreaterThanOrEqualTo(0));
        Assert.That(map.Territorios[index].id, Is.EqualTo("PhilipCollins"));
        Assert.That(WarDominionMapEditorWindow.ResolveTerritoryIndex(
            map, "missing"), Is.EqualTo(-1));
        Assert.That(map.Territorios.Count, Is.EqualTo(countBefore));
    }

    [Test]
    public void PearlHarborUsesCanonicalPhilipCollinsIdentity()
    {
        DefinicaoMapa map = AssetDatabase.LoadAssetAtPath<DefinicaoMapa>(
            "Assets/Resources/Mapas/PearlHarbor.asset");
        DefinicaoTerritorioMapa territory = map.Territorios.Single(item =>
            item.id == "PhilipCollins");
        DefinicaoRegiaoMapa north = map.Regioes.Single(item =>
            item.id == "north_sector");
        DefinicaoRegiaoMapa right = map.Regioes.Single(item =>
            item.id == "right_sector");
        string[] neighbours = map.Conexoes.Where(item =>
                item.origemId == "PhilipCollins" ||
                item.destinoId == "PhilipCollins")
            .Select(item => item.origemId == "PhilipCollins"
                ? item.destinoId : item.origemId)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();

        Assert.That(map.Territorios.Count, Is.EqualTo(45));
        Assert.That(map.Territorios.Select(item => item.id),
            Has.None.EqualTo("NGay"));
        Assert.That(territory.nomeExibido, Is.EqualTo("Philip Collins"));
        DefinicaoTerritorioMapa anderson = map.Territorios.Single(item =>
            item.id == "AndersonBase");
        DefinicaoRegiaoMapa left = map.Regioes.Single(item =>
            item.id == "left_sector");
        Assert.That(territory.regiaoId, Is.EqualTo("right_sector"));
        Assert.That(right.territorioIds, Has.Member("PhilipCollins"));
        Assert.That(north.territorioIds, Has.None.EqualTo("PhilipCollins"));
        Assert.That(anderson.regiaoId, Is.EqualTo("north_sector"));
        Assert.That(north.territorioIds, Has.Member("AndersonBase"));
        Assert.That(left.territorioIds, Has.None.EqualTo("AndersonBase"));
        Assert.That(neighbours, Is.EqualTo(new[]
        {
            "AbrahamLincolnCoast", "Phoenix"
        }));
    }

    [TestCase("Assets/Resources/Mapas/Classic.asset", 42)]
    [TestCase("Assets/Resources/Mapas/PearlHarbor.asset", 45)]
    [TestCase("Assets/Resources/Mapas/Riachuelo.asset", 37)]
    [TestCase("Assets/Resources/Mapas/Peloponeso.asset", 82)]
    [TestCase("Assets/Resources/Mapas/DarkWorld.asset", 43)]
    public void PromotedMapRegionsReferenceExistingTerritoriesExactlyOnce(
        string assetPath, int territoryCount)
    {
        DefinicaoMapa map = AssetDatabase.LoadAssetAtPath<DefinicaoMapa>(assetPath);
        var ids = new HashSet<string>(map.Territorios.Select(item => item.id),
            StringComparer.Ordinal);

        Assert.That(ids.Count, Is.EqualTo(territoryCount));
        var referencedIds = new List<string>();
        foreach (DefinicaoRegiaoMapa region in map.Regioes)
        {
            Assert.That(region.territorioIds.All(id =>
                !string.IsNullOrWhiteSpace(id)), Is.True, region.id);
            Assert.That(region.territorioIds.Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(region.territorioIds.Count), region.id);
            Assert.That(region.territorioIds.All(ids.Contains), Is.True, region.id);
            referencedIds.AddRange(region.territorioIds);

            foreach (string territoryId in region.territorioIds)
            {
                DefinicaoTerritorioMapa territory = map.Territorios.Single(item =>
                    item.id == territoryId);
                Assert.That(territory.regiaoId, Is.EqualTo(region.id), territoryId);
            }
        }

        Assert.That(referencedIds.Count, Is.EqualTo(territoryCount));
        Assert.That(referencedIds.Distinct(StringComparer.Ordinal).Count(),
            Is.EqualTo(territoryCount));
    }

    [Test]
    public void TerritoryConnectionAnchorUsesCurrentPromotedColliderGeometry()
    {
        var territoryObject = new GameObject("Alaska");
        try
        {
            TerritorioClique territory = territoryObject.AddComponent<TerritorioClique>();
            PolygonCollider2D collider = territoryObject.AddComponent<PolygonCollider2D>();
            collider.SetPath(0, new[]
            {
                new Vector2(2f, 3f), new Vector2(4f, 3f),
                new Vector2(4f, 5f), new Vector2(2f, 5f)
            });
            var staleCounter = new GameObject("ContadorModerno");
            staleCounter.transform.SetParent(territoryObject.transform, false);
            staleCounter.transform.position = new Vector3(-20f, -20f, 0f);

            Vector3 anchor = MapOverlayProjection.GetTerritoryWorldAnchor(territory);

            Assert.That(anchor.x, Is.EqualTo(3f).Within(0.001f));
            Assert.That(anchor.y, Is.EqualTo(4f).Within(0.001f));
            Assert.That(anchor, Is.Not.EqualTo(staleCounter.transform.position));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(territoryObject);
        }
    }

    [Test]
    public void AlaskaAndArgentinaConnectionAnchorsBelongToTheirPromotedBoundaries()
    {
        Scene scene = EditorSceneManager.OpenScene(
            WDMap16x9PromotionService.ClassicScenePath, OpenSceneMode.Additive);
        try
        {
            DefinicaoMapa map = AssetDatabase.LoadAssetAtPath<DefinicaoMapa>(
                WDMap16x9PromotionService.ClassicMapAssetPath);
            SpriteRenderer art = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<SpriteRenderer>(true))
                .First(renderer => renderer.sprite == map.ArteBase);
            TerritorioClique[] territories = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TerritorioClique>(true))
                .ToArray();
            WDMapMigrationBoundaryStore store = WDMapMigrationBoundaryRepository.Load();

            foreach (string id in new[] { "Alaska", "Argentina" })
            {
                TerritorioClique territory = territories.Single(item =>
                    item.idTerritorio == id);
                Assert.That(store.TryGet("classic", id, out var record), Is.True);
                Vector3 worldAnchor = MapOverlayProjection.GetTerritoryWorldAnchor(territory);
                Vector2 spriteLocal = art.transform.InverseTransformPoint(worldAnchor);
                Vector2 boundaryPixel =
                    WDMap16x9PromotionPlanner.SpriteLocalToBoundaryPixel(
                        spriteLocal, map.ArteBase);
                Assert.That(WDMap16x9PromotionPlanner.Contains(
                    record.paths, boundaryPixel, out _), Is.True, id);
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void FullHdAndFourKUseEquivalentNormalizedFraming()
    {
        Vector2 mapSize = new Vector2(19.2f, 10.8f);
        float fullHd = LayoutPartida.TamanhoOrtograficoNormalizado(
            mapSize, 1920f / 1080f);
        float fourK = LayoutPartida.TamanhoOrtograficoNormalizado(
            mapSize, 3840f / 2160f);

        Assert.That(fullHd, Is.EqualTo(5.4f).Within(0.0001f));
        Assert.That(fourK, Is.EqualTo(fullHd).Within(0.0001f));
    }

    [Test]
    public void ClassicLogicalConnectionGraphRemainsCanonicalAfterPromotion()
    {
        WDMap16x9PromotionReport report =
            WDMap16x9PromotionService.AnalyzeClassic(false);

        Assert.That(report.TerritoryIds, Is.EqualTo(42));
        Assert.That(report.ConnectionCount, Is.EqualTo(78));
        Assert.That(report.Blockers.Where(item =>
            item.StartsWith("Conexão referencia ID inexistente:",
                StringComparison.Ordinal)), Is.Empty);
        Assert.That(report.ConnectionsHash, Is.EqualTo(
            "7F2EDF08B5FB26D8AE9F11297FD0C363127FC9ED5B18A916944EA0AC37236851"));
    }

    [TestCase("pearl_harbor", 45, 45)]
    [TestCase("riachuelo", 37, 37)]
    [TestCase("peloponeso", 82, 82)]
    [TestCase("dark_world", 43, 43)]
    public void RemainingPromotedMapsPassCanonicalStructuralAudit(string mapId,
        int territories, int paths)
    {
        WDMap16x9PromotionTarget target = WDMap16x9PromotionService.RemainingMaps
            .Single(item => item.MapId == mapId);

        WDMap16x9PromotionReport report = WDMap16x9PromotionService.Analyze(
            target, false);

        string[] structuralBlockers = report.Blockers.Where(item =>
            !string.Equals(item, "Dry-run alterou o dirty state da cena.",
                StringComparison.Ordinal)).ToArray();
        Assert.That(structuralBlockers, Is.Empty,
            mapId + ": " + string.Join(" | ", structuralBlockers));
        Assert.That(report.TerritoryIds, Is.EqualTo(territories));
        Assert.That(report.Paths, Is.EqualTo(paths));
        Assert.That(report.Ambiguities, Is.Zero);
        Assert.That(report.MultipartTerritories, Is.Zero);
        Assert.That(report.TerritoryClicks, Is.EqualTo(territories));
        Assert.That(report.Colliders, Is.EqualTo(territories));
        Assert.That(report.CurrentArtPath, Is.EqualTo(target.TargetArtPath));
        Assert.That(report.ProposedOrthographicSize, Is.EqualTo(5.4f).Within(0.0001f));
    }

    private static WDMap16x9PromotionReport Report(int ids = 42, int paths = 44) =>
        new WDMap16x9PromotionReport
        {
            ExpectedTerritoryIds = ids,
            ExpectedPaths = paths,
            MapId = "classic"
        };

    private static WDMapMigrationBoundaryRecord Record(string id, int component,
        int pathCount = 1)
    {
        var record = new WDMapMigrationBoundaryRecord
        {
            mapId = "classic", territoryId = id,
            confidence = WDMapBoundaryConfidence.High
        };
        for (int index = 0; index < pathCount; index++)
        {
            int componentId = component + index;
            float offsetX = (componentId % 30) * 30f;
            float offsetY = (componentId / 30) * 30f;
            record.paths.Add(new WDMapMigrationBoundaryPath
            {
                componentIndex = componentId,
                sourceBounds = new RectInt((int)offsetX, (int)offsetY, 20, 20),
                points = new List<Vector2>
                {
                    new Vector2(offsetX, offsetY), new Vector2(offsetX + 20f, offsetY),
                    new Vector2(offsetX + 20f, offsetY + 20f),
                    new Vector2(offsetX, offsetY + 20f)
                }
            });
        }
        return record;
    }
}

internal static class WDMap16x9TestFileHash
{
    public static string ReadAllTextSafeHash(this string assetPath)
    {
        string absolute = Path.GetFullPath(assetPath);
        byte[] bytes = File.ReadAllBytes(absolute);
        using (System.Security.Cryptography.SHA256 sha =
            System.Security.Cryptography.SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(bytes));
    }
}
