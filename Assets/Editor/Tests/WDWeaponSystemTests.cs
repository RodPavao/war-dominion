using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class WDWeaponSystemTests
{
    private WDArmoryCatalogConfig catalog;

    [SetUp]
    public void SetUp()
    {
        catalog = ScriptableObject.CreateInstance<WDArmoryCatalogConfig>();
        catalog.Configure(Array.Empty<WDWeaponCategoryDefinition>(), Array.Empty<WDWeaponDefinition>());
        catalog.ConfigureProgressionProfiles(StandardProgressions());
    }

    [TearDown]
    public void TearDown() => UnityEngine.Object.DestroyImmediate(catalog);

    // ============================================================
    // 01. CATÁLOGO E PROGRESSÃO DATA-DRIVEN
    // ============================================================

    [Test]
    public void CatalogAcceptsMoreThanV1WeaponCount()
    {
        catalog.Configure(Array.Empty<WDWeaponCategoryDefinition>(),
            Enumerable.Range(0, 30).Select(index => Weapon($"weapon_{index}", 1, 0, 0)));
        Assert.That(catalog.Weapons.Count, Is.EqualTo(30));
        Assert.That(WDWeaponCatalogValidator.Validate(catalog), Is.Empty);
    }

    [Test]
    public void OneLevelWeaponIsAlreadyAtMaximumAndCannotUpgrade()
    {
        WDWeaponDefinition weapon = Weapon("peace", 1, 15, 0);
        catalog.Configure(Array.Empty<WDWeaponCategoryDefinition>(), new[] { weapon });
        var profile = new WDArmoryProfile(false, 100000, 0);
        profile.AddProgress(new WDWeaponProgress(weapon.Id, true, 1, 99999));
        var service = new WDArmoryService(catalog, profile);

        Assert.That(service.GetCollectionState(weapon.Id), Is.EqualTo(WDWeaponCollectionState.MaxLevel));
        Assert.That(service.Upgrade(weapon.Id), Is.EqualTo(WDArmoryEditResult.AlreadyMaxLevel));
    }

    [TestCase(4, 2, 500, 2500)]
    [TestCase(4, 3, 1000, 10000)]
    [TestCase(4, 4, 10000, 50000)]
    [TestCase(6, 2, 100, 500)]
    [TestCase(6, 3, 500, 2500)]
    [TestCase(6, 4, 1000, 10000)]
    [TestCase(6, 5, 5000, 25000)]
    [TestCase(6, 6, 10000, 50000)]
    public void StandardProgressionCurvesReturnConfiguredCosts(
        int maximumLevel, int targetLevel, int tokens, int coins)
    {
        WDWeaponDefinition weapon = Weapon("curve", maximumLevel, 10, 3);
        WDWeaponLevelDefinition upgrade = catalog.FindUpgrade(weapon, targetLevel);
        Assert.That(upgrade.RequiredTokens, Is.EqualTo(tokens));
        Assert.That(upgrade.CoinCost, Is.EqualTo(coins));
    }

    [Test]
    public void WeaponSpecificTokensRemainAfterMaximumLevel()
    {
        var progress = new WDWeaponProgress("air_strike", true, 6, 12345);
        Assert.That(progress.Level, Is.EqualTo(6));
        Assert.That(progress.Tokens, Is.EqualTo(12345));
        Assert.That(progress.WeaponId, Is.EqualTo("air_strike"));
    }

    [Test]
    public void EnergyAndCooldownDoNotDependOnPlayerLevel()
    {
        WDWeaponDefinition weapon = Weapon("air_strike", 6, 20, 3);
        var low = new WDWeaponRuntimeEntry(weapon.Id, 1);
        var high = new WDWeaponRuntimeEntry(weapon.Id, 6);
        Assert.That(weapon.EnergyCost, Is.EqualTo(20));
        Assert.That(weapon.CooldownRounds, Is.EqualTo(3));
        Assert.That(low.WeaponId, Is.EqualTo(high.WeaponId));
    }

    // ============================================================
    // 02. ESTADO RUNTIME, ENERGY E INSTALAÇÕES
    // ============================================================

    [Test]
    public void PlayerStartsWithZeroEnergy()
    {
        Assert.That(new WDWeaponPlayerRuntimeState("p1").Energy, Is.Zero);
    }

    [Test]
    public void EnergyPlantDefinitionCanCostZero()
    {
        Assert.That(Weapon("energy_plant", 4, 0, 5).EnergyCost, Is.Zero);
    }

    [Test]
    public void RuntimeStateIsSeparateFromDefinition()
    {
        WDWeaponDefinition definition = Weapon("sniper", 6, 15, 3);
        var first = new WDWeaponPlayerRuntimeState("p1");
        var second = new WDWeaponPlayerRuntimeState("p2");
        first.RegisterWeapon(definition.Id, 2).StartCooldown(3);
        second.RegisterWeapon(definition.Id, 5);
        Assert.That(first.GetWeapon(definition.Id).CooldownRemaining, Is.EqualTo(3));
        Assert.That(second.GetWeapon(definition.Id).CooldownRemaining, Is.Zero);
        Assert.That(definition.CooldownRounds, Is.EqualTo(3));
    }

    [Test]
    public void InstallationCapturePreservesRemainingState()
    {
        var installation = new WDWeaponInstallationState(
            "rocket_1", "rockets", "territory_a", "p1", 2, 4, 2, 3);
        installation.ConsumeCharge();
        installation.ConsumeDuration();
        installation.ConsumePayment();
        installation.Capture("p2");

        Assert.That(installation.OwnerPlayerId, Is.EqualTo("p2"));
        Assert.That(installation.DurationRemaining, Is.EqualTo(1));
        Assert.That(installation.BarrierRemaining, Is.EqualTo(4));
        Assert.That(installation.ChargesRemaining, Is.EqualTo(1));
        Assert.That(installation.PaymentsRemaining, Is.EqualTo(2));
    }

    // ============================================================
    // 03. PIPELINE, CUSTOS E REVALIDAÇÃO
    // ============================================================

    [Test]
    public void PipelineExposesEveryV2PhaseInDeterministicOrder()
    {
        var pipeline = new WDWeaponResolutionPipeline();
        var observed = new List<WDWeaponResolutionPhase>();
        foreach (WDWeaponResolutionPhase phase in pipeline.Phases.Reverse())
            pipeline.RegisterPhaseHandler(new FakePhaseHandler(phase, observed));
        pipeline.Resolve(Array.Empty<WDWeaponAction>(),
            Context(Weapon("phase", 1, 0, 0), ("p1", 0)));

        int[] phases = pipeline.Phases.Select(item => (int)item).ToArray();
        Assert.That(phases, Is.Ordered);
        Assert.That(pipeline.Phases.First(), Is.EqualTo(WDWeaponResolutionPhase.RoundStart));
        Assert.That(pipeline.Phases.Last(), Is.EqualTo(WDWeaponResolutionPhase.Consolidation));
        Assert.That(pipeline.Phases.Count, Is.EqualTo(14));
        Assert.That(observed, Is.EqualTo(pipeline.Phases));
    }

    [Test]
    public void ActionOrderUsesPlayerProgramOrderAndStableId()
    {
        WDWeaponDefinition weapon = Weapon("test", 1, 0, 0);
        var context = Context(weapon, ("p1", 1), ("p2", 0));
        var observed = new List<string>();
        var handler = new FakeHandler(observed);
        var pipeline = new WDWeaponResolutionPipeline();
        pipeline.RegisterActionHandler(handler);

        pipeline.Resolve(new[]
        {
            Action("z", "p1", weapon, 0),
            Action("b", "p2", weapon, 1),
            Action("a", "p2", weapon, 1)
        }, context);

        Assert.That(observed, Is.EqualTo(new[] { "a", "b", "z" }));
    }

    [Test]
    public void AnnulledBeforeExecutionDoesNotSpendEnergyOrStartCooldown()
    {
        WDWeaponDefinition weapon = Weapon("peace_target", 1, 20, 3);
        FakeContext context = Context(weapon, ("p1", 0));
        context.Player("p1").AddEnergy(20);
        var pipeline = new WDWeaponResolutionPipeline();
        pipeline.RegisterActionHandler(new FakeHandler(
            revalidation: WDWeaponRevalidationOutcome.Annulled));
        WDWeaponAction action = Action("annulled", "p1", weapon, 0);

        WDWeaponActionResult result = pipeline.Resolve(new[] { action }, context).Single();
        Assert.That(result.State, Is.EqualTo(WDWeaponActionState.Annulled));
        Assert.That(context.Player("p1").Energy, Is.EqualTo(20));
        Assert.That(context.Player("p1").GetWeapon(weapon.Id).CooldownRemaining, Is.Zero);
    }

    [Test]
    public void ExecutedActionSpendsEnergyAndStartsCooldown()
    {
        WDWeaponDefinition weapon = Weapon("strike", 1, 20, 3);
        FakeContext context = Context(weapon, ("p1", 0));
        context.Player("p1").AddEnergy(25);
        var pipeline = new WDWeaponResolutionPipeline();
        pipeline.RegisterActionHandler(new FakeHandler());

        WDWeaponActionResult result = pipeline.Resolve(
            new[] { Action("executed", "p1", weapon, 0) }, context).Single();
        Assert.That(result.State, Is.EqualTo(WDWeaponActionState.Executed));
        Assert.That(context.Player("p1").Energy, Is.EqualTo(5));
        Assert.That(context.Player("p1").GetWeapon(weapon.Id).CooldownRemaining, Is.EqualTo(3));
    }

    [Test]
    public void UpfrontCostTimingCommitsWhenScheduled()
    {
        WDWeaponDefinition drone = Weapon(
            "drone", 6, 25, 3, WDWeaponCostTiming.OnProgramming);
        var player = new WDWeaponPlayerRuntimeState("p1");
        player.AddEnergy(25);
        player.RegisterWeapon(drone.Id, 1);
        var scheduler = new WDWeaponActionScheduler();

        Assert.That(scheduler.TrySchedule(Action("drone_1", "p1", drone, 0), drone, player), Is.True);
        Assert.That(player.Energy, Is.Zero);
        Assert.That(player.GetWeapon(drone.Id).CooldownRemaining, Is.EqualTo(3));
        Assert.That(player.Actions.Single().CostCommitted, Is.True);
    }

    [Test]
    public void RevalidationCanMakeProgrammedActionImpossibleWithoutCost()
    {
        WDWeaponDefinition weapon = Weapon("strike", 1, 20, 3);
        FakeContext context = Context(weapon, ("p1", 0));
        context.Player("p1").AddEnergy(20);
        var pipeline = new WDWeaponResolutionPipeline();
        pipeline.RegisterActionHandler(new FakeHandler(
            revalidation: WDWeaponRevalidationOutcome.Impossible));

        WDWeaponActionResult result = pipeline.Resolve(
            new[] { Action("impossible", "p1", weapon, 0) }, context).Single();
        Assert.That(result.State, Is.EqualTo(WDWeaponActionState.Failed));
        Assert.That(context.Player("p1").Energy, Is.EqualTo(20));
    }

    [Test]
    public void EmptyTerritoryIsNotGloballyRejectedAsTarget()
    {
        WDWeaponDefinition weapon = Weapon("bombardment", 6, 25, 3, targetsEmpty: true);
        Assert.That(weapon.RequiresTargetTerritory, Is.True);
        Assert.That(weapon.CanTargetEmptyTerritory, Is.True);
    }

    [Test]
    public void RepeatingSameActionIdDoesNotExecuteTwice()
    {
        WDWeaponDefinition weapon = Weapon("strike", 1, 0, 0);
        FakeContext context = Context(weapon, ("p1", 0));
        var observed = new List<string>();
        var pipeline = new WDWeaponResolutionPipeline();
        pipeline.RegisterActionHandler(new FakeHandler(observed));
        WDWeaponAction action = Action("idempotent", "p1", weapon, 0);

        pipeline.Resolve(new[] { action }, context);
        pipeline.Resolve(new[] { action }, context);

        Assert.That(observed.Count, Is.EqualTo(1));
    }

    // ============================================================
    // 04. FIXTURES DO DOMÍNIO
    // ============================================================

    private static WDWeaponDefinition Weapon(
        string id, int maximumLevel, int energy, int cooldown,
        WDWeaponCostTiming timing = WDWeaponCostTiming.OnExecution,
        bool targetsEmpty = false)
    {
        var weapon = new WDWeaponDefinition(
            id, id, "test", Array.Empty<WDWeaponLevelDefinition>());
        weapon.ConfigureRuntime(
            maximumLevel, energy, cooldown, 0,
            WDWeaponBehaviorType.Specialized,
            WDWeaponResolutionPhase.ProgrammedOffense,
            timing, needsTerritoryTarget: true, targetsEmpty: targetsEmpty);
        return weapon;
    }

    private static IEnumerable<WDWeaponProgressionProfile> StandardProgressions() =>
        new[]
        {
            new WDWeaponProgressionProfile(1, Array.Empty<WDWeaponLevelDefinition>()),
            new WDWeaponProgressionProfile(4, new[]
            {
                new WDWeaponLevelDefinition(2, 500, 2500),
                new WDWeaponLevelDefinition(3, 1000, 10000),
                new WDWeaponLevelDefinition(4, 10000, 50000)
            }),
            new WDWeaponProgressionProfile(6, new[]
            {
                new WDWeaponLevelDefinition(2, 100, 500),
                new WDWeaponLevelDefinition(3, 500, 2500),
                new WDWeaponLevelDefinition(4, 1000, 10000),
                new WDWeaponLevelDefinition(5, 5000, 25000),
                new WDWeaponLevelDefinition(6, 10000, 50000)
            })
        };

    private static WDWeaponAction Action(
        string id, string playerId, WDWeaponDefinition weapon, int order) =>
        new(id, playerId, weapon.Id, 1, weapon.ResolutionPhase, order,
            targetTerritoryId: "target");

    private static FakeContext Context(
        WDWeaponDefinition weapon, params (string id, int order)[] players)
    {
        var context = new FakeContext(weapon);
        foreach ((string id, int order) in players)
        {
            var player = new WDWeaponPlayerRuntimeState(id);
            player.RegisterWeapon(weapon.Id, 1);
            context.AddPlayer(player, order);
        }
        return context;
    }

    private sealed class FakeContext : IWDWeaponResolutionContext
    {
        private readonly WDWeaponDefinition definition;
        private readonly Dictionary<string, WDWeaponPlayerRuntimeState> players = new();
        private readonly Dictionary<string, int> orders = new();

        public FakeContext(WDWeaponDefinition definition) => this.definition = definition;
        public void AddPlayer(WDWeaponPlayerRuntimeState player, int order)
        {
            players[player.PlayerId] = player;
            orders[player.PlayerId] = order;
        }
        public WDWeaponPlayerRuntimeState Player(string id) => players[id];
        public WDWeaponDefinition FindDefinition(string weaponId) =>
            definition.Id == weaponId ? definition : null;
        public WDWeaponPlayerRuntimeState FindPlayer(string playerId) =>
            players.TryGetValue(playerId, out WDWeaponPlayerRuntimeState player) ? player : null;
        public int GetPlayerOrder(string playerId) =>
            orders.TryGetValue(playerId, out int order) ? order : int.MaxValue;
    }

    private sealed class FakeHandler : IWDWeaponActionHandler
    {
        private readonly List<string> observed;
        private readonly WDWeaponRevalidationOutcome revalidation;

        public FakeHandler(
            List<string> observed = null,
            WDWeaponRevalidationOutcome revalidation = WDWeaponRevalidationOutcome.Valid)
        {
            this.observed = observed;
            this.revalidation = revalidation;
        }

        public bool CanHandle(WDWeaponAction action, WDWeaponDefinition definition) => true;
        public WDWeaponRevalidationResult Revalidate(
            IWDWeaponResolutionContext context, WDWeaponAction action,
            WDWeaponDefinition definition) => new(revalidation);
        public WDWeaponExecutionResult Execute(
            IWDWeaponResolutionContext context, WDWeaponAction action,
            WDWeaponDefinition definition)
        {
            observed?.Add(action.ActionId);
            return new WDWeaponExecutionResult(WDWeaponExecutionOutcome.Executed);
        }
    }

    private sealed class FakePhaseHandler : IWDWeaponPhaseHandler
    {
        private readonly List<WDWeaponResolutionPhase> observed;
        public WDWeaponResolutionPhase Phase { get; }
        public int Order => 0;
        public string StableId => Phase.ToString();

        public FakePhaseHandler(
            WDWeaponResolutionPhase phase,
            List<WDWeaponResolutionPhase> observed)
        {
            Phase = phase;
            this.observed = observed;
        }

        public void Execute(IWDWeaponResolutionContext context) => observed.Add(Phase);
    }
}
