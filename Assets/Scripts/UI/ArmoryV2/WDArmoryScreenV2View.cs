using UnityEngine;
using UnityEngine.UI;

public sealed class WDArmoryScreenV2View : WDArmoryScreenView
{
    // 01. A V2 reutiliza somente o contrato funcional da View.
    // Toda a geometria permanece serializada no prefab aprovado.
    public override void ApplyResponsiveLayout()
    {
        // 02. O shell V2 mantém o frame lógico 1920×1080 serializado.
        // Background e veil continuam fullscreen fora desse frame.
    }

    // ============================================================
    // 03. APLICAÇÃO PERSISTENTE SEM RECONSTRUIR A HIERARQUIA
    // ============================================================
    public void ApplyVisualSettings(WDArmoryV2VisualSettings settings)
    {
        if (settings == null) return;
        SetPanelColor(Header, settings.panelColor);
        SetPanelColor(DeckPanel, settings.panelColor);
        SetPanelColor(CatalogPanel, settings.panelColor);
        SetPanelColor(DetailsPanel, settings.panelColor);

        // ============================================================
        // 04. CORES SERIALIZADAS DE AÇÕES ESPECÍFICAS
        // ============================================================
        // EVOLUIR preserva a cor aprovada no prefab V2. Somente as ações que
        // compartilham o estilo secundário recebem buttonColor.
        string[] styledButtons = { "Back", "SaveDeck", "Notifications" };
        foreach (Button button in GetComponentsInChildren<Button>(true))
            if (System.Array.IndexOf(styledButtons, button.name) >= 0 && button.targetGraphic is Image image)
                image.color = settings.buttonColor;
        Transform add = FindNamed(transform, "AddToDeck");
        if (add != null && add.GetComponent<Button>()?.targetGraphic is Image primary) primary.color = settings.buttonPrimaryColor;

        ConfigureGrid("MainSlots", settings.mainSlotSize, settings.mainSlotSpacing);
        ConfigureGrid("SupportSlots", settings.supportSlotSize, settings.supportSlotSpacing);
        ConfigureGrid("WeaponGrid", settings.catalogCardSize, settings.catalogSpacing);
        // ============================================================
        // 05. TIPOGRAFIA SERIALIZADA É A AUTORIDADE DA V2
        // ============================================================
        // Pesos, tamanhos, wrapping, autosizing e overflow permanecem como
        // aprovados em cada TMP_Text do prefab. Settings visuais não podem
        // substituir semanticamente Gabarito/Inter em toda a hierarquia.
        foreach (WDArmoryWeaponCardView card in CatalogCards) card?.ApplyVisualSettings(settings);
        foreach (WDArmorySlotView slot in MainSlots) slot?.ApplyVisualSettings(settings, false);
        foreach (WDArmorySlotView slot in SupportSlots) slot?.ApplyVisualSettings(settings, true);
    }

    private void ConfigureGrid(string name, Vector2 size, Vector2 spacing)
    {
        GridLayoutGroup grid = FindNamed(transform, name)?.GetComponent<GridLayoutGroup>();
        if (grid == null) return;
        grid.cellSize = size; grid.spacing = spacing;
    }
    private static void SetPanelColor(RectTransform panel, Color color)
    {
        if (panel != null && panel.TryGetComponent(out Image image)) image.color = color;
    }
    private static Transform FindNamed(Transform root, string name)
    {
        foreach (Transform item in root.GetComponentsInChildren<Transform>(true)) if (item.name == name) return item;
        return null;
    }
}
