using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public enum WDMapMigrationConfidence { High, Medium, Low, Unresolved }

[Serializable]
public struct WDMapMigrationTransform
{
    public Vector2 Scale;
    public Vector2 OffsetPixels;
    public float RotationDegrees;

    public WDMapMigrationTransform(Vector2 scale, Vector2 offsetPixels,
        float rotationDegrees = 0f)
    {
        Scale = scale;
        OffsetPixels = offsetPixels;
        RotationDegrees = rotationDegrees;
    }

    public Vector2 TransformPoint(Vector2 point)
    {
        float radians = RotationDegrees * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(radians);
        float sine = Mathf.Sin(radians);
        Vector2 scaled = Vector2.Scale(point, Scale);
        return new Vector2(scaled.x * cosine - scaled.y * sine,
            scaled.x * sine + scaled.y * cosine) + OffsetPixels;
    }
}

public sealed class WDMapMigrationFitResult
{
    public string TerritoryId { get; }
    public string DisplayName { get; }
    public WDMapMigrationTransform OriginalTransform { get; }
    public WDMapMigrationTransform InitialCandidateTransform { get; }
    public WDMapMigrationTransform CandidateTransform { get; private set; }
    public WDMapMigrationConfidence Confidence { get; }
    public float Score { get; }
    public string Diagnostic { get; }
    public bool Accepted { get; set; }
    public bool Rejected { get; set; }

    public WDMapMigrationFitResult(string territoryId, string displayName,
        WDMapMigrationTransform originalTransform,
        WDMapMigrationTransform candidateTransform, float score,
        WDMapMigrationConfidence confidence, string diagnostic)
    {
        TerritoryId = territoryId;
        DisplayName = displayName;
        OriginalTransform = originalTransform;
        InitialCandidateTransform = candidateTransform;
        CandidateTransform = candidateTransform;
        Score = score;
        Confidence = confidence;
        Diagnostic = diagnostic;
    }

    public void AdjustCandidate(WDMapMigrationTransform candidateTransform)
    {
        CandidateTransform = candidateTransform;
        Accepted = false;
        Rejected = false;
    }

    public void ResetCandidate() => AdjustCandidate(InitialCandidateTransform);
}

public sealed class WDMapMigrationAnalysis
{
    public IReadOnlyList<WDMapMigrationFitResult> Results { get; }
    public int Total => Results.Count;
    public int AcceptedCount => Results.Count(item => item.Accepted);

    public WDMapMigrationAnalysis(IEnumerable<WDMapMigrationFitResult> results) =>
        Results = (results ?? Array.Empty<WDMapMigrationFitResult>()).ToArray();

    public int Count(WDMapMigrationConfidence confidence) =>
        Results.Count(item => item.Confidence == confidence);

    public int AcceptAllHigh()
    {
        int accepted = 0;
        foreach (WDMapMigrationFitResult result in Results)
        {
            if (result.Confidence != WDMapMigrationConfidence.High || result.Accepted)
                continue;
            result.Accepted = true;
            result.Rejected = false;
            accepted++;
        }
        return accepted;
    }

    public void Reset(WDMapMigrationFitResult result)
    {
        if (result == null || !Results.Contains(result)) return;
        result.Accepted = false;
        result.Rejected = false;
    }
}

public static class WarDominionMapMigrationFitter
{
    private const int SearchRadiusPixels = 16;
    private const int TranslationStepPixels = 4;
    private static readonly float[] LocalScaleFactors = { 0.97f, 1f, 1.03f };

    // ============================================================
    // 01. API EDITOR-SIDE: ANÁLISE EXPLÍCITA E CANCELÁVEL
    // ============================================================

    public static WDMapMigrationAnalysis Analyze(DefinicaoMapa map, Sprite candidate,
        WDMapMigrationTransform globalTransform, Func<int, int, string, bool> progress = null)
    {
        if (map == null || candidate == null)
            return new WDMapMigrationAnalysis(Array.Empty<WDMapMigrationFitResult>());

        float[,] edgeMap = BuildEdgeMap(ReadTexture(candidate));
        var results = new List<WDMapMigrationFitResult>();
        for (int index = 0; index < map.Territorios.Count; index++)
        {
            DefinicaoTerritorioMapa territory = map.Territorios[index];
            if (progress != null && !progress(index, map.Territorios.Count, territory.id))
                break;
            bool[,] mask = TryReadClassicMask(territory.id);
            results.Add(mask == null
                ? Unresolved(territory, globalTransform, "Máscara seed não encontrada.")
                : Fit(territory.id, territory.nomeExibido, mask, edgeMap, globalTransform));
        }
        return new WDMapMigrationAnalysis(results);
    }

    // ============================================================
    // 02. FIT DETERMINÍSTICO: BORDA + PROXIMIDADE + LIMITES
    // ============================================================

    public static WDMapMigrationFitResult Fit(string territoryId, string displayName,
        bool[,] sourceMask, float[,] targetEdges, WDMapMigrationTransform globalTransform)
    {
        if (sourceMask == null || targetEdges == null)
            return new WDMapMigrationFitResult(territoryId, displayName, globalTransform,
                globalTransform, 0f, WDMapMigrationConfidence.Unresolved,
                "Seed ou edge map ausente.");

        List<Vector2> boundary = ExtractBoundary(sourceMask, 480);
        // Um polígono fechado válido exige três vértices únicos e a repetição
        // do primeiro ponto. O limite antigo de oito pertencia à nuvem de
        // pixels não ordenada usada antes da extração por loops.
        if (boundary.Count < 4 || boundary[0] != boundary[boundary.Count - 1])
            return new WDMapMigrationFitResult(territoryId, displayName, globalTransform,
                globalTransform, 0f, WDMapMigrationConfidence.Unresolved,
                "Contorno seed insuficiente.");

        float bestScore = -1f;
        WDMapMigrationTransform best = globalTransform;
        foreach (float localX in LocalScaleFactors)
        foreach (float localY in LocalScaleFactors)
        for (int dy = -SearchRadiusPixels; dy <= SearchRadiusPixels; dy += TranslationStepPixels)
        for (int dx = -SearchRadiusPixels; dx <= SearchRadiusPixels; dx += TranslationStepPixels)
        {
            var candidate = new WDMapMigrationTransform(
                Vector2.Scale(globalTransform.Scale, new Vector2(localX, localY)),
                globalTransform.OffsetPixels + new Vector2(dx, dy));
            float score = Score(boundary, targetEdges, candidate, globalTransform);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        WDMapMigrationConfidence confidence = Classify(bestScore);
        string diagnostic = confidence == WDMapMigrationConfidence.High
            ? "Encaixe local forte dentro dos limites conservadores."
            : confidence == WDMapMigrationConfidence.Medium
                ? "Encaixe provável; revisar visualmente antes de aceitar."
                : confidence == WDMapMigrationConfidence.Low
                    ? "Bordas ambíguas ou decorativas reduziram a confiança."
                    : "Nenhuma correspondência local segura foi encontrada.";
        return new WDMapMigrationFitResult(territoryId, displayName, globalTransform,
            best, Mathf.Clamp01(bestScore), confidence, diagnostic);
    }

    public static WDMapMigrationConfidence Classify(float score)
    {
        if (score >= 0.72f) return WDMapMigrationConfidence.High;
        if (score >= 0.52f) return WDMapMigrationConfidence.Medium;
        if (score >= 0.32f) return WDMapMigrationConfidence.Low;
        return WDMapMigrationConfidence.Unresolved;
    }

    public static IReadOnlyList<Vector2> GetPreviewBoundary(string territoryId,
        WDMapMigrationTransform transform, int maximumSamples = 720)
    {
        bool[,] mask = TryReadClassicMask(territoryId);
        if (mask == null) return Array.Empty<Vector2>();
        return ExtractBoundary(mask, maximumSamples)
            .Select(transform.TransformPoint).ToArray();
    }

    private static float Score(IReadOnlyList<Vector2> boundary, float[,] edges,
        WDMapMigrationTransform candidate, WDMapMigrationTransform global)
    {
        int height = edges.GetLength(0);
        int width = edges.GetLength(1);
        float edgeScore = 0f;
        int inside = 0;
        foreach (Vector2 source in boundary)
        {
            Vector2 point = candidate.TransformPoint(source);
            int x = Mathf.RoundToInt(point.x);
            int y = Mathf.RoundToInt(point.y);
            if (x < 0 || y < 0 || x >= width || y >= height) continue;
            inside++;
            float local = 0f;
            for (int oy = -2; oy <= 2; oy++)
            for (int ox = -2; ox <= 2; ox++)
                if (x + ox >= 0 && y + oy >= 0 && x + ox < width && y + oy < height)
                    local = Mathf.Max(local, edges[y + oy, x + ox]);
            edgeScore += local;
        }
        if (inside == 0) return 0f;
        float coverage = (float)inside / boundary.Count;
        float displacement = Vector2.Distance(candidate.OffsetPixels, global.OffsetPixels) /
            (SearchRadiusPixels * 1.414214f);
        Vector2 relativeScale = new Vector2(candidate.Scale.x / global.Scale.x,
            candidate.Scale.y / global.Scale.y);
        float scalePenalty = (Mathf.Abs(relativeScale.x - 1f) +
            Mathf.Abs(relativeScale.y - 1f)) / 0.06f;
        return Mathf.Clamp01((edgeScore / inside) * 0.78f + coverage * 0.22f -
            displacement * 0.05f - scalePenalty * 0.04f);
    }

    // ============================================================
    // 03. EXTRAÇÃO DE SEED E EDGE MAP SEM ASSET DERIVADO
    // ============================================================

    private static bool[,] TryReadClassicMask(string territoryId)
    {
        string path = $"Assets/TerritoriosDefinitivos/{territoryId}.png";
        if (!File.Exists(path)) return null;
        Texture2D texture = ReadPng(path);
        try
        {
            Color32[] pixels = texture.GetPixels32();
            var mask = new bool[texture.height, texture.width];
            for (int y = 0; y < texture.height; y++)
            for (int x = 0; x < texture.width; x++)
                mask[texture.height - 1 - y, x] = pixels[y * texture.width + x].a >= 128;
            return mask;
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    private static Texture2D ReadTexture(Sprite sprite)
    {
        string path = AssetDatabase.GetAssetPath(sprite);
        return ReadPng(path);
    }

    private static Texture2D ReadPng(string assetPath)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(assetPath), false))
        {
            UnityEngine.Object.DestroyImmediate(texture);
            throw new InvalidOperationException("Falha ao ler imagem: " + assetPath);
        }
        return texture;
    }

    private static float[,] BuildEdgeMap(Texture2D texture)
    {
        try
        {
            int width = texture.width;
            int height = texture.height;
            Color32[] pixels = texture.GetPixels32();
            var luminance = new float[height, width];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Color32 color = pixels[(height - 1 - y) * width + x];
                luminance[y, x] = (color.r * 0.2126f + color.g * 0.7152f +
                    color.b * 0.0722f) / 255f;
            }
            var edges = new float[height, width];
            for (int y = 1; y < height - 1; y++)
            for (int x = 1; x < width - 1; x++)
            {
                float gradientX = Mathf.Abs(luminance[y, x + 1] - luminance[y, x - 1]);
                float gradientY = Mathf.Abs(luminance[y + 1, x] - luminance[y - 1, x]);
                float darkness = 1f - luminance[y, x];
                edges[y, x] = Mathf.Clamp01((gradientX + gradientY) * 1.8f + darkness * 0.18f);
            }
            return edges;
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    private static List<Vector2> ExtractBoundary(bool[,] mask, int maximumSamples)
    {
        int height = mask.GetLength(0);
        int width = mask.GetLength(1);
        var outgoing = new Dictionary<Vector2Int, List<Vector2Int>>();
        void AddEdge(Vector2Int from, Vector2Int to)
        {
            if (!outgoing.TryGetValue(from, out List<Vector2Int> destinations))
            {
                destinations = new List<Vector2Int>(2);
                outgoing.Add(from, destinations);
            }
            destinations.Add(to);
        }

        bool Filled(int x, int y) => x >= 0 && y >= 0 && x < width && y < height &&
            mask[y, x];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (!mask[y, x]) continue;
            if (!Filled(x, y - 1)) AddEdge(new Vector2Int(x, y),
                new Vector2Int(x + 1, y));
            if (!Filled(x + 1, y)) AddEdge(new Vector2Int(x + 1, y),
                new Vector2Int(x + 1, y + 1));
            if (!Filled(x, y + 1)) AddEdge(new Vector2Int(x + 1, y + 1),
                new Vector2Int(x, y + 1));
            if (!Filled(x - 1, y)) AddEdge(new Vector2Int(x, y + 1),
                new Vector2Int(x, y));
        }

        var loops = new List<List<Vector2>>();
        while (outgoing.Count > 0)
        {
            Vector2Int start = outgoing.Keys.OrderBy(point => point.y)
                .ThenBy(point => point.x).First();
            Vector2Int current = start;
            var loop = new List<Vector2>();
            int safety = outgoing.Sum(pair => pair.Value.Count) + 1;
            while (safety-- > 0)
            {
                loop.Add(current);
                if (!outgoing.TryGetValue(current, out List<Vector2Int> destinations) ||
                    destinations.Count == 0) break;
                destinations.Sort((first, second) => first.y != second.y
                    ? first.y.CompareTo(second.y) : first.x.CompareTo(second.x));
                Vector2Int next = destinations[0];
                destinations.RemoveAt(0);
                if (destinations.Count == 0) outgoing.Remove(current);
                current = next;
                if (current == start)
                {
                    loop.Add(start);
                    break;
                }
            }
            if (loop.Count >= 4 && loop[0] == loop[loop.Count - 1]) loops.Add(loop);
        }

        List<Vector2> boundary = loops.OrderByDescending(AbsoluteArea).FirstOrDefault();
        if (boundary == null) return new List<Vector2>();
        boundary = RemoveCollinearPoints(boundary);
        if (boundary.Count <= maximumSamples) return boundary;
        int uniqueCount = boundary.Count - 1;
        int stride = Mathf.CeilToInt((float)uniqueCount / (maximumSamples - 1));
        var sampled = boundary.Take(uniqueCount).Where((_, index) => index % stride == 0)
            .Take(maximumSamples - 1).ToList();
        sampled.Add(sampled[0]);
        return sampled;
    }

    private static float AbsoluteArea(IReadOnlyList<Vector2> polygon)
    {
        float twiceArea = 0f;
        for (int index = 0; index < polygon.Count - 1; index++)
            twiceArea += polygon[index].x * polygon[index + 1].y -
                polygon[index + 1].x * polygon[index].y;
        return Mathf.Abs(twiceArea) * 0.5f;
    }

    private static List<Vector2> RemoveCollinearPoints(IReadOnlyList<Vector2> closed)
    {
        int uniqueCount = closed.Count - 1;
        var result = new List<Vector2>();
        for (int index = 0; index < uniqueCount; index++)
        {
            Vector2 previous = closed[(index - 1 + uniqueCount) % uniqueCount];
            Vector2 current = closed[index];
            Vector2 next = closed[(index + 1) % uniqueCount];
            Vector2 first = current - previous;
            Vector2 second = next - current;
            if (!Mathf.Approximately(first.x * second.y - first.y * second.x, 0f))
                result.Add(current);
        }
        if (result.Count > 0) result.Add(result[0]);
        return result;
    }

    private static WDMapMigrationFitResult Unresolved(DefinicaoTerritorioMapa territory,
        WDMapMigrationTransform global, string diagnostic) =>
        new WDMapMigrationFitResult(territory.id, territory.nomeExibido, global, global,
            0f, WDMapMigrationConfidence.Unresolved, diagnostic);
}
