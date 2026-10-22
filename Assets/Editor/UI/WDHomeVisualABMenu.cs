#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class WDHomeVisualABMenu
{
    private const string Root = "War Dominion/UI/Home Visual A-B/";

    [MenuItem(Root + "A - Chrome Premium")]
    private static void Chrome() => Select(WDHomeVisualVariant.ChromePremium);

    [MenuItem(Root + "B - Sapphire Glass")]
    private static void Sapphire() => Select(WDHomeVisualVariant.SapphireGlass);

    [MenuItem(Root + "Restaurar Original")]
    private static void Original() => Select(WDHomeVisualVariant.Original);

    private static void Select(WDHomeVisualVariant variant)
    {
        PlayerPrefs.SetInt(WDHomeVisualAppearanceController.PlayerPrefsKey, (int)variant);
        PlayerPrefs.Save();
        WarDominionHomeController home = Object.FindFirstObjectByType<WarDominionHomeController>();
        if (Application.isPlaying && home != null)
            home.SetHomeVisualVariant(variant);
        Debug.Log($"Home Visual A/B: {variant}. F6 alterna A/B durante o Play Mode.");
    }
}
#endif
