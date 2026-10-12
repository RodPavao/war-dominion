#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class WDVisualEditorWindow : EditorWindow
{
    private const string SettingsPath = "Assets/Resources/UI/ArmoryV2/WDArmoryV2VisualSettings.asset";
    private const string CatalogPath = "Assets/Resources/UI/Home/WDArmoryCatalogConfig.asset";
    private const string VisualCatalogPath = "Assets/Resources/UI/Armory/WDArmoryVisualCatalog.asset";
    private WDArmoryV2VisualSettings settings;
    private SerializedObject serialized;
    private Vector2 scroll;

    // ============================================================
    // 01. ENTRADA PERMANENTE DO EDITOR VISUAL
    // ============================================================
    [MenuItem("War Dominion/UI/Visual Editor", priority = 100)]
    public static void Open() => GetWindow<WDVisualEditorWindow>("WD Visual Editor");

    public static void CreateSettingsAsset() => EnsureSettingsAsset();

    private void OnEnable()
    {
        settings = EnsureSettingsAsset();
        serialized = settings != null ? new SerializedObject(settings) : null;
    }

    private void OnGUI()
    {
        if (settings == null || serialized == null) { EditorGUILayout.HelpBox("Configuração visual indisponível.", MessageType.Error); return; }
        serialized.Update();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("WAR DOMINION · ARSENAL V2", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Valores exibidos aqui são a autoridade aplicada ao abrir e atualizar o Arsenal V2.", MessageType.Info);

        DrawSection("Painéis e botões", "panelColor", "buttonColor", "buttonPrimaryColor");
        DrawSection("Slots principais e APOIO", "mainSlotColor", "supportSlotColor", "slotBorderColor", "selectedColor");
        DrawSection("Cards e estados", "catalogCardColor", "catalogBorderColor", "equippedBorderColor", "disabledColor");
        DrawSection("Tipografia", "titleFont", "functionalFont", "screenTitleSize", "detailTitleSize", "slotNameSize",
            "detailTitleWordWrap", "detailTitleOverflow", "detailTitleAutoSizing");
        DrawSection("Dimensões e espaçamentos", "mainSlotSize", "mainSlotSpacing", "supportSlotSize", "supportSlotSpacing",
            "catalogCardSize", "catalogSpacing");
        DrawArtworkSection();
        EditorGUILayout.EndScrollView();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Salvar no asset", GUILayout.Height(28f))) Save();
        if (GUILayout.Button("Aplicar na visualização aberta", GUILayout.Height(28f))) ApplyToOpenViews();
        if (GUILayout.Button("Restaurar V2 oficial", GUILayout.Height(28f))) RestoreOfficial();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField("Asset", SettingsPath, EditorStyles.miniLabel);
    }

    // ============================================================
    // 02. SEÇÕES ORGANIZADAS POR RESPONSABILIDADE VISUAL
    // ============================================================
    private void DrawSection(string title, params string[] properties)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        foreach (string property in properties) EditorGUILayout.PropertyField(serialized.FindProperty(property), true);
        EditorGUILayout.EndVertical();
    }

    private void DrawArtworkSection()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Artes canônicas · 26 armas", EditorStyles.boldLabel);
        WDArmoryVisualCatalog visualCatalog = AssetDatabase.LoadAssetAtPath<WDArmoryVisualCatalog>(VisualCatalogPath);
        SerializedProperty entries = serialized.FindProperty("artworkOverrides");
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            string id = entry.FindPropertyRelative("weaponId").stringValue;
            string displayName = entry.FindPropertyRelative("displayName").stringValue;
            SerializedProperty sprite = entry.FindPropertyRelative("sprite");
            Texture fallback = visualCatalog?.Resolve(id);
            Object effective = sprite.objectReferenceValue != null ? sprite.objectReferenceValue : fallback;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label(effective != null ? AssetPreview.GetAssetPreview(effective) ?? AssetPreview.GetMiniThumbnail(effective) : null,
                GUILayout.Width(64f), GUILayout.Height(64f));
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(displayName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("ID: " + id, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Efetivo: " + (effective != null ? AssetDatabase.GetAssetPath(effective) : "AUSENTE"), EditorStyles.miniLabel);
            EditorGUILayout.PropertyField(sprite, new GUIContent("Override Sprite"));
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }
    }

    // ============================================================
    // 03. PERSISTÊNCIA EXPLÍCITA E PREVIEW CONTROLADO
    // ============================================================
    private void Save()
    {
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        ApplyToOpenViews();
    }

    private void RestoreOfficial()
    {
        Undo.RecordObject(settings, "Restaurar visual oficial do Arsenal V2");
        settings.RestoreOfficialValues();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        serialized.Update();
        ApplyToOpenViews();
    }

    private void ApplyToOpenViews()
    {
        serialized.ApplyModifiedProperties();
        foreach (WDArmoryScreenV2View view in Resources.FindObjectsOfTypeAll<WDArmoryScreenV2View>()
            .Where(view => !EditorUtility.IsPersistent(view))) view.ApplyVisualSettings(settings);
        SceneView.RepaintAll(); Repaint();
    }

    public static WDArmoryV2VisualSettings EnsureSettingsAsset()
    {
        WDArmoryV2VisualSettings asset = AssetDatabase.LoadAssetAtPath<WDArmoryV2VisualSettings>(SettingsPath);
        if (asset != null) return asset;
        asset = CreateInstance<WDArmoryV2VisualSettings>();
        WarDominionUITheme theme = AssetDatabase.LoadAssetAtPath<WarDominionUITheme>("Assets/Resources/UI/WarDominionUITheme.asset");
        if (theme != null) { asset.titleFont = theme.MasterGabaritoBold; asset.functionalFont = theme.MasterInterRegular; }
        asset.SynchronizeArtworkEntries(AssetDatabase.LoadAssetAtPath<WDArmoryCatalogConfig>(CatalogPath));
        AssetDatabase.CreateAsset(asset, SettingsPath);
        AssetDatabase.SaveAssets();
        return asset;
    }
}
#endif
