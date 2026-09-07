using UnityEditor;
using UnityEngine;

public sealed class WarDominionGameSettingsWindow : EditorWindow
{
    // ============================================================
    // 01. FONTE ÚNICA E ROTA ADMINISTRATIVA
    // ============================================================

    private const string MenuPath =
        "War Dominion/Configurações do Jogo/Modos e Disponibilidade";
    private const string AssetPath =
        "Assets/Resources/UI/Home/WarDominionMatchFlowConfig.asset";

    private WarDominionMatchFlowConfig config;
    private UnityEditor.Editor configEditor;
    private Vector2 scrollPosition;

    [MenuItem(MenuPath, priority = 10)]
    private static void OpenWindow()
    {
        WarDominionGameSettingsWindow window =
            GetWindow<WarDominionGameSettingsWindow>("Modos e Disponibilidade");
        window.minSize = new Vector2(520f, 520f);
        window.Show();
    }

    private void OnEnable() => LoadCanonicalConfig();

    private void OnDisable()
    {
        if (configEditor != null)
            DestroyImmediate(configEditor);
    }

    // ============================================================
    // 02. INSPECTOR EMBUTIDO DO ASSET CANÔNICO
    // ============================================================

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "MODOS E DISPONIBILIDADE", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Esta janela edita diretamente o WarDominionMatchFlowConfig usado pela Home. " +
            "Modes, Maps e Map Compatibility permanecem no mesmo asset e não são duplicados.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.ObjectField(
                "Configuração ativa", config,
                typeof(WarDominionMatchFlowConfig), false);
            if (GUILayout.Button("Localizar", GUILayout.Width(90f)))
                PingConfig();
            if (GUILayout.Button("Recarregar", GUILayout.Width(90f)))
                LoadCanonicalConfig();
        }

        if (config == null)
        {
            EditorGUILayout.HelpBox(
                $"Configuração não encontrada em {AssetPath}.", MessageType.Error);
            return;
        }

        EditorGUILayout.Space(6f);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        configEditor?.OnInspectorGUI();
        EditorGUILayout.EndScrollView();
    }

    // ============================================================
    // 03. LOCALIZAÇÃO SEGURA SEM CRIAR OUTRO ASSET
    // ============================================================

    private void LoadCanonicalConfig()
    {
        WarDominionMatchFlowConfig loaded =
            AssetDatabase.LoadAssetAtPath<WarDominionMatchFlowConfig>(AssetPath);
        if (loaded == config && configEditor != null)
            return;

        if (configEditor != null)
            DestroyImmediate(configEditor);
        config = loaded;
        configEditor = config != null
            ? UnityEditor.Editor.CreateEditor(config)
            : null;
        Repaint();
    }

    private void PingConfig()
    {
        if (config == null)
            LoadCanonicalConfig();
        Selection.activeObject = config;
        if (config != null)
            EditorGUIUtility.PingObject(config);
    }
}
