using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class WDWeaponExecutionCoordinatorTests
{
    private WDArmoryCatalogConfig catalog;
    private TestSource source;
    private WDGameWeaponContext context;
    private WDWeaponMatchRuntime runtime;
    private WDWeaponInstallationRegistry registry;
    private WDGameWeaponMutationPort mutations;
    private WDWeaponExecutionCoordinator coordinator;
    private int actionSequence;

    [SetUp]
    public void SetUp()
    {
        catalog = Resources.Load<WDArmoryCatalogConfig>("UI/Home/WDArmoryCatalogConfig");
        Assert.That(catalog, Is.Not.Null);
        source = TestSource.Create();
        context = new WDGameWeaponContext(source);
        var loadout = new AllWeaponsLoadout(catalog);
        runtime = new WDWeaponMatchRuntime(
            catalog, new[] { "p1", "p2", "p3" }, loadout, loadout);
        runtime.RefreshLoadouts();
        runtime.AdvanceCooldownsForRound(1);
        foreach (string playerId in new[] { "p1", "p2", "p3" })
            runtime.AddEnergy(playerId, 200);
        registry = new WDWeaponInstallationRegistry();
        mutations = new WDGameWeaponMutationPort(source, runtime, catalog, registry);
        coordinator = new WDWeaponExecutionCoordinator(
            context, runtime, mutations, registry, catalog);
    }

    // ============================================================
    // 01. REFORÇOS: ESTADO REAL, NÍVEIS, ALIANÇA E CUSTOS
    // ============================================================

    [TestCase(1, 3)]
    [TestCase(2, 4)]
    [TestCase(3, 5)]
    [TestCase(4, 6)]
    [TestCase(5, 7)]
    [TestCase(6, 8)]
    public void ReinforcementsUseCanonicalLevelTable(int level, int expected)
    {
        SetLevel("p1", WDSupportWeaponIds.Reinforcements, level);
        WDWeaponAction action = Action(WDSupportWeaponIds.Reinforcements, "owned");

        AssertExecuted(action);
        Assert.That(context.FindTerritory("owned").Troops, Is.EqualTo(5 + expected));
        AssertRuntime("p1", WDSupportWeaponIds.Reinforcements, 175, 3, 1);
    }

    [Test]
    public void ReinforcementsCanTargetAllyButNotOpponent()
    {
        AssertExecuted(Action(WDSupportWeaponIds.Reinforcements, "ally"));
        Assert.That(context.FindTerritory("ally").Troops, Is.EqualTo(12));

        WDWeaponAction enemy = Action(WDSupportWeaponIds.Reinforcements, "owned", "p3");
        Assert.That(coordinator.Execute(enemy).Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.Invalidated));
        Assert.That(context.FindTerritory("owned").Troops, Is.EqualTo(5));
        AssertRuntime("p3", WDSupportWeaponIds.Reinforcements, 200, 0, 0);
    }

    [Test]
    public void FrozenPreparationActionFlowsThroughCoordinatorOnce()
    {
        var preparation = new WDWeaponPreparationService(runtime, context, catalog);
        preparation.BeginRound(1);
        WDWeaponProgrammingResult programmed = preparation.Program(
            new WDWeaponProgrammingRequest("p1", WDSupportWeaponIds.Reinforcements,
                targetTerritoryId: "owned"));
        Assert.That(programmed.Succeeded, Is.True);
        IReadOnlyList<WDWeaponAction> frozen = preparation.FreezeRound();

        Assert.That(coordinator.ExecuteFrozenActions(frozen).Single().Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.Executed));
        Assert.That(coordinator.ExecuteFrozenActions(frozen).Single().Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.AlreadyProcessed));
        Assert.That(context.FindTerritory("owned").Troops, Is.EqualTo(13));
        AssertRuntime("p1", WDSupportWeaponIds.Reinforcements, 175, 3, 1);
    }

    // ============================================================
    // 02. BUNKER E FLAK: REGISTRO AUTORITATIVO SEM STACKING
    // ============================================================

    [TestCase(1, 2)]
    [TestCase(2, 3)]
    [TestCase(3, 4)]
    [TestCase(4, 5)]
    public void BunkerInstallsCanonicalBarrier(int level, int barrier)
    {
        SetLevel("p1", WDSupportWeaponIds.Bunker, level);
        WDWeaponAction action = Action(WDSupportWeaponIds.Bunker, "owned",
            installationId: $"bunker-{level}");
        AssertExecuted(action);

        WDWeaponInstallationState installed = registry.Find($"bunker-{level}");
        Assert.That(installed.BarrierRemaining, Is.EqualTo(barrier));
        Assert.That(installed.OwnerPlayerId, Is.EqualTo("p1"));
        AssertRuntime("p1", WDSupportWeaponIds.Bunker, 190, 3, 1);
    }

    [TestCase(1, 0.50d)]
    [TestCase(2, 0.65d)]
    [TestCase(3, 0.80d)]
    [TestCase(4, 0.90d)]
    public void FlakStoresLevelDurationAndCanonicalChance(int level, double chance)
    {
        SetLevel("p1", WDSupportWeaponIds.FlakTower, level);
        AssertExecuted(Action(WDSupportWeaponIds.FlakTower, "owned",
            installationId: $"flak-{level}"));

        WDWeaponInstallationState installed = registry.Find($"flak-{level}");
        Assert.That(installed.Level, Is.EqualTo(level));
        Assert.That(installed.DurationRemaining, Is.EqualTo(3));
        Assert.That(installed.IsTransportable, Is.True);
        Assert.That(WDSupportWeaponTables.FlakChance(installed.Level), Is.EqualTo(chance));
        AssertRuntime("p1", WDSupportWeaponIds.FlakTower, 195, 3, 1);
    }

    [TestCase(WDSupportWeaponIds.Bunker)]
    [TestCase(WDSupportWeaponIds.FlakTower)]
    [TestCase(WDStrategicWeaponIds.EnergyPlant)]
    public void DuplicateInstallationFailsWithoutSecondCost(string weaponId)
    {
        AssertExecuted(Action(weaponId, "owned", installationId: weaponId + "-1"));
        int energyAfterFirst = runtime.FindPlayer("p1").Energy;
        WDWeaponRuntimeEntry entry = runtime.FindPlayer("p1").GetWeapon(weaponId);
        entry.StartCooldown(0);
        WDWeaponAction duplicate = Action(weaponId, "owned", installationId: weaponId + "-2");

        Assert.That(coordinator.Execute(duplicate).Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.Invalidated));
        Assert.That(runtime.FindPlayer("p1").Energy, Is.EqualTo(energyAfterFirst));
        Assert.That(entry.Uses, Is.EqualTo(1));
        Assert.That(registry.GetByTerritory("owned").Count(item => item.WeaponId == weaponId),
            Is.EqualTo(1));
    }

    // ============================================================
    // 03. CENTRAL: INSTALAÇÃO UNIVERSAL E TRÊS PAGAMENTOS FASE 0
    // ============================================================

    [TestCase(1, 5)]
    [TestCase(2, 10)]
    [TestCase(3, 15)]
    [TestCase(4, 20)]
    public void EnergyPlantPaysOnlyOnThreeFollowingRounds(int level, int payment)
    {
        SetLevel("p1", WDStrategicWeaponIds.EnergyPlant, level);
        AssertExecuted(Action(WDStrategicWeaponIds.EnergyPlant, "owned",
            installationId: "central"));
        AssertRuntime("p1", WDStrategicWeaponIds.EnergyPlant, 200, 5, 1);

        Assert.That(coordinator.ProcessEnergyPlantPayments(1), Is.Zero);
        for (int round = 2; round <= 4; round++)
        {
            Assert.That(coordinator.ProcessEnergyPlantPayments(round), Is.EqualTo(1));
            Assert.That(coordinator.ProcessEnergyPlantPayments(round), Is.Zero);
        }
        Assert.That(runtime.FindPlayer("p1").Energy, Is.EqualTo(200 + payment * 3));
        Assert.That(registry.Find("central").PaymentsRemaining, Is.Zero);
        Assert.That(registry.Find("central").IsDestroyed, Is.False);
        Assert.That(coordinator.ProcessEnergyPlantPayments(5), Is.Zero);
    }

    [Test]
    public void EnergyPlantThirdOwnActivationIsRejectedWithoutCost()
    {
        ExecuteCentralAtRound("owned", "central-1", 1);
        ExecuteCentralAtRound("owned2", "central-2", 6);
        int before = runtime.FindPlayer("p1").Energy;
        source.CurrentRound = 11;
        runtime.AdvanceCooldownsForRound(11);
        WDWeaponAction third = Action(WDStrategicWeaponIds.EnergyPlant, "owned3",
            installationId: "central-3", round: 11);

        Assert.That(coordinator.Execute(third).Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.Invalidated));
        Assert.That(runtime.FindPlayer("p1").Energy, Is.EqualTo(before));
        Assert.That(registry.Find("central-3"), Is.Null);
    }

    // ============================================================
    // 04. LOTE FECHADO, IDEMPOTÊNCIA E LEITURA IMEDIATA
    // ============================================================

    [Test]
    public void UnsupportedWeaponIsDeferredWithoutAnyCostOrMutation()
    {
        WDWeaponAction action = Action(WDTacticalWeaponIds.Sniper, "enemy");
        string before = source.Fingerprint();

        Assert.That(coordinator.Execute(action).Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.Deferred));
        Assert.That(source.Fingerprint(), Is.EqualTo(before));
        AssertRuntime("p1", WDTacticalWeaponIds.Sniper, 200, 0, 0);
    }

    [Test]
    public void SameActionIdNeverDuplicatesEffectOrCosts()
    {
        WDWeaponAction action = Action(WDSupportWeaponIds.Reinforcements, "owned");
        AssertExecuted(action);
        Assert.That(coordinator.Execute(action).Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.AlreadyProcessed));
        Assert.That(context.FindTerritory("owned").Troops, Is.EqualTo(13));
        AssertRuntime("p1", WDSupportWeaponIds.Reinforcements, 175, 3, 1);
    }

    [Test]
    public void ReadOnlyContextImmediatelySeesCoordinatorMutation()
    {
        Assert.That(context.FindTerritory("owned").Troops, Is.EqualTo(5));
        AssertExecuted(Action(WDSupportWeaponIds.Reinforcements, "owned"));
        Assert.That(context.FindTerritory("owned").Troops, Is.EqualTo(13));
    }

    // ============================================================
    // 05. DANO, FLAK, LIFECYCLE E INSTALAÇÕES OFENSIVAS REAIS
    // ============================================================

    [TestCase(WDSupportWeaponIds.AirStrike, 20)]
    [TestCase(WDStrategicWeaponIds.LongRangeBomb, 25)]
    [TestCase(WDSpecialWeaponIds.ChemicalBomb, 25)]
    [TestCase(WDTacticalWeaponIds.Kamikaze, 25)]
    public void OffensiveBatchMutatesRealStateAndCommitsCanonicalCost(
        string weaponId, int cost)
    {
        source.Connect("owned", "enemy");
        WDWeaponAction action = Action(weaponId, "enemy", origin: "owned");
        AssertExecuted(action);
        Assert.That(context.FindTerritory("enemy").Troops, Is.LessThan(4));
        AssertRuntime("p1", weaponId, 200 - cost, 3, 1);
    }

    [Test]
    public void BunkerAbsorbsOverflowAndIsRemovedAtZero()
    {
        AssertExecuted(Action(WDSupportWeaponIds.Bunker, "enemy", "p3", "enemy-bunker"));
        runtime.FindPlayer("p1").GetWeapon(WDSupportWeaponIds.AirStrike).SetLevel(6, 6);
        source.Connect("owned", "enemy");
        AssertExecuted(Action(WDSupportWeaponIds.AirStrike, "enemy", origin: "owned"));
        Assert.That(registry.Find("enemy-bunker"), Is.Null);
        Assert.That(context.FindTerritory("enemy").Troops, Is.EqualTo(2));
    }

    [Test]
    public void ChemicalIgnoresBunkerBarrier()
    {
        AssertExecuted(Action(WDSupportWeaponIds.Bunker, "enemy", "p3", "chemical-bunker"));
        source.Connect("owned", "enemy");
        AssertExecuted(Action(WDSpecialWeaponIds.ChemicalBomb, "enemy", origin: "owned"));
        Assert.That(registry.Find("chemical-bunker").BarrierRemaining, Is.EqualTo(5));
        Assert.That(context.FindTerritory("enemy").Troops, Is.EqualTo(2));
    }

    [Test]
    public void FlakSelectionUsesHighestLevelThenStableIdAndInterceptsDeterministically()
    {
        source.Connect("enemy2", "enemy");
        mutations.Install("flak-low", new WDWeaponInstallationState("z", "flak_tower",
            "enemy", "p3", 3, 0, 0, 0, 2, true, 1));
        mutations.Install("flak-high", new WDWeaponInstallationState("a", "flak_tower",
            "enemy2", "p3", 3, 0, 0, 0, 4, true, 1));
        coordinator = new WDWeaponExecutionCoordinator(context, runtime, mutations,
            registry, catalog, new FixedRandom(0));
        source.Connect("owned", "enemy");
        WDWeaponAction action = Action(WDSupportWeaponIds.AirStrike, "enemy", origin: "owned");
        Assert.That(coordinator.Execute(action).Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.Intercepted));
        Assert.That(context.FindTerritory("enemy").Troops, Is.EqualTo(4));
    }

    [Test]
    public void EmptyTerritoryRemovesDefenseAndAbandonsStrategicInstallations()
    {
        mutations.Install("b", new WDWeaponInstallationState("b", "bunker", "enemy",
            "p3", 0, 2, 0, 0));
        mutations.Install("r", new WDWeaponInstallationState("r", "rockets", "enemy",
            "p3", 0, 0, 2, 0));
        mutations.RemoveTroops("empty", "enemy", 4);
        Assert.That(registry.Find("b"), Is.Null);
        Assert.That(registry.Find("r").IsAbandoned, Is.True);
    }

    [Test]
    public void KamikazeSacrificePersistsWhenFlakIntercepts()
    {
        source.Connect("owned", "enemy");
        mutations.Install("flak", new WDWeaponInstallationState("flak", "flak_tower",
            "enemy", "p3", 3, 0, 0, 0, 4, true, 1));
        coordinator = new WDWeaponExecutionCoordinator(context, runtime, mutations,
            registry, catalog, new FixedRandom(0));
        WDWeaponAction action = Action(WDTacticalWeaponIds.Kamikaze, "enemy", origin: "owned");
        Assert.That(coordinator.Execute(action).Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.Intercepted));
        Assert.That(context.FindTerritory("owned").Troops, Is.EqualTo(4));
        Assert.That(context.FindTerritory("enemy").Troops, Is.EqualTo(4));
    }

    [Test]
    public void RocketsInstallWithTwoShotsAndLaunchWithoutSecondEnergyCost()
    {
        source.Connect("owned", "enemy");
        AssertExecuted(Action(WDStrategicWeaponIds.Rockets, "owned",
            installationId: "launcher"));
        Assert.That(registry.Find("launcher").ChargesRemaining, Is.EqualTo(2));
        int energy = runtime.FindPlayer("p1").Energy;
        WDWeaponAction launch = Action(WDStrategicWeaponIds.Rockets, "enemy",
            installationId: "launcher", origin: "owned",
            phase: WDWeaponResolutionPhase.ProgrammedOffense);
        AssertExecuted(launch);
        Assert.That(registry.Find("launcher").ChargesRemaining, Is.EqualTo(1));
        Assert.That(runtime.FindPlayer("p1").Energy, Is.EqualTo(energy));
    }

    [Test]
    public void ArtilleryChoosesUpToThreeTargetsAndConsumesOnlyWhenOperating()
    {
        source.Connect("owned", "enemy");
        AssertExecuted(Action(WDStrategicWeaponIds.Artillery, "owned",
            installationId: "artillery"));
        int before = context.FindTerritory("enemy").Troops;
        Assert.That(coordinator.ProcessArtillery(1), Is.EqualTo(1));
        Assert.That(context.FindTerritory("enemy").Troops, Is.LessThan(before));
        Assert.That(coordinator.ProcessArtillery(1), Is.Zero);
    }

    private void ExecuteCentralAtRound(string territory, string id, int round)
    {
        source.CurrentRound = round;
        runtime.AdvanceCooldownsForRound(round);
        AssertExecuted(Action(WDStrategicWeaponIds.EnergyPlant, territory,
            installationId: id, round: round));
    }

    private WDWeaponAction Action(
        string weaponId, string target, string author = "p1",
        string installationId = "", int? round = null, string origin = "",
        WDWeaponResolutionPhase? phase = null) =>
        new($"test-action-{actionSequence++}", author, weaponId,
            round ?? source.CurrentRound, phase ?? catalog.FindWeapon(weaponId).ResolutionPhase,
            actionSequence, originTerritoryId: origin, targetTerritoryId: target,
            installationId: installationId);

    private void AssertExecuted(WDWeaponAction action) => Assert.That(
        coordinator.Execute(action).Status,
        Is.EqualTo(WDIntegratedWeaponExecutionStatus.Executed));

    private void AssertRuntime(
        string playerId, string weaponId, int energy, int cooldown, int uses)
    {
        WDWeaponPlayerRuntimeState player = runtime.FindPlayer(playerId);
        WDWeaponRuntimeEntry weapon = player.GetWeapon(weaponId);
        Assert.That(player.Energy, Is.EqualTo(energy));
        Assert.That(weapon.CooldownRemaining, Is.EqualTo(cooldown));
        Assert.That(weapon.Uses, Is.EqualTo(uses));
    }

    private void SetLevel(string playerId, string weaponId, int level) =>
        runtime.FindPlayer(playerId).GetWeapon(weaponId).SetLevel(
            level, catalog.FindWeapon(weaponId).MaximumLevel);

    private sealed class AllWeaponsLoadout :
        IWDMatchWeaponLoadoutSource, IWDWeaponLevelSource
    {
        private readonly WDArmoryCatalogConfig catalog;
        public AllWeaponsLoadout(WDArmoryCatalogConfig catalog) => this.catalog = catalog;
        public bool TryGetWeaponIds(string playerId, out IReadOnlyList<string> ids)
        {
            ids = catalog.Weapons.Select(item => item.Id).ToArray();
            return true;
        }
        public bool TryGetLevel(string playerId, string weaponId, out int level)
        {
            WDWeaponDefinition weapon = catalog.FindWeapon(weaponId);
            level = weapon?.MaximumLevel ?? 0;
            return weapon != null;
        }
    }

    private sealed class TestSource :
        IWDGameWeaponMutableStateSource, IWDGameWeaponStateSource
    {
        private sealed class Territory
        {
            public string Id;
            public string Owner;
            public int Troops;
            public WDWeaponTerritoryStateKind State;
        }

        private readonly List<WDGameWeaponPlayerView> players = new();
        private readonly Dictionary<string, Territory> territories = new(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> neighbours = new(StringComparer.Ordinal);
        public int CurrentRound { get; set; } = 1;
        public string ActiveMapId => "execution_test";
        public IReadOnlyList<WDGameWeaponPlayerView> GetPlayers() => players;
        public IReadOnlyList<string> GetOfficialPlayerOrder() =>
            players.Select(item => item.PlayerId).ToArray();
        public IReadOnlyList<WDGameWeaponTerritoryView> GetTerritories() =>
            territories.Values.Select(View).ToArray();
        public WDGameWeaponTerritoryView FindTerritory(string id) =>
            id != null && territories.TryGetValue(id, out Territory value) ? View(value) : null;
        public bool PlayerExists(string id) => players.Any(item => item.PlayerId == id);
        public bool AreAllied(string first, string second)
        {
            WDGameWeaponPlayerView a = players.FirstOrDefault(item => item.PlayerId == first);
            WDGameWeaponPlayerView b = players.FirstOrDefault(item => item.PlayerId == second);
            return a != null && b != null && a.PlayerId != b.PlayerId &&
                players.Count(item => item.TeamIndex == a.TeamIndex) > 1 &&
                a.TeamIndex == b.TeamIndex;
        }
        public bool AreConnected(string a, string b, TipoConexaoMapa type) =>
            neighbours.TryGetValue(a, out HashSet<string> values) && values.Contains(b);
        public IReadOnlyList<string> GetNeighbours(string id, TipoConexaoMapa? type) =>
            neighbours.TryGetValue(id, out HashSet<string> values)
                ? values.OrderBy(x => x, StringComparer.Ordinal).ToArray() : Array.Empty<string>();
        public void Connect(string a, string b)
        {
            if (!neighbours.TryGetValue(a, out HashSet<string> av)) neighbours[a] = av = new();
            if (!neighbours.TryGetValue(b, out HashSet<string> bv)) neighbours[b] = bv = new();
            av.Add(b); bv.Add(a);
        }
        public bool TryApplyTerritory(
            string id, string owner, int troops, WDWeaponTerritoryStateKind state)
        {
            if (!territories.TryGetValue(id, out Territory value)) return false;
            value.Owner = owner;
            value.Troops = troops;
            value.State = state;
            return true;
        }
        public string Fingerprint() => string.Join("|", territories.Values
            .OrderBy(item => item.Id).Select(item => $"{item.Id}:{item.Owner}:{item.Troops}"));
        public static TestSource Create()
        {
            var source = new TestSource();
            source.players.AddRange(new[]
            {
                new WDGameWeaponPlayerView("p1", 0, 0),
                new WDGameWeaponPlayerView("p2", 1, 0),
                new WDGameWeaponPlayerView("p3", 2, 1)
            });
            source.Add("owned", "p1", 5);
            source.Add("owned2", "p1", 3);
            source.Add("owned3", "p1", 2);
            source.Add("ally", "p2", 4);
            source.Add("enemy", "p3", 4);
            source.Add("enemy2", "p3", 2);
            return source;
        }
        private void Add(string id, string owner, int troops) => territories.Add(id,
            new Territory { Id = id, Owner = owner, Troops = troops,
                State = WDWeaponTerritoryStateKind.Occupied });
        private static WDGameWeaponTerritoryView View(Territory value) =>
            new(value.Id, value.Owner, value.Troops, value.State);
    }

    private sealed class FixedRandom : IWDWeaponRandomSource
    {
        private readonly double value;
        public FixedRandom(double value) => this.value = value;
        public double NextUnit(string stableKey) => value;
    }
}
