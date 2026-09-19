using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class WDStoreTests
{
    private sealed class Clock : IWDStoreClock { public DateTime UtcNow { get; set; } = new(2026, 9, 19, 12, 0, 0, DateTimeKind.Utc); }
    private sealed class VipStore : IWDVipStateStore
    { public long Start; public long Ticks; public long LoadStartTicks() => Start;
      public long LoadExpirationTicks() => Ticks;
      public void SavePeriod(long startTicks,long expirationTicks) { Start=startTicks; Ticks=expirationTicks; } }
    private sealed class WalletRepository : IWDArmoryRepository
    {
        public WDArmoryProfile State = new(false, 1000, 100);
        public int Saves;
        public WDArmoryProfile Load(WDArmoryCatalogConfig _) => State;
        public void Save(WDArmoryProfile profile) { State=profile; Saves++; }
    }
    private WDStoreCatalogConfig catalog;
    private WDProfileRepository profiles;
    private WalletRepository wallet;
    private Clock clock;
    private VipStore vipStore;
    private WDVipEntitlement vip;
    private WDStoreService service;
    private WDStoreProduct avatar, frame, skin;
    private WDStoreCollection collection;

    [SetUp]
    public void SetUp()
    {
        catalog=ScriptableObject.CreateInstance<WDStoreCatalogConfig>();
        profiles=new WDProfileRepository(null,new WDMemoryProfilePrivacyStore());
        wallet=new WalletRepository();clock=new Clock();vipStore=new VipStore();vip=new WDVipEntitlement(clock,vipStore);
        avatar=new WDStoreProduct { id="avatar",title="Avatar",kind=WDStoreKind.Avatar,cosmeticId="avatar_05",
            price=20,currency=WDStoreCurrency.Emeralds,available=true,featured=true };
        frame=new WDStoreProduct { id="frame",title="Moldura",kind=WDStoreKind.Frame,cosmeticId="frame_05",
            price=100,currency=WDStoreCurrency.Coins,available=true };
        skin=new WDStoreProduct { id="skin",title="Skin",kind=WDStoreKind.TerritorySkin,cosmeticId="skin_05",
            price=100,currency=WDStoreCurrency.Coins,available=true };
        collection=new WDStoreCollection { id="bundle",title="Bundle",productIds=new List<string>{"frame","skin"},
            price=150,currency=WDStoreCurrency.Coins,available=true };
        catalog.Configure(new[]{avatar,frame,skin},new[]{collection},20,7);
        service=new WDStoreService(catalog,null,wallet,profiles,vip);
    }
    [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(catalog);

    // 01. Catálogo e carteira mock.
    [Test] public void ValidCatalogHasCanonicalReferences() => Assert.That(catalog.IsValid(profiles),Is.True);
    [Test] public void ResourceCatalogHasValidCanonicalProducts()
    { WDStoreCatalogConfig resource=Resources.Load<WDStoreCatalogConfig>("UI/Home/WDStoreCatalogConfig");
      Assert.That(resource,Is.Not.Null); Assert.That(resource.IsValid(profiles),Is.True); }
    [Test] public void DuplicateProductIdInvalidatesCatalog()
    { catalog.Configure(new[]{avatar,avatar},new[]{collection}); Assert.That(catalog.IsValid(profiles),Is.False); }
    [Test] public void UnknownCosmeticInvalidatesCatalog()
    { avatar.cosmeticId="absent"; Assert.That(catalog.IsValid(profiles),Is.False); }
    [Test] public void NegativePriceInvalidatesCatalog()
    { avatar.price=-1; Assert.That(catalog.IsValid(profiles),Is.False); }
    [Test] public void InvalidCurrencyInvalidatesCatalog()
    { avatar.currency=(WDStoreCurrency)99; Assert.That(catalog.IsValid(profiles),Is.False); }
    [Test] public void MissingBundleReferenceInvalidatesCatalog()
    { collection.productIds.Add("absent"); Assert.That(catalog.IsValid(profiles),Is.False); }
    [Test] public void DuplicateBundleReferenceInvalidatesCatalog()
    { collection.productIds.Add("skin"); Assert.That(catalog.IsValid(profiles),Is.False); }
    [Test] public void InitialWalletIsSharedArmoryProfile()
    { Assert.That(service.Wallet,Is.SameAs(wallet.State)); Assert.That(service.Wallet.Coins,Is.EqualTo(1000)); }
    [Test] public void CreditCoinsDoesNotChangeEmeralds()
    { service.Credit(WDStoreCurrency.Coins,10); Assert.That(wallet.State.Coins,Is.EqualTo(1010)); Assert.That(wallet.State.Emeralds,Is.EqualTo(100)); }
    [Test] public void NegativeCreditIsRejected()
    { Assert.That(service.Credit(WDStoreCurrency.Coins,-1),Is.False); Assert.That(wallet.Saves,Is.Zero); }
    [Test] public void OverflowCreditIsRejected()
    { wallet.State.SetCurrencies(int.MaxValue,100); Assert.That(service.Credit(WDStoreCurrency.Coins,1),Is.False); }

    // 02. Compras: débito + concessão em um único Save; equipar continua no Perfil.
    [Test] public void AvatarPurchaseDebitsEmeraldsAndProjectsOwnership()
    { Assert.That(service.BuyProduct("avatar"),Is.EqualTo(WDStorePurchaseStatus.Success));
      Assert.That(wallet.State.Emeralds,Is.EqualTo(80));
      Assert.That(profiles.Cosmetics.Single(c=>c.Id=="avatar_05").Owned,Is.True);
      Assert.That(wallet.Saves,Is.EqualTo(1)); }
    [Test] public void CoinPurchaseDoesNotDebitEmeralds()
    { service.BuyProduct("frame"); Assert.That(wallet.State.Coins,Is.EqualTo(900)); Assert.That(wallet.State.Emeralds,Is.EqualTo(100)); }
    [Test] public void RepeatPurchaseNeverChargesTwice()
    { service.BuyProduct("avatar"); Assert.That(service.BuyProduct("avatar"),Is.EqualTo(WDStorePurchaseStatus.AlreadyOwned));
      Assert.That(wallet.State.Emeralds,Is.EqualTo(80)); Assert.That(wallet.Saves,Is.EqualTo(1)); }
    [Test] public void InsufficientFundsNeverGrants()
    { wallet.State.SetCurrencies(1000,0); Assert.That(service.BuyProduct("avatar"),Is.EqualTo(WDStorePurchaseStatus.InsufficientFunds));
      Assert.That(wallet.State.OwnsCosmetic("avatar_05"),Is.False); Assert.That(wallet.Saves,Is.Zero); }
    [Test] public void UnknownProductNeverCharges()
    { Assert.That(service.BuyProduct("missing"),Is.EqualTo(WDStorePurchaseStatus.Unknown)); Assert.That(wallet.Saves,Is.Zero); }
    [Test] public void UnavailableProductNeverCharges()
    { avatar.available=false; Assert.That(service.BuyProduct("avatar"),Is.EqualTo(WDStorePurchaseStatus.Unavailable)); Assert.That(wallet.Saves,Is.Zero); }
    [Test] public void NonVipUsesFullPrice()
    { Assert.That(service.EffectivePrice(avatar),Is.EqualTo(20)); }
    [Test] public void ActiveVipUsesConfiguredDiscount()
    { vip.ActivateMock(TimeSpan.FromDays(7)); Assert.That(service.EffectivePrice(avatar),Is.EqualTo(16));
      service.BuyProduct("avatar"); Assert.That(wallet.State.Emeralds,Is.EqualTo(84)); }
    [Test] public void DiscountCannotProduceNegativePrice()
    { catalog.Configure(new[]{avatar,frame,skin},new[]{collection},100); vip.ActivateMock(TimeSpan.FromDays(1));
      Assert.That(service.EffectivePrice(avatar),Is.Zero); }
    [Test] public void VipRequiredProductBlocksUntilActivation()
    { avatar.vipPolicy=WDStoreVipPolicy.RequiredToAcquire;
      Assert.That(service.BuyProduct("avatar"),Is.EqualTo(WDStorePurchaseStatus.VipRequired));
      vip.ActivateMock(TimeSpan.FromDays(1)); Assert.That(service.BuyProduct("avatar"),Is.EqualTo(WDStorePurchaseStatus.Success)); }
    [Test] public void PurchaseDoesNotEquipAvatar()
    { string before=profiles.LocalProfile.Equipped[WDProfileCosmeticCategory.Avatar];
      service.BuyProduct("avatar"); Assert.That(profiles.LocalProfile.Equipped[WDProfileCosmeticCategory.Avatar],Is.EqualTo(before)); }
    [Test] public void StorePurchaseBecomesEquipableInCanonicalCustomization()
    {
        string before=profiles.LocalProfile.Equipped[WDProfileCosmeticCategory.Avatar];
        Assert.That(service.BuyProduct("avatar"),Is.EqualTo(WDStorePurchaseStatus.Success));
        WDProfileCosmetic acquired=profiles.Cosmetics.Single(c=>c.Id=="avatar_05");
        Assert.That(acquired.Owned,Is.True);
        Assert.That(profiles.LocalProfile.Equipped[WDProfileCosmeticCategory.Avatar],Is.EqualTo(before));
        var customization=new WDProfileCustomizationSession(profiles.LocalProfile,profiles.Cosmetics);
        Assert.That(customization.PreviewItem(acquired.Id),Is.True);
        Assert.That(customization.Save(),Is.True);
        Assert.That(profiles.LocalProfile.Equipped[WDProfileCosmeticCategory.Avatar],Is.EqualTo(acquired.Id));
    }

    // 03. Bundle mock de preço fixo, inclusive com ownership parcial.
    [Test] public void BundlePurchaseGrantsAllMissingOnce()
    { Assert.That(service.BuyCollection("bundle"),Is.EqualTo(WDStorePurchaseStatus.Success));
      Assert.That(wallet.State.OwnsCosmetic("frame_05"),Is.True); Assert.That(wallet.State.OwnsCosmetic("skin_05"),Is.True);
      Assert.That(wallet.State.Coins,Is.EqualTo(850)); Assert.That(wallet.Saves,Is.EqualTo(1)); }
    [Test] public void PartiallyOwnedBundleStillUsesFixedMockPrice()
    { wallet.State.AddOwnedCosmetic("frame_05"); service.BuyCollection("bundle");
      Assert.That(wallet.State.Coins,Is.EqualTo(850)); Assert.That(wallet.State.OwnedCosmeticIds.Count,Is.EqualTo(2)); }
    [Test] public void CompleteBundleCannotChargeAgain()
    { service.BuyCollection("bundle"); Assert.That(service.BuyCollection("bundle"),Is.EqualTo(WDStorePurchaseStatus.AlreadyOwned));
      Assert.That(wallet.State.Coins,Is.EqualTo(850)); Assert.That(wallet.Saves,Is.EqualTo(1)); }
    [Test] public void BundleInsufficientFundsLeavesEverythingUnchanged()
    { wallet.State.SetCurrencies(0,100); Assert.That(service.BuyCollection("bundle"),Is.EqualTo(WDStorePurchaseStatus.InsufficientFunds));
      Assert.That(wallet.State.OwnedCosmeticIds,Is.Empty); Assert.That(wallet.Saves,Is.Zero); }
    [Test] public void BundleInvalidCatalogCannotPartiallyGrant()
    { collection.productIds.Add("missing"); Assert.That(service.BuyCollection("bundle"),Is.EqualTo(WDStorePurchaseStatus.InvalidCatalog));
      Assert.That(wallet.State.OwnedCosmeticIds,Is.Empty); Assert.That(wallet.Saves,Is.Zero); }

    // 04. VIP temporário e benefícios consultáveis.
    [Test] public void VipStartsInactiveAndExpires()
    { Assert.That(vip.IsVipActive,Is.False); vip.ActivateMock(TimeSpan.FromDays(1));
      clock.UtcNow=clock.UtcNow.AddDays(1); Assert.That(vip.IsVipActive,Is.False); }
    [Test] public void VipRenewalExtendsFromExistingExpiration()
    { vip.ActivateMock(TimeSpan.FromDays(1)); DateTime first=vip.ExpiresUtc.Value;
      vip.ActivateMock(TimeSpan.FromDays(1)); Assert.That(vip.ExpiresUtc.Value,Is.EqualTo(first.AddDays(1))); }
    [Test] public void VipExpirationPersistsAcrossServiceRecreation()
    { vip.ActivateMock(TimeSpan.FromDays(1)); var restored=new WDVipEntitlement(clock,vipStore);
      Assert.That(restored.IsVipActive,Is.True); Assert.That(restored.ExpiresUtc,Is.EqualTo(vip.ExpiresUtc));
      Assert.That(restored.StartedUtc,Is.EqualTo(vip.StartedUtc)); }
    [Test] public void AllElevenBenefitsFollowActiveEntitlement()
    { foreach(WDVipBenefit benefit in Enum.GetValues(typeof(WDVipBenefit))) Assert.That(vip.HasBenefit(benefit),Is.False);
      vip.ActivateMock(TimeSpan.FromDays(1));
      foreach(WDVipBenefit benefit in Enum.GetValues(typeof(WDVipBenefit))) Assert.That(vip.HasBenefit(benefit),Is.True); }
    [Test] public void FutureMultipliersAreQueriesNotSchedulers()
    { Assert.That(vip.DailyCoinMultiplier,Is.EqualTo(1)); vip.ActivateMock(TimeSpan.FromDays(1));
      Assert.That(vip.DailyCoinMultiplier,Is.EqualTo(2)); Assert.That(vip.DailyEmeraldBonus,Is.EqualTo(1));
      Assert.That(vip.CommanderVoteMultiplier,Is.EqualTo(2)); }
    [Test] public void FreeDecksRemainAvailableAndExpiredVipDecksLockWithoutDeletingComposition()
    { wallet.State.SetVip(true); wallet.State.GetDeck(2).Rename("VIP DECK");
      Assert.That(wallet.State.IsDeckUnlocked(2),Is.True);
      wallet.State.SetVip(false); wallet.State.Normalize();
      Assert.That(wallet.State.IsDeckUnlocked(2),Is.False);
      Assert.That(wallet.State.IsDeckUnlocked(0),Is.True);
      Assert.That(wallet.State.GetDeck(2).Name,Is.EqualTo("VIP DECK")); }
    [Test] public void RepositoryProjectsEntitlementOntoArmoryWithoutDeletingVipDeck()
    {
        const string key="war_dominion.armory.profile.v1";
        bool existed=PlayerPrefs.HasKey(key); string backup=existed?PlayerPrefs.GetString(key):null;
        try
        {
            var repository=new WDPlayerPrefsArmoryRepository(vip);
            var state=new WDArmoryProfile(false,100,10);
            state.GetDeck(2).Rename("COMPOSIÇÃO PRESERVADA");
            repository.Save(state);
            Assert.That(repository.Load(null).IsDeckUnlocked(2),Is.False);
            vip.ActivateMock(TimeSpan.FromDays(1));
            Assert.That(repository.Load(null).IsDeckUnlocked(2),Is.True);
            clock.UtcNow=clock.UtcNow.AddDays(1);
            WDArmoryProfile expired=repository.Load(null);
            Assert.That(expired.IsDeckUnlocked(2),Is.False);
            Assert.That(expired.IsDeckUnlocked(0),Is.True);
            Assert.That(expired.GetDeck(2).Name,Is.EqualTo("COMPOSIÇÃO PRESERVADA"));
        }
        finally
        {
            if(existed) PlayerPrefs.SetString(key,backup); else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }
}
