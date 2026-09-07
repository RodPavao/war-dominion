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
    [SerializeField] private bool vipEnabledForDevelopment;

    public IReadOnlyList<WDWeaponCategoryDefinition> Categories => categories;
    public IReadOnlyList<WDWeaponDefinition> Weapons => weapons;
    public bool VipEnabledForDevelopment => vipEnabledForDevelopment;

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

    public void Configure(
        IEnumerable<WDWeaponCategoryDefinition> newCategories,
        IEnumerable<WDWeaponDefinition> newWeapons,
        bool developmentVip = false)
    {
        categories = newCategories?.ToList() ?? new List<WDWeaponCategoryDefinition>();
        weapons = newWeapons?.ToList() ?? new List<WDWeaponDefinition>();
        vipEnabledForDevelopment = developmentVip;
    }
}
