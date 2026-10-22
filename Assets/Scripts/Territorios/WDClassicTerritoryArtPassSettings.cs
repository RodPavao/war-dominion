using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "WDClassicTerritoryArtPassPrototype",
    menuName = "War Dominion/Visual/Classic Territory Art Pass Prototype")]
public sealed class WDClassicTerritoryArtPassSettings : ScriptableObject
{
    [Serializable]
    public sealed class TerritorySpriteOverride
    {
        [SerializeField] private string territoryId;
        [SerializeField] private Sprite derivedSprite;

        public string TerritoryId => territoryId;
        public Sprite DerivedSprite => derivedSprite;
    }

    public const string ResourcePath =
        "Config/WDClassicTerritoryArtPassPrototype";

    // ============================================================
    // 1. CATÁLOGO EXPERIMENTAL REVERSÍVEL E LIMITADO AO CLASSIC
    // ============================================================
    [Header("Experimental — Classic / Art Pass 2 Lot 1")]
    [SerializeField] private bool prototypeEnabled;
    [SerializeField] private string mapId = "classic";
    [SerializeField] private TerritorySpriteOverride[] territorySprites =
        Array.Empty<TerritorySpriteOverride>();

    public bool PrototypeEnabled => prototypeEnabled;
    public string MapId => mapId;
    public TerritorySpriteOverride[] TerritorySprites => territorySprites;

    public static WDClassicTerritoryArtPassSettings Load()
    {
        return Resources.Load<WDClassicTerritoryArtPassSettings>(ResourcePath);
    }

    // ============================================================
    // 2. RESOLUÇÃO ISOLADA DO SPRITE VISUAL
    // ============================================================
    // A comparação ordinal impede que nomes semelhantes ou outros mapas
    // recebam o experimento. Desativar a opção retorna sempre o sprite original.
    public Sprite ResolveSprite(
        string activeMapId,
        string activeTerritoryId,
        Sprite originalSprite)
    {
        if (!prototypeEnabled ||
            !string.Equals(activeMapId, mapId, StringComparison.Ordinal) ||
            territorySprites == null)
            return originalSprite;

        for (int index = 0; index < territorySprites.Length; index++)
        {
            TerritorySpriteOverride entry = territorySprites[index];
            if (entry != null && entry.DerivedSprite != null &&
                string.Equals(activeTerritoryId, entry.TerritoryId,
                    StringComparison.Ordinal))
                return entry.DerivedSprite;
        }

        return originalSprite;
    }
}
