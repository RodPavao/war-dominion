public sealed class WDArmoryWeaponCardV2View : WDArmoryWeaponCardView
{
    // 01. Adapter nominal para manter o prefab V2 independente do legado visual.
    public void Present(UnityEngine.Texture texture, string displayName, string level, string energy,
        string tokens, float progress, bool selected, bool locked)
    {
        int.TryParse(level?.Replace("NÍVEL", string.Empty).Trim(), out int parsedLevel);
        int.TryParse(energy?.Replace("ϟ", string.Empty).Trim(), out int parsedEnergy);
        SetData(new ViewData(string.Empty, displayName, string.Empty, parsedLevel, parsedEnergy,
            0, 0, locked ? VisualState.Locked : selected ? VisualState.Selected : VisualState.Available), texture);
    }
}
