using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class WDGameplayVisualBaselineCaptureTests
{
    // 1. Capture names carry every stable comparison dimension without depending on scene state.
    [Test]
    public void BaseNameContainsMapResolutionStateRoundAndTimestamp()
    {
        DateTime timestamp = new DateTime(2026, 10, 16, 21, 8, 50, 123, DateTimeKind.Local);
        var metadata = new WDGameplayBaselineMetadata
        {
            mapId = "dark_world",
            state = "names_zoom",
            width = 1920,
            height = 1080,
            round = 4
        };

        string result = WDGameplayVisualBaselinePaths.BuildBaseName(metadata, timestamp);

        Assert.That(result, Is.EqualTo(
            "dark_world_1920x1080_names_zoom_r4_20261016_210850_123"));
    }

    // 2. User-visible state labels remain stable so before/after batches sort consistently.
    [TestCase(WDGameplayBaselineState.Overview, "overview")]
    [TestCase(WDGameplayBaselineState.Selection, "selection")]
    [TestCase(WDGameplayBaselineState.NamesZoom, "names_zoom")]
    [TestCase(WDGameplayBaselineState.Resolution, "resolution")]
    public void StateSlugIsStable(WDGameplayBaselineState state, string expected)
    {
        Assert.That(WDGameplayVisualBaselinePaths.StateSlug(state), Is.EqualTo(expected));
    }

    // 3. Existing PNG or JSON files force a suffix and can never be overwritten by a later capture.
    [Test]
    public void UniqueStemSkipsExistingCaptureAndMetadata()
    {
        string directory = Path.Combine(Path.GetTempPath(),
            "WDGameplayBaselineTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string original = Path.Combine(directory, "classic_capture");
            File.WriteAllText(original + ".png", "existing");
            File.WriteAllText(Path.Combine(directory, "classic_capture_01.json"), "existing");

            string result = WDGameplayVisualBaselinePaths.GetUniqueStem(
                directory, "classic_capture");

            Assert.That(result, Is.EqualTo(Path.Combine(directory, "classic_capture_02")));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    // 4. Diagnostic output must remain outside the Unity import tree.
    [Test]
    public void OutputDirectoryIsOutsideAssetsAndInsideIgnoredTemp()
    {
        string root = Path.GetFullPath(WDGameplayVisualBaselinePaths.GetRootDirectory());
        string assets = Path.GetFullPath(UnityEngine.Application.dataPath);

        Assert.That(root.StartsWith(assets + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase), Is.False);
        Assert.That(root, Does.Contain(Path.Combine("Temp", "Diagnostics", "GameplayBaseline")));
    }

    // 5. Uma textura opaca com as dimensões corretas continua inválida quando todos os pixels
    // são pretos. Este é o cenário exato da regressão que originou a Fase 7.1.
    [Test]
    public void CaptureAnalysisRejectsOpaqueBlackTexture()
    {
        var texture = CreateSolidTexture(32, 18, new Color32(0, 0, 0, 255));
        try
        {
            WDGameplayCaptureAnalysis result = WDGameplayGameViewCapture.Analyze(texture);

            Assert.That(result.width, Is.EqualTo(32));
            Assert.That(result.height, Is.EqualTo(18));
            Assert.That(result.isUsable, Is.False);
            Assert.That(result.nonBlackPixels, Is.Zero);
            Assert.That(result.reason, Does.Contain("preta"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    // 6. Alpha zero também produziria um PNG visualmente vazio e deve ser diagnosticado antes
    // que qualquer arquivo ou metadado seja gravado.
    [Test]
    public void CaptureAnalysisRejectsFullyTransparentTexture()
    {
        var texture = CreateSolidTexture(20, 10, new Color32(180, 90, 40, 0));
        try
        {
            WDGameplayCaptureAnalysis result = WDGameplayGameViewCapture.Analyze(texture);

            Assert.That(result.isUsable, Is.False);
            Assert.That(result.visiblePixels, Is.Zero);
            Assert.That(result.reason, Does.Contain("transparente"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    // 7. Conteúdo com variação visual e pixels não pretos representa o mapa/HUD esperado e
    // atravessa a mesma validação que protege a gravação da baseline real.
    [Test]
    public void CaptureAnalysisAcceptsVisibleStructuredFrame()
    {
        var texture = CreateSolidTexture(40, 20, new Color32(8, 12, 20, 255));
        try
        {
            Color32[] pixels = texture.GetPixels32();
            for (int index = 0; index < pixels.Length; index += 3)
                pixels[index] = new Color32(42, 130, 210, 255);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            WDGameplayCaptureAnalysis result = WDGameplayGameViewCapture.Analyze(texture);

            Assert.That(result.isUsable, Is.True, result.ToDiagnosticString());
            Assert.That(result.nonBlackPixels, Is.EqualTo(pixels.Length));
            Assert.That(result.maxB, Is.EqualTo(210));
            Assert.That(result.meanLuminance, Is.GreaterThan(0d));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    // 8. Helper local cria dados legíveis e determinísticos sem depender de câmera, cena ou GPU.
    private static Texture2D CreateSolidTexture(int width, int height, Color32 color)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
        var pixels = new Color32[width * height];
        for (int index = 0; index < pixels.Length; index++)
            pixels[index] = color;
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }
}
