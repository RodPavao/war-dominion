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
    private readonly WDVipEntitlement vipEntitlement;

    public WDPlayerPrefsArmoryRepository(WDVipEntitlement entitlement = null) => vipEntitlement = entitlement;

    // 02. Sem autoridade de entitlement integrada, VIP legado de desenvolvimento não concede acesso.
    public static bool ApplyAvailableEntitlement(WDArmoryProfile profile)
    {
        if (profile == null || !profile.IsVip) return false;
        profile.SetVip(false);
        profile.Normalize();
        return true;
    }

    public WDArmoryProfile Load(WDArmoryCatalogConfig catalog)
    {
        WDArmoryProfile profile = null;
        if (PlayerPrefs.HasKey(StorageKey))
            profile = JsonUtility.FromJson<WDArmoryProfile>(PlayerPrefs.GetString(StorageKey));
        profile ??= CreateDevelopmentProfile(catalog);
        profile.Normalize();
        bool changed = profile.RemoveLegacyCardPlaceholders();
        if (vipEntitlement == null)
            changed |= ApplyAvailableEntitlement(profile);
        else if (profile.IsVip != vipEntitlement.IsVipActive)
        {
            profile.SetVip(vipEntitlement.IsVipActive);
            profile.Normalize();
            changed = true;
        }
#if UNITY_EDITOR
        changed |= WDArmoryDevelopmentBootstrap.Apply(catalog, profile);
#endif
        if (changed && PlayerPrefs.HasKey(StorageKey))
            Save(profile);
        return profile;
    }

    public void Save(WDArmoryProfile profile)
    {
        if (profile == null)
            return;
        if (vipEntitlement != null)
        {
            profile.SetVip(vipEntitlement.IsVipActive);
            profile.Normalize();
        }
        PlayerPrefs.SetString(StorageKey, JsonUtility.ToJson(profile));
        PlayerPrefs.Save();
    }

    private static WDArmoryProfile CreateDevelopmentProfile(WDArmoryCatalogConfig catalog)
    {
        var profile = new WDArmoryProfile(false, 1200, 25);
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

#if UNITY_EDITOR
public static class WDArmoryDevelopmentBootstrap
{
    // ============================================================
    // 01. COLEÇÃO CANÔNICA DISPONÍVEL SOMENTE PARA TESTES NO EDITOR
    // ============================================================

    public static bool Apply(WDArmoryCatalogConfig catalog, WDArmoryProfile profile)
    {
        if (catalog == null || profile == null ||
            !catalog.UnlockAllWeaponsForDevelopment)
            return false;

        bool changed = false;
        foreach (WDWeaponDefinition weapon in catalog.Weapons)
        {
            if (weapon == null || string.IsNullOrWhiteSpace(weapon.Id))
                continue;

            WDWeaponProgress progress = profile.GetProgress(weapon.Id);
            if (progress == null)
            {
                profile.AddProgress(new WDWeaponProgress(weapon.Id, true, 1, 0));
                changed = true;
                continue;
            }

            if (!progress.Obtained)
            {
                progress.SetProgress(true, progress.Level, progress.Tokens);
                changed = true;
            }
        }
        return changed;
    }
}
#endif
