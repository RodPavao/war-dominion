#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class WarDominionUIEditor
{
    // ============================================================
    // 01. APRESENTAÇÕES LEGADAS APOSENTADAS
    // ============================================================

    private const string MenuNovo = "War Dominion/Ferramentas de Desenvolvimento/UI/Usar HUD Novo";
    private const string MenuLegado = "War Dominion/Ferramentas de Desenvolvimento/UI/Usar HUD Legado";

    [MenuItem(MenuNovo)]
    private static void UsarHUDNovo() { }

    [MenuItem(MenuLegado)]
    private static void UsarHUDLegado() { }

    [MenuItem(MenuNovo, true)]
    private static bool ValidarHUDNovo()
    {
        Menu.SetChecked(MenuNovo, false);
        return false;
    }

    [MenuItem(MenuLegado, true)]
    private static bool ValidarHUDLegado()
    {
        Menu.SetChecked(MenuLegado, false);
        return false;
    }
}
#endif
