using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public enum WDMaskAuditClassification { Green, Yellow, Red }

public sealed class WDMaskComponent
{
    public int Area { get; }
    public RectInt Bounds { get; }
    public Vector2 Centroid { get; }

    public WDMaskComponent(int area, RectInt bounds, Vector2 centroid)
    {
        Area = area;
        Bounds = bounds;
        Centroid = centroid;
    }
}

public sealed class WDMaskAuditResult
{
    public int Width { get; }
    public int Height { get; }
    public int ExpectedTerritories { get; }
    public IReadOnlyList<WDMaskComponent> Components { get; }
    public int SignificantComponents { get; }
    public int SmallComponents { get; }
    public bool AlphaConsistent { get; }
    public bool ResolutionValid => Width == 1920 && Height == 1080;
    public bool PotentialMerge => SignificantComponents < ExpectedTerritories;
    public bool PotentialFragmentation => SignificantComponents > ExpectedTerritories;
    public WDMaskAuditClassification Classification { get; }

    public WDMaskAuditResult(int width, int height, int expectedTerritories,
        IReadOnlyList<WDMaskComponent> components, int minimumSignificantArea,
        bool alphaConsistent)
    {
        Width = width;
        Height = height;
        ExpectedTerritories = expectedTerritories;
        Components = components;
        SignificantComponents = components.Count(item =>
            item.Area >= minimumSignificantArea);
        SmallComponents = components.Count - SignificantComponents;
        AlphaConsistent = alphaConsistent;
        if (!ResolutionValid || !alphaConsistent || SignificantComponents < expectedTerritories)
            Classification = WDMaskAuditClassification.Red;
        else if (SignificantComponents == expectedTerritories && SmallComponents <= 1)
            Classification = WDMaskAuditClassification.Green;
        else
            Classification = WDMaskAuditClassification.Yellow;
    }
}

public static class WDMapTerritoryMaskAuditor
{
    public static readonly Color32 CanonicalFill = new Color32(241, 226, 197, 255);
    public const int MinimumSignificantArea = 500;

    public static WDMaskAuditResult Analyze(Color32[] pixels, int width, int height,
        int expectedTerritories, byte tolerance = 0)
    {
        if (pixels == null || pixels.Length != width * height)
            throw new ArgumentException("Pixel buffer does not match mask dimensions.",
                nameof(pixels));
        var foreground = new bool[pixels.Length];
        bool alphaConsistent = true;
        for (int index = 0; index < pixels.Length; index++)
        {
            Color32 pixel = pixels[index];
            bool canonicalColor =
                Math.Abs(pixel.r - CanonicalFill.r) <= tolerance &&
                Math.Abs(pixel.g - CanonicalFill.g) <= tolerance &&
                Math.Abs(pixel.b - CanonicalFill.b) <= tolerance;
            if (canonicalColor) alphaConsistent &= pixel.a == CanonicalFill.a;
            foreground[index] = canonicalColor && pixel.a == CanonicalFill.a;
        }

        var visited = new bool[pixels.Length];
        var queue = new int[pixels.Length];
        var components = new List<WDMaskComponent>();
        for (int start = 0; start < foreground.Length; start++)
        {
            if (!foreground[start] || visited[start]) continue;
            int read = 0;
            int write = 0;
            queue[write++] = start;
            visited[start] = true;
            int minimumX = start % width, maximumX = minimumX;
            int minimumY = start / width, maximumY = minimumY;
            long sumX = 0, sumY = 0;
            while (read < write)
            {
                int current = queue[read++];
                int x = current % width;
                int y = current / width;
                sumX += x;
                sumY += y;
                minimumX = Math.Min(minimumX, x);
                maximumX = Math.Max(maximumX, x);
                minimumY = Math.Min(minimumY, y);
                maximumY = Math.Max(maximumY, y);
                TryEnqueue(x > 0 ? current - 1 : -1, foreground, visited,
                    queue, ref write);
                TryEnqueue(x + 1 < width ? current + 1 : -1, foreground, visited,
                    queue, ref write);
                TryEnqueue(y > 0 ? current - width : -1, foreground, visited,
                    queue, ref write);
                TryEnqueue(y + 1 < height ? current + width : -1, foreground,
                    visited, queue, ref write);
            }
            components.Add(new WDMaskComponent(write,
                new RectInt(minimumX, minimumY, maximumX - minimumX + 1,
                    maximumY - minimumY + 1),
                new Vector2((float)sumX / write, (float)sumY / write)));
        }
        components.Sort((first, second) => second.Area.CompareTo(first.Area));
        return new WDMaskAuditResult(width, height, expectedTerritories, components,
            MinimumSignificantArea, alphaConsistent);
    }

    private static void TryEnqueue(int index, bool[] foreground, bool[] visited,
        int[] queue, ref int write)
    {
        if (index < 0 || !foreground[index] || visited[index]) return;
        visited[index] = true;
        queue[write++] = index;
    }
}

public static class WDMapTerritoryMaskAuditCommand
{
    private readonly struct Source
    {
        public readonly string MapAssetPath;
        public readonly string MaskPath;

        public Source(string mapAssetPath, string maskPath)
        {
            MapAssetPath = mapAssetPath;
            MaskPath = maskPath;
        }
    }

    private static readonly Source[] Sources =
    {
        new Source("Assets/Resources/Mapas/Classic.asset",
            "Assets/Mapas/Classic/Migration/WD_Map_Classic_TerritoryMask_16x9_v01.png"),
        new Source("Assets/Resources/Mapas/PearlHarbor.asset",
            "Assets/Mapas/Pearl Harbor/Migration/WD_Map_PearlHarbor_TerritoryMask_16x9_v01.png"),
        new Source("Assets/Resources/Mapas/Riachuelo.asset",
            "Assets/Mapas/Batalha do Riachuelo/Migration/WD_Map_Riachuelo_TerritoryMask_16x9_v01.png"),
        new Source("Assets/Resources/Mapas/Peloponeso.asset",
            "Assets/Mapas/Peloponeso/Migration/WD_Map_Peloponeso_TerritoryMask_16x9_v01.png"),
        new Source("Assets/Resources/Mapas/DarkWorld.asset",
            "Assets/Mapas/Dark World/Migration/WD_Map_DarkWorld_TerritoryMask_16x9_v01.png")
    };

    [MenuItem("War Dominion/Editor de Mapas/Auditar Territory Masks 16:9", priority = 3)]
    public static void AuditAll()
    {
        foreach (Source source in Sources)
        {
            DefinicaoMapa map = AssetDatabase.LoadAssetAtPath<DefinicaoMapa>(
                source.MapAssetPath);
            if (map == null || !File.Exists(source.MaskPath))
            {
                Debug.LogError($"[WD MASK AUDIT] Ausente: {source.MapAssetPath} | " +
                    source.MaskPath);
                continue;
            }
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false,
                true) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                if (!ImageConversion.LoadImage(texture,
                        File.ReadAllBytes(source.MaskPath), false))
                {
                    Debug.LogError($"[WD MASK AUDIT] Máscara não carregável: " +
                        source.MaskPath);
                    continue;
                }
                WDMaskAuditResult result = WDMapTerritoryMaskAuditor.Analyze(
                    texture.GetPixels32(), texture.width, texture.height,
                    map.Territorios.Count);
                Debug.Log($"[WD MASK AUDIT] {map.MapaId} | {source.MaskPath} | " +
                    $"{result.Width}x{result.Height} | oficial={map.Territorios.Count} | " +
                    $"brutos={result.Components.Count} | relevantes=" +
                    $"{result.SignificantComponents} | pequenos={result.SmallComponents} | " +
                    $"merge={result.PotentialMerge} | fragmentacao/multipartes=" +
                    $"{result.PotentialFragmentation} | alpha={result.AlphaConsistent} | " +
                    $"{result.Classification.ToString().ToUpperInvariant()}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
