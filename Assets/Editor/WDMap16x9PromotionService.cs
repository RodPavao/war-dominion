using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum WDMap16x9PromotionStatus { Ready, Warning, Blocked }

public sealed class WDMap16x9PromotionTarget
{
    public string DisplayName;
    public string MapId;
    public string MapAssetPath;
    public string ScenePath;
    public string TargetArtPath;
    public string RuntimeDirectory;
    public int TerritoryCount;
    public int PathCount;
    public IReadOnlyDictionary<string, int> Multipart =
        new Dictionary<string, int>(StringComparer.Ordinal);
}

[Serializable]
public sealed class WDMap16x9TerritoryPlan
{
    public string TerritoryId;
    public string GameObjectName;
    public int CurrentPathCount;
    public int NewPathCount;
    public int[] ComponentIds = Array.Empty<int>();
    public Rect CurrentBoundsPixels;
    public Rect NewBoundsPixels;
    public Vector2 CurrentAnchorPixels;
    public Vector2 ProjectedAnchorPixels;
    public bool IsMultipart;
    public bool CounterExists;
    public Vector2 CounterCurrentPixels;
    public Vector2 CounterProjectedPixels;
    public bool CounterPreservable;
    public Vector2 CounterFallbackPixels;
    public int CounterFallbackPath;
    public string VisualStrategy;
    public bool Promotable;
    public string Blocker;
}

[Serializable]
public sealed class WDMap16x9PromotionReport
{
    public string Timestamp;
    public string Head;
    public string MapId;
    public string ScenePath;
    public string CurrentArtPath;
    public string TargetArtPath;
    public WDMap16x9PromotionStatus Status;
    public int ExpectedTerritoryIds;
    public int TerritoryIds;
    public int ExpectedPaths;
    public int Paths;
    public int Ambiguities;
    public int MultipartTerritories;
    public int TerritoryClicks;
    public int Colliders;
    public int CountersPreservable;
    public int CountersRepositioned;
    public Bounds TargetSpriteBounds;
    public float CurrentOrthographicSize;
    public float ProposedOrthographicSize;
    public Vector3 CurrentCameraPosition;
    public Vector3 ProposedCameraPosition;
    public int ConnectionCount;
    public int RegionCount;
    public string ConnectionsHash;
    public string RegionsHash;
    public bool SourceSceneWasDirty;
    public bool SourceSceneIsDirtyAfterAnalysis;
    public readonly List<string> Warnings = new List<string>();
    public readonly List<string> Blockers = new List<string>();
    public readonly List<WDMap16x9TerritoryPlan> Territories =
        new List<WDMap16x9TerritoryPlan>();

    public string StatusLabel => Status.ToString().ToUpperInvariant();
}

// ============================================================
// 1. PLANEJADOR PURO: NÃO CONHECE SCENE, ASSETDATABASE OU GUI
// ============================================================
public static class WDMap16x9PromotionPlanner
{
    public static Color32 BuildTerritoryOverlayPixel(bool inside) => inside
        ? new Color32(255, 255, 255, 255)
        : new Color32(0, 0, 0, 0);

    public const int ClassicTerritoryCount = 42;
    public const int ClassicPathCount = 44;
    public static readonly IReadOnlyDictionary<string, int> ClassicMultipart =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "Indonesia", 2 }, { "UnitedKingdom", 2 }
        };

    public static void ValidateBoundaries(WDMap16x9PromotionReport report,
        IEnumerable<string> officialIds,
        IEnumerable<WDMapMigrationBoundaryRecord> sourceRecords,
        int canvasWidth, int canvasHeight,
        IReadOnlyDictionary<string, int> multipart = null)
    {
        string[] expected = (officialIds ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id)).ToArray();
        WDMapMigrationBoundaryRecord[] records = (sourceRecords ??
            Array.Empty<WDMapMigrationBoundaryRecord>()).ToArray();
        report.TerritoryIds = records.Length;
        report.Paths = records.Sum(record => record.paths?.Count ?? 0);
        report.Ambiguities = records.Count(record =>
            record.confidence == WDMapBoundaryConfidence.Ambiguous);
        report.MultipartTerritories = records.Count(record =>
            (record.paths?.Count ?? 0) > 1);

        AddDuplicates(report, expected, "TerritoryId oficial duplicado");
        AddDuplicates(report, records.Select(record => record.territoryId),
            "TerritoryId duplicado no boundary store");

        var expectedSet = new HashSet<string>(expected, StringComparer.Ordinal);
        var actualSet = new HashSet<string>(records.Select(record => record.territoryId),
            StringComparer.Ordinal);
        foreach (string missing in expectedSet.Except(actualSet).OrderBy(id => id))
            report.Blockers.Add("TerritoryId ausente: " + missing);
        foreach (string unknown in actualSet.Except(expectedSet).OrderBy(id => id))
            report.Blockers.Add("TerritoryId desconhecido: " + unknown);

        var components = new HashSet<int>();
        foreach (WDMapMigrationBoundaryRecord record in records)
        {
            int pathCount = record.paths?.Count ?? 0;
            IReadOnlyDictionary<string, int> allowedMultipart =
                multipart ?? ClassicMultipart;
            int expectedPaths = allowedMultipart.TryGetValue(record.territoryId,
                out int allowed) ? allowed : 1;
            if (pathCount != expectedPaths)
                report.Blockers.Add($"Multipart inválido: {record.territoryId} " +
                    $"possui {pathCount}, esperado {expectedPaths}.");
            if (record.confidence == WDMapBoundaryConfidence.Ambiguous)
                report.Blockers.Add("Boundary ambíguo: " + record.territoryId);
            foreach (WDMapMigrationBoundaryPath path in record.paths ??
                new List<WDMapMigrationBoundaryPath>())
            {
                if (!components.Add(path.componentIndex))
                    report.Blockers.Add("ComponentId duplicado: " + path.componentIndex);
                ValidatePath(report, record.territoryId, path, canvasWidth, canvasHeight);
            }
        }
        if (report.TerritoryIds != report.ExpectedTerritoryIds)
            report.Blockers.Add($"TerritoryIds: {report.TerritoryIds}/" +
                report.ExpectedTerritoryIds);
        if (report.Paths != report.ExpectedPaths)
            report.Blockers.Add($"Paths: {report.Paths}/{report.ExpectedPaths}");
    }

    public static Vector2 ProjectPixel(Vector2 source, Vector2 sourceSize,
        Vector2 targetSize) => new Vector2(
        sourceSize.x <= 0f ? 0f : source.x * targetSize.x / sourceSize.x,
        sourceSize.y <= 0f ? 0f : source.y * targetSize.y / sourceSize.y);

    // BoundaryStore usa origem superior; Sprite/Texture2D usa origem inferior.
    public static Vector2 BoundaryToUnityPixel(Vector2 boundaryPixel,
        float canvasHeight) => new Vector2(boundaryPixel.x,
            canvasHeight - boundaryPixel.y);

    public static Vector2 UnityToBoundaryPixel(Vector2 unityPixel,
        float canvasHeight) => new Vector2(unityPixel.x,
            canvasHeight - unityPixel.y);

    public static Vector2 BoundaryPixelToSpriteLocal(Vector2 boundaryPixel,
        Sprite sprite) => (BoundaryToUnityPixel(boundaryPixel,
            sprite.rect.height) - sprite.pivot) / sprite.pixelsPerUnit;

    public static Vector2 SpriteLocalToBoundaryPixel(Vector2 spriteLocal,
        Sprite sprite) => UnityToBoundaryPixel(spriteLocal * sprite.pixelsPerUnit +
            sprite.pivot, sprite.rect.height);

    public static bool Contains(IReadOnlyList<WDMapMigrationBoundaryPath> paths,
        Vector2 point, out int pathIndex)
    {
        pathIndex = -1;
        for (int index = 0; index < (paths?.Count ?? 0); index++)
            if (PointInPolygon(paths[index].points, point))
            {
                pathIndex = index;
                return true;
            }
        return false;
    }

    public static Vector2 FindInteriorPoint(
        IReadOnlyList<WDMapMigrationBoundaryPath> paths, out int pathIndex)
    {
        pathIndex = -1;
        float bestArea = float.MinValue;
        for (int index = 0; index < (paths?.Count ?? 0); index++)
        {
            float area = Mathf.Abs(SignedArea(paths[index].points));
            if (area > bestArea) { bestArea = area; pathIndex = index; }
        }
        if (pathIndex < 0) return Vector2.zero;
        IReadOnlyList<Vector2> polygon = paths[pathIndex].points;
        Vector2 centroid = PolygonCentroid(polygon);
        if (PointInPolygon(polygon, centroid)) return centroid;
        Rect bounds = BoundsOf(polygon);
        Vector2 best = polygon[0];
        float bestDistance = -1f;
        for (int y = 1; y < 16; y++)
        for (int x = 1; x < 16; x++)
        {
            Vector2 candidate = new Vector2(
                Mathf.Lerp(bounds.xMin, bounds.xMax, x / 16f),
                Mathf.Lerp(bounds.yMin, bounds.yMax, y / 16f));
            if (!PointInPolygon(polygon, candidate)) continue;
            float distance = DistanceToEdges(polygon, candidate);
            if (distance > bestDistance) { bestDistance = distance; best = candidate; }
        }
        return best;
    }

    public static float OrthographicSizeToContain(Vector2 worldSize, float aspect)
    {
        if (aspect <= 0f) return worldSize.y * 0.5f;
        return Mathf.Max(worldSize.y * 0.5f, worldSize.x / (2f * aspect));
    }

    public static string StableHash(IEnumerable<string> values)
    {
        string text = string.Join("\n", values ?? Array.Empty<string>());
        using (SHA256 sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text)))
                .Replace("-", string.Empty);
    }

    private static void ValidatePath(WDMap16x9PromotionReport report,
        string territoryId, WDMapMigrationBoundaryPath path, int width, int height)
    {
        if (path == null || path.points == null || path.points.Count < 3 ||
            path.points.Distinct().Count() < 3 ||
            Mathf.Abs(SignedArea(path.points)) < 0.5f)
        {
            report.Blockers.Add("Path degenerado: " + territoryId);
            return;
        }
        if (path.points.Any(point => float.IsNaN(point.x) || float.IsInfinity(point.x) ||
            float.IsNaN(point.y) || float.IsInfinity(point.y) || point.x < 0f || point.y < 0f ||
            point.x > width || point.y > height))
            report.Blockers.Add("Path fora do canvas: " + territoryId);
    }

    private static void AddDuplicates(WDMap16x9PromotionReport report,
        IEnumerable<string> values, string label)
    {
        foreach (IGrouping<string, string> duplicate in values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value, StringComparer.Ordinal).Where(group => group.Count() > 1))
            report.Blockers.Add(label + ": " + duplicate.Key);
    }

    private static float SignedArea(IReadOnlyList<Vector2> polygon)
    {
        float twiceArea = 0f;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 first = polygon[i];
            Vector2 second = polygon[(i + 1) % polygon.Count];
            twiceArea += first.x * second.y - second.x * first.y;
        }
        return twiceArea * 0.5f;
    }

    private static Vector2 PolygonCentroid(IReadOnlyList<Vector2> polygon)
    {
        float area6 = 0f;
        Vector2 sum = Vector2.zero;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 first = polygon[i];
            Vector2 second = polygon[(i + 1) % polygon.Count];
            float cross = first.x * second.y - second.x * first.y;
            area6 += cross;
            sum += (first + second) * cross;
        }
        return Mathf.Abs(area6) < 0.0001f
            ? polygon.Aggregate(Vector2.zero, (total, value) => total + value) /
              polygon.Count
            : sum / (3f * area6);
    }

    private static bool PointInPolygon(IReadOnlyList<Vector2> polygon, Vector2 point)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[j];
            if ((a.y > point.y) != (b.y > point.y) &&
                point.x < (b.x - a.x) * (point.y - a.y) /
                (b.y - a.y) + a.x) inside = !inside;
        }
        return inside;
    }

    private static Rect BoundsOf(IReadOnlyList<Vector2> points)
    {
        float minX = points.Min(point => point.x);
        float maxX = points.Max(point => point.x);
        float minY = points.Min(point => point.y);
        float maxY = points.Max(point => point.y);
        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private static float DistanceToEdges(IReadOnlyList<Vector2> polygon, Vector2 point)
    {
        float minimum = float.MaxValue;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[(i + 1) % polygon.Count];
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude <= 0f ? 0f :
                Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude);
            minimum = Mathf.Min(minimum, Vector2.Distance(point, a + ab * t));
        }
        return minimum;
    }
}

// ============================================================
// 2. CAPTURA EDITOR-ONLY E ORQUESTRAÇÃO SOMENTE LEITURA
// ============================================================
public static class WDMap16x9PromotionService
{
    public const string ClassicMapAssetPath = "Assets/Resources/Mapas/Classic.asset";
    public const string ClassicScenePath = "Assets/WarGame.unity";
    public const string ClassicTargetArtPath =
        "Assets/Mapas/Classic/Arte/WD_Map_ClassicMap_Background_16x9_v01.png";
    public const string ReportPath = "Docs/Diagnostics/WD16x9_Classic_DryRun.md";

    public static readonly WDMap16x9PromotionTarget Classic = new WDMap16x9PromotionTarget
    {
        DisplayName = "Classic", MapId = "classic", MapAssetPath = ClassicMapAssetPath,
        ScenePath = ClassicScenePath, TargetArtPath = ClassicTargetArtPath,
        RuntimeDirectory = "Assets/Mapas/Classic/Runtime16x9", TerritoryCount = 42,
        PathCount = 44, Multipart = WDMap16x9PromotionPlanner.ClassicMultipart
    };

    public static readonly IReadOnlyList<WDMap16x9PromotionTarget> RemainingMaps =
        new[]
        {
            new WDMap16x9PromotionTarget
            {
                DisplayName = "Pearl Harbor", MapId = "pearl_harbor",
                MapAssetPath = "Assets/Resources/Mapas/PearlHarbor.asset",
                ScenePath = "Assets/Mapas/Pearl Harbor/PearlHarbor.unity",
                TargetArtPath = "Assets/Mapas/Pearl Harbor/Arte/WD_Map_PearlHarbor_Background_16x9_v01.png",
                RuntimeDirectory = "Assets/Mapas/Pearl Harbor/Runtime16x9",
                TerritoryCount = 45, PathCount = 45
            },
            new WDMap16x9PromotionTarget
            {
                DisplayName = "Riachuelo", MapId = "riachuelo",
                MapAssetPath = "Assets/Resources/Mapas/Riachuelo.asset",
                ScenePath = "Assets/Mapas/Batalha do Riachuelo/Riachuelo.unity",
                TargetArtPath = "Assets/Mapas/Batalha do Riachuelo/Arte/WD_Map_Riachuelo_Background_16x9_v01.png",
                RuntimeDirectory = "Assets/Mapas/Batalha do Riachuelo/Runtime16x9",
                TerritoryCount = 37, PathCount = 37
            },
            new WDMap16x9PromotionTarget
            {
                DisplayName = "Peloponeso", MapId = "peloponeso",
                MapAssetPath = "Assets/Resources/Mapas/Peloponeso.asset",
                ScenePath = "Assets/Mapas/Peloponeso/Peloponeso.unity",
                TargetArtPath = "Assets/Mapas/Peloponeso/Arte/WD_Map_Peloponeso_Background_16x9_v01.png",
                RuntimeDirectory = "Assets/Mapas/Peloponeso/Runtime16x9",
                TerritoryCount = 82, PathCount = 82
            },
            new WDMap16x9PromotionTarget
            {
                DisplayName = "Dark World", MapId = "dark_world",
                MapAssetPath = "Assets/Resources/Mapas/DarkWorld.asset",
                ScenePath = "Assets/Mapas/Dark World/DarkWorld.unity",
                TargetArtPath = "Assets/Mapas/Dark World/Arte/WD_Map_DarkWorld_Background_16x9_v01.png",
                RuntimeDirectory = "Assets/Mapas/Dark World/Runtime16x9",
                TerritoryCount = 43, PathCount = 43
            }
        };

    [MenuItem("War Dominion/Editor de Mapas/Analisar promoção 16:9 do Classic")]
    public static void AnalyzeClassicFromMenu()
    {
        WDMap16x9PromotionReport report = AnalyzeClassic(true);
        Debug.Log($"[WD 16:9 DRY-RUN] Classic: {report.StatusLabel} | " +
            $"TerritoryIds={report.TerritoryIds}/{report.ExpectedTerritoryIds} | " +
            $"paths={report.Paths}/{report.ExpectedPaths} | " +
            $"blockers={report.Blockers.Count} | cena alterada=NÃO | " +
            $"relatório={ReportPath}");
    }

    public static WDMap16x9PromotionReport AnalyzeClassic(bool exportReport)
        => Analyze(Classic, exportReport);

    public static WDMap16x9PromotionReport Analyze(WDMap16x9PromotionTarget target,
        bool exportReport)
    {
        var report = new WDMap16x9PromotionReport
        {
            Timestamp = DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture),
            Head = ReadHead(), MapId = target.MapId, ScenePath = target.ScenePath,
            TargetArtPath = target.TargetArtPath,
            ExpectedTerritoryIds = target.TerritoryCount,
            ExpectedPaths = target.PathCount
        };
        DefinicaoMapa map = AssetDatabase.LoadAssetAtPath<DefinicaoMapa>(target.MapAssetPath);
        WDMapMigrationBoundaryStore store = WDMapMigrationBoundaryRepository.Load();
        Sprite targetSprite = LoadSprite(target.TargetArtPath);
        if (map == null) report.Blockers.Add("DefinicaoMapa ausente: " + target.DisplayName);
        if (store == null) report.Blockers.Add("Boundary store ausente.");
        if (targetSprite == null) report.Blockers.Add("Background alvo ausente ou sem Sprite.");
        if (map == null || store == null || targetSprite == null)
            return Finish(report, exportReport);

        report.CurrentArtPath = AssetDatabase.GetAssetPath(map.ArteBase);
        if (!string.Equals(map.MapaId, target.MapId, StringComparison.Ordinal))
            report.Blockers.Add("DefinicaoMapa incompatível: mapaId=" + map.MapaId);
        string configuredScene = map.Metadados.FirstOrDefault(metadata =>
            metadata != null && metadata.chave == "scenePath")?.valor;
        if (!string.Equals(configuredScene, target.ScenePath, StringComparison.Ordinal))
            report.Blockers.Add("Referência de cena inesperada: " + configuredScene);
        if (targetSprite.texture == null || targetSprite.rect.width != 1920f ||
            targetSprite.rect.height != 1080f)
            report.Blockers.Add("Background alvo não materializa Sprite 1920x1080.");
        report.TargetSpriteBounds = targetSprite.bounds;

        WDMapMigrationBoundaryRecord[] records = store.ForMap(target.MapId).ToArray();
        WDMap16x9PromotionPlanner.ValidateBoundaries(report,
            map.Territorios.Select(territory => territory.id), records, 1920, 1080,
            target.Multipart);
        ValidateLogicalData(report, map);
        CaptureScenePlan(report, map, targetSprite, records);
        return Finish(report, exportReport);
    }

    public static string ToMarkdown(WDMap16x9PromotionReport report)
    {
        var text = new StringBuilder();
        text.AppendLine("# WD 16:9 — Promotion Dry-Run").AppendLine()
            .AppendLine("- Timestamp: " + report.Timestamp)
            .AppendLine("- HEAD: `" + report.Head + "`")
            .AppendLine("- Map: `" + report.MapId + "`")
            .AppendLine("- Scene: `" + report.ScenePath + "`")
            .AppendLine("- Current art: `" + report.CurrentArtPath + "`")
            .AppendLine("- Target art: `" + report.TargetArtPath + "`")
            .AppendLine("- Result: **" + (report.Status == WDMap16x9PromotionStatus.Ready
                ? "READY FOR PROMOTION" : report.StatusLabel) + "**").AppendLine()
            .AppendLine("## Structural validation").AppendLine()
            .AppendLine($"- TerritoryIds: {report.TerritoryIds}/{report.ExpectedTerritoryIds}")
            .AppendLine($"- Polygon paths: {report.Paths}/{report.ExpectedPaths}")
            .AppendLine($"- Ambiguities: {report.Ambiguities}")
            .AppendLine($"- Multipart territories: {report.MultipartTerritories}")
            .AppendLine($"- TerritorioClique: {report.TerritoryClicks}/{report.ExpectedTerritoryIds}")
            .AppendLine($"- Colliders: {report.Colliders}/{report.ExpectedTerritoryIds}")
            .AppendLine($"- Connections: {report.ConnectionCount} (`{report.ConnectionsHash}`)")
            .AppendLine($"- Regions: {report.RegionCount} (`{report.RegionsHash}`)")
            .AppendLine($"- Scene dirty before/after: {report.SourceSceneWasDirty}/" +
                report.SourceSceneIsDirtyAfterAnalysis).AppendLine()
            .AppendLine("## Camera plan").AppendLine()
            .AppendLine("- Target bounds: " + Format(report.TargetSpriteBounds))
            .AppendLine($"- Orthographic size: {report.CurrentOrthographicSize:0.####} → " +
                $"{report.ProposedOrthographicSize:0.####}")
            .AppendLine("- Position: " + Format(report.CurrentCameraPosition) + " → " +
                Format(report.ProposedCameraPosition))
            .AppendLine("- Aspect policy: `max(height/2, width/(2*aspect))`; no stretch or crop; HUD remains ScreenSpaceOverlay.")
            .AppendLine().AppendLine("## Territory plan").AppendLine()
            .AppendLine("| TerritoryId | GO | Paths old→new | Components | Bounds old | Bounds new | Counter | Fallback/path | Visual | Result |")
            .AppendLine("|---|---|---:|---|---|---|---|---|---|---|");
        foreach (WDMap16x9TerritoryPlan plan in report.Territories)
            text.AppendLine($"| {plan.TerritoryId} | {plan.GameObjectName} | " +
                $"{plan.CurrentPathCount}→{plan.NewPathCount} | " +
                $"{string.Join(",", plan.ComponentIds)} | {Format(plan.CurrentBoundsPixels)} | " +
                $"{Format(plan.NewBoundsPixels)} | " +
                $"{(plan.CounterPreservable ? "PRESERVABLE" : "REPOSITION")} " +
                $"{Format(plan.CounterProjectedPixels)} | {Format(plan.CounterFallbackPixels)}/" +
                $"{plan.CounterFallbackPath} | {plan.VisualStrategy} | " +
                $"{(plan.Promotable ? "PROMOTABLE" : "BLOCKED: " + plan.Blocker)} |");
        AppendList(text, "Warnings", report.Warnings);
        AppendList(text, "Blockers", report.Blockers);
        text.AppendLine().AppendLine("## Future visual strategy").AppendLine()
            .AppendLine("Generate an editor-time territorial visual asset from each canonical boundary and use the same paths as the future PolygonCollider2D. Do not deform or retain the legacy territorial sprite as promoted geometry.")
            .AppendLine().AppendLine("## Labels").AppendLine()
            .AppendLine("Manual overrides remain unchanged. Runtime region labels depend on territory transforms and require visual review after a future promotion.")
            .AppendLine().AppendLine("> Nenhuma promoção foi aplicada.");
        return text.ToString();
    }

    private static void CaptureScenePlan(WDMap16x9PromotionReport report,
        DefinicaoMapa map, Sprite target,
        IReadOnlyList<WDMapMigrationBoundaryRecord> records)
    {
        Scene scene = SceneManager.GetSceneByPath(report.ScenePath);
        bool openedTemporarily = !scene.IsValid() || !scene.isLoaded;
        if (openedTemporarily)
            scene = EditorSceneManager.OpenScene(report.ScenePath, OpenSceneMode.Additive);
        report.SourceSceneWasDirty = scene.isDirty;
        try
        {
            TerritorioClique[] territories = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TerritorioClique>(true))
                .ToArray();
            SpriteRenderer[] renderers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<SpriteRenderer>(true))
                .ToArray();
            SpriteRenderer art = renderers.FirstOrDefault(renderer =>
                renderer.sprite == map.ArteBase);
            if (art == null) report.Blockers.Add("SpriteRenderer da arte atual não encontrado.");
            AddDuplicateTerritories(report, territories);
            report.TerritoryClicks = territories.Length;
            report.Colliders = territories.Count(territory =>
                territory.GetComponent<PolygonCollider2D>() != null);
            if (territories.Length != report.ExpectedTerritoryIds)
                report.Blockers.Add($"TerritorioClique: {territories.Length}/" +
                    report.ExpectedTerritoryIds);
            if (report.Colliders != report.ExpectedTerritoryIds)
                report.Blockers.Add($"Colliders: {report.Colliders}/" +
                    report.ExpectedTerritoryIds);
            if (art != null)
                foreach (DefinicaoTerritorioMapa definition in map.Territorios)
                    BuildTerritoryPlan(report, definition,
                        territories.Where(item => item.idTerritorio == definition.id).ToArray(),
                        records.FirstOrDefault(item => item.territoryId == definition.id),
                        art, map.ArteBase, target);
            Camera camera = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .FirstOrDefault(candidate => candidate.orthographic);
            if (camera == null) report.Blockers.Add("Câmera ortográfica ausente.");
            else if (art != null)
            {
                report.CurrentOrthographicSize = camera.orthographicSize;
                report.CurrentCameraPosition = camera.transform.position;
                Vector2 size = target.bounds.size;
                report.ProposedOrthographicSize =
                    WDMap16x9PromotionPlanner.OrthographicSizeToContain(size, 16f / 9f);
                Vector3 center = art.transform.TransformPoint(target.bounds.center);
                report.ProposedCameraPosition = new Vector3(center.x, center.y,
                    camera.transform.position.z);
            }
            report.SourceSceneIsDirtyAfterAnalysis = scene.isDirty;
            if (report.SourceSceneIsDirtyAfterAnalysis != report.SourceSceneWasDirty)
                report.Blockers.Add("Dry-run alterou o dirty state da cena.");
        }
        finally
        {
            if (openedTemporarily && scene.IsValid())
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void BuildTerritoryPlan(WDMap16x9PromotionReport report,
        DefinicaoTerritorioMapa definition, TerritorioClique[] matches,
        WDMapMigrationBoundaryRecord record, SpriteRenderer art,
        Sprite currentSprite, Sprite targetSprite)
    {
        var plan = new WDMap16x9TerritoryPlan { TerritoryId = definition.id };
        report.Territories.Add(plan);
        if (matches.Length != 1 || record == null)
        {
            plan.Blocker = matches.Length != 1 ? "runtime representation count=" +
                matches.Length : "boundary absent";
            return;
        }
        TerritorioClique territory = matches[0];
        PolygonCollider2D collider = territory.GetComponent<PolygonCollider2D>();
        plan.GameObjectName = territory.gameObject.name;
        plan.CurrentPathCount = collider == null ? 0 : collider.pathCount;
        plan.NewPathCount = record.paths.Count;
        plan.ComponentIds = record.paths.Select(path => path.componentIndex).ToArray();
        plan.IsMultipart = record.paths.Count > 1;
        plan.NewBoundsPixels = Union(record.paths.Select(path =>
            new Rect(path.sourceBounds.x, path.sourceBounds.y,
                path.sourceBounds.width, path.sourceBounds.height)));
        Matrix4x4 worldToArt = art.transform.worldToLocalMatrix;
        if (collider != null)
        {
            Matrix4x4 colliderToArt = worldToArt * collider.transform.localToWorldMatrix;
            var oldPoints = new List<Vector2>();
            for (int index = 0; index < collider.pathCount; index++)
                oldPoints.AddRange(collider.GetPath(index).Select(point =>
                    ArtLocalToPixel(colliderToArt.MultiplyPoint3x4(point), currentSprite)));
            if (oldPoints.Count > 0) plan.CurrentBoundsPixels = BoundsOf(oldPoints);
        }
        plan.CurrentAnchorPixels = ArtLocalToPixel(
            worldToArt.MultiplyPoint3x4(territory.transform.position), currentSprite);
        plan.ProjectedAnchorPixels = WDMap16x9PromotionPlanner.ProjectPixel(
            plan.CurrentAnchorPixels, currentSprite.rect.size, targetSprite.rect.size);
        Transform counter = territory.transform.Find("ContadorModerno");
        plan.CounterExists = counter != null;
        if (counter != null)
        {
            plan.CounterCurrentPixels = ArtLocalToPixel(
                worldToArt.MultiplyPoint3x4(counter.position), currentSprite);
            plan.CounterProjectedPixels = WDMap16x9PromotionPlanner.ProjectPixel(
                plan.CounterCurrentPixels, currentSprite.rect.size, targetSprite.rect.size);
            plan.CounterPreservable = WDMap16x9PromotionPlanner.Contains(record.paths,
                plan.CounterProjectedPixels, out _);
        }
        plan.CounterFallbackPixels = WDMap16x9PromotionPlanner.FindInteriorPoint(
            record.paths, out plan.CounterFallbackPath);
        if (plan.CounterPreservable) report.CountersPreservable++;
        else report.CountersRepositioned++;
        SpriteRenderer territoryRenderer = territory.GetComponent<SpriteRenderer>();
        plan.VisualStrategy = territoryRenderer == null
            ? "BLOCKED_NO_SPRITE_RENDERER"
            : "EDITOR_GENERATED_BOUNDARY_VISUAL";
        plan.Promotable = collider != null && territoryRenderer != null &&
            record.paths.Count > 0;
        if (!plan.Promotable) plan.Blocker = "Collider ou SpriteRenderer ausente.";
        if (!plan.Promotable) report.Blockers.Add(definition.id + ": " + plan.Blocker);
    }

    private static void ValidateLogicalData(WDMap16x9PromotionReport report,
        DefinicaoMapa map)
    {
        var ids = new HashSet<string>(map.Territorios.Select(item => item.id),
            StringComparer.Ordinal);
        report.ConnectionCount = map.Conexoes.Count;
        report.RegionCount = map.Regioes.Count;
        foreach (DefinicaoConexaoMapa connection in map.Conexoes)
            if (!ids.Contains(connection.origemId) || !ids.Contains(connection.destinoId))
                report.Blockers.Add($"Conexão referencia ID inexistente: " +
                    $"{connection.origemId}->{connection.destinoId}");
        foreach (DefinicaoRegiaoMapa region in map.Regioes)
        foreach (string territoryId in region.territorioIds)
            if (!ids.Contains(territoryId))
                report.Blockers.Add($"Região {region.id} referencia ID inexistente: " +
                    territoryId);
        report.ConnectionsHash = WDMap16x9PromotionPlanner.StableHash(map.Conexoes
            .Select(item => $"{item.origemId}|{item.destinoId}|{item.tipo}|{item.bidirecional}"));
        report.RegionsHash = WDMap16x9PromotionPlanner.StableHash(map.Regioes
            .OrderBy(item => item.id).Select(item => item.id + "|" +
                string.Join(",", item.territorioIds)));
    }

    private static WDMap16x9PromotionReport Finish(WDMap16x9PromotionReport report,
        bool exportReport)
    {
        report.Status = report.Blockers.Count > 0
            ? WDMap16x9PromotionStatus.Blocked
            : report.Warnings.Count > 0 ? WDMap16x9PromotionStatus.Warning
            : WDMap16x9PromotionStatus.Ready;
        if (exportReport)
        {
            string reportPath = report.MapId == "classic" ? ReportPath :
                "Docs/Diagnostics/WD16x9_" + report.MapId + "_DryRun.md";
            string absolute = Path.Combine(Directory.GetCurrentDirectory(), reportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            File.WriteAllText(absolute, ToMarkdown(report), new UTF8Encoding(false));
        }
        return report;
    }

    private static Sprite LoadSprite(string path) => AssetDatabase.LoadAllAssetsAtPath(path)
        .OfType<Sprite>().OrderBy(sprite => sprite.name, StringComparer.Ordinal).FirstOrDefault();

    private static void AddDuplicateTerritories(WDMap16x9PromotionReport report,
        IEnumerable<TerritorioClique> territories)
    {
        foreach (IGrouping<string, TerritorioClique> group in territories
            .GroupBy(item => item.idTerritorio ?? string.Empty, StringComparer.Ordinal)
            .Where(group => group.Count() > 1))
            report.Blockers.Add("TerritorioClique duplicado: " + group.Key);
    }

    private static Vector2 ArtLocalToPixel(Vector3 local, Sprite sprite) =>
        WDMap16x9PromotionPlanner.SpriteLocalToBoundaryPixel(local, sprite);

    private static Rect BoundsOf(IReadOnlyList<Vector2> points) => Rect.MinMaxRect(
        points.Min(point => point.x), points.Min(point => point.y),
        points.Max(point => point.x), points.Max(point => point.y));

    private static Rect Union(IEnumerable<Rect> source)
    {
        Rect[] rects = source.ToArray();
        if (rects.Length == 0) return default;
        Rect result = rects[0];
        for (int index = 1; index < rects.Length; index++)
            result = Rect.MinMaxRect(Mathf.Min(result.xMin, rects[index].xMin),
                Mathf.Min(result.yMin, rects[index].yMin),
                Mathf.Max(result.xMax, rects[index].xMax),
                Mathf.Max(result.yMax, rects[index].yMax));
        return result;
    }

    private static string ReadHead()
    {
        string git = Path.Combine(Directory.GetCurrentDirectory(), ".git");
        string headPath = Path.Combine(git, "HEAD");
        if (!File.Exists(headPath)) return "unavailable";
        string head = File.ReadAllText(headPath).Trim();
        if (!head.StartsWith("ref: ", StringComparison.Ordinal)) return head;
        string referencePath = Path.Combine(git, head.Substring(5).Replace('/',
            Path.DirectorySeparatorChar));
        return File.Exists(referencePath) ? File.ReadAllText(referencePath).Trim() : head;
    }

    private static void AppendList(StringBuilder text, string title,
        IReadOnlyList<string> entries)
    {
        text.AppendLine().AppendLine("## " + title).AppendLine();
        if (entries.Count == 0) text.AppendLine("- None");
        else foreach (string entry in entries) text.AppendLine("- " + entry);
    }

    private static string Format(Rect value) =>
        $"({value.x:0.#},{value.y:0.#},{value.width:0.#},{value.height:0.#})";
    private static string Format(Vector2 value) => $"({value.x:0.#},{value.y:0.#})";
    private static string Format(Vector3 value) =>
        $"({value.x:0.###},{value.y:0.###},{value.z:0.###})";
    private static string Format(Bounds value) =>
        $"center={Format(value.center)}, size={Format(value.size)}";
}
