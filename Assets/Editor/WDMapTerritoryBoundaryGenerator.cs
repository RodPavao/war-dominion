using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

public sealed class WDMapMaskRegion
{
    public int Index { get; }
    public int Area { get; }
    public RectInt Bounds { get; }
    public Vector2 Centroid { get; }
    public float Density => Bounds.width <= 0 || Bounds.height <= 0 ? 0f :
        (float)Area / (Bounds.width * Bounds.height);
    public IReadOnlyList<Vector2> Boundary { get; }
    internal HashSet<int> Pixels { get; }

    internal WDMapMaskRegion(int index, int area, RectInt bounds, Vector2 centroid,
        IReadOnlyList<Vector2> boundary, HashSet<int> pixels)
    {
        Index = index;
        Area = area;
        Bounds = bounds;
        Centroid = centroid;
        Boundary = boundary;
        Pixels = pixels;
    }
}

public sealed class WDMapBoundaryAssociation
{
    public string TerritoryId { get; }
    public WDMapMaskRegion Region { get; }
    public WDMapBoundaryConfidence Confidence { get; }
    public float Score { get; }

    public WDMapBoundaryAssociation(string territoryId, WDMapMaskRegion region,
        WDMapBoundaryConfidence confidence, float score)
    {
        TerritoryId = territoryId;
        Region = region;
        Confidence = confidence;
        Score = score;
    }
}

public sealed class WDMapBoundaryGenerationResult
{
    public string MapId { get; }
    public int OfficialTerritories { get; }
    public int RawComponents { get; }
    public int ValidComponents { get; }
    public int DiscardedArtifacts { get; }
    public IReadOnlyList<WDMapMigrationBoundaryRecord> Records { get; }
    public IReadOnlyList<WDMapMaskRegion> Unassociated { get; }

    public WDMapBoundaryGenerationResult(string mapId, int officialTerritories,
        int rawComponents, int validComponents, int discardedArtifacts,
        IReadOnlyList<WDMapMigrationBoundaryRecord> records,
        IReadOnlyList<WDMapMaskRegion> unassociated)
    {
        MapId = mapId;
        OfficialTerritories = officialTerritories;
        RawComponents = rawComponents;
        ValidComponents = validComponents;
        DiscardedArtifacts = discardedArtifacts;
        Records = records;
        Unassociated = unassociated;
    }
}

public static class WDMapTerritoryBoundaryGenerator
{
    public const int AlgorithmVersion = 4;
    public const float SimplificationTolerancePixels = 1f;

    private static readonly Dictionary<string, string> MaskPaths =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["classic"] = "Assets/Mapas/Classic/Migration/WD_Map_Classic_TerritoryMask_16x9_v01.png",
            ["pearl_harbor"] = "Assets/Mapas/Pearl Harbor/Migration/WD_Map_PearlHarbor_TerritoryMask_16x9_v01.png",
            ["riachuelo"] = "Assets/Mapas/Batalha do Riachuelo/Migration/WD_Map_Riachuelo_TerritoryMask_16x9_v01.png",
            ["peloponeso"] = "Assets/Mapas/Peloponeso/Migration/WD_Map_Peloponeso_TerritoryMask_16x9_v01.png",
            ["dark_world"] = "Assets/Mapas/Dark World/Migration/WD_Map_DarkWorld_TerritoryMask_16x9_v01.png"
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<Vector2>>
        ClassicSemanticGuide = new Dictionary<string, IReadOnlyList<Vector2>>(
            StringComparer.Ordinal)
        {
            ["India"] = new[] { new Vector2(1328.56f, 482.33f) },
            ["China"] = new[] { new Vector2(1403.31f, 405.76f) },
            ["Indonesia"] = new[]
            {
                new Vector2(1439.42f, 648.93f),
                new Vector2(1501.91f, 623.83f)
            },
            ["UnitedKingdom"] = new[]
            {
                new Vector2(917.92f, 286.55f),
                new Vector2(877.57f, 294.91f)
            }
        };

    private static readonly (string AssetPath, string MapId, int Territories,
        int RawComponents, int ValidComponents)[] RemainingMapBatch =
    {
        ("Assets/Resources/Mapas/PearlHarbor.asset", "pearl_harbor", 45, 46, 45),
        ("Assets/Resources/Mapas/Riachuelo.asset", "riachuelo", 37, 37, 37),
        ("Assets/Resources/Mapas/Peloponeso.asset", "peloponeso", 82, 88, 82),
        ("Assets/Resources/Mapas/DarkWorld.asset", "dark_world", 43, 43, 43)
    };

    private static readonly IReadOnlyDictionary<string, int>
        RiachueloHumanAssignments = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["LaPaz"] = 27,
            ["Parana"] = 3,
            ["Pilcomayo"] = 23,
            ["Formosa"] = 35
        };

    private static readonly IReadOnlyDictionary<string, int>
        PeloponneseHumanAssignments = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Brauron"] = 55,
            ["Caryae"] = 68,
            ["EpidaurosLimera"] = 50,
            ["Euonymon"] = 73,
            ["Icaria"] = 70,
            ["Kolonos"] = 58,
            ["Mantinea"] = 31,
            ["Myrrhinous"] = 71,
            ["Oetylus"] = 15,
            ["Paiania"] = 14,
            ["Sphettos"] = 65
        };

    private static readonly IReadOnlyDictionary<string, int>
        ClassicHumanCorrections = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["WestUSA"] = 10,
            ["EastUSA"] = 13,
            ["Mexico"] = 33,
            ["Venezuela"] = 24,
            ["Peru"] = 39,
            ["Ural"] = 6,
            ["Siberia"] = 2,
            ["Yakutsk"] = 19,
            ["Irkutsk"] = 31,
            ["Kamchatka"] = 15,
            ["Mongolia"] = 16,
            ["Japan"] = 28
        };

    public static bool TryGetMaskPath(string mapId, out string path) =>
        MaskPaths.TryGetValue(mapId ?? string.Empty, out path);

    [MenuItem("War Dominion/Map Editor/Gerar boundaries MASK AUTO dos 4 mapas restantes")]
    public static void GenerateRemainingMaps()
    {
        var generated = new List<WDMapBoundaryGenerationResult>();
        foreach (var item in RemainingMapBatch)
        {
            DefinicaoMapa map = AssetDatabase.LoadAssetAtPath<DefinicaoMapa>(item.AssetPath);
            if (map == null || map.MapaId != item.MapId)
                throw new InvalidOperationException("Definição de mapa inválida: " +
                    item.AssetPath);
            WDMapBoundaryGenerationResult result = Generate(map,
                WDMapSpatialSnapshot.Capture(map));
            ValidateSinglePartMapResult(result, item.Territories, item.RawComponents,
                item.ValidComponents);
            generated.Add(result);
        }

        WDMapMigrationBoundaryStore store = WDMapMigrationBoundaryRepository.LoadOrCreate();
        foreach (WDMapBoundaryGenerationResult result in generated)
            WDMapMigrationBoundaryRepository.SaveMap(store, result.MapId, result.Records);
        AssetDatabase.SaveAssets();
        foreach (WDMapBoundaryGenerationResult result in generated)
            Debug.Log(FormatGenerationSummary(result));
    }

    // 01. Extração explícita: flood-fill 4-conexo + loop de arestas de células.
    public static IReadOnlyList<WDMapMaskRegion> Extract(Color32[] unityPixels,
        int width, int height)
    {
        if (unityPixels == null || unityPixels.Length != width * height)
            throw new ArgumentException("Pixel buffer does not match mask dimensions.",
                nameof(unityPixels));
        var filled = new bool[unityPixels.Length];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            Color32 pixel = unityPixels[(height - 1 - y) * width + x];
            filled[y * width + x] = pixel.Equals(WDMapTerritoryMaskAuditor.CanonicalFill);
        }

        var visited = new bool[filled.Length];
        var queue = new int[filled.Length];
        var regions = new List<WDMapMaskRegion>();
        for (int start = 0; start < filled.Length; start++)
        {
            if (!filled[start] || visited[start]) continue;
            int read = 0, write = 0;
            queue[write++] = start;
            visited[start] = true;
            var pixels = new HashSet<int>();
            int minX = width, minY = height, maxX = 0, maxY = 0;
            long sumX = 0, sumY = 0;
            while (read < write)
            {
                int current = queue[read++];
                pixels.Add(current);
                int x = current % width;
                int y = current / width;
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                sumX += x; sumY += y;
                Enqueue(x > 0 ? current - 1 : -1);
                Enqueue(x + 1 < width ? current + 1 : -1);
                Enqueue(y > 0 ? current - width : -1);
                Enqueue(y + 1 < height ? current + width : -1);
            }
            var bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            IReadOnlyList<Vector2> boundary = TraceBoundary(pixels, width);
            regions.Add(new WDMapMaskRegion(regions.Count, write, bounds,
                new Vector2((float)sumX / write, (float)sumY / write), boundary, pixels));

            void Enqueue(int index)
            {
                if (index < 0 || !filled[index] || visited[index]) return;
                visited[index] = true;
                queue[write++] = index;
            }
        }
        return regions.OrderByDescending(item => item.Area)
            .ThenBy(item => item.Bounds.y).ThenBy(item => item.Bounds.x)
            .Select((item, index) => new WDMapMaskRegion(index, item.Area, item.Bounds,
                item.Centroid, item.Boundary, item.Pixels)).ToArray();
    }

    public static bool IsValidTerritoryComponent(string mapId, WDMapMaskRegion region)
    {
        if (region.Area < WDMapTerritoryMaskAuditor.MinimumSignificantArea) return false;
        // Peloponeso possui uma rede decorativa de pontes: pequena, ampla e muito vazada.
        return mapId != "peloponeso" || region.Area >= 2500 || region.Density >= 0.12f;
    }

    // 02. Associação conservadora. Contenção/overlap vence distância; empate fica ambíguo.
    public static IReadOnlyList<WDMapBoundaryAssociation> Associate(
        IReadOnlyList<WDMapMaskRegion> regions,
        IReadOnlyDictionary<string, Vector2> territoryAnchors,
        Vector2 canvasSize)
    {
        var remainingRegions = new HashSet<WDMapMaskRegion>(regions);
        var remainingTerritories = new HashSet<string>(territoryAnchors.Keys,
            StringComparer.Ordinal);
        var result = new List<WDMapBoundaryAssociation>();
        float diagonal = Mathf.Max(1f, canvasSize.magnitude);

        var candidates = new List<(string id, WDMapMaskRegion region, float score,
            float normalizedDistance, bool contains)>();
        foreach (KeyValuePair<string, Vector2> territory in territoryAnchors)
        foreach (WDMapMaskRegion region in regions)
        {
            bool contains = PointInPolygon(region.Boundary, territory.Value);
            float distance = Vector2.Distance(region.Centroid, territory.Value) / diagonal;
            float score = (contains ? 2f : 0f) + Mathf.Max(0f, 1f - distance * 5f);
            candidates.Add((territory.Key, region, score, distance, contains));
        }

        foreach (var candidate in candidates.OrderByDescending(item => item.score)
                     .ThenBy(item => item.normalizedDistance)
                     .ThenBy(item => item.id, StringComparer.Ordinal)
                     .ThenBy(item => item.region.Index))
        {
            if (!remainingTerritories.Contains(candidate.id) ||
                !remainingRegions.Contains(candidate.region)) continue;
            bool ambiguous = IsAmbiguous(candidate, candidates, remainingRegions);
            result.Add(new WDMapBoundaryAssociation(candidate.id, candidate.region,
                ambiguous ? WDMapBoundaryConfidence.Ambiguous :
                    Classify(candidate.contains, candidate.normalizedDistance), candidate.score));
            remainingTerritories.Remove(candidate.id);
            remainingRegions.Remove(candidate.region);
        }

        return result;
    }

    public static IReadOnlyList<WDMapBoundaryAssociation> AssociateWithSemanticGuide(
        IReadOnlyList<WDMapMaskRegion> regions,
        IReadOnlyDictionary<string, Vector2> territoryAnchors, Vector2 canvasSize,
        IReadOnlyDictionary<string, IReadOnlyList<Vector2>> semanticGuide)
    {
        if (semanticGuide == null || semanticGuide.Count == 0)
            return Associate(regions, territoryAnchors, canvasSize);
        var reservedRegions = new HashSet<WDMapMaskRegion>();
        var semantic = new List<WDMapBoundaryAssociation>();
        foreach (KeyValuePair<string, IReadOnlyList<Vector2>> territory in
                 semanticGuide.OrderBy(item => item.Key, StringComparer.Ordinal))
        foreach (Vector2 reference in territory.Value)
        {
            if (!territoryAnchors.ContainsKey(territory.Key))
                throw new InvalidOperationException("TerritoryId semântico inexistente: " +
                    territory.Key);
            WDMapMaskRegion region = regions.Where(item => !reservedRegions.Contains(item))
                .OrderBy(item => Vector2.Distance(item.Centroid, reference))
                .ThenBy(item => item.Index).FirstOrDefault();
            if (region == null || Vector2.Distance(region.Centroid, reference) > 2f)
                throw new InvalidOperationException("Componente semântico não encontrado para " +
                    territory.Key + " em " + reference + ".");
            reservedRegions.Add(region);
            semantic.Add(new WDMapBoundaryAssociation(territory.Key, region,
                WDMapBoundaryConfidence.High, 3f));
        }
        var remainingAnchors = territoryAnchors.Where(item =>
                !semanticGuide.ContainsKey(item.Key))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        var automatic = Associate(regions.Where(item => !reservedRegions.Contains(item))
            .ToArray(), remainingAnchors, canvasSize);
        return automatic.Concat(semantic).OrderBy(item => item.Region.Index).ToArray();
    }

    public static WDMapBoundaryGenerationResult Generate(DefinicaoMapa map,
        WDMapSpatialSnapshot spatialSnapshot)
    {
        if (map == null || !TryGetMaskPath(map.MapaId, out string maskPath) ||
            !File.Exists(maskPath))
            throw new InvalidOperationException("Territory Mask 16:9 não encontrada.");
        byte[] bytes = File.ReadAllBytes(maskPath);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true)
            { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            if (!ImageConversion.LoadImage(texture, bytes, false))
                throw new InvalidOperationException("Territory Mask inválida: " + maskPath);
            IReadOnlyList<WDMapMaskRegion> raw = Extract(texture.GetPixels32(),
                texture.width, texture.height);
            WDMapMaskRegion[] valid = raw.Where(item =>
                IsValidTerritoryComponent(map.MapaId, item)).ToArray();
            IReadOnlyDictionary<string, Vector2> anchors = BuildAnchors(map,
                spatialSnapshot, new Vector2(texture.width, texture.height));
            Vector2 canvas = new Vector2(texture.width, texture.height);
            IReadOnlyList<WDMapBoundaryAssociation> associations = map.MapaId == "classic"
                ? AssociateWithSemanticGuide(valid, anchors, canvas, ClassicSemanticGuide)
                : AssociateGlobalOneToOne(Associate(valid, anchors, canvas), valid,
                    anchors, canvas, map, spatialSnapshot);
            if (map.MapaId == "riachuelo")
                associations = ApplyHumanAssignments(associations,
                    RiachueloHumanAssignments);
            if (map.MapaId == "peloponeso")
                associations = ApplyHumanAssignments(associations,
                    PeloponneseHumanAssignments);
            if (map.MapaId == "classic")
                associations = ApplyHumanAssignments(associations,
                    ClassicHumanCorrections);
            if (map.MapaId == "classic") ValidateClassicAssociations(associations,
                anchors.Count, valid.Length);
            string hash = ComputeHash(bytes);
            var records = associations.GroupBy(item => item.TerritoryId)
                .Select(group => new WDMapMigrationBoundaryRecord
                {
                    mapId = map.MapaId,
                    territoryId = group.Key,
                    origin = "MASK_AUTO",
                    confidence = group.Any(item => item.Confidence ==
                        WDMapBoundaryConfidence.Ambiguous)
                        ? WDMapBoundaryConfidence.Ambiguous
                        : group.OrderByDescending(item => item.Confidence)
                            .First().Confidence,
                    reviewState = group.All(item => item.Confidence ==
                        WDMapBoundaryConfidence.High)
                        ? WDMapBoundaryReviewState.AutoNotReviewed
                        : WDMapBoundaryReviewState.ReviewShape,
                    maskAssetPath = maskPath,
                    maskHash = hash,
                    algorithmVersion = AlgorithmVersion,
                    paths = group.OrderBy(item => item.Region.Index).Select(item =>
                        new WDMapMigrationBoundaryPath
                        {
                            componentIndex = item.Region.Index,
                            sourceArea = item.Region.Area,
                            sourceBounds = item.Region.Bounds,
                            points = item.Region.Boundary.ToList()
                        }).ToList()
                }).OrderBy(item => item.territoryId, StringComparer.Ordinal).ToArray();
            var used = new HashSet<WDMapMaskRegion>(associations.Select(item => item.Region));
            ValidateGeneratedRecords(records, anchors.Count, valid.Length);
            return new WDMapBoundaryGenerationResult(map.MapaId, map.Territorios.Count,
                raw.Count, valid.Length, raw.Count - valid.Length, records,
                valid.Where(item => !used.Contains(item)).ToArray());
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    private static void ValidateClassicAssociations(
        IReadOnlyList<WDMapBoundaryAssociation> associations, int officialTerritories,
        int validComponents)
    {
        ILookup<string, WDMapBoundaryAssociation> byTerritory =
            associations.ToLookup(item => item.TerritoryId, StringComparer.Ordinal);
        bool valid = officialTerritories == 42 && validComponents == 44 &&
            associations.Count == 44 && byTerritory.Count == 42 &&
            byTerritory["India"].Count() == 1 && byTerritory["China"].Count() == 1 &&
            byTerritory["Indonesia"].Count() == 2 &&
            byTerritory["UnitedKingdom"].Count() == 2 &&
            byTerritory.All(group => group.Key == "Indonesia" ||
                group.Key == "UnitedKingdom" || group.Count() == 1) &&
            associations.Select(item => item.Region.Index).Distinct().Count() == 44;
        if (!valid)
            throw new InvalidOperationException("Associação do Classic não satisfaz o " +
                "gabarito semântico 42 TerritoryIds / 44 componentes.");
    }

    public static IReadOnlyList<WDMapBoundaryAssociation> AssociateGlobalOneToOne(
        IReadOnlyList<WDMapBoundaryAssociation> initial,
        IReadOnlyList<WDMapMaskRegion> regions,
        IReadOnlyDictionary<string, Vector2> anchors, Vector2 canvas,
        DefinicaoMapa map, WDMapSpatialSnapshot snapshot)
    {
        if (initial.Count <= 1 || initial.Count != regions.Count ||
            initial.Count != anchors.Count) return initial;
        var initialById = initial.ToDictionary(item => item.TerritoryId,
            StringComparer.Ordinal);
        string[] territoryIds = initial.Select(item => item.TerritoryId)
            .OrderBy(item => item, StringComparer.Ordinal).ToArray();
        WDMapMaskRegion[] candidateRegions = regions
            .OrderBy(item => item.Index).ToArray();
        var locked = initial.Where(item => item.Confidence !=
            WDMapBoundaryConfidence.Ambiguous).ToDictionary(item => item.TerritoryId,
            item => item.Region, StringComparer.Ordinal);
        var scores = new float[territoryIds.Length, candidateRegions.Length];
        var overlaps = new float[territoryIds.Length, candidateRegions.Length];
        var topology = new int[territoryIds.Length, candidateRegions.Length];
        var regionsById = map.Regioes.SelectMany(region => region.territorioIds.Select(id =>
                (TerritoryId: id, RegionId: region.id))).ToDictionary(
            item => item.TerritoryId, item => item.RegionId,
            StringComparer.Ordinal);
        var neighbours = BuildNeighbourMap(map);

        for (int territoryIndex = 0; territoryIndex < territoryIds.Length; territoryIndex++)
        for (int regionIndex = 0; regionIndex < candidateRegions.Length; regionIndex++)
        {
            string territoryId = territoryIds[territoryIndex];
            WDMapMaskRegion region = candidateRegions[regionIndex];
            float historicalCoverage = HistoricalOverlap(territoryId, region, map,
                snapshot, canvas);
            overlaps[territoryIndex, regionIndex] = historicalCoverage;
            bool containsAnchor = anchors.TryGetValue(territoryId, out Vector2 anchor) &&
                PointInPolygon(region.Boundary, anchor);
            float distance = anchors.TryGetValue(territoryId, out anchor)
                ? DistanceToBoundary(region.Boundary, anchor) / Mathf.Max(1f, canvas.magnitude)
                : 1f;
            int topologyMatches = neighbours.TryGetValue(territoryId,
                    out HashSet<string> expectedNeighbours)
                ? expectedNeighbours.Count(id => locked.TryGetValue(id,
                    out WDMapMaskRegion neighbourRegion) && AreAdjacent(region, neighbourRegion))
                : 0;
            topology[territoryIndex, regionIndex] = topologyMatches;
            int regionMatches = regionsById.TryGetValue(territoryId, out string regionId)
                ? locked.Count(pair => regionsById.TryGetValue(pair.Key, out string other) &&
                    other == regionId && AreNearby(region, pair.Value, canvas))
                : 0;
            scores[territoryIndex, regionIndex] = ComposeCompatibilityScore(
                historicalCoverage, containsAnchor, distance, topologyMatches,
                regionMatches);
        }

        int[] assignment = SolveMaximumAssignment(scores, out float bestTotal);
        var replacements = new Dictionary<string, WDMapBoundaryAssociation>(
            StringComparer.Ordinal);
        for (int territoryIndex = 0; territoryIndex < territoryIds.Length; territoryIndex++)
        {
            int regionIndex = assignment[territoryIndex];
            int[] alternate = SolveMaximumAssignment(scores, out float alternateTotal,
                territoryIndex, regionIndex);
            if (alternate.Length == 0) alternateTotal = float.NegativeInfinity;
            float globalMargin = bestTotal - alternateTotal;
            float overlap = overlaps[territoryIndex, regionIndex];
            int topologyMatches = topology[territoryIndex, regionIndex];
            string territoryId = territoryIds[territoryIndex];
            WDMapMaskRegion region = candidateRegions[regionIndex];
            WDMapBoundaryAssociation previous = initialById[territoryId];
            WDMapBoundaryConfidence confidence = previous.Region.Index == region.Index &&
                previous.Confidence != WDMapBoundaryConfidence.Ambiguous
                ? previous.Confidence
                : ClassifyGlobalEvidence(globalMargin, overlap, topologyMatches);
            replacements[territoryId] = new WDMapBoundaryAssociation(territoryId,
                region, confidence, scores[territoryIndex, regionIndex]);
            Debug.Log($"[WD MASK GLOBAL] {map.MapaId}/{territoryId} -> " +
                $"component={region.Index} | confidence={confidence} | " +
                $"overlap={overlap:F4} | topology={topologyMatches} | " +
                $"globalMargin={globalMargin:F4}");
            if (confidence == WDMapBoundaryConfidence.Ambiguous)
            {
                int[] bestCandidates = Enumerable.Range(0, candidateRegions.Length)
                    .OrderByDescending(index => scores[territoryIndex, index])
                    .ThenBy(index => candidateRegions[index].Index).Take(2).ToArray();
                string candidates = string.Join("; ", bestCandidates.Select(index =>
                    $"component={candidateRegions[index].Index}, " +
                    $"score={scores[territoryIndex, index]:F4}, " +
                    $"overlap={overlaps[territoryIndex, index]:F4}, " +
                    $"topology={topology[territoryIndex, index]}, " +
                    $"centroid={candidateRegions[index].Centroid}"));
                string officialNeighbours = neighbours.TryGetValue(territoryId,
                    out HashSet<string> ids) ? string.Join(",", ids.OrderBy(id => id,
                    StringComparer.Ordinal)) : string.Empty;
                Debug.Log($"[WD MASK UNRESOLVED] {map.MapaId}/{territoryId} | " +
                    $"assigned={region.Index} | candidates=[{candidates}] | " +
                    $"officialNeighbours=[{officialNeighbours}]");
            }
        }
        return replacements.Values.OrderBy(item => item.Region.Index).ToArray();
    }

    public static float ComposeCompatibilityScore(float historicalOverlap,
        bool containsAnchor, float normalizedBoundaryDistance, int topologyMatches,
        int regionMatches) => historicalOverlap * 1000f +
        (containsAnchor ? 100f : 0f) + topologyMatches * 8f +
        regionMatches * 0.5f + Mathf.Max(0f, 1f - normalizedBoundaryDistance * 8f);

    public static WDMapBoundaryConfidence ClassifyGlobalEvidence(float globalMargin,
        float historicalOverlap, int topologyMatches)
    {
        if (globalMargin <= 0.01f) return WDMapBoundaryConfidence.Ambiguous;
        if (historicalOverlap >= 0.05f) return WDMapBoundaryConfidence.High;
        return topologyMatches > 0 ? WDMapBoundaryConfidence.Medium :
            WDMapBoundaryConfidence.Low;
    }

    public static IReadOnlyList<WDMapBoundaryAssociation> ApplyHumanAssignments(
        IReadOnlyList<WDMapBoundaryAssociation> associations,
        IReadOnlyDictionary<string, int> assignments)
    {
        if (assignments == null || assignments.Count == 0) return associations;
        if (assignments.Values.Distinct().Count() != assignments.Count)
            throw new InvalidOperationException("Override humano contém componente duplicado.");
        ILookup<string, WDMapBoundaryAssociation> byTerritory =
            associations.ToLookup(item => item.TerritoryId, StringComparer.Ordinal);
        IGrouping<int, WDMapBoundaryAssociation> duplicateComponent = associations
            .GroupBy(item => item.Region.Index).FirstOrDefault(group => group.Count() > 1);
        if (duplicateComponent != null)
            throw new InvalidOperationException("Componente associado a mais de um " +
                "TerritoryId: " + duplicateComponent.Key + ".");
        var byComponent = associations.ToDictionary(item => item.Region.Index);
        if (assignments.Keys.Any(id => !byTerritory.Contains(id)) ||
            assignments.Values.Any(component => !byComponent.ContainsKey(component)))
            throw new InvalidOperationException("Override humano referencia identidade ausente.");
        if (assignments.Keys.Any(id => byTerritory[id].Count() != 1))
            throw new InvalidOperationException("Override humano unitário não pode substituir " +
                "diretamente um TerritoryId multipartes.");
        var previousComponents = new HashSet<int>(assignments.Keys.Select(id =>
            byTerritory[id].Single().Region.Index));
        if (!previousComponents.SetEquals(assignments.Values))
            throw new InvalidOperationException("Override humano tentaria alterar associação " +
                "fora do conjunto explicitamente autorizado.");

        var resolved = associations.Where(item => !assignments.ContainsKey(item.TerritoryId))
            .ToList();
        foreach (KeyValuePair<string, int> assignment in assignments)
            resolved.Add(new WDMapBoundaryAssociation(assignment.Key,
                byComponent[assignment.Value].Region, WDMapBoundaryConfidence.High,
                float.PositiveInfinity));
        return resolved.OrderBy(item => item.Region.Index).ToArray();
    }

    private static void ValidateGeneratedRecords(
        IReadOnlyList<WDMapMigrationBoundaryRecord> records, int officialTerritories,
        int validComponents)
    {
        if (records.Count != officialTerritories ||
            records.Any(record => record.paths == null || record.paths.Count == 0))
            throw new InvalidOperationException("Resultado MASK AUTO não cobre todos os " +
                "TerritoryIds oficiais.");
        int[] components = records.SelectMany(record => record.paths)
            .Select(path => path.componentIndex).ToArray();
        if (components.Length != validComponents ||
            components.Distinct().Count() != components.Length)
            throw new InvalidOperationException("Resultado MASK AUTO não forma uma atribuição " +
                "única e completa dos componentes territoriais.");
    }

    public static IReadOnlyList<Vector2> SimplifyClosed(IReadOnlyList<Vector2> closed,
        float tolerance = SimplificationTolerancePixels)
    {
        if (closed == null || closed.Count < 4) return closed ?? Array.Empty<Vector2>();
        var unique = closed.Take(closed.Count - 1).ToList();
        int start = unique.Select((point, index) => (point, index))
            .OrderBy(item => item.point.y).ThenBy(item => item.point.x).First().index;
        unique = unique.Skip(start).Concat(unique.Take(start)).ToList();
        var open = new List<Vector2>(unique) { unique[0] };
        var keep = new bool[open.Count]; keep[0] = keep[open.Count - 1] = true;
        SimplifyRange(open, 0, open.Count - 1, tolerance * tolerance, keep);
        var result = open.Where((_, index) => keep[index]).ToList();
        if (result.Count < 4) return closed.ToArray();
        result[result.Count - 1] = result[0];
        return result;
    }

    public static bool PointInPolygon(IReadOnlyList<Vector2> polygon, Vector2 point)
    {
        if (polygon == null || polygon.Count < 4) return false;
        bool inside = false;
        for (int i = 0, j = polygon.Count - 2; i < polygon.Count - 1; j = i++)
        {
            Vector2 a = polygon[i], b = polygon[j];
            if ((a.y > point.y) != (b.y > point.y) &&
                point.x < (b.x - a.x) * (point.y - a.y) /
                (b.y - a.y) + a.x) inside = !inside;
        }
        return inside;
    }

    private static IReadOnlyDictionary<string, Vector2> BuildAnchors(DefinicaoMapa map,
        WDMapSpatialSnapshot snapshot, Vector2 canvas)
    {
        Bounds bounds = map.ArteBase != null ? map.ArteBase.bounds :
            new Bounds(Vector3.zero, new Vector3(19.2f, 10.8f, 0f));
        WDMapMigrationCalibrationStore calibrations =
            WDMapMigrationCalibrationRepository.Load();
        return map.Territorios.ToDictionary(item => item.id, item =>
        {
            if (calibrations != null && calibrations.TryGet(map.MapaId, item.id,
                    out WDMapMigrationCalibrationRecord calibration))
            {
                IReadOnlyList<Vector2> transformed =
                    WarDominionMapMigrationFitter.GetPreviewBoundary(item.id,
                        calibration.CandidateTransform);
                if (transformed.Count > 0)
                    return transformed.Aggregate(Vector2.zero, (sum, point) => sum + point) /
                        transformed.Count;
            }
            Vector2 local = snapshot.ToArtLocal(item.id, item.posicaoContador);
            return new Vector2(
                Mathf.InverseLerp(bounds.min.x, bounds.max.x, local.x) * canvas.x,
                Mathf.InverseLerp(bounds.max.y, bounds.min.y, local.y) * canvas.y);
        }, StringComparer.Ordinal);
    }

    private static IReadOnlyList<Vector2> TraceBoundary(HashSet<int> pixels, int width)
    {
        var outgoing = new Dictionary<Vector2Int, List<Vector2Int>>();
        bool Filled(int x, int y) => x >= 0 && y >= 0 && pixels.Contains(y * width + x);
        void Edge(Vector2Int from, Vector2Int to)
        {
            if (!outgoing.TryGetValue(from, out List<Vector2Int> list))
                outgoing[from] = list = new List<Vector2Int>(2);
            list.Add(to);
        }
        foreach (int pixel in pixels)
        {
            int x = pixel % width, y = pixel / width;
            if (!Filled(x, y - 1)) Edge(new Vector2Int(x, y), new Vector2Int(x + 1, y));
            if (!Filled(x + 1, y)) Edge(new Vector2Int(x + 1, y), new Vector2Int(x + 1, y + 1));
            if (!Filled(x, y + 1)) Edge(new Vector2Int(x + 1, y + 1), new Vector2Int(x, y + 1));
            if (!Filled(x - 1, y)) Edge(new Vector2Int(x, y + 1), new Vector2Int(x, y));
        }
        var loops = new List<List<Vector2>>();
        while (outgoing.Count > 0)
        {
            Vector2Int start = outgoing.Keys.OrderBy(item => item.y)
                .ThenBy(item => item.x).First(), current = start;
            var loop = new List<Vector2>();
            int safety = outgoing.Sum(item => item.Value.Count) + 1;
            while (safety-- > 0)
            {
                loop.Add(current);
                if (!outgoing.TryGetValue(current, out List<Vector2Int> nexts) ||
                    nexts.Count == 0) break;
                nexts.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
                Vector2Int next = nexts[0]; nexts.RemoveAt(0);
                if (nexts.Count == 0) outgoing.Remove(current);
                current = next;
                if (current == start) { loop.Add(start); break; }
            }
            if (loop.Count >= 4 && loop[0] == loop[loop.Count - 1]) loops.Add(loop);
        }
        List<Vector2> outer = loops.OrderByDescending(Area).FirstOrDefault();
        return outer == null ? Array.Empty<Vector2>() : SimplifyClosed(outer);
    }

    private static float Area(IReadOnlyList<Vector2> points)
    {
        float area = 0f;
        for (int index = 0; index < points.Count - 1; index++)
            area += points[index].x * points[index + 1].y -
                points[index + 1].x * points[index].y;
        return Mathf.Abs(area) * 0.5f;
    }

    private static void SimplifyRange(IReadOnlyList<Vector2> points, int first, int last,
        float toleranceSquared, bool[] keep)
    {
        if (last <= first + 1) return;
        float maximum = -1f; int selected = -1;
        for (int index = first + 1; index < last; index++)
        {
            float distance = SegmentDistanceSquared(points[index], points[first], points[last]);
            if (distance > maximum) { maximum = distance; selected = index; }
        }
        if (maximum <= toleranceSquared) return;
        keep[selected] = true;
        SimplifyRange(points, first, selected, toleranceSquared, keep);
        SimplifyRange(points, selected, last, toleranceSquared, keep);
    }

    private static float SegmentDistanceSquared(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float length = segment.sqrMagnitude;
        if (length <= Mathf.Epsilon) return (point - start).sqrMagnitude;
        float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / length);
        return (point - (start + segment * t)).sqrMagnitude;
    }

    private static bool IsAmbiguous((string id, WDMapMaskRegion region, float score,
        float normalizedDistance, bool contains) candidate,
        IEnumerable<(string id, WDMapMaskRegion region, float score,
            float normalizedDistance, bool contains)> all,
        HashSet<WDMapMaskRegion> remainingRegions)
    {
        var alternative = all.Where(item => item.id == candidate.id &&
            item.region != candidate.region && remainingRegions.Contains(item.region))
            .OrderByDescending(item => item.score).FirstOrDefault();
        return alternative.region != null &&
            Mathf.Abs(candidate.score - alternative.score) < 0.01f;
    }

    private static WDMapBoundaryConfidence Classify(bool contains, float distance)
    {
        if (contains || distance <= 0.035f) return WDMapBoundaryConfidence.High;
        if (distance <= 0.08f) return WDMapBoundaryConfidence.Medium;
        if (distance <= 0.15f) return WDMapBoundaryConfidence.Low;
        return WDMapBoundaryConfidence.Ambiguous;
    }

    private static void ValidateSinglePartMapResult(
        WDMapBoundaryGenerationResult result, int expectedTerritories,
        int expectedRawComponents, int expectedValidComponents)
    {
        int pathCount = result.Records.Sum(item => item.paths.Count);
        int uniqueComponents = result.Records.SelectMany(item => item.paths)
            .Select(item => item.componentIndex).Distinct().Count();
        bool valid = result.OfficialTerritories == expectedTerritories &&
            result.RawComponents == expectedRawComponents &&
            result.ValidComponents == expectedValidComponents &&
            result.Records.Count == expectedTerritories &&
            pathCount == expectedTerritories &&
            uniqueComponents == expectedValidComponents &&
            result.Records.All(item => item.paths.Count == 1) &&
            result.Unassociated.Count == 0;
        if (!valid)
            throw new InvalidOperationException($"{result.MapId}: geração recusada; " +
                $"esperado {expectedTerritories} territórios single-part, " +
                $"raw={expectedRawComponents}, válidos={expectedValidComponents}; " +
                $"obtido registros={result.Records.Count}, paths={pathCount}, " +
                $"raw={result.RawComponents}, válidos={result.ValidComponents}, " +
                $"não associados={result.Unassociated.Count}.");
    }

    private static Dictionary<string, HashSet<string>> BuildNeighbourMap(
        DefinicaoMapa map)
    {
        var result = map.Territorios.ToDictionary(item => item.id,
            _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (DefinicaoConexaoMapa connection in map.Conexoes)
        {
            if (connection == null || !result.ContainsKey(connection.origemId) ||
                !result.ContainsKey(connection.destinoId)) continue;
            result[connection.origemId].Add(connection.destinoId);
            if (connection.bidirecional)
                result[connection.destinoId].Add(connection.origemId);
        }
        return result;
    }

    private static float HistoricalOverlap(string territoryId, WDMapMaskRegion region,
        DefinicaoMapa map, WDMapSpatialSnapshot snapshot, Vector2 canvas)
    {
        if (!snapshot.TryGetHistoricalPolygons(territoryId,
                out IReadOnlyList<IReadOnlyList<Vector2>> artPolygons)) return 0f;
        Bounds artBounds = map.ArteBase != null ? map.ArteBase.bounds :
            new Bounds(Vector3.zero, new Vector3(19.2f, 10.8f, 0f));
        IReadOnlyList<Vector2[]> polygons = artPolygons.Select(path => path.Select(point =>
            new Vector2(
                Mathf.InverseLerp(artBounds.min.x, artBounds.max.x, point.x) * canvas.x,
                Mathf.InverseLerp(artBounds.max.y, artBounds.min.y, point.y) * canvas.y))
            .Concat(new[]
            {
                new Vector2(
                    Mathf.InverseLerp(artBounds.min.x, artBounds.max.x, path[0].x) * canvas.x,
                    Mathf.InverseLerp(artBounds.max.y, artBounds.min.y, path[0].y) * canvas.y)
            }).ToArray()).ToArray();
        int width = Mathf.RoundToInt(canvas.x);
        int intersection = region.Pixels.Count(pixel =>
        {
            var point = new Vector2(pixel % width + 0.5f, pixel / width + 0.5f);
            return polygons.Any(polygon => PointInPolygon(polygon, point));
        });
        return region.Area <= 0 ? 0f : (float)intersection / region.Area;
    }

    private static float DistanceToBoundary(IReadOnlyList<Vector2> boundary, Vector2 point)
    {
        if (PointInPolygon(boundary, point)) return 0f;
        float minimum = float.PositiveInfinity;
        for (int index = 0; index < boundary.Count - 1; index++)
            minimum = Mathf.Min(minimum, Mathf.Sqrt(SegmentDistanceSquared(point,
                boundary[index], boundary[index + 1])));
        return minimum;
    }

    private static bool AreAdjacent(WDMapMaskRegion first, WDMapMaskRegion second)
    {
        RectInt a = first.Bounds;
        RectInt b = second.Bounds;
        int dx = Mathf.Max(0, Mathf.Max(a.xMin - b.xMax, b.xMin - a.xMax));
        int dy = Mathf.Max(0, Mathf.Max(a.yMin - b.yMax, b.yMin - a.yMax));
        return dx * dx + dy * dy <= 144;
    }

    private static bool AreNearby(WDMapMaskRegion first, WDMapMaskRegion second,
        Vector2 canvas) => Vector2.Distance(first.Centroid, second.Centroid) <=
        canvas.magnitude * 0.22f;

    public static int[] SolveMaximumAssignment(float[,] scores, out float total,
        int forbiddenRow = -1, int forbiddenColumn = -1)
    {
        int count = scores.GetLength(0);
        if (count == 0 || scores.GetLength(1) != count)
        {
            total = 0f;
            return Array.Empty<int>();
        }
        float maximum = scores.Cast<float>().DefaultIfEmpty(0f).Max();
        var u = new double[count + 1];
        var v = new double[count + 1];
        var p = new int[count + 1];
        var way = new int[count + 1];
        for (int row = 1; row <= count; row++)
        {
            p[0] = row;
            int column0 = 0;
            var minimum = Enumerable.Repeat(double.PositiveInfinity, count + 1).ToArray();
            var used = new bool[count + 1];
            do
            {
                used[column0] = true;
                int row0 = p[column0];
                double delta = double.PositiveInfinity;
                int column1 = 0;
                for (int column = 1; column <= count; column++)
                {
                    if (used[column]) continue;
                    double cost = row0 - 1 == forbiddenRow &&
                        column - 1 == forbiddenColumn ? 1e12 :
                        maximum - scores[row0 - 1, column - 1];
                    double current = cost - u[row0] - v[column];
                    if (current < minimum[column])
                    {
                        minimum[column] = current;
                        way[column] = column0;
                    }
                    if (minimum[column] < delta)
                    {
                        delta = minimum[column];
                        column1 = column;
                    }
                }
                for (int column = 0; column <= count; column++)
                    if (used[column])
                    {
                        u[p[column]] += delta;
                        v[column] -= delta;
                    }
                    else minimum[column] -= delta;
                column0 = column1;
            } while (p[column0] != 0);
            do
            {
                int column1 = way[column0];
                p[column0] = p[column1];
                column0 = column1;
            } while (column0 != 0);
        }
        var assignment = Enumerable.Repeat(-1, count).ToArray();
        for (int column = 1; column <= count; column++)
            assignment[p[column] - 1] = column - 1;
        if (assignment.Any(column => column < 0) ||
            (forbiddenRow >= 0 && assignment[forbiddenRow] == forbiddenColumn))
        {
            total = float.NegativeInfinity;
            return Array.Empty<int>();
        }
        total = Enumerable.Range(0, count).Sum(row => scores[row, assignment[row]]);
        return assignment;
    }

    private static string FormatGenerationSummary(WDMapBoundaryGenerationResult result)
    {
        int high = result.Records.Count(item => item.confidence ==
            WDMapBoundaryConfidence.High);
        int medium = result.Records.Count(item => item.confidence ==
            WDMapBoundaryConfidence.Medium);
        int low = result.Records.Count(item => item.confidence ==
            WDMapBoundaryConfidence.Low);
        int ambiguous = result.Records.Count(item => item.confidence ==
            WDMapBoundaryConfidence.Ambiguous);
        return $"[WD MASK BOUNDARY BATCH] {result.MapId} | " +
            $"oficiais={result.OfficialTerritories} | raw={result.RawComponents} | " +
            $"válidos={result.ValidComponents} | descartados={result.DiscardedArtifacts} | " +
            $"paths={result.Records.Sum(item => item.paths.Count)} | HIGH={high} | " +
            $"MEDIUM={medium} | LOW={low} | AMBIGUOUS={ambiguous} | " +
            $"multipartes={result.Records.Count(item => item.paths.Count > 1)} | " +
            $"não associados={result.Unassociated.Count}";
    }

    private static string ComputeHash(byte[] bytes)
    {
        using (SHA256 algorithm = SHA256.Create())
            return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", "")
                .ToLowerInvariant();
    }
}
