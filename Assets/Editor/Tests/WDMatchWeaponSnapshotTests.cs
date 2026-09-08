using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class WDMatchWeaponSnapshotTests
{
    private WDArmoryCatalogConfig catalog;
    private WDRandomCardsConfig randomConfig;

    [SetUp]
    public void SetUp()
    {
        catalog = Resources.Load<WDArmoryCatalogConfig>(
            "UI/Home/WDArmoryCatalogConfig");
        randomConfig = Resources.Load<WDRandomCardsConfig>(
            "Cartas/WDRandomCardsConfig");
        Assert.That(catalog, Is.Not.Null);
        Assert.That(randomConfig, Is.Not.Null);
    }

    // ============================================================
    // 01. IDENTIDADE CANÔNICA DO RANDOM CARDS
    // ============================================================

    [Test]
    public void RandomPoolContainsAllCanonicalWeaponsExceptUniversalEnergyPlant()
    {
        string[] pool = randomConfig.Cards.Select(card => card.CardId).ToArray();
        string[] expected = catalog.Weapons.Select(weapon => weapon.Id)
            .Where(id => id != WDMatchWeaponSnapshotFactory.UniversalEnergyPlantId)
            .ToArray();

        Assert.That(pool, Has.Length.EqualTo(24));
        Assert.That(pool, Is.EquivalentTo(expected));
        Assert.That(pool, Has.None.StartsWith("card_"));
    }

    [Test]
    public void RandomBasicPoolContainsExactlyTheEightApprovedWeapons()
    {
        Assert.That(randomConfig.Cards.Where(card => card.Basic)
            .Select(card => card.CardId), Is.EquivalentTo(new[]
        {
            "air_strike", "reinforcements", "minefield", "blitzkrieg",
            "transfer_nullifier", "sniper", "air_transport", "bunker"
        }));
    }

    [Test]
    public void RandomSnapshotUsesPermanentLevelOrTemporaryLevelOneWithoutMutation()
    {
        WDArmoryProfile profile = ProfileWithAllWeapons(false);
        profile.GetProgress("artillery").SetProgress(true, 4, 27);
        profile.GetProgress("drone").SetProgress(false, 6, 13);

        WDMatchWeaponLoadoutSnapshot snapshot =
            WDMatchWeaponSnapshotFactory.CreateRandomCards(catalog, profile);

        Assert.That(snapshot.TryGetLevel("artillery", out int owned), Is.True);
        Assert.That(owned, Is.EqualTo(4));
        Assert.That(snapshot.TryGetLevel("drone", out int temporary), Is.True);
        Assert.That(temporary, Is.EqualTo(1));
        Assert.That(profile.GetProgress("drone").Obtained, Is.False);
        Assert.That(profile.GetProgress("drone").Level, Is.EqualTo(6));
        Assert.That(profile.GetProgress("drone").Tokens, Is.EqualTo(13));
    }

    [TestCase(true, 4)]
    [TestCase(false, 1)]
    public void UniversalEnergyPlantUsesPermanentOrTemporaryLevelOne(
        bool obtained, int expectedLevel)
    {
        WDArmoryProfile profile = ProfileWithAllWeapons(false);
        profile.GetProgress("energy_plant").SetProgress(obtained, 4, 9);

        WDMatchWeaponLoadoutSnapshot snapshot =
            WDMatchWeaponSnapshotFactory.CreateRandomCards(catalog, profile);

        Assert.That(snapshot.UniversalWeapons.Single().WeaponId,
            Is.EqualTo("energy_plant"));
        Assert.That(snapshot.TryGetLevel("energy_plant", out int level), Is.True);
        Assert.That(level, Is.EqualTo(expectedLevel));
        Assert.That(profile.GetProgress("energy_plant").Obtained, Is.EqualTo(obtained));
    }

    // ============================================================
    // 02. SNAPSHOT NORMAL 8+2 E VALIDAÇÃO EXPLÍCITA
    // ============================================================

    [Test]
    public void NormalSnapshotContainsEightMainTwoSupportAndUniversalEnergyPlant()
    {
        WDArmoryProfile profile = ValidNormalProfile(out int deckIndex);
        WDMatchWeaponLoadoutSnapshot snapshot =
            WDMatchWeaponSnapshotFactory.CreateNormal(
                "deck_1", deckIndex, catalog, profile);

        Assert.That(snapshot.Status, Is.EqualTo(WDMatchWeaponSnapshotStatus.Valid));
        Assert.That(snapshot.MainSlots, Has.Count.EqualTo(8));
        Assert.That(snapshot.SupportSlots, Has.Count.EqualTo(2));
        Assert.That(snapshot.UniversalWeapons, Has.Count.EqualTo(1));
        Assert.That(snapshot.GetNormalWeaponIds().Count, Is.EqualTo(11));
        Assert.That(snapshot.MainSlots.Concat(snapshot.SupportSlots)
            .Any(slot => slot.WeaponId == "energy_plant"), Is.False);
    }

    [Test]
    public void NormalSnapshotKeepsPermanentLevelsAndDoesNotFollowLaterProfileChanges()
    {
        WDArmoryProfile profile = ValidNormalProfile(out int deckIndex);
        profile.GetProgress("artillery").SetProgress(true, 4, 0);
        WDMatchWeaponLoadoutSnapshot snapshot =
            WDMatchWeaponSnapshotFactory.CreateNormal(
                "deck_1", deckIndex, catalog, profile);

        profile.GetProgress("artillery").SetProgress(true, 1, 99);

        Assert.That(snapshot.TryGetLevel("artillery", out int level), Is.True);
        Assert.That(level, Is.EqualTo(4));
    }

    [Test]
    public void SeparateParticipantsReceiveIndependentSnapshotObjects()
    {
        WDArmoryProfile profile = ValidNormalProfile(out int deckIndex);
        WDMatchWeaponLoadoutSnapshot first = WDMatchWeaponSnapshotFactory.CreateNormal(
            "deck_1", deckIndex, catalog, profile);
        WDMatchWeaponLoadoutSnapshot second = WDMatchWeaponSnapshotFactory.CreateNormal(
            "deck_1", deckIndex, catalog, profile);

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.MainSlots, Is.Not.SameAs(second.MainSlots));
        Assert.That(first.GetNormalWeaponIds(), Is.EqualTo(second.GetNormalWeaponIds()));
    }

    [Test]
    public void InvalidOrLockedDeckIsReportedWithoutFallback()
    {
        WDArmoryProfile profile = ValidNormalProfile(out _);

        Assert.That(WDMatchWeaponSnapshotFactory.CreateNormal(
            "missing", 99, catalog, profile).Status,
            Is.EqualTo(WDMatchWeaponSnapshotStatus.InvalidDeck));
        Assert.That(WDMatchWeaponSnapshotFactory.CreateNormal(
            "deck_3", 2, catalog, profile).Status,
            Is.EqualTo(WDMatchWeaponSnapshotStatus.LockedDeck));
    }

    [Test]
    public void UniversalEnergyPlantRemainsAvailableWhenNormalDeckIsInvalid()
    {
        WDArmoryProfile profile = ProfileWithAllWeapons(false);
        WDMatchWeaponLoadoutSnapshot snapshot = WDMatchWeaponSnapshotFactory.CreateNormal(
            "missing", 99, catalog, profile);
        WDMatchSetup setup = Setup(snapshot, false);
        var runtime = new WDWeaponMatchRuntime(
            catalog, new[] { "p1" },
            new WDMatchSetupWeaponLoadoutSource(setup, _ => new string[0]),
            new WDMatchSetupWeaponLevelSource(setup));

        runtime.RefreshLoadouts();

        Assert.That(snapshot.Status, Is.EqualTo(WDMatchWeaponSnapshotStatus.InvalidDeck));
        Assert.That(runtime.GetAvailability("p1", "energy_plant").IsAvailable, Is.True);
        Assert.That(runtime.GetAvailability("p1", "artillery").Reason,
            Is.EqualTo(WDWeaponAvailabilityReason.NotInLoadout));
    }

    [Test]
    public void UnobtainedNormalWeaponIsRejectedInsteadOfReceivingTemporaryLevel()
    {
        WDArmoryProfile profile = ValidNormalProfile(out int deckIndex);
        profile.GetProgress("artillery").SetProgress(false, 4, 0);

        WDMatchWeaponLoadoutSnapshot snapshot = WDMatchWeaponSnapshotFactory.CreateNormal(
            "deck_1", deckIndex, catalog, profile);

        Assert.That(snapshot.Status,
            Is.EqualTo(WDMatchWeaponSnapshotStatus.WeaponNotObtained));
        Assert.That(snapshot.IsValid, Is.False);
    }

    [Test]
    public void MatchSetupFactoryCarriesIndependentSnapshotsForEveryParticipant()
    {
        WDArmoryProfile armory = ValidNormalProfile(out int deckIndex);
        WarDominionMatchFlowConfig flow = Resources.Load<WarDominionMatchFlowConfig>(
            "UI/Home/WarDominionMatchFlowConfig");
        WDMatchModeDefinition mode = flow.Modes.Single(item => item.Id == "2x2");
        WDMatchSubmodeDefinition submode = mode.Submodes.Single(item => item.Id == "normal");
        WDMatchSetup setup = WDMatchSetupFactory.Create(
            new WDMatchmakingRequest(mode, submode, "random_teammate"),
            Resources.Load<DefinicaoMapa>("Mapas/Classic"),
            Resources.Load<WarDominionHomeData>("UI/Home/WarDominionHomeMockData"),
            deckIndex, 31, catalog, armory);

        Assert.That(setup.Participants, Has.Count.EqualTo(4));
        Assert.That(setup.Participants.All(item => item.WeaponLoadout.IsValid), Is.True);
        Assert.That(setup.Participants.Select(item => item.WeaponLoadout).Distinct().Count(),
            Is.EqualTo(4));
        Assert.That(setup.Participants.All(item =>
            item.WeaponLoadout.MainSlots.Count == 8 &&
            item.WeaponLoadout.SupportSlots.Count == 2), Is.True);
    }

    [Test]
    public void LegacyCardPlaceholdersAreRejectedWithoutInventedMapping()
    {
        var profile = new WDArmoryProfile();
        profile.GetDeck(0).SetMain(0, "card_01");
        profile.GetDeck(0).SetSupport(0, "card_24");
        profile.GetDeck(0).SetMain(1, "artillery");

        Assert.That(profile.RemoveLegacyCardPlaceholders(), Is.True);
        Assert.That(profile.GetDeck(0).GetMain(0), Is.Empty);
        Assert.That(profile.GetDeck(0).GetSupport(0), Is.Empty);
        Assert.That(profile.GetDeck(0).GetMain(1), Is.EqualTo("artillery"));
    }

    // ============================================================
    // 03. RUNTIME CONSULTA SNAPSHOT SEM EXECUTAR ARMAS
    // ============================================================

    [Test]
    public void RuntimeReadsCanonicalNormalSnapshotAndUniversalEnergyPlant()
    {
        WDArmoryProfile profile = ValidNormalProfile(out int deckIndex);
        WDMatchWeaponLoadoutSnapshot snapshot = WDMatchWeaponSnapshotFactory.CreateNormal(
            "deck_1", deckIndex, catalog, profile);
        WDMatchSetup setup = Setup(snapshot, false);
        var runtime = new WDWeaponMatchRuntime(
            catalog, new[] { "p1" },
            new WDMatchSetupWeaponLoadoutSource(setup, _ => new string[0]),
            new WDMatchSetupWeaponLevelSource(setup));

        runtime.RefreshLoadouts();

        Assert.That(runtime.GetAvailability("p1", "energy_plant").IsAvailable, Is.True);
        Assert.That(runtime.GetAvailability("p1", "artillery").Reason,
            Is.EqualTo(WDWeaponAvailabilityReason.InsufficientEnergy));
        Assert.That(runtime.FindPlayer("p1").Energy, Is.Zero);
        Assert.That(runtime.FindPlayer("p1").Actions, Is.Empty);
    }

    [Test]
    public void RuntimeReadsRandomHandWithSnapshotLevelAndUniversalEnergyPlant()
    {
        WDArmoryProfile profile = ProfileWithAllWeapons(false);
        profile.GetProgress("artillery").SetProgress(true, 4, 0);
        WDMatchWeaponLoadoutSnapshot snapshot =
            WDMatchWeaponSnapshotFactory.CreateRandomCards(catalog, profile);
        WDMatchSetup setup = Setup(snapshot, true);
        var runtime = new WDWeaponMatchRuntime(
            catalog, new[] { "p1" },
            new WDMatchSetupWeaponLoadoutSource(setup, _ => new[] { "artillery" }),
            new WDMatchSetupWeaponLevelSource(setup));

        runtime.RefreshLoadouts();

        Assert.That(runtime.FindPlayer("p1").GetWeapon("artillery").Level, Is.EqualTo(4));
        Assert.That(runtime.GetAvailability("p1", "energy_plant").IsAvailable, Is.True);
        Assert.That(runtime.FindPlayer("p1").Actions, Is.Empty);
    }

    private WDArmoryProfile ValidNormalProfile(out int deckIndex)
    {
        WDArmoryProfile profile = ProfileWithAllWeapons(true);
        deckIndex = 0;
        WDSavedWeaponDeck deck = profile.GetDeck(deckIndex);
        string[] main =
        {
            "artillery", "rockets", "minefield", "blitzkrieg",
            "transfer_nullifier", "sniper", "kamikaze", "paratroopers"
        };
        string[] support = { "flak_tower", "bunker" };
        for (int i = 0; i < main.Length; i++) deck.SetMain(i, main[i]);
        for (int i = 0; i < support.Length; i++) deck.SetSupport(i, support[i]);
        return profile;
    }

    private WDArmoryProfile ProfileWithAllWeapons(bool obtained)
    {
        var profile = new WDArmoryProfile();
        foreach (WDWeaponDefinition weapon in catalog.Weapons)
            profile.AddProgress(new WDWeaponProgress(weapon.Id, obtained, 1, 0));
        return profile;
    }

    private static WDMatchSetup Setup(
        WDMatchWeaponLoadoutSnapshot snapshot, bool random) =>
        new("1x1", random ? "random_cards" : "normal", "classic", "",
            random ? "symmetric_random_cards" : "standard_deck", 10, true, 17,
            new[]
            {
                new WDMatchParticipant(
                    "p1", "", 0, 0, Color.white, "", snapshot.DeckId,
                    WDMatchParticipantKind.Local, snapshot)
            });
}
