using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "WDStoreCatalogConfig", menuName = "War Dominion/UI/Store Catalog")]
public sealed class WDStoreCatalogConfig : ScriptableObject
{
    // 01. Configuração local/mock: preços e desconto não são economia final.
    [SerializeField] private List<WDStoreProduct> products = new();
    [SerializeField] private List<WDStoreCollection> collections = new();
    [SerializeField, Range(0, 100)] private int vipDiscountPercent = 10;
    [SerializeField, Min(1)] private int mockVipDays = 30;
    public IReadOnlyList<WDStoreProduct> Products => products;
    public IReadOnlyList<WDStoreCollection> Collections => collections;
    public int VipDiscountPercent => Mathf.Clamp(vipDiscountPercent, 0, 100);
    public int MockVipDays => Mathf.Max(1, mockVipDays);
    public WDStoreProduct FindProduct(string id) => products.FirstOrDefault(p => p != null && p.id == id);
    public WDStoreCollection FindCollection(string id) => collections.FirstOrDefault(c => c != null && c.id == id);
    public void Configure(IEnumerable<WDStoreProduct> newProducts, IEnumerable<WDStoreCollection> newCollections,
        int discountPercent = 10, int vipDays = 30)
    {
        products = newProducts?.ToList() ?? new(); collections = newCollections?.ToList() ?? new();
        vipDiscountPercent = Mathf.Clamp(discountPercent, 0, 100); mockVipDays = Mathf.Max(1, vipDays);
    }
    public bool IsValid(WDProfileRepository profiles)
    {
        if (profiles == null || products == null || collections == null) return false;
        var productIds = new HashSet<string>(StringComparer.Ordinal);
        var cosmeticIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (WDStoreProduct p in products)
        {
            if (p == null || string.IsNullOrWhiteSpace(p.id) || !productIds.Add(p.id) ||
                string.IsNullOrWhiteSpace(p.cosmeticId) || !cosmeticIds.Add(p.cosmeticId) || p.price < 0 ||
                !Enum.IsDefined(typeof(WDStoreCurrency), p.currency) || !Enum.IsDefined(typeof(WDStoreKind), p.kind) ||
                !Enum.IsDefined(typeof(WDStoreVipPolicy), p.vipPolicy)) return false;
            WDProfileCosmeticCategory? category = p.kind switch
            {
                WDStoreKind.Avatar => WDProfileCosmeticCategory.Avatar,
                WDStoreKind.Frame => WDProfileCosmeticCategory.Frame,
                WDStoreKind.TerritorySkin => WDProfileCosmeticCategory.TerritorySkin,
                WDStoreKind.Effect => WDProfileCosmeticCategory.Effect,
                _ => null
            };
            if (category.HasValue && !profiles.Cosmetics.Any(c => c.Id == p.cosmeticId && c.Category == category.Value))
                return false;
        }
        var collectionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (WDStoreCollection c in collections)
            if (c == null || string.IsNullOrWhiteSpace(c.id) || !collectionIds.Add(c.id) || c.price < 0 ||
                !Enum.IsDefined(typeof(WDStoreCurrency), c.currency) || c.productIds == null || c.productIds.Count == 0 ||
                c.productIds.Distinct(StringComparer.Ordinal).Count() != c.productIds.Count ||
                c.productIds.Any(id => !productIds.Contains(id))) return false;
        return true;
    }
}
