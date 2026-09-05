using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "WDPlayerPaletteConfig",
    menuName = "War Dominion/Configuração/Paleta Global de Jogadores")]
public sealed class WDPlayerPaletteConfig : ScriptableObject
{
    // ============================================================
    // 01. FONTE GLOBAL PERSISTENTE
    // ============================================================

    public const string ResourcePath = "Config/WDPlayerPaletteConfig";

    [Header("Cores oficiais dos jogadores")]
    [SerializeField] private Color[] colors = new Color[10];

    [Header("Índice padrão por slot de jogador")]
    [SerializeField] private int[] defaultPlayerColorIndices =
        { 9, 1, 2, 4, 0, 6 };

    [Header("Território sem dono")]
    [SerializeField] private Color ownerlessTerritoryColor =
        new Color(0.95f, 0.91f, 0.78f, 0.92f);

    private static WDPlayerPaletteConfig cached;

    public IReadOnlyList<Color> Colors => colors;
    public Color OwnerlessTerritoryColor => ownerlessTerritoryColor;

    public Color GetColor(int colorIndex)
    {
        if (colors == null || colors.Length == 0)
            return Color.white;

        Color color = colors[Mathf.Clamp(colorIndex, 0, colors.Length - 1)];
        color.a = 1f;
        return color;
    }

    public int GetDefaultColorIndex(TerritorioClique.Dono owner)
    {
        int slotIndex = (int)owner - 1;
        if (slotIndex < 0 || defaultPlayerColorIndices == null ||
            slotIndex >= defaultPlayerColorIndices.Length)
            return 0;

        return defaultPlayerColorIndices[slotIndex];
    }

    // ============================================================
    // 02. ACESSO GLOBAL PARA CENAS ATUAIS E FUTURAS
    // ============================================================

    public static WDPlayerPaletteConfig LoadGlobal()
    {
        if (cached == null)
            cached = Resources.Load<WDPlayerPaletteConfig>(ResourcePath);

        if (cached == null)
            Debug.LogError("Paleta global ausente em Resources/" + ResourcePath + ".");

        return cached;
    }

    public static Color GetGlobalColor(int colorIndex)
    {
        WDPlayerPaletteConfig config = LoadGlobal();
        return config != null ? config.GetColor(colorIndex) : Color.white;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        cached = null;
    }
}
