#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

public static class WDVisualKitAssetBuilder
{
    private const string Root = "Assets/VisualKit";
    private const string ThemePath = "Assets/Resources/UI/WarDominionUITheme.asset";
    private const string TypographyPath = Root + "/Typography";
    private const string PrefabPath = Root + "/Prefabs";
    private const string ValidationPath = Root + "/Validation";

    private const string PortugueseCharacters =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789" +
        " ÁÀÂÃÉÊÍÓÔÕÚÜÇáàâãéêíóôõúüç" +
        ".,:;!?¿¡()[]{}+-–—/_%#@&'\"°×→←•·";

    // ============================================================
    // 01. ENTRADA ÚNICA E IDEMPOTENTE
    // ============================================================

    [MenuItem("War Dominion/VisualKit/Rebuild Official Assets")]
    public static void BuildAll()
    {
        EnsureFolders();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureTextureImports();
        Dictionary<string, TMP_FontAsset> fonts = BuildFontAssets();
        WarDominionUITheme theme = ConfigureTheme(fonts);
        BuildComponentPrefabs(theme);
        BuildValidationScene(theme);
        ValidateGeneratedAssets(theme);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("[WD-VISUALKIT] Importação, TMP Font Assets, Prefabs e galeria concluídos.");
    }

    // ============================================================
    // 02. IMPORTAÇÃO FÍSICA
    // ============================================================

    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "VisualKit");
        EnsureFolder(Root, "Typography");
        EnsureFolder(Root, "Prefabs");
        EnsureFolder(Root, "Validation");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }

    private static void ConfigureTextureImports()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;

            bool isNineSlice = path.Contains("/Sprites/Rounded", StringComparison.Ordinal);
            bool isBackground = path.Contains("/Backgrounds/", StringComparison.Ordinal);
            bool isWeapon = path.Contains("/Weapons/", StringComparison.Ordinal);
            bool isIcon = path.Contains("/Icons/", StringComparison.Ordinal);
            if (!isNineSlice && !isBackground && !isWeapon && !isIcon) continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = isIcon || isNineSlice
                ? TextureImporterCompression.Uncompressed
                : TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = isBackground ? 2048 : (isWeapon ? 2048 : 256);
            if (isNineSlice) importer.spriteBorder = new Vector4(16f, 16f, 16f, 16f);
            importer.SaveAndReimport();
        }
    }

    // ============================================================
    // 03. TIPOGRAFIA TMP ESTÁTICA
    // ============================================================

    private static Dictionary<string, TMP_FontAsset> BuildFontAssets()
    {
        var result = new Dictionary<string, TMP_FontAsset>();
        BuildFont("Gabarito-Bold", "Assets/Resources/UI/Fonts/Gabarito-Bold.ttf", result);
        BuildFont("Gabarito-ExtraBold", "Assets/Resources/UI/Fonts/Gabarito-ExtraBold.ttf", result);
        BuildFont("Gabarito-Black", "Assets/Resources/UI/Fonts/Gabarito-Black.ttf", result);
        BuildFont("Inter-Regular", "Assets/Resources/UI/Fonts/Inter-Regular.otf", result);
        BuildFont("Inter-Medium", "Assets/Resources/UI/Fonts/Inter-Medium.otf", result);
        BuildFont("Inter-SemiBold", "Assets/Resources/UI/Fonts/Inter-SemiBold.otf", result);
        return result;
    }

    private static void BuildFont(string name, string sourcePath,
        IDictionary<string, TMP_FontAsset> result)
    {
        string destination = TypographyPath + "/" + name + " SDF.asset";
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(destination);
        if (existing != null)
        {
            result[name] = existing;
            return;
        }

        Font source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
        if (source == null) throw new InvalidOperationException("Fonte física ausente: " + sourcePath);

        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
            source, 72, 8, GlyphRenderMode.SDFAA, 2048, 2048,
            AtlasPopulationMode.Dynamic, true);
        asset.name = name + " SDF";
        asset.TryAddCharacters(PortugueseCharacters, out string missing);
        asset.atlasPopulationMode = AtlasPopulationMode.Static;
        AssetDatabase.CreateAsset(asset, destination);

        foreach (Texture2D atlas in asset.atlasTextures)
        {
            if (atlas == null || AssetDatabase.Contains(atlas)) continue;
            atlas.name = name + " Atlas";
            AssetDatabase.AddObjectToAsset(atlas, asset);
        }

        if (asset.material != null && !AssetDatabase.Contains(asset.material))
        {
            asset.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
        }

        EditorUtility.SetDirty(asset);
        if (!string.IsNullOrEmpty(missing))
            Debug.LogWarning("[WD-VISUALKIT] Glifos ausentes em " + name + ": " + missing);
        result[name] = asset;
    }

    // ============================================================
    // 04. AUTORIDADE CENTRAL DO TEMA
    // ============================================================

    private static WarDominionUITheme ConfigureTheme(
        IReadOnlyDictionary<string, TMP_FontAsset> fonts)
    {
        WarDominionUITheme theme = AssetDatabase.LoadAssetAtPath<WarDominionUITheme>(ThemePath);
        if (theme == null) throw new InvalidOperationException("Tema ausente: " + ThemePath);

        SerializedObject serialized = new SerializedObject(theme);
        Assign(serialized, "masterGabaritoBold", fonts["Gabarito-Bold"]);
        Assign(serialized, "masterGabaritoExtraBold", fonts["Gabarito-ExtraBold"]);
        Assign(serialized, "masterGabaritoBlack", fonts["Gabarito-Black"]);
        Assign(serialized, "masterInterRegular", fonts["Inter-Regular"]);
        Assign(serialized, "masterInterMedium", fonts["Inter-Medium"]);
        Assign(serialized, "masterInterSemiBold", fonts["Inter-SemiBold"]);

        AssignSprite(serialized, "masterChevronLeftIcon", Root + "/Icons/Shared/Runtime/Chevron_Left.png");
        AssignSprite(serialized, "masterCoinIcon", Root + "/Icons/Shared/Runtime/Coin.png");
        AssignSprite(serialized, "masterGemIcon", Root + "/Icons/Shared/Runtime/Gem.png");
        AssignSprite(serialized, "masterBellIcon", Root + "/Icons/Shared/Runtime/Bell.png");
        AssignSprite(serialized, "masterRoundedFill", Root + "/Sprites/RoundedFill_R12.png");
        AssignSprite(serialized, "masterRoundedOutline", Root + "/Sprites/RoundedOutline_R12.png");
        AssignSprite(serialized, "masterArmoryEditIcon", Root + "/Icons/Arsenal/Runtime/Edit.png");
        AssignSprite(serialized, "masterArmoryLockIcon", Root + "/Icons/Arsenal/Runtime/Lock.png");
        AssignSprite(serialized, "masterArmorySaveIcon", Root + "/Icons/Arsenal/Runtime/Save.png");
        AssignSprite(serialized, "masterArmoryRemoveIcon", Root + "/Icons/Arsenal/Runtime/Remove.png");
        AssignSprite(serialized, "masterArmoryAddIcon", Root + "/Icons/Arsenal/Runtime/Add.png");
        AssignSprite(serialized, "masterArmorySearchIcon", Root + "/Icons/Arsenal/Runtime/Search.png");
        AssignSprite(serialized, "masterArmoryUpgradeIcon", Root + "/Icons/Arsenal/Runtime/Upgrade.png");
        AssignSprite(serialized, "masterArmoryEnergyIcon", Root + "/Icons/Arsenal/Runtime/Energy.png");
        AssignSprite(serialized, "masterArmoryArrowRightIcon", Root + "/Icons/Arsenal/Runtime/Arrow_Right.png");
        AssignSprite(serialized, "masterArmoryBackground", Root + "/Arsenal/Backgrounds/Arsenal_Ambiente_1920x1080.png");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(theme);
        return theme;
    }

    private static void Assign(SerializedObject target, string property, UnityEngine.Object value)
    {
        SerializedProperty field = target.FindProperty(property);
        if (field == null) throw new InvalidOperationException("Campo do tema ausente: " + property);
        field.objectReferenceValue = value;
    }

    private static void AssignSprite(SerializedObject target, string property, string path) =>
        Assign(target, property, AssetDatabase.LoadAssetAtPath<Sprite>(path));

    // ============================================================
    // 05. COMPONENTES UGUI SERIALIZADOS
    // ============================================================

    private static void BuildComponentPrefabs(WarDominionUITheme theme)
    {
        SavePrefab(BuildPanel(theme), "WDVisualKitPanel");
        SavePrefab(BuildButton(theme), "WDVisualKitButton");
        SavePrefab(BuildCard(theme), "WDVisualKitCard");
        SavePrefab(BuildField(theme), "WDVisualKitField");
        SavePrefab(BuildFilter(theme), "WDVisualKitFilter");
        SavePrefab(BuildBackNavigation(theme), "WDVisualKitBackNavigation");
        SavePrefab(BuildEconomy(theme), "WDVisualKitEconomyIndicator");
    }

    private static void SavePrefab(GameObject instance, string name)
    {
        PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath + "/" + name + ".prefab");
        UnityEngine.Object.DestroyImmediate(instance);
    }

    private static GameObject BuildPanel(WarDominionUITheme theme)
    {
        GameObject root = Ui("Panel", new Vector2(520f, 260f));
        AddSurface(root, theme.MasterRoundedFill, theme.MasterSurfacePrimary);
        AddOutline(root.transform, theme, theme.MasterGoldSoft);
        AddText(root.transform, "Título do painel", theme.MasterGabaritoExtraBold, 24f,
            theme.MasterTextPrimary, new Vector2(440f, 42f), new Vector2(0f, 76f));
        AddText(root.transform, "Informação funcional em Inter", theme.MasterInterRegular, 16f,
            theme.MasterTextSecondary, new Vector2(440f, 34f), new Vector2(0f, 30f));
        return root;
    }

    private static GameObject BuildButton(WarDominionUITheme theme)
    {
        GameObject root = Ui("Button", new Vector2(220f, 48f));
        Image surface = AddSurface(root, theme.MasterRoundedFill, theme.MasterGold);
        Button button = root.AddComponent<Button>();
        button.targetGraphic = surface;
        AddText(root.transform, "AÇÃO PRINCIPAL", theme.MasterGabaritoBold, 15f,
            theme.MasterBackground, new Vector2(190f, 30f), Vector2.zero);
        return root;
    }

    private static GameObject BuildCard(WarDominionUITheme theme)
    {
        GameObject root = Ui("Card", new Vector2(220f, 280f));
        AddSurface(root, theme.MasterRoundedFill, theme.MasterSurfaceElevated);
        AddOutline(root.transform, theme, theme.MasterBorderNeutral);
        GameObject preview = Ui("Artwork", new Vector2(188f, 150f), root.transform);
        Rect(preview).anchoredPosition = new Vector2(0f, 42f);
        AddSurface(preview, theme.MasterRoundedFill, theme.MasterSurfaceNavigation);
        AddText(root.transform, "CARD TÁTICO", theme.MasterGabaritoBold, 18f,
            theme.MasterTextPrimary, new Vector2(188f, 30f), new Vector2(0f, -62f));
        AddText(root.transform, "Estado · informação", theme.MasterInterMedium, 13f,
            theme.MasterCyan, new Vector2(188f, 28f), new Vector2(0f, -96f));
        return root;
    }

    private static GameObject BuildField(WarDominionUITheme theme)
    {
        GameObject root = Ui("Field", new Vector2(330f, 48f));
        AddSurface(root, theme.MasterRoundedFill, theme.MasterSurfaceNavigation);
        AddOutline(root.transform, theme, theme.MasterBorderNeutral);
        AddIcon(root.transform, "Search", theme.MasterArmorySearchIcon, 20f, new Vector2(-142f, 0f));
        AddText(root.transform, "Buscar...", theme.MasterInterRegular, 15f,
            theme.MasterTextMuted, new Vector2(260f, 30f), new Vector2(18f, 0f), TextAlignmentOptions.Left);
        return root;
    }

    private static GameObject BuildFilter(WarDominionUITheme theme)
    {
        GameObject root = Ui("Filter", new Vector2(120f, 40f));
        AddSurface(root, theme.MasterRoundedFill, theme.MasterSurfaceNavigation);
        AddOutline(root.transform, theme, theme.MasterCyan);
        AddText(root.transform, "SELECIONADO", theme.MasterGabaritoBold, 13f,
            theme.MasterCyan, new Vector2(104f, 28f), Vector2.zero);
        return root;
    }

    private static GameObject BuildBackNavigation(WarDominionUITheme theme)
    {
        GameObject root = Ui("BackNavigation", new Vector2(132f, 40f));
        AddSurface(root, theme.MasterRoundedFill, theme.MasterSurfaceNavigation);
        AddOutline(root.transform, theme, theme.MasterBorderNeutral);
        AddIcon(root.transform, "Chevron", theme.MasterChevronLeftIcon, 16f, new Vector2(-46f, 0f));
        AddText(root.transform, "VOLTAR", theme.MasterGabaritoBold, 14f,
            theme.MasterGold, new Vector2(82f, 28f), new Vector2(16f, 0f));
        return root;
    }

    private static GameObject BuildEconomy(WarDominionUITheme theme)
    {
        GameObject root = Ui("EconomyIndicator", new Vector2(126f, 40f));
        AddSurface(root, theme.MasterRoundedFill, theme.MasterSurfaceNavigation);
        AddOutline(root.transform, theme, theme.MasterBorderNeutral);
        AddIcon(root.transform, "Coin", theme.MasterCoinIcon, 18f, new Vector2(-40f, 0f));
        AddText(root.transform, "15.420", theme.MasterInterSemiBold, 15f,
            theme.MasterTextPrimary, new Vector2(72f, 28f), new Vector2(20f, 0f));
        return root;
    }

    // ============================================================
    // 06. GALERIA ISOLADA DE VALIDAÇÃO
    // ============================================================

    private static void BuildValidationScene(WarDominionUITheme theme)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "WDVisualKitGallery";
        GameObject canvasObject = new GameObject("VisualKitGalleryCanvas", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        GameObject cameraObject = new GameObject("ValidationCamera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = theme.MasterBackground;
        camera.orthographic = true;
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        GameObject background = Ui("Background", new Vector2(1920f, 1080f), canvasObject.transform);
        RectTransform backgroundRect = Rect(background);
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        AddSurface(background, null, theme.MasterBackground);

        AddText(background.transform, "VISUALKIT · PREMIUM TACTICAL", theme.MasterGabaritoBlack,
            34f, theme.MasterTextPrimary, new Vector2(900f, 54f), new Vector2(0f, 450f));
        AddText(background.transform, "Gabarito para títulos e ações · Inter para informação funcional",
            theme.MasterInterRegular, 17f, theme.MasterTextSecondary,
            new Vector2(900f, 36f), new Vector2(0f, 405f));

        AddPrefabInstance("WDVisualKitPanel", background.transform, new Vector2(-520f, 190f));
        AddPrefabInstance("WDVisualKitCard", background.transform, new Vector2(0f, 180f));
        AddPrefabInstance("WDVisualKitButton", background.transform, new Vector2(500f, 270f));
        AddPrefabInstance("WDVisualKitField", background.transform, new Vector2(500f, 190f));
        AddPrefabInstance("WDVisualKitFilter", background.transform, new Vector2(500f, 120f));
        AddPrefabInstance("WDVisualKitBackNavigation", background.transform, new Vector2(-580f, -80f));
        AddPrefabInstance("WDVisualKitEconomyIndicator", background.transform, new Vector2(-360f, -80f));

        Sprite[] icons = {
            theme.MasterBellIcon, theme.MasterChevronLeftIcon, theme.MasterCoinIcon, theme.MasterGemIcon,
            theme.MasterArmoryEditIcon, theme.MasterArmoryLockIcon, theme.MasterArmorySaveIcon,
            theme.MasterArmoryRemoveIcon, theme.MasterArmoryAddIcon, theme.MasterArmorySearchIcon,
            theme.MasterArmoryUpgradeIcon, theme.MasterArmoryEnergyIcon, theme.MasterArmoryArrowRightIcon
        };
        for (int index = 0; index < icons.Length; index++)
        {
            float x = -600f + (index % 7) * 200f;
            float y = -220f - (index / 7) * 110f;
            AddIcon(background.transform, "OfficialIcon_" + index, icons[index], 56f, new Vector2(x, y));
        }

        EditorSceneManager.SaveScene(scene, ValidationPath + "/WDVisualKitGallery.unity");
        RenderValidationPreview(camera);
    }

    private static void RenderValidationPreview(Camera camera)
    {
        const int width = 1920;
        const int height = 1080;
        RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        Texture2D capture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            Canvas.ForceUpdateCanvases();
            camera.targetTexture = target;
            RenderTexture.active = target;
            camera.Render();
            capture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            capture.Apply();
            File.WriteAllBytes(ValidationPath + "/WDVisualKitGalleryPreview.png", capture.EncodeToPNG());
            AssetDatabase.ImportAsset(ValidationPath + "/WDVisualKitGalleryPreview.png",
                ImportAssetOptions.ForceSynchronousImport);
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(capture);
        }
    }

    private static void AddPrefabInstance(string prefabName, Transform parent, Vector2 position)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            PrefabPath + "/" + prefabName + ".prefab");
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        Rect(instance).anchoredPosition = position;
    }

    // ============================================================
    // 07. PRIMITIVAS DE CONSTRUÇÃO EDITORIAL
    // ============================================================

    private static GameObject Ui(string name, Vector2 size, Transform parent = null)
    {
        GameObject value = new GameObject(name, typeof(RectTransform));
        if (parent != null) value.transform.SetParent(parent, false);
        Rect(value).sizeDelta = size;
        return value;
    }

    private static RectTransform Rect(GameObject value) => value.GetComponent<RectTransform>();

    private static Image AddSurface(GameObject target, Sprite sprite, Color color)
    {
        Image image = target.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null && sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void AddOutline(Transform parent, WarDominionUITheme theme, Color color)
    {
        GameObject outline = Ui("Outline", Vector2.zero, parent);
        RectTransform rect = Rect(outline);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = AddSurface(outline, theme.MasterRoundedOutline, color);
        image.type = Image.Type.Sliced;
    }

    private static void AddText(Transform parent, string content, TMP_FontAsset font,
        float size, Color color, Vector2 dimensions, Vector2 position,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        GameObject textObject = Ui("Text", dimensions, parent);
        Rect(textObject).anchoredPosition = position;
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
    }

    private static void AddIcon(Transform parent, string name, Sprite sprite,
        float size, Vector2 position)
    {
        GameObject iconObject = Ui(name, new Vector2(size, size), parent);
        Rect(iconObject).anchoredPosition = position;
        Image icon = iconObject.AddComponent<Image>();
        icon.sprite = sprite;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
    }

    // ============================================================
    // 08. INTEGRIDADE DOS ASSETS GERADOS
    // ============================================================

    private static void ValidateGeneratedAssets(WarDominionUITheme theme)
    {
        UnityEngine.Object[] required = {
            theme.MasterGabaritoBold, theme.MasterGabaritoExtraBold, theme.MasterGabaritoBlack,
            theme.MasterInterRegular, theme.MasterInterMedium, theme.MasterInterSemiBold,
            theme.MasterChevronLeftIcon, theme.MasterCoinIcon, theme.MasterGemIcon,
            theme.MasterBellIcon, theme.MasterRoundedFill, theme.MasterRoundedOutline,
            theme.MasterArmoryEditIcon, theme.MasterArmoryLockIcon, theme.MasterArmorySaveIcon,
            theme.MasterArmoryRemoveIcon, theme.MasterArmoryAddIcon, theme.MasterArmorySearchIcon,
            theme.MasterArmoryUpgradeIcon, theme.MasterArmoryEnergyIcon,
            theme.MasterArmoryArrowRightIcon, theme.MasterArmoryBackground
        };
        foreach (UnityEngine.Object item in required)
            if (item == null) throw new InvalidOperationException("Referência obrigatória ausente no tema.");

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabPath }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.GetComponents<Graphic>().Length > 1)
                        throw new InvalidOperationException("Graphics concorrentes em " + path + ": " + child.name);
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) > 0)
                        throw new InvalidOperationException("Missing Script em " + path + ": " + child.name);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
#endif
