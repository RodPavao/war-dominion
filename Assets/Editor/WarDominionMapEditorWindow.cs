using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class WarDominionMapEditorWindow : EditorWindow
{
    private enum Tab { Mapa, Arte, Territorios, Nomes, Regioes, Conexoes, Contadores, Migracao16x9, Thumbnail, Validacao }
    private enum MigrationFilter { Todos, High, Medium, Low, Unresolved, Validados }
    private enum MigrationViewMode { WdNovo, Antigo, AutoFit, Sobreposto }
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
    private string territoryNameSearch = string.Empty;
    private int selectedRegion;
    private string highlightedRegionTerritoryId;
    private string regionTerritorySearch = string.Empty;
    private int regionMoveTarget;
    private int connectionDestination;
    private TipoConexaoMapa connectionType;
    private bool showArt = true, showGeometry = true, showRegions = true,
        showConnections = true, showCounters = true, showOverlay;
    private bool draggingCounter;
    private bool draggingTerritoryName;
    private bool draggingRegionLabel;
    private bool migrationPreviewEnabled;
    private float migrationOverlayOpacity = 0.45f;
    private Vector2 migrationOverlayScale = Vector2.one;
    private Vector2 migrationOverlayOffset;
    private Sprite migrationCandidate;
    private readonly HashSet<string> migrationManualReview =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<Vector2>> migrationBoundaryCache =
        new Dictionary<string, IReadOnlyList<Vector2>>(StringComparer.Ordinal);
    private readonly Dictionary<string, int[]> migrationTriangleCache =
        new Dictionary<string, int[]>(StringComparer.Ordinal);
    private Vector3[] migrationOutlineBuffer = Array.Empty<Vector3>();
    private readonly Vector3[] migrationTriangleBuffer = new Vector3[3];
    private bool migrationDraggingProposal;
    private bool migrationFineDrag;
    private string migrationDraggedTerritoryId;
    private Vector2 migrationDragStartMouse;
    private Vector2 migrationDragStartOffset;
    private WDMapMigrationAnalysis migrationAnalysis;
    private WDMapMigrationCalibrationStore migrationCalibrationStore;
    private WDMapMigrationBoundaryStore migrationBoundaryStore;
    private WDMap16x9PromotionReport promotionDryRun;
    private bool migrationShowOld;
    private bool migrationShowAllTerritories;
    private bool migrationLightenTerritories = true;
    private float migrationLightening = 0.25f;
    [NonSerialized] private Texture2D migrationLightenedTexture;
    private MigrationFilter migrationFilter = MigrationFilter.Todos;
    private Vector2 migrationResultsScroll;
    private MigrationViewMode migrationViewMode;
    private bool migrationDefaultsApplied;
    private Rect lastPreviewViewport;
    private WDMapSpatialSnapshot spatialSnapshot = new WDMapSpatialSnapshot();
    private WDMapActivePreviewContext activePreviewContext;
    private bool rebuildingPreviewContext;
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
        migrationCalibrationStore = null;
        migrationBoundaryStore = null;
        Undo.undoRedoPerformed += OnUndoRedo;
        EditorApplication.projectChanged += OnProjectChanged;
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
        if (map == null) SelectMap(catalog != null ? catalog.MapaPadrao : null);
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
        EditorApplication.projectChanged -= OnProjectChanged;
        EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        EditorApplication.delayCall -= RebuildMigrationLightenedTexture;
        DestroyMigrationLightenedTexture();
    }
    private void OnUndoRedo()
    {
        serializedMap?.Update();
        InvalidatePreviewContext();
    }

    private void InvalidatePreviewContext()
    {
        activePreviewContext = null;
        Repaint();
    }

    private void OnProjectChanged()
    {
        if (rebuildingPreviewContext || activePreviewContext == null ||
            activePreviewContext.RelevantAssetsUnchanged(map))
            return;
        InvalidatePreviewContext();
    }

    private void OnHierarchyChanged()
    {
        if (rebuildingPreviewContext || map == null)
            return;
        string scenePath = map.Metadados?.FirstOrDefault(metadata =>
            metadata != null && metadata.chave == "scenePath")?.valor;
        UnityEngine.SceneManagement.Scene scene =
            UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath ?? string.Empty);
        if (scene.IsValid() && scene.isLoaded)
            InvalidatePreviewContext();
    }

    private void OnGUI()
    {
        DrawToolbar();
        if (map == null)
        {
            EditorGUILayout.HelpBox("Selecione um mapa do catálogo ou crie um novo.", MessageType.Info);
            return;
        }
        serializedMap.Update();
        EnsureActivePreviewContext();
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
        highlightedRegionTerritoryId = null;
        territorySelection.Clear();
        rebuildingPreviewContext = true;
        try
        {
            activePreviewContext = WDMapActivePreviewContext.Resolve(map);
            spatialSnapshot = activePreviewContext.SpatialSnapshot;
        }
        finally
        {
            rebuildingPreviewContext = false;
        }
        migrationCandidate = map != null ? map.ArteBase : null;
        migrationPreviewEnabled = false;
        migrationOverlayScale = Vector2.one;
        migrationOverlayOffset = Vector2.zero;
        migrationManualReview.Clear();
        migrationBoundaryCache.Clear();
        migrationTriangleCache.Clear();
        migrationAnalysis = null;
        migrationShowOld = false;
        migrationShowAllTerritories = false;
        migrationDraggingProposal = false;
        migrationDraggedTerritoryId = null;
        migrationDefaultsApplied = false;
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
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(420f),
                   GUILayout.ExpandHeight(true)))
        {
            sideScroll = EditorGUILayout.BeginScrollView(sideScroll, false, true,
                GUILayout.ExpandHeight(true));
            switch (tab)
            {
                case Tab.Mapa: DrawMap(); break;
                case Tab.Arte: DrawArt(); break;
                case Tab.Territorios: DrawTerritories(); break;
                case Tab.Nomes: DrawTerritoryNamesPanel(); break;
                case Tab.Regioes: DrawRegions(); break;
                case Tab.Conexoes: DrawConnections(); break;
                case Tab.Contadores: DrawCounters(); break;
                case Tab.Migracao16x9: DrawMigration16x9(); break;
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

    private void DrawTerritoryNamesPanel()
    {
        Header($"Nomes territoriais ({map.Territorios.Count})");
        territoryNameSearch = EditorGUILayout.TextField("Buscar", territoryNameSearch);
        foreach (DefinicaoTerritorioMapa territory in
                 WarDominionMapEditorOperations.SearchTerritories(map,
                     territoryNameSearch))
        {
            int index = ResolveTerritoryIndex(map, territory.id);
            string display = string.IsNullOrWhiteSpace(territory.nomeExibido)
                ? territory.id : territory.nomeExibido;
            if (GUILayout.Toggle(selectedTerritory == index,
                    $"{display}  [{territory.id}]", "Button"))
                selectedTerritory = index;
        }
        if (map.Territorios.Count == 0) return;
        selectedTerritory = Mathf.Clamp(selectedTerritory, 0,
            map.Territorios.Count - 1);
        DefinicaoTerritorioMapa selected = map.Territorios[selectedTerritory];
        if (selected == null) return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Território",
            string.IsNullOrWhiteSpace(selected.nomeExibido)
                ? selected.id : selected.nomeExibido);
        EditorGUILayout.LabelField("ID", selected.id);
        EditorGUILayout.LabelField("Posicionamento",
            selected.possuiPosicaoNomeManual ? "Manual" : "Automático");
        EditorGUI.BeginChangeCheck();
        Vector2 manual = EditorGUILayout.Vector2Field("Posição manual",
            selected.posicaoNomeManual);
        if (EditorGUI.EndChangeCheck() &&
            WarDominionMapEditorOperations.SetTerritoryNamePosition(map,
                selected.id, manual))
            AssetDatabase.SaveAssetIfDirty(map);
        EditorGUI.BeginChangeCheck();
        int fontSize = EditorGUILayout.IntSlider("Tamanho da fonte",
            selected.tamanhoFonteNome, 0,
            NomesTerritoriosSceneView.TamanhoFonteMaximo);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(map, "Alterar tamanho do nome de " + selected.id);
            selected.tamanhoFonteNome = fontSize == 0 ? 0 : Mathf.Max(
                NomesTerritoriosSceneView.TamanhoFonteMinimo, fontSize);
            EditorUtility.SetDirty(map);
            AssetDatabase.SaveAssetIfDirty(map);
        }
        using (new EditorGUI.DisabledScope(!selected.possuiPosicaoNomeManual))
            if (GUILayout.Button("Restaurar posição automática") &&
                WarDominionMapEditorOperations.ResetTerritoryNamePosition(map,
                    selected.id))
                AssetDatabase.SaveAssetIfDirty(map);
        if (GUILayout.Button("Restaurar todos para automático") &&
            EditorUtility.DisplayDialog("Restaurar todas as posições?",
                "Todos os overrides manuais deste mapa serão removidos.",
                "Restaurar", "Cancelar") &&
            WarDominionMapEditorOperations.ResetAllTerritoryNamePositions(map) > 0)
            AssetDatabase.SaveAssetIfDirty(map);
        EditorGUILayout.HelpBox(
            "Clique no território ou no nome no preview e arraste o nome selecionado. Undo/Redo é suportado.",
            MessageType.Info);
    }

    private void DrawRegions()
    {
        Header($"Regiões ({map.Regioes.Count})");
        if (map.Regioes.Count > 0)
        {
            string[] labels = map.Regioes.Select(region => region.nomeExibido + " [" + region.id + "]").ToArray();
            selectedRegion = EditorGUILayout.Popup("Região", Mathf.Clamp(selectedRegion, 0,
                map.Regioes.Count - 1), labels);
            DefinicaoRegiaoMapa region = map.Regioes[selectedRegion];
            DrawSelectedRegion(region);
            EditorGUILayout.LabelField("Posição da label",
                region.possuiDeslocamentoLabelManual ? "Manual" : "Automática");
            using (new EditorGUI.DisabledScope(!region.possuiDeslocamentoLabelManual))
                if (GUILayout.Button("Resetar label para Automático"))
                    WarDominionMapEditorOperations.ResetRegionLabelOffset(map, region);
            EditorGUILayout.HelpBox(
                "Arraste a label selecionada no preview. O deslocamento afeta somente a apresentação da região e suporta Undo/Redo.",
                MessageType.Info);
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField($"Membros — {region.territorioIds.Count} territórios",
                EditorStyles.boldLabel);
            foreach (string territoryId in region.territorioIds.ToArray())
            {
                int territoryIndex = ResolveTerritoryIndex(map, territoryId);
                DefinicaoTerritorioMapa territory = territoryIndex >= 0
                    ? map.Territorios[territoryIndex] : null;
                using (new EditorGUI.DisabledScope(territoryIndex < 0))
                {
                    bool selected = string.Equals(highlightedRegionTerritoryId,
                        territoryId, StringComparison.Ordinal);
                    string memberLabel = territory != null
                        ? $"{territory.nomeExibido}  [{territory.id}]" : territoryId + "  [AUSENTE]";
                    if (GUILayout.Toggle(selected, memberLabel, "Button") != selected)
                    {
                        highlightedRegionTerritoryId = selected ? null : territoryId;
                        if (!selected && territoryIndex >= 0)
                        {
                            selectedTerritory = territoryIndex;
                            FocusTerritoryInPreview(territoryIndex);
                        }
                        Repaint();
                    }
                }
            }
            DrawTerritorySearch();
            DrawSelectedRegionTerritoryPanel(labels);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Criar região")) AddRegion();
            using (new EditorGUI.DisabledScope(map.Regioes.Count == 0))
                if (GUILayout.Button("Remover região")) RemoveRegion();
        }
        EditorGUILayout.HelpBox("A associação permanece autoritativa em territorio.regiaoId e região.territorioIds.", MessageType.Info);
    }

    private void DrawSelectedRegion(DefinicaoRegiaoMapa region)
    {
        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField("REGIÃO SELECIONADA", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Nome", region.nomeExibido ?? string.Empty);
        EditorGUILayout.LabelField("ID", region.id ?? string.Empty);
        EditorGUILayout.LabelField("Quantidade", $"{region.territorioIds?.Count ?? 0} territórios");
        SerializedProperty property = serializedMap.FindProperty("regioes")
            .GetArrayElementAtIndex(selectedRegion);
        EditorGUILayout.PropertyField(property.FindPropertyRelative("nomeExibido"));
        EditorGUILayout.PropertyField(property.FindPropertyRelative("descricao"));
        EditorGUILayout.PropertyField(property.FindPropertyRelative("chaveTraducao"));
        EditorGUILayout.PropertyField(property.FindPropertyRelative("bonus"));
        EditorGUILayout.PropertyField(property.FindPropertyRelative("ordemExibicao"));
        EditorGUILayout.PropertyField(property.FindPropertyRelative("corDestaque"));
        EditorGUILayout.PropertyField(property.FindPropertyRelative("possuiDeslocamentoLabelManual"));
        EditorGUILayout.PropertyField(property.FindPropertyRelative("deslocamentoLabelManual"));
        EditorGUILayout.PropertyField(property.FindPropertyRelative("metadados"), true);
    }

    private void DrawTerritorySearch()
    {
        EditorGUILayout.Space(6f);
        regionTerritorySearch = EditorGUILayout.TextField("Buscar território...",
            regionTerritorySearch);
        if (string.IsNullOrWhiteSpace(regionTerritorySearch)) return;
        foreach (DefinicaoTerritorioMapa territory in
                 WarDominionMapEditorOperations.SearchTerritories(map,
                     regionTerritorySearch).Take(12))
            if (GUILayout.Button($"{territory.nomeExibido}  [{territory.id}]"))
                SelectRegionTerritory(territory.id, true);
    }

    private void DrawSelectedRegionTerritoryPanel(string[] regionLabels)
    {
        EditorGUILayout.Space(7f);
        EditorGUILayout.LabelField("TERRITÓRIO SELECIONADO", EditorStyles.boldLabel);
        int index = ResolveTerritoryIndex(map, highlightedRegionTerritoryId);
        if (index < 0)
        {
            EditorGUILayout.HelpBox("Nenhum território selecionado.", MessageType.None);
            return;
        }
        DefinicaoTerritorioMapa territory = map.Territorios[index];
        var diagnostic = WarDominionMapEditorOperations.DiagnoseRegionAssociation(
            map, territory.id);
        EditorGUILayout.LabelField("Nome", territory.nomeExibido ?? string.Empty);
        EditorGUILayout.LabelField("ID", territory.id ?? string.Empty);
        EditorGUILayout.LabelField("Região atual", diagnostic.DeclaredRegion != null
            ? $"{diagnostic.DeclaredRegion.nomeExibido} [{diagnostic.DeclaredRegion.id}]"
            : "SEM REGIÃO");
        if (!diagnostic.IsConsistent)
            EditorGUILayout.HelpBox(DescribeRegionInconsistency(diagnostic),
                MessageType.Warning);

        regionMoveTarget = Mathf.Clamp(regionMoveTarget, 0, map.Regioes.Count - 1);
        regionMoveTarget = EditorGUILayout.Popup("Mover para região", regionMoveTarget,
            regionLabels);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Mover / corrigir associação"))
            {
                WarDominionMapEditorOperations.MoveTerritoryToRegion(map, territory.id,
                    map.Regioes[regionMoveTarget].id);
                selectedRegion = regionMoveTarget;
                serializedMap.Update();
                Repaint();
            }
            using (new EditorGUI.DisabledScope(diagnostic.State ==
                       WarDominionMapEditorOperations.RegionAssociationState.Unassigned))
                if (GUILayout.Button("Remover da região"))
                {
                    WarDominionMapEditorOperations.RemoveTerritoryFromRegion(map,
                        territory.id);
                    serializedMap.Update();
                    Repaint();
                }
        }
        if (GUILayout.Button("+ Adicionar território existente à região selecionada"))
        {
            WarDominionMapEditorOperations.MoveTerritoryToRegion(map, territory.id,
                map.Regioes[selectedRegion].id);
            regionMoveTarget = selectedRegion;
            serializedMap.Update();
            Repaint();
        }
    }

    private static string DescribeRegionInconsistency(
        WarDominionMapEditorOperations.RegionAssociationDiagnostic diagnostic)
    {
        string listed = diagnostic.ContainingRegions.Count == 0 ? "nenhuma"
            : string.Join(", ", diagnostic.ContainingRegions.Select(region => region.id));
        return $"⚠ Associação inconsistente ({diagnostic.State}). " +
            $"territorio.regiaoId = {diagnostic.Territory?.regiaoId ?? "<ausente>"}; " +
            $"listas que contêm o território = {listed}.";
    }

    private void SelectRegionTerritory(string territoryId, bool focus)
    {
        int index = ResolveTerritoryIndex(map, territoryId);
        if (index < 0) return;
        selectedTerritory = index;
        highlightedRegionTerritoryId = territoryId;
        DefinicaoRegiaoMapa declared = map.Regioes.FirstOrDefault(region => region != null &&
            string.Equals(region.id, map.Territorios[index].regiaoId,
                StringComparison.Ordinal));
        if (declared != null) selectedRegion = Enumerable.Range(0, map.Regioes.Count)
            .First(regionIndex => ReferenceEquals(map.Regioes[regionIndex], declared));
        regionMoveTarget = Mathf.Clamp(selectedRegion, 0, map.Regioes.Count - 1);
        territorySelection.Click(index, false);
        if (focus) FocusTerritoryInPreview(index);
        Repaint();
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

    private void DrawMigration16x9()
    {
        EnsureMigrationResources();
        Header("Migração 16:9 — preview não destrutivo");
        EditorGUI.BeginChangeCheck();
        if (!migrationDefaultsApplied)
        {
            showConnections = false;
            showRegions = false;
            migrationDefaultsApplied = true;
        }

        EditorGUILayout.HelpBox(
            "Preview leve e não destrutivo: WD oficial + um único território por vez.",
            MessageType.Info);
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.ObjectField("Arte WD oficial", migrationCandidate,
                typeof(Sprite), false);
        bool nextLighten = EditorGUILayout.ToggleLeft("Clarear territórios",
            migrationLightenTerritories);
        float nextLightening = Mathf.Clamp(EditorGUILayout.DelayedFloatField(
            "Clareamento (%)", migrationLightening * 100f), 0f, 100f) / 100f;
        if (nextLighten != migrationLightenTerritories ||
            !Mathf.Approximately(nextLightening, migrationLightening))
        {
            migrationLightenTerritories = nextLighten;
            migrationLightening = nextLightening;
            ScheduleMigrationLightenedTextureRebuild();
        }

        using (new EditorGUI.DisabledScope(migrationCandidate == null))
            if (GUILayout.Button("CALCULAR PROPOSTAS")) RunMigrationAutoFit();

        using (new EditorGUI.DisabledScope(!WDMapTerritoryBoundaryGenerator.TryGetMaskPath(
                   map.MapaId, out _)))
            if (GUILayout.Button("GERAR BOUNDARIES DA TERRITORY MASK"))
                GenerateTerritoryMaskBoundaries();

        DrawMaskBoundaryStatus();
        DrawPromotionDryRun();

        if (migrationAnalysis == null)
        {
            EditorGUILayout.HelpBox(
                "Clique em CALCULAR PROPOSTAS. Nenhum cálculo ocorre ao abrir a aba ou durante repaint.",
                MessageType.None);
            EditorGUI.EndChangeCheck();
            return;
        }

        selectedTerritory = Mathf.Clamp(selectedTerritory, 0,
            map.Territorios.Count - 1);
        string[] territoryNames = map.Territorios.Select(item =>
            string.IsNullOrWhiteSpace(item.nomeExibido) ? item.id : item.nomeExibido)
            .ToArray();
        int directlySelected = EditorGUILayout.Popup("Território", selectedTerritory,
            territoryNames);
        if (directlySelected != selectedTerritory)
            SelectMigrationTerritory(directlySelected);
        DefinicaoTerritorioMapa territory = map.Territorios[selectedTerritory];
        WDMapMigrationFitResult selected = migrationAnalysis.Results.FirstOrDefault(item =>
            item.TerritoryId == territory.id);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Território", territory.nomeExibido,
            EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Índice",
            $"{selectedTerritory + 1} / {map.Territorios.Count}");
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("← Anterior")) SelectSimpleMigrationTerritory(-1);
            if (GUILayout.Button("Próximo →")) SelectSimpleMigrationTerritory(1);
        }
        migrationShowAllTerritories = EditorGUILayout.ToggleLeft(
            "Mostrar todos os territórios", migrationShowAllTerritories);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("FOCAR TERRITÓRIO")) FocusSelectedMigrationTerritory();
            if (GUILayout.Button("MAPA INTEIRO")) ResetMigrationViewport();
        }

        if (selected != null)
        {
            bool saved = TryGetMatchingSavedCalibration(selected, out var savedRecord);
            string status = savedRecord != null &&
                savedRecord.state == WDMapMigrationPersistenceState.AppliedToMap
                ? "APLICADO NO MAPA"
                : saved ? "SALVO PARA MIGRAÇÃO"
                : selected.Accepted ? "OK — NÃO APLICADO"
                : migrationManualReview.Contains(selected.TerritoryId) ? "REVISAR"
                : savedRecord != null ? "PENDENTE — ALTERAÇÃO NÃO SALVA" : "PENDENTE";
            EditorGUILayout.LabelField("Status", status);
            EditorGUILayout.LabelField("Confidence", selected.Confidence.ToString().ToUpperInvariant());
            EditorGUILayout.LabelField("Score", selected.Score.ToString("0.0000"));
            migrationShowOld = EditorGUILayout.ToggleLeft(
                "Mostrar contorno antigo", migrationShowOld);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("MARCAR OK"))
                {
                    selected.Accepted = true;
                    selected.Rejected = false;
                    migrationManualReview.Remove(selected.TerritoryId);
                }
                if (GUILayout.Button("MARCAR REVISÃO"))
                {
                    selected.Accepted = false;
                    selected.Rejected = true;
                    migrationManualReview.Add(selected.TerritoryId);
                }
            }

            WDMapMigrationTransform adjusted = selected.CandidateTransform;
            Vector2 adjustedOffset = EditorGUILayout.Vector2Field("Mover X/Y (px)",
                adjusted.OffsetPixels);
            Vector2 adjustedScale = EditorGUILayout.Vector2Field("Scale X/Y",
                adjusted.Scale);
            float adjustedRotation = EditorGUILayout.Slider("Rotação",
                adjusted.RotationDegrees, -5f, 5f);
            if (adjustedOffset != adjusted.OffsetPixels ||
                adjustedScale != adjusted.Scale ||
                !Mathf.Approximately(adjustedRotation, adjusted.RotationDegrees))
            {
                selected.AdjustCandidate(new WDMapMigrationTransform(adjustedScale,
                    adjustedOffset, adjustedRotation));
                migrationManualReview.Remove(selected.TerritoryId);
            }
            if (GUILayout.Button("RESETAR PROPOSTA"))
            {
                selected.ResetCandidate();
                migrationManualReview.Remove(selected.TerritoryId);
            }

            using (new EditorGUI.DisabledScope(!selected.Accepted))
                if (GUILayout.Button("APLICAR TERRITÓRIO (SALVAR PARA MIGRAÇÃO)"))
                    SaveMigrationCalibrations(new[] { selected },
                        "Salvar calibração territorial");
            if (savedRecord != null && GUILayout.Button("RESTAURAR ORIGINAL"))
            {
                WDMapMigrationCalibrationRepository.Remove(migrationCalibrationStore,
                    map.MapaId, selected.TerritoryId);
                selected.ResetCandidate();
                selected.Accepted = false;
                selected.Rejected = false;
                migrationManualReview.Remove(selected.TerritoryId);
            }
        }

        IReadOnlyList<WDMapMigrationFitResult> approved =
            WDMapMigrationCalibrationRepository.SelectApprovedUnsaved(
                migrationCalibrationStore, map.MapaId, migrationAnalysis.Results);
        using (new EditorGUI.DisabledScope(approved.Count == 0))
            if (GUILayout.Button($"APLICAR TODOS OK — SALVAR ({approved.Count})") &&
                EditorUtility.DisplayDialog("Salvar calibrações aprovadas",
                    $"Salvar {approved.Count} calibrações OK para a migração? " +
                    "Nenhuma scene será alterada.", "Salvar", "Cancelar"))
                SaveMigrationCalibrations(approved, "Salvar calibrações territoriais");

        WDMapMigrationProgress progress = WDMapMigrationProgress.Calculate(
            map.MapaId, migrationAnalysis.Results, migrationCalibrationStore,
            migrationManualReview);
        EditorGUILayout.LabelField("Salvos para migração",
            $"{progress.Saved} / {map.Territorios.Count}");
        EditorGUILayout.LabelField("OK não salvos", progress.ApprovedUnsaved.ToString());
        EditorGUILayout.LabelField("Revisar", progress.Review.ToString());
        EditorGUILayout.LabelField("Pendentes", progress.Pending.ToString());
        using (new EditorGUI.DisabledScope(true))
            GUILayout.Button(progress.Saved == map.Territorios.Count
                ? "FINALIZAR MIGRAÇÃO DO CLASSIC — PRONTO, AINDA BLOQUEADO"
                : "FINALIZAR MIGRAÇÃO DO CLASSIC — REQUER 42/42");

        EditorGUILayout.HelpBox(
            "Aplicado no mapa: 0. OK não grava; SALVAR persiste somente dados editoriais de migração.",
            MessageType.Warning);
        if (EditorGUI.EndChangeCheck()) Repaint();
    }

    // ============================================================
    // 16. PROMOÇÃO 16:9 — SOMENTE ANÁLISE, PLANO E RELATÓRIO
    // ============================================================
    private void DrawPromotionDryRun()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("PROMOÇÃO 16:9", EditorStyles.boldLabel);
        bool isClassic = string.Equals(map.MapaId, "classic",
            StringComparison.Ordinal);
        EditorGUILayout.LabelField("Mapa", map.NomeExibido);
        using (new EditorGUI.DisabledScope(!isClassic))
            if (GUILayout.Button("ANALISAR PROMOÇÃO — DRY-RUN"))
                promotionDryRun = WDMap16x9PromotionService.AnalyzeClassic(true);
        if (!isClassic)
        {
            EditorGUILayout.HelpBox(
                "O dry-run de promoção está habilitado somente para o piloto Classic.",
                MessageType.Info);
            return;
        }
        if (promotionDryRun != null)
        {
            MessageType messageType = promotionDryRun.Status ==
                WDMap16x9PromotionStatus.Blocked ? MessageType.Error :
                promotionDryRun.Status == WDMap16x9PromotionStatus.Warning
                    ? MessageType.Warning : MessageType.Info;
            EditorGUILayout.HelpBox(promotionDryRun.StatusLabel, messageType);
            EditorGUILayout.LabelField("TerritoryIds",
                $"{promotionDryRun.TerritoryIds}/{promotionDryRun.ExpectedTerritoryIds}");
            EditorGUILayout.LabelField("Paths",
                $"{promotionDryRun.Paths}/{promotionDryRun.ExpectedPaths}");
            EditorGUILayout.LabelField("Ambiguidades",
                promotionDryRun.Ambiguities.ToString());
            EditorGUILayout.LabelField("Multipartes",
                $"{promotionDryRun.MultipartTerritories}/2");
            EditorGUILayout.LabelField("TerritorioClique",
                $"{promotionDryRun.TerritoryClicks}/{promotionDryRun.ExpectedTerritoryIds}");
            EditorGUILayout.LabelField("Colliders",
                $"{promotionDryRun.Colliders}/{promotionDryRun.ExpectedTerritoryIds}");
            EditorGUILayout.LabelField("Conexões",
                promotionDryRun.Blockers.Any(item => item.StartsWith("Conexão ",
                    StringComparison.Ordinal)) ? "BLOQUEADO" : "OK");
            EditorGUILayout.LabelField("Regiões",
                promotionDryRun.Blockers.Any(item => item.StartsWith("Região ",
                    StringComparison.Ordinal)) ? "BLOQUEADO" : "OK");
            EditorGUILayout.LabelField("Representação visual",
                promotionDryRun.Territories.All(item => item.Promotable)
                    ? "PLANEJADA" : "BLOQUEADA");
            EditorGUILayout.LabelField("Contadores preserváveis",
                promotionDryRun.CountersPreservable.ToString());
            EditorGUILayout.LabelField("Contadores a reposicionar",
                promotionDryRun.CountersRepositioned.ToString());
            EditorGUILayout.LabelField("Câmera",
                $"proposta {promotionDryRun.ProposedOrthographicSize:0.####}");
            EditorGUILayout.LabelField("Cena alterada", "NÃO");
            EditorGUILayout.LabelField("Relatório", WDMap16x9PromotionService.ReportPath);
        }
        bool backupValid = WDMap16x9PromotionApplier.BackupIsValid(out _);
        bool canApply = WDMap16x9PromotionApplier.CanApply(promotionDryRun,
            backupValid);
        using (new EditorGUI.DisabledScope(!canApply))
            if (GUILayout.Button("APLICAR PROMOÇÃO DO CLASSIC 16:9") &&
                EditorUtility.DisplayDialog("Promover Classic para WD 16:9",
                    "Esta operação modificará a cena Classic e sua DefinicaoMapa. " +
                    "Backup seletivo validado. Deseja aplicar a promoção 16:9?",
                    "Aplicar promoção", "Cancelar"))
            {
                WDMap16x9PromotionResult result =
                    WDMap16x9PromotionApplier.ApplyClassic();
                if (!result.Applied)
                    EditorUtility.DisplayDialog("Promoção abortada", result.Message, "OK");
                else
                {
                    EditorUtility.DisplayDialog("Classic promovido",
                        "Promoção estrutural concluída. Valide manualmente em Play Mode.", "OK");
                    promotionDryRun = result.After;
                }
            }
        if (!canApply)
            EditorGUILayout.HelpBox("Aplicação bloqueada até dry-run READY e backup válido.",
                MessageType.Info);
    }

    private void GenerateTerritoryMaskBoundaries()
    {
        migrationBoundaryStore = WDMapMigrationBoundaryRepository.LoadOrCreate();
        bool hasExisting = migrationBoundaryStore.ForMap(map.MapaId).Any();
        if (hasExisting && !EditorUtility.DisplayDialog("Substituir boundaries MASK AUTO?",
                "Já existem boundaries editoriais para este mapa. A geração substituirá " +
                "somente os registros MASK AUTO deste mapa; calibrações antigas não serão tocadas.",
                "Gerar novamente", "Cancelar")) return;
        try
        {
            WDMapBoundaryGenerationResult result = WDMapTerritoryBoundaryGenerator.Generate(
                map, spatialSnapshot);
            WDMapMigrationBoundaryRepository.SaveMap(migrationBoundaryStore, map.MapaId,
                result.Records);
            int high = result.Records.Count(item => item.confidence ==
                WDMapBoundaryConfidence.High);
            int medium = result.Records.Count(item => item.confidence ==
                WDMapBoundaryConfidence.Medium);
            int low = result.Records.Count(item => item.confidence ==
                WDMapBoundaryConfidence.Low);
            int ambiguous = result.Records.Count(item => item.confidence ==
                WDMapBoundaryConfidence.Ambiguous);
            Debug.Log($"[WD MASK BOUNDARY] {result.MapId} | oficiais=" +
                $"{result.OfficialTerritories} | válidos={result.ValidComponents} | " +
                $"HIGH={high} | MEDIUM={medium} | LOW={low} | AMBIGUOUS={ambiguous} | " +
                $"multipartes={result.Records.Count(item => item.paths.Count > 1)} | " +
                $"artefatos={result.DiscardedArtifacts} | não associados=" +
                result.Unassociated.Count);
            Repaint();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Falha ao gerar boundaries", exception.Message, "OK");
        }
    }

    private void DrawMaskBoundaryStatus()
    {
        if (migrationBoundaryStore == null) return;
        WDMapMigrationBoundaryRecord[] records = migrationBoundaryStore.ForMap(map.MapaId)
            .ToArray();
        if (records.Length == 0) return;
        int paths = records.Sum(item => item.paths.Count);
        EditorGUILayout.LabelField("MASK AUTO",
            $"{records.Length} TerritoryIds | {paths} polygon paths");
        if (map.Territorios.Count == 0) return;
        string id = map.Territorios[Mathf.Clamp(selectedTerritory, 0,
            map.Territorios.Count - 1)].id;
        if (!migrationBoundaryStore.TryGet(map.MapaId, id,
                out WDMapMigrationBoundaryRecord selected))
        {
            EditorGUILayout.HelpBox("Território sem associação MASK AUTO.",
                MessageType.Warning);
            return;
        }
        EditorGUILayout.LabelField("Origem", selected.origin);
        EditorGUILayout.LabelField("Confidence", selected.confidence.ToString().ToUpperInvariant());
        EditorGUILayout.LabelField("Polygon paths", selected.paths.Count.ToString());
        EditorGUILayout.LabelField("Multipartes", selected.paths.Count > 1 ? "SIM" : "NÃO");
        EditorGUILayout.LabelField("Revisão", selected.reviewState.ToString());
    }

    private void SelectSimpleMigrationTerritory(int direction)
    {
        if (map.Territorios.Count == 0) return;
        SelectMigrationTerritory((selectedTerritory + direction +
            map.Territorios.Count) % map.Territorios.Count);
    }

    private void SelectMigrationTerritory(int index)
    {
        if (map == null || map.Territorios.Count == 0) return;
        selectedTerritory = Mathf.Clamp(index, 0, map.Territorios.Count - 1);
        migrationDraggingProposal = false;
        migrationDraggedTerritoryId = null;
        Repaint();
    }

    private void ResetMigrationViewport()
    {
        previewZoom = 1f;
        previewPan = Vector2.zero;
    }

    private void FocusSelectedMigrationTerritory()
    {
        if (migrationAnalysis == null || map.Territorios.Count == 0 ||
            lastPreviewViewport.width <= 0f) return;
        string id = map.Territorios[Mathf.Clamp(selectedTerritory, 0,
            map.Territorios.Count - 1)].id;
        WDMapMigrationFitResult result = migrationAnalysis.Results.FirstOrDefault(item =>
            item.TerritoryId == id);
        if (result == null || !migrationBoundaryCache.TryGetValue(id,
                out IReadOnlyList<Vector2> boundary) || boundary.Count < 2) return;

        Vector2 center = Vector2.zero;
        for (int index = 0; index < boundary.Count - 1; index++)
            center += result.CandidateTransform.TransformPoint(boundary[index]);
        center /= boundary.Count - 1;
        previewZoom = 2f;
        float fit = Mathf.Min(lastPreviewViewport.width / migrationCandidate.rect.width,
            lastPreviewViewport.height / migrationCandidate.rect.height);
        Vector2 displayedSize = migrationCandidate.rect.size * fit * previewZoom;
        Vector2 normalized = new Vector2(center.x / migrationCandidate.rect.width,
            center.y / migrationCandidate.rect.height);
        previewPan = -new Vector2((normalized.x - 0.5f) * displayedSize.x,
            (normalized.y - 0.5f) * displayedSize.y);
    }

    private void RunMigrationAutoFit()
    {
        var global = new WDMapMigrationTransform(
            new Vector2(1.0986257074f, 1.0671232877f),
            new Vector2(210.1374293f, 24.1068493f));
        try
        {
            migrationAnalysis = WarDominionMapMigrationFitter.Analyze(map,
                migrationCandidate, global, (index, total, id) =>
                    !EditorUtility.DisplayCancelableProgressBar(
                        "War Dominion — Auto-Fit territorial",
                        $"{id} ({index + 1}/{total})", (float)index / total));
        }
        finally { EditorUtility.ClearProgressBar(); }
        migrationBoundaryCache.Clear();
        migrationTriangleCache.Clear();
        if (migrationAnalysis != null)
            foreach (WDMapMigrationFitResult result in migrationAnalysis.Results)
            {
                migrationBoundaryCache[result.TerritoryId] =
                    WarDominionMapMigrationFitter.GetPreviewBoundary(result.TerritoryId,
                        new WDMapMigrationTransform(Vector2.one, Vector2.zero));
                migrationTriangleCache[result.TerritoryId] = TriangulateClosedPolygon(
                    migrationBoundaryCache[result.TerritoryId]);
                if (migrationCalibrationStore != null &&
                    migrationCalibrationStore.TryGet(map.MapaId, result.TerritoryId,
                        out WDMapMigrationCalibrationRecord saved))
                    result.AdjustCandidate(saved.CandidateTransform);
            }
        migrationPreviewEnabled = true;
        Repaint();
    }

    private bool TryGetMatchingSavedCalibration(WDMapMigrationFitResult result,
        out WDMapMigrationCalibrationRecord record)
    {
        record = null;
        if (migrationCalibrationStore == null ||
            !migrationCalibrationStore.TryGet(map.MapaId, result.TerritoryId, out record))
            return false;
        WDMapMigrationTransform saved = record.CandidateTransform;
        WDMapMigrationTransform current = result.CandidateTransform;
        return WDMapMigrationCalibrationRepository.Matches(migrationCalibrationStore,
            map.MapaId, result);
    }

    private void SaveMigrationCalibrations(IEnumerable<WDMapMigrationFitResult> results,
        string undoName)
    {
        migrationCalibrationStore = migrationCalibrationStore != null
            ? migrationCalibrationStore : WDMapMigrationCalibrationRepository.LoadOrCreate();
        WDMapMigrationCalibrationRepository.Save(migrationCalibrationStore, map.MapaId,
            results, undoName);
        Repaint();
    }

    private void DrawMigrationNavigation()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Anterior")) SelectMigrationRelative(-1, _ => true);
            if (GUILayout.Button("Próximo")) SelectMigrationRelative(1, _ => true);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Próximo não validado"))
                SelectMigrationRelative(1, item => !item.Accepted);
            if (GUILayout.Button("Próximo Low"))
                SelectMigrationRelative(1, item => item.Confidence == WDMapMigrationConfidence.Low);
            if (GUILayout.Button("Próximo Unresolved"))
                SelectMigrationRelative(1, item => item.Confidence == WDMapMigrationConfidence.Unresolved);
        }
    }

    private void DrawMigrationResults()
    {
        IEnumerable<WDMapMigrationFitResult> filtered = migrationAnalysis.Results.Where(FilterMigrationResult);
        migrationResultsScroll = EditorGUILayout.BeginScrollView(migrationResultsScroll,
            GUILayout.MinHeight(120f), GUILayout.MaxHeight(260f));
        foreach (WDMapMigrationFitResult result in filtered)
        {
            int index = map.Territorios.ToList().FindIndex(item => item.id == result.TerritoryId);
            string marker = result.Accepted ? "✓" : result.Confidence == WDMapMigrationConfidence.High
                ? "✓" : result.Confidence == WDMapMigrationConfidence.Medium ? "⚠" : "!";
            if (GUILayout.Toggle(index == selectedTerritory,
                    $"{marker} {result.TerritoryId} — {result.Confidence} — {result.Score:0.000}",
                    "Button"))
                selectedTerritory = Mathf.Max(0, index);
        }
        EditorGUILayout.EndScrollView();

        WDMapMigrationFitResult selected = migrationAnalysis.Results.FirstOrDefault(item =>
            map.Territorios[selectedTerritory].id == item.TerritoryId);
        if (selected == null) return;
        EditorGUILayout.HelpBox(selected.Diagnostic, MessageType.Info);
        EditorGUILayout.LabelField("Território", selected.DisplayName);
        EditorGUILayout.LabelField("Confidence", selected.Confidence.ToString().ToUpperInvariant());
        EditorGUILayout.LabelField("Score", selected.Score.ToString("0.0000"));
        EditorGUILayout.LabelField("REFERÊNCIA", "WD NOVO", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("PROPOSTA", "AUTO-FIT", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Global", DescribeTransform(selected.OriginalTransform));
        EditorGUILayout.LabelField("Candidato", DescribeTransform(selected.CandidateTransform));
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(selected.Confidence == WDMapMigrationConfidence.Low ||
                selected.Confidence == WDMapMigrationConfidence.Unresolved))
                if (GUILayout.Button("ACEITAR"))
                {
                    selected.Accepted = true;
                    selected.Rejected = false;
                }
            if (GUILayout.Button("REJEITAR"))
            {
                selected.Accepted = false;
                selected.Rejected = true;
            }
            if (GUILayout.Button("RESETAR")) migrationAnalysis.Reset(selected);
        }
        if (GUILayout.Button("AJUSTAR"))
            migrationViewMode = MigrationViewMode.AutoFit;
        WDMapMigrationTransform adjusted = selected.CandidateTransform;
        Vector2 adjustedOffset = EditorGUILayout.Vector2Field("Mover X/Y (px)",
            adjusted.OffsetPixels);
        Vector2 adjustedScale = EditorGUILayout.Vector2Field("Scale X/Y", adjusted.Scale);
        float adjustedRotation = EditorGUILayout.Slider("Rotação", adjusted.RotationDegrees,
            -5f, 5f);
        if (adjustedOffset != adjusted.OffsetPixels || adjustedScale != adjusted.Scale ||
            !Mathf.Approximately(adjustedRotation, adjusted.RotationDegrees))
            selected.AdjustCandidate(new WDMapMigrationTransform(adjustedScale,
                adjustedOffset, adjustedRotation));
        if (GUILayout.Button("MARCAR REVISÃO"))
            migrationManualReview.Add(selected.TerritoryId);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("FOCAR TERRITÓRIO")) FocusMigrationTerritory(selected);
            if (GUILayout.Button("VOLTAR AO MAPA INTEIRO"))
            {
                previewZoom = 1f;
                previewPan = Vector2.zero;
            }
        }
    }

    private bool FilterMigrationResult(WDMapMigrationFitResult item)
    {
        switch (migrationFilter)
        {
            case MigrationFilter.High: return item.Confidence == WDMapMigrationConfidence.High;
            case MigrationFilter.Medium: return item.Confidence == WDMapMigrationConfidence.Medium;
            case MigrationFilter.Low: return item.Confidence == WDMapMigrationConfidence.Low;
            case MigrationFilter.Unresolved: return item.Confidence == WDMapMigrationConfidence.Unresolved;
            case MigrationFilter.Validados: return item.Accepted;
            default: return true;
        }
    }

    private void SelectMigrationRelative(int direction,
        Func<WDMapMigrationFitResult, bool> predicate)
    {
        if (migrationAnalysis == null || migrationAnalysis.Total == 0) return;
        for (int step = 1; step <= map.Territorios.Count; step++)
        {
            int index = (selectedTerritory + direction * step) % map.Territorios.Count;
            if (index < 0) index += map.Territorios.Count;
            string id = map.Territorios[index].id;
            WDMapMigrationFitResult result = migrationAnalysis.Results.FirstOrDefault(item =>
                item.TerritoryId == id);
            if (result == null || !predicate(result)) continue;
            selectedTerritory = index;
            return;
        }
    }

    private static string DescribeTransform(WDMapMigrationTransform value) =>
        $"scale {value.Scale.x:0.0000}, {value.Scale.y:0.0000} | offset {value.OffsetPixels.x:0.0}, {value.OffsetPixels.y:0.0}px | rot {value.RotationDegrees:0.0}°";

    private static void DrawMigrationLegend()
    {
        EditorGUILayout.LabelField("LEGENDA", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("ANTIGO — IGNORAR PARA APROVAÇÃO (tracejado cinza)");
        EditorGUILayout.LabelField("WD NOVO — REFERÊNCIA OFICIAL (arte limpa)");
        EditorGUILayout.LabelField("AUTO-FIT — CLICÁVEL PROPOSTO (linha contínua magenta)");
    }

    private void FocusMigrationTerritory(WDMapMigrationFitResult result)
    {
        IReadOnlyList<Vector2> boundary = WarDominionMapMigrationFitter.GetPreviewBoundary(
            result.TerritoryId, result.CandidateTransform);
        if (boundary.Count == 0 || lastPreviewViewport.width <= 0f) return;
        Vector2 center = boundary.Aggregate(Vector2.zero, (sum, point) => sum + point) /
            boundary.Count;
        previewZoom = 3f;
        float fit = Mathf.Min(lastPreviewViewport.width / migrationCandidate.rect.width,
            lastPreviewViewport.height / migrationCandidate.rect.height);
        Vector2 displayedSize = migrationCandidate.rect.size * fit * previewZoom;
        Vector2 normalized = new Vector2(center.x / migrationCandidate.rect.width,
            center.y / migrationCandidate.rect.height);
        previewPan = -new Vector2((normalized.x - 0.5f) * displayedSize.x,
            (normalized.y - 0.5f) * displayedSize.y);
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
                if (tab == Tab.Migracao16x9)
                {
                    GUILayout.Label("WD NOVO + território selecionado",
                        EditorStyles.miniLabel);
                }
                else
                {
                    showArt = GUILayout.Toggle(showArt, "Arte", EditorStyles.toolbarButton);
                    showGeometry = GUILayout.Toggle(showGeometry, "Geometria", EditorStyles.toolbarButton);
                    showRegions = GUILayout.Toggle(showRegions, "Regiões", EditorStyles.toolbarButton);
                    showConnections = GUILayout.Toggle(showConnections, "Conexões", EditorStyles.toolbarButton);
                    showCounters = GUILayout.Toggle(showCounters, "Contadores", EditorStyles.toolbarButton);
                    showOverlay = GUILayout.Toggle(showOverlay, "Overlay", EditorStyles.toolbarButton);
                }
                if (GUILayout.Button("Ajustar", EditorStyles.toolbarButton)) { previewZoom = 1f; previewPan = Vector2.zero; }
            }
            Rect viewport = GUILayoutUtility.GetRect(400f, 4000f, 400f, 4000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            lastPreviewViewport = viewport;
            EditorGUI.DrawRect(viewport, new Color(0.08f, 0.09f, 0.11f));
            HandlePreviewInput(viewport);
            DrawMapPreview(viewport);
        }
    }

    private void DrawMapPreview(Rect viewport)
    {
        if (tab == Tab.Migracao16x9)
        {
            DrawMigrationPreview(viewport);
            return;
        }
        EnsureActivePreviewContext();
        Sprite art = activePreviewContext?.Art;
        if (art == null) return;
        float fit = Mathf.Min(viewport.width / art.rect.width, viewport.height / art.rect.height);
        Vector2 size = art.rect.size * fit * previewZoom;
        Rect artRect = new Rect(viewport.center - size * 0.5f + previewPan, size);
        if (showArt) GUI.DrawTextureWithTexCoords(artRect, art.texture,
            new Rect(art.rect.x / art.texture.width, art.rect.y / art.texture.height,
                art.rect.width / art.texture.width, art.rect.height / art.texture.height), true);
        if (migrationPreviewEnabled && migrationCandidate != null)
        {
            Vector2 overlaySize = Vector2.Scale(artRect.size, migrationOverlayScale);
            Rect overlayRect = new Rect(
                artRect.center - overlaySize * 0.5f + migrationOverlayOffset,
                overlaySize);
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, migrationOverlayOpacity);
            GUI.DrawTextureWithTexCoords(overlayRect, migrationCandidate.texture,
                new Rect(migrationCandidate.rect.x / migrationCandidate.texture.width,
                    migrationCandidate.rect.y / migrationCandidate.texture.height,
                    migrationCandidate.rect.width / migrationCandidate.texture.width,
                    migrationCandidate.rect.height / migrationCandidate.texture.height), true);
            GUI.color = previous;
        }
        var projection = new WDMapViewportTransform(artRect,
            activePreviewContext.ArtBounds);
        if (showRegions)
            DrawRegionLabels(projection);
        if (tab == Tab.Nomes)
            DrawTerritoryNamesPreview(projection);
        if (showCounters || showConnections || tab == Tab.Regioes ||
            tab == Tab.Nomes)
        {
            var projectedPositions = new Vector2[map.Territorios.Count];
            for (int index = 0; index < projectedPositions.Length; index++)
                projectedPositions[index] = projection.MapToViewport(
                    activePreviewContext.ToArtLocal(map.Territorios[index].id,
                        map.Territorios[index].posicaoContador));
            Func<int, Vector2> convert = index => projectedPositions[index];
            HandleTerritoryMarkers(convert, projection);
            DrawRegionTerritoryHighlight(convert);
            if (showConnections)
            {
                Handles.BeginGUI(); Handles.color = new Color(0.2f, 0.85f, 1f, 0.65f);
                foreach (DefinicaoConexaoMapa connection in map.Conexoes)
                {
                    if (activePreviewContext.TryGetTerritoryIndex(
                            connection.origemId, out int ai) &&
                        activePreviewContext.TryGetTerritoryIndex(
                            connection.destinoId, out int bi))
                        Handles.DrawAAPolyLine(2f, convert(ai), convert(bi));
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

    private void DrawMigrationPreview(Rect viewport)
    {
        Sprite art = migrationCandidate;
        if (art == null)
        {
            GUI.Label(new Rect(viewport.center.x - 180f, viewport.center.y - 12f,
                360f, 24f), "Arte WD 16:9 indisponível.",
                EditorStyles.centeredGreyMiniLabel);
            return;
        }
        float fit = Mathf.Min(viewport.width / art.rect.width,
            viewport.height / art.rect.height);
        Vector2 size = art.rect.size * fit * previewZoom;
        Rect artRect = new Rect(viewport.center - size * 0.5f + previewPan, size);
        Texture previewTexture = migrationLightenTerritories &&
            migrationLightenedTexture != null ? migrationLightenedTexture : art.texture;
        Rect textureCoordinates = previewTexture == migrationLightenedTexture
            ? new Rect(0f, 0f, 1f, 1f)
            : new Rect(art.rect.x / art.texture.width, art.rect.y / art.texture.height,
                art.rect.width / art.texture.width, art.rect.height / art.texture.height);
        GUI.DrawTextureWithTexCoords(artRect, previewTexture, textureCoordinates, true);

        if (migrationBoundaryStore != null)
            DrawMaskAutoBoundaries(artRect);

        if (map.Territorios.Count > 0)
        {
            selectedTerritory = Mathf.Clamp(selectedTerritory, 0, map.Territorios.Count - 1);
            string id = map.Territorios[selectedTerritory].id;
            if (migrationShowOld)
                DrawMigrationBoundary(artRect, id,
                    new WDMapMigrationTransform(Vector2.one,
                        new Vector2((1920f - 1365f) * 0.5f,
                            (1080f - 1024f) * 0.5f)),
                    new Color(0.75f, 0.78f, 0.82f, 0.9f), true,
                    "Centro antigo");

            if (migrationAnalysis != null)
            {
                WDMapMigrationFitResult result = migrationAnalysis.Results.FirstOrDefault(item =>
                    item.TerritoryId == id);
                if (result != null)
                {
                    if (migrationShowAllTerritories)
                        DrawMigrationOverviewBoundaries(artRect, id);
                    HandleMigrationProposalDrag(artRect, art.rect.size, id, result);
                    DrawMigrationBoundary(artRect, id, result.CandidateTransform,
                        new Color(1f, 0.12f, 0.72f, 0.98f), false, "Centro proposto");
                }
            }
        }

        Rect badge = new Rect(viewport.x + 8f, viewport.y + 8f, 300f, 24f);
        EditorGUI.DrawRect(badge, new Color(0.02f, 0.03f, 0.05f, 0.88f));
        GUI.Label(badge, migrationAnalysis == null
            ? "WD NOVO — aguardando propostas"
            : "WD NOVO — proposta do território atual",
            EditorStyles.whiteBoldLabel);
    }

    private void DrawMaskAutoBoundaries(Rect artRect)
    {
        if (map == null || map.Territorios.Count == 0) return;
        string selectedId = map.Territorios[Mathf.Clamp(selectedTerritory, 0,
            map.Territorios.Count - 1)].id;
        IEnumerable<WDMapMigrationBoundaryRecord> records =
            migrationBoundaryStore.ForMap(map.MapaId);
        if (!migrationShowAllTerritories)
            records = records.Where(item => item.territoryId == selectedId);
        Handles.BeginGUI();
        foreach (WDMapMigrationBoundaryRecord record in records)
        {
            Handles.color = record.territoryId == selectedId
                ? new Color(1f, 0.05f, 0.8f, 1f)
                : GetMaskBoundaryColor(record.confidence);
            foreach (WDMapMigrationBoundaryPath path in record.paths)
            {
                if (path.points == null || path.points.Count < 2) continue;
                Vector3[] points = path.points.Select(point =>
                    (Vector3)MigrationPixelToViewport(artRect, point)).ToArray();
                Handles.DrawAAPolyLine(record.territoryId == selectedId ? 4f : 1.5f,
                    points);
            }
        }
        Handles.EndGUI();
    }

    private static Color GetMaskBoundaryColor(WDMapBoundaryConfidence confidence)
    {
        switch (confidence)
        {
            case WDMapBoundaryConfidence.High:
                return new Color(0.15f, 1f, 0.3f, 0.9f);
            case WDMapBoundaryConfidence.Medium:
                return new Color(0.1f, 0.9f, 1f, 0.9f);
            case WDMapBoundaryConfidence.Low:
                return new Color(1f, 0.58f, 0.08f, 0.95f);
            default:
                return new Color(1f, 0.12f, 0.12f, 1f);
        }
    }

    private void DrawMigrationOverviewBoundaries(Rect artRect, string selectedId)
    {
        foreach (WDMapMigrationFitResult item in migrationAnalysis.Results)
        {
            if (item.TerritoryId == selectedId) continue;
            Color color = GetMigrationOverviewColor(item);
            DrawMigrationOutlineOnly(artRect, item.TerritoryId,
                item.CandidateTransform, color, 1.5f);
        }

        Rect legend = new Rect(artRect.x + 8f, artRect.yMax - 24f, 430f, 18f);
        GUI.Label(legend,
            "Selecionado: magenta  •  Salvo: verde  •  OK: ciano  •  Revisar: laranja  •  Pendente: cinza",
            EditorStyles.whiteMiniLabel);
    }

    private Color GetMigrationOverviewColor(WDMapMigrationFitResult result)
    {
        if (TryGetMatchingSavedCalibration(result, out _))
            return new Color(0.2f, 0.95f, 0.35f, 0.9f);
        if (result.Accepted)
            return new Color(0.1f, 0.85f, 1f, 0.9f);
        if (migrationManualReview.Contains(result.TerritoryId))
            return new Color(1f, 0.62f, 0.08f, 0.9f);
        return new Color(0.75f, 0.55f, 0.72f, 0.62f);
    }

    private void DrawMigrationOutlineOnly(Rect artRect, string territoryId,
        WDMapMigrationTransform transform, Color color, float width)
    {
        if (!migrationBoundaryCache.TryGetValue(territoryId,
                out IReadOnlyList<Vector2> boundary) || boundary.Count < 2) return;
        if (migrationOutlineBuffer.Length != boundary.Count)
            migrationOutlineBuffer = new Vector3[boundary.Count];
        for (int index = 0; index < boundary.Count; index++)
            migrationOutlineBuffer[index] = MigrationPixelToViewport(artRect,
                transform.TransformPoint(boundary[index]));
        Handles.BeginGUI();
        Handles.color = color;
        Handles.DrawAAPolyLine(width, migrationOutlineBuffer);
        Handles.EndGUI();
    }

    private void HandleMigrationProposalDrag(Rect artRect, Vector2 canvasSize,
        string territoryId, WDMapMigrationFitResult result)
    {
        Event current = Event.current;
        if (current.rawType == EventType.MouseUp && migrationDraggingProposal)
        {
            migrationDraggingProposal = false;
            migrationDraggedTerritoryId = null;
            Repaint();
            return;
        }

        if (!migrationBoundaryCache.TryGetValue(territoryId,
                out IReadOnlyList<Vector2> boundary)) return;
        Vector2 proposalPoint = MigrationViewportToPixel(artRect, current.mousePosition);
        bool hovered = artRect.Contains(current.mousePosition) &&
            WDMapMigrationDragMath.ContainsTransformedPoint(boundary,
                result.CandidateTransform, proposalPoint);
        if (hovered)
            EditorGUIUtility.AddCursorRect(new Rect(current.mousePosition - Vector2.one * 2f,
                Vector2.one * 4f), MouseCursor.MoveArrow);

        if (current.type == EventType.MouseDown && current.button == 0 && hovered)
        {
            migrationDraggingProposal = true;
            migrationFineDrag = current.shift;
            migrationDraggedTerritoryId = territoryId;
            migrationDragStartMouse = current.mousePosition;
            migrationDragStartOffset = result.CandidateTransform.OffsetPixels;
            current.Use();
            return;
        }

        if (current.type != EventType.MouseDrag || current.button != 0 ||
            !migrationDraggingProposal || migrationDraggedTerritoryId != territoryId)
            return;
        Vector2 screenDelta = current.mousePosition - migrationDragStartMouse;
        Vector2 proposalDelta = WDMapMigrationDragMath.ScreenDeltaToProposalDelta(
            screenDelta, artRect.size, canvasSize, migrationFineDrag ? 0.25f : 1f);
        result.AdjustCandidate(WDMapMigrationDragMath.TranslateOffset(
            result.CandidateTransform, migrationDragStartOffset + proposalDelta));
        migrationManualReview.Remove(territoryId);
        current.Use();
        Repaint();
    }

    private void DrawMigrationBoundary(Rect artRect, string territoryId,
        WDMapMigrationTransform transform, Color color, bool dotted, string centerLabel)
    {
        if (!migrationBoundaryCache.TryGetValue(territoryId,
                out IReadOnlyList<Vector2> sourceBoundary) || sourceBoundary.Count < 2)
            return;
        int uniqueCount = sourceBoundary.Count - 1;
        if (migrationOutlineBuffer.Length != sourceBoundary.Count)
            migrationOutlineBuffer = new Vector3[sourceBoundary.Count];
        Vector2 center = Vector2.zero;
        for (int index = 0; index < sourceBoundary.Count; index++)
        {
            Vector2 transformed = transform.TransformPoint(sourceBoundary[index]);
            center += transformed;
            migrationOutlineBuffer[index] = MigrationPixelToViewport(artRect, transformed);
        }

        center /= sourceBoundary.Count;
        Handles.BeginGUI();
        if (!dotted && migrationTriangleCache.TryGetValue(territoryId,
                out int[] triangles))
        {
            Handles.color = new Color(color.r, color.g, color.b, 0.12f);
            for (int index = 0; index < triangles.Length; index += 3)
            {
                migrationTriangleBuffer[0] = migrationOutlineBuffer[triangles[index]];
                migrationTriangleBuffer[1] = migrationOutlineBuffer[triangles[index + 1]];
                migrationTriangleBuffer[2] = migrationOutlineBuffer[triangles[index + 2]];
                Handles.DrawAAConvexPolygon(migrationTriangleBuffer);
            }
            Handles.color = color;
            Handles.DrawAAPolyLine(4f, migrationOutlineBuffer);
        }
        else
        {
            Handles.color = color;
            for (int index = 0; index < uniqueCount; index += 8)
            {
                int next = Mathf.Min(index + 4, uniqueCount);
                Handles.DrawAAPolyLine(2.5f, migrationOutlineBuffer[index],
                    migrationOutlineBuffer[next]);
            }
        }
        Handles.EndGUI();

        Vector2 centerViewport = MigrationPixelToViewport(artRect, center);
        Rect label = new Rect(centerViewport.x - 55f, centerViewport.y - 10f, 110f, 20f);
        EditorGUI.DrawRect(label, new Color(color.r, color.g, color.b, 0.82f));
        GUI.Label(label, centerLabel, EditorStyles.centeredGreyMiniLabel);
    }

    private static int[] TriangulateClosedPolygon(IReadOnlyList<Vector2> closed)
    {
        int count = closed == null ? 0 : closed.Count - 1;
        if (count < 3) return Array.Empty<int>();
        var remaining = Enumerable.Range(0, count).ToList();
        var triangles = new List<int>((count - 2) * 3);
        bool clockwise = SignedArea(closed, count) < 0f;
        int safety = count * count;
        while (remaining.Count > 2 && safety-- > 0)
        {
            bool clipped = false;
            for (int index = 0; index < remaining.Count; index++)
            {
                int previous = remaining[(index - 1 + remaining.Count) % remaining.Count];
                int current = remaining[index];
                int next = remaining[(index + 1) % remaining.Count];
                float cross = Cross(closed[previous], closed[current], closed[next]);
                if (clockwise ? cross >= 0f : cross <= 0f) continue;
                bool contains = remaining.Any(candidate => candidate != previous &&
                    candidate != current && candidate != next && PointInTriangle(
                        closed[candidate], closed[previous], closed[current], closed[next]));
                if (contains) continue;
                triangles.Add(previous);
                triangles.Add(current);
                triangles.Add(next);
                remaining.RemoveAt(index);
                clipped = true;
                break;
            }
            if (!clipped) break;
        }
        return triangles.ToArray();
    }

    private static float SignedArea(IReadOnlyList<Vector2> polygon, int count)
    {
        float twiceArea = 0f;
        for (int index = 0; index < count; index++)
        {
            Vector2 first = polygon[index];
            Vector2 second = polygon[(index + 1) % count];
            twiceArea += first.x * second.y - second.x * first.y;
        }
        return twiceArea * 0.5f;
    }

    private static float Cross(Vector2 first, Vector2 second, Vector2 third) =>
        (second.x - first.x) * (third.y - first.y) -
        (second.y - first.y) * (third.x - first.x);

    private static bool PointInTriangle(Vector2 point, Vector2 first, Vector2 second,
        Vector2 third)
    {
        float firstCross = Cross(first, second, point);
        float secondCross = Cross(second, third, point);
        float thirdCross = Cross(third, first, point);
        bool hasNegative = firstCross < 0f || secondCross < 0f || thirdCross < 0f;
        bool hasPositive = firstCross > 0f || secondCross > 0f || thirdCross > 0f;
        return !(hasNegative && hasPositive);
    }

    private void DrawMigrationConnections(Rect artRect)
    {
        if (migrationAnalysis == null) return;
        Handles.BeginGUI();
        Handles.color = new Color(0.15f, 0.9f, 1f, 0.82f);
        string selectedId = map.Territorios.Count == 0 ? string.Empty :
            map.Territorios[Mathf.Clamp(selectedTerritory, 0,
                map.Territorios.Count - 1)].id;
        foreach (DefinicaoConexaoMapa connection in map.Conexoes)
        {
            WDMapMigrationFitResult first = migrationAnalysis.Results.FirstOrDefault(item =>
                item.TerritoryId == connection.origemId);
            WDMapMigrationFitResult second = migrationAnalysis.Results.FirstOrDefault(item =>
                item.TerritoryId == connection.destinoId);
            if (first == null || second == null) continue;
            Vector2 a = GetMigrationCenter(first.TerritoryId, first.CandidateTransform);
            Vector2 b = GetMigrationCenter(second.TerritoryId, second.CandidateTransform);
            bool selected = connection.origemId == selectedId ||
                connection.destinoId == selectedId;
            Handles.color = selected ? new Color(1f, 0.82f, 0.15f, 0.98f) :
                new Color(0.15f, 0.9f, 1f, 0.62f);
            Handles.DrawAAPolyLine(selected ? 3f : 1.5f,
                MigrationPixelToViewport(artRect, a),
                MigrationPixelToViewport(artRect, b));
        }
        Handles.EndGUI();
    }

    private void DrawMigrationRegionLabels(Rect artRect)
    {
        if (migrationAnalysis == null) return;
        foreach (DefinicaoRegiaoMapa region in map.Regioes)
        {
            Vector2[] centers = region.territorioIds
                .Select(id => migrationAnalysis.Results.FirstOrDefault(item =>
                    item.TerritoryId == id))
                .Where(item => item != null)
                .Select(item => GetMigrationCenter(item.TerritoryId,
                    item.CandidateTransform))
                .Where(point => point != Vector2.zero)
                .ToArray();
            if (centers.Length == 0) continue;
            Vector2 center = centers.Aggregate(Vector2.zero,
                (sum, point) => sum + point) / centers.Length;
            Vector2 point = MigrationPixelToViewport(artRect, center);
            string content = WDMatchInformationalOverlayController.DescribeRegionInline(region);
            Vector2 size = new Vector2(Mathf.Max(110f, content.Length * 7f + 18f), 24f);
            Rect badge = new Rect(point - size * 0.5f, size);
            EditorGUI.DrawRect(badge, new Color(0.03f, 0.05f, 0.08f, 0.9f));
            GUI.Label(badge, content, EditorStyles.centeredGreyMiniLabel);
        }
    }

    private static Vector2 GetMigrationCenter(string territoryId,
        WDMapMigrationTransform transform)
    {
        IReadOnlyList<Vector2> boundary = WarDominionMapMigrationFitter.GetPreviewBoundary(
            territoryId, transform, 240);
        return boundary.Count == 0 ? Vector2.zero :
            boundary.Aggregate(Vector2.zero, (sum, point) => sum + point) / boundary.Count;
    }

    private static Vector2 MigrationPixelToViewport(Rect artRect, Vector2 point) =>
        new Vector2(artRect.xMin + point.x / 1920f * artRect.width,
            artRect.yMin + point.y / 1080f * artRect.height);

    private static Vector2 MigrationViewportToPixel(Rect artRect, Vector2 point) =>
        new Vector2((point.x - artRect.xMin) / artRect.width * 1920f,
            (point.y - artRect.yMin) / artRect.height * 1080f);

    private void DrawMigrationViewportLegend(Rect viewport)
    {
        string mode = migrationViewMode == MigrationViewMode.WdNovo ? "WD NOVO — ARTE OFICIAL"
            : migrationViewMode == MigrationViewMode.Antigo ? "ANTIGO — REFERÊNCIA HISTÓRICA"
            : migrationViewMode == MigrationViewMode.AutoFit ? "WD NOVO + AUTO-FIT — MODO DE APROVAÇÃO"
            : "SOBREPOSTO — DIAGNÓSTICO";
        Rect badge = new Rect(viewport.x + 8f, viewport.y + 8f, 330f, 24f);
        EditorGUI.DrawRect(badge, new Color(0.02f, 0.03f, 0.05f, 0.88f));
        GUI.Label(badge, mode, EditorStyles.whiteBoldLabel);
    }

    private static Sprite LoadSpriteSubAsset(string path) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();

    private void ScheduleMigrationLightenedTextureRebuild()
    {
        EditorApplication.delayCall -= RebuildMigrationLightenedTexture;
        EditorApplication.delayCall += RebuildMigrationLightenedTexture;
    }

    private void RebuildMigrationLightenedTexture()
    {
        EditorApplication.delayCall -= RebuildMigrationLightenedTexture;
        DestroyMigrationLightenedTexture();
        if (!migrationLightenTerritories || migrationCandidate == null) return;
        string path = AssetDatabase.GetAssetPath(migrationCandidate);
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            name = "WD Migration Lightened Preview",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear
        };
        if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false))
        {
            DestroyImmediate(texture);
            return;
        }
        Color32[] pixels = texture.GetPixels32();
        for (int index = 0; index < pixels.Length; index++)
            pixels[index] = WDMapMigrationPreviewLightening.LightenPixel(pixels[index],
                migrationLightening);
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        migrationLightenedTexture = texture;
        Repaint();
    }

    private void DestroyMigrationLightenedTexture()
    {
        if (migrationLightenedTexture == null) return;
        DestroyImmediate(migrationLightenedTexture);
        migrationLightenedTexture = null;
    }

    private void DrawRegionLabels(WDMapViewportTransform projection)
    {
        if (map.Regioes.Count == 0) return;
        Event current = Event.current;
        for (int index = 0; index < map.Regioes.Count; index++)
        {
            DefinicaoRegiaoMapa region = map.Regioes[index];
            Vector3[] positions = region.territorioIds
                .Select(id => map.Territorios.FirstOrDefault(item => item.id == id))
                .Where(item => item != null)
                .Select(item => (Vector3)activePreviewContext.ToArtLocal(
                    item.id, item.posicaoContador))
                .ToArray();
            if (!WDMatchInformationalOverlayController.TryCalculateRegionPosition(
                positions, region.possuiDeslocamentoLabelManual,
                region.deslocamentoLabelManual, out Vector3 effectivePosition)) continue;
            WDMatchInformationalOverlayController.TryCalculateRegionAnchor(
                positions, out Vector3 automatic);
            Vector2 effective = effectivePosition;
            Vector2 point = projection.MapToViewport(effective);
            string content = WDMatchInformationalOverlayController.DescribeRegionInline(region);
            Vector2 size = new Vector2(Mathf.Max(104f, content.Length * 7f + 18f), 24f);
            Rect badge = new Rect(point - size * 0.5f, size);
            EditorGUI.DrawRect(badge, index == selectedRegion
                ? new Color(0.12f, 0.72f, 0.95f, 0.92f)
                : new Color(0.04f, 0.05f, 0.07f, 0.82f));
            GUI.Label(badge, content, EditorStyles.centeredGreyMiniLabel);

            if (tab != Tab.Regioes || index != selectedRegion) continue;
            if (current.type == EventType.MouseDown && current.button == 0 && badge.Contains(current.mousePosition))
            {
                Undo.RecordObject(map, "Mover label da região");
                draggingRegionLabel = true;
                current.Use();
            }
            else if (current.type == EventType.MouseDrag && current.button == 0 && draggingRegionLabel)
            {
                Vector2 dragged = projection.ViewportToMap(current.mousePosition);
                region.possuiDeslocamentoLabelManual = true;
                region.deslocamentoLabelManual = dragged - (Vector2)automatic;
                EditorUtility.SetDirty(map);
                current.Use();
                Repaint();
            }
        }
        if (current.rawType == EventType.MouseUp)
            draggingRegionLabel = false;
    }

    private void HandlePreviewInput(Rect viewport)
    {
        Event e = Event.current;
        if (!viewport.Contains(e.mousePosition)) return;
        bool migrationPreview = tab == Tab.Migracao16x9;
        if (e.type == EventType.ScrollWheel && (!migrationPreview || e.control || e.command))
        {
            float previousZoom = previewZoom;
            float minimumZoom = migrationPreview ? 0.75f : 0.2f;
            float maximumZoom = migrationPreview ? 4f : 8f;
            previewZoom = Mathf.Clamp(previewZoom * (1f - e.delta.y * 0.06f),
                minimumZoom, maximumZoom);
            if (migrationPreview && !Mathf.Approximately(previousZoom, previewZoom))
            {
                Vector2 cursorFromCenter = e.mousePosition - viewport.center;
                previewPan = cursorFromCenter -
                    (cursorFromCenter - previewPan) * (previewZoom / previousZoom);
            }
            e.Use(); Repaint();
        }
        else if (e.type == EventType.MouseDrag &&
            (e.button == 2 || (!migrationPreview && e.alt)))
        {
            previewPan += e.delta; e.Use(); Repaint();
        }
    }

    private void HandleTerritoryMarkers(Func<int, Vector2> convert,
        WDMapViewportTransform projection)
    {
        Event e = Event.current;
        if (map.Territorios.Count == 0) return;
        int nearest = (tab == Tab.Regioes || tab == Tab.Nomes) &&
            activePreviewContext.TryHitTerritory(projection.ViewportToMap(e.mousePosition),
                out int hitIndex) ? hitIndex : -1;
        float nearestDistance = 14f;
        for (int i = 0; nearest < 0 && i < map.Territorios.Count; i++)
        {
            float distance = Vector2.Distance(e.mousePosition,
                convert(i));
            if (distance < nearestDistance) { nearest = i; nearestDistance = distance; }
        }
        if (e.type == EventType.MouseDown && e.button == 0 && nearest >= 0)
        {
            territorySelection.Click(nearest, e.control || e.command);
            selectedTerritory = nearest;
            if (tab == Tab.Regioes)
                SelectRegionTerritory(map.Territorios[nearest].id, false);
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
            position.vector2Value = activePreviewContext.ToStoredPosition(
                map.Territorios[selectedTerritory].id, artLocal);
            serializedMap.ApplyModifiedProperties();
            EditorUtility.SetDirty(map);
            e.Use(); Repaint();
        }
        else if (e.rawType == EventType.MouseUp)
            draggingCounter = false;
    }

    private void DrawTerritoryNamesPreview(WDMapViewportTransform projection)
    {
        if (map.Territorios.Count == 0) return;
        Event current = Event.current;
        for (int index = 0; index < map.Territorios.Count; index++)
        {
            DefinicaoTerritorioMapa territory = map.Territorios[index];
            if (territory == null) continue;
            Vector2 artLocal = ResolveTerritoryNameArtLocal(territory);
            Vector2 point = projection.MapToViewport(artLocal);
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = territory.tamanhoFonteNome > 0
                    ? Mathf.Clamp(territory.tamanhoFonteNome,
                        NomesTerritoriosSceneView.TamanhoFonteMinimo,
                        NomesTerritoriosSceneView.TamanhoFonteMaximo)
                    : 12
            };
            style.normal.textColor = index == selectedTerritory
                ? new Color(1f, 0.86f, 0.12f)
                : Color.white;
            string display = string.IsNullOrWhiteSpace(territory.nomeExibido)
                ? territory.id : territory.nomeExibido;
            Vector2 measured = style.CalcSize(new GUIContent(display));
            Rect labelRect = new Rect(point.x - measured.x * 0.5f - 5f,
                point.y - measured.y * 0.5f - 3f, measured.x + 10f,
                measured.y + 6f);
            if (index == selectedTerritory)
                EditorGUI.DrawRect(labelRect,
                    new Color(0.05f, 0.12f, 0.18f, 0.82f));
            GUI.Label(labelRect, display, style);

            if (current.type == EventType.MouseDown && current.button == 0 &&
                labelRect.Contains(current.mousePosition))
            {
                selectedTerritory = index;
                territorySelection.Click(index, false);
                draggingTerritoryName = true;
                Undo.RecordObject(map, "Mover nome de " + territory.id);
                current.Use();
                Repaint();
            }
        }

        if (draggingTerritoryName && current.type == EventType.MouseDrag &&
            current.button == 0)
        {
            DefinicaoTerritorioMapa selected = map.Territorios[
                Mathf.Clamp(selectedTerritory, 0, map.Territorios.Count - 1)];
            Vector2 artLocal = projection.ViewportToMap(current.mousePosition);
            selected.posicaoNomeManual = activePreviewContext.ToStoredPosition(
                selected.id, artLocal);
            selected.possuiPosicaoNomeManual = true;
            EditorUtility.SetDirty(map);
            current.Use();
            Repaint();
        }
        else if (draggingTerritoryName && current.rawType == EventType.MouseUp)
        {
            draggingTerritoryName = false;
            AssetDatabase.SaveAssetIfDirty(map);
        }

        if (selectedTerritory >= 0 && selectedTerritory < map.Territorios.Count)
            DrawTerritoryOutline(map.Territorios[selectedTerritory].id,
                new Color(1f, 0.86f, 0.08f, 1f), 4f);
    }

    private Vector2 ResolveTerritoryNameArtLocal(
        DefinicaoTerritorioMapa territory)
    {
        if (territory.possuiPosicaoNomeManual)
            return activePreviewContext.ToArtLocal(territory.id,
                territory.posicaoNomeManual);
        if (!activePreviewContext.TryGetTerritoryPolygons(territory.id,
                out IReadOnlyList<IReadOnlyList<Vector2>> polygons))
            return activePreviewContext.ToArtLocal(territory.id,
                territory.posicaoContador);
        bool initialized = false;
        Bounds bounds = default;
        foreach (IReadOnlyList<Vector2> polygon in polygons)
            foreach (Vector2 point in polygon)
                if (!initialized)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    initialized = true;
                }
                else
                    bounds.Encapsulate(point);
        return initialized ? (Vector2)bounds.center : Vector2.zero;
    }

    internal static int ResolveTerritoryIndex(DefinicaoMapa definition,
        string territoryId)
    {
        if (definition == null || string.IsNullOrWhiteSpace(territoryId))
            return -1;
        for (int index = 0; index < definition.Territorios.Count; index++)
            if (string.Equals(definition.Territorios[index]?.id, territoryId,
                    StringComparison.Ordinal))
                return index;
        return -1;
    }

    private void DrawRegionTerritoryHighlight(Func<int, Vector2> convert)
    {
        if (tab != Tab.Regioes)
            return;
        DefinicaoRegiaoMapa selected = map.Regioes.Count > 0
            ? map.Regioes[Mathf.Clamp(selectedRegion, 0, map.Regioes.Count - 1)] : null;
        if (selected != null)
            foreach (string memberId in selected.territorioIds)
                DrawTerritoryOutline(memberId, new Color(0.08f, 0.82f, 1f, 0.58f), 2f);
        if (string.IsNullOrWhiteSpace(highlightedRegionTerritoryId)) return;
        int index = ResolveTerritoryIndex(map, highlightedRegionTerritoryId);
        if (index < 0)
            return;
        Vector2 point = convert(index);
        Color previous = Handles.color;
        Handles.BeginGUI();
        Handles.color = new Color(1f, 0.86f, 0.08f, 0.95f);
        Handles.DrawWireDisc(point, Vector3.forward, 18f, 3f);
        Handles.DrawAAPolyLine(3f, point + Vector2.left * 25f,
            point + Vector2.right * 25f);
        Handles.DrawAAPolyLine(3f, point + Vector2.down * 25f,
            point + Vector2.up * 25f);
        Handles.EndGUI();
        Handles.color = previous;
        DrawTerritoryOutline(highlightedRegionTerritoryId,
            new Color(1f, 0.86f, 0.08f, 1f), 4f);
    }

    private void DrawTerritoryOutline(string territoryId, Color color, float width)
    {
        if (!activePreviewContext.TryGetTerritoryPolygons(territoryId,
                out IReadOnlyList<IReadOnlyList<Vector2>> polygons)) return;
        Sprite art = activePreviewContext.Art;
        float fit = Mathf.Min(lastPreviewViewport.width / art.rect.width,
            lastPreviewViewport.height / art.rect.height);
        Rect artRect = new Rect(lastPreviewViewport.center -
            art.rect.size * fit * previewZoom * 0.5f + previewPan,
            art.rect.size * fit * previewZoom);
        var projection = new WDMapViewportTransform(artRect, activePreviewContext.ArtBounds);
        Handles.BeginGUI();
        Handles.color = color;
        foreach (IReadOnlyList<Vector2> polygon in polygons)
        {
            if (polygon == null || polygon.Count < 2) continue;
            Vector3[] points = polygon.Select(point =>
                (Vector3)projection.MapToViewport(point)).ToArray();
            if (points[0] != points[points.Length - 1])
                points = points.Concat(new[] { points[0] }).ToArray();
            Handles.DrawAAPolyLine(width, points);
        }
        Handles.EndGUI();
    }

    private void FocusTerritoryInPreview(int territoryIndex)
    {
        if (lastPreviewViewport.width <= 0f || activePreviewContext?.Art == null ||
            territoryIndex < 0 || territoryIndex >= map.Territorios.Count) return;
        DefinicaoTerritorioMapa territory = map.Territorios[territoryIndex];
        Sprite art = activePreviewContext.Art;
        float fit = Mathf.Min(lastPreviewViewport.width / art.rect.width,
            lastPreviewViewport.height / art.rect.height);
        Rect currentArt = new Rect(lastPreviewViewport.center -
            art.rect.size * fit * previewZoom * 0.5f + previewPan,
            art.rect.size * fit * previewZoom);
        var projection = new WDMapViewportTransform(currentArt,
            activePreviewContext.ArtBounds);
        Vector2 point = projection.MapToViewport(activePreviewContext.ToArtLocal(
            territory.id, territory.posicaoContador));
        Rect safe = new Rect(lastPreviewViewport.x + 30f,
            lastPreviewViewport.y + 30f, lastPreviewViewport.width - 60f,
            lastPreviewViewport.height - 60f);
        if (!safe.Contains(point)) previewPan += lastPreviewViewport.center - point;
    }

    // ============================================================
    // 04. CRIAÇÃO SEGURA E UTILITÁRIOS
    // ============================================================

    private void NewMap()
    {
        WarDominionNewMapWindow.Open(created => SelectMap(created));
    }

    private void EnsureActivePreviewContext()
    {
        if (map == null)
        {
            activePreviewContext = null;
            spatialSnapshot = new WDMapSpatialSnapshot();
            return;
        }
        if (activePreviewContext != null && activePreviewContext.Matches(map))
            return;
        rebuildingPreviewContext = true;
        try
        {
            activePreviewContext = WDMapActivePreviewContext.Resolve(map);
            spatialSnapshot = activePreviewContext.SpatialSnapshot;
        }
        finally
        {
            rebuildingPreviewContext = false;
        }
        if (migrationCandidate != activePreviewContext.Art)
        {
            DestroyMigrationLightenedTexture();
            migrationCandidate = activePreviewContext.Art;
            migrationAnalysis = null;
            migrationBoundaryCache.Clear();
            migrationTriangleCache.Clear();
            if (tab == Tab.Migracao16x9)
                ScheduleMigrationLightenedTextureRebuild();
        }
    }

    private void EnsureMigrationResources()
    {
        if (migrationCalibrationStore == null)
            migrationCalibrationStore = WDMapMigrationCalibrationRepository.Load();
        if (migrationBoundaryStore == null)
            migrationBoundaryStore = WDMapMigrationBoundaryRepository.Load();
        if (migrationLightenTerritories && migrationLightenedTexture == null)
            ScheduleMigrationLightenedTextureRebuild();
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

public static class WDMapMigrationDragMath
{
    public static Vector2 ScreenDeltaToProposalDelta(Vector2 screenDelta,
        Vector2 previewSize, Vector2 proposalCanvasSize, float sensitivity)
    {
        if (previewSize.x <= 0f || previewSize.y <= 0f) return Vector2.zero;
        return new Vector2(screenDelta.x / previewSize.x * proposalCanvasSize.x,
            screenDelta.y / previewSize.y * proposalCanvasSize.y) * sensitivity;
    }

    public static WDMapMigrationTransform TranslateOffset(
        WDMapMigrationTransform transform, Vector2 offset) =>
        new WDMapMigrationTransform(transform.Scale, offset,
            transform.RotationDegrees);

    public static bool ContainsTransformedPoint(IReadOnlyList<Vector2> closedBoundary,
        WDMapMigrationTransform transform, Vector2 point)
    {
        if (closedBoundary == null || closedBoundary.Count < 4) return false;
        bool inside = false;
        int uniqueCount = closedBoundary.Count - 1;
        Vector2 previous = transform.TransformPoint(closedBoundary[uniqueCount - 1]);
        for (int index = 0; index < uniqueCount; index++)
        {
            Vector2 current = transform.TransformPoint(closedBoundary[index]);
            if (PointOnSegment(point, previous, current)) return true;
            bool crosses = (current.y > point.y) != (previous.y > point.y) &&
                point.x < (previous.x - current.x) * (point.y - current.y) /
                (previous.y - current.y) + current.x;
            if (crosses) inside = !inside;
            previous = current;
        }
        return inside;
    }

    private static bool PointOnSegment(Vector2 point, Vector2 first, Vector2 second)
    {
        Vector2 segment = second - first;
        Vector2 relative = point - first;
        float cross = segment.x * relative.y - segment.y * relative.x;
        if (Mathf.Abs(cross) > 0.01f) return false;
        float dot = Vector2.Dot(relative, segment);
        return dot >= 0f && dot <= segment.sqrMagnitude;
    }
}

public static class WDMapMigrationPreviewLightening
{
    public static Color32 LightenPixel(Color32 pixel, float intensity)
    {
        if (pixel.a == 0 || intensity <= 0f) return pixel;
        float red = pixel.r / 255f;
        float green = pixel.g / 255f;
        float blue = pixel.b / 255f;
        float minimum = Mathf.Min(red, Mathf.Min(green, blue));
        float maximum = Mathf.Max(red, Mathf.Max(green, blue));
        float luminance = red * 0.2126f + green * 0.7152f + blue * 0.0722f;

        // A arte WD usa preenchimentos territoriais pretos e linhas internas
        // cinza um pouco mais claras. A curva precisa produzir contraste visível
        // já em 25%, preservando oceano, highlights e linhas.
        if (maximum - minimum > 0.04f || luminance > 0.035f) return pixel;
        byte lifted = (byte)Mathf.RoundToInt(Mathf.Lerp(luminance, 0.75f,
            Mathf.Clamp01(intensity)) * 255f);
        return new Color32(lifted, lifted, lifted, pixel.a);
    }
}

public readonly struct WDMapMigrationProgress
{
    public int Saved { get; }
    public int ApprovedUnsaved { get; }
    public int Review { get; }
    public int Pending { get; }

    private WDMapMigrationProgress(int saved, int approvedUnsaved, int review,
        int pending)
    {
        Saved = saved;
        ApprovedUnsaved = approvedUnsaved;
        Review = review;
        Pending = pending;
    }

    public static WDMapMigrationProgress Calculate(string mapId,
        IEnumerable<WDMapMigrationFitResult> results,
        WDMapMigrationCalibrationStore store, ISet<string> manualReview)
    {
        WDMapMigrationFitResult[] items = (results ??
            Array.Empty<WDMapMigrationFitResult>()).ToArray();
        int saved = items.Count(item =>
            WDMapMigrationCalibrationRepository.Matches(store, mapId, item));
        int approved = items.Count(item => item.Accepted &&
            !WDMapMigrationCalibrationRepository.Matches(store, mapId, item));
        int review = items.Count(item => !item.Accepted && manualReview != null &&
            manualReview.Contains(item.TerritoryId));
        return new WDMapMigrationProgress(saved, approved, review,
            Mathf.Max(0, items.Length - saved - approved - review));
    }
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
