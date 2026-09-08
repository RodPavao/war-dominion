using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public sealed class WDSupportWeaponTests
{
    // ============================================================
    // 01. CATÁLOGO E TABELAS OFICIAIS
    // ============================================================

    [Test]
    public void OfficialCatalogContainsEightSupportWeaponsWithStableIds()
    {
        IReadOnlyList<WDWeaponDefinition> definitions = Definitions();
        Assert.That(definitions.Count, Is.EqualTo(8));
        Assert.That(definitions.Select(item => item.Id).Distinct().Count(), Is.EqualTo(8));
        Assert.That(definitions.All(item => item.CategoryId == "support"), Is.True);
    }

    [TestCase(1, 2)] [TestCase(2, 3)] [TestCase(3, 4)] [TestCase(4, 5)] [TestCase(5, 6)] [TestCase(6, 7)]
    public void AirStrikeDamageMatchesLevel(int level, int expected) =>
        Assert.That(WDSupportWeaponTables.AirStrikeDamage(level), Is.EqualTo(expected));

    [TestCase(1, 3)] [TestCase(2, 4)] [TestCase(3, 5)] [TestCase(4, 6)] [TestCase(5, 7)] [TestCase(6, 8)]
    public void ReinforcementMatchesLevel(int level, int expected) =>
        Assert.That(WDSupportWeaponTables.Reinforcement(level), Is.EqualTo(expected));

    [TestCase(1, 5)] [TestCase(2, 7)] [TestCase(3, 10)] [TestCase(4, 12)] [TestCase(5, 15)] [TestCase(6, 18)]
    public void AirTransportCapacityMatchesLevel(int level, int expected) =>
        Assert.That(WDSupportWeaponTables.AirTransportCapacity(level), Is.EqualTo(expected));

    [TestCase(1, .50)] [TestCase(2, .65)] [TestCase(3, .80)] [TestCase(4, .90)]
    public void FlakChanceMatchesLevel(int level, double expected) =>
        Assert.That(WDSupportWeaponTables.FlakChance(level), Is.EqualTo(expected).Within(.001));

    [TestCase(1, 2)] [TestCase(2, 3)] [TestCase(3, 4)] [TestCase(4, 5)]
    public void BunkerBarrierMatchesLevel(int level, int expected) =>
        Assert.That(WDSupportWeaponTables.BunkerBarrier(level), Is.EqualTo(expected));

    [TestCase(1, 4)] [TestCase(2, 5)] [TestCase(3, 6)] [TestCase(4, 7)] [TestCase(5, 8)] [TestCase(6, 9)]
    public void RecoveryLimitMatchesLevel(int level, int expected) =>
        Assert.That(WDSupportWeaponTables.RecoveryLimit(level), Is.EqualTo(expected));

    [Test]
    public void ReserveTableMatchesAllDocumentedCells()
    {
        int[,] expected = { {1,1,2,4,5,6}, {1,1,2,4,6,7}, {1,2,2,5,6,7}, {1,2,2,5,7,9}, {1,2,3,6,8,10}, {1,2,3,8,10,12} };
        for (int level = 1; level <= 6; level++)
            for (int lost = 1; lost <= 6; lost++)
                Assert.That(WDSupportWeaponTables.ReserveTroops(level, lost), Is.EqualTo(expected[level - 1, lost - 1]));
        Assert.That(WDSupportWeaponTables.ReserveTroops(6, 99), Is.EqualTo(12));
    }

    [TestCase(1, 1,1,1)] [TestCase(2, 2,1,1)] [TestCase(3, 2,2,1)]
    [TestCase(4, 2,2,2)] [TestCase(5, 3,2,2)] [TestCase(6, 3,3,2)]
    public void MercenaryTableMatchesLevel(int level, int first, int second, int third) =>
        Assert.That(WDSupportWeaponTables.MercenaryPayments(level), Is.EqualTo(new[] { first, second, third }));

    // ============================================================
    // 02. ATAQUE AÉREO, FLAK E BUNKER
    // ============================================================

    [Test]
    public void AirStrikeRequiresDirectNeighbourAndNeverConquers()
    {
        TestContext context = Context(WDSupportWeaponIds.AirStrike, 6, 100);
        Territory(context, "a", "p1", 5).AddNeighbour("b");
        WDWeaponTerritoryState target = Territory(context, "b", "p2", 3);
        WDWeaponActionResult result = Resolve(context, Action(WDSupportWeaponIds.AirStrike, "a", "b"));
        Assert.That(result.State, Is.EqualTo(WDWeaponActionState.Executed));
        Assert.That(target.State, Is.EqualTo(WDWeaponTerritoryStateKind.Empty));
        Assert.That(target.OwnerPlayerId, Is.Empty);
    }

    [Test]
    public void AirStrikeRejectsNonNeighbour()
    {
        TestContext context = Context(WDSupportWeaponIds.AirStrike, 1, 100);
        Territory(context, "a", "p1", 5); Territory(context, "b", "p2", 5);
        Assert.That(Resolve(context, Action(WDSupportWeaponIds.AirStrike, "a", "b")).State, Is.EqualTo(WDWeaponActionState.Failed));
    }

    [Test]
    public void BunkerAbsorbsDamageAndOverflowHitsTroops()
    {
        TestContext context = Context(WDSupportWeaponIds.AirStrike, 6, 100);
        Territory(context, "a", "p1", 5).AddNeighbour("b");
        WDWeaponTerritoryState target = Territory(context, "b", "p2", 10);
        var bunker = new WDWeaponInstallationState("bn", WDSupportWeaponIds.Bunker, "b", "p2", 0, 5, 0, 0, 4);
        target.AddInstallation(bunker);
        Resolve(context, Action(WDSupportWeaponIds.AirStrike, "a", "b"));
        Assert.That(bunker.BarrierRemaining, Is.Zero);
        Assert.That(target.Troops, Is.EqualTo(8));
    }

    [TestCase(0.0, WDWeaponExecutionOutcome.Intercepted)]
    [TestCase(0.99, WDWeaponExecutionOutcome.Executed)]
    public void FlakInterceptionCanBeForced(double roll, WDWeaponExecutionOutcome expected)
    {
        TestContext context = Context(WDSupportWeaponIds.AirStrike, 1, 100, roll);
        Territory(context, "a", "p1", 5).AddNeighbour("b");
        WDWeaponTerritoryState target = Territory(context, "b", "p2", 5);
        WDWeaponTerritoryState flakSite = Territory(context, "f", "p2", 2); flakSite.AddNeighbour("b");
        flakSite.AddInstallation(new WDWeaponInstallationState("flak", WDSupportWeaponIds.FlakTower, "f", "p2", 3, 0, 0, 0, 1, true));
        Assert.That(Resolve(context, Action(WDSupportWeaponIds.AirStrike, "a", "b")).ExecutionOutcome, Is.EqualTo(expected));
        Assert.That(target.Troops, Is.EqualTo(expected == WDWeaponExecutionOutcome.Intercepted ? 5 : 3));
    }

    [Test]
    public void HighestLevelFlakWinsWithDeterministicTieBreak()
    {
        TestContext context = Context(WDSupportWeaponIds.AirStrike, 1, 100, .7);
        WDWeaponTerritoryState target = Territory(context, "target", "p2", 5);
        foreach ((string territory, string id, int level) in new[] { ("f1", "z", 2), ("f2", "b", 4), ("f3", "a", 4) })
        { WDWeaponTerritoryState site = Territory(context, territory, "p2", 1); site.AddNeighbour("target"); site.AddInstallation(new WDWeaponInstallationState(id, WDSupportWeaponIds.FlakTower, territory, "p2", 3, 0, 0, 0, level, true)); }
        Assert.That(WDSupportWeaponInteractionService.SelectCoveringFlak(context, target).InstallationId, Is.EqualTo("a"));
    }

    [Test]
    public void FlakCoversOwnSiteAndAlliedNeighbourButNotUnrelatedTerritory()
    {
        TestContext context = Context(WDSupportWeaponIds.AirStrike, 1, 100); context.Ally("p1", "p2");
        WDWeaponTerritoryState site = Territory(context, "site", "p1", 1);
        site.AddInstallation(new WDWeaponInstallationState("f", WDSupportWeaponIds.FlakTower, "site", "p1", 3, 0, 0, 0, 2, true));
        WDWeaponTerritoryState ally = Territory(context, "ally", "p2", 1); site.AddNeighbour("ally");
        WDWeaponTerritoryState far = Territory(context, "far", "p2", 1);
        Assert.That(WDSupportWeaponInteractionService.SelectCoveringFlak(context, site), Is.Not.Null);
        Assert.That(WDSupportWeaponInteractionService.SelectCoveringFlak(context, ally), Is.Not.Null);
        Assert.That(WDSupportWeaponInteractionService.SelectCoveringFlak(context, far), Is.Null);
    }

    [Test]
    public void EmptyTerritoryDestroysFlakAndBunkerButCapturePreservesState()
    {
        WDWeaponTerritoryState territory = new("t", "p1", 1);
        var flak = new WDWeaponInstallationState("f", WDSupportWeaponIds.FlakTower, "t", "p1", 2, 0, 0, 0, 3, true);
        var bunker = new WDWeaponInstallationState("b", WDSupportWeaponIds.Bunker, "t", "p1", 0, 4, 0, 0, 3);
        territory.AddInstallation(flak); territory.AddInstallation(bunker);
        territory.Capture("p2");
        Assert.That(flak.OwnerPlayerId, Is.EqualTo("p2")); Assert.That(flak.DurationRemaining, Is.EqualTo(2));
        Assert.That(bunker.BarrierRemaining, Is.EqualTo(4));
        territory.MakeEmpty();
        Assert.That(flak.IsDestroyed, Is.True); Assert.That(bunker.IsDestroyed, Is.True);
    }

    [Test]
    public void FlakDurationExpiresDeterministicallyDuringConsolidation()
    {
        TestContext context = Context(WDSupportWeaponIds.FlakTower, 1, 100);
        WDWeaponTerritoryState site = Territory(context, "site", "p1", 1);
        var flak = new WDWeaponInstallationState("f", WDSupportWeaponIds.FlakTower,
            "site", "p1", 1, 0, 0, 0, 1, true);
        site.AddInstallation(flak);
        Pipeline().Resolve(Array.Empty<WDWeaponAction>(), context);
        Assert.That(flak.DurationRemaining, Is.Zero);
        Assert.That(flak.IsDestroyed, Is.True);
    }

    // ============================================================
    // 03. REFORÇOS, TRANSPORTE E HISTÓRICO
    // ============================================================

    [TestCase(false)] [TestCase(true)]
    public void ReinforcementsApplyDirectlyToOwnedOrAlliedTerritory(bool allied)
    {
        TestContext context = Context(WDSupportWeaponIds.Reinforcements, 3, 100);
        if (allied) context.Ally("p1", "p2");
        WDWeaponTerritoryState target = Territory(context, "t", allied ? "p2" : "p1", 2);
        Resolve(context, Action(WDSupportWeaponIds.Reinforcements, target: "t"));
        Assert.That(target.Troops, Is.EqualTo(7)); Assert.That(context.Pool("p1"), Is.Zero);
    }

    [Test]
    public void AirTransportWorksAtAnyDistanceAndLeavesOneAtOrigin()
    {
        TestContext context = Context(WDSupportWeaponIds.AirTransport, 1, 100);
        WDWeaponTerritoryState origin = Territory(context, "a", "p1", 6);
        WDWeaponTerritoryState target = Territory(context, "z", "p1", 1);
        Resolve(context, Action(WDSupportWeaponIds.AirTransport, "a", "z", parameters: new[] { new WDWeaponActionParameter("troops", "5") }));
        Assert.That(origin.Troops, Is.EqualTo(1)); Assert.That(target.Troops, Is.EqualTo(6));
    }

    [TestCase(WDSupportWeaponIds.Bunker)] [TestCase(WDSupportWeaponIds.Minefield)]
    public void AirTransportRejectsNonTransportableInstallation(string weaponId)
    {
        TestContext context = Context(WDSupportWeaponIds.AirTransport, 6, 100);
        WDWeaponTerritoryState origin = Territory(context, "a", "p1", 10); Territory(context, "b", "p1", 1);
        origin.AddInstallation(new WDWeaponInstallationState("i", weaponId, "a", "p1", 0, 0, 0, 0, 1, false));
        Assert.That(Resolve(context, Action(WDSupportWeaponIds.AirTransport, "a", "b", "i", new[] { new WDWeaponActionParameter("troops", "2") })).State, Is.EqualTo(WDWeaponActionState.Failed));
    }

    [Test]
    public void AirTransportRejectsDuplicateInstallationTypeAtDestination()
    {
        TestContext context = Context(WDSupportWeaponIds.AirTransport, 6, 100);
        WDWeaponTerritoryState origin = Territory(context, "a", "p1", 10); WDWeaponTerritoryState target = Territory(context, "b", "p1", 1);
        origin.AddInstallation(new WDWeaponInstallationState("i1", WDSupportWeaponIds.FlakTower, "a", "p1", 2, 0, 0, 0, 1, true));
        target.AddInstallation(new WDWeaponInstallationState("i2", WDSupportWeaponIds.FlakTower, "b", "p1", 2, 0, 0, 0, 1, true));
        Assert.That(Resolve(context, Action(WDSupportWeaponIds.AirTransport, "a", "b", "i1", new[] { new WDWeaponActionParameter("troops", "2") })).State, Is.EqualTo(WDWeaponActionState.Failed));
    }

    [Test]
    public void ReserveTroopsUsesAuthoritativeLossHistoryAndZeroIsIneligible()
    {
        TestContext context = Context(WDSupportWeaponIds.ReserveTroops, 6, 100);
        Assert.That(Resolve(context, Action(WDSupportWeaponIds.ReserveTroops)).State, Is.EqualTo(WDWeaponActionState.Failed));
        context.Lost["p1"] = 5;
        Assert.That(Resolve(context, Action(WDSupportWeaponIds.ReserveTroops, actionId: "second")).State, Is.EqualTo(WDWeaponActionState.Executed));
        Assert.That(context.Pool("p1"), Is.EqualTo(10));
    }

    [TestCase(3, 3)] [TestCase(20, 9)]
    public void RecoveryDoesNotExceedRecordedCasualtiesOrLevelLimit(int casualties, int expected)
    {
        TestContext context = Context(WDSupportWeaponIds.TroopRecovery, 6, 100);
        WDWeaponTerritoryState target = Territory(context, "t", "p1", 2);
        context.Records["t"] = new WDPreviousRoundTerritoryRecord("t", "p1", "p1", casualties, true);
        Resolve(context, Action(WDSupportWeaponIds.TroopRecovery, target: "t"));
        Assert.That(target.Troops, Is.EqualTo(2 + expected));
    }

    [Test]
    public void RecoveryRejectsTerritoryThatChangedOwner()
    {
        TestContext context = Context(WDSupportWeaponIds.TroopRecovery, 6, 100);
        Territory(context, "t", "p1", 2); context.Records["t"] = new WDPreviousRoundTerritoryRecord("t", "p2", "p1", 5, true);
        Assert.That(Resolve(context, Action(WDSupportWeaponIds.TroopRecovery, target: "t")).State, Is.EqualTo(WDWeaponActionState.Failed));
    }

    [Test]
    public void MercenariesPayAuthorAcrossThreeRoundsExactlyOnce()
    {
        TestContext context = Context(WDSupportWeaponIds.Mercenaries, 6, 100); context.CurrentRoundValue = 4;
        WDWeaponResolutionPipeline pipeline = Pipeline();
        pipeline.Resolve(new[] { Action(WDSupportWeaponIds.Mercenaries) }, context);
        Assert.That(context.Pool("p1"), Is.EqualTo(3));
        context.CurrentRoundValue = 5; pipeline.Resolve(Array.Empty<WDWeaponAction>(), context);
        Assert.That(context.Pool("p1"), Is.EqualTo(6));
        pipeline.Resolve(Array.Empty<WDWeaponAction>(), context); Assert.That(context.Pool("p1"), Is.EqualTo(6));
        context.CurrentRoundValue = 6; pipeline.Resolve(Array.Empty<WDWeaponAction>(), context);
        Assert.That(context.Pool("p1"), Is.EqualTo(8));
    }

    // ============================================================
    // 04. FIXTURE AUTORITATIVO E PIPELINE REAL
    // ============================================================

    private static TestContext Context(string weaponId, int level, int energy, double roll = .99)
    {
        WDWeaponDefinition definition = Definitions().Single(item => item.Id == weaponId);
        var context = new TestContext(definition, roll); context.AddPlayer("p1", energy, level); context.AddPlayer("p2", energy, level); return context;
    }
    private static WDWeaponTerritoryState Territory(TestContext context, string id, string owner, int troops)
    { var territory = new WDWeaponTerritoryState(id, owner, troops); context.AddTerritory(territory); return territory; }
    private static WDWeaponAction Action(string weaponId, string origin = "", string target = "", string installation = "",
        IEnumerable<WDWeaponActionParameter> parameters = null, string actionId = "action")
    {
        WDWeaponDefinition definition = Definitions().Single(item => item.Id == weaponId);
        return new WDWeaponAction(actionId, "p1", weaponId, 1, definition.ResolutionPhase, 0, origin, target, "", installation, parameters);
    }
    private static WDWeaponResolutionPipeline Pipeline() { var pipeline = new WDWeaponResolutionPipeline(); pipeline.RegisterSupportWeapons(); return pipeline; }
    private static WDWeaponActionResult Resolve(TestContext context, WDWeaponAction action) => Pipeline().Resolve(new[] { action }, context).Single();

    private static IReadOnlyList<WDWeaponDefinition> Definitions()
    {
        return new[]
        {
            Definition(WDSupportWeaponIds.AirStrike, 6, 20, 3, 1, WDWeaponBehaviorType.Specialized, WDWeaponResolutionPhase.ProgrammedOffense, true, true, false, true),
            Definition(WDSupportWeaponIds.Reinforcements, 6, 25, 3, 1, WDWeaponBehaviorType.Reinforcement, WDWeaponResolutionPhase.Reinforcement, false, true),
            Definition(WDSupportWeaponIds.AirTransport, 6, 5, 3, 0, WDWeaponBehaviorType.Logistics, WDWeaponResolutionPhase.InstallationAndLogistics, true, true),
            Definition(WDSupportWeaponIds.FlakTower, 4, 5, 0, 0, WDWeaponBehaviorType.Installation, WDWeaponResolutionPhase.InstallationAndLogistics, false, true, true),
            Definition(WDSupportWeaponIds.Bunker, 4, 10, 0, 0, WDWeaponBehaviorType.Installation, WDWeaponResolutionPhase.InstallationAndLogistics, false, true, true),
            Definition(WDSupportWeaponIds.ReserveTroops, 6, 15, 3, 0, WDWeaponBehaviorType.Reinforcement, WDWeaponResolutionPhase.Reinforcement),
            Definition(WDSupportWeaponIds.TroopRecovery, 6, 15, 3, 0, WDWeaponBehaviorType.Reinforcement, WDWeaponResolutionPhase.Reinforcement, false, true),
            Definition(WDSupportWeaponIds.Mercenaries, 6, 20, 3, 0, WDWeaponBehaviorType.Reinforcement, WDWeaponResolutionPhase.Reinforcement)
        };
    }

    private static WDWeaponDefinition Definition(string id, int levels, int energy,
        int cooldown, int uses, WDWeaponBehaviorType behavior, WDWeaponResolutionPhase phase,
        bool origin = false, bool target = false, bool installation = false, bool empty = false)
    {
        var definition = new WDWeaponDefinition(id, id, "support", null);
        definition.ConfigureRuntime(levels, energy, cooldown, uses, behavior, phase,
            WDWeaponCostTiming.OnExecution, origin, target, false, empty, installation);
        return definition;
    }

    private sealed class FixedRandom : IWDWeaponRandomSource { private readonly double value; public FixedRandom(double value) => this.value = value; public double NextUnit(string stableKey) => value; }
    private sealed class TestContext : IWDSupportWeaponContext, IWDWeaponTerritoryEnumerable
    {
        private readonly WDWeaponDefinition definition; private readonly Dictionary<string, WDWeaponPlayerRuntimeState> players = new();
        private readonly Dictionary<string, WDWeaponTerritoryState> territories = new(); private readonly HashSet<string> allies = new(); private readonly Dictionary<string, int> pools = new();
        public readonly Dictionary<string, int> Lost = new(); public readonly Dictionary<string, WDPreviousRoundTerritoryRecord> Records = new();
        public readonly List<WDMercenaryPayment> Payments = new(); public int CurrentRoundValue = 1;
        public TestContext(WDWeaponDefinition definition, double roll) { this.definition = definition; Random = new FixedRandom(roll); }
        public int CurrentRound => CurrentRoundValue; public IWDWeaponRandomSource Random { get; } public IEnumerable<WDMercenaryPayment> MercenaryPayments => Payments; public IEnumerable<WDWeaponTerritoryState> Territories => territories.Values;
        public void AddPlayer(string id, int energy, int level) { var player = new WDWeaponPlayerRuntimeState(id, energy); player.RegisterWeapon(definition.Id, level); players[id] = player; }
        public void AddTerritory(WDWeaponTerritoryState territory) => territories[territory.TerritoryId] = territory;
        public void Ally(string a, string b) { allies.Add(a + ":" + b); allies.Add(b + ":" + a); }
        public int Pool(string id) => pools.TryGetValue(id, out int value) ? value : 0;
        public WDWeaponDefinition FindDefinition(string weaponId) => weaponId == definition.Id ? definition : null;
        public WDWeaponPlayerRuntimeState FindPlayer(string playerId) => players.TryGetValue(playerId, out var value) ? value : null;
        public int GetPlayerOrder(string playerId) => playerId == "p1" ? 0 : 1;
        public WDWeaponTerritoryState FindTerritory(string territoryId) => territories.TryGetValue(territoryId ?? "", out var value) ? value : null;
        public bool AreAllied(string firstPlayerId, string secondPlayerId) => allies.Contains(firstPlayerId + ":" + secondPlayerId);
        public int GetTerritoriesLostLastRound(string playerId) => Lost.TryGetValue(playerId, out int value) ? value : 0;
        public WDPreviousRoundTerritoryRecord FindPreviousRoundRecord(string territoryId) => Records.TryGetValue(territoryId ?? "", out var value) ? value : null;
        public void AddReinforcementPool(string playerId, int amount, string sourceWeaponId) => pools[playerId] = Pool(playerId) + amount;
        public void AddMercenaryPayment(WDMercenaryPayment payment) => Payments.Add(payment);
    }
}
