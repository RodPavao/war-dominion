using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public enum WDMapMigrationPersistenceState
{
    SavedForMigration,
    AppliedToMap
}

[Serializable]
public sealed class WDMapMigrationCalibrationRecord
{
    public string mapId;
    public string territoryId;
    public Vector2 scale;
    public Vector2 offsetPixels;
    public float rotationDegrees;
    public Vector2 originalScale;
    public Vector2 originalOffsetPixels;
    public float originalRotationDegrees;
    public WDMapMigrationPersistenceState state;

    public WDMapMigrationTransform CandidateTransform =>
        new WDMapMigrationTransform(scale, offsetPixels, rotationDegrees);

    public WDMapMigrationTransform OriginalTransform =>
        new WDMapMigrationTransform(originalScale, originalOffsetPixels,
            originalRotationDegrees);
}

public sealed class WDMapMigrationCalibrationStore : ScriptableObject
{
    [SerializeField] private List<WDMapMigrationCalibrationRecord> calibrations =
        new List<WDMapMigrationCalibrationRecord>();

    public IReadOnlyList<WDMapMigrationCalibrationRecord> Calibrations => calibrations;

    public bool TryGet(string mapId, string territoryId,
        out WDMapMigrationCalibrationRecord record)
    {
        record = calibrations.FirstOrDefault(item => item.mapId == mapId &&
            item.territoryId == territoryId);
        return record != null;
    }

    public void Upsert(string mapId, string territoryId,
        WDMapMigrationTransform candidate, WDMapMigrationTransform original)
    {
        if (!TryGet(mapId, territoryId, out WDMapMigrationCalibrationRecord record))
        {
            record = new WDMapMigrationCalibrationRecord
            {
                mapId = mapId,
                territoryId = territoryId
            };
            calibrations.Add(record);
        }
        record.scale = candidate.Scale;
        record.offsetPixels = candidate.OffsetPixels;
        record.rotationDegrees = candidate.RotationDegrees;
        record.originalScale = original.Scale;
        record.originalOffsetPixels = original.OffsetPixels;
        record.originalRotationDegrees = original.RotationDegrees;
        record.state = WDMapMigrationPersistenceState.SavedForMigration;
    }

    public bool Remove(string mapId, string territoryId)
    {
        int removed = calibrations.RemoveAll(item => item.mapId == mapId &&
            item.territoryId == territoryId);
        return removed > 0;
    }

    public int CountForMap(string mapId) => calibrations.Count(item => item.mapId == mapId);
}

public static class WDMapMigrationCalibrationRepository
{
    public const string AssetPath =
        "Assets/Editor/WDMapMigrationCalibrationStore.asset";

    public static WDMapMigrationCalibrationStore Load() =>
        AssetDatabase.LoadAssetAtPath<WDMapMigrationCalibrationStore>(AssetPath);

    public static WDMapMigrationCalibrationStore LoadOrCreate()
    {
        WDMapMigrationCalibrationStore store =
            AssetDatabase.LoadAssetAtPath<WDMapMigrationCalibrationStore>(AssetPath);
        if (store != null) return store;
        store = ScriptableObject.CreateInstance<WDMapMigrationCalibrationStore>();
        AssetDatabase.CreateAsset(store, AssetPath);
        AssetDatabase.SaveAssetIfDirty(store);
        return store;
    }

    public static void Save(WDMapMigrationCalibrationStore store, string mapId,
        IEnumerable<WDMapMigrationFitResult> results, string undoName)
    {
        if (store == null || results == null) return;
        Undo.RecordObject(store, undoName);
        foreach (WDMapMigrationFitResult result in results)
            store.Upsert(mapId, result.TerritoryId, result.CandidateTransform,
                result.InitialCandidateTransform);
        EditorUtility.SetDirty(store);
        AssetDatabase.SaveAssetIfDirty(store);
    }

    public static void Remove(WDMapMigrationCalibrationStore store, string mapId,
        string territoryId)
    {
        if (store == null || !store.TryGet(mapId, territoryId, out _)) return;
        Undo.RecordObject(store, "Restaurar calibração original");
        store.Remove(mapId, territoryId);
        EditorUtility.SetDirty(store);
        AssetDatabase.SaveAssetIfDirty(store);
    }

    public static IReadOnlyList<WDMapMigrationFitResult> SelectApprovedUnsaved(
        WDMapMigrationCalibrationStore store, string mapId,
        IEnumerable<WDMapMigrationFitResult> results) => (results ??
            Array.Empty<WDMapMigrationFitResult>()).Where(result => result.Accepted &&
            !Matches(store, mapId, result)).ToArray();

    public static bool Matches(WDMapMigrationCalibrationStore store, string mapId,
        WDMapMigrationFitResult result)
    {
        if (store == null || result == null ||
            !store.TryGet(mapId, result.TerritoryId,
                out WDMapMigrationCalibrationRecord record)) return false;
        WDMapMigrationTransform saved = record.CandidateTransform;
        WDMapMigrationTransform current = result.CandidateTransform;
        return saved.Scale == current.Scale && saved.OffsetPixels == current.OffsetPixels &&
            Mathf.Approximately(saved.RotationDegrees, current.RotationDegrees);
    }
}
