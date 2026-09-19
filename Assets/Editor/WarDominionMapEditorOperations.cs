using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WarDominionMapEditorOperations
{
    public static bool SetTerritoryNamePosition(DefinicaoMapa map,
        string territoryId, Vector2 storedPosition)
    {
        DefinicaoTerritorioMapa territory = map?.Territorios.FirstOrDefault(item =>
            item != null && string.Equals(item.id, territoryId,
                StringComparison.Ordinal));
        if (territory == null || territory.possuiPosicaoNomeManual &&
            territory.posicaoNomeManual == storedPosition) return false;
        Undo.RecordObject(map, "Mover nome de " + territoryId);
        territory.posicaoNomeManual = storedPosition;
        territory.possuiPosicaoNomeManual = true;
        EditorUtility.SetDirty(map);
        return true;
    }

    public static bool ResetTerritoryNamePosition(DefinicaoMapa map,
        string territoryId)
    {
        DefinicaoTerritorioMapa territory = map?.Territorios.FirstOrDefault(item =>
            item != null && string.Equals(item.id, territoryId,
                StringComparison.Ordinal));
        if (territory == null || !territory.possuiPosicaoNomeManual) return false;
        Undo.RecordObject(map, "Restaurar posição automática de " + territoryId);
        territory.possuiPosicaoNomeManual = false;
        EditorUtility.SetDirty(map);
        return true;
    }

    public static int ResetAllTerritoryNamePositions(DefinicaoMapa map)
    {
        if (map == null) return 0;
        int count = map.Territorios.Count(item =>
            item != null && item.possuiPosicaoNomeManual);
        if (count == 0) return 0;
        Undo.RecordObject(map, "Restaurar todas as posições automáticas");
        foreach (DefinicaoTerritorioMapa territory in map.Territorios)
            if (territory != null)
                territory.possuiPosicaoNomeManual = false;
        EditorUtility.SetDirty(map);
        return count;
    }

    public enum RegionAssociationState
    {
        Consistent,
        Unassigned,
        TerritoryPointsToMissingRegion,
        RegionMissingTerritory,
        RegionContainsTerritoryWithDifferentOwner,
        DuplicatedAcrossRegions
    }

    public sealed class RegionAssociationDiagnostic
    {
        public RegionAssociationState State { get; }
        public DefinicaoTerritorioMapa Territory { get; }
        public DefinicaoRegiaoMapa DeclaredRegion { get; }
        public IReadOnlyList<DefinicaoRegiaoMapa> ContainingRegions { get; }
        public bool IsConsistent => State == RegionAssociationState.Consistent ||
            State == RegionAssociationState.Unassigned;

        public RegionAssociationDiagnostic(RegionAssociationState state,
            DefinicaoTerritorioMapa territory, DefinicaoRegiaoMapa declaredRegion,
            IReadOnlyList<DefinicaoRegiaoMapa> containingRegions)
        {
            State = state;
            Territory = territory;
            DeclaredRegion = declaredRegion;
            ContainingRegions = containingRegions ?? Array.Empty<DefinicaoRegiaoMapa>();
        }
    }

    // ============================================================
    // 00. ASSOCIAÇÃO REGIONAL ATÔMICA E DIAGNÓSTICO NÃO DESTRUTIVO
    // ============================================================

    public static IReadOnlyList<DefinicaoTerritorioMapa> SearchTerritories(
        DefinicaoMapa map, string query)
    {
        if (map == null) return Array.Empty<DefinicaoTerritorioMapa>();
        string term = query?.Trim() ?? string.Empty;
        return map.Territorios.Where(territory => territory != null &&
            (term.Length == 0 || (territory.id ?? string.Empty).IndexOf(term,
                 StringComparison.OrdinalIgnoreCase) >= 0 ||
             (territory.nomeExibido ?? string.Empty).IndexOf(term,
                 StringComparison.OrdinalIgnoreCase) >= 0))
            .OrderBy(territory => string.IsNullOrWhiteSpace(territory.regiaoId) ? 0 : 1)
            .ThenBy(territory => territory.nomeExibido ?? territory.id,
                StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static RegionAssociationDiagnostic DiagnoseRegionAssociation(
        DefinicaoMapa map, string territoryId)
    {
        DefinicaoTerritorioMapa territory = map?.Territorios.FirstOrDefault(item =>
            item != null && string.Equals(item.id, territoryId, StringComparison.Ordinal));
        if (territory == null)
            return new RegionAssociationDiagnostic(
                RegionAssociationState.TerritoryPointsToMissingRegion, null, null, null);
        DefinicaoRegiaoMapa declared = map.Regioes.FirstOrDefault(region => region != null &&
            string.Equals(region.id, territory.regiaoId, StringComparison.Ordinal));
        DefinicaoRegiaoMapa[] containing = map.Regioes.Where(region =>
            region?.territorioIds != null && region.territorioIds.Contains(territory.id))
            .ToArray();
        RegionAssociationState state;
        if (string.IsNullOrWhiteSpace(territory.regiaoId) && containing.Length == 0)
            state = RegionAssociationState.Unassigned;
        else if (!string.IsNullOrWhiteSpace(territory.regiaoId) && declared == null)
            state = RegionAssociationState.TerritoryPointsToMissingRegion;
        else if (containing.Length > 1 || map.Regioes.Any(region =>
                     region?.territorioIds != null && region.territorioIds.Count(id =>
                         string.Equals(id, territory.id, StringComparison.Ordinal)) > 1))
            state = RegionAssociationState.DuplicatedAcrossRegions;
        else if (declared != null && containing.Length == 0)
            state = RegionAssociationState.RegionMissingTerritory;
        else if (containing.Length == 1 && !ReferenceEquals(containing[0], declared))
            state = RegionAssociationState.RegionContainsTerritoryWithDifferentOwner;
        else
            state = RegionAssociationState.Consistent;
        return new RegionAssociationDiagnostic(state, territory, declared, containing);
    }

    public static bool MoveTerritoryToRegion(DefinicaoMapa map, string territoryId,
        string targetRegionId)
    {
        if (map == null || string.IsNullOrWhiteSpace(territoryId) ||
            string.IsNullOrWhiteSpace(targetRegionId)) return false;
        DefinicaoTerritorioMapa territory = map.Territorios.FirstOrDefault(item =>
            item != null && string.Equals(item.id, territoryId, StringComparison.Ordinal));
        DefinicaoRegiaoMapa target = map.Regioes.FirstOrDefault(region => region != null &&
            string.Equals(region.id, targetRegionId, StringComparison.Ordinal));
        if (territory == null || target == null) return false;
        RegionAssociationDiagnostic before = DiagnoseRegionAssociation(map, territoryId);
        if (before.State == RegionAssociationState.Consistent &&
            ReferenceEquals(before.DeclaredRegion, target)) return false;

        Undo.RecordObject(map, "Mover território para região");
        foreach (DefinicaoRegiaoMapa region in map.Regioes)
            if (region?.territorioIds != null)
                region.territorioIds.RemoveAll(id => string.Equals(id, territoryId,
                    StringComparison.Ordinal));
        target.territorioIds ??= new List<string>();
        target.territorioIds.Add(territoryId);
        territory.regiaoId = target.id;
        EditorUtility.SetDirty(map);
        return true;
    }

    public static bool RemoveTerritoryFromRegion(DefinicaoMapa map, string territoryId)
    {
        if (map == null || string.IsNullOrWhiteSpace(territoryId)) return false;
        DefinicaoTerritorioMapa territory = map.Territorios.FirstOrDefault(item =>
            item != null && string.Equals(item.id, territoryId, StringComparison.Ordinal));
        if (territory == null) return false;
        bool contained = map.Regioes.Any(region => region?.territorioIds != null &&
            region.territorioIds.Contains(territoryId));
        if (!contained && string.IsNullOrWhiteSpace(territory.regiaoId)) return false;

        Undo.RecordObject(map, "Remover território da região");
        foreach (DefinicaoRegiaoMapa region in map.Regioes)
            if (region?.territorioIds != null)
                region.territorioIds.RemoveAll(id => string.Equals(id, territoryId,
                    StringComparison.Ordinal));
        territory.regiaoId = string.Empty;
        EditorUtility.SetDirty(map);
        return true;
    }

    // ============================================================
    // 01. CONEXÕES BIDIRECIONAIS COMO UM ÚNICO REGISTRO AUTORITATIVO
    // ============================================================

    public static bool AddBidirectionalConnection(DefinicaoMapa map, string originId,
        string destinationId, TipoConexaoMapa type)
    {
        if (map == null || string.IsNullOrWhiteSpace(originId) ||
            string.IsNullOrWhiteSpace(destinationId) || originId == destinationId ||
            ContainsConnection(map, originId, destinationId))
            return false;

        SerializedObject serialized = new SerializedObject(map);
        SerializedProperty connections = serialized.FindProperty("conexoes");
        Undo.RecordObject(map, "Adicionar conexão do mapa");
        connections.InsertArrayElementAtIndex(connections.arraySize);
        SerializedProperty connection = connections.GetArrayElementAtIndex(connections.arraySize - 1);
        connection.FindPropertyRelative("origemId").stringValue = originId;
        connection.FindPropertyRelative("destinoId").stringValue = destinationId;
        connection.FindPropertyRelative("tipo").enumValueIndex = (int)type;
        connection.FindPropertyRelative("bidirecional").boolValue = true;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(map);
        return true;
    }

    public static bool RemoveConnection(DefinicaoMapa map, string firstId, string secondId)
    {
        if (map == null) return false;
        SerializedObject serialized = new SerializedObject(map);
        SerializedProperty connections = serialized.FindProperty("conexoes");
        for (int i = 0; i < connections.arraySize; i++)
        {
            SerializedProperty connection = connections.GetArrayElementAtIndex(i);
            string origin = connection.FindPropertyRelative("origemId").stringValue;
            string destination = connection.FindPropertyRelative("destinoId").stringValue;
            if (!SamePair(origin, destination, firstId, secondId)) continue;
            Undo.RecordObject(map, "Remover conexão do mapa");
            connections.DeleteArrayElementAtIndex(i);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(map);
            return true;
        }
        return false;
    }

    public static bool ContainsConnection(DefinicaoMapa map, string firstId, string secondId) =>
        map != null && map.Conexoes.Any(connection => connection != null &&
            SamePair(connection.origemId, connection.destinoId, firstId, secondId));

    private static bool SamePair(string a, string b, string x, string y) =>
        (a == x && b == y) || (a == y && b == x);

    // ============================================================
    // 02. VALIDAÇÃO NÃO DESTRUTIVA E RESUMO
    // ============================================================

    public static WDMapEditorValidationReport Validate(DefinicaoMapa map,
        IEnumerable<DefinicaoMapa> catalogMaps)
    {
        var issues = ValidadorDefinicaoMapa.Validar(map);
        if (map != null)
        {
            foreach (DefinicaoTerritorioMapa territory in map.Territorios)
                if (territory != null && string.IsNullOrWhiteSpace(territory.nomeExibido))
                    issues.Add(new ProblemaValidacaoMapa(SeveridadeProblemaMapa.Erro,
                        "TERRITORIO_SEM_NOME", $"{territory.id} não possui nome visível."));
            foreach (DefinicaoRegiaoMapa region in map.Regioes)
                if (region != null && region.bonus <= 0)
                    issues.Add(new ProblemaValidacaoMapa(SeveridadeProblemaMapa.Aviso,
                        "REGIAO_SEM_BONUS", $"{region.id} não possui bônus positivo configurado."));
            foreach (DefinicaoConexaoMapa connection in map.Conexoes)
                if (connection != null && !connection.bidirecional)
                    issues.Add(new ProblemaValidacaoMapa(SeveridadeProblemaMapa.Erro,
                        "CONEXAO_NAO_BIDIRECIONAL",
                        $"{connection.origemId} -> {connection.destinoId} não está marcada como bidirecional."));
        }
        if (map != null && catalogMaps != null && catalogMaps.Count(candidate =>
                candidate != null && candidate.MapaId == map.MapaId) > 1)
            issues.Add(new ProblemaValidacaoMapa(SeveridadeProblemaMapa.Erro,
                "MAPA_ID_DUPLICADO", $"O ID {map.MapaId} aparece mais de uma vez no catálogo."));
        return new WDMapEditorValidationReport(map, issues);
    }

    // ============================================================
    // 03. TROCA DE ARTE PRESERVANDO OS DADOS DO MAPA
    // ============================================================

    public static bool ReplaceMainArt(DefinicaoMapa map, Sprite newArt)
    {
        if (map == null || map.ArteBase == newArt) return false;
        SerializedObject serialized = new SerializedObject(map);
        Undo.RecordObject(map, "Substituir arte principal do mapa");
        serialized.FindProperty("arteBase").objectReferenceValue = newArt;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(map);
        return true;
    }

    public static bool SetRegionLabelOffset(DefinicaoMapa map,
        DefinicaoRegiaoMapa region, Vector2 offset)
    {
        if (map == null || region == null || !map.Regioes.Contains(region)) return false;
        Undo.RecordObject(map, "Mover label da região");
        region.possuiDeslocamentoLabelManual = true;
        region.deslocamentoLabelManual = offset;
        EditorUtility.SetDirty(map);
        return true;
    }

    public static bool ResetRegionLabelOffset(DefinicaoMapa map,
        DefinicaoRegiaoMapa region)
    {
        if (map == null || region == null || !map.Regioes.Contains(region)) return false;
        Undo.RecordObject(map, "Resetar label da região para automático");
        region.possuiDeslocamentoLabelManual = false;
        region.deslocamentoLabelManual = Vector2.zero;
        EditorUtility.SetDirty(map);
        return true;
    }

    public static void Save(DefinicaoMapa map)
    {
        if (map == null) return;
        EditorUtility.SetDirty(map);
        AssetDatabase.SaveAssets();
    }

    public static bool RegisterInCatalog(CatalogoMapas catalog, DefinicaoMapa map)
    {
        if (catalog == null || map == null || catalog.Mapas.Contains(map)) return false;
        Undo.RecordObject(catalog, "Registrar mapa no catálogo");
        var maps = catalog.Mapas.Where(item => item != null).ToList();
        maps.Add(map);
        catalog.ConfigurarNoEditor(catalog.MapaPadrao ?? map, maps);
        EditorUtility.SetDirty(catalog);
        return true;
    }
}

public sealed class WDMapEditorValidationReport
{
    public DefinicaoMapa Map { get; }
    public IReadOnlyList<ProblemaValidacaoMapa> Issues { get; }
    public int ErrorCount => Issues.Count(issue => issue.Severidade == SeveridadeProblemaMapa.Erro);
    public int WarningCount => Issues.Count - ErrorCount;
    public int ConnectionCount => Map?.Conexoes?.Count ?? 0;
    public int TerritoryCount => Map?.Territorios?.Count ?? 0;
    public int RegionCount => Map?.Regioes?.Count ?? 0;

    public WDMapEditorValidationReport(DefinicaoMapa map,
        IReadOnlyList<ProblemaValidacaoMapa> issues)
    {
        Map = map;
        Issues = issues ?? Array.Empty<ProblemaValidacaoMapa>();
    }
}

public readonly struct WDMapViewportTransform
{
    // ============================================================
    // 04. TRANSFORMAÇÃO AUTORITATIVA ART LOCAL ↔ VIEWPORT
    // ============================================================

    private readonly Rect viewportRect;
    private readonly Bounds spriteBounds;

    public WDMapViewportTransform(Rect viewportRect, Bounds spriteBounds)
    {
        this.viewportRect = viewportRect;
        this.spriteBounds = spriteBounds;
    }

    public Vector2 MapToViewport(Vector2 artLocalPosition)
    {
        float x = Mathf.InverseLerp(spriteBounds.min.x, spriteBounds.max.x, artLocalPosition.x);
        float y = Mathf.InverseLerp(spriteBounds.min.y, spriteBounds.max.y, artLocalPosition.y);
        return new Vector2(
            Mathf.Lerp(viewportRect.xMin, viewportRect.xMax, x),
            Mathf.Lerp(viewportRect.yMax, viewportRect.yMin, y));
    }

    public Vector2 ViewportToMap(Vector2 viewportPosition)
    {
        float x = Mathf.InverseLerp(viewportRect.xMin, viewportRect.xMax, viewportPosition.x);
        float y = Mathf.InverseLerp(viewportRect.yMax, viewportRect.yMin, viewportPosition.y);
        return new Vector2(
            Mathf.Lerp(spriteBounds.min.x, spriteBounds.max.x, x),
            Mathf.Lerp(spriteBounds.min.y, spriteBounds.max.y, y));
    }
}

public sealed class WDMapActivePreviewContext
{
    public DefinicaoMapa Map { get; }
    public Sprite Art { get; }
    public Bounds ArtBounds => Art != null ? Art.bounds : default;
    public bool UsesArtLocalTerritoryPositions { get; }
    public WDMapSpatialSnapshot SpatialSnapshot { get; }
    private readonly Dictionary<string, int> territoryIndexById;
    private readonly Hash128 relevantAssetRevision;

    internal WDMapActivePreviewContext(DefinicaoMapa map,
        WDMapSpatialSnapshot spatialSnapshot)
    {
        Map = map;
        Art = map != null ? map.ArteBase : null;
        SpatialSnapshot = spatialSnapshot ?? new WDMapSpatialSnapshot();
        relevantAssetRevision = CalculateRelevantAssetRevision(map);
        UsesArtLocalTerritoryPositions = Art != null && Art.rect.height > 0f &&
            Mathf.Abs(Art.rect.width / Art.rect.height - 16f / 9f) < 0.001f;
        territoryIndexById = new Dictionary<string, int>(StringComparer.Ordinal);
        if (map?.Territorios != null)
            for (int index = 0; index < map.Territorios.Count; index++)
            {
                string id = map.Territorios[index]?.id;
                if (!string.IsNullOrWhiteSpace(id)) territoryIndexById[id] = index;
            }
    }

    public static WDMapActivePreviewContext Resolve(DefinicaoMapa map) =>
        new WDMapActivePreviewContext(map, BuildSpatialSnapshot(map));

    private static WDMapSpatialSnapshot BuildSpatialSnapshot(DefinicaoMapa map)
    {
        bool requiresScene = RequiresSceneSnapshot(map);
        WDMapSpatialSnapshot snapshot = requiresScene
            ? WDMapSpatialSnapshot.Capture(map) : new WDMapSpatialSnapshot();
        if (!requiresScene)
            snapshot.AddCanonicalBoundaries(map,
                WDMapMigrationBoundaryRepository.Load());
        return snapshot;
    }

    public static bool RequiresSceneSnapshot(DefinicaoMapa map) =>
        map?.ArteBase != null && map.ArteBase.rect.height > 0f &&
        Mathf.Abs(map.ArteBase.rect.width / map.ArteBase.rect.height -
            16f / 9f) >= 0.001f;

    public bool Matches(DefinicaoMapa map) => ReferenceEquals(Map, map) &&
        map != null && ReferenceEquals(Art, map.ArteBase);

    public bool RelevantAssetsUnchanged(DefinicaoMapa map) => Matches(map) &&
        relevantAssetRevision == CalculateRelevantAssetRevision(map);

    public Vector2 ToArtLocal(string territoryId, Vector2 storedPosition) =>
        UsesArtLocalTerritoryPositions
            ? storedPosition
            : SpatialSnapshot.ToArtLocal(territoryId, storedPosition);

    public Vector2 ToStoredPosition(string territoryId, Vector2 artLocalPosition) =>
        UsesArtLocalTerritoryPositions
            ? artLocalPosition
            : SpatialSnapshot.ToTerritoryLocal(territoryId, artLocalPosition);

    public bool TryGetTerritoryIndex(string territoryId, out int index) =>
        territoryIndexById.TryGetValue(territoryId ?? string.Empty, out index);

    public bool TryGetTerritoryPolygons(string territoryId,
        out IReadOnlyList<IReadOnlyList<Vector2>> polygons) =>
        SpatialSnapshot.TryGetHistoricalPolygons(territoryId, out polygons);

    public bool TryHitTerritory(Vector2 artLocalPoint, out int territoryIndex)
    {
        territoryIndex = -1;
        foreach (KeyValuePair<string, int> item in territoryIndexById
                     .OrderBy(pair => pair.Value))
            if (SpatialSnapshot.Contains(item.Key, artLocalPoint))
            {
                territoryIndex = item.Value;
                return true;
            }
        return false;
    }

    private static Hash128 CalculateRelevantAssetRevision(DefinicaoMapa map)
    {
        if (map == null)
            return default;
        string mapPath = AssetDatabase.GetAssetPath(map);
        string scenePath = map.Metadados?.FirstOrDefault(metadata =>
            metadata != null && metadata.chave == "scenePath")?.valor;
        string artPath = map.ArteBase != null
            ? AssetDatabase.GetAssetPath(map.ArteBase)
            : string.Empty;
        string signature = (string.IsNullOrEmpty(mapPath)
                ? string.Empty : AssetDatabase.GetAssetDependencyHash(mapPath).ToString()) +
            "|" + (string.IsNullOrEmpty(scenePath) || !System.IO.File.Exists(scenePath)
                ? string.Empty : System.IO.File.GetLastWriteTimeUtc(scenePath).Ticks.ToString()) +
            "|" + (string.IsNullOrEmpty(artPath)
                ? string.Empty : AssetDatabase.GetAssetDependencyHash(artPath).ToString());
        return Hash128.Compute(signature);
    }
}

public sealed class WDMapSpatialSnapshot
{
    private readonly Dictionary<string, Matrix4x4> territoryToArt =
        new Dictionary<string, Matrix4x4>(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<IReadOnlyList<Vector2>>>
        historicalPolygons = new Dictionary<string,
            IReadOnlyList<IReadOnlyList<Vector2>>>(StringComparer.Ordinal);

    public Vector2 ToArtLocal(string territoryId, Vector2 territoryLocalPosition)
    {
        Matrix4x4 matrix = territoryToArt.TryGetValue(territoryId ?? string.Empty, out Matrix4x4 value)
            ? value : Matrix4x4.identity;
        return matrix.MultiplyPoint3x4(territoryLocalPosition);
    }

    public Vector2 ToTerritoryLocal(string territoryId, Vector2 artLocalPosition)
    {
        Matrix4x4 matrix = territoryToArt.TryGetValue(territoryId ?? string.Empty, out Matrix4x4 value)
            ? value : Matrix4x4.identity;
        return matrix.inverse.MultiplyPoint3x4(artLocalPosition);
    }

    public bool TryGetHistoricalPolygons(string territoryId,
        out IReadOnlyList<IReadOnlyList<Vector2>> polygons) =>
        historicalPolygons.TryGetValue(territoryId ?? string.Empty, out polygons);

    public bool Contains(string territoryId, Vector2 artLocalPoint) =>
        historicalPolygons.TryGetValue(territoryId ?? string.Empty,
            out IReadOnlyList<IReadOnlyList<Vector2>> polygons) &&
        polygons.Any(polygon => PointInPolygon(polygon, artLocalPoint));

    public void AddCanonicalBoundaries(DefinicaoMapa map,
        WDMapMigrationBoundaryStore store)
    {
        if (map?.ArteBase == null || store == null) return;
        foreach (WDMapMigrationBoundaryRecord record in store.ForMap(map.MapaId))
        {
            var polygons = new List<IReadOnlyList<Vector2>>();
            foreach (WDMapMigrationBoundaryPath path in record.paths ??
                     new List<WDMapMigrationBoundaryPath>())
            {
                Vector2[] converted = (path.points ?? new List<Vector2>())
                    .Select(point => WDMap16x9PromotionPlanner
                        .BoundaryPixelToSpriteLocal(point, map.ArteBase)).ToArray();
                if (converted.Length >= 3) polygons.Add(converted);
            }
            if (polygons.Count > 0)
                historicalPolygons[record.territoryId] = polygons;
        }
    }

    internal void SetPolygonsForTests(string territoryId,
        IReadOnlyList<IReadOnlyList<Vector2>> polygons)
    {
        historicalPolygons[territoryId] = polygons;
    }

    private static bool PointInPolygon(IReadOnlyList<Vector2> polygon, Vector2 point)
    {
        if (polygon == null || polygon.Count < 3) return false;
        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            Vector2 current = polygon[i];
            Vector2 previous = polygon[j];
            if (PointOnSegment(point, previous, current)) return true;
            if ((current.y > point.y) != (previous.y > point.y) &&
                point.x < (previous.x - current.x) * (point.y - current.y) /
                (previous.y - current.y) + current.x) inside = !inside;
        }
        return inside;
    }

    private static bool PointOnSegment(Vector2 point, Vector2 first, Vector2 second)
    {
        Vector2 segment = second - first;
        Vector2 relative = point - first;
        if (segment.sqrMagnitude <= 0.00000001f)
            return relative.sqrMagnitude <= 0.00000001f;
        float cross = segment.x * relative.y - segment.y * relative.x;
        if (Mathf.Abs(cross) > 0.0001f) return false;
        float dot = Vector2.Dot(relative, segment);
        return dot >= 0f && dot <= segment.sqrMagnitude;
    }

    // ============================================================
    // 05. LEITURA NÃO DESTRUTIVA DAS MATRIZES DA SCENE ASSOCIADA
    // ============================================================

    public static WDMapSpatialSnapshot Capture(DefinicaoMapa map)
    {
        var snapshot = new WDMapSpatialSnapshot();
        string scenePath = map?.Metadados?.FirstOrDefault(metadata =>
            metadata != null && metadata.chave == "scenePath")?.valor;
        if (map == null || string.IsNullOrWhiteSpace(scenePath)) return snapshot;

        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool openedTemporarily = !scene.IsValid() || !scene.isLoaded;
        if (openedTemporarily)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        try
        {
            SpriteRenderer artRenderer = null;
            var territories = new List<TerritorioClique>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                territories.AddRange(root.GetComponentsInChildren<TerritorioClique>(true));
                foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                    if (renderer.sprite == map.ArteBase) { artRenderer = renderer; break; }
            }
            Matrix4x4 worldToArt = artRenderer != null
                ? artRenderer.transform.worldToLocalMatrix : Matrix4x4.identity;
            foreach (TerritorioClique territory in territories)
                if (territory != null && !string.IsNullOrWhiteSpace(territory.idTerritorio))
                {
                    snapshot.territoryToArt[territory.idTerritorio] =
                        worldToArt * territory.transform.localToWorldMatrix;
                    PolygonCollider2D collider =
                        territory.GetComponent<PolygonCollider2D>();
                    if (collider == null || collider.pathCount == 0) continue;
                    Matrix4x4 colliderToArt = worldToArt *
                        collider.transform.localToWorldMatrix;
                    var paths = new List<IReadOnlyList<Vector2>>(collider.pathCount);
                    for (int pathIndex = 0; pathIndex < collider.pathCount; pathIndex++)
                    {
                        Vector2[] path = collider.GetPath(pathIndex).Select(point =>
                            (Vector2)colliderToArt.MultiplyPoint3x4(point)).ToArray();
                        if (path.Length >= 3) paths.Add(path);
                    }
                    if (paths.Count > 0)
                        snapshot.historicalPolygons[territory.idTerritorio] = paths;
                }
        }
        finally
        {
            if (openedTemporarily && scene.IsValid())
                EditorSceneManager.CloseScene(scene, true);
        }
        return snapshot;
    }
}

public sealed class WDMapEditorSelection
{
    // ============================================================
    // 06. SELEÇÃO SIMPLES/MÚLTIPLA INDEPENDENTE DA INTERFACE
    // ============================================================

    private readonly HashSet<int> selected = new HashSet<int>();
    public IReadOnlyList<int> Indices => selected.OrderBy(index => index).ToArray();
    public bool HasPair => selected.Count == 2;
    public bool Contains(int index) => selected.Contains(index);

    public void Click(int index, bool additive)
    {
        if (!additive)
        {
            selected.Clear();
            selected.Add(index);
            return;
        }
        if (!selected.Add(index)) selected.Remove(index);
    }

    public void Clear() => selected.Clear();
}
