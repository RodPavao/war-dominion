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

    [Header("Cores oficiais dos jogadores (HEX RGB)")]
    [SerializeField] private string[] colorHexCodes = System.Array.Empty<string>();

    [Header("Índice padrão por slot de jogador")]
    [SerializeField] private int[] defaultPlayerColorIndices =
        { 9, 1, 2, 4, 0, 6 };

    [Header("Território sem dono")]
    [SerializeField] private Color ownerlessTerritoryColor =
        new Color(0.95f, 0.91f, 0.78f, 0.92f);

    private static WDPlayerPaletteConfig cached;

    public IReadOnlyList<Color> Colors
    {
        get
        {
            var result = new Color[colorHexCodes?.Length ?? 0];
            for (int i = 0; i < result.Length; i++)
                result[i] = ParseHexColor(colorHexCodes[i]);
            return result;
        }
    }
    public Color OwnerlessTerritoryColor => ownerlessTerritoryColor;

    public Color GetColor(int colorIndex)
    {
        if (colorHexCodes == null || colorHexCodes.Length == 0)
            return Color.white;

        return ParseHexColor(
            colorHexCodes[Mathf.Clamp(colorIndex, 0, colorHexCodes.Length - 1)]);
    }

    public int GetDefaultColorIndex(TerritorioClique.Dono owner)
    {
        return GetDefaultColorIndexForSlot((int)owner - 1);
    }

    public int GetDefaultColorIndexForSlot(int slotIndex)
    {
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

    public static Color GetGlobalDefaultColorForSlot(int slotIndex)
    {
        WDPlayerPaletteConfig config = LoadGlobal();
        return config != null
            ? config.GetColor(config.GetDefaultColorIndexForSlot(slotIndex))
            : Color.white;
    }

    public static IReadOnlyList<Color> GetGlobalColors()
    {
        WDPlayerPaletteConfig config = LoadGlobal();
        return config != null ? config.Colors : System.Array.Empty<Color>();
    }

    public static Color GetGlobalOwnerlessColor()
    {
        WDPlayerPaletteConfig config = LoadGlobal();
        return config != null ? config.OwnerlessTerritoryColor : Color.white;
    }

    // ============================================================
    // 03. CONVERSÃO EXATA, INDEPENDENTE DO COLOR SPACE DO PROJETO
    // ============================================================

    private static Color ParseHexColor(string hex)
    {
        string normalized = string.IsNullOrWhiteSpace(hex)
            ? string.Empty
            : hex.Trim().TrimStart('#');
        if (normalized.Length == 6 &&
            byte.TryParse(normalized.Substring(0, 2),
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out byte red) &&
            byte.TryParse(normalized.Substring(2, 2),
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out byte green) &&
            byte.TryParse(normalized.Substring(4, 2),
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out byte blue))
        {
            return (Color)new Color32(red, green, blue, 255);
        }

        Debug.LogError("Cor inválida na paleta global: '" + hex + "'.");
        return Color.white;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        cached = null;
    }
}
