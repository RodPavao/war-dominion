using UnityEngine;

public class PaletaJogadores : MonoBehaviour
{
    // ============================================================
    // 01. FACHADA LEGADA PARA A CONFIGURAÇÃO GLOBAL
    // ============================================================

    public static PaletaJogadores instance;

    public static readonly Color CorTerritorioSemDonoPadrao =
        new Color(0.95f, 0.91f, 0.78f, 0.92f);

    private void Awake()
    {
        instance = this;
    }

    public Color ObterCor(TerritorioClique.Dono dono)
    {
        WDPlayerPaletteConfig config = WDPlayerPaletteConfig.LoadGlobal();
        if (config == null)
            return Color.white;

        return config.GetColor(config.GetDefaultColorIndex(dono));
    }

    // ============================================================
    // 02. COR EFETIVA DA PARTIDA E ESTADO SEM DONO
    // ============================================================

    public static Color ObterCorAtiva(TerritorioClique.Dono dono)
    {
        if (WDMatchSetupContext.TryGetParticipant(
                dono,
                out WDMatchParticipant participant))
            return participant.MatchColor;

        WDPlayerPaletteConfig config = WDPlayerPaletteConfig.LoadGlobal();
        if (config == null)
            return Color.white;

        return config.GetColor(config.GetDefaultColorIndex(dono));
    }

    public static Color ObterCorNeutraAtiva()
    {
        WDPlayerPaletteConfig config = WDPlayerPaletteConfig.LoadGlobal();
        return config != null
            ? config.OwnerlessTerritoryColor
            : CorTerritorioSemDonoPadrao;
    }
}
