using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum WDStoreCurrency { Coins, Emeralds }
public enum WDStoreKind { Avatar, Frame, TerritorySkin, Effect, ProfileCustomization, Emoji, Other }
public enum WDStoreVipPolicy { None, RequiredToAcquire, RequiredToUse, GrantedBenefit }
public enum WDStorePurchaseStatus { Success, Unknown, Unavailable, AlreadyOwned, VipRequired, InsufficientFunds, InvalidCatalog }
public enum WDVipBenefit
{
    ExtraDecks, CommanderVoteDouble, DailyCoinsDouble, DailyEmerald,
    CosmeticDiscount, ExclusiveAvatars, VipFrames, ProfileBadge,
    ProfileCustomization, ExclusiveEmoji, MonthlyCosmetic
}

[Serializable]
public sealed class WDStoreProduct
{
    public string id;
    public string title;
    public string description;
    public WDStoreKind kind;
    public string cosmeticId;
    public int price;
    public WDStoreCurrency currency;
    public bool featured;
    public bool available = true;
    public int order;
    public WDStoreVipPolicy vipPolicy;
}

[Serializable]
public sealed class WDStoreCollection
{
    public string id;
    public string title;
    public string description;
    public List<string> productIds = new();
    public int price;
    public WDStoreCurrency currency;
    public bool featured;
    public bool available = true;
    public int order;
}

public interface IWDStoreClock { DateTime UtcNow { get; } }
public sealed class WDSystemStoreClock : IWDStoreClock { public DateTime UtcNow => DateTime.UtcNow; }
public interface IWDVipStateStore
{
    long LoadStartTicks(); long LoadExpirationTicks(); void SavePeriod(long startTicks, long expirationTicks);
}
public sealed class WDPlayerPrefsVipStateStore : IWDVipStateStore
{
    private const string StartKey = "war_dominion.vip.mock_start.v1";
    private const string EndKey = "war_dominion.vip.mock_expiration.v1";
    public long LoadStartTicks() => long.TryParse(PlayerPrefs.GetString(StartKey, "0"), out long ticks) ? ticks : 0;
    public long LoadExpirationTicks() => long.TryParse(PlayerPrefs.GetString(EndKey, "0"), out long ticks) ? ticks : 0;
    public void SavePeriod(long startTicks, long expirationTicks)
    { PlayerPrefs.SetString(StartKey, startTicks.ToString()); PlayerPrefs.SetString(EndKey, expirationTicks.ToString()); PlayerPrefs.Save(); }
}
public sealed class WDVipEntitlement
{
    // 02. Entitlement temporário único; pagamento futuro fica fora desta autoridade.
    private readonly IWDStoreClock clock;
    private readonly IWDVipStateStore store;
    private long startTicks;
    private long expirationTicks;
    public WDVipEntitlement(IWDStoreClock newClock, IWDVipStateStore newStore)
    { clock = newClock ?? throw new ArgumentNullException(nameof(newClock));
      store = newStore ?? throw new ArgumentNullException(nameof(newStore));
      startTicks = store.LoadStartTicks(); expirationTicks = store.LoadExpirationTicks(); }
    public DateTime? StartedUtc => startTicks > 0 ? new DateTime(startTicks, DateTimeKind.Utc) : null;
    public DateTime? ExpiresUtc => expirationTicks > 0 ? new DateTime(expirationTicks, DateTimeKind.Utc) : null;
    public bool IsVipActive => ExpiresUtc.HasValue && ExpiresUtc.Value > clock.UtcNow;
    public TimeSpan Remaining => IsVipActive ? ExpiresUtc.Value - clock.UtcNow : TimeSpan.Zero;
    public void ActivateMock(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        DateTime start = IsVipActive ? ExpiresUtc.Value : clock.UtcNow;
        if (!IsVipActive) startTicks = clock.UtcNow.Ticks;
        expirationTicks = start.Add(duration).Ticks; store.SavePeriod(startTicks, expirationTicks);
    }
    public bool HasBenefit(WDVipBenefit benefit) => Enum.IsDefined(typeof(WDVipBenefit), benefit) && IsVipActive;
    public int DailyCoinMultiplier => HasBenefit(WDVipBenefit.DailyCoinsDouble) ? 2 : 1;
    public int DailyEmeraldBonus => HasBenefit(WDVipBenefit.DailyEmerald) ? 1 : 0;
    public int CommanderVoteMultiplier => HasBenefit(WDVipBenefit.CommanderVoteDouble) ? 2 : 1;
}

public sealed class WDStoreService
{
    private readonly WDStoreCatalogConfig catalog;
    private readonly WDArmoryCatalogConfig armoryCatalog;
    private readonly IWDArmoryRepository wallet;
    private readonly WDProfileRepository profiles;
    public WDVipEntitlement Vip { get; }
    public WDStoreCatalogConfig Catalog => catalog;
    public WDProfileRepository Profiles => profiles;
    public WDStoreService(WDStoreCatalogConfig newCatalog, WDArmoryCatalogConfig newArmoryCatalog,
        IWDArmoryRepository newWallet, WDProfileRepository newProfiles, WDVipEntitlement newVip)
    {
        catalog = newCatalog ?? throw new ArgumentNullException(nameof(newCatalog)); armoryCatalog = newArmoryCatalog;
        wallet = newWallet ?? throw new ArgumentNullException(nameof(newWallet));
        profiles = newProfiles ?? throw new ArgumentNullException(nameof(newProfiles));
        Vip = newVip ?? throw new ArgumentNullException(nameof(newVip));
        RefreshOwnership();
    }
    public WDArmoryProfile Wallet => wallet.Load(armoryCatalog);
    public void RefreshOwnership() => profiles.ApplyPurchasedOwnership(Wallet.OwnedCosmeticIds);
    public void RefreshVipProjection() => wallet.Load(armoryCatalog);
    public bool Owns(WDStoreProduct product) => product != null &&
        (Wallet.OwnsCosmetic(product.cosmeticId) || profiles.Cosmetics.Any(c => c.Id == product.cosmeticId && c.Owned));
    public int EffectivePrice(WDStoreProduct product)
    {
        if (product == null) return 0;
        int percent = Vip.HasBenefit(WDVipBenefit.CosmeticDiscount) ? catalog.VipDiscountPercent : 0;
        return Mathf.Max(0, (int)((long)product.price * (100 - percent) / 100));
    }
    public bool Credit(WDStoreCurrency currency, int amount)
    {
        if (amount < 0 || !Enum.IsDefined(typeof(WDStoreCurrency), currency)) return false;
        WDArmoryProfile state = Wallet;
        long coins = (long)state.Coins + (currency == WDStoreCurrency.Coins ? amount : 0);
        long emeralds = (long)state.Emeralds + (currency == WDStoreCurrency.Emeralds ? amount : 0);
        if (coins > int.MaxValue || emeralds > int.MaxValue) return false;
        state.SetCurrencies((int)coins, (int)emeralds); wallet.Save(state); return true;
    }
    private static bool Debit(WDArmoryProfile state, WDStoreCurrency currency, int price) =>
        price >= 0 && (currency == WDStoreCurrency.Coins ? state.SpendCoins(price) : state.SpendEmeralds(price));
    public WDStorePurchaseStatus BuyProduct(string id)
    {
        if (!catalog.IsValid(profiles)) return WDStorePurchaseStatus.InvalidCatalog;
        WDStoreProduct product = catalog.FindProduct(id);
        if (product == null) return WDStorePurchaseStatus.Unknown;
        if (!product.available) return WDStorePurchaseStatus.Unavailable;
        WDArmoryProfile state = Wallet;
        if (state.OwnsCosmetic(product.cosmeticId) || profiles.Cosmetics.Any(c => c.Id == product.cosmeticId && c.Owned))
            return WDStorePurchaseStatus.AlreadyOwned;
        if (product.vipPolicy == WDStoreVipPolicy.RequiredToAcquire && !Vip.IsVipActive)
            return WDStorePurchaseStatus.VipRequired;
        if (!Debit(state, product.currency, EffectivePrice(product))) return WDStorePurchaseStatus.InsufficientFunds;
        state.AddOwnedCosmetic(product.cosmeticId); wallet.Save(state);
        profiles.ApplyPurchasedOwnership(state.OwnedCosmeticIds); return WDStorePurchaseStatus.Success;
    }
    public WDStorePurchaseStatus BuyCollection(string id)
    {
        if (!catalog.IsValid(profiles)) return WDStorePurchaseStatus.InvalidCatalog;
        WDStoreCollection collection = catalog.FindCollection(id);
        if (collection == null) return WDStorePurchaseStatus.Unknown;
        if (!collection.available) return WDStorePurchaseStatus.Unavailable;
        WDStoreProduct[] products = collection.productIds.Select(catalog.FindProduct).ToArray();
        if (products.Any(p => !p.available)) return WDStorePurchaseStatus.Unavailable;
        WDArmoryProfile state = Wallet;
        WDStoreProduct[] missing = products.Where(p => !state.OwnsCosmetic(p.cosmeticId) &&
            !profiles.Cosmetics.Any(c => c.Id == p.cosmeticId && c.Owned)).ToArray();
        if (missing.Length == 0) return WDStorePurchaseStatus.AlreadyOwned;
        if (missing.Any(p => p.vipPolicy == WDStoreVipPolicy.RequiredToAcquire) && !Vip.IsVipActive)
            return WDStorePurchaseStatus.VipRequired;
        // 03. Bundle mock: preço fixo se faltar qualquer item. Um único Save concede todos os ausentes.
        if (!Debit(state, collection.currency, collection.price)) return WDStorePurchaseStatus.InsufficientFunds;
        foreach (WDStoreProduct product in missing) state.AddOwnedCosmetic(product.cosmeticId);
        wallet.Save(state); profiles.ApplyPurchasedOwnership(state.OwnedCosmeticIds);
        return WDStorePurchaseStatus.Success;
    }
}
