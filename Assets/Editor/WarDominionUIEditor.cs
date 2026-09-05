#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class WarDominionUIEditor
{
    // ============================================================
    // 01. ALTERNÂNCIA DE VALIDAÇÃO HUD NOVO / HUD LEGADO
    // ============================================================

    private const string MenuNovo = "War Dominion/Ferramentas de Desenvolvimento/UI/Usar HUD Novo";
    private const string MenuLegado = "War Dominion/Ferramentas de Desenvolvimento/UI/Usar HUD Legado";

    [MenuItem(MenuNovo)]
    private static void UsarHUDNovo() => DefinirModo(true);

    [MenuItem(MenuLegado)]
    private static void UsarHUDLegado() => DefinirModo(false);

    [MenuItem(MenuNovo, true)]
    private static bool ValidarHUDNovo()
    {
        bool ativo = PlayerPrefs.GetInt(UICompositionRoot.ChaveHUDNovo, 1) == 1;
        Menu.SetChecked(MenuNovo, ativo);
        return true;
    }

    [MenuItem(MenuLegado, true)]
    private static bool ValidarHUDLegado()
    {
        bool ativo = PlayerPrefs.GetInt(UICompositionRoot.ChaveHUDNovo, 1) == 0;
        Menu.SetChecked(MenuLegado, ativo);
        return true;
    }

    private static void DefinirModo(bool hudNovo)
    {
        PlayerPrefs.SetInt(UICompositionRoot.ChaveHUDNovo, hudNovo ? 1 : 0);
        PlayerPrefs.Save();
        UICompositionRoot root = Object.FindAnyObjectByType<UICompositionRoot>();
        root?.DefinirHUDNovoAtivo(hudNovo);
    }
}
#endif
