#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Progressive, reversible visual isolation for the runtime Profile hierarchy.
/// The window changes only enabled flags on runtime Graphic and BaseMeshEffect instances.
/// </summary>
public sealed class WDProfileVisualInspectorWindow : EditorWindow
{
    private const string MenuPath = "War Dominion/UI/Profile/Visual Inspector";
    private const string RuntimeReportMenu = "War Dominion/UI/Profile/Capture Runtime Color Diagnostics";
    private const string LogPrefix = "[WD-PROFILE-VISUAL-INSPECTOR]";

    private static readonly string[] StageLabels =
    {
        "ETAPA 1 - Fundo original e veu",
        "ETAPA 2 - Preenchimentos principais",
        "ETAPA 3 - Cards de Estatisticas",
        "ETAPA 4 - Bordas dos paineis e cards",
        "ETAPA 5 - Textos, icones e imagens",
        "ETAPA 6 - Interface completa restaurada"
    };

    private static readonly string[] InspectableNames =
    {
        "Header", "IdentityPanel", "Statistics", "Metric_0", "Metric_1", "Metric_2", "Metric_3",
        "Performance", "CurrentDivision", "Achievements"
    };

    private static readonly HashSet<string> MainSurfaceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Header", "IdentityPanel", "Statistics", "Performance", "CurrentDivision", "Achievements", "Equipped"
    };

    private static readonly Dictionary<string, FigmaReference> FigmaReferences =
        new Dictionary<string, FigmaReference>(StringComparer.OrdinalIgnoreCase)
        {
            { "Header", new FigmaReference("#070A0F", 0.84f) },
            { "IdentityPanel", new FigmaReference("#08111D", 0.949f) },
            { "Metric_0", new FigmaReference("#050C16", 1f) },
            { "Metric_1", new FigmaReference("#050C16", 1f) },
            { "Metric_2", new FigmaReference("#050C16", 1f) },
            { "Metric_3", new FigmaReference("#050C16", 1f) }
        };

    private readonly Dictionary<Graphic, bool> graphicStates = new Dictionary<Graphic, bool>();
    private readonly Dictionary<BaseMeshEffect, bool> effectStates = new Dictionary<BaseMeshEffect, bool>();
    private WDHomeProfilePanel profile;
    private int stageIndex = 5;
    private int selectedComponent = 3;
    private Vector2 scroll;
    private double nextRefresh;
    private string status = "Entre em Play Mode e abra o Perfil.";
    private Graphic colorTestGraphic;
    private Color colorTestOriginal;
    private ColorTestMode colorTestMode;

    // 01. The window is an Editor-only entry point and never adds a component to a scene or prefab.
    [MenuItem(MenuPath, false, 2311)]
    private static void Open()
    {
        GetWindow<WDProfileVisualInspectorWindow>("Profile Visual Inspector").Show();
    }

    private void OnEnable()
    {
        EditorApplication.update += EditorUpdate;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private void OnDisable()
    {
        RestoreOriginalState();
        EditorApplication.update -= EditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }

    private void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingPlayMode || change == PlayModeStateChange.EnteredEditMode)
            RestoreOriginalState();
    }

    // 02. Runtime hierarchies can be rebuilt when the Profile view changes. Newly created visuals are
    // snapshotted before the active stage is reapplied, preserving their real original enabled state.
    private void EditorUpdate()
    {
        if (EditorApplication.timeSinceStartup < nextRefresh)
            return;
        nextRefresh = EditorApplication.timeSinceStartup + 0.25d;

        if (!EditorApplication.isPlaying)
        {
            if (profile != null || graphicStates.Count > 0)
                RestoreOriginalState();
            profile = null;
            status = "Entre em Play Mode e abra o Perfil.";
            Repaint();
            return;
        }

        WDHomeProfilePanel active = FindActiveProfile();
        if (active != profile)
        {
            RestoreOriginalState();
            profile = active;
            if (profile != null)
            {
                CaptureNewOriginalStates();
                ApplyStage(stageIndex);
            }
        }
        else if (profile != null)
        {
            int previousCount = graphicStates.Count;
            CaptureNewOriginalStates();
            if (graphicStates.Count != previousCount && stageIndex < 5)
                ApplyStage(stageIndex);
        }

        status = profile == null
            ? "Perfil ativo nao encontrado. Abra Home > Perfil."
            : $"Perfil encontrado: {HierarchyPath(profile.transform)}";
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("WAR DOMINION - PERFIL", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Inspecao visual progressiva e reversivel", EditorStyles.miniBoldLabel);
        EditorGUILayout.HelpBox(status, profile == null ? MessageType.Warning : MessageType.Info);

        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || profile == null))
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Etapas cumulativas", EditorStyles.boldLabel);
            for (int i = 0; i < StageLabels.Length; i++)
            {
                bool selected = i == stageIndex;
                if (GUILayout.Toggle(selected, StageLabels[i], "Button") && !selected)
                    ApplyStage(i);
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Etapa anterior")) ApplyStage(Mathf.Max(0, stageIndex - 1));
            if (GUILayout.Button("Proxima etapa")) ApplyStage(Mathf.Min(5, stageIndex + 1));
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Restaurar interface completa")) ApplyStage(5);
            if (GUILayout.Button("Gerar relatorio completo de runtime"))
                EditorApplication.ExecuteMenuItem(RuntimeReportMenu);

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Inspecao individual", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            selectedComponent = EditorGUILayout.Popup("Componente", selectedComponent, InspectableNames);
            if (EditorGUI.EndChangeCheck())
                RestoreColorTest();
            DrawSelectedComponent(InspectableNames[selectedComponent]);

            EditorGUILayout.Space(10f);
            DrawColorConversionTest();
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            "Fechar esta janela, clicar em Restaurar ou sair do Play Mode restaura os estados capturados. " +
            "A ferramenta nao desativa GameObjects e nao altera cores, materiais ou layout.", MessageType.None);
    }

    // 03. Each stage reveals one additional visual category. Stage 6 restores every original flag,
    // including Graphics that were intentionally disabled by the production UI.
    private void ApplyStage(int requestedStage)
    {
        RestoreColorTest();
        if (profile == null)
            return;

        CaptureNewOriginalStates();
        stageIndex = Mathf.Clamp(requestedStage, 0, 5);
        if (stageIndex == 5)
        {
            RestoreOriginalState(false);
            Canvas.ForceUpdateCanvases();
            status = StageLabels[stageIndex];
            Debug.Log($"{LogPrefix} {status}");
            return;
        }

        foreach (KeyValuePair<Graphic, bool> state in graphicStates)
        {
            if (state.Key == null)
                continue;
            state.Key.enabled = state.Value && IsVisibleAtStage(state.Key, stageIndex);
        }

        foreach (KeyValuePair<BaseMeshEffect, bool> state in effectStates)
        {
            if (state.Key != null)
                state.Key.enabled = false;
        }

        Canvas.ForceUpdateCanvases();
        status = StageLabels[stageIndex];
        Debug.Log($"{LogPrefix} {status}; Graphics ativos={graphicStates.Keys.Count(item => item != null && item.enabled)}");
    }

    private static bool IsVisibleAtStage(Graphic graphic, int stage)
    {
        if (IsBackgroundLayer(graphic))
            return true;
        if (stage >= 1 && IsMainSurfaceFill(graphic))
            return true;
        if (stage >= 2 && IsStatisticsCardFill(graphic))
            return true;
        if (stage >= 3 && IsRelevantBorder(graphic))
            return true;
        return stage >= 4;
    }

    private static bool IsBackgroundLayer(Graphic graphic)
    {
        return graphic.transform.parent == null ||
               graphic.transform.GetComponent<WDHomeProfilePanel>() != null ||
               graphic.name.Equals("ProfileBackground", StringComparison.OrdinalIgnoreCase) ||
               graphic.name.Equals("ProfileBackgroundVeil", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMainSurfaceFill(Graphic graphic)
    {
        return MainSurfaceNames.Contains(graphic.name) && graphic.transform == FindNamedAncestor(graphic.transform, graphic.name);
    }

    private static bool IsStatisticsCardFill(Graphic graphic)
    {
        return graphic.name.StartsWith("Metric_", StringComparison.OrdinalIgnoreCase) && graphic is Image;
    }

    private static bool IsRelevantBorder(Graphic graphic)
    {
        if (!graphic.name.Equals("ThinBorder", StringComparison.OrdinalIgnoreCase) &&
            !graphic.name.Equals("Border", StringComparison.OrdinalIgnoreCase))
            return false;

        Transform current = graphic.transform.parent;
        while (current != null)
        {
            if (MainSurfaceNames.Contains(current.name) || current.name.StartsWith("Metric_", StringComparison.OrdinalIgnoreCase))
                return true;
            current = current.parent;
        }
        return false;
    }

    private static Transform FindNamedAncestor(Transform transform, string name)
    {
        Transform current = transform;
        while (current != null)
        {
            if (current.name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return current;
            current = current.parent;
        }
        return null;
    }

    // 04. Original enabled flags are recorded once per runtime instance and restored without Undo,
    // because these objects belong to Play Mode and must never be serialized back into project assets.
    private void CaptureNewOriginalStates()
    {
        if (profile == null)
            return;

        foreach (Graphic graphic in profile.GetComponentsInChildren<Graphic>(true))
        {
            if (!graphicStates.ContainsKey(graphic))
                graphicStates.Add(graphic, graphic.enabled);
        }

        foreach (BaseMeshEffect effect in profile.GetComponentsInChildren<BaseMeshEffect>(true))
        {
            if (!effectStates.ContainsKey(effect))
                effectStates.Add(effect, effect.enabled);
        }
    }

    private void RestoreOriginalState(bool clear = true)
    {
        RestoreColorTest();
        foreach (KeyValuePair<Graphic, bool> state in graphicStates)
            if (state.Key != null) state.Key.enabled = state.Value;
        foreach (KeyValuePair<BaseMeshEffect, bool> state in effectStates)
            if (state.Key != null) state.Key.enabled = state.Value;

        if (clear)
        {
            graphicStates.Clear();
            effectStates.Clear();
        }
        Canvas.ForceUpdateCanvases();
    }

    // 05. Controlled color-space experiment. Only Metric_0.Graphic.color is changed, preserving
    // Metric_1..3 as side-by-side controls and keeping the original alpha, material and shader.
    private void DrawColorConversionTest()
    {
        EditorGUILayout.LabelField("TESTE DE CONVERSAO DE CORES", EditorStyles.boldLabel);
        Graphic metric = FindSurfaceGraphic("Metric_0");
        if (metric == null)
        {
            EditorGUILayout.HelpBox("Metric_0 nao foi encontrado na hierarquia ativa.", MessageType.Warning);
            return;
        }

        if (colorTestGraphic == null)
        {
            DrawLine("Alvo", HierarchyPath(metric.transform));
            DrawLine("Cor original", ColorSummary(metric.color));
        }
        else
        {
            DrawLine("Alvo", HierarchyPath(colorTestGraphic.transform));
            DrawLine("Cor original", ColorSummary(colorTestOriginal));
        }

        Color directSrgb = DirectSrgbMetricColor(
            colorTestGraphic != null ? colorTestOriginal.a : metric.color.a);
        DrawLine("Cor experimental", ColorSummary(directSrgb));
        DrawLine("Alpha", (colorTestGraphic != null ? colorTestOriginal.a : metric.color.a).ToString("0.####"));
        Material material = metric.materialForRendering;
        DrawLine("Material", material != null ? material.name : "null");
        DrawLine("Shader", material != null && material.shader != null ? material.shader.name : "null");
        DrawLine("Color Space", QualitySettings.activeColorSpace.ToString());
        DrawLine("Modo", ColorTestModeLabel(colorTestMode));

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("A - ORIGINAL")) ApplyOriginalColorTest(metric);
        if (GUILayout.Button("B - SRGB DIRETO")) ApplyDirectSrgbColorTest(metric);
        if (GUILayout.Button("C - RESTAURAR")) RestoreColorTest();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "Somente Metric_0 e alterado. Metric_1, Metric_2 e Metric_3 permanecem como controles visuais.",
            MessageType.None);
    }

    private void ApplyOriginalColorTest(Graphic metric)
    {
        CaptureColorTestOriginal(metric);
        if (colorTestGraphic == null)
            return;
        colorTestGraphic.color = colorTestOriginal;
        colorTestMode = ColorTestMode.Original;
        Canvas.ForceUpdateCanvases();
        Debug.Log($"{LogPrefix} TESTE DE COR A - ORIGINAL; {ColorSummary(colorTestGraphic.color)}");
    }

    private void ApplyDirectSrgbColorTest(Graphic metric)
    {
        CaptureColorTestOriginal(metric);
        if (colorTestGraphic == null)
            return;
        colorTestGraphic.color = DirectSrgbMetricColor(colorTestOriginal.a);
        colorTestMode = ColorTestMode.DirectSrgb;
        Canvas.ForceUpdateCanvases();
        Debug.Log($"{LogPrefix} TESTE DE COR B - SRGB DIRETO; {ColorSummary(colorTestGraphic.color)}");
    }

    private void CaptureColorTestOriginal(Graphic metric)
    {
        if (metric == null)
            return;
        if (colorTestGraphic == metric)
            return;
        RestoreColorTest();
        colorTestGraphic = metric;
        colorTestOriginal = metric.color;
        colorTestMode = ColorTestMode.Original;
    }

    private void RestoreColorTest()
    {
        if (colorTestGraphic != null)
            colorTestGraphic.color = colorTestOriginal;
        colorTestGraphic = null;
        colorTestOriginal = default;
        colorTestMode = ColorTestMode.None;
        if (EditorApplication.isPlaying)
            Canvas.ForceUpdateCanvases();
    }

    private static Color DirectSrgbMetricColor(float alpha)
    {
        return new Color(5f / 255f, 12f / 255f, 22f / 255f, alpha);
    }

    private static string ColorTestModeLabel(ColorTestMode mode)
    {
        return mode switch
        {
            ColorTestMode.Original => "A - ORIGINAL",
            ColorTestMode.DirectSrgb => "B - SRGB DIRETO",
            _ => "C - RESTAURADO / INATIVO"
        };
    }

    // 06. The component panel reads the same runtime evidence as the text diagnostic and maps code
    // responsibility only where the construction method can be established from the current source.
    private void DrawSelectedComponent(string objectName)
    {
        Graphic graphic = FindSurfaceGraphic(objectName);
        if (graphic == null)
        {
            EditorGUILayout.HelpBox($"{objectName} nao foi encontrado na hierarquia ativa.", MessageType.Warning);
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(190f), GUILayout.MaxHeight(330f));
        EditorGUILayout.SelectableLabel(HierarchyPath(graphic.transform), EditorStyles.textField, GUILayout.Height(36f));
        DrawLine("Tipo", graphic.GetType().Name);
        if (FigmaReferences.TryGetValue(objectName, out FigmaReference figma))
            DrawLine("Figma", $"{figma.Hex}; alpha={figma.Alpha:0.###}");
        else
            DrawLine("Figma", "Sem valor confirmado para esta superficie");
        DrawLine("Graphic.color", ColorSummary(graphic.color));
        DrawLine("CanvasRenderer", ColorSummary(graphic.canvasRenderer.GetColor()));
        DrawLine("Renderer alpha", graphic.canvasRenderer.GetAlpha().ToString("0.####"));
        DrawLine("Alpha herdado", graphic.canvasRenderer.GetInheritedAlpha().ToString("0.####"));
        DrawLine("CanvasGroups", CanvasGroupSummary(graphic.transform));
        Material material = graphic.materialForRendering;
        DrawLine("Material", material != null ? material.name : "null");
        DrawLine("Shader", material != null && material.shader != null ? material.shader.name : "null");
        DrawLine("Depth / sibling", $"{graphic.depth} / {graphic.transform.GetSiblingIndex()}");
        DrawLine("Sobrepostos", OverlaySummary(graphic));
        DrawLine("Codigo responsavel", CodeResponsibility(objectName));
        EditorGUILayout.EndScrollView();
    }

    private Graphic FindSurfaceGraphic(string objectName)
    {
        if (profile == null)
            return null;
        return profile.GetComponentsInChildren<Graphic>(true)
            .FirstOrDefault(item => item.name.Equals(objectName, StringComparison.OrdinalIgnoreCase) &&
                                    (item is Image || item is RawImage));
    }

    private static void DrawLine(string label, string value)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(120f));
        EditorGUILayout.SelectableLabel(value ?? string.Empty, EditorStyles.textField, GUILayout.Height(34f));
        EditorGUILayout.EndHorizontal();
    }

    private static string OverlaySummary(Graphic graphic)
    {
        WDHomeProfilePanel owner = graphic.GetComponentInParent<WDHomeProfilePanel>();
        if (owner == null)
            return "Perfil ancestral nao encontrado";

        Canvas canvas = graphic.canvas;
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
        Vector2 screenCenter = RectTransformUtility.WorldToScreenPoint(eventCamera,
            graphic.rectTransform.TransformPoint(graphic.rectTransform.rect.center));
        Graphic[] descendants = owner.GetComponentsInChildren<Graphic>(true)
            .Where(item => item != graphic && item.gameObject.activeInHierarchy && item.enabled &&
                           item.depth >= graphic.depth &&
                           RectTransformUtility.RectangleContainsScreenPoint(item.rectTransform, screenCenter, eventCamera))
            .OrderBy(item => item.depth)
            .ToArray();
        if (descendants.Length == 0)
            return "nenhum Graphic acima do centro da superficie";
        return string.Join(", ", descendants.Take(12).Select(item => $"{item.name}[d={item.depth}]")) +
               (descendants.Length > 12 ? $" (+{descendants.Length - 12})" : string.Empty);
    }

    private static string CanvasGroupSummary(Transform transform)
    {
        var groups = new List<string>();
        float accumulated = 1f;
        Transform current = transform;
        while (current != null)
        {
            foreach (CanvasGroup group in current.GetComponents<CanvasGroup>())
            {
                accumulated *= group.alpha;
                groups.Add($"{current.name}={group.alpha:0.###}");
                if (group.ignoreParentGroups)
                    return $"acumulado={accumulated:0.####}; {string.Join(" > ", groups)}; stop=ignoreParentGroups";
            }
            current = current.parent;
        }
        return groups.Count == 0 ? "acumulado=1; nenhum" : $"acumulado={accumulated:0.####}; {string.Join(" > ", groups)}";
    }

    private static string CodeResponsibility(string objectName)
    {
        if (objectName.Equals("Header", StringComparison.OrdinalIgnoreCase))
            return "WDHomeProfilePanel.BuildHeader -> ApplyProfileSurface";
        if (objectName.StartsWith("Metric_", StringComparison.OrdinalIgnoreCase))
            return "WDHomeProfilePanel.BuildMetricRow -> Panel";
        if (MainSurfaceNames.Contains(objectName))
            return "WDHomeProfilePanel.RenderProfile -> Panel";
        return "Nao comprovado";
    }

    private static WDHomeProfilePanel FindActiveProfile()
    {
        return UnityEngine.Object.FindObjectsByType<WDHomeProfilePanel>(FindObjectsInactive.Include)
            .FirstOrDefault(item => item.gameObject.activeInHierarchy);
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
        return $"atribuida=({color.r:0.####},{color.g:0.####},{color.b:0.####},{color.a:0.####}); " +
               $"RawHex=#{ColorUtility.ToHtmlStringRGBA(color)}; GammaView=#{ColorUtility.ToHtmlStringRGBA(gamma)}";
    }

    private readonly struct FigmaReference
    {
        public FigmaReference(string hex, float alpha)
        {
            Hex = hex;
            Alpha = alpha;
        }

        public string Hex { get; }
        public float Alpha { get; }
    }

    private enum ColorTestMode
    {
        None,
        Original,
        DirectSrgb
    }
}
#endif
