using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Gera exclusivamente os assets isolados do laboratório visual.</summary>
public static class WDCombatAnimationLabAuthoring
{
    public const string ScenePath = "Assets/Scenes/Development/WDCombatAnimationLab.unity";
    public const string PresetPath = "Assets/Resources/Development/WDCombatMotionPreset.asset";
    public const string ParticlePrefabPath = "Assets/Prefabs/Development/WDCombatImpactParticleLab.prefab";
    private const string SpritePath = "Assets/Editor Default Resources/Development/WDCombatLabDisc.png";

    // ============================================================
    // 01. AUTHORING AUTOMÁTICO SEGURO — cria apenas assets ausentes
    // ============================================================
    [InitializeOnLoadMethod]
    private static void ScheduleMissingAssets()
    {
        if (File.Exists(ScenePath) && File.Exists(PresetPath) && File.Exists(ParticlePrefabPath)) return;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.delayCall += BuildMissingAssets;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.delayCall += BuildMissingAssets;
    }

    [MenuItem("War Dominion/Development/Combat Animation Lab/Open")]
    public static void OpenLab()
    {
        BuildMissingAssets();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    [MenuItem("War Dominion/Development/Combat Animation Lab/Rebuild Isolated Assets")]
    public static void RebuildLab()
    {
        if (!EditorUtility.DisplayDialog("Reconstruir laboratório",
                "Somente a cena e o prefab experimentais serão substituídos. O preset será preservado.",
                "Reconstruir", "Cancelar")) return;
        AssetDatabase.DeleteAsset(ScenePath);
        AssetDatabase.DeleteAsset(ParticlePrefabPath);
        BuildMissingAssets();
    }

    private static void BuildMissingAssets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        EnsureFolders();
        Sprite disc = EnsureDiscSprite();
        WDCombatMotionPreset preset = EnsurePreset();
        GameObject particlePrefab = EnsureParticlePrefab(disc);
        if (!File.Exists(ScenePath)) BuildScene(preset, particlePrefab, disc);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // ============================================================
    // 02. RECURSOS EDITÁVEIS — preset e partículas persistentes
    // ============================================================
    private static WDCombatMotionPreset EnsurePreset()
    {
        WDCombatMotionPreset preset = AssetDatabase.LoadAssetAtPath<WDCombatMotionPreset>(PresetPath);
        if (preset != null) return preset;
        preset = ScriptableObject.CreateInstance<WDCombatMotionPreset>();
        AssetDatabase.CreateAsset(preset, PresetPath);
        return preset;
    }

    private static GameObject EnsureParticlePrefab(Sprite disc)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ParticlePrefabPath);
        if (prefab != null) return prefab;

        GameObject root = new("ImpactParticles_Editable", typeof(ParticleSystem));
        ParticleSystem system = root.GetComponent<ParticleSystem>();
        ParticleSystem.MainModule main = system.main;
        main.duration = 0.55f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.32f, 0.58f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.32f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.76f, 0.63f, 0.38f, 0.88f), new Color(0.16f, 0.82f, 0.92f, 0.65f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.useUnscaledTime = true;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });
        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.24f;
        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.35f, 0.4f, 0.46f), 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
        ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = 40;
        renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
        if (disc != null) renderer.renderMode = ParticleSystemRenderMode.Billboard;
        prefab = PrefabUtility.SaveAsPrefabAsset(root, ParticlePrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    // ============================================================
    // 03. CENA — hierarquia explícita, persistente e sem gameplay
    // ============================================================
    private static void BuildScene(WDCombatMotionPreset preset, GameObject particlePrefab, Sprite disc)
    {
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            scene.name = "WDCombatAnimationLab";
            GameObject root = new("WDCombatAnimationLab");
            SceneManager.MoveGameObjectToScene(root, scene);
            GameObject cameraObject = new("LabCamera", typeof(Camera));
            cameraObject.transform.SetParent(root.transform);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.4f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.backgroundColor = new Color(0.018f, 0.038f, 0.065f);

            Transform stage = new GameObject("01_WorldStage").transform;
            stage.SetParent(root.transform);
            Transform origin = CreateTerritory(stage, "OriginTerritory_EDIT_ME", new Vector3(-3.2f, 0.4f),
                new Color(0.06f, 0.68f, 0.78f), disc, 18);
            Transform destination = CreateTerritory(stage, "DestinationTerritory_EDIT_ME", new Vector3(3.2f, 0.4f),
                new Color(0.68f, 0.20f, 0.28f), disc, 11);
            Transform travelling = CreateCounter(stage, "TravellingCounter_EXPERIMENTAL", origin.position, disc, 18,
                new Color(0.06f, 0.68f, 0.78f));
            Transform destinationCounter = destination.Find("ArmyCounter");
            SpriteRenderer glow = CreateGlow(destination, disc);

            Transform effects = new GameObject("02_ImpactAndParticles_EDIT_IN_INSPECTOR").transform;
            effects.SetParent(root.transform);
            effects.position = destination.position;
            GameObject particle = (GameObject)PrefabUtility.InstantiatePrefab(particlePrefab, scene);
            particle.name = "ImpactParticles_EDIT_ME";
            particle.transform.SetParent(effects, false);
            ParticleSystem particleSystem = particle.GetComponent<ParticleSystem>();

            GameObject adapterObject = new("03_SpatialAdapter_FUTURE_DOTWEEN_BRIDGE",
                typeof(WDCombatMotionAdapter));
            adapterObject.transform.SetParent(root.transform);
            WDCombatMotionAdapter adapter = adapterObject.GetComponent<WDCombatMotionAdapter>();
            adapter.Configure(origin, destination, travelling, preset);

            Canvas canvas = CreateCanvas(root.transform, camera);
            Button play = CreateButton(canvas.transform, "PlayButton", "INICIAR", new Vector2(-300f, -445f));
            Button reset = CreateButton(canvas.transform, "ResetButton", "REINICIAR", new Vector2(-100f, -445f));
            Button speed = CreateButton(canvas.transform, "SpeedButton", "VELOCIDADE 1×", new Vector2(120f, -445f));
            Button stop = CreateButton(canvas.transform, "StopButton", "INTERROMPER", new Vector2(340f, -445f));
            TMP_Text speedText = speed.GetComponentInChildren<TMP_Text>();
            CreateHeading(canvas.transform);
            // ============================================================
            // 05. INPUT — módulo compatível com mouse e toque do Input System
            // ============================================================
            GameObject eventSystemObject = new("EventSystem", typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            eventSystemObject.transform.SetParent(root.transform);
            eventSystemObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

            WDCombatAnimationLabController controller = root.AddComponent<WDCombatAnimationLabController>();
            SerializedObject serialized = new(controller);
            Set(serialized, "preset", preset);
            Set(serialized, "spatialAdapter", adapter);
            Set(serialized, "travellingCounter", travelling);
            Set(serialized, "destinationCounter", destinationCounter);
            Set(serialized, "destinationGlow", glow);
            Set(serialized, "impactParticles", particleSystem);
            Set(serialized, "speedLabel", speedText);
            Set(serialized, "playButton", play);
            Set(serialized, "resetButton", reset);
            Set(serialized, "speedButton", speed);
            Set(serialized, "stopButton", stop);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Não foi possível salvar o laboratório.");
        }
        finally
        {
            if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            EditorSceneManager.RestoreSceneManagerSetup(previous);
        }
    }

    private static Transform CreateTerritory(Transform parent, string name, Vector3 position,
        Color color, Sprite sprite, int troops)
    {
        GameObject territory = new(name, typeof(SpriteRenderer));
        territory.transform.SetParent(parent);
        territory.transform.position = position;
        territory.transform.localScale = new Vector3(2.2f, 1.55f, 1f);
        SpriteRenderer renderer = territory.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = new Color(color.r * 0.42f, color.g * 0.42f, color.b * 0.42f, 1f);
        renderer.sortingOrder = 1;
        Transform counter = CreateCounter(territory.transform, "ArmyCounter", position, sprite, troops, color);
        counter.localPosition = Vector3.zero;
        counter.localScale = new Vector3(0.72f / territory.transform.localScale.x,
            0.72f / territory.transform.localScale.y, 0.72f);
        return territory.transform;
    }

    private static Transform CreateCounter(Transform parent, string name, Vector3 position,
        Sprite sprite, int troops, Color color)
    {
        GameObject counter = new(name, typeof(SpriteRenderer));
        counter.transform.SetParent(parent);
        counter.transform.position = position;
        counter.transform.localScale = Vector3.one * 0.72f;
        SpriteRenderer renderer = counter.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = 20;
        GameObject label = new("TroopCount", typeof(TextMeshPro));
        label.transform.SetParent(counter.transform, false);
        label.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        TextMeshPro text = label.GetComponent<TextMeshPro>();
        text.text = troops.ToString();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 4f;
        text.color = Color.white;
        text.sortingOrder = 21;
        text.rectTransform.sizeDelta = Vector2.one;
        return counter.transform;
    }

    private static SpriteRenderer CreateGlow(Transform destination, Sprite sprite)
    {
        GameObject glow = new("ImpactGlow_EDIT_ME", typeof(SpriteRenderer));
        glow.transform.SetParent(destination, false);
        glow.transform.localScale = Vector3.one * 1.25f;
        SpriteRenderer renderer = glow.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = new Color(0.93f, 0.73f, 0.32f, 0f);
        renderer.sortingOrder = 3;
        return renderer;
    }

    private static Canvas CreateCanvas(Transform parent, Camera camera)
    {
        GameObject canvasObject = new("04_ExperimentalCanvas_1920x1080", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(parent);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        return canvas;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 position)
    {
        GameObject root = new(name, typeof(RectTransform), typeof(Image), typeof(Button));
        root.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(180f, 54f);
        root.GetComponent<Image>().color = new Color(0.07f, 0.13f, 0.20f, 0.96f);
        GameObject textObject = new("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(root.transform, false);
        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 18f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.86f, 0.73f, 0.49f);
        return root.GetComponent<Button>();
    }

    private static void CreateHeading(Transform parent)
    {
        GameObject titleObject = new("LabInstructions", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObject.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)titleObject.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -35f);
        rect.sizeDelta = new Vector2(1200f, 90f);
        TextMeshProUGUI text = titleObject.GetComponent<TextMeshProUGUI>();
        text.text = "LABORATÓRIO DE ANIMAÇÕES DE COMBATE\nEdite o preset e as partículas no Inspector";
        text.fontSize = 25f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
    }

    // ============================================================
    // 04. ASSET VISUAL NEUTRO — exclusivo do laboratório
    // ============================================================
    private static Sprite EnsureDiscSprite()
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (sprite != null) return sprite;
        const int size = 128;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        Vector2 center = Vector2.one * (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float distance = Vector2.Distance(new Vector2(x, y), center) / (size * 0.5f);
            float alpha = Mathf.Clamp01((1f - distance) * 18f);
            pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
        }
        texture.SetPixels(pixels);
        texture.Apply();
        File.WriteAllBytes(SpritePath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 128f;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "Scenes"); EnsureFolder("Assets/Scenes", "Development");
        EnsureFolder("Assets", "Resources"); EnsureFolder("Assets/Resources", "Development");
        EnsureFolder("Assets", "Prefabs"); EnsureFolder("Assets/Prefabs", "Development");
        EnsureFolder("Assets", "Editor Default Resources");
        EnsureFolder("Assets/Editor Default Resources", "Development");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }

    private static void Set(SerializedObject serialized, string property, UnityEngine.Object value)
    {
        SerializedProperty target = serialized.FindProperty(property);
        if (target == null) throw new MissingFieldException(serialized.targetObject.GetType().Name, property);
        target.objectReferenceValue = value;
    }
}
