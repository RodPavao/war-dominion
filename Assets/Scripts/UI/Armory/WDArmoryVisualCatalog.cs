using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WDArmoryVisualCatalog", menuName = "War Dominion/UI/Armory Visual Catalog")]
public sealed class WDArmoryVisualCatalog : ScriptableObject
{
    // ============================================================
    // 01. ARTWORKS OFICIAIS DO VISUALKIT
    // ============================================================

    [Serializable]
    public sealed class Entry
    {
        [SerializeField] private string weaponId;
        [SerializeField] private Texture2D artwork;

        public string WeaponId => weaponId;
        public Texture2D Artwork => artwork;

        public Entry(string id, Texture2D texture)
        {
            weaponId = id;
            artwork = texture;
        }
    }

    [SerializeField] private List<Entry> weapons = new();
    private Dictionary<string, Texture2D> cache;

    public IReadOnlyList<Entry> Weapons => weapons;

    public Texture2D Resolve(string weaponId)
    {
        if (string.IsNullOrWhiteSpace(weaponId)) return null;
        EnsureCache();
        return cache.TryGetValue(weaponId, out Texture2D texture) ? texture : null;
    }

#if UNITY_EDITOR
    public void Configure(IEnumerable<Entry> entries)
    {
        weapons = entries != null ? new List<Entry>(entries) : new List<Entry>();
        cache = null;
    }
#endif

    private void EnsureCache()
    {
        if (cache != null) return;
        cache = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        foreach (Entry entry in weapons)
            if (entry != null && !string.IsNullOrWhiteSpace(entry.WeaponId) && entry.Artwork != null)
                cache[entry.WeaponId] = entry.Artwork;
    }
}
