using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WarDominionMapEditorOperations
{
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

public sealed class WDMapSpatialSnapshot
{
    private readonly Dictionary<string, Matrix4x4> territoryToArt =
        new Dictionary<string, Matrix4x4>(StringComparer.Ordinal);

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
                    snapshot.territoryToArt[territory.idTerritorio] =
                        worldToArt * territory.transform.localToWorldMatrix;
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
