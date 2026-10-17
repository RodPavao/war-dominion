#if UNITY_EDITOR
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

public enum WDGameplayBaselineState
{
    Overview,
    Selection,
    NamesZoom,
    Resolution
}

[Serializable]
public sealed class WDGameplayBaselineMetadata
{
    public string mapId;
    public string state;
    public int width;
    public int height;
    public string capturedAtLocal;
    public string capturedAtUtc;
    public int round;
    public string phase;
    public string scene;
    public string unityVersion;
}

/// <summary>
/// Captures the current Game View for visual comparison without changing runtime state.
/// PNG and metadata files are written below the project's ignored Temp directory.
/// </summary>
public sealed class WDGameplayVisualBaselineCapture : EditorWindow
{
    private const string MenuPath = "War Dominion/UI/Gameplay/Visual Baseline Capture";
    private const string LogPrefix = "[WD-GAMEPLAY-BASELINE]";
    private WDGameplayBaselineState selectedState = WDGameplayBaselineState.Overview;

    // 1. The window owns only the human label for the current capture. It never creates or
    // modifies gameplay state, map setup, camera settings, resolution, or scene objects.
    [MenuItem(MenuPath, false, 2400)]
    private static void OpenWindow()
    {
        WDGameplayVisualBaselineCapture window = GetWindow<WDGameplayVisualBaselineCapture>();
        window.titleContent = new GUIContent("Gameplay Baseline");
        window.minSize = new Vector2(390f, 190f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Gameplay Visual Baseline", EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(
            "Captura a Game View exatamente como ela está. A ferramenta não altera a partida.",
            MessageType.Info);

        selectedState = (WDGameplayBaselineState)EditorGUILayout.EnumPopup(
            "Estado documentado", selectedState);

        string output = WDGameplayVisualBaselinePaths.GetRootDirectory();
        EditorGUILayout.LabelField("Saída", output, EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space(8f);

        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
        {
            if (GUILayout.Button("CAPTURAR GAME VIEW", GUILayout.Height(34f)))
                CaptureCurrentGameView(selectedState);
        }

        if (!EditorApplication.isPlaying)
            EditorGUILayout.HelpBox("Entre no Play Mode para habilitar a captura.", MessageType.Warning);
    }

    // 2. A captura é agendada para o fim de um frame posterior. ScreenCapture é a API pública
    // do Unity para a Game View e só tem resultado definido depois que o frame terminou.
    internal static bool CaptureCurrentGameView(WDGameplayBaselineState state)
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning($"{LogPrefix} A captura só está disponível durante o Play Mode.");
            return false;
        }

        try
        {
            bool scheduled = WDGameplayGameViewCapture.RequestCapture(
                (screenshot, analysis, error) => CompleteCapture(state, screenshot, analysis, error));
            if (scheduled)
                Debug.Log($"{LogPrefix} Captura agendada para o fim do próximo frame renderizado.");
            return scheduled;
        }
        catch (Exception exception)
        {
            Debug.LogException(new InvalidOperationException(
                $"{LogPrefix} Falha ao capturar a Game View. Nenhum estado da partida foi alterado.",
                exception));
            return false;
        }
    }

    // 3. Arquivos só são criados depois da confirmação de dimensões e conteúdo visível. Uma
    // textura preta, transparente, uniforme ou ilegível gera diagnóstico e nunca vira baseline.
    private static void CompleteCapture(WDGameplayBaselineState state, Texture2D screenshot,
        WDGameplayCaptureAnalysis analysis, Exception captureError)
    {
        try
        {
            if (captureError != null)
                throw captureError;
            if (screenshot == null)
                throw new InvalidOperationException("A Game View não retornou uma imagem.");

            DateTime localNow = DateTime.Now;
            WDGameplayBaselineMetadata metadata = CollectMetadata(
                state, screenshot.width, screenshot.height, localNow);
            string directory = WDGameplayVisualBaselinePaths.GetMapDirectory(metadata.mapId);
            Directory.CreateDirectory(directory);

            string baseName = WDGameplayVisualBaselinePaths.BuildBaseName(metadata, localNow);
            string uniqueStem = WDGameplayVisualBaselinePaths.GetUniqueStem(directory, baseName);
            string pngPath = uniqueStem + ".png";
            string metadataPath = uniqueStem + ".json";

            byte[] png = screenshot.EncodeToPNG();
            if (png == null || png.Length == 0)
                throw new InvalidOperationException("A codificação PNG retornou um arquivo vazio.");

            File.WriteAllBytes(pngPath, png);
            File.WriteAllText(metadataPath, JsonUtility.ToJson(metadata, true));

            Debug.Log($"{LogPrefix} Captura salva em: {pngPath}\nMetadados: {metadataPath}\n" +
                      $"Diagnóstico: {analysis.ToDiagnosticString()}");
            EditorUtility.RevealInFinder(pngPath);
        }
        catch (Exception exception)
        {
            Debug.LogException(new InvalidOperationException(
                $"{LogPrefix} A captura foi rejeitada; nenhum PNG ou JSON inutilizável foi salvo. " +
                analysis.ToDiagnosticString(), exception));
        }
        finally
        {
            if (screenshot != null)
                DestroyImmediate(screenshot);
        }
    }

    // 4. Runtime services are optional inputs. Missing services produce explicit metadata rather
    // than coupling the capture command to a particular scene or initialization order.
    internal static WDGameplayBaselineMetadata CollectMetadata(
        WDGameplayBaselineState state, int width, int height, DateTime localNow)
    {
        MapaAtivo activeMap = MapaAtivo.Instance;
        GameManager gameManager = FindAnyObjectByType<GameManager>();
        string mapId = activeMap != null && activeMap.Definicao != null
            ? activeMap.Definicao.MapaId
            : "unknown_map";

        return new WDGameplayBaselineMetadata
        {
            mapId = WDGameplayVisualBaselinePaths.SanitizeSegment(mapId),
            state = WDGameplayVisualBaselinePaths.StateSlug(state),
            width = width,
            height = height,
            capturedAtLocal = localNow.ToString("O", CultureInfo.InvariantCulture),
            capturedAtUtc = localNow.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            round = gameManager != null ? gameManager.RodadaAtual : -1,
            phase = gameManager != null ? gameManager.faseAtual.ToString() : "unknown",
            scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
            unityVersion = Application.unityVersion
        };
    }
}

/// <summary>Captures the completed Game View frame without changing cameras or resolution.</summary>
public static class WDGameplayGameViewCapture
{
    private static bool capturePending;

    // 5. ScreenCapture is public and supported by Unity 6. The runtime runner waits one complete
    // frame and then WaitForEndOfFrame, avoiding the undefined read that occurs during Editor GUI.
    public static bool RequestCapture(
        Action<Texture2D, WDGameplayCaptureAnalysis, Exception> completed)
    {
        if (completed == null)
            throw new ArgumentNullException(nameof(completed));
        if (!EditorApplication.isPlaying)
            throw new InvalidOperationException("A captura exige Play Mode ativo.");
        if (EditorApplication.isPaused)
            throw new InvalidOperationException("Despause o Play Mode antes de capturar a Game View.");
        if (capturePending)
        {
            Debug.LogWarning("[WD-GAMEPLAY-BASELINE] Já existe uma captura aguardando o fim do frame.");
            return false;
        }

        capturePending = true;
        WDGameplayFrameCaptureRunner.Begin((texture, analysis, error) =>
        {
            capturePending = false;
            completed(texture, analysis, error);
        });
        return true;
    }

    // 6. A análise percorre os pixels efetivamente lidos da GPU. O limiar é deliberadamente
    // conservador: mapa + HUD não podem ser uma superfície uniforme ou quase toda preta.
    public static WDGameplayCaptureAnalysis Analyze(Texture2D texture)
    {
        if (texture == null)
            return WDGameplayCaptureAnalysis.Invalid("texture=null");
        if (texture.width <= 0 || texture.height <= 0)
            return WDGameplayCaptureAnalysis.Invalid(
                $"dimensões inválidas {texture.width}x{texture.height}");

        Color32[] pixels;
        try
        {
            pixels = texture.GetPixels32();
        }
        catch (Exception exception)
        {
            return WDGameplayCaptureAnalysis.Invalid(
                $"pixels ilegíveis ({exception.GetType().Name}: {exception.Message})",
                texture.width, texture.height);
        }

        if (pixels == null || pixels.Length != texture.width * texture.height)
            return WDGameplayCaptureAnalysis.Invalid(
                $"quantidade de pixels inválida ({pixels?.Length ?? 0})",
                texture.width, texture.height);

        int visible = 0;
        int nonBlack = 0;
        byte minR = byte.MaxValue, minG = byte.MaxValue, minB = byte.MaxValue;
        byte maxR = 0, maxG = 0, maxB = 0;
        double luminanceSum = 0d;
        foreach (Color32 pixel in pixels)
        {
            if (pixel.a > 3)
                visible++;
            if (pixel.a > 3 && (pixel.r > 3 || pixel.g > 3 || pixel.b > 3))
                nonBlack++;
            minR = Math.Min(minR, pixel.r);
            minG = Math.Min(minG, pixel.g);
            minB = Math.Min(minB, pixel.b);
            maxR = Math.Max(maxR, pixel.r);
            maxG = Math.Max(maxG, pixel.g);
            maxB = Math.Max(maxB, pixel.b);
            luminanceSum += 0.2126d * pixel.r + 0.7152d * pixel.g + 0.0722d * pixel.b;
        }

        int minimumVisibleContent = Math.Max(1, (int)Math.Ceiling(pixels.Length * 0.001d));
        bool uniform = maxR - minR <= 2 && maxG - minG <= 2 && maxB - minB <= 2;
        bool usable = visible >= minimumVisibleContent &&
                      nonBlack >= minimumVisibleContent && !uniform;
        string reason = usable
            ? "ok"
            : visible < minimumVisibleContent
                ? "imagem vazia ou transparente"
                : nonBlack < minimumVisibleContent
                    ? "imagem preta ou sem conteúdo visível suficiente"
                    : "imagem uniforme, sem mapa/HUD detectável";

        return new WDGameplayCaptureAnalysis(texture.width, texture.height, pixels.Length,
            visible, nonBlack, minR, minG, minB, maxR, maxG, maxB,
            luminanceSum / pixels.Length, usable, reason);
    }
}

/// <summary>Executes the public screenshot API at a defined frame boundary.</summary>
internal sealed class WDGameplayFrameCaptureRunner : MonoBehaviour
{
    private Action<Texture2D, WDGameplayCaptureAnalysis, Exception> completed;

    // 7. HideAndDontSave keeps the runner out of scene serialization and hierarchy workflows.
    // It exists only for two frame boundaries and never touches gameplay objects or settings.
    internal static void Begin(
        Action<Texture2D, WDGameplayCaptureAnalysis, Exception> completed)
    {
        var host = new GameObject("WDGameplayBaselineCaptureRunner")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        var runner = host.AddComponent<WDGameplayFrameCaptureRunner>();
        runner.completed = completed;
        runner.StartCoroutine(runner.CaptureAtEndOfFrame());
    }

    // 8. The initial yield guarantees a new player frame after the Editor button event. The
    // second yield runs after cameras and overlay canvases have completed that frame.
    private IEnumerator CaptureAtEndOfFrame()
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        Texture2D texture = null;
        WDGameplayCaptureAnalysis analysis = default;
        Exception error = null;
        try
        {
            int expectedWidth = Screen.width;
            int expectedHeight = Screen.height;
            texture = ScreenCapture.CaptureScreenshotAsTexture();
            if (texture == null)
                throw new InvalidOperationException("ScreenCapture retornou texture=null.");
            if (texture.width != expectedWidth || texture.height != expectedHeight)
                throw new InvalidOperationException(
                    $"Resolução inesperada: Game View {expectedWidth}x{expectedHeight}, " +
                    $"captura {texture.width}x{texture.height}.");

            analysis = WDGameplayGameViewCapture.Analyze(texture);
            if (!analysis.isUsable)
                throw new InvalidOperationException(
                    "A leitura de pixels foi rejeitada: " + analysis.ToDiagnosticString());
        }
        catch (Exception exception)
        {
            error = exception;
        }
        finally
        {
            try
            {
                completed?.Invoke(texture, analysis, error);
            }
            finally
            {
                Destroy(gameObject);
            }
        }
    }
}

/// <summary>Objective pixel diagnostics used before accepting a visual baseline.</summary>
public readonly struct WDGameplayCaptureAnalysis
{
    public readonly int width;
    public readonly int height;
    public readonly int totalPixels;
    public readonly int visiblePixels;
    public readonly int nonBlackPixels;
    public readonly byte minR;
    public readonly byte minG;
    public readonly byte minB;
    public readonly byte maxR;
    public readonly byte maxG;
    public readonly byte maxB;
    public readonly double meanLuminance;
    public readonly bool isUsable;
    public readonly string reason;

    // 9. Diagnostics are data-only so tests can prove rejection without opening a Game View.
    public WDGameplayCaptureAnalysis(int width, int height, int totalPixels,
        int visiblePixels, int nonBlackPixels, byte minR, byte minG, byte minB,
        byte maxR, byte maxG, byte maxB, double meanLuminance, bool isUsable,
        string reason)
    {
        this.width = width;
        this.height = height;
        this.totalPixels = totalPixels;
        this.visiblePixels = visiblePixels;
        this.nonBlackPixels = nonBlackPixels;
        this.minR = minR;
        this.minG = minG;
        this.minB = minB;
        this.maxR = maxR;
        this.maxG = maxG;
        this.maxB = maxB;
        this.meanLuminance = meanLuminance;
        this.isUsable = isUsable;
        this.reason = reason ?? "unknown";
    }

    public static WDGameplayCaptureAnalysis Invalid(string reason, int width = 0,
        int height = 0)
    {
        return new WDGameplayCaptureAnalysis(width, height, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0d, false, reason);
    }

    public string ToDiagnosticString()
    {
        double visiblePercent = totalPixels > 0 ? visiblePixels * 100d / totalPixels : 0d;
        double nonBlackPercent = totalPixels > 0 ? nonBlackPixels * 100d / totalPixels : 0d;
        return $"{width}x{height}; pixels={totalPixels}; visíveis={visiblePixels} " +
               $"({visiblePercent:0.###}%); não pretos={nonBlackPixels} " +
               $"({nonBlackPercent:0.###}%); RGB min=({minR},{minG},{minB}) " +
               $"max=({maxR},{maxG},{maxB}); luminância média={meanLuminance:0.###}; " +
               $"resultado={(isUsable ? "válido" : "inválido")}; motivo={reason}";
    }
}

/// <summary>Pure path and naming helpers kept testable without entering Play Mode.</summary>
public static class WDGameplayVisualBaselinePaths
{
    // 10. Temp is outside Assets and already ignored by the repository. Per-map folders keep
    // Classic, Dark World, and future maps comparable without importing diagnostic PNG files.
    public static string GetRootDirectory()
    {
        return Path.GetFullPath(Path.Combine(
            Application.dataPath, "..", "Temp", "Diagnostics", "GameplayBaseline"));
    }

    public static string GetMapDirectory(string mapId)
    {
        return Path.Combine(GetRootDirectory(), SanitizeSegment(mapId));
    }

    public static string BuildBaseName(WDGameplayBaselineMetadata metadata, DateTime localNow)
    {
        if (metadata == null)
            throw new ArgumentNullException(nameof(metadata));

        string map = SanitizeSegment(metadata.mapId);
        string state = SanitizeSegment(metadata.state);
        string timestamp = localNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
        return $"{map}_{metadata.width}x{metadata.height}_{state}_r{metadata.round}_{timestamp}";
    }

    public static string GetUniqueStem(string directory, string baseName)
    {
        string safeBaseName = SanitizeSegment(baseName);
        string candidate = Path.Combine(directory, safeBaseName);
        int suffix = 1;
        while (File.Exists(candidate + ".png") || File.Exists(candidate + ".json"))
        {
            candidate = Path.Combine(directory, $"{safeBaseName}_{suffix:00}");
            suffix++;
        }

        return candidate;
    }

    public static string StateSlug(WDGameplayBaselineState state)
    {
        return state switch
        {
            WDGameplayBaselineState.Overview => "overview",
            WDGameplayBaselineState.Selection => "selection",
            WDGameplayBaselineState.NamesZoom => "names_zoom",
            WDGameplayBaselineState.Resolution => "resolution",
            _ => "unknown_state"
        };
    }

    public static string SanitizeSegment(string value)
    {
        string source = string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().ToLowerInvariant();
        foreach (char invalid in Path.GetInvalidFileNameChars())
            source = source.Replace(invalid, '_');
        source = source.Replace(' ', '_');
        while (source.Contains("__"))
            source = source.Replace("__", "_");
        return source.Trim('_');
    }
}
#endif
