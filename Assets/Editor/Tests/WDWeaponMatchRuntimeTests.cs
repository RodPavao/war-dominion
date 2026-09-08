using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class WDWeaponMatchRuntimeTests
{
    private WDArmoryCatalogConfig catalog;
    private FakeLoadoutSource loadouts;
    private FakeLevelSource levels;
    private WDWeaponMatchRuntime runtime;

    [SetUp]
    public void SetUp()
    {
        catalog = Resources.Load<WDArmoryCatalogConfig>(
            "UI/Home/WDArmoryCatalogConfig");
        Assert.That(catalog, Is.Not.Null);

        loadouts = new FakeLoadoutSource();
        loadouts.Set("p1", "energy_plant", "artillery", "sabotage");
        loadouts.Set("p2", "energy_plant");
        levels = new FakeLevelSource();
        levels.Set("p1", "energy_plant", 4);
        levels.Set("p1", "artillery", 4);
        levels.Set("p1", "sabotage", 6);
        levels.Set("p2", "energy_plant", 1);

        runtime = new WDWeaponMatchRuntime(
            catalog, new[] { "p1", "p2" }, loadouts, levels);
        runtime.RefreshLoadouts();
        runtime.AdvanceCooldownsForRound(1);
    }

    // ============================================================
    // 01. ENERGY VOLÁTIL E ISOLADA POR JOGADOR
    // ============================================================

    [Test]
    public void EveryPlayerStartsWithZeroEnergy()
    {
        Assert.That(runtime.Players.All(player => player.Energy == 0), Is.True);
    }

    [Test]
    public void AddsAndSpendsEnergyWithoutAllowingNegativeBalance()
    {
        Assert.That(runtime.AddEnergy("p1", 30), Is.True);
        Assert.That(runtime.TrySpendEnergy("p1", 20), Is.True);
        Assert.That(runtime.TrySpendEnergy("p1", 11), Is.False);
        Assert.That(runtime.TrySpendEnergy("p1", -1), Is.False);
        Assert.That(runtime.FindPlayer("p1").Energy, Is.EqualTo(10));
    }

    [Test]
    public void RuntimeEnergyIsIsolatedPerPlayer()
    {
        runtime.AddEnergy("p1", 15);

        Assert.That(runtime.FindPlayer("p1").Energy, Is.EqualTo(15));
        Assert.That(runtime.FindPlayer("p2").Energy, Is.Zero);
    }

    // ============================================================
    // 02. LOADOUT, NÍVEL E DISPONIBILIDADE ESTRUTURADA
    // ============================================================

    [Test]
    public void ZeroCostEnergyPlantIsAvailableWithZeroEnergy()
    {
        WDWeaponAvailability result = runtime.GetAvailability("p1", "energy_plant");

        Assert.That(result.IsAvailable, Is.True);
        Assert.That(result.Level, Is.EqualTo(4));
        Assert.That(result.Energy, Is.Zero);
    }

    [Test]
    public void PositiveCostWeaponStartsUnavailableForInsufficientEnergy()
    {
        Assert.That(runtime.GetAvailability("p1", "artillery").Reason,
            Is.EqualTo(WDWeaponAvailabilityReason.InsufficientEnergy));
    }

    [Test]
    public void WeaponOutsideLoadoutIsUnavailable()
    {
        Assert.That(runtime.GetAvailability("p1", "drone").Reason,
            Is.EqualTo(WDWeaponAvailabilityReason.NotInLoadout));
    }

    [TestCase("infiltrator", 1)]
    [TestCase("artillery", 4)]
    [TestCase("drone", 6)]
    public void EffectiveLevelRespectsIndividualMaximum(string weaponId, int maximum)
    {
        loadouts.Set("p1", weaponId);
        levels.Set("p1", weaponId, maximum);
        runtime.RefreshLoadout("p1");

        Assert.That(runtime.FindPlayer("p1").GetWeapon(weaponId).Level,
            Is.EqualTo(maximum));
    }

    [Test]
    public void MissingAndInvalidLevelsHaveDistinctReasons()
    {
        loadouts.Set("p1", "artillery", "infiltrator");
        levels.Remove("p1", "artillery");
        levels.Set("p1", "infiltrator", 2);
        runtime.RefreshLoadout("p1");

        Assert.That(runtime.GetAvailability("p1", "artillery").Reason,
            Is.EqualTo(WDWeaponAvailabilityReason.LevelUnavailable));
        Assert.That(runtime.GetAvailability("p1", "infiltrator").Reason,
            Is.EqualTo(WDWeaponAvailabilityReason.InvalidLevel));
    }

    [Test]
    public void UnknownPlayerAndWeaponHaveStructuredReasons()
    {
        Assert.That(runtime.GetAvailability("missing", "artillery").Reason,
            Is.EqualTo(WDWeaponAvailabilityReason.PlayerNotFound));
        Assert.That(runtime.GetAvailability("p1", "missing").Reason,
            Is.EqualTo(WDWeaponAvailabilityReason.WeaponNotFound));
    }

    [Test]
    public void UnresolvedNormalLoadoutIsReportedWithoutInventingCards()
    {
        WDMatchSetup setup = Setup("standard");
        var source = new WDMatchSetupWeaponLoadoutSource(setup, _ => Array.Empty<string>());
        var isolated = new WDWeaponMatchRuntime(
            catalog, new[] { "p1" }, source, levels);
        isolated.RefreshLoadouts();

        Assert.That(isolated.GetAvailability("p1", "artillery").Reason,
            Is.EqualTo(WDWeaponAvailabilityReason.LoadoutUnavailable));
    }

    [Test]
    public void RandomCardsSourceUsesTechnicalIdsWithoutVisibleNameMapping()
    {
        WDMatchSetup setup = Setup("symmetric_random_cards");
        var source = new WDMatchSetupWeaponLoadoutSource(
            setup, _ => new[] { "artillery", "energy_plant" });

        Assert.That(source.TryGetWeaponIds("p1", out IReadOnlyList<string> ids),
            Is.True);
        Assert.That(ids, Is.EqualTo(new[] { "artillery", "energy_plant" }));
    }

    [Test]
    public void NonCanonicalRandomCardIdIsReportedExplicitly()
    {
        loadouts.Set("p1", "card_01");
        runtime.RefreshLoadout("p1");

        Assert.That(runtime.GetInvalidLoadoutIds("p1"),
            Is.EqualTo(new[] { "card_01" }));
        Assert.That(runtime.GetAvailability("p1", "card_01").Reason,
            Is.EqualTo(WDWeaponAvailabilityReason.InvalidLoadoutIdentity));
    }

    // ============================================================
    // 03. COOLDOWN, USOS E AUSÊNCIA DE EXECUÇÃO
    // ============================================================

    [Test]
    public void CooldownAdvancesOncePerRealRoundAndIsIndependentOfLevel()
    {
        WDWeaponRuntimeEntry artillery = runtime.FindPlayer("p1").GetWeapon("artillery");
        artillery.StartCooldown(3);

        runtime.AdvanceCooldownsForRound(2);
        Assert.That(artillery.CooldownRemaining, Is.EqualTo(2));
        Assert.That(runtime.GetAvailability("p1", "artillery").Reason,
            Is.EqualTo(WDWeaponAvailabilityReason.InsufficientEnergy));
        runtime.AddEnergy("p1", 30);
        Assert.That(runtime.GetAvailability("p1", "artillery").Reason,
            Is.EqualTo(WDWeaponAvailabilityReason.Cooldown));
        runtime.AdvanceCooldownsForRound(2);
        Assert.That(artillery.CooldownRemaining, Is.EqualTo(2));
        runtime.AdvanceCooldownsForRound(4);
        Assert.That(artillery.CooldownRemaining, Is.Zero);
        Assert.That(artillery.Level, Is.EqualTo(4));
    }

    [Test]
    public void UsageLimitProducesStructuredReason()
    {
        runtime.AddEnergy("p1", 100);
        runtime.FindPlayer("p1").GetWeapon("sabotage").RecordUse();

        Assert.That(runtime.GetAvailability("p1", "sabotage").Reason,
            Is.EqualTo(WDWeaponAvailabilityReason.UsageLimit));
    }

    [Test]
    public void AvailabilityQueriesDoNotExecuteOrProgramWeapons()
    {
        WDWeaponPlayerRuntimeState player = runtime.FindPlayer("p1");
        int energyBefore = player.Energy;
        int usesBefore = player.GetWeapon("energy_plant").Uses;

        _ = runtime.GetAvailability("p1", "energy_plant");

        Assert.That(player.Energy, Is.EqualTo(energyBefore));
        Assert.That(player.GetWeapon("energy_plant").Uses, Is.EqualTo(usesBefore));
        Assert.That(player.Actions, Is.Empty);
    }

    [Test]
    public void RuntimeLevelDoesNotAlterPermanentProgressionObject()
    {
        var permanent = new WDWeaponProgress("artillery", true, 1, 25);

        levels.Set("p1", "artillery", 4);
        runtime.RefreshLoadout("p1");

        Assert.That(runtime.FindPlayer("p1").GetWeapon("artillery").Level,
            Is.EqualTo(4));
        Assert.That(permanent.Level, Is.EqualTo(1));
        Assert.That(permanent.Tokens, Is.EqualTo(25));
    }

    private static WDMatchSetup Setup(string cardRuleId)
    {
        return new WDMatchSetup(
            "1x1", "normal", "classic", "", cardRuleId, 10, true, 1,
            new[]
            {
                new WDMatchParticipant(
                    "p1", "", 0, 0, Color.white, "", "deck_1",
                    WDMatchParticipantKind.Local)
            });
    }

    private sealed class FakeLoadoutSource : IWDMatchWeaponLoadoutSource
    {
        private readonly Dictionary<string, IReadOnlyList<string>> values =
            new(StringComparer.Ordinal);

        public void Set(string playerId, params string[] weaponIds) =>
            values[playerId] = weaponIds;

        public bool TryGetWeaponIds(string playerId, out IReadOnlyList<string> weaponIds) =>
            values.TryGetValue(playerId, out weaponIds);
    }

    private sealed class FakeLevelSource : IWDWeaponLevelSource
    {
        private readonly Dictionary<string, int> values = new(StringComparer.Ordinal);

        public void Set(string playerId, string weaponId, int level) =>
            values[$"{playerId}:{weaponId}"] = level;

        public void Remove(string playerId, string weaponId) =>
            values.Remove($"{playerId}:{weaponId}");

        public bool TryGetLevel(string playerId, string weaponId, out int level) =>
            values.TryGetValue($"{playerId}:{weaponId}", out level);
    }
}
