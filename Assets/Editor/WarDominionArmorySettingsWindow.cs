using UnityEditor;
using UnityEngine;

public sealed class WarDominionArmorySettingsWindow : EditorWindow
{
    // ============================================================
    // 01. FONTE ÚNICA E ROTA ADMINISTRATIVA
    // ============================================================

    private const string MenuPath =
        "War Dominion/Configurações do Jogo/Armas e Progressão";
    private const string AssetPath =
        "Assets/Resources/UI/Home/WDArmoryCatalogConfig.asset";

    private WDArmoryCatalogConfig catalog;
    private UnityEditor.Editor catalogEditor;
    private Vector2 scrollPosition;

    [MenuItem(MenuPath, priority = 11)]
    private static void OpenWindow()
    {
        WarDominionArmorySettingsWindow window =
            GetWindow<WarDominionArmorySettingsWindow>("Armas e Progressão");
        window.minSize = new Vector2(520f, 520f);
        window.Show();
    }

    private void OnEnable() => LoadCanonicalCatalog();

    private void OnDisable()
    {
        if (catalogEditor != null)
            DestroyImmediate(catalogEditor);
    }

    // ============================================================
    // 02. INSPECTOR EMBUTIDO DO CATÁLOGO CANÔNICO
    // ============================================================

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "ARMAS E PROGRESSÃO", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Esta janela edita diretamente o WDArmoryCatalogConfig usado pela seção ARMAS. " +
            "Categorias, armas, progressão e parâmetros de desenvolvimento permanecem no " +
            "mesmo asset e não são duplicados.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.ObjectField(
                "Catálogo ativo", catalog,
                typeof(WDArmoryCatalogConfig), false);
            if (GUILayout.Button("Localizar", GUILayout.Width(90f)))
                PingCatalog();
            if (GUILayout.Button("Recarregar", GUILayout.Width(90f)))
                LoadCanonicalCatalog();
        }

        if (catalog == null)
        {
            EditorGUILayout.HelpBox(
                $"Catálogo não encontrado em {AssetPath}.", MessageType.Error);
            return;
        }

        EditorGUILayout.Space(6f);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        catalogEditor?.OnInspectorGUI();
        EditorGUILayout.EndScrollView();
    }

    // ============================================================
    // 03. LOCALIZAÇÃO SEGURA SEM CRIAR OUTRO ASSET
    // ============================================================

    private void LoadCanonicalCatalog()
    {
        WDArmoryCatalogConfig loaded =
            AssetDatabase.LoadAssetAtPath<WDArmoryCatalogConfig>(AssetPath);
        if (loaded == catalog && catalogEditor != null)
            return;

        if (catalogEditor != null)
            DestroyImmediate(catalogEditor);
        catalog = loaded;
        catalogEditor = catalog != null
            ? UnityEditor.Editor.CreateEditor(catalog)
            : null;
        Repaint();
    }

    private void PingCatalog()
    {
        if (catalog == null)
            LoadCanonicalCatalog();
        Selection.activeObject = catalog;
        if (catalog != null)
            EditorGUIUtility.PingObject(catalog);
    }
}
