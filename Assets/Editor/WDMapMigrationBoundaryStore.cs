using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public enum WDMapBoundaryConfidence { High, Medium, Low, Ambiguous }
public enum WDMapBoundaryReviewState { AutoNotReviewed, ReviewShape, Approved, SavedForMigration }

[Serializable]
public sealed class WDMapMigrationBoundaryPath
{
    public int componentIndex;
    public int sourceArea;
    public RectInt sourceBounds;
    public List<Vector2> points = new List<Vector2>();
}

[Serializable]
public sealed class WDMapMigrationBoundaryRecord
{
    public string mapId;
    public string territoryId;
    public string origin = "MASK_AUTO";
    public WDMapBoundaryConfidence confidence;
    public WDMapBoundaryReviewState reviewState;
    public string maskAssetPath;
    public string maskHash;
    public int algorithmVersion;
    public List<WDMapMigrationBoundaryPath> paths = new List<WDMapMigrationBoundaryPath>();
}

public sealed class WDMapMigrationBoundaryStore : ScriptableObject
{
    [SerializeField] private List<WDMapMigrationBoundaryRecord> boundaries =
        new List<WDMapMigrationBoundaryRecord>();

    public IReadOnlyList<WDMapMigrationBoundaryRecord> Boundaries => boundaries;

    public IEnumerable<WDMapMigrationBoundaryRecord> ForMap(string mapId) =>
        boundaries.Where(item => item.mapId == mapId);

    public bool TryGet(string mapId, string territoryId,
        out WDMapMigrationBoundaryRecord record)
    {
        record = boundaries.FirstOrDefault(item => item.mapId == mapId &&
            item.territoryId == territoryId);
        return record != null;
    }

    public void ReplaceMap(string mapId, IEnumerable<WDMapMigrationBoundaryRecord> records)
    {
        WDMapMigrationBoundaryRecord[] materialized =
            (records ?? Array.Empty<WDMapMigrationBoundaryRecord>()).ToArray();
        List<WDMapMigrationBoundaryRecord> replacement = boundaries
            .Where(item => item.mapId != mapId).Concat(materialized).ToList();
        replacement.Sort((first, second) =>
        {
            int mapComparison = string.CompareOrdinal(first.mapId, second.mapId);
            return mapComparison != 0 ? mapComparison :
                string.CompareOrdinal(first.territoryId, second.territoryId);
        });
        boundaries = replacement;
    }
}

public static class WDMapMigrationBoundaryRepository
{
    public const string AssetPath = "Assets/Editor/WDMapMigrationBoundaryStore.asset";

    public static WDMapMigrationBoundaryStore Load() =>
        AssetDatabase.LoadAssetAtPath<WDMapMigrationBoundaryStore>(AssetPath);

    public static WDMapMigrationBoundaryStore LoadOrCreate()
    {
        WDMapMigrationBoundaryStore store = Load();
        if (store != null) return store;
        store = ScriptableObject.CreateInstance<WDMapMigrationBoundaryStore>();
        AssetDatabase.CreateAsset(store, AssetPath);
        AssetDatabase.SaveAssetIfDirty(store);
        return store;
    }

    public static void SaveMap(WDMapMigrationBoundaryStore store, string mapId,
        IEnumerable<WDMapMigrationBoundaryRecord> records)
    {
        Undo.RecordObject(store, "Gerar boundaries da Territory Mask");
        store.ReplaceMap(mapId, records);
        EditorUtility.SetDirty(store);
        AssetDatabase.SaveAssetIfDirty(store);
    }
}
