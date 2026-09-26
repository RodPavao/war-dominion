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
    public void RoundStartPaymentImmediatelyChangesCanonicalCardAvailability()
    {
        SetLevel("p1", WDStrategicWeaponIds.EnergyPlant, 1);
        Assert.That(runtime.TrySpendEnergy("p1", 200), Is.True);
        AssertExecuted(Action(WDStrategicWeaponIds.EnergyPlant, "owned",
            installationId: "central-hud"));
        Assert.That(runtime.GetAvailability("p1", WDSupportWeaponIds.FlakTower).Reason,
            Is.EqualTo(WDWeaponAvailabilityReason.InsufficientEnergy));

        source.CurrentRound = 2;
        runtime.AdvanceCooldownsForRound(2);
        Assert.That(coordinator.ProcessRoundStart(2), Is.EqualTo(1));

        Assert.That(runtime.FindPlayer("p1").Energy, Is.EqualTo(5));
        Assert.That(runtime.GetAvailability("p1", WDSupportWeaponIds.FlakTower)
            .IsAvailable, Is.True);
    }

    [Test]
    public void SuccessfulTerrestrialAttackEnergyIsDeferredAndIdempotent()
    {
        ResultadoAcaoTerrestre success = TerrestrialResult(
            ResultadoAcaoTerrestre.TipoResultado.Ataque, true);

        Assert.That(roundState.ScheduleEnergyFromTerrestrialResults(
            "resolution-1", 1, new[] { success }, OwnerPlayerId), Is.EqualTo(1));
        Assert.That(roundState.ScheduleEnergyFromTerrestrialResults(
            "resolution-1", 1, new[] { success }, OwnerPlayerId), Is.Zero);
        Assert.That(runtime.FindPlayer("p1").Energy, Is.EqualTo(200));
        Assert.That(coordinator.ProcessPendingAttackEnergy(1), Is.Zero);
        Assert.That(runtime.FindPlayer("p1").Energy, Is.EqualTo(200));

        Assert.That(coordinator.ProcessPendingAttackEnergy(2), Is.EqualTo(1));
        Assert.That(runtime.FindPlayer("p1").Energy, Is.EqualTo(205));
        Assert.That(coordinator.ProcessPendingAttackEnergy(2), Is.Zero);
        Assert.That(runtime.FindPlayer("p1").Energy, Is.EqualTo(205));
    }

    [Test]
    public void OnlySuccessfulAttacksAccumulateFiveEnergyEach()
    {
        ResultadoAcaoTerrestre successA = TerrestrialResult(
            ResultadoAcaoTerrestre.TipoResultado.Ataque, true, 1);
        ResultadoAcaoTerrestre successB = TerrestrialResult(
            ResultadoAcaoTerrestre.TipoResultado.Ataque, true, 2);
        ResultadoAcaoTerrestre failed = TerrestrialResult(
            ResultadoAcaoTerrestre.TipoResultado.Ataque, false, 3);
        ResultadoAcaoTerrestre movement = TerrestrialResult(
            ResultadoAcaoTerrestre.TipoResultado.OcupacaoVazio, true, 4);

        Assert.That(roundState.ScheduleEnergyFromTerrestrialResults(
            "resolution-2", 1, new[] { successA, successB, failed, movement },
            OwnerPlayerId), Is.EqualTo(2));
        Assert.That(coordinator.ProcessPendingAttackEnergy(2), Is.EqualTo(2));
        Assert.That(runtime.FindPlayer("p1").Energy, Is.EqualTo(210));
    }

    [Test]
    public void AttackEnergyAndEnergyPlantPaymentSumAtNextPreparation()
    {
        SetLevel("p1", WDStrategicWeaponIds.EnergyPlant, 1);
        AssertExecuted(Action(WDStrategicWeaponIds.EnergyPlant, "owned",
            installationId: "central-sum"));
        Assert.That(roundState.ScheduleEnergyFromTerrestrialResults(
            "resolution-sum", 1,
            new[] { TerrestrialResult(ResultadoAcaoTerrestre.TipoResultado.Ataque, true) },
            OwnerPlayerId), Is.EqualTo(1));

        source.CurrentRound = 2;
        runtime.AdvanceCooldownsForRound(2);
        Assert.That(coordinator.ProcessRoundStart(2), Is.EqualTo(2));
        Assert.That(runtime.FindPlayer("p1").Energy, Is.EqualTo(210));
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
    public void DroneInstallationAndSameRoundShotUseCanonicalPersistentState()
    {
        SetLevel("p1", WDSpecialWeaponIds.Drone, 1);
        WDWeaponAction install = Action(WDSpecialWeaponIds.Drone, "owned",
            installationId: "drone-live",
            parameters: new[] { new WDWeaponActionParameter("mode", "install") });
        WDWeaponAction shot = Action(WDSpecialWeaponIds.Drone, "enemy",
            installationId: "drone-live", phase: WDWeaponResolutionPhase.ProgrammedOffense,
            parameters: new[] { new WDWeaponActionParameter("mode", "shot") });

        WDIntegratedWeaponExecutionResult[] results = coordinator
            .ExecuteFrozenActions(new[] { shot, install }).ToArray();

        Assert.That(results.Single(x => x.ActionId == install.ActionId).Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.Executed));
        Assert.That(results.Single(x => x.ActionId == shot.ActionId).Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.Launched));
        Assert.That(context.FindTerritory("enemy").Troops, Is.EqualTo(3));
        Assert.That(registry.Find("drone-live").ChargesRemaining, Is.EqualTo(1));
        AssertRuntime("p1", WDSpecialWeaponIds.Drone, 170, 3, 1);

        WDWeaponAction repeated = Action(WDSpecialWeaponIds.Drone, "enemy",
            installationId: "drone-live", phase: WDWeaponResolutionPhase.ProgrammedOffense,
            parameters: new[] { new WDWeaponActionParameter("mode", "shot") });
        Assert.That(coordinator.Execute(repeated).Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.Superseded));
        Assert.That(registry.Find("drone-live").ChargesRemaining, Is.EqualTo(1));

        source.CurrentRound = 2;
        roundState.BeginRound(2);
        WDWeaponAction second = Action(WDSpecialWeaponIds.Drone, "enemy",
            installationId: "drone-live", round: 2,
            phase: WDWeaponResolutionPhase.ProgrammedOffense,
            parameters: new[] { new WDWeaponActionParameter("mode", "shot") });
        Assert.That(coordinator.Execute(second).Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.Launched));
        Assert.That(registry.Find("drone-live"), Is.Null);
    }

    [Test]
    public void InfiltratorBlocksExactProgrammedActionBeforePeaceWithoutChargingTarget()
    {
        EnterRoundThree();
        WDWeaponAction peace = Action(WDStrategicWeaponIds.Peace, "", "p3",
            targetPlayer: "p1");
        WDWeaponAction infiltrator = Action(WDSpecialWeaponIds.Infiltrator, "",
            targetPlayer: "p3", phase: WDWeaponResolutionPhase.Infiltration,
            parameters: new[] { new WDWeaponActionParameter("targetWeaponId", peace.WeaponId) });

        WDIntegratedWeaponExecutionResult[] results = coordinator
            .ExecuteFrozenActions(new[] { peace, infiltrator }).ToArray();

        Assert.That(results.Single(x => x.ActionId == infiltrator.ActionId).Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.Executed));
        Assert.That(results.Single(x => x.ActionId == peace.ActionId).Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.BlockedByInfiltrator));
        AssertRuntime("p3", WDStrategicWeaponIds.Peace, 200, 3, 0);
    }

    [Test]
    public void RadarPreparationUseIsPrivateIdempotentAndRemovedAfterReveal()
    {
        var radar = new WDWeaponInstallationState("radar-live", WDTacticalWeaponIds.Radar,
            "owned", "p1", 0, 0, 1, 0, 1, true, 0);
        Assert.That(mutations.Install("install-radar-fixture", radar).Succeeded, Is.True);

        WDRadarUseResult first = coordinator.UseRadarDuringPreparation(
            "scan-1", "p1", radar.InstallationId, "enemy", Array.Empty<WDWeaponAction>());
        WDRadarUseResult duplicate = coordinator.UseRadarDuringPreparation(
            "scan-1", "p1", radar.InstallationId, "enemy", Array.Empty<WDWeaponAction>());

        Assert.That(first.Status, Is.EqualTo(WDRadarUseStatus.Used));
        Assert.That(first.Reveal.CanBeReadBy("p1"), Is.True);
        Assert.That(first.Reveal.CanBeReadBy("p2"), Is.False);
        Assert.That(duplicate.Status, Is.EqualTo(WDRadarUseStatus.AlreadyProcessed));
        Assert.That(registry.Find(radar.InstallationId), Is.Null);

        source.CurrentRound = 2;
        roundState.BeginRound(2);
        Assert.That(roundState.GetRadarResult("scan-1", "p1"), Is.Null);
    }

    [Test]
    public void RadarInstalledThisRoundScansRegardlessOfProgrammedSniper()
    {
        var newRadar = new WDWeaponInstallationState("radar-new", WDTacticalWeaponIds.Radar,
            "owned", "p1", 0, 0, 1, 0, 1, true, 1);
        Assert.That(mutations.Install("install-radar-new", newRadar).Succeeded, Is.True);
        Assert.That(coordinator.UseRadarDuringPreparation("scan-new", "p1", "radar-new",
            "enemy", Array.Empty<WDWeaponAction>()).Status,
            Is.EqualTo(WDRadarUseStatus.Used));

        mutations.RemoveInstallation("remove-new", "radar-new");
        var oldRadar = new WDWeaponInstallationState("radar-old", WDTacticalWeaponIds.Radar,
            "owned", "p1", 0, 0, 1, 0, 1, true, 0);
        Assert.That(mutations.Install("install-radar-old", oldRadar).Succeeded, Is.True);
        WDWeaponAction sniper = Action(WDTacticalWeaponIds.Sniper, "owned", "p3");
        WDRadarUseResult result = coordinator.UseRadarDuringPreparation(
            "scan-with-sniper", "p1", "radar-old", "enemy", new[] { sniper });
        Assert.That(result.Status, Is.EqualTo(WDRadarUseStatus.Used));
        Assert.That(result.Reveal.CanBeReadBy("p1"), Is.True);
        Assert.That(oldRadar.ChargesRemaining, Is.Zero);
        Assert.That(registry.Find("radar-old"), Is.Null);
    }

    [Test]
    public void PeaceBlocksSniperWithoutDamageCostOrResidualOriginBlock()
    {
        WDWeaponAction peace = Action(WDStrategicWeaponIds.Peace, "",
            targetPlayer: "p3");
        WDWeaponAction sniper = Action(WDTacticalWeaponIds.Sniper, "enemy");
        var pipeline = new WDWeaponResolutionPipeline();

        WDGlobalResolutionResult result = pipeline.ResolveRound(
            new WDGlobalResolutionRequest("peace-sniper", 1,
                new[] { sniper, peace }, coordinator));

        Assert.That(result.Entries.Single(entry => entry.StableId == sniper.ActionId)
            .WeaponStatus, Is.EqualTo(WDIntegratedWeaponExecutionStatus.Invalidated));
        Assert.That(context.FindTerritory("enemy").Troops, Is.EqualTo(4));
        Assert.That(roundState.IsOriginBlocked("enemy"), Is.False);
        AssertRuntime("p1", WDTacticalWeaponIds.Sniper, 185, 0, 0);
    }

    [Test]
    public void InfiltratorBlockedSniperProducesNoDamageOrResidualOriginBlock()
    {
        EnterRoundThree();
        WDWeaponAction sniper = Action(WDTacticalWeaponIds.Sniper, "owned", "p3");
        WDWeaponAction infiltrator = InfiltratorAction(
            "infiltrator-blocks-sniper", "p1", "p3",
            WDTacticalWeaponIds.Sniper, 1);
        var pipeline = new WDWeaponResolutionPipeline();

        WDGlobalResolutionResult result = pipeline.ResolveRound(
            new WDGlobalResolutionRequest("infiltrator-sniper", 3,
                new[] { sniper, infiltrator }, coordinator));

        Assert.That(result.Entries.Single(entry => entry.StableId == sniper.ActionId)
            .WeaponStatus,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.BlockedByInfiltrator));
        Assert.That(context.FindTerritory("owned").Troops, Is.EqualTo(5));
        Assert.That(roundState.IsOriginBlocked("owned"), Is.False);
        AssertRuntime("p3", WDTacticalWeaponIds.Sniper, 200, 3, 0);
    }

    [Test]
    public void RadarReportsFlakCoverageWithoutExposingItToAlly()
    {
        var flak = new WDWeaponInstallationState("enemy-flak", WDSupportWeaponIds.FlakTower,
            "enemy", "p3", 3, 0, 0, 0, 4, true, 0);
        var radar = new WDWeaponInstallationState("radar-cover", WDTacticalWeaponIds.Radar,
            "owned", "p1", 0, 0, 1, 0, 1, true, 0);
        Assert.That(mutations.Install("install-enemy-flak", flak).Succeeded, Is.True);
        Assert.That(mutations.Install("install-radar-cover", radar).Succeeded, Is.True);

        WDRadarUseResult result = coordinator.UseRadarDuringPreparation("scan-cover",
            "p1", radar.InstallationId, "enemy", Array.Empty<WDWeaponAction>());

        Assert.That(result.Status, Is.EqualTo(WDRadarUseStatus.Used));
        Assert.That(result.Reveal.HasFlakCoverage, Is.True);
        Assert.That(roundState.GetRadarResult("scan-cover", "p2"), Is.Null);
    }

    [Test]
    public void InfiltratorMissingTargetIsInvalidAndDuplicateTargetIsSuperseded()
    {
        EnterRoundThree();
        WDWeaponAction peace = Action(WDStrategicWeaponIds.Peace, "", "p3",
            targetPlayer: "p1");
        WDWeaponAction first = Action(WDSpecialWeaponIds.Infiltrator, "", "p1",
            targetPlayer: "p3", phase: WDWeaponResolutionPhase.Infiltration,
            parameters: new[] { new WDWeaponActionParameter("targetWeaponId", peace.WeaponId) });
        WDWeaponAction second = Action(WDSpecialWeaponIds.Infiltrator, "", "p2",
            targetPlayer: "p3", phase: WDWeaponResolutionPhase.Infiltration,
            parameters: new[] { new WDWeaponActionParameter("targetWeaponId", peace.WeaponId) });
        WDWeaponAction missing = Action(WDSpecialWeaponIds.Infiltrator, "", "p2",
            targetPlayer: "p3", phase: WDWeaponResolutionPhase.Infiltration,
            parameters: new[] { new WDWeaponActionParameter("targetWeaponId", "unknown") });

        WDIntegratedWeaponExecutionResult[] results = coordinator
            .ExecuteFrozenActions(new[] { second, peace, first, missing }).ToArray();

        Assert.That(results.Single(x => x.ActionId == first.ActionId).Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.Executed));
        Assert.That(results.Single(x => x.ActionId == second.ActionId).Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.Superseded));
        Assert.That(results.Single(x => x.ActionId == missing.ActionId).Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.Invalidated));
        AssertRuntime("p1", WDSpecialWeaponIds.Infiltrator, 180, 4, 1);
        AssertRuntime("p2", WDSpecialWeaponIds.Infiltrator, 200, 0, 0);
        AssertRuntime("p3", WDStrategicWeaponIds.Peace, 200, 3, 0);
    }

    [Test]
    public void InfiltratorDuplicateTargetUsesProgramOrderBeforeActionId()
    {
        EnterRoundThree();
        WDWeaponAction peace = Action(WDStrategicWeaponIds.Peace, "", "p3",
            targetPlayer: "p1");
        WDWeaponAction earlier = InfiltratorAction(
            "infiltrator-z", "p1", "p3", peace.WeaponId, 10);
        WDWeaponAction later = InfiltratorAction(
            "infiltrator-a", "p1", "p3", peace.WeaponId, 20);

        WDIntegratedWeaponExecutionResult[] results = coordinator
            .ExecuteFrozenActions(new[] { later, peace, earlier }).ToArray();

        Assert.That(results.Single(x => x.ActionId == earlier.ActionId).Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.Executed));
        Assert.That(results.Single(x => x.ActionId == later.ActionId).Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.Superseded));
        AssertRuntime("p1", WDSpecialWeaponIds.Infiltrator, 180, 4, 1);
    }

    [Test]
    public void InfiltratorDuplicateTargetUsesActionIdAsFinalTieBreaker()
    {
        EnterRoundThree();
        WDWeaponAction peace = Action(WDStrategicWeaponIds.Peace, "", "p3",
            targetPlayer: "p1");
        WDWeaponAction firstId = InfiltratorAction(
            "infiltrator-a", "p1", "p3", peace.WeaponId, 10);
        WDWeaponAction lastId = InfiltratorAction(
            "infiltrator-z", "p1", "p3", peace.WeaponId, 10);

        WDIntegratedWeaponExecutionResult[] results = coordinator
            .ExecuteFrozenActions(new[] { lastId, peace, firstId }).ToArray();

        Assert.That(results.Single(x => x.ActionId == firstId.ActionId).Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.Executed));
        Assert.That(results.Single(x => x.ActionId == lastId.ActionId).Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.Superseded));
        AssertRuntime("p1", WDSpecialWeaponIds.Infiltrator, 180, 4, 1);
    }

    [Test]
    public void InfiltratorForcesCooldownOnWeaponThatWasNotProgrammed()
    {
        EnterRoundThree();
        WDWeaponAction infiltrator = InfiltratorAction(
            "infiltrator-unprogrammed", "p1", "p3",
            WDSupportWeaponIds.AirStrike, 10);

        WDIntegratedWeaponExecutionResult result = coordinator.ExecuteFrozenActions(
            new[] { infiltrator }).Single();

        Assert.That(result.Status, Is.EqualTo(WDIntegratedWeaponExecutionStatus.Executed));
        AssertRuntime("p1", WDSpecialWeaponIds.Infiltrator, 180, 4, 1);
        AssertRuntime("p3", WDSupportWeaponIds.AirStrike, 200, 3, 0);
    }

    [Test]
    public void MutualInfiltratorsUseOfficialPlayerOrderWithoutRetroactivity()
    {
        EnterRoundThree();
        WDWeaponAction first = InfiltratorAction(
            "infiltrator-p1", "p1", "p3", WDSpecialWeaponIds.Infiltrator, 10);
        WDWeaponAction second = InfiltratorAction(
            "infiltrator-p3", "p3", "p1", WDSpecialWeaponIds.Infiltrator, 10);

        WDIntegratedWeaponExecutionResult[] results = coordinator
            .ExecuteFrozenActions(new[] { second, first }).ToArray();

        Assert.That(results.Single(x => x.ActionId == first.ActionId).Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.Executed));
        Assert.That(results.Single(x => x.ActionId == second.ActionId).Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.BlockedByInfiltrator));
        AssertRuntime("p1", WDSpecialWeaponIds.Infiltrator, 180, 4, 1);
        AssertRuntime("p3", WDSpecialWeaponIds.Infiltrator, 200, 3, 0);
    }

    [Test]
    public void InfiltratorDoesNotBlockPersistentDroneOperation()
    {
        EnterRoundThree();
        WDWeaponAction shot = Action(WDSpecialWeaponIds.Drone, "enemy", "p3",
            installationId: "enemy-drone",
            phase: WDWeaponResolutionPhase.ProgrammedOffense,
            parameters: new[] { new WDWeaponActionParameter("mode", "shot") });
        WDWeaponAction infiltrator = Action(WDSpecialWeaponIds.Infiltrator, "", "p1",
            targetPlayer: "p3", phase: WDWeaponResolutionPhase.Infiltration,
            parameters: new[] { new WDWeaponActionParameter("targetWeaponId", shot.WeaponId) });

        WDIntegratedWeaponExecutionResult result = coordinator
            .ExecuteFrozenActions(new[] { shot, infiltrator })
            .Single(item => item.ActionId == infiltrator.ActionId);

        Assert.That(result.Status, Is.EqualTo(WDIntegratedWeaponExecutionStatus.Executed));
    }

    [Test]
    public void InfiltratorBlocksNewInstallationButNotExistingInstallationOperation()
    {
        EnterRoundThree();
        var existing = new WDWeaponInstallationState("existing-rockets",
            WDStrategicWeaponIds.Rockets, "enemy", "p3", 0, 0, 2, 0, 2, true, 1);
        Assert.That(mutations.Install("existing-rockets-fixture", existing).Succeeded,
            Is.True);
        WDWeaponAction install = Action(WDStrategicWeaponIds.Rockets, "enemy", "p3",
            installationId: "new-rockets");
        WDWeaponAction launch = Action(WDStrategicWeaponIds.Rockets, "owned", "p3",
            installationId: existing.InstallationId, origin: "enemy",
            phase: WDWeaponResolutionPhase.ProgrammedOffense);
        WDWeaponAction infiltrator = InfiltratorAction(
            "infiltrator-rockets", "p1", "p3", WDStrategicWeaponIds.Rockets, 1);

        WDIntegratedWeaponExecutionResult[] results = coordinator.ExecuteFrozenActions(
            new[] { launch, install, infiltrator }).ToArray();

        Assert.That(results.Single(item => item.ActionId == install.ActionId).Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.BlockedByInfiltrator));
        Assert.That(results.Single(item => item.ActionId == launch.ActionId).Status,
            Is.Not.EqualTo(WDIntegratedWeaponExecutionStatus.BlockedByInfiltrator));
    }

    [TestCase("owned")]
    [TestCase("ally")]
    public void DronePersistentShotRejectsOwnAndAlliedTerritories(string targetId)
    {
        var drone = new WDWeaponInstallationState("drone-invalid", WDSpecialWeaponIds.Drone,
            "owned", "p1", 0, 0, 2, 0, 1, true, 0);
        Assert.That(mutations.Install("install-drone-invalid", drone).Succeeded, Is.True);
        WDWeaponAction shot = Action(WDSpecialWeaponIds.Drone, targetId,
            installationId: drone.InstallationId,
            phase: WDWeaponResolutionPhase.ProgrammedOffense,
            parameters: new[] { new WDWeaponActionParameter("mode", "shot") });

        Assert.That(coordinator.Execute(shot).Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.Invalidated));
        Assert.That(drone.ChargesRemaining, Is.EqualTo(2));
    }

    [TestCase("neutral", 0, WDWeaponTerritoryStateKind.Empty)]
    [TestCase("empty", 0, WDWeaponTerritoryStateKind.Empty)]
    public void DroneCanLaunchAtNeutralOrEmptyAndConsumesCharge(
        string targetId, int expectedTroops, WDWeaponTerritoryStateKind expectedState)
    {
        var drone = new WDWeaponInstallationState("drone-open", WDSpecialWeaponIds.Drone,
            "owned", "p1", 0, 0, 2, 0, 1, true, 0);
        Assert.That(mutations.Install("install-drone-open", drone).Succeeded, Is.True);
        WDWeaponAction shot = Action(WDSpecialWeaponIds.Drone, targetId,
            installationId: drone.InstallationId,
            phase: WDWeaponResolutionPhase.ProgrammedOffense,
            parameters: new[] { new WDWeaponActionParameter("mode", "shot") });

        Assert.That(coordinator.Execute(shot).Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.Launched));
        Assert.That(context.FindTerritory(targetId).Troops, Is.EqualTo(expectedTroops));
        Assert.That(context.FindTerritory(targetId).State, Is.EqualTo(expectedState));
        Assert.That(drone.ChargesRemaining, Is.EqualTo(1));
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

    [Test]
    public void SniperAndFlakDoNotBlockAirTransport()
    {
        mutations.Install("transport-flak", new WDWeaponInstallationState(
            "transport-flak", WDSupportWeaponIds.FlakTower, "owned", "p1",
            2, 0, 0, 0, 3, true, 1));
        WDWeaponAction transport = Action(WDSupportWeaponIds.AirTransport, "ally",
            origin: "owned", installationId: "transport-flak",
            parameters: new[] { new WDWeaponActionParameter("troops", "2") });
        WDWeaponAction sniper = Action(WDTacticalWeaponIds.Sniper, "owned", "p3");
        var pipeline = new WDWeaponResolutionPipeline();

        WDGlobalResolutionResult result = pipeline.ResolveRound(
            new WDGlobalResolutionRequest("transport-sniper-flak", 1,
                new[] { sniper, transport }, coordinator));

        Assert.That(result.Entries.Single(entry => entry.StableId == transport.ActionId)
            .WeaponStatus, Is.EqualTo(WDIntegratedWeaponExecutionStatus.Executed));
        Assert.That(context.FindTerritory("ally").Troops, Is.EqualTo(6));
        Assert.That(registry.Find("transport-flak").TerritoryId, Is.EqualTo("ally"));
        Assert.That(registry.Find("transport-flak").DurationRemaining, Is.EqualTo(2));
    }

    [TestCase(WDSupportWeaponIds.FlakTower, 2, 0, 0, 0)]
    [TestCase(WDStrategicWeaponIds.EnergyPlant, 0, 0, 0, 2)]
    [TestCase(WDStrategicWeaponIds.Artillery, 0, 0, 5, 0)]
    [TestCase(WDStrategicWeaponIds.Rockets, 0, 0, 2, 0)]
    [TestCase(WDSpecialWeaponIds.Drone, 0, 0, 2, 0)]
    [TestCase(WDTacticalWeaponIds.Radar, 0, 0, 1, 0)]
    public void AirTransportPreservesCanonicalInstallationState(
        string weaponId, int duration, int barrier, int charges, int payments)
    {
        string id = "transport-" + weaponId;
        var installation = new WDWeaponInstallationState(id, weaponId, "owned", "p1",
            duration, barrier, charges, payments, 3, true, 0);
        installation.TryMarkAutomaticRound(0);
        Assert.That(mutations.Install("fixture-" + id, installation).Succeeded, Is.True);

        AssertExecuted(Action(WDSupportWeaponIds.AirTransport, "ally", origin: "owned",
            installationId: id,
            parameters: new[] { new WDWeaponActionParameter("troops", "1") }));

        WDWeaponInstallationState moved = registry.Find(id);
        Assert.That(moved.OwnerPlayerId, Is.EqualTo("p2"));
        Assert.That(moved.TerritoryId, Is.EqualTo("ally"));
        Assert.That(moved.Level, Is.EqualTo(3));
        Assert.That(moved.DurationRemaining, Is.EqualTo(duration));
        Assert.That(moved.BarrierRemaining, Is.EqualTo(barrier));
        Assert.That(moved.ChargesRemaining, Is.EqualTo(charges));
        Assert.That(moved.PaymentsRemaining, Is.EqualTo(payments));
        Assert.That(moved.LastAutomaticRound, Is.EqualTo(0));
    }

    [Test]
    public void TransportedEnergyPlantPaysNewOwnerOnceWithoutRestartingSchedule()
    {
        var plant = new WDWeaponInstallationState("moving-plant",
            WDStrategicWeaponIds.EnergyPlant, "owned", "p1", 0, 0, 0, 3,
            1, true, 0);
        Assert.That(mutations.Install("moving-plant-fixture", plant).Succeeded, Is.True);
        AssertExecuted(Action(WDSupportWeaponIds.AirTransport, "ally", origin: "owned",
            installationId: plant.InstallationId,
            parameters: new[] { new WDWeaponActionParameter("troops", "1") }));
        int before = runtime.FindPlayer("p2").Energy;

        source.CurrentRound = 2;
        Assert.That(coordinator.ProcessEnergyPlantPayments(2), Is.EqualTo(1));
        Assert.That(coordinator.ProcessEnergyPlantPayments(2), Is.Zero);

        Assert.That(runtime.FindPlayer("p2").Energy - before, Is.EqualTo(5));
        Assert.That(plant.PaymentsRemaining, Is.EqualTo(2));
    }

    [Test]
    public void RadarHistoryRemainsPrivateAfterUnusedRadarIsTransported()
    {
        var radar = new WDWeaponInstallationState("moving-radar", WDTacticalWeaponIds.Radar,
            "owned", "p1", 0, 0, 1, 0, 1, true, 0);
        Assert.That(mutations.Install("moving-radar-fixture", radar).Succeeded, Is.True);
        Assert.That(roundState.StoreRadarResult(new WDRadarRevealResult(
            1, "enemy", true, "p1", "historic-scan")), Is.True);

        AssertExecuted(Action(WDSupportWeaponIds.AirTransport, "ally", origin: "owned",
            installationId: radar.InstallationId,
            parameters: new[] { new WDWeaponActionParameter("troops", "1") }));

        Assert.That(radar.OwnerPlayerId, Is.EqualTo("p2"));
        Assert.That(roundState.GetRadarResult("historic-scan", "p1"), Is.Not.Null);
        Assert.That(roundState.GetRadarResult("historic-scan", "p2"), Is.Null);
    }

    [TestCase(WDSupportWeaponIds.Bunker, 0, 7, 0, 0)]
    [TestCase(WDSupportWeaponIds.FlakTower, 2, 0, 0, 0)]
    [TestCase(WDStrategicWeaponIds.EnergyPlant, 0, 0, 0, 2)]
    [TestCase(WDStrategicWeaponIds.Artillery, 0, 0, 5, 0)]
    [TestCase(WDStrategicWeaponIds.Rockets, 0, 0, 2, 0)]
    [TestCase(WDSpecialWeaponIds.Drone, 0, 0, 2, 0)]
    [TestCase(WDTacticalWeaponIds.Radar, 0, 0, 1, 0)]
    public void NeutralThenCapturePreservesCanonicalInstallationState(
        string weaponId, int duration, int barrier, int charges, int payments)
    {
        string id = "captured-" + weaponId;
        var installation = new WDWeaponInstallationState(id, weaponId, "owned", "p1",
            duration, barrier, charges, payments, 3,
            WDWeaponInstallationRegistry.IsAirTransportAllowed(weaponId), 0);
        installation.TryMarkAutomaticRound(0);
        Assert.That(mutations.Install("fixture-" + id, installation).Succeeded, Is.True);

        Assert.That(mutations.MakeNeutral("neutral-" + id, "owned", 1).Succeeded, Is.True);
        Assert.That(installation.IsAbandoned, Is.True);
        Assert.That(mutations.MakeOccupied("capture-" + id, "owned", "p2", 3).Succeeded,
            Is.True);

        Assert.That(installation.OwnerPlayerId, Is.EqualTo("p2"));
        Assert.That(installation.Level, Is.EqualTo(3));
        Assert.That(installation.DurationRemaining, Is.EqualTo(duration));
        Assert.That(installation.BarrierRemaining, Is.EqualTo(barrier));
        Assert.That(installation.ChargesRemaining, Is.EqualTo(charges));
        Assert.That(installation.PaymentsRemaining, Is.EqualTo(payments));
        Assert.That(installation.LastAutomaticRound, Is.EqualTo(0));
    }

    [Test]
    public void AirTransportRejectsQuantityAboveLevelCapacityWithoutMutation()
    {
        source.TryApplyTerritory("owned", "p1", 20, WDWeaponTerritoryStateKind.Occupied);
        SetLevel("p1", WDSupportWeaponIds.AirTransport, 1);
        WDWeaponAction action = Action(WDSupportWeaponIds.AirTransport, "ally",
            origin: "owned", parameters: new[]
            {
                new WDWeaponActionParameter("troops", "6")
            });

        Assert.That(coordinator.Execute(action).Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.Invalidated));
        Assert.That(context.FindTerritory("owned").Troops, Is.EqualTo(20));
        Assert.That(context.FindTerritory("ally").Troops, Is.EqualTo(4));
    }

    [Test]
    public void AirTransportRejectsQuantityThatWouldEmptyOriginWithoutMutation()
    {
        SetLevel("p1", WDSupportWeaponIds.AirTransport, 6);
        WDWeaponAction action = Action(WDSupportWeaponIds.AirTransport, "ally",
            origin: "owned", parameters: new[]
            {
                new WDWeaponActionParameter("troops", "5")
            });

        Assert.That(coordinator.Execute(action).Status,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.Invalidated));
        Assert.That(context.FindTerritory("owned").Troops, Is.EqualTo(5));
        Assert.That(context.FindTerritory("ally").Troops, Is.EqualTo(4));
    }

    [Test]
    public void AirTransportUsesSelectedQuantityAndLeavesOneAtOrigin()
    {
        SetLevel("p1", WDSupportWeaponIds.AirTransport, 1);
        AssertExecuted(Action(WDSupportWeaponIds.AirTransport, "ally",
            origin: "owned", parameters: new[]
            {
                new WDWeaponActionParameter("troops", "4")
            }));

        Assert.That(context.FindTerritory("owned").Troops, Is.EqualTo(1));
        Assert.That(context.FindTerritory("ally").Troops, Is.EqualTo(8));
    }

    [TestCase(WDSupportWeaponIds.Minefield)]
    [TestCase(WDSupportWeaponIds.FlakTower)]
    public void HiddenInstallationVisibilityExcludesEnemyAndSpectator(string weaponId)
    {
        Assert.That(WDWeaponMatchUIController.IsInstallationVisibleTo(
            weaponId, "p1", "p1", false), Is.True);
        Assert.That(WDWeaponMatchUIController.IsInstallationVisibleTo(
            weaponId, "p1", "p2", true), Is.True);
        Assert.That(WDWeaponMatchUIController.IsInstallationVisibleTo(
            weaponId, "p1", "p3", false), Is.False);
        Assert.That(WDWeaponMatchUIController.IsInstallationVisibleTo(
            weaponId, "p1", string.Empty, false), Is.False);
    }

    [TestCase(1,2)] [TestCase(2,3)] [TestCase(3,4)] [TestCase(4,5)]
    public void ParatroopersOccupyEmptyAtAnyDistance(int level,int troops)
    {
        mutations.MakeEmpty("empty-enemy","enemy"); SetLevel("p1",WDTacticalWeaponIds.Paratroopers,level);
        AssertExecuted(Action(WDTacticalWeaponIds.Paratroopers,"enemy"));
        Assert.That(context.FindTerritory("enemy").OwnerPlayerId,Is.EqualTo("p1"));
        Assert.That(context.FindTerritory("enemy").Troops,Is.EqualTo(troops));
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

    // ============================================================
    // 10. INTEGRAÇÃO DE RODADA: PREPARAÇÃO, ESTADO VIVO E LIFECYCLE
    // ============================================================

    [Test]
    public void FrozenPreparationCrossesGlobalPipelineBeforeTerrestrialAndConsolidation()
    {
        var preparation = new WDWeaponPreparationService(runtime, context, catalog);
        preparation.BeginRound(1);
        Assert.That(preparation.Program(new WDWeaponProgrammingRequest(
            "p1", WDSupportWeaponIds.Reinforcements,
            targetTerritoryId: "owned")).Succeeded, Is.True);

        IReadOnlyList<WDWeaponAction> frozen = preparation.FreezeRound();
        int troopsObservedByTerrestrial = -1;
        int troopsObservedByConsolidation = -1;
        var pipeline = new WDWeaponResolutionPipeline();
        WDGlobalResolutionResult result = pipeline.ResolveRound(
            new WDGlobalResolutionRequest(
                "integration:complete-round", 1, frozen, coordinator,
                resolveTerrestrial: () =>
                {
                    troopsObservedByTerrestrial = context.FindTerritory("owned").Troops;
                    return 1;
                },
                consolidate: () =>
                {
                    troopsObservedByConsolidation = context.FindTerritory("owned").Troops;
                    roundState.ObserveTerritoryHistory(1, context.Territories);
                }));

        Assert.That(frozen, Has.Count.EqualTo(1));
        Assert.That(troopsObservedByTerrestrial, Is.EqualTo(13));
        Assert.That(troopsObservedByConsolidation, Is.EqualTo(13));
        Assert.That(context.FindTerritory("owned").Troops, Is.EqualTo(13));
        Assert.That(result.Entries.Single(item =>
            item.Kind == WDGlobalResolutionEntryKind.WeaponAction).WeaponStatus,
            Is.EqualTo(WDIntegratedWeaponExecutionStatus.Executed));
    }

    [Test]
    public void DuplicateGlobalResolutionDoesNotRepeatCentralMercenariesOrSystemHooks()
    {
        SetLevel("p1", WDStrategicWeaponIds.EnergyPlant, 1);
        SetLevel("p1", WDSupportWeaponIds.Mercenaries, 1);
        var pipeline = new WDWeaponResolutionPipeline();
        int terrestrialCalls = 0;
        int consolidationCalls = 0;
        var request = new WDGlobalResolutionRequest(
            "integration:idempotent-economy", 1,
            new[]
            {
                Action(WDStrategicWeaponIds.EnergyPlant, "owned",
                    installationId: "integration-central"),
                Action(WDSupportWeaponIds.Mercenaries, "")
            },
            coordinator,
            resolveTerrestrial: () => ++terrestrialCalls,
            consolidate: () => consolidationCalls++);

        WDGlobalResolutionResult first = pipeline.ResolveRound(request);
        int energyAfterFirst = runtime.FindPlayer("p1").Energy;
        int poolAfterFirst = roundState.TakeReinforcementPool("p1");
        WDGlobalResolutionResult second = pipeline.ResolveRound(request);

        Assert.That(second, Is.SameAs(first));
        Assert.That(runtime.FindPlayer("p1").Energy, Is.EqualTo(energyAfterFirst));
        Assert.That(poolAfterFirst, Is.EqualTo(1));
        Assert.That(roundState.TakeReinforcementPool("p1"), Is.Zero);
        Assert.That(terrestrialCalls, Is.EqualTo(1));
        Assert.That(consolidationCalls, Is.EqualTo(1));
        Assert.That(registry.GetByTerritory("owned").Count(item =>
            item.WeaponId == WDStrategicWeaponIds.EnergyPlant), Is.EqualTo(1));
    }

    [Test]
    public void NextRoundExpiresPeaceButPreservesCentralAndPaysOnlyOnce()
    {
        SetLevel("p1", WDStrategicWeaponIds.EnergyPlant, 1);
        var pipeline = new WDWeaponResolutionPipeline();
        pipeline.ResolveRound(new WDGlobalResolutionRequest(
            "integration:lifecycle:round-1", 1,
            new[]
            {
                Action(WDStrategicWeaponIds.EnergyPlant, "owned",
                    installationId: "lifecycle-central"),
                Action(WDStrategicWeaponIds.Peace, "", targetPlayer: "p3")
            }, coordinator));
        Assert.That(roundState.HasPeace("p1", "p3"), Is.True);

        source.CurrentRound = 2;
        runtime.AdvanceCooldownsForRound(2);
        int energyBeforeRoundTwo = runtime.FindPlayer("p1").Energy;
        WDGlobalResolutionResult roundTwo = pipeline.ResolveRound(
            new WDGlobalResolutionRequest(
                "integration:lifecycle:round-2", 2,
                Array.Empty<WDWeaponAction>(), coordinator));
        int energyAfterRoundTwo = runtime.FindPlayer("p1").Energy;
        WDGlobalResolutionResult duplicate = pipeline.ResolveRound(
            new WDGlobalResolutionRequest(
                "integration:lifecycle:round-2", 2,
                Array.Empty<WDWeaponAction>(), coordinator));

        Assert.That(roundState.HasPeace("p1", "p3"), Is.False);
        Assert.That(registry.Find("lifecycle-central"), Is.Not.Null);
        Assert.That(registry.Find("lifecycle-central").IsDestroyed, Is.False);
        Assert.That(energyAfterRoundTwo - energyBeforeRoundTwo, Is.EqualTo(5));
        Assert.That(runtime.FindPlayer("p1").Energy, Is.EqualTo(energyAfterRoundTwo));
        Assert.That(duplicate, Is.SameAs(roundTwo));
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

    private static WDWeaponAction InfiltratorAction(
        string actionId, string author, string targetPlayer,
        string targetWeaponId, int programOrder) =>
        new(actionId, author, WDSpecialWeaponIds.Infiltrator, 3,
            WDWeaponResolutionPhase.Infiltration, programOrder,
            targetPlayerId: targetPlayer,
            parameters: new[]
            {
                new WDWeaponActionParameter("targetWeaponId", targetWeaponId)
            });

    private void EnterRoundThree()
    {
        source.CurrentRound = 3;
        runtime.AdvanceCooldownsForRound(2);
        runtime.AdvanceCooldownsForRound(3);
        roundState.BeginRound(3);
    }

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

    private static string OwnerPlayerId(TerritorioClique.Dono owner) =>
        owner == TerritorioClique.Dono.Jogador1 ? "p1" : string.Empty;

    private static ResultadoAcaoTerrestre TerrestrialResult(
        ResultadoAcaoTerrestre.TipoResultado type, bool successful,
        int position = 1)
    {
        var order = new OrdemTerrestre(
            null, null, 3, TerritorioClique.Dono.Jogador1, position);
        ResultadoCombate combat = type == ResultadoAcaoTerrestre.TipoResultado.Ataque
            ? new ResultadoCombate(
                true, successful, 3, 2, successful ? 1 : 0,
                successful ? 0 : 1, successful ? "Conquista" : "Defesa")
            : null;
        return new ResultadoAcaoTerrestre(
            true, type, 3, string.Empty, combat, order);
    }

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

    [Test]
    public void HudCardInteractivityUsesCanonicalAvailability()
    {
        var available = new WDWeaponHudCardState("radar", "Radar", 1, 25, 0,
            WDWeaponAvailabilityReason.Available, false, false);
        var insufficient = new WDWeaponHudCardState("radar", "Radar", 1, 25, 0,
            WDWeaponAvailabilityReason.InsufficientEnergy, false, false);
        var cooldown = new WDWeaponHudCardState("radar", "Radar", 1, 25, 2,
            WDWeaponAvailabilityReason.Cooldown, false, false);

        Assert.That(WarDominionMatchHUD.IsCardInteractable(available, true), Is.True);
        Assert.That(WarDominionMatchHUD.IsCardInteractable(insufficient, true), Is.False);
        Assert.That(WarDominionMatchHUD.IsCardInteractable(cooldown, true), Is.False);
        Assert.That(WarDominionMatchHUD.IsCardInteractable(available, false), Is.False);
        Assert.That(WarDominionMatchHUD.ShouldAcceptInsufficientEnergyClick(
            insufficient, true), Is.True);
        Assert.That(WarDominionMatchHUD.ShouldAcceptInsufficientEnergyClick(
            cooldown, true), Is.False);
        Assert.That(WarDominionMatchHUD.ShouldAcceptInsufficientEnergyClick(
            insufficient, false), Is.False);
    }

    [Test]
    public void WeaponCardIgnoresHoverButKeepsPressFeedback()
    {
        var cardObject = new GameObject("TouchFirstWeaponCard");
        WarDominionUITheme testTheme = ScriptableObject.CreateInstance<WarDominionUITheme>();
        try
        {
            WDUIPremiumButton card = cardObject.AddComponent<WDUIPremiumButton>();
            card.Build(testTheme, "INFILTRADO", null);
            card.SetHoverFeedbackEnabled(false);
            Color normal = cardObject.GetComponent<UnityEngine.UI.Image>().color;

            card.OnPointerEnter(null);
            Color afterHover = cardObject.GetComponent<UnityEngine.UI.Image>().color;
            card.OnPointerDown(null);
            Color whilePressed = cardObject.GetComponent<UnityEngine.UI.Image>().color;

            Assert.That(card.HoverFeedbackEnabled, Is.False);
            Assert.That(afterHover, Is.EqualTo(normal));
            Assert.That(whilePressed, Is.Not.EqualTo(normal));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(cardObject);
            UnityEngine.Object.DestroyImmediate(testTheme);
        }
    }

    [Test]
    public void WeaponCardStatusKeepsEnergyCostVisibleAcrossLockAndAvailability()
    {
        var lockedRoundOne = new WDWeaponHudCardState(
            WDSpecialWeaponIds.Infiltrator, "Infiltrado", 1, 20, 2,
            WDWeaponAvailabilityReason.InitialRoundLock, false, false);
        var lockedRoundTwo = new WDWeaponHudCardState(
            WDSpecialWeaponIds.Infiltrator, "Infiltrado", 1, 20, 1,
            WDWeaponAvailabilityReason.InitialRoundLock, false, false);
        var insufficient = new WDWeaponHudCardState(
            WDSpecialWeaponIds.Infiltrator, "Infiltrado", 1, 20, 0,
            WDWeaponAvailabilityReason.InsufficientEnergy, false, false);
        var cooldown = new WDWeaponHudCardState(
            WDSpecialWeaponIds.Infiltrator, "Infiltrado", 1, 20, 4,
            WDWeaponAvailabilityReason.Cooldown, false, false);

        Assert.That(WarDominionMatchHUD.FormatCardStatus(lockedRoundOne, 0),
            Is.EqualTo("BLOQUEADA 2  |  E 20"));
        Assert.That(WarDominionMatchHUD.FormatCardStatus(lockedRoundTwo, 0),
            Is.EqualTo("BLOQUEADA 1  |  E 20"));
        Assert.That(WarDominionMatchHUD.FormatCardStatus(insufficient, 0),
            Is.EqualTo("ENERGIA 0/20"));
        Assert.That(WarDominionMatchHUD.FormatCardStatus(cooldown, 0),
            Is.EqualTo("CD 4  |  E 20"));
    }

    [Test]
    public void ActionsControlOccupiesCompactPositionWithOrWithoutTeams()
    {
        Assert.That(WarDominionMatchHUD.ActionsControlTop(false), Is.EqualTo(-803f));
        Assert.That(WarDominionMatchHUD.ActionsControlTop(true), Is.EqualTo(-803f));
        Assert.That(WarDominionMatchHUD.ActionsControlTop(true) -
            WarDominionMatchHUD.ActionsControlTop(false), Is.Zero);
    }

    // 01. A mesma seleção deve ativar somente uma das duas composições de HUD.
    [Test]
    public void MatchHudSelectionKeepsNewAndLegacyModesExclusive()
    {
        var newHud = new GameObject("FinalMatchHUD");
        var legacyHud = new GameObject("LegacyHUD");
        Canvas legacy = legacyHud.AddComponent<Canvas>();
        try
        {
            UICompositionRoot.ApplyHudMode(newHud, legacy, true);
            Assert.That(newHud.activeSelf, Is.False);
            Assert.That(legacy.enabled, Is.False);

            UICompositionRoot.ApplyHudMode(newHud, legacy, false);
            Assert.That(newHud.activeSelf, Is.False);
            Assert.That(legacy.enabled, Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(legacyHud);
            UnityEngine.Object.DestroyImmediate(newHud);
        }
    }

    [Test]
    public void TacticalPingIsPrivateToTeamAndCarriesNoGameplayMutation()
    {
        var ping = new WDTacticalPing(WDTacticalPingKind.Focus, "owned",
            TerritorioClique.Dono.Jogador1, TerritorioClique.Dono.Jogador3);

        Assert.That(WDMatchInformationalOverlayController.IsVisibleTo(
            ping, TerritorioClique.Dono.Jogador1, false, false), Is.True);
        Assert.That(WDMatchInformationalOverlayController.IsVisibleTo(
            ping, TerritorioClique.Dono.Jogador3, false, true), Is.True);
        Assert.That(WDMatchInformationalOverlayController.IsVisibleTo(
            ping, TerritorioClique.Dono.Jogador2, false, false), Is.False);
        Assert.That(WDMatchInformationalOverlayController.IsVisibleTo(
            ping, TerritorioClique.Dono.Jogador3, true, true), Is.False);
        Assert.That(ping.TerritoryId, Is.EqualTo("owned"));
    }

    [Test]
    public void RegionOverlayDescriptionUsesCanonicalNameAndBonus()
    {
        var region = new DefinicaoRegiaoMapa { nomeExibido = "Norte", bonus = 5 };
        Assert.That(WDMatchInformationalOverlayController.DescribeRegion(region),
            Is.EqualTo("Norte\nBÔNUS +5"));
        Assert.That(WDMatchInformationalOverlayController.DescribeRegionBonus(region),
            Is.EqualTo("+5"));
    }

    [Test]
    public void CompleteRegionsAddTroopBonusesWithoutChangingEnergy()
    {
        var regions = new[]
        {
            new DefinicaoRegiaoMapa
            {
                bonus = 3,
                territorioIds = new List<string> { "a", "b" }
            },
            new DefinicaoRegiaoMapa
            {
                bonus = 5,
                territorioIds = new List<string> { "c", "d" }
            }
        };
        var owned = new HashSet<string>(new[] { "a", "b", "c", "d" });
        int energyBefore = runtime.FindPlayer("p1").Energy;

        Assert.That(MapaAtivo.CalcularBonusRegioesControladas(
            regions, owned.Contains), Is.EqualTo(8));
        owned.Remove("d");
        Assert.That(MapaAtivo.CalcularBonusRegioesControladas(
            regions, owned.Contains), Is.EqualTo(3));
        Assert.That(runtime.FindPlayer("p1").Energy, Is.EqualTo(energyBefore));
    }

    [Test]
    public void RegionOverlayHidesAndRestoresCountersWithoutChangingTroops()
    {
        var territoryObject = new GameObject("RegionTerritory");
        var counterObject = new GameObject("Counter");
        var overlayObject = new GameObject("RegionOverlay");
        try
        {
            territoryObject.AddComponent<TerritorioTropas>();
            TerritorioClique territory = territoryObject.AddComponent<TerritorioClique>();
            Assert.That(territory.TentarAplicarEstadoAutoritativo(
                TerritorioClique.Dono.Jogador1, 7,
                TerritorioClique.EstadoTerritorial.Ocupado), Is.True);
            counterObject.transform.SetParent(territoryObject.transform);
            counterObject.AddComponent<ContadorTropas>();
            WDMatchInformationalOverlayController overlay =
                overlayObject.AddComponent<WDMatchInformationalOverlayController>();

            overlay.SetTerritoryCountersForRegions(new[] { territory }, true);
            Assert.That(counterObject.activeSelf, Is.False);
            Assert.That(territory.Tropas, Is.EqualTo(7));
            Assert.That(territory.dono, Is.EqualTo(TerritorioClique.Dono.Jogador1));
            Assert.That(territory.Estado,
                Is.EqualTo(TerritorioClique.EstadoTerritorial.Ocupado));

            overlay.SetTerritoryCountersForRegions(null, false);
            Assert.That(counterObject.activeSelf, Is.True);
            Assert.That(territory.Tropas, Is.EqualTo(7));
            Assert.That(territory.dono, Is.EqualTo(TerritorioClique.Dono.Jogador1));
            Assert.That(territory.Estado,
                Is.EqualTo(TerritorioClique.EstadoTerritorial.Ocupado));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(overlayObject);
            UnityEngine.Object.DestroyImmediate(territoryObject);
        }
    }

    [Test]
    public void ConquestPresentationUsesCanonicalEventAndOnePointFiveSeconds()
    {
        ResolutionVisualEvent visual = ResolutionVisualEvent.Attack(
            TerritorioClique.Dono.Jogador1, "owned", "enemy", 4,
            5, 1, 2, 3, true, TerritorioClique.Dono.Jogador2,
            TerritorioClique.Dono.Jogador1);

        Assert.That(visual.Conquered, Is.True);
        Assert.That(visual.Result, Is.EqualTo(ResolutionVisualResult.TerritoryConquered));
        Assert.That(TerritorioVisual.DuracaoDestaqueConquista, Is.EqualTo(1.5f));
    }

    [Test]
    public void EmptyOccupationMovementIsCanonicalConquestEvent()
    {
        ResolutionVisualEvent visual = ResolutionVisualEvent.Movement(
            TerritorioClique.Dono.Jogador1, "owned", "empty", 2,
            4, 2, 0, 2, TerritorioClique.Dono.Neutro,
            TerritorioClique.Dono.Jogador1);

        Assert.That(visual.Conquered, Is.True);
        Assert.That(visual.Result, Is.EqualTo(ResolutionVisualResult.TerritoryConquered));
        Assert.That(visual.PreviousOwner, Is.EqualTo(TerritorioClique.Dono.Neutro));
        Assert.That(visual.NewOwner, Is.EqualTo(TerritorioClique.Dono.Jogador1));
    }

    [Test]
    public void TerritoryTargetingResolvesClicksOriginatingOnArmyCounter()
    {
        var territoryObject = new GameObject("TargetTerritory");
        var counterObject = new GameObject("Counter");
        try
        {
            territoryObject.AddComponent<TerritorioTropas>();
            TerritorioClique territory = territoryObject.AddComponent<TerritorioClique>();
            counterObject.transform.SetParent(territoryObject.transform);
            ContadorTropas counter = counterObject.AddComponent<ContadorTropas>();
            counter.Configurar(territory);
            BoxCollider2D collider = counterObject.AddComponent<BoxCollider2D>();

            Assert.That(InputPartida.TentarObterTerritorio(collider, out TerritorioClique result),
                Is.True);
            Assert.That(result, Is.SameAs(territory));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(territoryObject);
        }
    }

    [Test]
    public void RegionCenterUsesAllCanonicalTerritoryPositions()
    {
        Vector3[] positions =
        {
            new Vector3(-3f, 2f, 0f),
            new Vector3(3f, 4f, 0f),
            new Vector3(0f, 0f, 0f)
        };

        Assert.That(WDMatchInformationalOverlayController.TryCalculateRegionCenter(
            positions, out Vector3 center), Is.True);
        Assert.That(center, Is.EqualTo(new Vector3(0f, 2f, 0f)));
        Assert.That(WDMatchInformationalOverlayController.TryCalculateRegionCenter(
            Array.Empty<Vector3>(), out _), Is.False);
    }

    [Test]
    public void SelectionRaisePersistsUntilCleanupWithoutMovingTerritory()
    {
        var territoryObject = new GameObject("RaisedTerritory");
        try
        {
            territoryObject.AddComponent<SpriteRenderer>();
            territoryObject.AddComponent<TerritorioTropas>();
            TerritorioClique territory = territoryObject.AddComponent<TerritorioClique>();
            TerritorioVisual visual = territoryObject.AddComponent<TerritorioVisual>();
            Assert.That(territory.TentarAplicarEstadoAutoritativo(
                TerritorioClique.Dono.Jogador1, 6,
                TerritorioClique.EstadoTerritorial.Ocupado), Is.True);
            Vector3 position = territoryObject.transform.localPosition;
            Vector3 worldPosition = territoryObject.transform.position;
            Vector3 baseScale = territoryObject.transform.localScale;

            territory.DestacarSelecao();
            float selectedVisualScale = visual.SelectionVisualScale;
            for (int index = 0; index < 10; index++)
                territory.DestacarSelecao();

            Assert.That(visual.SelectionRaised, Is.True);
            Assert.That(territoryObject.transform.localPosition, Is.EqualTo(position));
            Assert.That(territoryObject.transform.position, Is.EqualTo(worldPosition));
            Assert.That(territoryObject.transform.localScale, Is.EqualTo(baseScale));
            Assert.That(visual.SelectionVisualScale, Is.EqualTo(selectedVisualScale));
            Assert.That(selectedVisualScale, Is.EqualTo(1.055f).Within(0.0001f));
            Assert.That(territory.dono, Is.EqualTo(TerritorioClique.Dono.Jogador1));
            Assert.That(territory.Tropas, Is.EqualTo(6));
            Assert.That(territory.Estado,
                Is.EqualTo(TerritorioClique.EstadoTerritorial.Ocupado));

            territory.RemoverDestaqueSelecao();
            Assert.That(visual.SelectionRaised, Is.False);
            Assert.That(territoryObject.transform.localPosition, Is.EqualTo(position));
            Assert.That(territoryObject.transform.position, Is.EqualTo(worldPosition));
            Assert.That(territoryObject.transform.localScale, Is.EqualTo(baseScale));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(territoryObject);
        }
    }

    [Test]
    public void SelectionScaleUsesOffCenterSpriteVisualCenterWithoutTranslation()
    {
        var territoryObject = new GameObject("OffCenterTerritory");
        var texture = new Texture2D(20, 10);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 20f, 10f),
            new Vector2(0.1f, 0.8f), 10f);
        try
        {
            SpriteRenderer renderer = territoryObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            territoryObject.AddComponent<TerritorioTropas>();
            TerritorioClique territory = territoryObject.AddComponent<TerritorioClique>();
            TerritorioVisual visual = territoryObject.AddComponent<TerritorioVisual>();
            territoryObject.transform.position = new Vector3(4f, -3f, 0f);
            Vector3 rootPosition = territoryObject.transform.position;
            Vector3 visualCenter = renderer.bounds.center;

            territory.DestacarSelecao();
            SpriteRenderer proxy = territoryObject.transform
                .Find("SelectionScaleRoot/SelectionSprite")
                .GetComponent<SpriteRenderer>();

            Assert.That(territoryObject.transform.position, Is.EqualTo(rootPosition));
            Assert.That(visual.SelectionVisualCenterWorld, Is.EqualTo(visualCenter));
            Assert.That(proxy.bounds.center.x, Is.EqualTo(visualCenter.x).Within(0.0001f));
            Assert.That(proxy.bounds.center.y, Is.EqualTo(visualCenter.y).Within(0.0001f));
            Assert.That(visual.SelectionVisualScale, Is.EqualTo(1.055f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(territoryObject);
            UnityEngine.Object.DestroyImmediate(sprite);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void SelectionCenterRemainsInvariantAtLeftCenterAndRightMapPositions()
    {
        var texture = new Texture2D(400, 100);
        Sprite mapSizedMask = Sprite.Create(texture,
            new Rect(0f, 0f, 400f, 100f), new Vector2(0.5f, 0.5f), 10f);
        var territoryObjects = new List<GameObject>();
        try
        {
            foreach (float centerX in new[] { -12f, 0f, 12f })
            {
                var territoryObject = new GameObject($"Territory-{centerX}");
                territoryObjects.Add(territoryObject);
                SpriteRenderer renderer = territoryObject.AddComponent<SpriteRenderer>();
                renderer.sprite = mapSizedMask;
                PolygonCollider2D collider = territoryObject.AddComponent<PolygonCollider2D>();
                collider.SetPath(0, new[]
                {
                    new Vector2(centerX - 0.8f, -0.6f),
                    new Vector2(centerX + 0.8f, -0.6f),
                    new Vector2(centerX + 0.8f, 0.6f),
                    new Vector2(centerX - 0.8f, 0.6f)
                });
                territoryObject.AddComponent<TerritorioTropas>();
                TerritorioClique territory = territoryObject.AddComponent<TerritorioClique>();
                TerritorioVisual visual = territoryObject.AddComponent<TerritorioVisual>();
                Vector3 before = collider.bounds.center;
                Vector3 rootBefore = territoryObject.transform.position;

                territory.DestacarSelecao();
                Vector3 after = visual.SelectionVisualCenterWorld;
                territory.DestacarSelecao();

                Assert.That(Vector3.Distance(before, after), Is.LessThan(0.0001f),
                    $"Selection center moved at map X={centerX}.");
                Assert.That(territoryObject.transform.position, Is.EqualTo(rootBefore));
                Assert.That(visual.SelectionVisualScale, Is.EqualTo(1.055f).Within(0.0001f));
            }
        }
        finally
        {
            foreach (GameObject item in territoryObjects)
                UnityEngine.Object.DestroyImmediate(item);
            UnityEngine.Object.DestroyImmediate(mapSizedMask);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void OriginAndDestinationSelectionKeepBothTerritoryTransformsFixed()
    {
        var originObject = new GameObject("Origin");
        var destinationObject = new GameObject("Destination");
        try
        {
            TerritorioClique origin = CreateSelectableTerritory(originObject);
            TerritorioClique destination = CreateSelectableTerritory(destinationObject);
            originObject.transform.position = new Vector3(-4f, 2f, 0f);
            destinationObject.transform.position = new Vector3(3f, -1f, 0f);
            Vector3 originPosition = originObject.transform.position;
            Vector3 destinationPosition = destinationObject.transform.position;

            origin.DestacarSelecao();
            destination.DestacarSelecao();

            Assert.That(originObject.transform.position, Is.EqualTo(originPosition));
            Assert.That(destinationObject.transform.position, Is.EqualTo(destinationPosition));
            Assert.That(originObject.GetComponent<TerritorioVisual>().SelectionRaised, Is.True);
            Assert.That(destinationObject.GetComponent<TerritorioVisual>().SelectionRaised, Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(originObject);
            UnityEngine.Object.DestroyImmediate(destinationObject);
        }
    }

    [Test]
    public void RegionLabelPositionIsClampedInsideItsViewport()
    {
        Rect viewport = new Rect(-500f, -300f, 1000f, 600f);
        Vector2 size = new Vector2(250f, 64f);

        Vector2 result = WDMatchInformationalOverlayController.ClampToViewport(
            new Vector2(900f, -900f), viewport, size);

        Assert.That(result.x, Is.EqualTo(375f));
        Assert.That(result.y, Is.EqualTo(-268f));
    }

    [Test]
    public void RegionLabelUsesInlineContentAndGrowsOnlyWithPreferredText()
    {
        DefinicaoRegiaoMapa shortRegion = Region("asia", "a");
        shortRegion.nomeExibido = "ÁSIA";
        shortRegion.bonus = 3;

        Assert.That(WDMatchInformationalOverlayController.DescribeRegionInline(shortRegion),
            Is.EqualTo("ÁSIA +3"));
        Vector2 shortSize = WDMatchInformationalOverlayController.CalculateRegionBadgeSize(
            new Vector2(62f, 20f));
        Vector2 longSize = WDMatchInformationalOverlayController.CalculateRegionBadgeSize(
            new Vector2(148f, 20f));

        Assert.That(shortSize, Is.EqualTo(new Vector2(90f, 34f)));
        Assert.That(longSize, Is.EqualTo(new Vector2(176f, 34f)));
        Assert.That(longSize.x, Is.GreaterThan(shortSize.x));
        Assert.That(longSize.y, Is.EqualTo(shortSize.y));
    }

    private static TerritorioClique CreateSelectableTerritory(GameObject target)
    {
        target.AddComponent<SpriteRenderer>();
        target.AddComponent<TerritorioTropas>();
        target.AddComponent<TerritorioVisual>();
        TerritorioClique territory = target.AddComponent<TerritorioClique>();
        Assert.That(territory.TentarAplicarEstadoAutoritativo(
            TerritorioClique.Dono.Jogador1, 3,
            TerritorioClique.EstadoTerritorial.Ocupado), Is.True);
        return territory;
    }

    [Test]
    public void CompletingLastTerritoryEmitsOneCanonicalRegionEvent()
    {
        DefinicaoRegiaoMapa region = Region("south", "a", "b", "c");
        var owners = new Dictionary<string, TerritorioClique.Dono>
        {
            ["a"] = TerritorioClique.Dono.Jogador1,
            ["b"] = TerritorioClique.Dono.Jogador1,
            ["c"] = TerritorioClique.Dono.Jogador2
        };

        IReadOnlyList<ResolutionVisualEvent> events =
            WDRegionConquestVisualDetector.Detect(new[] { region }, owners,
                new[]
                {
                    new WDTerritoryOwnershipChange("c",
                        TerritorioClique.Dono.Jogador2,
                        TerritorioClique.Dono.Jogador1)
                });

        Assert.That(events.Count, Is.EqualTo(1));
        Assert.That(events[0].Type, Is.EqualTo(ResolutionVisualEventType.RegionConquest));
        Assert.That(events[0].Result, Is.EqualTo(ResolutionVisualResult.RegionConquered));
        Assert.That(events[0].RegionId, Is.EqualTo("south"));
        Assert.That(events[0].RegionTerritoryIds, Is.EquivalentTo(new[] { "a", "b", "c" }));
        Assert.That(events[0].Player, Is.EqualTo(TerritorioClique.Dono.Jogador1));
    }

    [Test]
    public void RegionConquestIsInterleavedBeforeTheNextTerritoryAction()
    {
        DefinicaoRegiaoMapa region = Region("south", "a", "b");
        var owners = new Dictionary<string, TerritorioClique.Dono>
        {
            ["a"] = TerritorioClique.Dono.Jogador1,
            ["b"] = TerritorioClique.Dono.Jogador2,
            ["c"] = TerritorioClique.Dono.Jogador2
        };
        ResolutionVisualEvent conquering = ResolutionVisualEvent.Attack(
            TerritorioClique.Dono.Jogador1, "a", "b", 3, 4, 1, 2, 1,
            true, TerritorioClique.Dono.Jogador2, TerritorioClique.Dono.Jogador1);
        ResolutionVisualEvent next = ResolutionVisualEvent.Attack(
            TerritorioClique.Dono.Jogador1, "b", "c", 1, 1, 1, 3, 2,
            false, TerritorioClique.Dono.Jogador2, TerritorioClique.Dono.Jogador2);

        IReadOnlyList<ResolutionVisualEvent> ordered =
            WDRegionConquestVisualDetector.InterleaveWithTerritoryResults(
                new[] { region }, owners, new[] { conquering, next });

        Assert.That(ordered.Count, Is.EqualTo(3));
        Assert.That(ordered[0], Is.SameAs(conquering));
        Assert.That(ordered[1].Type, Is.EqualTo(ResolutionVisualEventType.RegionConquest));
        Assert.That(ordered[1].RegionId, Is.EqualTo("south"));
        Assert.That(ordered[2], Is.SameAs(next));
    }

    [Test]
    public void OneTerritoryActionCompletingMultipleRegionsUsesDefinitionOrder()
    {
        DefinicaoRegiaoMapa first = Region("first", "shared", "a");
        DefinicaoRegiaoMapa second = Region("second", "shared", "b");
        var owners = new Dictionary<string, TerritorioClique.Dono>
        {
            ["shared"] = TerritorioClique.Dono.Jogador2,
            ["a"] = TerritorioClique.Dono.Jogador1,
            ["b"] = TerritorioClique.Dono.Jogador1
        };
        ResolutionVisualEvent conquering = ResolutionVisualEvent.Attack(
            TerritorioClique.Dono.Jogador1, "a", "shared", 3, 4, 1, 2, 1,
            true, TerritorioClique.Dono.Jogador2, TerritorioClique.Dono.Jogador1);

        IReadOnlyList<ResolutionVisualEvent> ordered =
            WDRegionConquestVisualDetector.InterleaveWithTerritoryResults(
                new[] { first, second }, owners, new[] { conquering });

        Assert.That(ordered.Select(item => item.RegionId),
            Is.EqualTo(new[] { string.Empty, "first", "second" }));
    }

    [Test]
    public void IncompleteOrAlliedRegionDoesNotEmitRegionEvent()
    {
        DefinicaoRegiaoMapa region = Region("north", "a", "b", "c");
        var owners = new Dictionary<string, TerritorioClique.Dono>
        {
            ["a"] = TerritorioClique.Dono.Jogador1,
            ["b"] = TerritorioClique.Dono.Jogador3,
            ["c"] = TerritorioClique.Dono.Jogador2
        };

        IReadOnlyList<ResolutionVisualEvent> events =
            WDRegionConquestVisualDetector.Detect(new[] { region }, owners,
                new[]
                {
                    new WDTerritoryOwnershipChange("c",
                        TerritorioClique.Dono.Jogador2,
                        TerritorioClique.Dono.Jogador1)
                });

        Assert.That(events, Is.Empty);
        Assert.That(owners["b"], Is.EqualTo(TerritorioClique.Dono.Jogador3));
    }

    [Test]
    public void AlreadyControlledRegionIsNotReemittedAndDetectionDoesNotMutateGameplay()
    {
        DefinicaoRegiaoMapa region = Region("west", "a", "b");
        var owners = new Dictionary<string, TerritorioClique.Dono>
        {
            ["a"] = TerritorioClique.Dono.Jogador1,
            ["b"] = TerritorioClique.Dono.Jogador1,
            ["outside"] = TerritorioClique.Dono.Jogador2
        };

        IReadOnlyList<ResolutionVisualEvent> events =
            WDRegionConquestVisualDetector.Detect(new[] { region }, owners,
                new[]
                {
                    new WDTerritoryOwnershipChange("outside",
                        TerritorioClique.Dono.Jogador2,
                        TerritorioClique.Dono.Jogador1)
                });

        Assert.That(events, Is.Empty);
        Assert.That(owners["outside"], Is.EqualTo(TerritorioClique.Dono.Jogador2));
    }

    private static DefinicaoRegiaoMapa Region(string id, params string[] territories) =>
        new DefinicaoRegiaoMapa
        {
            id = id,
            nomeExibido = id,
            bonus = 2,
            territorioIds = territories.ToList()
        };

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
            source.Add("neutral", "", 1, WDWeaponTerritoryStateKind.Neutral);
            source.Add("empty", "", 0, WDWeaponTerritoryStateKind.Empty);
            return source;
        }
        private void Add(string id, string owner, int troops) => territories.Add(id,
            new Territory { Id = id, Owner = owner, Troops = troops,
                State = WDWeaponTerritoryStateKind.Occupied });
        private void Add(string id, string owner, int troops,
            WDWeaponTerritoryStateKind state) => territories.Add(id,
            new Territory { Id = id, Owner = owner, Troops = troops, State = state });
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
