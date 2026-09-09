using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(
    fileName = "WDArmoryCatalogConfig",
    menuName = "War Dominion/UI/Armory Catalog Config")]
public sealed class WDArmoryCatalogConfig : ScriptableObject
{
    // ============================================================
    // 01. CATÁLOGO FUNCIONAL DATA-DRIVEN
    // ============================================================

    [SerializeField] private List<WDWeaponCategoryDefinition> categories = new();
    [SerializeField] private List<WDWeaponDefinition> weapons = new();
    [SerializeField] private List<WDWeaponProgressionProfile> progressionProfiles = new();
    [SerializeField] private bool vipEnabledForDevelopment;
    [SerializeField] private bool unlockAllWeaponsForDevelopment;

    public IReadOnlyList<WDWeaponCategoryDefinition> Categories => categories;
    public IReadOnlyList<WDWeaponDefinition> Weapons => weapons;
    public IReadOnlyList<WDWeaponProgressionProfile> ProgressionProfiles => progressionProfiles;
    public bool VipEnabledForDevelopment => vipEnabledForDevelopment;
    public bool UnlockAllWeaponsForDevelopment => unlockAllWeaponsForDevelopment;

    public WDWeaponDefinition FindWeapon(string weaponId) =>
        weapons?.FirstOrDefault(item => item != null && item.Id == weaponId);

    public bool IsSupportEligible(WDWeaponDefinition weapon)
    {
        if (weapon == null)
            return false;
        WDWeaponCategoryDefinition category = categories?.FirstOrDefault(
            item => item != null && item.Id == weapon.CategoryId);
        return category != null && category.SupportEligible;
    }

    public WDWeaponLevelDefinition FindUpgrade(
        WDWeaponDefinition weapon, int targetLevel)
    {
        if (weapon == null || targetLevel < 2 || targetLevel > weapon.MaximumLevel)
            return null;

        WDWeaponProgressionProfile profile = progressionProfiles?.FirstOrDefault(
            item => item != null && item.MaximumLevel == weapon.MaximumLevel);
        WDWeaponLevelDefinition standard = profile?.GetUpgradeToLevel(targetLevel);
        return standard ?? weapon.GetUpgradeToLevel(targetLevel);
    }

    public void Configure(
        IEnumerable<WDWeaponCategoryDefinition> newCategories,
        IEnumerable<WDWeaponDefinition> newWeapons,
        bool developmentVip = false,
        bool developmentUnlockAllWeapons = false)
    {
        categories = newCategories?.ToList() ?? new List<WDWeaponCategoryDefinition>();
        weapons = newWeapons?.ToList() ?? new List<WDWeaponDefinition>();
        vipEnabledForDevelopment = developmentVip;
        unlockAllWeaponsForDevelopment = developmentUnlockAllWeapons;
    }

    public void ConfigureProgressionProfiles(
        IEnumerable<WDWeaponProgressionProfile> profiles)
    {
        progressionProfiles = profiles?.ToList() ??
            new List<WDWeaponProgressionProfile>();
    }
}
