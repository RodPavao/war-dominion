using UnityEngine;

public class PaletaJogadores : MonoBehaviour
{
    // ============================================================
    // 01. FACHADA LEGADA PARA A CONFIGURAÇÃO GLOBAL
    // ============================================================

    public static PaletaJogadores instance;

    private void Awake()
    {
        instance = this;
    }

    public Color ObterCor(TerritorioClique.Dono dono)
    {
        return ObterCorAtiva(dono);
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

        return WDPlayerPaletteConfig.GetGlobalDefaultColorForSlot((int)dono - 1);
    }

    public static Color ObterCorNeutraAtiva()
    {
        return WDPlayerPaletteConfig.GetGlobalOwnerlessColor();
    }
}
