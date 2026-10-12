using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class WDArmoryTests
{
    private WDArmoryCatalogConfig catalog;
    private WDArmoryProfile profile;
    private WDArmoryService service;

    [SetUp]
    public void SetUp()
    {
        catalog = ScriptableObject.CreateInstance<WDArmoryCatalogConfig>();
        catalog.Configure(
            new[]
            {
                new WDWeaponCategoryDefinition("support", "Apoio", true),
                new WDWeaponCategoryDefinition("tactical", "Tática", false)
            },
            Enumerable.Range(0, 12).Select(index => Weapon(
                $"weapon_{index}", index < 3 ? "support" : "tactical")));
        profile = new WDArmoryProfile(false, 1000, 25);
        foreach (WDWeaponDefinition weapon in catalog.Weapons)
            profile.AddProgress(new WDWeaponProgress(weapon.Id, weapon.Id != "weapon_11", 1, 100));
        service = new WDArmoryService(catalog, profile);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(catalog);

    // ============================================================
    // 01. SLOTS PERSISTENTES E ACESSO VIP
    // ============================================================

    [Test]
    public void NonVipHasOnlyTwoUsableDecks()
    {
        Assert.That(profile.Decks, Has.Count.EqualTo(5));
        Assert.That(Enumerable.Range(0, 5).Count(profile.IsDeckUnlocked), Is.EqualTo(2));
    }

    [Test]
    public void VipHasFiveUsableDecks()
    {
        service.SetVipForDevelopment(true);
        Assert.That(Enumerable.Range(0, 5).All(profile.IsDeckUnlocked), Is.True);
    }

    [Test]
    public void LockedDeckRejectsEditing()
    {
        Assert.That(service.AssignMain(2, 0, "weapon_0"), Is.EqualTo(WDArmoryEditResult.LockedDeck));
        Assert.That(service.RenameDeck(2, "Bloqueado"), Is.EqualTo(WDArmoryEditResult.LockedDeck));
    }

    [Test]
    public void DeckNameUsesSharedTwentyFourCharacterLimit()
    {
        Assert.That(service.RenameDeck(0, new string('D', 24)), Is.EqualTo(WDArmoryEditResult.Success));
        Assert.That(profile.GetDeck(0).Name, Is.EqualTo(new string('D', 24)));
        Assert.That(service.RenameDeck(0, new string('X', 25)), Is.EqualTo(WDArmoryEditResult.InvalidName));
        Assert.That(profile.GetDeck(0).Name, Is.EqualTo(new string('D', 24)));
    }

    [Test]
    public void VipExpirationLocksWithoutDeletingComposition()
    {
        service.SetVipForDevelopment(true);
        Assert.That(service.AssignMain(4, 0, "weapon_0"), Is.EqualTo(WDArmoryEditResult.Success));
        service.SetVipForDevelopment(false);

        Assert.That(profile.IsDeckUnlocked(4), Is.False);
        Assert.That(profile.GetDeck(4).GetMain(0), Is.EqualTo("weapon_0"));

        service.SetVipForDevelopment(true);
        Assert.That(profile.GetDeck(4).GetMain(0), Is.EqualTo("weapon_0"));
    }

    [Test]
    public void FiveDeckCompositionsRemainIndependent()
    {
        service.SetVipForDevelopment(true);
        for (int i = 0; i < 5; i++)
            Assert.That(service.AssignMain(i, 0, $"weapon_{i}"), Is.EqualTo(WDArmoryEditResult.Success));

        Assert.That(profile.Decks.Select(deck => deck.GetMain(0)).Distinct().Count(), Is.EqualTo(5));
    }

    [Test]
    public void BlockedDeckRequirementKeepsArmoryRouteAndDeckData()
    {
        var navigation = new WDArmoryVipNavigation();
        string nameBefore = profile.GetDeck(2).Name;
        string[] mainBefore = profile.GetDeck(2).MainWeaponIds.ToArray();

        Assert.That(service.AssignMain(2, 0, "weapon_0"), Is.EqualTo(WDArmoryEditResult.LockedDeck));
        navigation.OpenRequirementModal();

        Assert.That(navigation.IsRequirementModalOpen, Is.True);
        Assert.That(navigation.Destination, Is.EqualTo(WDHomeDestination.Armory));
        Assert.That(profile.GetDeck(2).Name, Is.EqualTo(nameBefore));
        Assert.That(profile.GetDeck(2).MainWeaponIds, Is.EqualTo(mainBefore));
    }

    [Test]
    public void ClosingVipRequirementKeepsArmoryAsCurrentDestination()
    {
        var navigation = new WDArmoryVipNavigation();
        navigation.OpenRequirementModal();
        navigation.CloseRequirementModal();

        Assert.That(navigation.IsRequirementModalOpen, Is.False);
        Assert.That(navigation.Destination, Is.EqualTo(WDHomeDestination.Armory));
    }

    [Test]
    public void VipRequirementActionRoutesDirectlyToStoreVip()
    {
        var navigation = new WDArmoryVipNavigation();
        navigation.OpenRequirementModal();
        navigation.NavigateToStoreVip();

        Assert.That(navigation.IsRequirementModalOpen, Is.False);
        Assert.That(navigation.Destination, Is.EqualTo(WDHomeDestination.StoreVip));
    }

    // ============================================================
    // 02. COMPOSIÇÃO 8 + 2 E ELEGIBILIDADE
    // ============================================================

    [Test]
    public void DeckHasExactlyEightMainAndTwoSupportSlots()
    {
        Assert.That(profile.GetDeck(0).MainWeaponIds.Count, Is.EqualTo(8));
        Assert.That(profile.GetDeck(0).SupportWeaponIds.Count, Is.EqualTo(2));
    }

    [Test]
    public void MainSlotsRejectNinthIndex()
    {
        Assert.That(service.AssignMain(0, 8, "weapon_0"), Is.EqualTo(WDArmoryEditResult.InvalidSlot));
    }

    [Test]
    public void MainWeaponCannotDuplicateInSameDeck()
    {
        Assert.That(service.AssignMain(0, 0, "weapon_3"), Is.EqualTo(WDArmoryEditResult.Success));
        Assert.That(service.AssignMain(0, 1, "weapon_3"), Is.EqualTo(WDArmoryEditResult.DuplicateWeapon));
    }

    [Test]
    public void SupportWeaponsCannotRepeatEachOther()
    {
        Assert.That(service.AssignSupport(0, 0, "weapon_0"), Is.EqualTo(WDArmoryEditResult.Success));
        Assert.That(service.AssignSupport(0, 1, "weapon_0"), Is.EqualTo(WDArmoryEditResult.DuplicateWeapon));
    }

    [Test]
    public void SupportCannotRepeatMainWeapon()
    {
        Assert.That(service.AssignMain(0, 0, "weapon_0"), Is.EqualTo(WDArmoryEditResult.Success));
        Assert.That(service.AssignSupport(0, 0, "weapon_0"), Is.EqualTo(WDArmoryEditResult.DuplicateWeapon));
    }

    [Test]
    public void NonSupportCategoryCannotOccupySupportSlot()
    {
        Assert.That(service.AssignSupport(0, 0, "weapon_3"), Is.EqualTo(WDArmoryEditResult.NotSupportEligible));
    }

    [Test]
    public void NotObtainedWeaponCannotBeAdded()
    {
        Assert.That(service.AssignMain(0, 0, "weapon_11"), Is.EqualTo(WDArmoryEditResult.WeaponNotObtained));
    }

    [Test]
    public void DevelopmentBootstrapCompletesExistingCanonicalCollectionWithoutChangingDecks()
    {
        catalog.Configure(catalog.Categories, catalog.Weapons,
            developmentUnlockAllWeapons: true);
        var existingProfile = new WDArmoryProfile();
        existingProfile.AddProgress(new WDWeaponProgress("legacy_weapon", true));
        existingProfile.GetDeck(0).SetMain(0, "weapon_0");
        string[] deckBefore = existingProfile.GetDeck(0).MainWeaponIds.ToArray();

        Assert.That(WDArmoryDevelopmentBootstrap.Apply(catalog, existingProfile), Is.True);

        Assert.That(catalog.Weapons.All(weapon =>
            existingProfile.GetProgress(weapon.Id)?.Obtained == true), Is.True);
        Assert.That(existingProfile.GetDeck(0).MainWeaponIds, Is.EqualTo(deckBefore));
        Assert.That(WDArmoryDevelopmentBootstrap.Apply(catalog, existingProfile), Is.False);
    }

    [Test]
    public void TwoSupportUsesAreIndependentAndLimitedToOneEach()
    {
        var usage = new WDMatchSupportUsage();
        Assert.That(usage.TryConsume(0), Is.True);
        Assert.That(usage.TryConsume(0), Is.False);
        Assert.That(usage.TryConsume(1), Is.True);
        Assert.That(usage.TryConsume(1), Is.False);
    }

    // ============================================================
    // 03. NÍVEIS, TOKENS E MOEDAS
    // ============================================================

    [Test]
    public void UpgradeNeverExceedsLevelSixAndLevelSixIsMax()
    {
        WDWeaponProgress progress = profile.GetProgress("weapon_0");
        progress.SetProgress(true, 6, 999);

        Assert.That(service.GetCollectionState("weapon_0"), Is.EqualTo(WDWeaponCollectionState.MaxLevel));
        Assert.That(service.Upgrade("weapon_0"), Is.EqualTo(WDArmoryEditResult.AlreadyMaxLevel));
        Assert.That(progress.Level, Is.EqualTo(6));
    }

    [Test]
    public void UpgradeRejectsInsufficientTokens()
    {
        profile.GetProgress("weapon_0").SetProgress(true, 1, 4);
        Assert.That(service.Upgrade("weapon_0"), Is.EqualTo(WDArmoryEditResult.InsufficientTokens));
    }

    [Test]
    public void UpgradeRejectsInsufficientCoins()
    {
        profile.SetCurrencies(49, profile.Emeralds);
        Assert.That(service.Upgrade("weapon_0"), Is.EqualTo(WDArmoryEditResult.InsufficientCoins));
    }

    [Test]
    public void UpgradeConsumesConfiguredTokensAndCoins()
    {
        WDWeaponProgress progress = profile.GetProgress("weapon_0");
        int coinsBefore = profile.Coins;
        int tokensBefore = progress.Tokens;

        Assert.That(service.Upgrade("weapon_0"), Is.EqualTo(WDArmoryEditResult.Success));
        Assert.That(progress.Level, Is.EqualTo(2));
        Assert.That(progress.Tokens, Is.EqualTo(tokensBefore - 10));
        Assert.That(profile.Coins, Is.EqualTo(coinsBefore - 50));
    }

    private static WDWeaponDefinition Weapon(string id, string categoryId) =>
        new(id, id, categoryId, Enumerable.Range(2, 5).Select(level =>
            new WDWeaponLevelDefinition(level, level == 2 ? 10 : level * 20, level == 2 ? 50 : level * 100)));
}
