using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class WarDominionMapEditorWindow : EditorWindow
{
    private enum Tab { Mapa, Arte, Territorios, Regioes, Conexoes, Contadores, Thumbnail, Validacao }
    private const string CatalogPath = "Assets/Resources/Mapas/CatalogoMapas.asset";
    private CatalogoMapas catalog;
    private DefinicaoMapa map;
    private SerializedObject serializedMap;
    private Tab tab;
    private Vector2 sideScroll;
    private Vector2 previewPan;
    private float previewZoom = 1f;
    private string search = string.Empty;
    private int selectedTerritory;
    private int selectedRegion;
    private int connectionDestination;
    private TipoConexaoMapa connectionType;
    private bool showArt = true, showGeometry = true, showRegions = true,
        showConnections = true, showCounters = true, showOverlay;
    private bool draggingCounter;
    private WDMapSpatialSnapshot spatialSnapshot = new WDMapSpatialSnapshot();
    private readonly WDMapEditorSelection territorySelection = new WDMapEditorSelection();

    // ============================================================
    // 01. CICLO DE VIDA, CATÁLOGO E MENU PRINCIPAL
    // ============================================================

    [MenuItem("War Dominion/Editor de Mapas/Abrir Editor", priority = 1)]
    public static void Open()
    {
        WarDominionMapEditorWindow window = GetWindow<WarDominionMapEditorWindow>();
        window.titleContent = new GUIContent("War Dominion Map Editor");
        window.minSize = new Vector2(980f, 620f);
        window.Show();
    }

    private void OnEnable()
    {
        catalog = AssetDatabase.LoadAssetAtPath<CatalogoMapas>(CatalogPath);
        Undo.undoRedoPerformed += OnUndoRedo;
        if (map == null) SelectMap(catalog != null ? catalog.MapaPadrao : null);
    }

    private void OnDisable() => Undo.undoRedoPerformed -= OnUndoRedo;
    private void OnUndoRedo() { serializedMap?.Update(); Repaint(); }

    private void OnGUI()
    {
        DrawToolbar();
        if (map == null)
        {
            EditorGUILayout.HelpBox("Selecione um mapa do catálogo ou crie um novo.", MessageType.Info);
            return;
        }
        serializedMap.Update();
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawSidePanel();
            DrawPreview();
        }
        if (serializedMap.ApplyModifiedProperties()) EditorUtility.SetDirty(map);
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            DefinicaoMapa next = (DefinicaoMapa)EditorGUILayout.ObjectField(map,
                typeof(DefinicaoMapa), false, GUILayout.Width(260f));
            if (next != map && ConfirmSwitch()) SelectMap(next);
            if (GUILayout.Button("Novo Mapa", EditorStyles.toolbarButton)) NewMap();
            if (GUILayout.Button("Salvar", EditorStyles.toolbarButton))
                WarDominionMapEditorOperations.Save(map);
            if (GUILayout.Button("Desfazer", EditorStyles.toolbarButton)) Undo.PerformUndo();
            if (GUILayout.Button("Refazer", EditorStyles.toolbarButton)) Undo.PerformRedo();
            GUILayout.FlexibleSpace();
            GUILayout.Label(EditorUtility.IsDirty(map) ? "Alterações não salvas *" : "Salvo");
        }
        string[] names = Enum.GetNames(typeof(Tab));
        tab = (Tab)GUILayout.Toolbar((int)tab, names);
    }

    private void SelectMap(DefinicaoMapa selected)
    {
        map = selected;
        serializedMap = map != null ? new SerializedObject(map) : null;
        selectedTerritory = 0;
        territorySelection.Clear();
        spatialSnapshot = WDMapSpatialSnapshot.Capture(map);
        previewPan = Vector2.zero;
        previewZoom = 1f;
        Repaint();
    }

    private bool ConfirmSwitch() => map == null || !EditorUtility.IsDirty(map) ||
        EditorUtility.DisplayDialog("Alterações não salvas",
            "O mapa atual possui alterações não salvas. Trocar de mapa sem salvar?",
            "Descartar e trocar", "Cancelar");

    // ============================================================
    // 02. MÓDULOS LATERAIS DATA-DRIVEN
    // ============================================================

    private void DrawSidePanel()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(390f)))
        {
            sideScroll = EditorGUILayout.BeginScrollView(sideScroll);
            switch (tab)
            {
                case Tab.Mapa: DrawMap(); break;
                case Tab.Arte: DrawArt(); break;
                case Tab.Territorios: DrawTerritories(); break;
                case Tab.Regioes: DrawRegions(); break;
                case Tab.Conexoes: DrawConnections(); break;
                case Tab.Contadores: DrawCounters(); break;
                case Tab.Thumbnail: DrawThumbnail(); break;
                case Tab.Validacao: DrawValidation(); break;
            }
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawMap()
    {
        Header("Identidade e enquadramento real");
        Property("mapaId"); Property("nomeInterno"); Property("nomeExibido");
        Property("descricao"); Property("temaId"); Property("versao");
        EditorGUILayout.Space();
        Property("centroCamera"); Property("tamanhoOrtografico");
        Property("deslocamentoEnquadramento");
        EditorGUILayout.HelpBox("O zoom do viewport não altera estes valores de jogo.", MessageType.Info);
    }

    private void DrawArt()
    {
        Header("Arte Principal");
        SerializedProperty art = serializedMap.FindProperty("arteBase");
        Sprite previous = art.objectReferenceValue as Sprite;
        Sprite next = (Sprite)EditorGUILayout.ObjectField("Arte Principal", previous,
            typeof(Sprite), false);
        if (next != previous && ConfirmArtChange(previous, next))
            WarDominionMapEditorOperations.ReplaceMainArt(map, next);
        if (next != null)
        {
            EditorGUILayout.LabelField("Resolução", $"{next.texture.width} × {next.texture.height}");
            EditorGUILayout.LabelField("Proporção", (next.rect.width / next.rect.height).ToString("0.000"));
            EditorGUILayout.LabelField("Pixels por unidade", next.pixelsPerUnit.ToString("0.##"));
        }
        EditorGUILayout.HelpBox("Ao trocar arte com fronteiras diferentes, revise geometria, contadores e validação. Nenhum dado é regenerado silenciosamente.", MessageType.Warning);
    }

    private void DrawTerritories()
    {
        Header($"Territórios ({map.Territorios.Count})");
        search = EditorGUILayout.TextField("Buscar território", search);
        string[] labels = map.Territorios.Select(item => item?.nomeExibido + "  [" + item?.id + "]").ToArray();
        IEnumerable<int> matches = Enumerable.Range(0, labels.Length).Where(index =>
            string.IsNullOrWhiteSpace(search) || labels[index].IndexOf(search,
                StringComparison.OrdinalIgnoreCase) >= 0);
        foreach (int index in matches)
            if (GUILayout.Toggle(selectedTerritory == index, labels[index], "Button"))
                selectedTerritory = index;
        if (map.Territorios.Count > 0)
            EditorGUILayout.PropertyField(serializedMap.FindProperty("territorios")
                .GetArrayElementAtIndex(Mathf.Clamp(selectedTerritory, 0, map.Territorios.Count - 1)), true);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Criar território")) AddTerritory();
            using (new EditorGUI.DisabledScope(map.Territorios.Count == 0))
                if (GUILayout.Button("Remover território")) RemoveTerritory();
        }
        if (GUILayout.Button("Gerar Geometria Automaticamente (pipeline assistido)"))
            EditorUtility.DisplayDialog("Geometria assistida",
                "A V1 preserva os geradores 1:1 existentes. Use Ferramentas de Desenvolvimento > Mapas > Gerar ou Atualizar para o mapa e revise o resultado antes de salvar.", "OK");
    }

    private void DrawRegions()
    {
        Header($"Regiões ({map.Regioes.Count})");
        if (map.Regioes.Count > 0)
        {
            string[] labels = map.Regioes.Select(region => region.nomeExibido + " [" + region.id + "]").ToArray();
            selectedRegion = EditorGUILayout.Popup("Região", Mathf.Clamp(selectedRegion, 0,
                map.Regioes.Count - 1), labels);
            EditorGUILayout.PropertyField(serializedMap.FindProperty("regioes")
                .GetArrayElementAtIndex(selectedRegion), true);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Criar região")) AddRegion();
            using (new EditorGUI.DisabledScope(map.Regioes.Count == 0))
                if (GUILayout.Button("Remover região")) RemoveRegion();
        }
        EditorGUILayout.HelpBox("A associação permanece autoritativa em territorio.regiaoId e região.territorioIds.", MessageType.Info);
    }

    private void DrawConnections()
    {
        Header($"Conexões ({map.Conexoes.Count})");
        if (map.Territorios.Count < 2) return;
        selectedTerritory = Mathf.Clamp(selectedTerritory, 0, map.Territorios.Count - 1);
        string[] ids = map.Territorios.Select(item => item.id).ToArray();
        int[] pair = territorySelection.Indices.ToArray();
        bool hasVisualPair = pair.Length == 2;
        int originIndex = hasVisualPair ? pair[0] : selectedTerritory;
        int destinationIndex = hasVisualPair ? pair[1] : connectionDestination;
        EditorGUILayout.LabelField("Origem", ids[originIndex]);
        connectionDestination = EditorGUILayout.Popup("Destino", connectionDestination, ids);
        connectionType = (TipoConexaoMapa)EditorGUILayout.EnumPopup("Tipo", connectionType);
        if (pair.Length > 0)
            EditorGUILayout.HelpBox(pair.Length == 2
                ? $"Par visual: {ids[pair[0]]} ↔ {ids[pair[1]]}"
                : pair.Length == 1 ? $"Selecionado: {ids[pair[0]]}. Use Ctrl + clique para formar o par."
                : "Mais de dois territórios selecionados. Reduza a seleção para exatamente dois.",
                pair.Length > 2 ? MessageType.Warning : MessageType.Info);
        bool invalidMultipleSelection = pair.Length > 2;
        bool connectionExists = WarDominionMapEditorOperations.ContainsConnection(
            map, ids[originIndex], ids[destinationIndex]);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(invalidMultipleSelection || connectionExists || originIndex == destinationIndex))
            if (GUILayout.Button("Adicionar bidirecional"))
                WarDominionMapEditorOperations.AddBidirectionalConnection(map,
                    ids[originIndex], ids[destinationIndex], connectionType);
            using (new EditorGUI.DisabledScope(invalidMultipleSelection || !connectionExists))
            if (GUILayout.Button("Remover"))
                WarDominionMapEditorOperations.RemoveConnection(map,
                    ids[originIndex], ids[destinationIndex]);
        }
        if (GUILayout.Button("Abrir modo visual Criar Conexões"))
            EditorApplication.ExecuteMenuItem("War Dominion/Ferramentas de Desenvolvimento/Mapas/Editor de Vizinhanças");
        foreach (DefinicaoConexaoMapa connection in map.Conexoes)
            EditorGUILayout.LabelField($"{connection.origemId} ↔ {connection.destinoId} [{connection.tipo}]");
    }

    private void DrawCounters()
    {
        Header("Posição do contador");
        if (map.Territorios.Count == 0) return;
        selectedTerritory = Mathf.Clamp(selectedTerritory, 0, map.Territorios.Count - 1);
        SerializedProperty territory = serializedMap.FindProperty("territorios").GetArrayElementAtIndex(selectedTerritory);
        EditorGUILayout.LabelField(map.Territorios[selectedTerritory].nomeExibido);
        EditorGUILayout.PropertyField(territory.FindPropertyRelative("possuiPosicaoContador"));
        EditorGUILayout.PropertyField(territory.FindPropertyRelative("posicaoContador"));
        if (GUILayout.Button("Resetar posição do contador") &&
            EditorUtility.DisplayDialog("Resetar contador",
                "Definir a posição do contador como (0, 0)?", "Resetar", "Cancelar"))
        {
            Undo.RecordObject(map, "Resetar posição do contador");
            territory.FindPropertyRelative("posicaoContador").vector2Value = Vector2.zero;
        }
        EditorGUILayout.HelpBox("Arraste o marcador C no viewport para mover o contador. Undo é suportado.", MessageType.Info);
    }

    private void DrawThumbnail()
    {
        Header("Thumbnail automático — V1 assistida");
        EditorGUILayout.HelpBox("O preview usa a arte original sem modificá-la. Auto-enquadramento/crop persistente será concluído quando o campo de thumbnail fizer parte de DefinicaoMapa.", MessageType.Info);
        if (GUILayout.Button("Auto Enquadrar Preview")) { previewZoom = 1f; previewPan = Vector2.zero; }
        using (new EditorGUI.DisabledScope(map.ArteBase == null))
            if (GUILayout.Button("Gerar Thumbnail Automaticamente (16:9)"))
                GenerateThumbnail();
    }

    private void DrawValidation()
    {
        WDMapEditorValidationReport report = WarDominionMapEditorOperations.Validate(map,
            catalog != null ? catalog.Mapas : Array.Empty<DefinicaoMapa>());
        Header("Relatório de validação");
        EditorGUILayout.LabelField($"{report.TerritoryCount} territórios | {report.RegionCount} regiões | {report.ConnectionCount} conexões");
        EditorGUILayout.LabelField($"{report.ErrorCount} erros | {report.WarningCount} avisos");
        foreach (ProblemaValidacaoMapa issue in report.Issues)
            EditorGUILayout.HelpBox(issue.ToString(), issue.Severidade == SeveridadeProblemaMapa.Erro
                ? MessageType.Error : MessageType.Warning);
        bool registered = catalog != null && catalog.Mapas.Contains(map);
        using (new EditorGUI.DisabledScope(report.ErrorCount > 0 || registered))
            if (GUILayout.Button(registered ? "MAPA JÁ REGISTRADO" : "ADICIONAR AO CATÁLOGO"))
                WarDominionMapEditorOperations.RegisterInCatalog(catalog, map);
    }

    // ============================================================
    // 03. VIEWPORT: ARTE, CAMADAS, ZOOM, PAN E CONTADORES
    // ============================================================

    private void DrawPreview()
    {
        using (new EditorGUILayout.VerticalScope())
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                showArt = GUILayout.Toggle(showArt, "Arte", EditorStyles.toolbarButton);
                showGeometry = GUILayout.Toggle(showGeometry, "Geometria", EditorStyles.toolbarButton);
                showRegions = GUILayout.Toggle(showRegions, "Regiões", EditorStyles.toolbarButton);
                showConnections = GUILayout.Toggle(showConnections, "Conexões", EditorStyles.toolbarButton);
                showCounters = GUILayout.Toggle(showCounters, "Contadores", EditorStyles.toolbarButton);
                showOverlay = GUILayout.Toggle(showOverlay, "Overlay", EditorStyles.toolbarButton);
                if (GUILayout.Button("Ajustar", EditorStyles.toolbarButton)) { previewZoom = 1f; previewPan = Vector2.zero; }
            }
            Rect viewport = GUILayoutUtility.GetRect(400f, 4000f, 400f, 4000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(viewport, new Color(0.08f, 0.09f, 0.11f));
            HandlePreviewInput(viewport);
            DrawMapPreview(viewport);
        }
    }

    private void DrawMapPreview(Rect viewport)
    {
        Sprite art = map.ArteBase;
        if (art == null) return;
        float fit = Mathf.Min(viewport.width / art.rect.width, viewport.height / art.rect.height);
        Vector2 size = art.rect.size * fit * previewZoom;
        Rect artRect = new Rect(viewport.center - size * 0.5f + previewPan, size);
        if (showArt) GUI.DrawTextureWithTexCoords(artRect, art.texture,
            new Rect(art.rect.x / art.texture.width, art.rect.y / art.texture.height,
                art.rect.width / art.texture.width, art.rect.height / art.texture.height), true);
        if (showCounters || showConnections)
        {
            var projection = new WDMapViewportTransform(artRect, art.bounds);
            Func<int, Vector2> convert = index => projection.MapToViewport(
                spatialSnapshot.ToArtLocal(map.Territorios[index].id,
                    map.Territorios[index].posicaoContador));
            HandleTerritoryMarkers(convert, projection);
            if (showConnections)
            {
                Handles.BeginGUI(); Handles.color = new Color(0.2f, 0.85f, 1f, 0.65f);
                foreach (DefinicaoConexaoMapa connection in map.Conexoes)
                {
                    DefinicaoTerritorioMapa a = map.Territorios.FirstOrDefault(t => t.id == connection.origemId);
                    DefinicaoTerritorioMapa b = map.Territorios.FirstOrDefault(t => t.id == connection.destinoId);
                    int ai = map.Territorios.ToList().IndexOf(a);
                    int bi = map.Territorios.ToList().IndexOf(b);
                    if (ai >= 0 && bi >= 0) Handles.DrawAAPolyLine(2f, convert(ai), convert(bi));
                }
                int[] pair = territorySelection.Indices.ToArray();
                if (pair.Length == 2)
                {
                    bool exists = WarDominionMapEditorOperations.ContainsConnection(map,
                        map.Territorios[pair[0]].id, map.Territorios[pair[1]].id);
                    Handles.color = exists ? Color.green : new Color(1f, 0.65f, 0.1f, 0.85f);
                    Handles.DrawAAPolyLine(exists ? 4f : 2f, convert(pair[0]), convert(pair[1]));
                }
                Handles.EndGUI();
            }
            if (showCounters)
                for (int i = 0; i < map.Territorios.Count; i++)
                {
                    Vector2 point = convert(i);
                    Rect marker = new Rect(point.x - 8f, point.y - 8f, 16f, 16f);
                    EditorGUI.DrawRect(marker, territorySelection.Contains(i) || i == selectedTerritory
                        ? Color.yellow : Color.cyan);
                    GUI.Label(marker, "C", EditorStyles.centeredGreyMiniLabel);
                }
        }
        GUI.Label(new Rect(viewport.x + 8f, viewport.y + 8f, 260f, 22f),
            $"Zoom de edição {previewZoom:0.00}×", EditorStyles.whiteMiniLabel);
    }

    private void HandlePreviewInput(Rect viewport)
    {
        Event e = Event.current;
        if (!viewport.Contains(e.mousePosition)) return;
        if (e.type == EventType.ScrollWheel)
        {
            previewZoom = Mathf.Clamp(previewZoom * (1f - e.delta.y * 0.04f), 0.2f, 8f);
            e.Use(); Repaint();
        }
        else if (e.type == EventType.MouseDrag && (e.button == 2 || e.alt))
        {
            previewPan += e.delta; e.Use(); Repaint();
        }
    }

    private void HandleTerritoryMarkers(Func<int, Vector2> convert,
        WDMapViewportTransform projection)
    {
        Event e = Event.current;
        if (map.Territorios.Count == 0) return;
        int nearest = -1;
        float nearestDistance = 14f;
        for (int i = 0; i < map.Territorios.Count; i++)
        {
            float distance = Vector2.Distance(e.mousePosition,
                convert(i));
            if (distance < nearestDistance) { nearest = i; nearestDistance = distance; }
        }
        if (e.type == EventType.MouseDown && e.button == 0 && nearest >= 0)
        {
            territorySelection.Click(nearest, e.control || e.command);
            selectedTerritory = nearest;
            draggingCounter = tab == Tab.Contadores;
            if (draggingCounter) Undo.RecordObject(map, "Mover contador no mapa");
            e.Use(); Repaint();
        }
        else if (e.type == EventType.MouseDrag && e.button == 0 && draggingCounter)
        {
            SerializedProperty position = serializedMap.FindProperty("territorios")
                .GetArrayElementAtIndex(selectedTerritory)
                .FindPropertyRelative("posicaoContador");
            Vector2 artLocal = projection.ViewportToMap(e.mousePosition);
            position.vector2Value = spatialSnapshot.ToTerritoryLocal(
                map.Territorios[selectedTerritory].id, artLocal);
            serializedMap.ApplyModifiedProperties();
            EditorUtility.SetDirty(map);
            e.Use(); Repaint();
        }
        else if (e.rawType == EventType.MouseUp)
            draggingCounter = false;
    }

    // ============================================================
    // 04. CRIAÇÃO SEGURA E UTILITÁRIOS
    // ============================================================

    private void NewMap()
    {
        WarDominionNewMapWindow.Open(created => SelectMap(created));
    }

    private void GenerateThumbnail()
    {
        string path = EditorUtility.SaveFilePanelInProject("Salvar thumbnail",
            map.MapaId + "_Thumbnail", "png", "Escolha onde salvar o thumbnail gerado.");
        if (string.IsNullOrEmpty(path)) return;
        Texture source = map.ArteBase.texture;
        const int width = 512, height = 288;
        RenderTexture render = RenderTexture.GetTemporary(width, height, 0,
            RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture previous = RenderTexture.active;
        float sourceAspect = (float)source.width / source.height;
        float targetAspect = (float)width / height;
        Vector2 scale = Vector2.one;
        Vector2 offset = Vector2.zero;
        if (sourceAspect > targetAspect)
        {
            scale.x = targetAspect / sourceAspect;
            offset.x = (1f - scale.x) * 0.5f;
        }
        else
        {
            scale.y = sourceAspect / targetAspect;
            offset.y = (1f - scale.y) * 0.5f;
        }
        Graphics.Blit(source, render, scale, offset);
        RenderTexture.active = render;
        Texture2D output = new Texture2D(width, height, TextureFormat.RGBA32, false);
        output.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        output.Apply();
        File.WriteAllBytes(Path.GetFullPath(path), output.EncodeToPNG());
        DestroyImmediate(output);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(render);
        AssetDatabase.ImportAsset(path);
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private bool ConfirmArtChange(Sprite previous, Sprite next)
    {
        if (previous == null || next == null) return EditorUtility.DisplayDialog("Alterar arte",
            "Esta ação altera apenas a referência; geometria e dados serão preservados.", "Continuar", "Cancelar");
        return EditorUtility.DisplayDialog("Substituir Arte Principal",
            "Confirme que a nova arte mantém a geometria. Caso contrário, o mapa ficará marcado para revisão pela validação visual.",
            "Preservar dados e substituir", "Cancelar");
    }

    private void AddTerritory()
    {
        SerializedProperty items = serializedMap.FindProperty("territorios");
        Undo.RecordObject(map, "Criar território");
        items.InsertArrayElementAtIndex(items.arraySize);
        SerializedProperty item = items.GetArrayElementAtIndex(items.arraySize - 1);
        item.FindPropertyRelative("id").stringValue = "novo_territorio";
        item.FindPropertyRelative("nomeExibido").stringValue = "Novo Território";
        item.FindPropertyRelative("regiaoId").stringValue = string.Empty;
        item.FindPropertyRelative("possuiPosicaoContador").boolValue = false;
        selectedTerritory = items.arraySize - 1;
    }

    private void RemoveTerritory()
    {
        DefinicaoTerritorioMapa territory = map.Territorios[selectedTerritory];
        if (!EditorUtility.DisplayDialog("Remover território",
                $"Remover '{territory.nomeExibido}'? Conexões e regiões deverão ser revisadas.",
                "Remover", "Cancelar")) return;
        Undo.RecordObject(map, "Remover território");
        serializedMap.FindProperty("territorios").DeleteArrayElementAtIndex(selectedTerritory);
        selectedTerritory = Mathf.Max(0, selectedTerritory - 1);
    }

    private void AddRegion()
    {
        SerializedProperty items = serializedMap.FindProperty("regioes");
        Undo.RecordObject(map, "Criar região");
        items.InsertArrayElementAtIndex(items.arraySize);
        SerializedProperty item = items.GetArrayElementAtIndex(items.arraySize - 1);
        item.FindPropertyRelative("id").stringValue = "nova_regiao";
        item.FindPropertyRelative("nomeExibido").stringValue = "Nova Região";
        item.FindPropertyRelative("bonus").intValue = 0;
        item.FindPropertyRelative("territorioIds").ClearArray();
        selectedRegion = items.arraySize - 1;
    }

    private void RemoveRegion()
    {
        DefinicaoRegiaoMapa region = map.Regioes[selectedRegion];
        if (!EditorUtility.DisplayDialog("Remover região",
                $"Remover '{region.nomeExibido}'? Os territórios associados ficarão inválidos até serem reatribuídos.",
                "Remover", "Cancelar")) return;
        Undo.RecordObject(map, "Remover região");
        serializedMap.FindProperty("regioes").DeleteArrayElementAtIndex(selectedRegion);
        selectedRegion = Mathf.Max(0, selectedRegion - 1);
    }

    private void Property(string name) => EditorGUILayout.PropertyField(serializedMap.FindProperty(name), true);
    private static void Header(string label) { EditorGUILayout.Space(6f); EditorGUILayout.LabelField(label, EditorStyles.boldLabel); }
}

public sealed class WarDominionNewMapWindow : EditorWindow
{
    private string displayName = "Novo Mapa";
    private string technicalId = "novo_mapa";
    private string scenePath = "Assets/Mapas/NovoMapa/NovoMapa.unity";
    private Sprite mainArt;
    private Texture2D thumbnail;
    private Action<DefinicaoMapa> completed;

    // ============================================================
    // 05. ASSISTENTE DE CRIAÇÃO SEM CÓDIGO ESPECÍFICO POR MAPA
    // ============================================================

    public static void Open(Action<DefinicaoMapa> onCompleted)
    {
        WarDominionNewMapWindow window = CreateInstance<WarDominionNewMapWindow>();
        window.titleContent = new GUIContent("Novo Mapa");
        window.minSize = new Vector2(440f, 260f);
        window.completed = onCompleted;
        window.ShowUtility();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Nova Definição de Mapa", EditorStyles.boldLabel);
        displayName = EditorGUILayout.TextField("Nome visível", displayName);
        technicalId = EditorGUILayout.TextField("ID técnico único", technicalId);
        scenePath = EditorGUILayout.TextField("Scene associada", scenePath);
        mainArt = (Sprite)EditorGUILayout.ObjectField("Arte Principal", mainArt, typeof(Sprite), false);
        thumbnail = (Texture2D)EditorGUILayout.ObjectField("Thumbnail opcional", thumbnail, typeof(Texture2D), false);
        EditorGUILayout.HelpBox("Territórios e regiões podem começar vazios. O mapa só deve ser registrado no catálogo após passar pela validação.", MessageType.Info);
        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(displayName) ||
            string.IsNullOrWhiteSpace(technicalId)))
            if (GUILayout.Button("CRIAR ASSET")) CreateMap();
    }

    private void CreateMap()
    {
        bool duplicateId = AssetDatabase.FindAssets("t:DefinicaoMapa")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<DefinicaoMapa>)
            .Any(existing => existing != null && existing.MapaId == technicalId.Trim());
        if (duplicateId)
        {
            EditorUtility.DisplayDialog("ID duplicado",
                $"Já existe um mapa com o ID '{technicalId.Trim()}'.", "OK");
            return;
        }
        string path = EditorUtility.SaveFilePanelInProject("Salvar definição", technicalId,
            "asset", "Escolha a pasta definitiva do mapa.");
        if (string.IsNullOrEmpty(path)) return;
        DefinicaoMapa map = CreateInstance<DefinicaoMapa>();
        map.ConfigurarNoEditor(technicalId.Trim(), technicalId.Trim(), displayName.Trim(),
            string.Empty, "default", mainArt, Vector2.zero, 5f,
            new List<DefinicaoTerritorioMapa>(), new List<DefinicaoRegiaoMapa>(),
            new List<DefinicaoConexaoMapa>());
        AssetDatabase.CreateAsset(map, path);
        SerializedObject serialized = new SerializedObject(map);
        SerializedProperty metadata = serialized.FindProperty("metadados");
        AddMetadata(metadata, "scenePath", scenePath);
        AddMetadata(metadata, "thumbnailPath", AssetDatabase.GetAssetPath(thumbnail));
        serialized.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        Selection.activeObject = map;
        completed?.Invoke(map);
        Close();
    }

    private static void AddMetadata(SerializedProperty metadata, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        metadata.InsertArrayElementAtIndex(metadata.arraySize);
        SerializedProperty item = metadata.GetArrayElementAtIndex(metadata.arraySize - 1);
        item.FindPropertyRelative("chave").stringValue = key;
        item.FindPropertyRelative("valor").stringValue = value;
    }
}

public static class WarDominionGlobalSettingsMenu
{
    // ============================================================
    // 06. CONFIGURAÇÕES GLOBAIS CENTRALIZADAS
    // ============================================================

    [MenuItem("War Dominion/Configurações/Paleta Global de Jogadores")]
    private static void SelectGlobalPlayerPalette()
    {
        WDPlayerPaletteConfig palette = AssetDatabase.LoadAssetAtPath<WDPlayerPaletteConfig>(
            "Assets/Resources/Config/WDPlayerPaletteConfig.asset");
        Selection.activeObject = palette;
        if (palette != null) EditorGUIUtility.PingObject(palette);
    }
}
