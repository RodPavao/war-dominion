#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Captures the effective runtime presentation of the Profile UI without changing it.
/// Reports are written outside Assets so the diagnostic never creates imported content.
/// </summary>
public static class WDProfileRuntimeColorDiagnostic
{
    private const string MenuPath = "War Dominion/UI/Profile/Capture Runtime Color Diagnostics";
    private const string LogPrefix = "[WD-PROFILE-COLOR-DIAGNOSTIC]";

    private static readonly Dictionary<string, ExpectedColor> ExpectedByName =
        new Dictionary<string, ExpectedColor>(StringComparer.OrdinalIgnoreCase)
        {
            { "IdentityPanel", new ExpectedColor("Painel Identidade", "#08111D", 0.949f) },
            { "Metric_0", new ExpectedColor("Card Estatisticas", "#050C16", 1f) },
            { "Metric_1", new ExpectedColor("Card Estatisticas", "#050C16", 1f) },
            { "Metric_2", new ExpectedColor("Card Estatisticas", "#050C16", 1f) },
            { "Metric_3", new ExpectedColor("Card Estatisticas", "#050C16", 1f) },
            { "Achievement_0", new ExpectedColor("Card Conquistas", "#050C16", 0.902f) },
            { "Achievement_1", new ExpectedColor("Card Conquistas", "#050C16", 0.902f) },
            { "Achievement_2", new ExpectedColor("Card Conquistas", "#050C16", 0.902f) },
            { "Achievement_3", new ExpectedColor("Card Conquistas", "#050C16", 0.902f) },
            { "Header", new ExpectedColor("Header", "#070A0F", 0.84f) }
        };

    // 1. The menu remains disabled outside Play Mode because runtime ancestry and CanvasRenderer values
    // only have diagnostic meaning after the Profile has been created and laid out by the Home flow.
    [MenuItem(MenuPath, true)]
    private static bool ValidateCapture()
    {
        return EditorApplication.isPlaying && !EditorApplication.isPaused;
    }

    [MenuItem(MenuPath, false, 2310)]
    private static void Capture()
    {
        WDHomeProfilePanel profile = UnityEngine.Object
            .FindObjectsByType<WDHomeProfilePanel>(FindObjectsInactive.Include)
            .FirstOrDefault(candidate => candidate.gameObject.activeInHierarchy);
        if (profile == null || !profile.gameObject.activeInHierarchy)
        {
            Debug.LogError($"{LogPrefix} Abra o Perfil na Home durante o Play Mode antes de executar o diagnostico.");
            return;
        }

        try
        {
            string outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "Diagnostics"));
            Directory.CreateDirectory(outputDirectory);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string reportPath = Path.Combine(outputDirectory, $"WDProfileRuntimeColorDiagnostic_{stamp}.txt");
            string latestPath = Path.Combine(outputDirectory, "WDProfileRuntimeColorDiagnostic_latest.txt");
            string screenshotPath = Path.Combine(outputDirectory, $"WDProfileRuntimeColorDiagnostic_{stamp}.png");

            Texture2D screenshot = CaptureGameView(screenshotPath);
            string report = BuildReport(profile, screenshot, screenshotPath);
            File.WriteAllText(reportPath, report, new UTF8Encoding(false));
            File.WriteAllText(latestPath, report, new UTF8Encoding(false));

            if (screenshot != null)
                UnityEngine.Object.DestroyImmediate(screenshot);

            Debug.Log($"{LogPrefix} Relatorio salvo em: {reportPath}");
            EditorUtility.RevealInFinder(reportPath);
        }
        catch (Exception exception)
        {
            Debug.LogException(new InvalidOperationException(
                $"{LogPrefix} Falha ao gerar o relatorio. Nenhum componente da UI foi alterado.", exception));
        }
    }

    // 2. The report records source colors in both storage spaces, accumulated opacity, renderer state,
    // assets and draw order so assigned values can be separated from the final composited pixel.
    private static string BuildReport(WDHomeProfilePanel profile, Texture2D screenshot, string screenshotPath)
    {
        var builder = new StringBuilder(128 * 1024);
        Graphic[] graphics = profile.GetComponentsInChildren<Graphic>(true);
        Canvas[] canvases = profile.GetComponentsInParent<Canvas>(true);
        Canvas rootCanvas = canvases.FirstOrDefault(canvas => canvas.isRootCanvas) ?? canvases.FirstOrDefault();
        Camera eventCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;

        builder.AppendLine("WAR DOMINION - DIAGNOSTICO DE RUNTIME DAS CORES DO PERFIL");
        builder.AppendLine($"Gerado em: {DateTime.Now:O}");
        builder.AppendLine($"Unity: {Application.unityVersion}");
        builder.AppendLine($"Color Space ativo: {QualitySettings.activeColorSpace}");
        builder.AppendLine($"Screen: {Screen.width}x{Screen.height} @ {Screen.dpi:0.##} dpi");
        builder.AppendLine($"Safe Area: {FormatRect(Screen.safeArea)}");
        builder.AppendLine($"Perfil: {HierarchyPath(profile.transform)}");
        builder.AppendLine($"Graphics encontrados: {graphics.Length}");
        builder.AppendLine($"Captura: {(screenshot != null ? screenshotPath : "indisponivel")}");
        builder.AppendLine();

        AppendCanvasSection(builder, rootCanvas);
        AppendExpectedSection(builder);
        AppendEquivalenceSection(builder, graphics);

        builder.AppendLine("=== COMPONENTES ===");
        foreach (Graphic graphic in graphics.OrderBy(item => item.depth).ThenBy(item => HierarchyPath(item.transform)))
        {
            AppendGraphic(builder, graphic, graphics, screenshot, eventCamera);
        }

        builder.AppendLine();
        builder.AppendLine("=== INTERPRETACAO ===");
        builder.AppendLine("RawRGBA e CanvasRendererRGBA sao os valores armazenados/entregues ao Canvas.");
        builder.AppendLine("GammaView converte o RGB armazenado para sRGB apenas para comparacao numerica com o hexadecimal do Figma.");
        builder.AppendLine("PixelFinal e a amostra ja composta da Game View e inclui background, veil, superficies e blending.");
        builder.AppendLine("Uma amostra marcada OCCLUDED nao deve ser usada para concluir a cor da superficie.");
        builder.AppendLine("A ferramenta nao altera Graphic, CanvasRenderer, CanvasGroup, material, shader ou layout.");
        return builder.ToString();
    }

    private static void AppendCanvasSection(StringBuilder builder, Canvas canvas)
    {
        builder.AppendLine("=== PIPELINE E CANVAS ===");
        if (canvas == null)
        {
            builder.AppendLine("Canvas raiz nao encontrado.");
            builder.AppendLine();
            return;
        }

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        builder.AppendLine($"Canvas: {HierarchyPath(canvas.transform)}");
        builder.AppendLine($"RenderMode={canvas.renderMode}; scaleFactor={canvas.scaleFactor:0.####}; pixelPerfect={canvas.pixelPerfect}; " +
                           $"sortingLayer={canvas.sortingLayerName}; sortingOrder={canvas.sortingOrder}; renderOrder={canvas.renderOrder}");
        if (scaler != null)
        {
            builder.AppendLine($"CanvasScaler: mode={scaler.uiScaleMode}; referenceResolution={scaler.referenceResolution}; " +
                               $"match={scaler.matchWidthOrHeight:0.####}; referencePPU={scaler.referencePixelsPerUnit:0.####}");
        }
        builder.AppendLine();
    }

    private static void AppendExpectedSection(StringBuilder builder)
    {
        builder.AppendLine("=== REFERENCIAS FIGMA CONFIRMADAS ===");
        builder.AppendLine("Painel Identidade: RGB #08111D; alpha 94,9%");
        builder.AppendLine("Card de Estatisticas: RGB #050C16; alpha 100%");
        builder.AppendLine("Header: RGB #070A0F; alpha 84%");
        builder.AppendLine("Card de Conquistas: RGB #050C16; alpha 90,2%");
        builder.AppendLine();
    }

    // 3. Equivalent named components are grouped to expose divergent runtime configuration without
    // assuming that a shared visual role necessarily has a shared parent or creation path.
    private static void AppendEquivalenceSection(StringBuilder builder, Graphic[] graphics)
    {
        builder.AppendLine("=== EQUIVALENCIA DE COMPONENTES ===");
        string[][] groups =
        {
            new[] { "Metric_0", "Metric_1", "Metric_2", "Metric_3" },
            new[] { "Achievement_0", "Achievement_1", "Achievement_2", "Achievement_3" },
            new[] { "IdentityPanel", "Statistics", "Performance", "CurrentDivision", "Achievements" }
        };

        foreach (string[] group in groups)
        {
            builder.AppendLine($"Grupo: {string.Join(", ", group)}");
            foreach (string name in group)
            {
                Graphic graphic = FindSurfaceGraphic(graphics, name);
                builder.AppendLine(graphic == null
                    ? $"  {name}: NAO ENCONTRADO"
                    : $"  {name}: {ColorSummary(graphic.color)}; renderer={ColorSummary(graphic.canvasRenderer.GetColor())}; " +
                      $"alphaAcumulado={AccumulatedCanvasGroupAlpha(graphic.transform, null):0.####}; material={MaterialSummary(graphic.materialForRendering)}");
            }
        }
        builder.AppendLine();
    }

    private static void AppendGraphic(
        StringBuilder builder,
        Graphic graphic,
        Graphic[] allGraphics,
        Texture2D screenshot,
        Camera eventCamera)
    {
        RectTransform rect = graphic.rectTransform;
        var canvasGroups = new List<string>();
        float groupAlpha = AccumulatedCanvasGroupAlpha(graphic.transform, canvasGroups);
        CanvasRenderer renderer = graphic.canvasRenderer;
        Material material = graphic.materialForRendering;
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);

        builder.AppendLine("---");
        builder.AppendLine($"Path: {HierarchyPath(graphic.transform)}");
        builder.AppendLine($"Type={graphic.GetType().Name}; activeSelf={graphic.gameObject.activeSelf}; activeInHierarchy={graphic.gameObject.activeInHierarchy}; " +
                           $"enabled={graphic.enabled}; raycast={graphic.raycastTarget}");
        builder.AppendLine($"Depth={graphic.depth}; siblingIndex={graphic.transform.GetSiblingIndex()}; rendererCull={renderer.cull}; rendererMaterialCount={renderer.materialCount}");
        builder.AppendLine($"RawRGBA={ColorSummary(graphic.color)}");
        builder.AppendLine($"CanvasRendererRGBA={ColorSummary(renderer.GetColor())}; CanvasRendererAlpha={renderer.GetAlpha():0.####}; " +
                           $"CanvasRendererInheritedAlpha={renderer.GetInheritedAlpha():0.####}");
        builder.AppendLine($"CanvasGroupAccumulatedAlpha={groupAlpha:0.####}; chain={(canvasGroups.Count == 0 ? "nenhum" : string.Join(" | ", canvasGroups))}");
        builder.AppendLine($"Material={MaterialSummary(material)}");
        builder.AppendLine($"Rect={FormatRect(rect.rect)}; anchoredPosition={FormatVector(rect.anchoredPosition)}; sizeDelta={FormatVector(rect.sizeDelta)}; " +
                           $"anchorMin={FormatVector(rect.anchorMin)}; anchorMax={FormatVector(rect.anchorMax)}; pivot={FormatVector(rect.pivot)}; lossyScale={FormatVector(rect.lossyScale)}");
        builder.AppendLine($"WorldCorners={string.Join("; ", corners.Select(FormatVector))}");

        if (graphic is Image image)
        {
            builder.AppendLine($"Image: sprite={AssetSummary(image.sprite)}; type={image.type}; preserveAspect={image.preserveAspect}; " +
                               $"fillMethod={image.fillMethod}; fillAmount={image.fillAmount:0.####}; pixelsPerUnitMultiplier={image.pixelsPerUnitMultiplier:0.####}");
        }
        else if (graphic is RawImage rawImage)
        {
            builder.AppendLine($"RawImage: texture={AssetSummary(rawImage.texture)}; uvRect={FormatRect(rawImage.uvRect)}");
        }
        else if (graphic is TMP_Text text)
        {
            builder.AppendLine($"TMP: text='{Compact(text.text)}'; font={AssetSummary(text.font)}; fontMaterial={MaterialSummary(text.fontSharedMaterial)}; " +
                               $"fontSize={text.fontSize:0.##}; alignment={text.alignment}");
        }

        AppendEffectsAndMasks(builder, graphic);
        AppendExpectedComparison(builder, graphic);
        AppendOverlayChildren(builder, graphic);
        AppendPixelSamples(builder, graphic, allGraphics, screenshot, eventCamera);
    }

    private static void AppendEffectsAndMasks(StringBuilder builder, Graphic graphic)
    {
        UnityEngine.UI.Outline[] outlines = graphic.GetComponents<UnityEngine.UI.Outline>();
        foreach (UnityEngine.UI.Outline outline in outlines)
        {
            builder.AppendLine($"Outline: enabled={outline.enabled}; effectColor={ColorSummary(outline.effectColor)}; " +
                               $"effectDistance={FormatVector(outline.effectDistance)}; useGraphicAlpha={outline.useGraphicAlpha}");
        }

        Shadow[] shadows = graphic.GetComponents<Shadow>();
        foreach (Shadow shadow in shadows.Where(item => !(item is UnityEngine.UI.Outline)))
        {
            builder.AppendLine($"Shadow: enabled={shadow.enabled}; effectColor={ColorSummary(shadow.effectColor)}; " +
                               $"effectDistance={FormatVector(shadow.effectDistance)}; useGraphicAlpha={shadow.useGraphicAlpha}");
        }

        var clipping = new List<string>();
        Transform current = graphic.transform;
        while (current != null)
        {
            Mask mask = current.GetComponent<Mask>();
            RectMask2D rectMask = current.GetComponent<RectMask2D>();
            if (mask != null)
                clipping.Add($"Mask {HierarchyPath(current)} enabled={mask.enabled} showGraphic={mask.showMaskGraphic}");
            if (rectMask != null)
                clipping.Add($"RectMask2D {HierarchyPath(current)} enabled={rectMask.enabled} padding={rectMask.padding}");
            current = current.parent;
        }
        if (clipping.Count > 0)
            builder.AppendLine($"ClippingChain: {string.Join(" | ", clipping)}");
    }

    private static void AppendExpectedComparison(StringBuilder builder, Graphic graphic)
    {
        if (!ExpectedByName.TryGetValue(graphic.gameObject.name, out ExpectedColor expected))
            return;

        Color gamma = graphic.color.gamma;
        Color expectedColor = expected.Color;
        builder.AppendLine($"Expected[{expected.Role}]={expected.Hex} alpha={expected.Alpha:0.###}; " +
                           $"DeltaGammaRGBA=({gamma.r - expectedColor.r:0.####}, {gamma.g - expectedColor.g:0.####}, " +
                           $"{gamma.b - expectedColor.b:0.####}, {graphic.color.a - expected.Alpha:0.####})");
    }

    private static void AppendOverlayChildren(StringBuilder builder, Graphic graphic)
    {
        Graphic[] descendants = graphic.GetComponentsInChildren<Graphic>(true)
            .Where(item => item != graphic)
            .OrderBy(item => item.depth)
            .ToArray();
        if (descendants.Length == 0)
            return;

        builder.AppendLine("DescendantGraphics:");
        foreach (Graphic descendant in descendants)
        {
            builder.AppendLine($"  depth={descendant.depth}; active={descendant.gameObject.activeInHierarchy}; " +
                               $"path={HierarchyPath(descendant.transform)}; color={ColorSummary(descendant.color)}");
        }
    }

    // 4. Pixel samples are accepted only when the inspected Graphic is the highest-depth Graphic at
    // that coordinate. This prevents text, icons and borders from being mistaken for surface color.
    private static void AppendPixelSamples(
        StringBuilder builder,
        Graphic graphic,
        Graphic[] allGraphics,
        Texture2D screenshot,
        Camera eventCamera)
    {
        if (screenshot == null || !(graphic is Image || graphic is RawImage) || !graphic.gameObject.activeInHierarchy)
            return;

        Vector2[] normalizedPoints =
        {
            new Vector2(0.22f, 0.22f),
            new Vector2(0.78f, 0.22f),
            new Vector2(0.22f, 0.78f),
            new Vector2(0.78f, 0.78f),
            new Vector2(0.5f, 0.5f)
        };

        builder.AppendLine("PixelSamples:");
        foreach (Vector2 normalized in normalizedPoints)
        {
            Vector3 world = graphic.rectTransform.TransformPoint(new Vector3(
                Mathf.Lerp(graphic.rectTransform.rect.xMin, graphic.rectTransform.rect.xMax, normalized.x),
                Mathf.Lerp(graphic.rectTransform.rect.yMin, graphic.rectTransform.rect.yMax, normalized.y),
                0f));
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(eventCamera, world);
            int x = Mathf.Clamp(Mathf.RoundToInt(screen.x), 0, screenshot.width - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(screen.y), 0, screenshot.height - 1);
            Graphic topmost = TopmostGraphicAt(allGraphics, screen, eventCamera);
            bool clean = topmost == graphic;
            Color pixel = screenshot.GetPixel(x, y);
            builder.AppendLine($"  uv={FormatVector(normalized)} screen=({x},{y}) PixelFinal={ColorSummary(pixel)} " +
                               $"status={(clean ? "CLEAN" : "OCCLUDED")} topmost={(topmost != null ? HierarchyPath(topmost.transform) : "nenhum")}");
        }
    }

    private static Graphic TopmostGraphicAt(Graphic[] graphics, Vector2 screenPoint, Camera eventCamera)
    {
        return graphics
            .Where(item => item != null && item.enabled && item.gameObject.activeInHierarchy && !item.canvasRenderer.cull)
            .Where(item => RectTransformUtility.RectangleContainsScreenPoint(item.rectTransform, screenPoint, eventCamera))
            .OrderByDescending(item => item.depth)
            .FirstOrDefault();
    }

    private static Texture2D CaptureGameView(string screenshotPath)
    {
        try
        {
            Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            if (screenshot == null)
                return null;

            File.WriteAllBytes(screenshotPath, screenshot.EncodeToPNG());
            return screenshot;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"{LogPrefix} A captura de pixels ficou indisponivel: {exception.Message}");
            return null;
        }
    }

    private static Graphic FindSurfaceGraphic(IEnumerable<Graphic> graphics, string objectName)
    {
        return graphics.FirstOrDefault(item => item.gameObject.name.Equals(objectName, StringComparison.OrdinalIgnoreCase) &&
                                               (item is Image || item is RawImage));
    }

    private static float AccumulatedCanvasGroupAlpha(Transform transform, List<string> details)
    {
        float alpha = 1f;
        Transform current = transform;
        while (current != null)
        {
            CanvasGroup[] groups = current.GetComponents<CanvasGroup>();
            bool stop = false;
            foreach (CanvasGroup group in groups)
            {
                alpha *= group.alpha;
                details?.Add($"{HierarchyPath(group.transform)}: alpha={group.alpha:0.####}, ignoreParentGroups={group.ignoreParentGroups}, " +
                             $"interactable={group.interactable}, blocksRaycasts={group.blocksRaycasts}");
                stop |= group.ignoreParentGroups;
            }

            if (stop)
                break;
            current = current.parent;
        }
        return alpha;
    }

    private static string HierarchyPath(Transform transform)
    {
        var names = new Stack<string>();
        Transform current = transform;
        while (current != null)
        {
            names.Push(current.name);
            current = current.parent;
        }
        return string.Join("/", names);
    }

    private static string ColorSummary(Color color)
    {
        Color gamma = color.gamma;
        return $"linear=({color.r:0.####},{color.g:0.####},{color.b:0.####},{color.a:0.####}) " +
               $"GammaView=({gamma.r:0.####},{gamma.g:0.####},{gamma.b:0.####},{gamma.a:0.####}) " +
               $"GammaHex=#{ColorUtility.ToHtmlStringRGBA(gamma)}";
    }

    private static string MaterialSummary(Material material)
    {
        return material == null ? "null" : $"{material.name} [shader={material.shader?.name ?? "null"}; path={AssetDatabase.GetAssetPath(material)}]";
    }

    private static string AssetSummary(UnityEngine.Object asset)
    {
        return asset == null ? "null" : $"{asset.name} [{AssetDatabase.GetAssetPath(asset)}]";
    }

    private static string Compact(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        string compact = value.Replace('\r', ' ').Replace('\n', ' ');
        return compact.Length <= 80 ? compact : compact.Substring(0, 77) + "...";
    }

    private static string FormatRect(Rect value)
    {
        return $"({value.x:0.##},{value.y:0.##},{value.width:0.##},{value.height:0.##})";
    }

    private static string FormatVector(Vector2 value)
    {
        return $"({value.x:0.##},{value.y:0.##})";
    }

    private static string FormatVector(Vector3 value)
    {
        return $"({value.x:0.##},{value.y:0.##},{value.z:0.##})";
    }

    private readonly struct ExpectedColor
    {
        public ExpectedColor(string role, string hex, float alpha)
        {
            Role = role;
            Hex = hex;
            Alpha = alpha;
            ColorUtility.TryParseHtmlString(hex, out Color parsed);
            Color = parsed;
        }

        public string Role { get; }
        public string Hex { get; }
        public float Alpha { get; }
        public Color Color { get; }
    }
}
#endif
