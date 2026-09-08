using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public sealed class WDWeaponPreparationServiceTests
{
    private WDArmoryCatalogConfig catalog;
    private TestLoadoutSource loadouts;
    private TestLevelSource levels;
    private TestStateSource state;
    private WDWeaponMatchRuntime runtime;
    private WDWeaponPreparationService service;

    [SetUp]
    public void SetUp()
    {
        catalog = UnityEngine.Resources.Load<WDArmoryCatalogConfig>(
            "UI/Home/WDArmoryCatalogConfig");
        Assert.That(catalog, Is.Not.Null);

        loadouts = new TestLoadoutSource();
        levels = new TestLevelSource(catalog);
        string[] all = catalog.Weapons.Select(item => item.Id).ToArray();
        loadouts.Set("p1", all);
        loadouts.Set("p2", all);
        loadouts.Set("p3", all);
        state = TestStateSource.Create();
        runtime = new WDWeaponMatchRuntime(
            catalog, new[] { "p1", "p2", "p3" }, loadouts, levels);
        runtime.RefreshLoadouts();
        runtime.AdvanceCooldownsForRound(2);
        runtime.AddEnergy("p1", 1000);
        runtime.AddEnergy("p2", 1000);
        runtime.AddEnergy("p3", 1000);
        service = new WDWeaponPreparationService(
            runtime, new WDGameWeaponContext(state), catalog);
        service.BeginRound(2);
    }

    // ============================================================
    // 01. DISPONIBILIDADE E IDENTIDADE CANÔNICA
    // ============================================================

    [Test]
    public void WeaponOutsideLoadoutDoesNotProgram()
    {
        loadouts.Set("p1", WDStrategicWeaponIds.EnergyPlant);
        runtime.RefreshLoadout("p1");

        WDWeaponProgrammingResult result = service.Program(
            Request("p1", WDStrategicWeaponIds.Artillery,
                target: "p1_a", installation: "artillery-1"));

        Assert.That(result.Reason, Is.EqualTo(WDWeaponProgrammingReason.WeaponUnavailable));
        Assert.That(result.Availability.Reason,
            Is.EqualTo(WDWeaponAvailabilityReason.NotInLoadout));
    }

    [Test]
    public void InsufficientEnergyAndCooldownDoNotProgram()
    {
        var zeroRuntime = RuntimeWithEnergy(0);
        var zeroService = Service(zeroRuntime);
        Assert.That(zeroService.Program(Request("p1", WDSupportWeaponIds.AirStrike,
            origin: "p1_a", target: "p2_a")).Availability.Reason,
            Is.EqualTo(WDWeaponAvailabilityReason.InsufficientEnergy));

        runtime.FindPlayer("p1").GetWeapon(WDSupportWeaponIds.AirStrike).StartCooldown(2);
        Assert.That(service.Program(Request("p1", WDSupportWeaponIds.AirStrike,
            origin: "p1_a", target: "p2_a")).Availability.Reason,
            Is.EqualTo(WDWeaponAvailabilityReason.Cooldown));
    }

    [Test]
    public void EnergyPlantProgramsInRoundOneWithZeroEnergy()
    {
        WDWeaponMatchRuntime zeroRuntime = RuntimeWithEnergy(0);
        WDWeaponPreparationService zeroService = Service(zeroRuntime, 1);

        WDWeaponProgrammingResult result = zeroService.Program(
            Request("p1", WDStrategicWeaponIds.EnergyPlant,
                target: "p1_a", installation: "plant-1"));

        Assert.That(result.Succeeded, Is.True);
        Assert.That(zeroRuntime.FindPlayer("p1").Energy, Is.Zero);
    }

    [Test]
    public void OptionExposesLevelCostCooldownReasonAndRequirement()
    {
        WDWeaponProgrammingOption option = service.GetOption(
            "p1", WDSupportWeaponIds.AirStrike);

        Assert.That(option.Availability.Level, Is.GreaterThan(0));
        Assert.That(option.Availability.EnergyCost, Is.GreaterThan(0));
        Assert.That(option.Availability.Reason,
            Is.EqualTo(WDWeaponAvailabilityReason.Available));
        Assert.That(option.Requirement.Fields.HasFlag(
            WDWeaponSelectionFields.OriginTerritory), Is.True);
        Assert.That(option.Requirement.Fields.HasFlag(
            WDWeaponSelectionFields.TargetTerritory), Is.True);

        service.SelectWeapon("p1", WDSupportWeaponIds.AirStrike);
        Assert.That(service.GetSelectedOption("p1").WeaponId,
            Is.EqualTo(WDSupportWeaponIds.AirStrike));
        service.ClearSelection("p1");
        Assert.That(service.GetSelectedOption("p1"), Is.Null);
    }

    // ============================================================
    // 02. CAMPOS, RELAÇÕES E GRAFO DA INTENÇÃO
    // ============================================================

    [Test]
    public void RequiredOriginTerritoryAndPlayerTargetReturnStructuredReasons()
    {
        Assert.That(service.Program(Request("p1", WDSupportWeaponIds.AirStrike,
            target: "p2_a")).Reason,
            Is.EqualTo(WDWeaponProgrammingReason.MissingOriginTerritory));
        Assert.That(service.Program(Request("p1", WDStrategicWeaponIds.Sabotage)).Reason,
            Is.EqualTo(WDWeaponProgrammingReason.MissingTargetPlayer));
    }

    [Test]
    public void InstallationRequiresOwnTerritoryAndStableInstallationId()
    {
        Assert.That(service.Program(Request("p1", WDSupportWeaponIds.Bunker,
            target: "p1_a")).Reason,
            Is.EqualTo(WDWeaponProgrammingReason.MissingInstallation));
        Assert.That(service.Program(Request("p1", WDSupportWeaponIds.Bunker,
            target: "p2_a", installation: "bunker-1")).Reason,
            Is.EqualTo(WDWeaponProgrammingReason.InvalidTargetTerritory));
    }

    [Test]
    public void AirStrikeRequiresAdjacentEnemy()
    {
        Assert.That(service.Program(Request("p1", WDSupportWeaponIds.AirStrike,
            origin: "p1_a", target: "p2_far")).Reason,
            Is.EqualTo(WDWeaponProgrammingReason.TargetNotAdjacent));
        Assert.That(service.Program(Request("p1", WDSupportWeaponIds.AirStrike,
            origin: "p1_a", target: "p3_a")).Reason,
            Is.EqualTo(WDWeaponProgrammingReason.InvalidTargetTerritory));
        Assert.That(service.Program(Request("p1", WDSupportWeaponIds.AirStrike,
            origin: "p1_a", target: "p2_a")).Succeeded, Is.True);
    }

    [Test]
    public void KamikazeUsesGraphDistanceOfThree()
    {
        Assert.That(service.Program(Request("p1", WDTacticalWeaponIds.Kamikaze,
            origin: "p1_a", target: "p2_far")).Succeeded, Is.True);
        Assert.That(service.Program(Request("p1", WDTacticalWeaponIds.Kamikaze,
            origin: "p1_a", target: "p2_too_far")).Reason,
            Is.EqualTo(WDWeaponProgrammingReason.TargetOutOfRange));
    }

    [Test]
    public void EmptyTargetIsControlledPerWeapon()
    {
        Assert.That(service.Program(Request("p1", WDTacticalWeaponIds.Paratroopers,
            target: "empty")).Succeeded, Is.True);
        Assert.That(service.Program(Request("p1", WDSupportWeaponIds.AirStrike,
            origin: "p1_a", target: "empty")).Reason,
            Is.EqualTo(WDWeaponProgrammingReason.EmptyTargetNotAllowed));
    }

    [Test]
    public void PlayerTargetMustBeOpponent()
    {
        Assert.That(service.Program(Request("p1", WDStrategicWeaponIds.Peace,
            targetPlayer: "p3")).Reason,
            Is.EqualTo(WDWeaponProgrammingReason.InvalidTargetPlayer));
        Assert.That(service.Program(Request("p1", WDStrategicWeaponIds.Peace,
            targetPlayer: "p2")).Succeeded, Is.True);
        Assert.That(service.Program(Request("p1", WDStrategicWeaponIds.Peace,
            targetPlayer: "p1")).Reason,
            Is.EqualTo(WDWeaponProgrammingReason.InvalidTargetPlayer));
    }

    [Test]
    public void AirTransportRequiresStructuredParameters()
    {
        Assert.That(service.Program(Request("p1", WDSupportWeaponIds.AirTransport,
            origin: "p1_a", target: "p3_a")).Reason,
            Is.EqualTo(WDWeaponProgrammingReason.MissingParameters));
    }

    [Test]
    public void DroneAndRocketLaunchHaveDistinctSelectionContracts()
    {
        Assert.That(service.GetRequirement(WDSpecialWeaponIds.Drone).Fields,
            Is.EqualTo(WDWeaponSelectionFields.SpecialFlow));
        WDWeaponSelectionRequirement rocket = service.GetRequirement(
            WDStrategicWeaponIds.Rockets, WDWeaponIntentKind.RocketLaunch);
        Assert.That(rocket.Fields.HasFlag(WDWeaponSelectionFields.Installation), Is.True);
        Assert.That(rocket.MaximumGraphDistance, Is.EqualTo(3));
    }

    // ============================================================
    // 03. ARMAZENAMENTO, CANCELAMENTO E REPROGRAMAÇÃO
    // ============================================================

    [Test]
    public void ValidActionHasStableUniqueIdentityAndImmutablePayload()
    {
        WDWeaponProgrammingResult first = service.Program(Request(
            "p1", WDStrategicWeaponIds.LongRangeBomb, target: "p2_a"));
        WDWeaponProgrammingResult second = service.Program(Request(
            "p1", WDStrategicWeaponIds.LongRangeBomb, target: "neutral"));

        Assert.That(first.Action.ActionId, Is.Not.EqualTo(second.Action.ActionId));
        Assert.That(first.Action.AuthorPlayerId, Is.EqualTo("p1"));
        Assert.That(first.Action.WeaponId, Is.EqualTo(WDStrategicWeaponIds.LongRangeBomb));
        Assert.That(first.Action.Round, Is.EqualTo(2));
        Assert.That(first.Action.State, Is.EqualTo(WDWeaponActionState.Programmed));
    }

    [Test]
    public void ExactDuplicateIntentIsRejectedWithoutUniversalOnePerRoundLimit()
    {
        Assert.That(service.Program(Request("p1", WDStrategicWeaponIds.LongRangeBomb,
            target: "p2_a")).Succeeded, Is.True);
        Assert.That(service.Program(Request("p1", WDStrategicWeaponIds.LongRangeBomb,
            target: "p2_a")).Reason,
            Is.EqualTo(WDWeaponProgrammingReason.DuplicateIntent));
        Assert.That(service.Program(Request("p1", WDStrategicWeaponIds.LongRangeBomb,
            target: "neutral")).Succeeded, Is.True);
    }

    [Test]
    public void CanonicalUseLimitCountsPendingIntentWithoutStartingRuntimeUse()
    {
        Assert.That(service.Program(Request("p1", WDSupportWeaponIds.Reinforcements,
            target: "p1_a")).Succeeded, Is.True);
        Assert.That(service.Program(Request("p1", WDSupportWeaponIds.Reinforcements,
            target: "p3_a")).Reason,
            Is.EqualTo(WDWeaponProgrammingReason.ProgrammingUsageLimit));
        Assert.That(runtime.FindPlayer("p1")
            .GetWeapon(WDSupportWeaponIds.Reinforcements).Uses, Is.Zero);
    }

    [Test]
    public void CancellationRemovesOnlyOwnedIntentWithoutCost()
    {
        int energy = runtime.FindPlayer("p1").Energy;
        WDWeaponAction action = service.Program(Request(
            "p1", WDSupportWeaponIds.Reinforcements, target: "p1_a")).Action;

        Assert.That(service.Cancel("p2", action.ActionId).Reason,
            Is.EqualTo(WDWeaponProgrammingReason.ActionNotOwned));
        Assert.That(service.Cancel("p1", action.ActionId).Succeeded, Is.True);
        Assert.That(service.GetPlayerActions("p1", 2), Is.Empty);
        Assert.That(runtime.FindPlayer("p1").Energy, Is.EqualTo(energy));
    }

    [Test]
    public void ReplacementIsAtomicAndCreatesNewActionId()
    {
        WDWeaponAction original = service.Program(Request(
            "p1", WDSupportWeaponIds.Reinforcements, target: "p1_a")).Action;
        WDWeaponProgrammingResult invalid = service.Replace("p1", original.ActionId,
            Request("p1", WDSupportWeaponIds.Reinforcements, target: "empty"));
        Assert.That(invalid.Succeeded, Is.False);
        Assert.That(service.GetPlayerActions("p1", 2).Single().ActionId,
            Is.EqualTo(original.ActionId));

        WDWeaponProgrammingResult valid = service.Replace("p1", original.ActionId,
            Request("p1", WDSupportWeaponIds.Reinforcements, target: "p3_a"));
        Assert.That(valid.Succeeded, Is.True);
        Assert.That(valid.Action.ActionId, Is.Not.EqualTo(original.ActionId));
        Assert.That(service.GetPlayerActions("p1", 2).Single().TargetTerritoryId,
            Is.EqualTo("p3_a"));
    }

    [Test]
    public void ActionsRemainIndependentByPlayerAndRound()
    {
        service.Program(Request("p1", WDSupportWeaponIds.Reinforcements,
            target: "p1_a"));
        service.Program(Request("p2", WDSupportWeaponIds.Reinforcements,
            target: "p2_a"));
        service.BeginRound(3);
        service.Program(Request("p1", WDStrategicWeaponIds.LongRangeBomb,
            target: "p2_a"));

        Assert.That(service.GetPlayerActions("p1", 2).Count, Is.EqualTo(1));
        Assert.That(service.GetPlayerActions("p2", 2).Count, Is.EqualTo(1));
        Assert.That(service.GetPlayerActions("p1", 3).Count, Is.EqualTo(1));
    }

    // ============================================================
    // 04. CONGELAMENTO DETERMINÍSTICO E AUSÊNCIA DE EFEITOS
    // ============================================================

    [Test]
    public void FreezeOrdersByPhasePlayerOrderProgrammingAndActionId()
    {
        service.Program(Request("p1", WDSupportWeaponIds.Reinforcements,
            target: "p1_a"));
        service.Program(Request("p2", WDSupportWeaponIds.Reinforcements,
            target: "p2_a"));
        service.Program(Request("p1", WDStrategicWeaponIds.Peace,
            targetPlayer: "p2"));

        IReadOnlyList<WDWeaponAction> frozen = service.FreezeRound();

        Assert.That(frozen.Select(item => item.WeaponId), Is.EqualTo(new[]
        {
            WDStrategicWeaponIds.Peace,
            WDSupportWeaponIds.Reinforcements,
            WDSupportWeaponIds.Reinforcements
        }));
        Assert.That(frozen.Skip(1).Select(item => item.AuthorPlayerId),
            Is.EqualTo(new[] { "p2", "p1" }));
        Assert.That(frozen, Is.InstanceOf<System.Collections.ObjectModel.ReadOnlyCollection<WDWeaponAction>>());
        Assert.That(service.Program(Request("p1", WDSupportWeaponIds.Reinforcements,
            target: "p3_a")).Reason,
            Is.EqualTo(WDWeaponProgrammingReason.RoundFrozen));
    }

    [Test]
    public void ProgrammingDoesNotMutateBoardEnergyCooldownUsesOrExecutionQueue()
    {
        string boardBefore = state.Fingerprint();
        WDWeaponPlayerRuntimeState player = runtime.FindPlayer("p1");
        int energyBefore = player.Energy;
        WDWeaponRuntimeEntry weapon = player.GetWeapon(WDSupportWeaponIds.AirStrike);
        int cooldownBefore = weapon.CooldownRemaining;
        int usesBefore = weapon.Uses;

        WDWeaponProgrammingResult result = service.Program(Request(
            "p1", WDSupportWeaponIds.AirStrike, origin: "p1_a", target: "p2_a"));

        Assert.That(result.Succeeded, Is.True);
        Assert.That(state.Fingerprint(), Is.EqualTo(boardBefore));
        Assert.That(player.Energy, Is.EqualTo(energyBefore));
        Assert.That(weapon.CooldownRemaining, Is.EqualTo(cooldownBefore));
        Assert.That(weapon.Uses, Is.EqualTo(usesBefore));
        Assert.That(player.Actions, Is.Empty);
    }

    // ============================================================
    // 05. FIXTURES READ-ONLY
    // ============================================================

    private WDWeaponPreparationService Service(
        WDWeaponMatchRuntime selectedRuntime, int round = 2)
    {
        var result = new WDWeaponPreparationService(
            selectedRuntime, new WDGameWeaponContext(state), catalog);
        result.BeginRound(round);
        return result;
    }

    private WDWeaponMatchRuntime RuntimeWithEnergy(int energy)
    {
        var result = new WDWeaponMatchRuntime(
            catalog, new[] { "p1", "p2", "p3" }, loadouts, levels);
        result.RefreshLoadouts();
        result.AdvanceCooldownsForRound(2);
        result.AddEnergy("p1", energy);
        return result;
    }

    private static WDWeaponProgrammingRequest Request(
        string player, string weapon, string origin = "", string target = "",
        string targetPlayer = "", string installation = "") =>
        new(player, weapon, originTerritoryId: origin,
            targetTerritoryId: target, targetPlayerId: targetPlayer,
            installationId: installation);

    private sealed class TestLoadoutSource : IWDMatchWeaponLoadoutSource
    {
        private readonly Dictionary<string, IReadOnlyList<string>> values =
            new(StringComparer.Ordinal);
        public void Set(string playerId, params string[] ids) => values[playerId] = ids;
        public bool TryGetWeaponIds(string playerId, out IReadOnlyList<string> ids) =>
            values.TryGetValue(playerId, out ids);
    }

    private sealed class TestLevelSource : IWDWeaponLevelSource
    {
        private readonly WDArmoryCatalogConfig catalog;
        public TestLevelSource(WDArmoryCatalogConfig catalog) => this.catalog = catalog;
        public bool TryGetLevel(string playerId, string weaponId, out int level)
        {
            WDWeaponDefinition definition = catalog.FindWeapon(weaponId);
            level = definition?.MaximumLevel ?? 0;
            return definition != null;
        }
    }

    private sealed class TestStateSource : IWDGameWeaponStateSource
    {
        private readonly List<WDGameWeaponPlayerView> players = new();
        private readonly List<string> order = new();
        private readonly List<WDGameWeaponTerritoryView> territories = new();
        private readonly List<(string A, string B)> links = new();

        public int CurrentRound => 2;
        public string ActiveMapId => "test_map";
        public IReadOnlyList<WDGameWeaponPlayerView> GetPlayers() => players;
        public IReadOnlyList<string> GetOfficialPlayerOrder() => order;
        public IReadOnlyList<WDGameWeaponTerritoryView> GetTerritories() => territories;
        public bool AreConnected(string originId, string targetId, TipoConexaoMapa type) =>
            links.Any(link => link.A == originId && link.B == targetId ||
                link.A == targetId && link.B == originId);
        public IReadOnlyList<string> GetNeighbours(string territoryId, TipoConexaoMapa? type) =>
            links.Where(link => link.A == territoryId || link.B == territoryId)
                .Select(link => link.A == territoryId ? link.B : link.A)
                .OrderBy(id => id, StringComparer.Ordinal).ToArray();
        public string Fingerprint() => string.Join("|", territories.Select(item =>
            $"{item.TerritoryId}:{item.OwnerPlayerId}:{item.Troops}:{item.State}"));

        public static TestStateSource Create()
        {
            var result = new TestStateSource();
            result.players.AddRange(new[]
            {
                new WDGameWeaponPlayerView("p1", 0, 0),
                new WDGameWeaponPlayerView("p2", 1, 1),
                new WDGameWeaponPlayerView("p3", 2, 0)
            });
            result.order.AddRange(new[] { "p2", "p1", "p3" });
            result.territories.AddRange(new[]
            {
                Territory("p1_a", "p1", 5), Territory("p2_a", "p2", 5),
                Territory("p3_a", "p3", 5), Territory("p2_far", "p2", 4),
                Territory("p2_too_far", "p2", 3),
                new WDGameWeaponTerritoryView("neutral", "", 1,
                    WDWeaponTerritoryStateKind.Neutral),
                new WDGameWeaponTerritoryView("empty", "", 0,
                    WDWeaponTerritoryStateKind.Empty)
            });
            result.links.AddRange(new[]
            {
                ("p1_a", "p2_a"), ("p1_a", "p3_a"), ("p1_a", "empty"),
                ("p2_a", "neutral"), ("neutral", "p2_far"),
                ("p2_far", "p2_too_far")
            });
            return result;
        }

        private static WDGameWeaponTerritoryView Territory(
            string id, string owner, int troops) =>
            new(id, owner, troops, WDWeaponTerritoryStateKind.Occupied);
    }
}
