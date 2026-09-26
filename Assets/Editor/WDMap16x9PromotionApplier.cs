using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class WDMap16x9PromotionResult
{
    public bool Applied;
    public string Message;
    public WDMap16x9PromotionReport Before;
    public WDMap16x9PromotionReport After;
    public readonly List<string> GeneratedAssets = new List<string>();
}

// ============================================================
// 1. APLICAÇÃO TRANSACIONAL DO PILOTO CLASSIC
// ============================================================
public static class WDMap16x9PromotionApplier
{
    public const string BackupDirectory =
        "_LocalBackups/WD16x9_PrePromotion_20260912_101318";
    public const string RuntimeDirectory = "Assets/Mapas/Classic/Runtime16x9";
    public const string PromotionReportPath =
        "Docs/Diagnostics/WD16x9_Classic_Promotion.md";
    private const string SceneHash =
        "1E8DC44914C0C771C4744B200C89FB67EE26290A74545E48D6969F25A79AC1C5";
    private const string DefinitionHash =
        "3F28AE401223ACE5E521048631D17C5B2E637817CAEA13885DFEBC0B7A4C97E1";
    private const string BackgroundHash =
        "429EAC51A1CCEC080AD41C3576F5BFF8997FC6352A62B1060DF7257BE4E30946";
    private const string BackupBoundaryHash =
        "89248A7B8364B352368E5D987CCB415701FEED94142BC3A1B6C327F6EE1B9DF6";
    private const string CurrentBoundaryHash =
        "0C4529C262BDCEB81F95CB9F488AE3D784B78D8BBE567E3AA87F54706F1166E7";

    [MenuItem("War Dominion/Editor de Mapas/Promover quatro mapas restantes para 16:9")]
    public static void PromoteRemainingMapsFromMenu()
    {
        foreach (WDMap16x9PromotionTarget target in
                 WDMap16x9PromotionService.RemainingMaps)
        {
            WDMap16x9PromotionReport dryRun = WDMap16x9PromotionService.Analyze(
                target, true);
            if (dryRun.Status != WDMap16x9PromotionStatus.Ready)
            {
                Debug.LogError("[WD 16:9] " + target.DisplayName +
                    " bloqueado no dry-run: " + string.Join(" | ", dryRun.Blockers));
                continue;
            }
            string backup = CreateBackup(target);
            WDMap16x9PromotionResult result = Apply(target, backup);
            if (result.Applied)
                Debug.Log("[WD 16:9] " + target.DisplayName +
                    " promovido com sucesso. Backup: " + backup);
            else
                Debug.LogError("[WD 16:9] " + target.DisplayName +
                    " não foi promovido: " + result.Message);
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    public static bool CanApply(WDMap16x9PromotionReport report, bool backupValid) =>
        report != null && report.Status == WDMap16x9PromotionStatus.Ready &&
        report.Blockers.Count == 0 && backupValid;

    public static bool BackupIsValid(out string reason)
    {
        string manifest = Path.Combine(BackupDirectory, "MANIFEST.md");
        if (!Directory.Exists(BackupDirectory) || !File.Exists(manifest))
        { reason = "Backup ou manifesto ausente."; return false; }
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { WDMap16x9PromotionService.ClassicScenePath, SceneHash },
            { WDMap16x9PromotionService.ClassicMapAssetPath, DefinitionHash },
            { WDMap16x9PromotionService.ClassicTargetArtPath, BackgroundHash },
            { WDMapMigrationBoundaryRepository.AssetPath, BackupBoundaryHash }
        };
        foreach (KeyValuePair<string, string> item in expected)
        {
            if (!File.Exists(item.Key)) { reason = "Fonte ausente: " + item.Key; return false; }
            string copy = Path.Combine(BackupDirectory, item.Key);
            if (!File.Exists(copy) || !string.Equals(Hash(copy), item.Value,
                    StringComparison.Ordinal))
            { reason = "Cópia de rollback inválida: " + item.Key; return false; }
        }
        if (!string.Equals(Hash(WDMap16x9PromotionService.ClassicTargetArtPath),
                BackgroundHash, StringComparison.Ordinal) ||
            !string.Equals(Hash(WDMapMigrationBoundaryRepository.AssetPath), CurrentBoundaryHash,
                StringComparison.Ordinal))
        { reason = "Fonte geométrica ou background canônico divergiu."; return false; }
        reason = string.Empty;
        return true;
    }

    public static WDMap16x9PromotionResult ApplyClassic()
        => Apply(WDMap16x9PromotionService.Classic, BackupDirectory, true);

    public static string CreateBackup(WDMap16x9PromotionTarget target)
    {
        string root = "_LocalBackups/WD16x9_PrePromotion_" +
            target.MapId + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string[] sources =
        {
            target.ScenePath, target.MapAssetPath, target.TargetArtPath,
            target.TargetArtPath + ".meta", WDMapMigrationBoundaryRepository.AssetPath,
            WDMapMigrationBoundaryRepository.AssetPath + ".meta"
        };
        var manifest = new StringBuilder()
            .AppendLine("# WD 16:9 Pre-Promotion Backup")
            .AppendLine().AppendLine("- Map: `" + target.MapId + "`")
            .AppendLine("- Timestamp: `" + DateTimeOffset.Now.ToString("o") + "`")
            .AppendLine("- Current art: `" + CurrentArtPath(target) + "`")
            .AppendLine("- Target art: `" + target.TargetArtPath + "`")
            .AppendLine().AppendLine("## Files");
        foreach (string source in sources.Where(File.Exists))
        {
            string destination = Path.Combine(root, source);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(source, destination, true);
            manifest.AppendLine("- `" + source + "` — `" + Hash(source) + "`");
        }
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "MANIFEST.md"), manifest.ToString(),
            new UTF8Encoding(false));
        return root;
    }

    public static bool BackupIsValid(WDMap16x9PromotionTarget target,
        string backupDirectory, out string reason)
    {
        string manifest = Path.Combine(backupDirectory, "MANIFEST.md");
        if (!File.Exists(manifest))
        { reason = "Manifesto do backup ausente."; return false; }
        foreach (string source in new[] { target.ScenePath, target.MapAssetPath,
                     target.TargetArtPath, WDMapMigrationBoundaryRepository.AssetPath })
        {
            string copy = Path.Combine(backupDirectory, source);
            if (!File.Exists(source) || !File.Exists(copy) ||
                !string.Equals(Hash(source), Hash(copy), StringComparison.Ordinal))
            { reason = "Backup divergiu da fonte: " + source; return false; }
        }
        reason = string.Empty;
        return true;
    }

    public static WDMap16x9PromotionResult Apply(WDMap16x9PromotionTarget target,
        string backupDirectory, bool useClassicPinnedBackup = false)
    {
        var result = new WDMap16x9PromotionResult();
        result.Before = WDMap16x9PromotionService.Analyze(target, false);
        if (result.Before.Status != WDMap16x9PromotionStatus.Ready)
            return Abort(result, "Dry-run não está READY.");
        bool backupValid = useClassicPinnedBackup
            ? BackupIsValid(out string backupFailure)
            : BackupIsValid(target, backupDirectory, out backupFailure);
        if (!backupValid)
            return Abort(result, backupFailure);

        DefinicaoMapa map = AssetDatabase.LoadAssetAtPath<DefinicaoMapa>(
            target.MapAssetPath);
        WDMapMigrationBoundaryStore store = WDMapMigrationBoundaryRepository.Load();
        Sprite targetSprite = AssetDatabase.LoadAllAssetsAtPath(
            target.TargetArtPath).OfType<Sprite>().First();
        WDMapMigrationBoundaryRecord[] records = store.ForMap(target.MapId).ToArray();
        string connectionsBefore = result.Before.ConnectionsHash;
        string regionsBefore = result.Before.RegionsHash;
        Scene scene = SceneManager.GetSceneByPath(
            target.ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(
            target.ScenePath, OpenSceneMode.Additive);

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Promover " + target.DisplayName + " para WD 16:9");
        var createdAssets = new List<string>();
        var overwrittenAssets = new Dictionary<string, string>(StringComparer.Ordinal);
        var overwrittenMetas = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            SpriteRenderer art = FindArt(scene, map.ArteBase);
            if (art == null) return Abort(result, "Renderer da arte vigente não encontrado.");
            TerritorioClique[] territories = FindTerritories(scene);
            Dictionary<string, WDMap16x9TerritoryPlan> plans = result.Before.Territories
                .ToDictionary(item => item.TerritoryId, StringComparer.Ordinal);

            string temporaryDirectory = Path.Combine(backupDirectory,
                "GeneratedRuntime16x9_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(temporaryDirectory);
            Texture2D source = LoadPng(target.TargetArtPath);
            try
            {
                foreach (WDMapMigrationBoundaryRecord record in records)
                {
                    string fileName = Sanitize(record.territoryId) + ".png";
                    string temporary = Path.Combine(temporaryDirectory, fileName);
                    File.WriteAllBytes(temporary, BuildTerritoryPng(source, record));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }

            if (!AssetDatabase.IsValidFolder(target.RuntimeDirectory))
            {
                string parent = Path.GetDirectoryName(target.RuntimeDirectory)
                    .Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, "Runtime16x9");
                result.GeneratedAssets.Add(target.RuntimeDirectory);
            }
            foreach (WDMapMigrationBoundaryRecord record in records)
            {
                string fileName = Sanitize(record.territoryId) + ".png";
                string destination = target.RuntimeDirectory + "/" + fileName;
                if (File.Exists(destination))
                {
                    string previous = Path.Combine(temporaryDirectory,
                        "previous_" + fileName);
                    File.Copy(destination, previous, true);
                    overwrittenAssets[destination] = previous;
                    if (File.Exists(destination + ".meta"))
                    {
                        string previousMeta = previous + ".meta";
                        File.Copy(destination + ".meta", previousMeta, true);
                        overwrittenMetas[destination + ".meta"] = previousMeta;
                    }
                }
                else createdAssets.Add(destination);
                File.Copy(Path.Combine(temporaryDirectory, fileName), destination, true);
                AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
                ConfigureSprite(destination, targetSprite.pixelsPerUnit);
                result.GeneratedAssets.Add(destination);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Undo.RecordObject(map, "Atualizar arte " + target.DisplayName + " 16:9");
            var serializedMap = new SerializedObject(map);
            serializedMap.FindProperty("arteBase").objectReferenceValue = targetSprite;
            serializedMap.ApplyModifiedPropertiesWithoutUndo();

            Undo.RecordObject(art, "Atualizar background " + target.DisplayName + " 16:9");
            Undo.RecordObject(art.transform, "Normalizar background " + target.DisplayName + " 16:9");
            art.sprite = targetSprite;
            art.transform.localScale = Vector3.one;
            foreach (TerritorioClique territory in territories)
            {
                WDMapMigrationBoundaryRecord record = records.Single(item =>
                    item.territoryId == territory.idTerritorio);
                WDMap16x9TerritoryPlan plan = plans[territory.idTerritorio];
                ApplyTerritory(territory, art, targetSprite, record, plan,
                    target.RuntimeDirectory,
                    map.Territorios.Single(item => item.id == territory.idTerritorio));
            }
            Camera camera = scene.GetRootGameObjects().SelectMany(root =>
                root.GetComponentsInChildren<Camera>(true)).First(item => item.orthographic);
            Undo.RecordObject(camera.transform, "Enquadrar " + target.DisplayName + " 16:9");
            Undo.RecordObject(camera, "Enquadrar " + target.DisplayName + " 16:9");
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            Vector3 center = art.transform.TransformPoint(targetSprite.bounds.center);
            camera.transform.position = new Vector3(center.x, center.y,
                camera.transform.position.z);
            camera.orthographicSize = WDMap16x9PromotionPlanner.OrthographicSizeToContain(
                targetSprite.bounds.size, 16f / 9f);

            ValidateAppliedInMemory(scene, map, targetSprite, records, connectionsBefore,
                regionsBefore, result.Before.ExpectedTerritoryIds);
            EditorUtility.SetDirty(map);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssetIfDirty(map);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Unity não salvou " + target.ScenePath + ".");
            result.Applied = true;
            result.Message = target.DisplayName.ToUpperInvariant() + " PROMOTED";
            result.After = WDMap16x9PromotionService.Analyze(target, false);
            string reportPath = "Docs/Diagnostics/WD16x9_" + target.MapId +
                "_Promotion.md";
            File.WriteAllText(reportPath, BuildReport(result, target, backupDirectory),
                new UTF8Encoding(false));
            return result;
        }
        catch (Exception exception)
        {
            Undo.RevertAllDownToGroup(undoGroup);
            foreach (string asset in createdAssets.AsEnumerable().Reverse())
                AssetDatabase.DeleteAsset(asset);
            foreach (KeyValuePair<string, string> previous in overwrittenAssets)
            {
                File.Copy(previous.Value, previous.Key, true);
                if (overwrittenMetas.TryGetValue(previous.Key + ".meta",
                        out string previousMeta))
                    File.Copy(previousMeta, previous.Key + ".meta", true);
                AssetDatabase.ImportAsset(previous.Key,
                    ImportAssetOptions.ForceSynchronousImport);
            }
            if (result.GeneratedAssets.Contains(target.RuntimeDirectory) &&
                AssetDatabase.IsValidFolder(target.RuntimeDirectory))
                AssetDatabase.DeleteAsset(target.RuntimeDirectory);
            return Abort(result, exception.GetType().Name + ": " + exception.Message);
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
            if (opened && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void ApplyTerritory(TerritorioClique territory, SpriteRenderer art,
        Sprite target, WDMapMigrationBoundaryRecord record,
        WDMap16x9TerritoryPlan plan, string runtimeDirectory,
        DefinicaoTerritorioMapa definition)
    {
        PolygonCollider2D collider = territory.GetComponent<PolygonCollider2D>();
        SpriteRenderer renderer = territory.GetComponent<SpriteRenderer>();
        Transform counter = territory.transform.Find("ContadorModerno");
        RectInt crop = CropBounds(record, Mathf.RoundToInt(target.rect.height));
        Vector2 counterPixel = plan.CounterPreservable
            ? plan.CounterProjectedPixels : plan.CounterFallbackPixels;
        Undo.RecordObject(territory.transform, "Migrar território 16:9");
        Undo.RecordObject(collider, "Migrar collider 16:9");
        Undo.RecordObject(renderer, "Migrar visual 16:9");
        if (counter != null) Undo.RecordObject(counter, "Migrar contador 16:9");

        territory.transform.SetPositionAndRotation(
            art.transform.TransformPoint(UnityPixelToArt(crop.min, target)), art.transform.rotation);
        territory.transform.localScale = RelativeScale(territory.transform.parent,
            art.transform.lossyScale);
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(runtimeDirectory + "/" +
            Sanitize(record.territoryId) + ".png").OfType<Sprite>().First();
        renderer.sprite = sprite;
        collider.pathCount = record.paths.Count;
        for (int pathIndex = 0; pathIndex < record.paths.Count; pathIndex++)
            collider.SetPath(pathIndex, record.paths[pathIndex].points.Select(point =>
                (WDMap16x9PromotionPlanner.BoundaryToUnityPixel(point,
                    target.rect.height) - crop.min) / target.pixelsPerUnit).ToArray());
        if (counter != null)
            counter.position = art.transform.TransformPoint(
                WDMap16x9PromotionPlanner.BoundaryPixelToSpriteLocal(counterPixel, target));
        definition.posicaoContador =
            WDMap16x9PromotionPlanner.BoundaryPixelToSpriteLocal(counterPixel, target);
        definition.possuiPosicaoContador = true;
    }

    private static byte[] BuildTerritoryPng(Texture2D source,
        WDMapMigrationBoundaryRecord record)
    {
        RectInt bounds = CropBounds(record, source.height);
        var output = new Texture2D(bounds.width, bounds.height, TextureFormat.RGBA32,
            false, false);
        var pixels = new Color32[bounds.width * bounds.height];
        for (int y = 0; y < bounds.height; y++)
        for (int x = 0; x < bounds.width; x++)
        {
            int globalX = bounds.x + x;
            int globalY = bounds.y + y;
            Vector2 sample = WDMap16x9PromotionPlanner.UnityToBoundaryPixel(
                new Vector2(globalX + 0.5f, globalY + 0.5f), source.height);
            pixels[y * bounds.width + x] =
                WDMap16x9PromotionPlanner.BuildTerritoryOverlayPixel(
                    WDMap16x9PromotionPlanner.Contains(record.paths, sample, out _));
        }
        output.SetPixels32(pixels);
        output.Apply(false, false);
        byte[] png = output.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(output);
        return png;
    }

    private static void ConfigureSprite(string path, float ppu)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = ppu;
        importer.spritePivot = Vector2.zero;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = Vector2.zero;
        importer.SetTextureSettings(settings);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static void ValidateAppliedInMemory(Scene scene, DefinicaoMapa map,
        Sprite target, IReadOnlyList<WDMapMigrationBoundaryRecord> records,
        string connectionsHash, string regionsHash, int expectedTerritories)
    {
        TerritorioClique[] territories = FindTerritories(scene);
        if (territories.Length != expectedTerritories || territories
                .Select(item => item.idTerritorio).Distinct(StringComparer.Ordinal)
                .Count() != expectedTerritories)
            throw new InvalidOperationException("TerritorioClique pós-aplicação inválido.");
        foreach (TerritorioClique territory in territories)
        {
            WDMapMigrationBoundaryRecord record = records.Single(item =>
                item.territoryId == territory.idTerritorio);
            PolygonCollider2D collider = territory.GetComponent<PolygonCollider2D>();
            if (collider == null || collider.pathCount != record.paths.Count ||
                territory.GetComponent<SpriteRenderer>()?.sprite == null)
                throw new InvalidOperationException("Visual/collider inválido: " +
                    territory.idTerritorio);
        }
        SpriteRenderer art = FindArt(scene, target);
        if (art == null || map.ArteBase != target)
            throw new InvalidOperationException("Background e DefinicaoMapa divergentes.");
        string currentConnections = WDMap16x9PromotionPlanner.StableHash(map.Conexoes
            .Select(item => $"{item.origemId}|{item.destinoId}|{item.tipo}|{item.bidirecional}"));
        string currentRegions = WDMap16x9PromotionPlanner.StableHash(map.Regioes
            .OrderBy(item => item.id).Select(item => item.id + "|" +
                string.Join(",", item.territorioIds)));
        if (connectionsHash != currentConnections || regionsHash != currentRegions)
            throw new InvalidOperationException("Conexões ou regiões foram alteradas.");
    }

    private static WDMap16x9PromotionResult Abort(WDMap16x9PromotionResult result,
        string message) { result.Applied = false; result.Message = message; return result; }
    private static TerritorioClique[] FindTerritories(Scene scene) => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<TerritorioClique>(true)).ToArray();
    private static SpriteRenderer FindArt(Scene scene, Sprite sprite) => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<SpriteRenderer>(true))
        .FirstOrDefault(item => item.sprite == sprite);
    private static Texture2D LoadPng(string path)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(File.ReadAllBytes(path), false))
            throw new InvalidOperationException("PNG alvo não pôde ser lido.");
        return texture;
    }
    private static RectInt CropBounds(WDMapMigrationBoundaryRecord record,
        int canvasHeight)
    {
        int minX = record.paths.Min(path => path.sourceBounds.xMin);
        int minY = canvasHeight - record.paths.Max(path => path.sourceBounds.yMax);
        int maxX = record.paths.Max(path => path.sourceBounds.xMax);
        int maxY = canvasHeight - record.paths.Min(path => path.sourceBounds.yMin);
        return new RectInt(minX, minY, maxX - minX, maxY - minY);
    }
    private static Vector2 UnityPixelToArt(Vector2 pixel, Sprite sprite) =>
        (pixel - sprite.pivot) / sprite.pixelsPerUnit;
    private static Vector3 RelativeScale(Transform parent, Vector3 worldScale)
    {
        if (parent == null) return worldScale;
        Vector3 parentScale = parent.lossyScale;
        return new Vector3(worldScale.x / parentScale.x, worldScale.y / parentScale.y,
            worldScale.z / parentScale.z);
    }
    private static string Sanitize(string value) => string.Concat(value.Select(character =>
        Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
    private static string Hash(string path)
    {
        using (SHA256 sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)))
                .Replace("-", string.Empty);
    }
    private static string CurrentArtPath(WDMap16x9PromotionTarget target)
    {
        DefinicaoMapa map = AssetDatabase.LoadAssetAtPath<DefinicaoMapa>(
            target.MapAssetPath);
        return map == null ? string.Empty : AssetDatabase.GetAssetPath(map.ArteBase);
    }

    private static string BuildReport(WDMap16x9PromotionResult result,
        WDMap16x9PromotionTarget target, string backupDirectory) =>
        "# WD 16:9 — " + target.DisplayName + " Promotion\n\n" +
        "- Timestamp: " + DateTimeOffset.Now.ToString("o") + "\n" +
        "- HEAD: `" + result.Before.Head + "`\n" +
        "- Backup: `" + backupDirectory + "`\n" +
        "- Result: **" + (result.Applied ? target.DisplayName.ToUpperInvariant() +
            " PROMOTED" :
            "PROMOTION ABORTED") + "**\n" +
        "- Background: `" + result.Before.CurrentArtPath + "` → `" +
            target.TargetArtPath + "`\n" +
        $"- TerritoryIds: {result.Before.TerritoryIds}\n" +
        $"- Paths: {result.Before.Paths}\n" +
        $"- Multipartes: {result.Before.MultipartTerritories}\n" +
        $"- Counters preserved/repositioned: {result.Before.CountersPreservable}/" +
        $"{result.Before.CountersRepositioned}\n" +
        $"- Connections hash: `{result.Before.ConnectionsHash}`\n" +
        $"- Regions hash: `{result.Before.RegionsHash}`\n" +
        "- Generated assets:\n" + string.Join("\n", result.GeneratedAssets
            .Select(path => "  - `" + path + "`")) + "\n";
}
