using UnityEngine;

public interface IWDArmoryRepository
{
    WDArmoryProfile Load(WDArmoryCatalogConfig catalog);
    void Save(WDArmoryProfile profile);
}

public sealed class WDPlayerPrefsArmoryRepository : IWDArmoryRepository
{
    // ============================================================
    // 01. ADAPTADOR LOCAL SUBSTITUÍVEL POR CONTA/BACKEND
    // ============================================================

    private const string StorageKey = "war_dominion.armory.profile.v1";

    public WDArmoryProfile Load(WDArmoryCatalogConfig catalog)
    {
        WDArmoryProfile profile = null;
        if (PlayerPrefs.HasKey(StorageKey))
            profile = JsonUtility.FromJson<WDArmoryProfile>(PlayerPrefs.GetString(StorageKey));
        profile ??= CreateDevelopmentProfile(catalog);
        profile.Normalize();
        return profile;
    }

    public void Save(WDArmoryProfile profile)
    {
        if (profile == null)
            return;
        PlayerPrefs.SetString(StorageKey, JsonUtility.ToJson(profile));
        PlayerPrefs.Save();
    }

    private static WDArmoryProfile CreateDevelopmentProfile(WDArmoryCatalogConfig catalog)
    {
        var profile = new WDArmoryProfile(
            catalog != null && catalog.VipEnabledForDevelopment, 1200, 25);
        if (catalog == null)
            return profile;

        for (int i = 0; i < catalog.Weapons.Count; i++)
        {
            WDWeaponDefinition weapon = catalog.Weapons[i];
            bool obtained = i < 4;
            profile.AddProgress(new WDWeaponProgress(
                weapon.Id, obtained, i == 3 ? WDWeaponDefinition.MaxLevel : 1,
                obtained ? i * 12 : 0));
        }
        return profile;
    }
}
