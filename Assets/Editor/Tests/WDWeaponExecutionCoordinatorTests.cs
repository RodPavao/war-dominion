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
    private WDWeaponRoundRuntime roundState;
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
        roundState = new WDWeaponRoundRuntime(); roundState.BeginRound(1);
        mutations = new WDGameWeaponMutationPort(source, runtime, catalog, registry, roundState);
        coordinator = new WDWeaponExecutionCoordinator(
            context, runtime, mutations, registry, catalog, null, roundState);
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
    public void UnknownWeaponIsDeferredWithoutAnyCostOrMutation()
    {
        const string weaponId = "future_non_canonical_weapon";
        var action = new WDWeaponAction("unknown", "p1", weaponId, 1,
            WDWeaponResolutionPhase.Consolidation, 0);
        string before = source.Fingerprint();

        Assert.That(coordinator.Execute(action).Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.Deferred));
        Assert.That(source.Fingerprint(), Is.EqualTo(before));
        Assert.That(runtime.FindPlayer("p1").Energy, Is.EqualTo(200));
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

    // ============================================================
    // 06. SEGUNDO LOTE: EFEITOS REAIS, MODIFICADORES E HISTÓRICO
    // ============================================================

    [Test]
    public void AtomicBombFreezesNeighboursAndAppliesCentralAndCollateralDamage()
    {
        source.Connect("enemy", "owned"); source.Connect("enemy", "ally");
        SetLevel("p1", WDSpecialWeaponIds.AtomicBomb, 1);
        AssertExecuted(Action(WDSpecialWeaponIds.AtomicBomb, "enemy"));
        Assert.That(context.FindTerritory("enemy").Troops, Is.EqualTo(1));
        Assert.That(context.FindTerritory("owned").Troops, Is.EqualTo(3));
        Assert.That(context.FindTerritory("ally").Troops, Is.EqualTo(2));
        AssertRuntime("p1", WDSpecialWeaponIds.AtomicBomb, 160, 3, 1);
    }

    [Test]
    public void SniperKillsOneAndBlocksRocketsAndArtilleryOriginForRound()
    {
        AssertExecuted(Action(WDTacticalWeaponIds.Sniper, "enemy"));
        Assert.That(context.FindTerritory("enemy").Troops, Is.EqualTo(3));
        Assert.That(roundState.IsOriginBlocked("enemy"), Is.True);
        mutations.Install("art", new WDWeaponInstallationState("art", "artillery",
            "enemy", "p3", 2, 0, 0, 0, 1, false, 1));
        source.Connect("enemy", "owned");
        Assert.That(coordinator.ProcessArtillery(1), Is.Zero);
        Assert.That(registry.Find("art").DurationRemaining, Is.EqualTo(2));
    }

    [Test]
    public void PropagandaUsesCurrentTroopsAndPreservesHalfAsNeutral()
    {
        SetLevel("p1", WDSpecialWeaponIds.WarPropaganda, 2);
        AssertExecuted(Action(WDSpecialWeaponIds.WarPropaganda, "enemy"));
        Assert.That(context.FindTerritory("enemy").State,
            Is.EqualTo(WDWeaponTerritoryStateKind.Neutral));
        Assert.That(context.FindTerritory("enemy").Troops, Is.EqualTo(2));
        Assert.That(context.FindTerritory("enemy").OwnerPlayerId, Is.Empty);
    }

    [TestCase(1, 15, 9, 8)]
    [TestCase(2, 20, 10, 8)]
    [TestCase(3, 25, 10, 8)]
    [TestCase(4, 30, 10, 7)]
    [TestCase(5, 35, 10, 7)]
    [TestCase(6, 40, 10, 6)]
    public void SabotageUsesLargestRateAndFloorsNaturalOnly(
        int level, int percent, int natural, int expected)
    {
        SetLevel("p1", WDStrategicWeaponIds.Sabotage, level);
        AssertExecuted(Action(WDStrategicWeaponIds.Sabotage, "",
            targetPlayer: "p3"));
        Assert.That(WDStrategicWeaponTables.SabotageRate(level) * 100,
            Is.EqualTo(percent).Within(.001));
        Assert.That(roundState.ApplyNaturalReinforcement("p3", natural),
            Is.EqualTo(expected));
    }

    [Test]
    public void FirstTransferNullifierWinsWithoutChargingLaterAction()
    {
        AssertExecuted(Action(WDTacticalWeaponIds.TransferNullifier, "",
            targetPlayer: "p3"));
        runtime.FindPlayer("p2").GetWeapon(WDTacticalWeaponIds.TransferNullifier)
            .SetLevel(1, 1);
        WDWeaponAction later = Action(WDTacticalWeaponIds.TransferNullifier, "", "p2",
            targetPlayer: "p3");
        Assert.That(coordinator.Execute(later).Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.Superseded));
        Assert.That(roundState.IsTransferBlocked("p3"), Is.True);
        AssertRuntime("p2", WDTacticalWeaponIds.TransferNullifier, 200, 0, 0);
    }

    [TestCase(1, 4)] [TestCase(2, 5)] [TestCase(3, 6)] [TestCase(4, 7)]
    public void BlitzkriegChangesOnlyCurrentRoundLimit(int level, int expected)
    {
        SetLevel("p1", WDTacticalWeaponIds.Blitzkrieg, level);
        AssertExecuted(Action(WDTacticalWeaponIds.Blitzkrieg, ""));
        Assert.That(roundState.GetTerrestrialLimit("p1"), Is.EqualTo(expected));
        roundState.BeginRound(2);
        Assert.That(roundState.GetTerrestrialLimit("p1"), Is.EqualTo(3));
    }

    [TestCase(1,1,1)] [TestCase(1,6,6)] [TestCase(2,5,6)]
    [TestCase(3,2,2)] [TestCase(4,6,9)] [TestCase(5,3,3)]
    [TestCase(6,1,1)] [TestCase(6,2,2)] [TestCase(6,3,3)]
    [TestCase(6,4,8)] [TestCase(6,5,10)] [TestCase(6,7,12)]
    public void ReserveTroopsUsesAuthoritativePreviousRoundLosses(
        int level, int losses, int expected)
    {
        for (int i = 0; i < losses; i++)
            roundState.RecordOwnershipChange(1, "lost-" + i, "p1", "p3");
        roundState.BeginRound(2); source.CurrentRound = 2; runtime.AdvanceCooldownsForRound(2);
        SetLevel("p1", WDSupportWeaponIds.ReserveTroops, level);
        AssertExecuted(Action(WDSupportWeaponIds.ReserveTroops, "", round: 2));
        Assert.That(roundState.GetReinforcementPool("p1"), Is.EqualTo(expected));
    }

    [Test]
    public void DroneReconCommitsOnceAndAttackUsesDiscoveryWithoutSecondCost()
    {
        SetLevel("p1", WDSpecialWeaponIds.Drone, 1);
        WDWeaponAction recon = Action(WDSpecialWeaponIds.Drone, "",
            parameters: new[] { new WDWeaponActionParameter("mode", "recon"),
                new WDWeaponActionParameter("options", "enemy") });
        Assert.That(coordinator.Execute(recon).Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.ReconCompleted));
        int energy = runtime.FindPlayer("p1").Energy;
        WDWeaponAction attack = Action(WDSpecialWeaponIds.Drone, "enemy",
            parameters: new[] { new WDWeaponActionParameter("mode", "attack"),
                new WDWeaponActionParameter("recon", recon.ActionId) });
        AssertExecuted(attack);
        Assert.That(context.FindTerritory("enemy").Troops, Is.EqualTo(2));
        Assert.That(runtime.FindPlayer("p1").Energy, Is.EqualTo(energy));
    }

    // ============================================================
    // 07. ÚLTIMO LOTE: SETE ARMAS CANÔNICAS SEM DEFERRED
    // ============================================================

    [Test]
    public void PeaceIsMutualAndReciprocalActionHasNoCost()
    {
        AssertExecuted(Action(WDStrategicWeaponIds.Peace, "", targetPlayer: "p3"));
        Assert.That(roundState.HasPeace("p3", "p1"), Is.True);
        WDWeaponAction reciprocal=Action(WDStrategicWeaponIds.Peace,"","p3",targetPlayer:"p1");
        Assert.That(coordinator.Execute(reciprocal).Status,Is.EqualTo(WDIntegratedWeaponExecutionStatus.Superseded));
        AssertRuntime("p3",WDStrategicWeaponIds.Peace,200,0,0);
    }

    [Test]
    public void PeaceResolvedFirstBlocksSabotageWithoutCost()
    {
        var peace=Action(WDStrategicWeaponIds.Peace,"",targetPlayer:"p3");
        var sabotage=Action(WDStrategicWeaponIds.Sabotage,"",targetPlayer:"p3");
        var results=coordinator.ExecuteFrozenActions(new[]{sabotage,peace});
        Assert.That(results.Single(x=>x.ActionId==sabotage.ActionId).Status,Is.EqualTo(WDIntegratedWeaponExecutionStatus.Invalidated));
        AssertRuntime("p1",WDStrategicWeaponIds.Sabotage,185,0,0);
    }

    [TestCase(1,2)] [TestCase(2,3)] [TestCase(3,4)]
    [TestCase(4,5)] [TestCase(5,6)] [TestCase(6,7)]
    public void MinefieldInstallsCanonicalPersistentDamage(int level,int damageValue)
    {
        SetLevel("p1",WDSupportWeaponIds.Minefield,level);
        AssertExecuted(Action(WDSupportWeaponIds.Minefield,"owned",installationId:"mine"));
        Assert.That(registry.Find("mine").ChargesRemaining,Is.EqualTo(1));
        Assert.That(WDTacticalWeaponTables.MineDamage(registry.Find("mine").Level),Is.EqualTo(damageValue));
    }

    [Test]
    public void TerrestrialEntryIntoMinedEmptyTerritoryAppliesDamageBeforeOccupation()
    {
        RunEmptyTerritoryEntryScenario(
            originTroops: 6, incoming: 4, mineDamage: 2, mineEnabled: true,
            expectedOriginTroops: 2, expectedTargetTroops: 2,
            expectedOccupied: true, expectedMineConsumed: true);
    }

    [Test]
    public void TerrestrialEntryIntoMinedEmptyTerritoryKeepsItEmptyWhenAllEntrantsDie()
    {
        RunEmptyTerritoryEntryScenario(
            originTroops: 3, incoming: 2, mineDamage: 2, mineEnabled: true,
            expectedOriginTroops: 1, expectedTargetTroops: 0,
            expectedOccupied: false, expectedMineConsumed: true);
    }

    [Test]
    public void TerrestrialEntryIgnoresTemporarilyDisabledMineAndOccupiesEmptyTerritory()
    {
        RunEmptyTerritoryEntryScenario(
            originTroops: 3, incoming: 2, mineDamage: 2, mineEnabled: false,
            expectedOriginTroops: 1, expectedTargetTroops: 2,
            expectedOccupied: true, expectedMineConsumed: false);
    }

    [Test]
    public void AirTransportMovesTroopsAndFlakWithoutResettingDuration()
    {
        mutations.Install("flak",new WDWeaponInstallationState("flak","flak_tower","owned","p1",2,0,0,0,3,true,1));
        var action=Action(WDSupportWeaponIds.AirTransport,"ally",origin:"owned",installationId:"flak",
            parameters:new[]{new WDWeaponActionParameter("troops","3")});
        AssertExecuted(action);
        Assert.That(context.FindTerritory("owned").Troops,Is.EqualTo(2));
        Assert.That(context.FindTerritory("ally").Troops,Is.EqualTo(7));
        Assert.That(registry.Find("flak").TerritoryId,Is.EqualTo("ally"));
        Assert.That(registry.Find("flak").DurationRemaining,Is.EqualTo(2));
    }

    [TestCase(1,2)] [TestCase(2,3)] [TestCase(3,4)] [TestCase(4,5)]
    public void ParatroopersOccupyEmptyAtAnyDistance(int level,int troops)
    {
        mutations.MakeEmpty("empty-enemy","enemy"); SetLevel("p1",WDTacticalWeaponIds.Paratroopers,level);
        AssertExecuted(Action(WDTacticalWeaponIds.Paratroopers,"enemy"));
        Assert.That(context.FindTerritory("enemy").OwnerPlayerId,Is.EqualTo("p1"));
        Assert.That(context.FindTerritory("enemy").Troops,Is.EqualTo(troops));
    }

    [Test]
    public void InfiltratorDisablesOriginBunkerAndMineOnlyForRound()
    {
        mutations.Install("b",new WDWeaponInstallationState("b","bunker","enemy","p3",0,4,0,0));
        mutations.Install("m",new WDWeaponInstallationState("m","minefield","enemy","p3",0,0,1,0));
        AssertExecuted(Action(WDSpecialWeaponIds.Infiltrator,"enemy"));
        Assert.That(roundState.IsOriginBlocked("enemy"),Is.True);
        Assert.That(registry.Find("b").IsTemporarilyDisabled,Is.True);
        coordinator.ProcessRoundStart(2);
        Assert.That(registry.Find("b").IsTemporarilyDisabled,Is.False);
        Assert.That(registry.Find("b").BarrierRemaining,Is.EqualTo(4));
    }

    [TestCase(1,4)] [TestCase(2,5)] [TestCase(3,6)]
    [TestCase(4,7)] [TestCase(5,8)] [TestCase(6,9)]
    public void TroopRecoveryUsesAuthoritativePreviousRoundCasualties(int level,int limit)
    {
        roundState.CaptureTerritorySnapshot(context.Territories);
        source.TryApplyTerritory("owned","p1",0,WDWeaponTerritoryStateKind.Empty);
        source.TryApplyTerritory("owned","p1",1,WDWeaponTerritoryStateKind.Occupied);
        roundState.ObserveTerritoryHistory(1,context.Territories);
        roundState.BeginRound(2);source.CurrentRound=2;runtime.AdvanceCooldownsForRound(2);
        SetLevel("p1",WDSupportWeaponIds.TroopRecovery,level);
        AssertExecuted(Action(WDSupportWeaponIds.TroopRecovery,"owned",round:2));
        Assert.That(context.FindTerritory("owned").Troops,Is.EqualTo(1+Math.Min(4,limit)));
    }

    [TestCase(1,1,1,1)] [TestCase(2,2,1,1)] [TestCase(3,2,2,1)]
    [TestCase(4,2,2,2)] [TestCase(5,3,2,2)] [TestCase(6,3,3,2)]
    public void MercenariesPayThreeCanonicalIdempotentInstallments(int level,int first,int second,int third)
    {
        SetLevel("p1",WDSupportWeaponIds.Mercenaries,level);
        AssertExecuted(Action(WDSupportWeaponIds.Mercenaries,""));
        Assert.That(roundState.TakeReinforcementPool("p1"),Is.EqualTo(first));
        coordinator.ProcessRoundStart(2);Assert.That(roundState.TakeReinforcementPool("p1"),Is.EqualTo(second));
        Assert.That(roundState.PayMercenaries(2),Is.Zero);
        coordinator.ProcessRoundStart(3);Assert.That(roundState.TakeReinforcementPool("p1"),Is.EqualTo(third));
    }

    // ============================================================
    // 09. GLOBAL RESOLUTION V2: ORDEM, ESTADO VIVO E IDEMPOTÊNCIA
    // ============================================================

    [Test]
    public void GlobalResolutionRunsEveryCanonicalPhaseAndSystemHookInOrder()
    {
        var pipeline = new WDWeaponResolutionPipeline();
        int transfers = 0, terrestrial = 0, consolidation = 0;
        WDGlobalResolutionResult result = pipeline.ResolveRound(
            new WDGlobalResolutionRequest(
                "resolution:order", 1,
                new[]
                {
                    Action(WDSupportWeaponIds.Reinforcements, "owned"),
                    Action(WDStrategicWeaponIds.Peace, "", targetPlayer: "p3"),
                    Action(WDTacticalWeaponIds.Blitzkrieg, "")
                },
                coordinator,
                resolveTransfers: () => ++transfers,
                resolveTerrestrial: () => ++terrestrial,
                consolidate: () => consolidation++));

        WDWeaponResolutionPhase[] starts = result.Entries
            .Where(item => item.Kind == WDGlobalResolutionEntryKind.PhaseStarted)
            .Select(item => item.Phase).ToArray();
        Assert.That(starts, Is.EqualTo(pipeline.Phases));
        Assert.That(transfers, Is.EqualTo(1));
        Assert.That(terrestrial, Is.EqualTo(1));
        Assert.That(consolidation, Is.EqualTo(1));
        Assert.That(result.Entries.Where(item =>
            item.Kind == WDGlobalResolutionEntryKind.WeaponAction)
            .Select(item => item.Phase), Is.Ordered);
    }

    [Test]
    public void EqualSabotageUsesOfficialPlayerOrderWithoutDependingOnInputOrder()
    {
        SetLevel("p1", WDStrategicWeaponIds.Sabotage, 1);
        SetLevel("p2", WDStrategicWeaponIds.Sabotage, 1);
        WDWeaponAction firstOfficial = Action(
            WDStrategicWeaponIds.Sabotage, "", "p1", targetPlayer: "p3");
        WDWeaponAction secondOfficial = Action(
            WDStrategicWeaponIds.Sabotage, "", "p2", targetPlayer: "p3");

        IReadOnlyList<WDIntegratedWeaponExecutionResult> results =
            coordinator.ExecuteFrozenActions(new[] { secondOfficial, firstOfficial });

        Assert.That(results.Single(item => item.ActionId == firstOfficial.ActionId).Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.Executed));
        Assert.That(results.Single(item => item.ActionId == secondOfficial.ActionId).Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.Superseded));
    }

    [Test]
    public void GlobalResolutionIdPreventsEveryMutationAndHookFromRunningTwice()
    {
        var pipeline = new WDWeaponResolutionPipeline();
        int transferCalls = 0;
        var request = new WDGlobalResolutionRequest(
            "resolution:idempotent", 1,
            new[] { Action(WDSupportWeaponIds.Reinforcements, "owned") },
            coordinator, resolveTransfers: () => ++transferCalls);

        WDGlobalResolutionResult first = pipeline.ResolveRound(request);
        WDGlobalResolutionResult second = pipeline.ResolveRound(request);

        Assert.That(second, Is.SameAs(first));
        Assert.That(context.FindTerritory("owned").Troops, Is.EqualTo(13));
        Assert.That(transferCalls, Is.EqualTo(1));
    }

    [Test]
    public void GlobalResolutionSniperChangesLiveStateBeforePropagandaRevalidation()
    {
        SetLevel("p1", WDSpecialWeaponIds.WarPropaganda, 1);
        var pipeline = new WDWeaponResolutionPipeline();
        pipeline.ResolveRound(new WDGlobalResolutionRequest(
            "resolution:sniper-propaganda", 1,
            new[]
            {
                Action(WDSpecialWeaponIds.WarPropaganda, "enemy"),
                Action(WDTacticalWeaponIds.Sniper, "enemy")
            }, coordinator));

        Assert.That(context.FindTerritory("enemy").State,
            Is.EqualTo(WDWeaponTerritoryStateKind.Neutral));
        Assert.That(context.FindTerritory("enemy").Troops, Is.EqualTo(1));
    }

    [Test]
    public void GlobalResolutionParatroopersMutateLiveStateBeforeTerrestrialPhase()
    {
        mutations.MakeEmpty("global-empty", "enemy2");
        bool terrestrialObservedOccupation = false;
        var pipeline = new WDWeaponResolutionPipeline();
        pipeline.ResolveRound(new WDGlobalResolutionRequest(
            "resolution:paratroopers-terrestrial", 1,
            new[] { Action(WDTacticalWeaponIds.Paratroopers, "enemy2") },
            coordinator,
            resolveTerrestrial: () =>
            {
                WDGameWeaponTerritoryView current = context.FindTerritory("enemy2");
                terrestrialObservedOccupation = current.OwnerPlayerId == "p1" &&
                    current.State == WDWeaponTerritoryStateKind.Occupied;
                return terrestrialObservedOccupation ? 1 : 0;
            }));

        Assert.That(terrestrialObservedOccupation, Is.True);
    }

    [Test]
    public void GlobalResolutionNextRoundExpiresTemporaryPeaceAndKeepsTechnicalDeferred()
    {
        var pipeline = new WDWeaponResolutionPipeline();
        pipeline.ResolveRound(new WDGlobalResolutionRequest(
            "resolution:round-1", 1,
            new[] { Action(WDStrategicWeaponIds.Peace, "", targetPlayer: "p3") },
            coordinator));
        Assert.That(roundState.HasPeace("p1", "p3"), Is.True);

        source.CurrentRound = 2;
        runtime.AdvanceCooldownsForRound(2);
        WDWeaponAction unknown = new(
            "unknown-action", "p1", "future_weapon", 2,
            WDWeaponResolutionPhase.ProgrammedOffense, 0);
        WDGlobalResolutionResult next = pipeline.ResolveRound(
            new WDGlobalResolutionRequest(
                "resolution:round-2", 2, new[] { unknown }, coordinator));

        Assert.That(roundState.HasPeace("p1", "p3"), Is.False);
        Assert.That(next.Entries.Single(item => item.StableId == "unknown-action")
            .WeaponStatus, Is.EqualTo(WDIntegratedWeaponExecutionStatus.Deferred));
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
        WDWeaponResolutionPhase? phase = null, string targetPlayer = "",
        IEnumerable<WDWeaponActionParameter> parameters = null) =>
        new($"test-action-{actionSequence++}", author, weaponId,
            round ?? source.CurrentRound, phase ?? catalog.FindWeapon(weaponId).ResolutionPhase,
            actionSequence, originTerritoryId: origin, targetTerritoryId: target,
            targetPlayerId: targetPlayer, installationId: installationId,
            parameters: parameters);

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

    // ============================================================
    // 08. FIXTURE DA PORTA DE ENTRADA HOSTIL EM TERRITÓRIO VAZIO
    // ============================================================

    private static void RunEmptyTerritoryEntryScenario(
        int originTroops, int incoming, int mineDamage, bool mineEnabled,
        int expectedOriginTroops, int expectedTargetTroops,
        bool expectedOccupied, bool expectedMineConsumed)
    {
        GameObject originObject = new("Origin");
        GameObject targetObject = new("EmptyTarget");
        GameObject resolverObject = new("TerrestrialResolver");
        try
        {
            TerritorioClique origin = CreateTerritory(
                originObject, "origin", TerritorioClique.Dono.Jogador1,
                originTroops, TerritorioClique.EstadoTerritorial.Ocupado);
            TerritorioClique target = CreateTerritory(
                targetObject, "empty", TerritorioClique.Dono.Neutro,
                0, TerritorioClique.EstadoTerritorial.Vazio);
            originObject.GetComponent<TerritorioFronteiras>()
                .Configurar(new[] { target });

            bool mineConsumed = false;
            var resolver = resolverObject.AddComponent<ResolvedorAcoesTerrestres>();
            resolver.Inicializar(null, (order, amount) =>
            {
                if (!mineEnabled)
                    return amount;

                int losses = Math.Min(amount, mineDamage);
                Assert.That(order.Origem.RemoverTropas(losses), Is.True);
                mineConsumed = true;
                return amount - losses;
            });

            ResultadoAcaoTerrestre result = resolver.ResolverOrdem(
                new OrdemTerrestre(origin, target, incoming,
                    TerritorioClique.Dono.Jogador1, 0));

            Assert.That(result.Executada, Is.EqualTo(expectedOccupied));
            Assert.That(origin.Tropas, Is.EqualTo(expectedOriginTroops));
            Assert.That(target.Tropas, Is.EqualTo(expectedTargetTroops));
            Assert.That(target.IsOccupied, Is.EqualTo(expectedOccupied));
            Assert.That(target.IsEmpty, Is.EqualTo(!expectedOccupied));
            Assert.That(mineConsumed, Is.EqualTo(expectedMineConsumed));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(resolverObject);
            UnityEngine.Object.DestroyImmediate(targetObject);
            UnityEngine.Object.DestroyImmediate(originObject);
        }
    }

    private static TerritorioClique CreateTerritory(
        GameObject gameObject, string id, TerritorioClique.Dono owner,
        int troops, TerritorioClique.EstadoTerritorial state)
    {
        gameObject.AddComponent<TerritorioTropas>();
        gameObject.AddComponent<TerritorioFronteiras>();
        TerritorioClique territory = gameObject.AddComponent<TerritorioClique>();
        territory.idTerritorio = id;
        Assert.That(territory.TentarAplicarEstadoAutoritativo(owner, troops, state), Is.True);
        return territory;
    }

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
