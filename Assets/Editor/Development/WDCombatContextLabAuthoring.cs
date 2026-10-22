using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Gera a cena contextual sem salvar qualquer alteração em WarGame.</summary>
public static class WDCombatContextLabAuthoring
{
    public const string ScenePath = "Assets/Scenes/Development/WDCombatContextLab.unity";
    private const string SourceScenePath = "Assets/WarGame.unity";
    private const string PresetPath =
        "UserSettings/WDCombatContextLabVisualPreset.json";
    private static readonly string[] TerritoryIds =
        { "Brazil", "Venezuela", "Argentina", "Peru" };

    // JsonUtility preenche estes campos por reflexão; o compilador não observa
    // essas atribuições estáticas, portanto o warning é desabilitado no DTO.
#pragma warning disable CS0649
    [Serializable]
    private sealed class VisualPreset
    {
        public string capturedAt;
        public string source;
        public float indicatorOpacity, chevronArmLength, chevronThickness,
            dotRadius, glowIntensity, contrast, animationSpeedMultiplier;
        public string confirmedArrowTint, confirmedOutlineColor,
            confirmedCardBackground, confirmedCardBorder, confirmedNumberColor;
        public float confirmedArrowOpacity, confirmedBodyThickness,
            confirmedOutlineThickness, confirmedOuterGlow, confirmedReflection,
            confirmedHeadLength, confirmedHeadWidth,
            confirmedCardBorderThickness, confirmedCardTopReflection,
            confirmedCardCornerRadius, confirmedCardWidth, confirmedCardHeight,
            confirmedNumberSize, confirmedShadowThickness,
            confirmedShadowIntensity, confirmedSpecularIntensity,
            confirmedReliefIntensity, confirmedSurfaceOpacity,
            confirmedOriginMargin, confirmedDestinationMargin,
            confirmedCurveHeight, confirmedOriginExtension,
            confirmedDestinationExtension, confirmedCurvePosition;
        public bool confirmedAdaptiveOutline;
    }
#pragma warning restore CS0649

    // ============================================================
    // 00.1. PRESET LOCAL — captura inclusive valores ajustados em Play Mode
    // ============================================================
    [MenuItem("War Dominion/Development/Save Combat Context Lab Visual Preset")]
    public static void SaveVisualPreset()
    {
        WDCombatContextLabController controller = Resources
            .FindObjectsOfTypeAll<WDCombatContextLabController>()
            .FirstOrDefault(item => item != null && item.gameObject.scene.IsValid());
        if (controller == null)
        {
            EditorUtility.DisplayDialog("Combat Context Lab",
                "Nenhum WDCombatContextLabController ativo foi encontrado.", "OK");
            return;
        }
        VisualPreset preset = ReadPreset(new SerializedObject(controller));
        preset.capturedAt = DateTimeOffset.Now.ToString("O");
        preset.source = Application.isPlaying
            ? "WDCombatContextLabController Inspector during active Play Mode"
            : "WDCombatContextLabController Inspector in Edit Mode";
        Directory.CreateDirectory(Path.GetDirectoryName(PresetPath));
        File.WriteAllText(PresetPath,
            JsonUtility.ToJson(preset, true) + Environment.NewLine);
        Debug.Log("[WDCombatContextLab] Preset visual salvo em " + PresetPath);
    }

    // ============================================================
    // 00. PRIMEIRA IMPORTAÇÃO — materializa a cena solicitada uma única vez
    // ============================================================
    [InitializeOnLoadMethod]
    private static void BuildMissingSceneAfterCompilation()
    {
        if (!ShouldRebuildScene())
            return;
        EditorApplication.delayCall += () =>
        {
            if (ShouldRebuildScene() && !EditorApplication.isCompiling &&
                !EditorApplication.isPlayingOrWillChangePlaymode)
                Build();
        };
    }

    private static bool ShouldRebuildScene()
    {
        const string authoringPath =
            "Assets/Editor/Development/WDCombatContextLabAuthoring.cs";
        return !File.Exists(ScenePath) ||
            File.GetLastWriteTimeUtc(ScenePath) <
            File.GetLastWriteTimeUtc(authoringPath);
    }

    // ============================================================
    // 01. AUTORIA ISOLADA — clona objetos reais e restaura o setup do Editor
    // ============================================================
    [MenuItem("War Dominion/Development/Build Combat Context Lab")]
    public static void Build()
    {
        EnsureFolders();
        Scene openLab = SceneManager.GetSceneByPath(ScenePath);
        // 01.1. CENA ABERTA — nunca descarta alterações locais silenciosamente.
        // Uma cena limpa pode ser fechada temporariamente porque o setup exato
        // do Editor é restaurado após a geração. Cena suja exige ação do usuário.
        if (openLab.IsValid() && openLab.isLoaded && openLab.isDirty)
        {
            const string guidance =
                "A geração foi interrompida porque WDCombatContextLab.unity " +
                "possui alterações não salvas. Salve ou descarte essas alterações " +
                "explicitamente e execute Build Combat Context Lab novamente.";
            Debug.LogWarning("[WDCombatContextLab] " + guidance);
            EditorUtility.DisplayDialog("Combat Context Lab", guidance, "Entendi");
            return;
        }

        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        Scene source = default;
        Scene lab = default;
        bool sourceOpenedByBuild = false;
        try
        {
            source = SceneManager.GetSceneByPath(SourceScenePath);
            if (!source.IsValid() || !source.isLoaded)
            {
                source = EditorSceneManager.OpenScene(SourceScenePath,
                    OpenSceneMode.Additive);
                sourceOpenedByBuild = true;
            }
            if (openLab.IsValid() && openLab.isLoaded &&
                !EditorSceneManager.CloseScene(openLab, true))
                throw new InvalidOperationException(
                    "Não foi possível fechar temporariamente a cena limpa do laboratório.");
            Dictionary<string, TerritorioClique> sourceTerritories =
                FindSourceTerritories(source);
            ValidateSourceTerritories(sourceTerritories);

            lab = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
            lab.name = "WDCombatContextLab";
            BuildLabScene(lab, sourceTerritories);
            ValidateGeneratedScene(lab);
            EditorSceneManager.MarkSceneDirty(lab);
            if (!EditorSceneManager.SaveScene(lab, ScenePath))
                throw new InvalidOperationException("Não foi possível salvar " + ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[WDCombatContextLab] Cena criada em " + ScenePath);
        }
        finally
        {
            if (lab.IsValid() && lab.isLoaded)
                EditorSceneManager.CloseScene(lab, true);
            if (sourceOpenedByBuild && source.IsValid() && source.isLoaded)
                EditorSceneManager.CloseScene(source, true);
            EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }
    }

    private static Dictionary<string, TerritorioClique> FindSourceTerritories(Scene scene)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<TerritorioClique>(true))
            .Where(item => item != null && TerritoryIds.Contains(item.idTerritorio))
            .GroupBy(item => item.idTerritorio, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(),
                StringComparer.Ordinal);
    }

    private static void ValidateSourceTerritories(
        IReadOnlyDictionary<string, TerritorioClique> territories)
    {
        foreach (string id in TerritoryIds)
            if (!territories.ContainsKey(id))
                throw new InvalidOperationException(
                    $"Território real '{id}' não foi encontrado em {SourceScenePath}.");
    }

    // ============================================================
    // 02. COMPOSIÇÃO — mundo real, overlay oficial e controles externos
    // ============================================================
    private static void BuildLabScene(Scene scene,
        IReadOnlyDictionary<string, TerritorioClique> sourceTerritories)
    {
        GameObject root = new("WDCombatContextLab");
        SceneManager.MoveGameObjectToScene(root, scene);
        Transform world = new GameObject("01_RealTerritoryStage").transform;
        world.SetParent(root.transform, false);

        TerritorioClique brazil = CloneTerritory(sourceTerritories["Brazil"], world, scene);
        TerritorioClique venezuela = CloneTerritory(sourceTerritories["Venezuela"], world, scene);
        TerritorioClique argentina = CloneTerritory(sourceTerritories["Argentina"], world, scene);
        TerritorioClique peru = CloneTerritory(sourceTerritories["Peru"], world, scene);
        var localTerritories = new Dictionary<string, TerritorioClique>(StringComparer.Ordinal)
        {
            ["Brazil"] = brazil,
            ["Venezuela"] = venezuela,
            ["Argentina"] = argentina,
            ["Peru"] = peru
        };
        RemapSceneReferences(sourceTerritories, localTerritories, sourceTerritories["Brazil"].gameObject.scene);

        Camera camera = CreateCamera(root.transform, new[]
            { brazil, venezuela, argentina, peru });
        Canvas canvas = CreateCanvas(root.transform);
        RectTransform overlay = CreateStretchLayer("MapOverlayLayer", canvas.transform);
        RectTransform controls = CreateControlPanel(canvas.transform);

        TMP_Dropdown dropdown = CreateDropdown(controls, new Vector2(0f, -26f));
        Button start = CreateButton(controls, "Start", "INICIAR", new Vector2(-285f, -94f));
        Button reset = CreateButton(controls, "Reset", "REINICIAR", new Vector2(-95f, -94f));
        Button speed = CreateButton(controls, "Speed", "VELOCIDADE 1×", new Vector2(105f, -94f));
        Button stop = CreateButton(controls, "Stop", "INTERROMPER", new Vector2(305f, -94f));
        TMP_Text status = CreateText(controls, "Status",
            new Vector2(0f, -150f), new Vector2(900f, 36f), 16f);

        GameObject eventSystem = new("EventSystem", typeof(EventSystem),
            typeof(InputSystemUIInputModule));
        eventSystem.transform.SetParent(root.transform, false);
        ConfigureInputSystem(eventSystem.GetComponent<InputSystemUIInputModule>());

        WDCombatContextLabController controller =
            root.AddComponent<WDCombatContextLabController>();
        SerializedObject serialized = new(controller);
        Set(serialized, "origin", brazil);
        Set(serialized, "enemy", venezuela);
        Set(serialized, "ally", argentina);
        Set(serialized, "handoff", peru);
        Set(serialized, "mapCamera", camera);
        Set(serialized, "mapOverlay", overlay);
        Set(serialized, "theme", AssetDatabase.LoadAssetAtPath<WarDominionUITheme>(
            "Assets/Resources/UI/WarDominionUITheme.asset"));
        Set(serialized, "scenarioDropdown", dropdown);
        Set(serialized, "speedLabel", speed.GetComponentInChildren<TMP_Text>());
        Set(serialized, "statusLabel", status);
        Set(serialized, "startButton", start);
        Set(serialized, "resetButton", reset);
        Set(serialized, "stopButton", stop);
        Set(serialized, "speedButton", speed);
        // 02.1. PRESET APROVADO — a cena gerada recebe valores persistentes,
        // ainda editáveis no Inspector e sem modificar o tema da partida.
        Set(serialized, "indicatorOpacity",
            WDPreparedIndicatorStyle.LabApprovedIndicatorOpacity);
        Set(serialized, "chevronArmLength",
            WDPreparedIndicatorStyle.LabApprovedChevronArmLength);
        Set(serialized, "chevronThickness",
            WDPreparedIndicatorStyle.LabApprovedChevronThickness);
        Set(serialized, "dotRadius", WDPreparedIndicatorStyle.LabApprovedDotRadius);
        Set(serialized, "glowIntensity",
            WDPreparedIndicatorStyle.LabApprovedGlowIntensity);
        Set(serialized, "contrast", WDPreparedIndicatorStyle.LabApprovedContrast);
        Set(serialized, "animationSpeedMultiplier",
            WDPreparedIndicatorStyle.LabApprovedAnimationSpeedMultiplier);
        ApplyVisualPreset(serialized);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ApplyVisualPreset(SerializedObject target)
    {
        if (!File.Exists(PresetPath))
            return;
        VisualPreset p = JsonUtility.FromJson<VisualPreset>(
            File.ReadAllText(PresetPath));
        if (p == null)
            return;
        foreach (string name in new[] { "indicatorOpacity", "chevronArmLength",
            "chevronThickness", "dotRadius", "glowIntensity", "contrast",
            "animationSpeedMultiplier", "confirmedArrowOpacity",
            "confirmedBodyThickness", "confirmedOutlineThickness",
            "confirmedOuterGlow", "confirmedReflection", "confirmedHeadLength",
            "confirmedHeadWidth", "confirmedCardBorderThickness",
            "confirmedCardTopReflection", "confirmedCardCornerRadius",
            "confirmedCardWidth", "confirmedCardHeight", "confirmedNumberSize",
            "confirmedShadowThickness", "confirmedShadowIntensity",
            "confirmedSpecularIntensity", "confirmedReliefIntensity",
            "confirmedSurfaceOpacity", "confirmedOriginMargin",
            "confirmedDestinationMargin", "confirmedCurveHeight",
            "confirmedOriginExtension", "confirmedDestinationExtension",
            "confirmedCurvePosition" })
            Set(target, name, (float)typeof(VisualPreset).GetField(name)
                .GetValue(p));
        Set(target, "confirmedAdaptiveOutline", p.confirmedAdaptiveOutline);
        SetColor(target, "confirmedArrowTint", p.confirmedArrowTint);
        SetColor(target, "confirmedOutlineColor", p.confirmedOutlineColor);
        SetColor(target, "confirmedCardBackground", p.confirmedCardBackground);
        SetColor(target, "confirmedCardBorder", p.confirmedCardBorder);
        SetColor(target, "confirmedNumberColor", p.confirmedNumberColor);
    }

    private static VisualPreset ReadPreset(SerializedObject source)
    {
        var p = new VisualPreset();
        foreach (string name in new[] { "indicatorOpacity", "chevronArmLength",
            "chevronThickness", "dotRadius", "glowIntensity", "contrast",
            "animationSpeedMultiplier", "confirmedArrowOpacity",
            "confirmedBodyThickness", "confirmedOutlineThickness",
            "confirmedOuterGlow", "confirmedReflection", "confirmedHeadLength",
            "confirmedHeadWidth", "confirmedCardBorderThickness",
            "confirmedCardTopReflection", "confirmedCardCornerRadius",
            "confirmedCardWidth", "confirmedCardHeight", "confirmedNumberSize",
            "confirmedShadowThickness", "confirmedShadowIntensity",
            "confirmedSpecularIntensity", "confirmedReliefIntensity",
            "confirmedSurfaceOpacity", "confirmedOriginMargin",
            "confirmedDestinationMargin", "confirmedCurveHeight",
            "confirmedOriginExtension", "confirmedDestinationExtension",
            "confirmedCurvePosition" })
            typeof(VisualPreset).GetField(name).SetValue(p,
                source.FindProperty(name).floatValue);
        p.confirmedAdaptiveOutline = source
            .FindProperty("confirmedAdaptiveOutline").boolValue;
        p.confirmedArrowTint = Hex(source.FindProperty("confirmedArrowTint").colorValue);
        p.confirmedOutlineColor = Hex(source.FindProperty("confirmedOutlineColor").colorValue);
        p.confirmedCardBackground = Hex(source.FindProperty("confirmedCardBackground").colorValue);
        p.confirmedCardBorder = Hex(source.FindProperty("confirmedCardBorder").colorValue);
        p.confirmedNumberColor = Hex(source.FindProperty("confirmedNumberColor").colorValue);
        return p;
    }

    private static string Hex(Color value) => "#" +
        ColorUtility.ToHtmlStringRGBA(value);

    private static void SetColor(SerializedObject target, string name,
        string hex)
    {
        if (!string.IsNullOrWhiteSpace(hex) && !hex.StartsWith("#"))
            hex = "#" + hex;
        if (ColorUtility.TryParseHtmlString(hex, out Color value))
            target.FindProperty(name).colorValue = value;
    }

    private static TerritorioClique CloneTerritory(TerritorioClique source,
        Transform parent, Scene scene)
    {
        GameObject clone = UnityEngine.Object.Instantiate(source.gameObject);
        clone.name = source.gameObject.name + "_ContextLab";
        SceneManager.MoveGameObjectToScene(clone, scene);
        clone.transform.SetParent(parent, true);
        // 02.2.1. RESÍDUO SERIALIZADO — o Brazil original ainda referencia o
        // antigo TerritorioBordaVisual, cujo script não existe no projeto.
        // Remove somente behaviours realmente ausentes no clone experimental.
        foreach (Transform child in clone.GetComponentsInChildren<Transform>(true))
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                child.gameObject) > 0)
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(
                    child.gameObject);
        // 02.2. O clone mantém integralmente os componentes do território e do
        // contador. Desabilitação genérica também atingiria TMP e destruiria a
        // fidelidade que o laboratório precisa comprovar.
        return clone.GetComponent<TerritorioClique>();
    }

    // ============================================================
    // 02.3. REFERÊNCIAS — vizinhos locais são remapeados; externos são removidos
    // ============================================================
    private static void RemapSceneReferences(
        IReadOnlyDictionary<string, TerritorioClique> sourceTerritories,
        IReadOnlyDictionary<string, TerritorioClique> localTerritories,
        Scene sourceScene)
    {
        foreach (KeyValuePair<string, TerritorioClique> pair in localTerritories)
        {
            TerritorioFronteiras sourceBorders =
                sourceTerritories[pair.Key].GetComponent<TerritorioFronteiras>();
            TerritorioFronteiras localBorders =
                pair.Value.GetComponent<TerritorioFronteiras>();
            if (localBorders != null)
            {
                TerritorioClique[] localNeighbours = sourceBorders == null
                    ? Array.Empty<TerritorioClique>()
                    : sourceBorders.VizinhosLegados
                        .Where(neighbour => neighbour != null &&
                            localTerritories.ContainsKey(neighbour.idTerritorio))
                        .Select(neighbour => localTerritories[neighbour.idTerritorio])
                        .ToArray();
                localBorders.Configurar(localNeighbours);
            }

            foreach (Component component in pair.Value.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                    continue;
                SerializedObject serialized = new(component);
                SerializedProperty property = serialized.GetIterator();
                bool changed = false;
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference ||
                        property.objectReferenceValue == null)
                        continue;
                    if (!TryGetScene(property.objectReferenceValue, out Scene referencedScene) ||
                        referencedScene != sourceScene)
                        continue;

                    if (property.objectReferenceValue is TerritorioClique sourceTerritory &&
                        localTerritories.TryGetValue(sourceTerritory.idTerritorio,
                            out TerritorioClique localTerritory))
                        property.objectReferenceValue = localTerritory;
                    else
                        property.objectReferenceValue = null;
                    changed = true;
                }
                if (changed)
                    serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    private static bool TryGetScene(UnityEngine.Object value, out Scene scene)
    {
        if (value is Component component)
        {
            scene = component.gameObject.scene;
            return scene.IsValid();
        }
        if (value is GameObject gameObject)
        {
            scene = gameObject.scene;
            return scene.IsValid();
        }
        scene = default;
        return false;
    }

    // ============================================================
    // 02.4. INPUT — referências pertencentes ao asset oficial do Input System
    // ============================================================
    private static void ConfigureInputSystem(InputSystemUIInputModule module)
    {
        InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
            "Assets/InputSystem_Actions.inputactions");
        if (actions == null)
            throw new InvalidOperationException(
                "InputSystem_Actions.inputactions não foi encontrado.");

        // 02.4.1. Substitui primeiro todas as referências temporárias criadas
        // pelo componente. Definir actionsAsset antes faria o módulo tentar
        // migrar as ações avulsas de AssignDefaultActions e repetiria a exceção.
        module.point = Reference(actions, "UI/Point");
        module.leftClick = Reference(actions, "UI/Click");
        module.rightClick = Reference(actions, "UI/RightClick");
        module.middleClick = Reference(actions, "UI/MiddleClick");
        module.scrollWheel = Reference(actions, "UI/ScrollWheel");
        module.move = Reference(actions, "UI/Navigate");
        module.submit = Reference(actions, "UI/Submit");
        module.cancel = Reference(actions, "UI/Cancel");
        module.trackedDevicePosition = Reference(actions, "UI/TrackedDevicePosition");
        module.trackedDeviceOrientation = Reference(actions, "UI/TrackedDeviceOrientation");
        module.actionsAsset = actions;
    }

    private static InputActionReference Reference(InputActionAsset asset, string path)
    {
        InputAction action = asset.FindAction(path, true);
        return InputActionReference.Create(action);
    }

    // ============================================================
    // 02.5. VALIDAÇÃO — impede salvar input avulso ou referência entre cenas
    // ============================================================
    private static void ValidateGeneratedScene(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        InputSystemUIInputModule module = roots
            .SelectMany(root => root.GetComponentsInChildren<InputSystemUIInputModule>(true))
            .SingleOrDefault();
        InputActionAsset expectedAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
            "Assets/InputSystem_Actions.inputactions");
        if (module == null || module.actionsAsset != expectedAsset)
            throw new InvalidOperationException(
                "O laboratório não está ligado ao InputActionAsset oficial.");

        InputActionReference[] references =
        {
            module.point, module.leftClick, module.rightClick, module.middleClick,
            module.scrollWheel, module.move, module.submit, module.cancel,
            module.trackedDevicePosition, module.trackedDeviceOrientation
        };
        if (references.Any(reference => reference == null || reference.action == null ||
            reference.action.actionMap == null ||
            reference.action.actionMap.asset != expectedAsset))
            throw new InvalidOperationException(
                "O módulo de UI contém uma ação sem vínculo com o asset oficial.");

        int territoryCount = roots
            .SelectMany(root => root.GetComponentsInChildren<TerritorioClique>(true))
            .Count(territory => TerritoryIds.Contains(territory.idTerritorio));
        if (territoryCount != TerritoryIds.Length)
            throw new InvalidOperationException(
                $"O laboratório contém {territoryCount} de {TerritoryIds.Length} territórios.");

        string[] buttonNames = { "Start", "Reset", "Speed", "Stop" };
        foreach (string buttonName in buttonNames)
        {
            Button button = roots
                .SelectMany(root => root.GetComponentsInChildren<Button>(true))
                .SingleOrDefault(item => item.name == buttonName);
            RectTransform label = button == null
                ? null
                : button.transform.Find("Label") as RectTransform;
            if (label == null || label.anchorMin != Vector2.zero ||
                label.anchorMax != Vector2.one || label.offsetMin != Vector2.zero ||
                label.offsetMax != Vector2.zero)
                throw new InvalidOperationException(
                    $"O rótulo do botão {buttonName} não ocupa sua área clicável.");
        }

        foreach (Component component in roots
            .SelectMany(root => root.GetComponentsInChildren<Component>(true)))
        {
            if (component == null)
                continue;
            SerializedProperty property = new SerializedObject(component).GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference ||
                    property.objectReferenceValue == null ||
                    !TryGetScene(property.objectReferenceValue, out Scene referencedScene))
                    continue;
                if (referencedScene != scene)
                    throw new InvalidOperationException(
                        $"Referência entre cenas detectada em {component.name}." +
                        $"{property.propertyPath}: {referencedScene.path}");
            }
        }

        foreach (GameObject gameObject in roots.SelectMany(root =>
            root.GetComponentsInChildren<Transform>(true)).Select(item => item.gameObject))
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject) > 0)
                throw new InvalidOperationException(
                    $"Script ausente detectado em {gameObject.name}.");
    }

    private static Camera CreateCamera(Transform parent,
        IReadOnlyList<TerritorioClique> territories)
    {
        Bounds bounds = territories[0].GetComponent<Renderer>().bounds;
        foreach (TerritorioClique territory in territories)
            foreach (Renderer renderer in territory.GetComponentsInChildren<Renderer>(true))
                bounds.Encapsulate(renderer.bounds);
        GameObject go = new("LabMapCamera", typeof(Camera));
        go.tag = "MainCamera";
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10f);
        Camera camera = go.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = Mathf.Max(3.4f, bounds.extents.y * 1.55f);
        camera.backgroundColor = new Color(.018f, .035f, .055f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        return camera;
    }

    private static Canvas CreateCanvas(Transform parent)
    {
        GameObject go = new("02_OfficialCanvas_1920x1080", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(parent, false);
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        return canvas;
    }

    private static RectTransform CreateStretchLayer(string name, Transform parent)
    {
        GameObject go = new(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static RectTransform CreateControlPanel(Transform parent)
    {
        GameObject go = new("03_LabControls", typeof(RectTransform), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
        rect.pivot = new Vector2(.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -22f);
        rect.sizeDelta = new Vector2(1040f, 205f);
        go.GetComponent<Image>().color = new Color(.025f, .055f, .09f, .96f);
        return rect;
    }

    private static TMP_Dropdown CreateDropdown(Transform parent, Vector2 position)
    {
        GameObject go = new("Scenario", typeof(RectTransform), typeof(Image),
            typeof(TMP_Dropdown));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(780f, 48f);
        go.GetComponent<Image>().color = new Color(.04f, .10f, .16f, 1f);
        TMP_Text caption = CreateText(go.transform, "Label", Vector2.zero,
            rect.sizeDelta - new Vector2(52f, 0f), 18f);
        caption.alignment = TextAlignmentOptions.Left;
        // 02.5.1. CAMPO SELECIONADO — stretch real dentro do dropdown evita
        // que o helper ancorado ao topo desloque o texto meia altura acima.
        StretchDropdownLabel(caption.rectTransform, 16f, 52f);
        TMP_Dropdown dropdown = go.GetComponent<TMP_Dropdown>();
        dropdown.captionText = caption;

        // 02.1. Estrutura padrão completa do TMP_Dropdown. O laboratório não
        // depende de prefab de UI e continua selecionável por mouse e toque.
        GameObject templateObject = new("Template", typeof(RectTransform),
            typeof(Image), typeof(ScrollRect));
        RectTransform template = templateObject.GetComponent<RectTransform>();
        template.SetParent(go.transform, false);
        template.anchorMin = new Vector2(0f, 0f);
        template.anchorMax = new Vector2(1f, 0f);
        template.pivot = new Vector2(.5f, 1f);
        template.anchoredPosition = new Vector2(0f, -2f);
        template.sizeDelta = new Vector2(0f, 320f);
        templateObject.GetComponent<Image>().color = new Color(.025f, .06f, .095f, 1f);

        GameObject viewportObject = new("Viewport", typeof(RectTransform),
            typeof(Image), typeof(Mask));
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        viewport.SetParent(template, false);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = viewport.offsetMax = Vector2.zero;
        viewportObject.GetComponent<Image>().color = Color.white;
        viewportObject.GetComponent<Mask>().showMaskGraphic = false;

        GameObject contentObject = new("Content", typeof(RectTransform));
        RectTransform content = contentObject.GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1f);
        content.sizeDelta = new Vector2(0f, 320f);

        GameObject itemObject = new("Item", typeof(RectTransform), typeof(Toggle));
        RectTransform item = itemObject.GetComponent<RectTransform>();
        item.SetParent(content, false);
        item.anchorMin = new Vector2(0f, 1f);
        item.anchorMax = Vector2.one;
        item.pivot = new Vector2(.5f, 1f);
        item.anchoredPosition = Vector2.zero;
        item.sizeDelta = new Vector2(0f, 40f);
        GameObject backgroundObject = new("Item Background", typeof(RectTransform),
            typeof(Image));
        RectTransform background = backgroundObject.GetComponent<RectTransform>();
        background.SetParent(item, false);
        background.anchorMin = Vector2.zero;
        background.anchorMax = Vector2.one;
        background.offsetMin = background.offsetMax = Vector2.zero;
        backgroundObject.GetComponent<Image>().color = new Color(.07f, .18f, .25f, .9f);
        TMP_Text itemLabel = CreateText(item, "Item Label", Vector2.zero,
            new Vector2(720f, 40f), 16f);
        itemLabel.alignment = TextAlignmentOptions.Left;
        // 02.5.2. OPÇÕES — cada label ocupa exatamente a linha clicável que o
        // TMP_Dropdown clona, mantendo texto e índice sob o mesmo Toggle.
        StretchDropdownLabel(itemLabel.rectTransform, 16f, 16f);
        Toggle toggle = itemObject.GetComponent<Toggle>();
        toggle.targetGraphic = backgroundObject.GetComponent<Image>();

        ScrollRect scroll = templateObject.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        dropdown.template = template;
        dropdown.itemText = itemLabel;
        templateObject.SetActive(false);
        return dropdown;
    }

    internal static void StretchDropdownLabel(RectTransform rect,
        float left, float right)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = new Vector2(left, 0f);
        rect.offsetMax = new Vector2(-right, 0f);
        rect.anchoredPosition = Vector2.zero;
    }

    private static Button CreateButton(Transform parent, string name,
        string label, Vector2 position)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(180f, 48f);
        go.GetComponent<Image>().color = new Color(.055f, .13f, .20f, 1f);
        TMP_Text buttonLabel = CreateText(go.transform, "Label", Vector2.zero,
            rect.sizeDelta, 16f);
        // 02.6. RÓTULO DO BOTÃO — ocupa exatamente o RectTransform clicável.
        // O helper genérico ancora textos pelo topo; no botão isso deslocava o
        // centro vertical em meia altura. O stretch local preserva o botão.
        RectTransform labelRect = buttonLabel.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.pivot = new Vector2(.5f, .5f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        buttonLabel.alignment = TextAlignmentOptions.Center;
        buttonLabel.text = label;
        return go.GetComponent<Button>();
    }

    private static TMP_Text CreateText(Transform parent, string name,
        Vector2 position, Vector2 size, float fontSize)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    // ============================================================
    // 03. SUPORTE DE AUTORIA — somente assets novos do laboratório
    // ============================================================
    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes/Development"))
            AssetDatabase.CreateFolder("Assets/Scenes", "Development");
    }

    private static void Set(SerializedObject serialized, string property,
        UnityEngine.Object value)
    {
        SerializedProperty target = serialized.FindProperty(property) ??
            throw new MissingFieldException(serialized.targetObject.GetType().Name, property);
        target.objectReferenceValue = value;
    }

    private static void Set(SerializedObject serialized, string property,
        float value)
    {
        SerializedProperty target = serialized.FindProperty(property) ??
            throw new MissingFieldException(serialized.targetObject.GetType().Name, property);
        target.floatValue = value;
    }

    private static void Set(SerializedObject serialized, string property,
        bool value)
    {
        SerializedProperty target = serialized.FindProperty(property) ??
            throw new MissingFieldException(serialized.targetObject.GetType().Name, property);
        target.boolValue = value;
    }
}
