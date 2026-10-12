using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

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
    public void InfiltratorUnlocksOnRoundThreeWithVisibleTwoOneCountdown()
    {
        service.BeginRound(1);
        WDWeaponAvailability roundOne = service.GetOption(
            "p1", WDSpecialWeaponIds.Infiltrator).Availability;
        service.BeginRound(2);
        WDWeaponAvailability roundTwo = service.GetOption(
            "p1", WDSpecialWeaponIds.Infiltrator).Availability;
        service.BeginRound(3);
        WDWeaponAvailability roundThree = service.GetOption(
            "p1", WDSpecialWeaponIds.Infiltrator).Availability;

        Assert.That(roundOne.Reason, Is.EqualTo(WDWeaponAvailabilityReason.InitialRoundLock));
        Assert.That(roundOne.CooldownRemaining, Is.EqualTo(2));
        Assert.That(roundTwo.Reason, Is.EqualTo(WDWeaponAvailabilityReason.InitialRoundLock));
        Assert.That(roundTwo.CooldownRemaining, Is.EqualTo(1));
        Assert.That(roundThree.IsAvailable, Is.True);
    }

    [Test]
    public void InfiltratorRequiresTwentyEnergyWithoutCreatingResidualIntentOrCooldown()
    {
        WDWeaponMatchRuntime lowRuntime = RuntimeWithEnergy(19);
        WDWeaponPreparationService lowService = Service(lowRuntime, 3);
        var request = new WDWeaponProgrammingRequest(
            "p1", WDSpecialWeaponIds.Infiltrator,
            targetPlayerId: "p2",
            parameters: new[]
            {
                new WDWeaponActionParameter(
                    "targetWeaponId", WDSupportWeaponIds.AirStrike)
            });

        WDWeaponProgrammingResult denied = lowService.Program(request);

        Assert.That(denied.Succeeded, Is.False);
        Assert.That(denied.Availability.Reason,
            Is.EqualTo(WDWeaponAvailabilityReason.InsufficientEnergy));
        Assert.That(denied.Availability.EnergyCost, Is.EqualTo(20));
        Assert.That(denied.Action, Is.Null);
        Assert.That(lowService.GetPlayerActions("p1", 3), Is.Empty);
        Assert.That(lowRuntime.FindPlayer("p1").Energy, Is.EqualTo(19));
        Assert.That(lowRuntime.FindPlayer("p1").GetWeapon(
            WDSpecialWeaponIds.Infiltrator).CooldownRemaining, Is.Zero);

        WDWeaponMatchRuntime exactRuntime = RuntimeWithEnergy(20);
        WDWeaponPreparationService exactService = Service(exactRuntime, 3);
        WDWeaponProgrammingResult accepted = exactService.Program(request);

        Assert.That(accepted.Succeeded, Is.True);
        Assert.That(accepted.Action, Is.Not.Null);
        Assert.That(exactRuntime.FindPlayer("p1").Energy, Is.EqualTo(20));
        Assert.That(exactService.Cancel("p1", accepted.Action.ActionId).Succeeded,
            Is.True);
        Assert.That(exactRuntime.FindPlayer("p1").Energy, Is.EqualTo(20));
        Assert.That(exactRuntime.FindPlayer("p1").GetWeapon(
            WDSpecialWeaponIds.Infiltrator).CooldownRemaining, Is.Zero);
    }

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
    public void DroneInstallationShotAndRocketLaunchHaveDistinctSelectionContracts()
    {
        Assert.That(service.GetRequirement(WDSpecialWeaponIds.Drone).Fields,
            Is.EqualTo(WDWeaponSelectionFields.TargetTerritory |
                WDWeaponSelectionFields.Installation));
        WDWeaponSelectionRequirement droneShot = service.GetRequirement(
            WDSpecialWeaponIds.Drone, WDWeaponIntentKind.DroneShot);
        Assert.That(droneShot.Fields, Is.EqualTo(WDWeaponSelectionFields.TargetTerritory |
            WDWeaponSelectionFields.Installation));
        Assert.That(droneShot.AllowsEmptyTarget, Is.True);
        WDWeaponSelectionRequirement rocket = service.GetRequirement(
            WDStrategicWeaponIds.Rockets, WDWeaponIntentKind.RocketLaunch);
        Assert.That(rocket.Fields.HasFlag(WDWeaponSelectionFields.Installation), Is.True);
        Assert.That(rocket.MaximumGraphDistance, Is.EqualTo(3));
    }

    [Test]
    public void InfiltratorTargetQueryExposesCanonicalOpponentDeckWeapons()
    {
        WDWeaponProgrammingResult target = service.Program(Request("p2",
            WDStrategicWeaponIds.Peace, targetPlayer: "p1"));
        Assert.That(target.Succeeded, Is.True);

        IReadOnlyList<WDInfiltratorWeaponTarget> options = service
            .GetEligibleInfiltratorWeapons("p1", "p2");

        Assert.That(options.Select(item => item.WeaponId),
            Does.Contain(WDStrategicWeaponIds.Peace));
        Assert.That(options.Single(item => item.WeaponId == WDStrategicWeaponIds.Peace)
            .DisplayName, Is.EqualTo("Paz"));
    }

    [Test]
    public void InfiltratorTargetQueryDoesNotDependOnProgrammedOperations()
    {
        WDWeaponProgrammingResult droneShot = service.Program(new WDWeaponProgrammingRequest(
            "p2", WDSpecialWeaponIds.Drone, WDWeaponIntentKind.DroneShot,
            targetTerritoryId: "p1_a", installationId: "drone-live",
            parameters: new[] { new WDWeaponActionParameter("mode", "shot") }));
        WDWeaponProgrammingResult peace = service.Program(Request("p2",
            WDStrategicWeaponIds.Peace, targetPlayer: "p1"));

        IReadOnlyList<WDInfiltratorWeaponTarget> options = service
            .GetEligibleInfiltratorWeapons("p1", "p2");

        Assert.That(droneShot.Succeeded, Is.True);
        Assert.That(peace.Succeeded, Is.True);
        Assert.That(options.Select(item => item.WeaponId),
            Does.Contain(WDStrategicWeaponIds.Peace));
        Assert.That(options.All(item => !string.IsNullOrEmpty(item.WeaponId)), Is.True);
    }

    // ============================================================
    // 03. ARMAZENAMENTO, CANCELAMENTO E REPROGRAMAÇÃO
    // ============================================================

    [Test]
    public void ValidActionsHaveStableIdentityAndRemainIndependentByWeapon()
    {
        WDWeaponProgrammingResult first = service.Program(Request(
            "p1", WDStrategicWeaponIds.LongRangeBomb, target: "p2_a"));
        WDWeaponProgrammingResult second = service.Program(Request(
            "p1", WDSupportWeaponIds.AirStrike, origin: "p1_a", target: "p2_a"));

        Assert.That(first.Action.ActionId, Is.Not.EqualTo(second.Action.ActionId));
        Assert.That(first.Action.AuthorPlayerId, Is.EqualTo("p1"));
        Assert.That(first.Action.WeaponId, Is.EqualTo(WDStrategicWeaponIds.LongRangeBomb));
        Assert.That(first.Action.Round, Is.EqualTo(2));
        Assert.That(first.Action.State, Is.EqualTo(WDWeaponActionState.Programmed));
        Assert.That(second.Succeeded, Is.True);
    }

    [Test]
    public void SameCardCannotBeProgrammedTwiceInOnePreparation()
    {
        Assert.That(service.Program(Request("p1", WDStrategicWeaponIds.LongRangeBomb,
            target: "p2_a")).Succeeded, Is.True);
        Assert.That(service.Program(Request("p1", WDStrategicWeaponIds.LongRangeBomb,
            target: "p2_a")).Reason,
            Is.EqualTo(WDWeaponProgrammingReason.ProgrammingUsageLimit));
        WDWeaponProgrammingResult secondTarget = service.Program(Request(
            "p1", WDStrategicWeaponIds.LongRangeBomb, target: "neutral"));
        Assert.That(secondTarget.Reason,
            Is.EqualTo(WDWeaponProgrammingReason.ProgrammingUsageLimit));
        Assert.That(secondTarget.Availability.Reason,
            Is.EqualTo(WDWeaponAvailabilityReason.AlreadyProgrammedThisPreparation));
        Assert.That(service.GetOption("p1", WDStrategicWeaponIds.LongRangeBomb)
            .Availability.Reason,
            Is.EqualTo(WDWeaponAvailabilityReason.AlreadyProgrammedThisPreparation));
    }

    [Test]
    public void CancellingCardProgrammingAllowsItAgainInSamePreparation()
    {
        WDWeaponProgrammingResult first = service.Program(Request(
            "p1", WDStrategicWeaponIds.LongRangeBomb, target: "p2_a"));

        Assert.That(service.Cancel("p1", first.Action.ActionId).Succeeded, Is.True);
        Assert.That(service.GetOption("p1", WDStrategicWeaponIds.LongRangeBomb)
            .Availability.IsAvailable, Is.True);
        Assert.That(service.Program(Request("p1", WDStrategicWeaponIds.LongRangeBomb,
            target: "neutral")).Succeeded, Is.True);
    }

    [Test]
    public void EnergyPlantMatchLimitDoesNotPermitTwoInSamePreparation()
    {
        Assert.That(service.Program(Request("p1", WDStrategicWeaponIds.EnergyPlant,
            target: "p1_a", installation: "central-a")).Succeeded, Is.True);
        WDWeaponProgrammingResult second = service.Program(Request("p1",
            WDStrategicWeaponIds.EnergyPlant, target: "p3_a",
            installation: "central-b"));

        Assert.That(second.Reason,
            Is.EqualTo(WDWeaponProgrammingReason.ProgrammingUsageLimit));
        Assert.That(runtime.FindPlayer("p1").GetWeapon(
            WDStrategicWeaponIds.EnergyPlant).Uses, Is.Zero);
    }

    [Test]
    public void PersistentInstallationOperationsDoNotCountAsSecondCardProgramming()
    {
        WDWeaponProgrammingResult drone = service.Program(Request("p1",
            WDSpecialWeaponIds.Drone, target: "p1_a", installation: "drone-pending"));
        WDWeaponProgrammingResult shot = service.Program(new WDWeaponProgrammingRequest(
            "p1", WDSpecialWeaponIds.Drone, WDWeaponIntentKind.DroneShot,
            targetTerritoryId: "empty", installationId: "drone-pending",
            parameters: new[] { new WDWeaponActionParameter("mode", "shot") }));

        Assert.That(drone.Succeeded, Is.True);
        Assert.That(shot.Succeeded, Is.True);
        Assert.That(service.GetPlayerActions("p1", 2).Count, Is.EqualTo(2));
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

    [TestCase(WDTacticalWeaponIds.Blitzkrieg, "")]
    [TestCase(WDSupportWeaponIds.ReserveTroops, "")]
    [TestCase(WDSupportWeaponIds.TroopRecovery, "p1_a")]
    public void CancellingProvisionalBenefitLeavesNoRuntimeOrBoardResidue(
        string weaponId, string targetTerritoryId)
    {
        string boardBefore = state.Fingerprint();
        WDWeaponPlayerRuntimeState player = runtime.FindPlayer("p1");
        int energyBefore = player.Energy;
        WDWeaponRuntimeEntry entry = player.GetWeapon(weaponId);
        int cooldownBefore = entry.CooldownRemaining;
        int usesBefore = entry.Uses;
        WDWeaponProgrammingResult programmed = service.Program(
            Request("p1", weaponId, target: targetTerritoryId));

        Assert.That(programmed.Succeeded, Is.True);
        Assert.That(service.Cancel("p1", programmed.Action.ActionId).Succeeded, Is.True);
        Assert.That(service.GetPlayerActions("p1", 2), Is.Empty);
        Assert.That(state.Fingerprint(), Is.EqualTo(boardBefore));
        Assert.That(player.Energy, Is.EqualTo(energyBefore));
        Assert.That(entry.CooldownRemaining, Is.EqualTo(cooldownBefore));
        Assert.That(entry.Uses, Is.EqualTo(usesBefore));
    }

    [Test]
    public void CancellingInstallationRemovesDependentOperationByInstallationId()
    {
        WDWeaponProgrammingResult installation = service.Program(Request("p1",
            WDSpecialWeaponIds.Drone, target: "p1_a", installation: "drone-pending"));
        WDWeaponProgrammingResult shot = service.Program(new WDWeaponProgrammingRequest(
            "p1", WDSpecialWeaponIds.Drone, WDWeaponIntentKind.DroneShot,
            targetTerritoryId: "p2_a", installationId: "drone-pending"));

        Assert.That(installation.Succeeded, Is.True);
        Assert.That(shot.Succeeded, Is.True);
        Assert.That(service.Cancel("p1", installation.Action.ActionId).Succeeded, Is.True);
        Assert.That(service.GetPlayerActions("p1", 2), Is.Empty);
    }

    [Test]
    public void ConfirmedRadarActionIsDirectAndIrreversible()
    {
        WDWeaponProgrammingResult radar = service.Program(Request("p1",
            WDTacticalWeaponIds.Radar, target: "p2_a"));

        Assert.That(radar.Succeeded, Is.True);
        Assert.That(radar.Action.InstallationId, Is.Empty);
        Assert.That(service.MarkIrreversible(radar.Action.ActionId), Is.True);
        Assert.That(service.Cancel("p1", radar.Action.ActionId).Reason,
            Is.EqualTo(WDWeaponProgrammingReason.IrreversibleAction));
        Assert.That(service.GetPlayerActions("p1", 2).Single().ActionId,
            Is.EqualTo(radar.Action.ActionId));
    }

    [Test]
    public void RadarSelectionIsGlobalNonAlliedAndHasNoInstallationField()
    {
        WDWeaponSelectionRequirement radar = service.GetRequirement(
            WDTacticalWeaponIds.Radar);

        Assert.That(radar.Fields, Is.EqualTo(WDWeaponSelectionFields.TargetTerritory));
        Assert.That(radar.TargetRelation, Is.EqualTo(WDWeaponTerritoryRelation.NonAllied));
        Assert.That(radar.AllowsEmptyTarget, Is.True);
        Assert.That(service.GetSelectionReason(Request("p1", WDTacticalWeaponIds.Radar,
            target: "p1_a")), Is.EqualTo(WDWeaponProgrammingReason.InvalidTargetTerritory));
        Assert.That(service.GetSelectionReason(Request("p1", WDTacticalWeaponIds.Radar,
            target: "p3_a")), Is.EqualTo(WDWeaponProgrammingReason.InvalidTargetTerritory));
        Assert.That(service.GetPlayerActions("p1", 2), Is.Empty,
            "A validação usada pela UI não pode programar nem revelar informação.");
        Assert.That(service.Program(Request("p1", WDTacticalWeaponIds.Radar,
            target: "p2_a")).Succeeded, Is.True);
    }

    [TestCase("p2_a")]
    [TestCase("neutral")]
    [TestCase("empty")]
    public void RadarAcceptsEveryCanonicalGlobalTarget(string territoryId)
    {
        WDWeaponProgrammingResult result = service.Program(Request(
            "p1", WDTacticalWeaponIds.Radar, target: territoryId));

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Action.OriginTerritoryId, Is.Empty);
        Assert.That(result.Action.InstallationId, Is.Empty);
        Assert.That(result.Action.TargetTerritoryId, Is.EqualTo(territoryId));
    }

    [Test]
    public void BlitzkriegExpandsPreparationLimitAndCancellationRemovesDependentOrders()
    {
        GameObject managerObject = new("PreparationBenefitsManager");
        var territoryObjects = new List<GameObject>();
        try
        {
            GameManager manager = ConfigureManager(managerObject, out FilaAcoes queue);
            int normalLimit = manager.LimiteAcoesPorRodada;
            WDWeaponProgrammingResult programmed = manager.ProgramarArma(
                Request("p1", WDTacticalWeaponIds.Blitzkrieg));

            Assert.That(programmed.Succeeded, Is.True);
            Assert.That(normalLimit, Is.EqualTo(3));
            Assert.That(manager.LimiteAcoesPorRodada, Is.EqualTo(7));
            for (int i = 0; i < manager.LimiteAcoesPorRodada; i++)
            {
                GameObject originObject = new("Origin" + i);
                GameObject targetObject = new("Target" + i);
                territoryObjects.Add(originObject); territoryObjects.Add(targetObject);
                TerritorioClique origin = originObject.AddComponent<TerritorioClique>();
                TerritorioClique target = targetObject.AddComponent<TerritorioClique>();
                Assert.That(queue.Adicionar(new OrdemTerrestre(origin, target, 1,
                    TerritorioClique.Dono.Jogador1, i + 1), manager.LimiteAcoesPorRodada),
                    Is.True);
            }

            Assert.That(manager.CancelarAcaoArma("p1", programmed.Action.ActionId).Succeeded,
                Is.True);
            Assert.That(manager.LimiteAcoesPorRodada, Is.EqualTo(3));
            Assert.That(queue.QuantidadePara(TerritorioClique.Dono.Jogador1), Is.EqualTo(3));
        }
        finally
        {
            foreach (GameObject item in territoryObjects) UnityEngine.Object.DestroyImmediate(item);
            UnityEngine.Object.DestroyImmediate(managerObject);
        }
    }

    [Test]
    public void ReserveTroopsAreUsableDuringPreparationAndCancelWithoutResidue()
    {
        GameObject managerObject = new("ReserveBenefitsManager");
        GameObject territoryObject = new("ReserveTerritory");
        try
        {
            GameManager manager = ConfigureManager(managerObject, out _);
            WDWeaponRoundRuntime round = GetPrivate<WDWeaponRoundRuntime>(manager,
                "weaponRoundRuntime");
            round.CaptureOwnershipSnapshot(new[]
            {
                View("lost-a", "p1", 2), View("lost-b", "p1", 2)
            });
            round.ObserveOwnershipChanges(1, new[]
            {
                View("lost-a", "p2", 2), View("lost-b", "p2", 2)
            });
            round.BeginRound(2);
            manager.DefinirReforcos(2);
            TerritorioClique territory = CreateOwnedTerritory(territoryObject, "reserve-target", 1);
            WDWeaponProgrammingResult programmed = manager.ProgramarArma(
                Request("p1", WDSupportWeaponIds.ReserveTroops));

            Assert.That(programmed.Succeeded, Is.True);
            Assert.That(manager.reforcosDisponiveis, Is.EqualTo(4));
            Assert.That(manager.DistribuirReforcos(territory, 4), Is.EqualTo(4));
            Assert.That(territory.Tropas, Is.EqualTo(5));
            Assert.That(manager.CancelarAcaoArma("p1", programmed.Action.ActionId).Succeeded,
                Is.True);
            Assert.That(territory.Tropas, Is.EqualTo(3));
            Assert.That(manager.reforcosDisponiveis, Is.Zero);
            Assert.That(manager.DistribuicoesReforcos.Sum(item => item.Quantidade), Is.EqualTo(2));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(territoryObject);
            UnityEngine.Object.DestroyImmediate(managerObject);
        }
    }

    [Test]
    public void TroopRecoveryIsUsableDuringPreparationAndCancellationRemovesAllocation()
    {
        GameObject managerObject = new("RecoveryBenefitsManager");
        GameObject territoryObject = new("RecoveryTerritory");
        try
        {
            GameManager manager = ConfigureManager(managerObject, out _);
            WDWeaponRoundRuntime round = GetPrivate<WDWeaponRoundRuntime>(manager,
                "weaponRoundRuntime");
            round.CaptureTerritorySnapshot(new[] { View("p1_a", "p1", 5) });
            round.ObserveTerritoryHistory(1, new[] { View("p1_a", "p1", 2) });
            round.BeginRound(2);
            manager.DefinirReforcos(0);
            TerritorioClique territory = CreateOwnedTerritory(territoryObject, "p1_a", 2);
            WDWeaponProgrammingResult programmed = manager.ProgramarArma(
                Request("p1", WDSupportWeaponIds.TroopRecovery, target: "p1_a"));

            Assert.That(programmed.Succeeded, Is.True);
            Assert.That(manager.reforcosDisponiveis, Is.EqualTo(3));
            Assert.That(manager.DistribuirReforcos(territory, 3), Is.EqualTo(3));
            Assert.That(territory.Tropas, Is.EqualTo(5));
            Assert.That(manager.CancelarAcaoArma("p1", programmed.Action.ActionId).Succeeded,
                Is.True);
            Assert.That(territory.Tropas, Is.EqualTo(2));
            Assert.That(manager.reforcosDisponiveis, Is.Zero);
            Assert.That(manager.DistribuicoesReforcos, Is.Empty);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(territoryObject);
            UnityEngine.Object.DestroyImmediate(managerObject);
        }
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

    private GameManager ConfigureManager(GameObject gameObject, out FilaAcoes queue)
    {
        GameManager manager = gameObject.AddComponent<GameManager>();
        queue = gameObject.AddComponent<FilaAcoes>();
        SetPrivate(manager, "filaAcoes", queue);
        SetPrivate(manager, "weaponPreparation", service);
        var round = new WDWeaponRoundRuntime();
        SetPrivate(manager, "weaponRoundRuntime", round);
        SetPrivate(manager, "bloqueioInicioPartida", false);
        manager.faseAtual = GameManager.FaseTurno.Preparacao;
        manager.estadoPreparacao = GameManager.EstadoPreparacao.Preparando;
        manager.jogadorLocal = TerritorioClique.Dono.Jogador1;
        return manager;
    }

    private static WDGameWeaponTerritoryView View(string id, string owner, int troops) =>
        new(id, owner, troops, WDWeaponTerritoryStateKind.Occupied);

    private static TerritorioClique CreateOwnedTerritory(
        GameObject gameObject, string id, int troops)
    {
        gameObject.AddComponent<TerritorioTropas>();
        gameObject.AddComponent<TerritorioFronteiras>();
        TerritorioClique territory = gameObject.AddComponent<TerritorioClique>();
        territory.idTerritorio = id;
        Assert.That(territory.TentarAplicarEstadoAutoritativo(
            TerritorioClique.Dono.Jogador1, troops,
            TerritorioClique.EstadoTerritorial.Ocupado), Is.True);
        return territory;
    }

    private static void SetPrivate(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(target, value);

    private static T GetPrivate<T>(object target, string name) where T : class =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(target) as T;

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

    [Test]
    public void CancellationQueryUsesTheSameOwnershipIrreversibilityAndFreezeRulesAsCommand()
    {
        WDWeaponProgrammingResult programmed = service.Program(
            Request("p1", WDStrategicWeaponIds.Sabotage, targetPlayer: "p2"));
        Assert.That(programmed.Succeeded, Is.True);
        string id = programmed.Action.ActionId;
        Assert.That(service.GetCancellationReason("p1", id), Is.EqualTo(WDWeaponProgrammingReason.Success));
        Assert.That(service.GetCancellationReason("p2", id), Is.EqualTo(service.Cancel("p2", id).Reason));
        service.MarkIrreversible(id);
        Assert.That(service.GetCancellationReason("p1", id), Is.EqualTo(WDWeaponProgrammingReason.IrreversibleAction));
        Assert.That(service.Cancel("p1", id).Reason, Is.EqualTo(WDWeaponProgrammingReason.IrreversibleAction));
        service.FreezeRound();
        Assert.That(service.GetCancellationReason("p1", id), Is.EqualTo(WDWeaponProgrammingReason.RoundFrozen));
        Assert.That(service.Cancel("p1", id).Reason, Is.EqualTo(WDWeaponProgrammingReason.RoundFrozen));
        Assert.That(service.GetPlayerActions("p1", 2).Single().ActionId, Is.EqualTo(id));
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public void ActionsDrawerUsesCanonicalProgrammingAndXRespectsFreeze(bool freeze, bool infiltrator)
    {
        WDMatchSetup previous = WDMatchSetupContext.Current;
        GameObject managerObject = new("ActionsIntegrationManager");
        GameObject viewObject = new("ActionsIntegration", typeof(RectTransform));
        WarDominionUITheme theme = ScriptableObject.CreateInstance<WarDominionUITheme>();
        var previousEvents = RegisteredActionEventSystems().ToArray();
        var cameraObject = new GameObject("ActionsRaycastCamera", typeof(Camera));
        Camera testCamera = cameraObject.GetComponent<Camera>();
        testCamera.enabled = false;
        testCamera.cullingMask = 1 << 31;
        testCamera.clearFlags = CameraClearFlags.SolidColor;
        ActionsFixtureGraphicRaycaster raycaster = null;
        var target = new RenderTexture(1920, 1080, 24);
        testCamera.targetTexture = target;
        var events = new GameObject("ActionsEvents");
        events.SetActive(false);
        var eventSystem = events.AddComponent<ActionsFixtureEventSystem>();
        var inputModule = events.AddComponent<ActionsFixtureInputModule>();
        var inputActions = ScriptableObject.CreateInstance<UnityEngine.InputSystem.InputActionAsset>();
        var map = inputActions.AddActionMap("UI");
        var point = UnityEngine.InputSystem.InputActionReference.Create(
            map.AddAction("Point", UnityEngine.InputSystem.InputActionType.PassThrough, "<Pointer>/position"));
        var click = UnityEngine.InputSystem.InputActionReference.Create(
            map.AddAction("Click", UnityEngine.InputSystem.InputActionType.PassThrough, "<Pointer>/press"));
        inputModule.actionsAsset = inputActions;
        inputModule.point = point;
        inputModule.leftClick = click;
        try
        {
            // EditMode síncrono não executa automaticamente o lifecycle destes componentes.
            // Usar os callbacks reais, nunca escrever diretamente na lista estática.
            foreach (var previousEvent in previousEvents)
                EventSystemLifecycle(previousEvent, "OnDisable");
            events.SetActive(true);
            eventSystem.BeginFixture();
            inputModule.BeginFixture();
            eventSystem.ActivateModule();
            Assert.That(UnityEngine.EventSystems.EventSystem.current, Is.SameAs(eventSystem));
            Assert.That(RegisteredActionEventSystems(), Is.EqualTo(new[] { eventSystem }));
            Assert.That(eventSystem.currentInputModule, Is.SameAs(inputModule));
            StoreActionsSetup();
            service.BeginRound(3);
            GameManager manager = ConfigureManager(managerObject, out _);
            var rounds = managerObject.AddComponent<GerenciadorRodada>();
            SetPrivate(rounds, "rodadaAtual", 3);
            SetPrivate(manager, "gerenciadorRodada", rounds);
            SetPrivate(manager, "weaponRuntime", runtime);
            WDWeaponMatchUIController controller = managerObject.AddComponent<WDWeaponMatchUIController>();
            controller.Configure(manager);
            MatchUIPresenter presenter = managerObject.AddComponent<MatchUIPresenter>();
            SetPrivate(presenter, "gameManager", manager);
            var canvas = viewObject.AddComponent<Canvas>();
            // Renderização isolada: não depende da Game View nem de um frame do Editor.
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = testCamera;
            canvas.planeDistance = 1;
            raycaster = viewObject.AddComponent<ActionsFixtureGraphicRaycaster>();
            raycaster.BeginFixture();
            Assert.That(UnityEngine.EventSystems.RaycasterManager.GetRaycasters(), Does.Contain(raycaster));
            var uiRect = WDUIFactory.Rect("UI", viewObject.transform);
            var ui = uiRect.gameObject.AddComponent<WDMatchUIV2>();
            var boundCommands = new MatchUICommands(manager);
            ui.Build(theme, boundCommands, controller, null);
            var view = uiRect.Find("MatchActions").GetComponent<WDMatchActionsView>();
            view.Bind(boundCommands, controller);
            presenter.SolicitarSnapshotAtual();
            view.Present(presenter.EstadoAtual);
            uiRect.Find("LeftCommandRail/Actions").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(view.IsOpen, Is.True, "AÇÕES do HUD abre o drawer.");
            RenderActionsRaycastFixture(viewObject, testCamera, target, view);
            var outside = view.transform.Find("ActionsOutside").gameObject;
            var drawerPanel = view.transform.Find("ActionsDrawer").gameObject;
            foreach (int pointerId in new[] { -1, 1 }) // mouse e touch pelo mesmo contrato UI
            {
                RenderActionsRaycastFixture(viewObject, testCamera, target, view);
                Assert.That(outside.GetComponent<UnityEngine.UI.Image>().depth, Is.GreaterThanOrEqualTo(0),
                    "Pré-condição: o GraphicRaycaster exige um Canvas processado pela renderização.");
                var pointer = new UnityEngine.EventSystems.PointerEventData(events.GetComponent<UnityEngine.EventSystems.EventSystem>())
                    { pointerId = pointerId, button = UnityEngine.EventSystems.PointerEventData.InputButton.Left,
                      position = new Vector2(target.width - 2, target.height / 2) };
                Assert.That(WDMatchActionsView.ConsumesMapPointer(pointer.position), Is.True,
                    "Raycast atual bloqueia mapa antes do cache do InputModule.");
                var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer, hits);
                Assert.That(hits, Is.Not.Empty, "O Canvas renderizado deve produzir hits reais.");
                Assert.That(hits[0].gameObject, Is.EqualTo(outside), "HUD subjacente não recebe o toque.");
                UnityEngine.EventSystems.ExecuteEvents.Execute(drawerPanel, pointer,
                    UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
                Assert.That(view.IsOpen, Is.True, "Clique interno não fecha.");
                UnityEngine.EventSystems.ExecuteEvents.Execute(outside, pointer,
                    UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
                Assert.That(view.IsOpen, Is.False);
                Assert.That(outside.activeSelf, Is.False);
                Assert.That(WDMatchActionsView.ConsumesMapPointer(pointer.position), Is.True,
                    "O frame de fechamento não pode programar/selecionar no mapa.");
                uiRect.Find("LeftCommandRail/Actions").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            }
            string before = state.Fingerprint();
            int energy = runtime.FindPlayer("p1").Energy;
            Assert.That(controller.SelectWeapon(infiltrator ? WDSpecialWeaponIds.Infiltrator : WDStrategicWeaponIds.Sabotage), Is.True);
            view.Refresh();
            Transform popup = view.transform.Find("WeaponTargetPopup");
            Assert.That(popup.gameObject.activeSelf, Is.True);
            Transform rows = popup.Find("Panel/OpponentsViewport/Opponents");
            Assert.That(rows.childCount, Is.EqualTo(1), "Apenas p2 é adversário; p3 é aliado.");
            if (infiltrator)
                rows.Find("Opponent-p2/ArsenalViewport/Arsenal/Weapon-" + WDStrategicWeaponIds.Sabotage)
                    .GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            else
                rows.Find("Opponent-p2").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            WDWeaponAction action = controller.GetProgrammedActions().Single();
            Assert.That(action.TargetPlayerId, Is.EqualTo("p2"));
            if (infiltrator)
                Assert.That(action.Parameters.Single(p => p.Key == "targetWeaponId").Value,
                    Is.EqualTo(WDStrategicWeaponIds.Sabotage));
            Assert.That(popup.gameObject.activeSelf, Is.False);
            Assert.That(state.Fingerprint(), Is.EqualTo(before));
            Assert.That(runtime.FindPlayer("p1").Energy, Is.EqualTo(energy), "A UI não executa nem cobra a Resolution.");
            Assert.That(controller.SelectWeapon(WDSupportWeaponIds.Minefield), Is.True);
            Assert.That(controller.HandleTerritory("p1_a"), Is.True);
            string otherId = controller.GetProgrammedActions().Single(x => x.ActionId != action.ActionId).ActionId;
            if (freeze) service.FreezeRound();
            view.Refresh();
            Transform drawerRows = view.transform.Find("ActionsDrawer/ListViewport/List");
            var cancel = drawerRows.Find("Weapon-" + action.ActionId + "/Cancel").GetComponent<UnityEngine.UI.Button>();
            Assert.That(cancel.interactable, Is.EqualTo(!freeze));
            var hit = (RectTransform)cancel.transform;
            Assert.That(hit.sizeDelta, Is.EqualTo(new Vector2(44, 44)));
            Assert.That(hit.anchoredPosition, Is.EqualTo(new Vector2(304, -10)));
            Assert.That(hit.anchoredPosition.y - hit.sizeDelta.y, Is.GreaterThanOrEqualTo(-64));
            var visual = (RectTransform)hit.Find("Visual");
            Assert.That(visual.sizeDelta, Is.EqualTo(new Vector2(24, 24)));
            Assert.That(hit.anchoredPosition + visual.anchoredPosition, Is.EqualTo(new Vector2(324, -20)));
            Color red = new Color32(217, 90, 90, 255);
            Assert.That(visual.Find("Label").GetComponent<TMPro.TMP_Text>().color, Is.EqualTo(red));
            Assert.That(visual.Find("Border").GetComponent<UnityEngine.UI.Image>().color, Is.EqualTo(red));
            RenderActionsRaycastFixture(viewObject, testCamera, target, view);
            var firstPointer = new UnityEngine.EventSystems.PointerEventData(events.GetComponent<UnityEngine.EventSystems.EventSystem>())
            {
                pointerId = infiltrator ? 1 : -1,
                button = UnityEngine.EventSystems.PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(testCamera, hit.TransformPoint(new Vector3(2, -22, 0)))
            };
            var cancelHits = new List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(firstPointer, cancelHits);
            Assert.That(cancelHits, Is.Not.Empty, "O X renderizado deve participar do raycast real.");
            Assert.That(cancelHits[0].gameObject, Is.EqualTo(cancel.gameObject), "A expansão local pertence ao X correto.");
            Assert.That(WDMatchActionsView.ConsumesMapPointer(firstPointer.position), Is.True);
            GameObject pressed = cancel.gameObject;
            UnityEngine.EventSystems.ExecuteEvents.Execute(pressed, firstPointer,
                UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);
            // Reprodução da cadeia real Configure -> BindCommands/BindWeapons -> Present
            // entre o primeiro down e up. Não houve foco/clique prévio neste X.
            ui.BindCommands(boundCommands);
            ui.BindWeapons(controller);
            presenter.SolicitarSnapshotAtual();
            view.Present(presenter.EstadoAtual);
            Assert.That(drawerRows.Find("Weapon-" + action.ActionId + "/Cancel").gameObject, Is.SameAs(pressed));
            UnityEngine.EventSystems.ExecuteEvents.Execute(pressed, firstPointer,
                UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler);
            UnityEngine.EventSystems.ExecuteEvents.Execute(pressed, firstPointer,
                UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            if (freeze) cancel.onClick.Invoke(); // Callback atrasado também deve ser recusado.
            Assert.That(controller.GetProgrammedActions().Any(x => x.ActionId == otherId), Is.True,
                "A ação da outra linha permanece programada.");
            Assert.That(view.IsOpen, Is.True, "X cancela a ação sem fechar o drawer.");
            Assert.That(controller.GetProgrammedActions().Count, Is.EqualTo(freeze ? 2 : 1));
            view.Toggle(); view.Toggle();
            view.Bind(boundCommands, controller);
            view.Refresh();
            Assert.That(drawerRows.childCount, Is.EqualTo(freeze ? 2 : 1));
            Assert.That(state.Fingerprint(), Is.EqualTo(before));
        }
        finally
        {
            inputModule.EndFixture();
            eventSystem.EndFixture();
            if (raycaster != null) raycaster.EndFixture();
            UnityEngine.Object.DestroyImmediate(viewObject);
            UnityEngine.Object.DestroyImmediate(events);
            inputActions.Disable();
            UnityEngine.Object.DestroyImmediate(point);
            UnityEngine.Object.DestroyImmediate(click);
            UnityEngine.Object.DestroyImmediate(inputActions);
            foreach (var previousEvent in previousEvents)
                if (previousEvent != null && !RegisteredActionEventSystems().Contains(previousEvent))
                    EventSystemLifecycle(previousEvent, "OnEnable");
            // Não atribuir null/current desconhecido: OnDisable remove a fixture e a
            // ordem de registro original restaura naturalmente o current anterior.
            testCamera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(cameraObject);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(managerObject);
            UnityEngine.Object.DestroyImmediate(theme);
            if (previous == null) WDMatchSetupContext.Clear(); else WDMatchSetupContext.Store(previous);
        }
    }

    private static List<UnityEngine.EventSystems.EventSystem> RegisteredActionEventSystems() =>
        (List<UnityEngine.EventSystems.EventSystem>)typeof(UnityEngine.EventSystems.EventSystem)
            .GetField("m_EventSystems", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

    private static void EventSystemLifecycle(UnityEngine.EventSystems.EventSystem system, string callback) =>
        typeof(UnityEngine.EventSystems.EventSystem).GetMethod(callback,
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(system, null);

    // Adaptadores de lifecycle somente da fixture: raycast, módulos e callbacks são
    // os originais do UGUI/Input System, sem hits ou resultados substituídos.
    private sealed class ActionsFixtureEventSystem : UnityEngine.EventSystems.EventSystem
    {
        private bool registered;
        protected override void OnEnable()
        {
            if (registered) return;
            base.OnEnable();
            registered = true;
        }
        protected override void OnDisable()
        {
            if (!registered) return;
            base.OnDisable();
            registered = false;
        }
        public void BeginFixture() => OnEnable();
        public void ActivateModule() { UpdateModules(); base.Update(); }
        public void EndFixture() => OnDisable();
    }

    private sealed class ActionsFixtureGraphicRaycaster : UnityEngine.UI.GraphicRaycaster
    {
        private bool registered;
        protected override void OnEnable()
        {
            if (registered) return;
            base.OnEnable();
            registered = true;
        }
        protected override void OnDisable()
        {
            if (!registered) return;
            base.OnDisable();
            registered = false;
        }
        public void BeginFixture() => OnEnable();
        public void EndFixture() => OnDisable();
    }

    private sealed class ActionsFixtureInputModule : UnityEngine.InputSystem.UI.InputSystemUIInputModule
    {
        private bool initialized;
        protected override void OnEnable()
        {
            if (initialized || actionsAsset == null) return;
            base.OnEnable();
            initialized = true;
        }
        protected override void OnDisable()
        {
            if (!initialized) return;
            base.OnDisable();
            initialized = false;
        }
        public void BeginFixture() => OnEnable();
        public void EndFixture() => OnDisable();
    }

    private static void RenderActionsRaycastFixture(GameObject root, Camera camera,
        RenderTexture target, WDMatchActionsView view)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = 31;
        Canvas.ForceUpdateCanvases();
        view.Refresh();
        Canvas.ForceUpdateCanvases();
        if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null)
            camera.Render();
        else
        {
            var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target };
            Assert.That(UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(camera, request), Is.True,
                "A fixture precisa renderizar seu Canvas real antes de consultar GraphicRaycaster.");
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request);
        }
    }

    [Test]
    public void RejectedWeaponTerritoryConsumesClickWithoutProgrammingOrExecuting()
    {
        WDMatchSetup previous = WDMatchSetupContext.Current;
        GameObject owner = new("WeaponClickManager");
        try
        {
            StoreActionsSetup();
            service.BeginRound(1);
            GameManager manager = ConfigureManager(owner, out _);
            var controller = owner.AddComponent<WDWeaponMatchUIController>();
            controller.Configure(manager);
            string before = state.Fingerprint();
            Assert.That(controller.SelectWeapon(WDSupportWeaponIds.Minefield), Is.True);
            Assert.That(controller.HandleTerritory("missing-territory"), Is.True);
            Assert.That(service.GetPlayerActions("p1", 1), Is.Empty);
            Assert.That(controller.Stage, Is.EqualTo(WDWeaponHudSelectionStage.TargetTerritory));
            Assert.That(state.Fingerprint(), Is.EqualTo(before));
            Assert.That(controller.HandleTerritory("p1_a"), Is.True);
            Assert.That(service.GetPlayerActions("p1", 1).Count, Is.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
            if (previous == null) WDMatchSetupContext.Clear(); else WDMatchSetupContext.Store(previous);
        }
    }

    [TestCase(1920f, false)]
    [TestCase(2400f, true)]
    public void SupportHubUsesRealSlotsAndCanonicalActionsWithoutEarlyEffects(float width, bool freeze)
    {
        WDMatchSetup previous = WDMatchSetupContext.Current;
        var managerObject = new GameObject("SupportManager");
        var root = new GameObject("SupportSafeArea", typeof(RectTransform));
        var theme = ScriptableObject.CreateInstance<WarDominionUITheme>();
        try
        {
            StoreSupportSetup();
            GameManager manager = ConfigureManager(managerObject, out _);
            var rounds = managerObject.AddComponent<GerenciadorRodada>();
            SetPrivate(rounds, "rodadaAtual", 2);
            SetPrivate(manager, "gerenciadorRodada", rounds);
            SetPrivate(manager, "weaponRuntime", runtime);
            var controller = managerObject.AddComponent<WDWeaponMatchUIController>();
            controller.Configure(manager);
            var presenter = managerObject.AddComponent<MatchUIPresenter>();
            SetPrivate(presenter, "gameManager", manager);
            ((RectTransform)root.transform).sizeDelta = new Vector2(width, 1080);
            var rect = WDUIFactory.Rect("UI", root.transform);
            var ui = rect.gameObject.AddComponent<WDMatchUIV2>();
            ui.Build(theme, new MatchUICommands(manager), controller, null);
            var support = rect.Find("MatchSupport").GetComponent<WDMatchSupportView>();
            var actions = rect.Find("MatchActions").GetComponent<WDMatchActionsView>();
            presenter.SolicitarSnapshotAtual();
            actions.Present(presenter.EstadoAtual);
            var panel = (RectTransform)support.transform.Find("SupportHub");
            Assert.That(support.IsOpen, Is.False);
            rect.Find("LeftCommandRail/Support").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(support.IsOpen, Is.True);
            Assert.That(panel.anchoredPosition, Is.EqualTo(new Vector2(290, -826)));
            Assert.That(panel.sizeDelta, Is.EqualTo(new Vector2(252, 145)));
            var rail = (RectTransform)rect.Find("LeftCommandRail");
            Assert.That(panel.anchoredPosition.x - rail.anchoredPosition.x - rail.sizeDelta.x, Is.EqualTo(5));
            Assert.That(panel.anchoredPosition.y - panel.sizeDelta.y, Is.EqualTo(-971));
            Assert.That(panel.Find("Title"), Is.Null);
            Assert.That(panel.Find("Close"), Is.Null);
            Assert.That(panel.Find("Chevron"), Is.Null);
            var corners = new Vector3[4]; panel.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector3 point = root.transform.InverseTransformPoint(corner);
                Assert.That(Mathf.Abs(point.x), Is.LessThanOrEqualTo(width / 2));
                Assert.That(Mathf.Abs(point.y), Is.LessThanOrEqualTo(540));
            }
            Assert.That(support.transform.Find("SupportOutside").gameObject.activeSelf, Is.True);
            var cards = controller.GetSupportCards();
            Assert.That(cards.Select(c => c.WeaponId), Is.EqualTo(new[] { WDSupportWeaponIds.Reinforcements, WDSupportWeaponIds.AirTransport }));
            Assert.That(cards.All(c => catalog.IsSupportEligible(catalog.FindWeapon(c.WeaponId))), Is.True);
            Assert.That(cards[0].EnergyCost, Is.EqualTo(catalog.FindWeapon(cards[0].WeaponId).EnergyCost));
            var first = panel.Find("SupportSlot1").GetComponent<UnityEngine.UI.Button>();
            var second = panel.Find("SupportSlot2").GetComponent<UnityEngine.UI.Button>();
            Assert.That(first.GetComponent<WDUIPremiumButton>(), Is.Not.Null,
                "APOIO reutiliza o mesmo componente canônico dos cards de arma.");
            Assert.That(first.transform.Find("Content").GetComponent<TMPro.TMP_Text>().text,
                Is.EqualTo(WDMatchUIV2.FormatWeaponCardLabel(
                    cards[0].DisplayName, cards[0].Level, cards[0].EnergyCost)));
            Assert.That(first.interactable && second.interactable, Is.True);
            Assert.That(((RectTransform)first.transform).sizeDelta, Is.EqualTo(new Vector2(109, 121)));
            Assert.That(panel.Find("SupportSlot3"), Is.Null);
            Assert.That(first.transform.Find("Accent"), Is.Null);
            Assert.That(((RectTransform)first.transform).anchoredPosition, Is.EqualTo(new Vector2(11, -12)));
            Assert.That(((RectTransform)second.transform).anchoredPosition, Is.EqualTo(new Vector2(132, -12)));
            first.onClick.Invoke();
            Assert.That(controller.Stage, Is.EqualTo(WDWeaponHudSelectionStage.TargetTerritory));
            support.Bind(controller); support.Refresh();
            Assert.That(panel.Find("SupportSlot1").gameObject, Is.SameAs(first.gameObject), "Rebind preserva o alvo entre down/up.");
            string before = state.Fingerprint();
            int energy = runtime.FindPlayer("p1").Energy;
            Assert.That(controller.HandleTerritory("p2_a"), Is.True, "Alvo inválido é consumido.");
            Assert.That(controller.GetProgrammedActions(), Is.Empty);
            Assert.That(controller.HandleTerritory("p3_a"), Is.True, "Aliado é alvo canônico válido.");
            var action = controller.GetProgrammedActions().Single();
            Assert.That(action.WeaponId, Is.EqualTo(WDSupportWeaponIds.Reinforcements));
            support.Refresh();
            Assert.That(first.interactable, Is.False, "Carta programada não programa novamente pelo slot.");
            Assert.That(controller.GetSupportCards()[0].Programmed, Is.True);
            Assert.That(state.Fingerprint(), Is.EqualTo(before));
            Assert.That(runtime.FindPlayer("p1").Energy, Is.EqualTo(energy));
            actions.Toggle(); actions.Refresh();
            if (freeze) service.FreezeRound();
            support.Refresh();
            Assert.That(second.interactable, Is.EqualTo(!freeze));
            actions.Refresh();
            var cancel = actions.transform.Find("ActionsDrawer/ListViewport/List/Weapon-" + action.ActionId + "/Cancel")
                .GetComponent<UnityEngine.UI.Button>();
            Assert.That(cancel.interactable, Is.EqualTo(!freeze));
            if (freeze)
                Assert.That(controller.CancelAction(action.ActionId).Reason, Is.EqualTo(WDWeaponProgrammingReason.RoundFrozen));
            else cancel.onClick.Invoke();
            Assert.That(controller.GetProgrammedActions().Count, Is.EqualTo(freeze ? 1 : 0));
            support.transform.Find("SupportOutside").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(support.IsOpen, Is.False);
            Assert.That(WDMatchSupportView.ConsumesMapPointer(Vector2.zero), Is.True, "Fechamento consumido também após ocultar o Graphic.");
            support.Toggle(); support.Bind(controller); support.Refresh();
            Assert.That(support.IsOpen, Is.True);
            Assert.That(panel.Find("SupportSlot1").gameObject, Is.SameAs(first.gameObject));
            support.Close();
            // Sem loadout não fabricar uma carta; slots físicos permanecem vazios.
            WDMatchSetupContext.Clear();
            support.Refresh();
            Assert.That(first.interactable || second.interactable, Is.False);
            Assert.That(first.transform.Find("Content").GetComponent<TMPro.TMP_Text>().text, Is.Empty);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(managerObject);
            UnityEngine.Object.DestroyImmediate(theme);
            if (previous == null) WDMatchSetupContext.Clear(); else WDMatchSetupContext.Store(previous);
        }
    }

    [TestCase(-1)]
    [TestCase(1)]
    public void InteractionRadialCapturesWholeGestureBeforeCacheAndReleasesNextInput(int pointerId)
    {
        var previousEvents = RegisteredActionEventSystems().ToArray();
        WDMatchSetup previousSetup = WDMatchSetupContext.Current;
        var managerObject = new GameObject("RadialManager");
        var events = new GameObject("RadialEvents"); events.SetActive(false);
        var eventSystem = events.AddComponent<ActionsFixtureEventSystem>();
        var root = new GameObject("RadialRaycast", typeof(RectTransform));
        var cameraObject = new GameObject("RadialCamera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        var target = new RenderTexture(1920, 1080, 24);
        var theme = ScriptableObject.CreateInstance<WarDominionUITheme>();
        ActionsFixtureGraphicRaycaster raycaster = null;
        WDMatchInteractionView interaction = null;
        try
        {
            StoreActionsSetup();
            var manager = ConfigureManager(managerObject, out _);
            var presenter = managerObject.AddComponent<MatchUIPresenter>(); SetPrivate(presenter, "gameManager", manager);
            presenter.SolicitarSnapshotAtual();
            foreach (var previousEvent in previousEvents) EventSystemLifecycle(previousEvent, "OnDisable");
            events.SetActive(true); eventSystem.BeginFixture();
            Assert.That(eventSystem.currentInputModule, Is.Null);
            camera.enabled = false; camera.cullingMask = 1 << 31; camera.targetTexture = target;
            var canvas = root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 1;
            raycaster = root.AddComponent<ActionsFixtureGraphicRaycaster>(); raycaster.BeginFixture();
            var rect = WDUIFactory.Rect("UI", root.transform);
            var ui = rect.gameObject.AddComponent<WDMatchUIV2>(); ui.Build(theme, null, null, null);
            ui.Present(presenter.EstadoAtual);
            var view = rect.Find("MatchInteractions").GetComponent<WDMatchInteractionView>();
            interaction = view;
            // A View comum não executa lifecycle automaticamente nesta fixture
            // EditMode. Registrar pela mesma entrada usada pelo Player, sem
            // inserir itens manualmente em Live nem fabricar resultados de raycast.
            typeof(WDMatchInteractionView).GetMethod("OnEnable",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
            var actions = rect.Find("MatchActions").GetComponent<WDMatchActionsView>();
            var entry = ui.GetComponentsInChildren<Transform>().Single(t => t.name == "AvatarSlot" && t.parent.name == "Player-Jogador1");
            var underlying = rect.Find("LeftCommandRail/PrimaryNavigation/Menu").GetComponent<UnityEngine.UI.Button>();
            int underlyingClicks = 0; underlying.onClick.AddListener(() => underlyingClicks++);
            // Build mede a lista ainda vazia. Reproduzir o LateUpdate do contraste
            // após Present antes de renderizar: sem isso a altura zero comprime
            // as linhas e sobrepõe os AvatarSlot nesta fixture síncrona EditMode.
            var players = (RectTransform)rect.Find("Players");
            rect.Find("PlayersContrast").GetComponent<WDMatchPlayersContrast>().Refresh();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(players);
            var avatars = players.GetComponentsInChildren<UnityEngine.UI.Button>()
                .Where(button => button.name == "AvatarSlot").ToArray();
            Assert.That(avatars.Length, Is.EqualTo(3));
            Assert.That(players.rect.height, Is.EqualTo(3 * 54f + 2 * 22f));
            RenderActionsRaycastFixture(root, camera, target, actions);
            Vector2 entryPoint = RectTransformUtility.WorldToScreenPoint(camera, entry.TransformPoint(((RectTransform)entry).rect.center));
            Assert.That(view.isActiveAndEnabled && canvas.isActiveAndEnabled && raycaster.isActiveAndEnabled, Is.True);
            Assert.That(UnityEngine.EventSystems.EventSystem.current, Is.SameAs(eventSystem));
            Assert.That(entry.gameObject.activeInHierarchy && entry.GetComponent<UnityEngine.UI.Image>().raycastTarget, Is.True);
            Assert.That(view.IsOpen, Is.False, "A entrada deve ser reconhecida antes de abrir a superfície de captura.");
            var entryHits = new List<UnityEngine.EventSystems.RaycastResult>();
            eventSystem.RaycastAll(new UnityEngine.EventSystems.PointerEventData(eventSystem)
                { position = entryPoint, pointerId = pointerId }, entryHits);
            Assert.That(entryHits, Is.Not.Empty, "Canvas renderizado deve atingir a entrada ativa.");
            foreach (var avatar in avatars)
            {
                var avatarRect = (RectTransform)avatar.transform;
                var position = RectTransformUtility.WorldToScreenPoint(camera, avatarRect.TransformPoint(avatarRect.rect.center));
                var avatarHits = new List<UnityEngine.EventSystems.RaycastResult>();
                eventSystem.RaycastAll(new UnityEngine.EventSystems.PointerEventData(eventSystem)
                    { position = position, pointerId = pointerId }, avatarHits);
                Assert.That(avatarHits, Is.Not.Empty);
                var actual = UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(avatarHits[0].gameObject);
                Assert.That(actual, Is.SameAs(avatar.gameObject),
                    $"Esperado RadialRaycast/UI/Players/{avatar.transform.parent.name}/AvatarSlot #{avatar.gameObject.GetEntityId()}; " +
                    $"receiver real {(actual == null ? "null" : actual.transform.parent.name + "/" + actual.name + " #" + actual.GetEntityId())}. " +
                    "Cada centro deve encaminhar ao seu próprio jogador, sem sobreposição.");
                Assert.That(avatarHits[0].module, Is.SameAs(raycaster));
            }
            var frameQuery = typeof(WDMatchInteractionView).GetMethod("ConsumesMapPointerAtFrame", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That((bool)frameQuery.Invoke(null, new object[] { entryPoint, Time.frameCount + 1 }), Is.True,
                "Raycast atual da entrada protege o primeiro frame sem cache/latch anterior.");
            entry.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(view.IsOpen, Is.True);
            RenderActionsRaycastFixture(root, camera, target, actions);
            var center = view.transform.Find("EmojiRadial/AvatarHit");
            var pointer = new UnityEngine.EventSystems.PointerEventData(eventSystem)
            { pointerId = pointerId, button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
            UnityEngine.EventSystems.ExecuteEvents.Execute(center.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            Assert.That(view.IsOpen, Is.True, "Toque interno não fecha.");
            var underlyingRect = (RectTransform)underlying.transform;
            pointer.position = RectTransformUtility.WorldToScreenPoint(camera, underlyingRect.TransformPoint(underlyingRect.rect.center));
            Assert.That(WDMatchInteractionView.ConsumesMapPointer(pointer.position), Is.True);
            var hits = new List<UnityEngine.EventSystems.RaycastResult>(); eventSystem.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            var receiver = UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(hits[0].gameObject);
            Assert.That(receiver, Is.EqualTo(view.transform.Find("InteractionOutside").gameObject));
            UnityEngine.EventSystems.ExecuteEvents.Execute(receiver, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);
            Assert.That(view.IsOpen, Is.True);
            UnityEngine.EventSystems.ExecuteEvents.Execute(receiver, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler);
            UnityEngine.EventSystems.ExecuteEvents.Execute(receiver, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            Assert.That(view.IsOpen, Is.False); Assert.That(underlyingClicks, Is.Zero);
            Assert.That(WDMatchInteractionView.ConsumesMapPointer(pointer.position), Is.True, "Fechamento consome o mesmo frame.");
            RenderActionsRaycastFixture(root, camera, target, actions);
            Assert.That((bool)frameQuery.Invoke(null, new object[] { pointer.position, Time.frameCount + 1 }), Is.False);
            hits.Clear(); eventSystem.RaycastAll(pointer, hits); Assert.That(hits, Is.Not.Empty);
            receiver = UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(hits[0].gameObject);
            Assert.That(receiver, Is.EqualTo(underlying.gameObject));
            UnityEngine.EventSystems.ExecuteEvents.Execute(receiver, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            Assert.That(underlyingClicks, Is.EqualTo(1));
        }
        finally
        {
            if (interaction != null)
                typeof(WDMatchInteractionView).GetMethod("OnDisable",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(interaction, null);
            eventSystem.EndFixture(); if (raycaster != null) raycaster.EndFixture();
            UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(events);
            foreach (var old in previousEvents)
                if (old != null && !RegisteredActionEventSystems().Contains(old)) EventSystemLifecycle(old, "OnEnable");
            camera.targetTexture = null; UnityEngine.Object.DestroyImmediate(cameraObject);
            target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(theme);
            UnityEngine.Object.DestroyImmediate(managerObject);
            if (previousSetup == null) WDMatchSetupContext.Clear(); else WDMatchSetupContext.Store(previousSetup);
        }
    }

    [TestCase(-1)]
    [TestCase(1)]
    public void SupportFreshRaycastBlocksEntryAndHubWithoutAnInputModuleCache(int pointerId)
    {
        var previousEvents = RegisteredActionEventSystems().ToArray();
        var events = new GameObject("SupportEvents");
        events.SetActive(false);
        var eventSystem = events.AddComponent<ActionsFixtureEventSystem>();
        var root = new GameObject("SupportRaycast", typeof(RectTransform));
        var cameraObject = new GameObject("SupportRaycastCamera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        var target = new RenderTexture(1920, 1080, 24);
        var theme = ScriptableObject.CreateInstance<WarDominionUITheme>();
        ActionsFixtureGraphicRaycaster raycaster = null;
        try
        {
            foreach (var previousEvent in previousEvents) EventSystemLifecycle(previousEvent, "OnDisable");
            events.SetActive(true); eventSystem.BeginFixture();
            Assert.That(UnityEngine.EventSystems.EventSystem.current, Is.SameAs(eventSystem));
            Assert.That(eventSystem.currentInputModule, Is.Null, "A proteção não depende do cache de um InputModule.");
            camera.enabled = false; camera.cullingMask = 1 << 31; camera.targetTexture = target;
            var canvas = root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 1;
            raycaster = root.AddComponent<ActionsFixtureGraphicRaycaster>(); raycaster.BeginFixture();
            var rect = WDUIFactory.Rect("UI", root.transform);
            var ui = rect.gameObject.AddComponent<WDMatchUIV2>(); ui.Build(theme, null, null, null);
            var support = rect.Find("MatchSupport").GetComponent<WDMatchSupportView>();
            var actions = rect.Find("MatchActions").GetComponent<WDMatchActionsView>();
            var panel = (RectTransform)support.transform.Find("SupportHub");
            var entry = (RectTransform)rect.Find("LeftCommandRail/Support");
            RenderActionsRaycastFixture(root, camera, target, actions);
            // Build mantém SendMoves inativo até receber um snapshot de partida.
            // Esta fixture de input não apresenta snapshot: usar um controle já ativo.
            var underlying = rect.Find("LeftCommandRail/Transfer").GetComponent<UnityEngine.UI.Button>();
            Assert.That(underlying.gameObject.activeInHierarchy && underlying.interactable, Is.True);
            int underlyingClicks = 0;
            underlying.onClick.AddListener(() => underlyingClicks++);
            var underlyingRect = (RectTransform)underlying.transform;
            Vector2 outside = RectTransformUtility.WorldToScreenPoint(camera, underlyingRect.TransformPoint(underlyingRect.rect.center));
            var baselineHits = new List<UnityEngine.EventSystems.RaycastResult>();
            eventSystem.RaycastAll(new UnityEngine.EventSystems.PointerEventData(eventSystem)
                { pointerId = pointerId, position = outside }, baselineHits);
            Assert.That(baselineHits, Is.Not.Empty, "Pré-condição: há um controle renderizado sob a futura captura.");
            Assert.That(UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(baselineHits[0].gameObject),
                Is.EqualTo(underlying.gameObject));
            Assert.That(WDMatchSupportView.ConsumesMapPointer(outside), Is.False);
            Assert.That(WDMatchSupportView.ConsumesMapPointer(RectTransformUtility.WorldToScreenPoint(camera, entry.TransformPoint(entry.rect.center))), Is.True);
            // Apenas prepara a superfície aberta. Nenhum callback de consumo foi chamado:
            // assim a assertion abaixo não pode passar pelo latch do frame de fechamento.
            panel.gameObject.SetActive(true); support.Refresh();
            RenderActionsRaycastFixture(root, camera, target, actions);
            Assert.That(panel.GetComponent<UnityEngine.UI.Image>().depth, Is.GreaterThanOrEqualTo(0));
            var pointer = new UnityEngine.EventSystems.PointerEventData(eventSystem)
            {
                pointerId = pointerId, button = UnityEngine.EventSystems.PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(camera, panel.TransformPoint(new Vector3(5, -5, 0)))
            };
            Assert.That(WDMatchSupportView.ConsumesMapPointer(pointer.position), Is.True);
            Assert.That(WDMatchSupportView.ConsumesMapPointer(outside), Is.True, "A captura externa bloqueia o mapa antes do cache.");
            UnityEngine.EventSystems.ExecuteEvents.Execute(panel.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            Assert.That(support.IsOpen, Is.True);
            pointer.position = outside;
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            eventSystem.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject.name, Is.EqualTo("SupportOutside"));
            UnityEngine.EventSystems.ExecuteEvents.Execute(hits[0].gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);
            Assert.That(support.IsOpen, Is.True, "Captura dura até o release.");
            UnityEngine.EventSystems.ExecuteEvents.Execute(hits[0].gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler);
            UnityEngine.EventSystems.ExecuteEvents.Execute(hits[0].gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            Assert.That(underlyingClicks, Is.EqualTo(0));
            Assert.That(support.IsOpen, Is.False);
            Assert.That(WDMatchSupportView.ConsumesMapPointer(pointer.position), Is.True, "Mesmo frame continua consumido após ocultar a UI.");
            // EditMode síncrono não é relógio de gameplay. Avaliar a mesma função do
            // runtime no frame seguinte sem apagar/modificar o latch de consumo.
            var query = typeof(WDMatchSupportView).GetMethod("ConsumesMapPointerAtFrame", BindingFlags.Static | BindingFlags.NonPublic);
            RenderActionsRaycastFixture(root, camera, target, actions);
            Assert.That((bool)query.Invoke(null, new object[] { outside, Time.frameCount + 1 }), Is.False);
            hits.Clear(); eventSystem.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, "Após fechar, o mesmo controle deve voltar ao raycast.");
            var receiver = UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(hits[0].gameObject);
            Assert.That(receiver, Is.EqualTo(underlying.gameObject), "O primeiro hit deve encaminhar ao controle esperado, sem bloqueador residual.");
            UnityEngine.EventSystems.ExecuteEvents.Execute(receiver, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            Assert.That(underlyingClicks, Is.EqualTo(1));
        }
        finally
        {
            eventSystem.EndFixture();
            if (raycaster != null) raycaster.EndFixture();
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(events);
            foreach (var previousEvent in previousEvents)
                if (previousEvent != null && !RegisteredActionEventSystems().Contains(previousEvent)) EventSystemLifecycle(previousEvent, "OnEnable");
            camera.targetTexture = null; UnityEngine.Object.DestroyImmediate(cameraObject);
            target.Release(); UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void SupportTransportModalCollectsTroopsWithoutExecutingOrInventingInstallationSelection()
    {
        WDMatchSetup previous = WDMatchSetupContext.Current;
        FieldInfo activeMapField = typeof(MapaAtivo).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object previousMap = activeMapField.GetValue(null);
        var managerObject = new GameObject("SupportTransportManager");
        var originObject = new GameObject("SupportTransportOrigin");
        var root = new GameObject("SupportTransportUI", typeof(RectTransform));
        var theme = ScriptableObject.CreateInstance<WarDominionUITheme>();
        try
        {
            activeMapField.SetValue(null, null); // Isola a consulta territorial da cena aberta no Editor.
            StoreSupportSetup();
            CreateOwnedTerritory(originObject, "p1_a", 5);
            var manager = ConfigureManager(managerObject, out _);
            var rounds = managerObject.AddComponent<GerenciadorRodada>();
            SetPrivate(rounds, "rodadaAtual", 2); SetPrivate(manager, "gerenciadorRodada", rounds);
            SetPrivate(manager, "weaponRuntime", runtime);
            var controller = managerObject.AddComponent<WDWeaponMatchUIController>(); controller.Configure(manager);
            var view = root.AddComponent<WDMatchSupportView>(); view.Build(theme); view.Bind(controller);
            view.Toggle();
            root.transform.Find("SupportHub/SupportSlot2").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(controller.Stage, Is.EqualTo(WDWeaponHudSelectionStage.Origin));
            controller.HandleTerritory("p1_a"); controller.HandleTerritory("p3_a"); view.Refresh();
            var modal = root.transform.Find("SupportTransport");
            Assert.That(modal.gameObject.activeSelf, Is.True);
            Assert.That(((RectTransform)modal.Find("Panel")).sizeDelta, Is.EqualTo(new Vector2(760, 726)));
            Assert.That(controller.MaximumTransportTroops, Is.EqualTo(4), "Deixa uma tropa na origem real.");
            modal.Find("Panel/Troops/Plus").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(controller.TransportTroops, Is.EqualTo(2));
            Assert.That(controller.GetProgrammedActions(), Is.Empty);
            Assert.That(modal.Find("Panel/Installation/InstallationSelectionPending").GetComponent<UnityEngine.UI.Button>(), Is.Null);
            int energy = runtime.FindPlayer("p1").Energy;
            string before = state.Fingerprint();
            modal.Find("Panel/Confirm").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            var action = controller.GetProgrammedActions().Single();
            Assert.That(action.WeaponId, Is.EqualTo(WDSupportWeaponIds.AirTransport));
            Assert.That(action.Parameters.Single(p => p.Key == "troops").Value, Is.EqualTo("2"));
            Assert.That(action.InstallationId, Is.Empty);
            Assert.That(modal.gameObject.activeSelf, Is.False);
            Assert.That(state.Fingerprint(), Is.EqualTo(before));
            Assert.That(runtime.FindPlayer("p1").Energy, Is.EqualTo(energy));
        }
        finally
        {
            activeMapField.SetValue(null, previousMap);
            UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(originObject);
            UnityEngine.Object.DestroyImmediate(managerObject); UnityEngine.Object.DestroyImmediate(theme);
            if (previous == null) WDMatchSetupContext.Clear(); else WDMatchSetupContext.Store(previous);
        }
    }

    private void StoreSupportSetup()
    {
        var slots = new[] { WDSupportWeaponIds.Reinforcements, WDSupportWeaponIds.AirTransport }
            .Select((id, index) => new WDMatchWeaponSlotSnapshot(id, catalog.FindWeapon(id).MaximumLevel, WDMatchWeaponSlotKind.Support, index)).ToArray();
        var snapshot = new WDMatchWeaponLoadoutSnapshot("support-test", WDMatchWeaponSnapshotStatus.Valid,
            Array.Empty<WDMatchWeaponSlotSnapshot>(), slots, Array.Empty<WDMatchWeaponSlotSnapshot>(),
            slots.Select(slot => new KeyValuePair<string, int>(slot.WeaponId, slot.Level)));
        WDMatchSetupContext.Store(new WDMatchSetup("teams", "", "test_map", "", "", 10, false, 42, new[]
        {
            new WDMatchParticipant("p1", "LOCAL", 0, 0, Color.red, "", "support-test", WDMatchParticipantKind.Local, snapshot),
            new WDMatchParticipant("p2", "OPPONENT", 1, 1, Color.blue, "", "", WDMatchParticipantKind.Remote),
            new WDMatchParticipant("p3", "ALLY", 0, 2, Color.green, "", "", WDMatchParticipantKind.Remote)
        }));
    }

    private static void StoreActionsSetup() => WDMatchSetupContext.Store(new WDMatchSetup(
        "teams", "", "test_map", "", "", 10, false, 42, new[]
        {
            new WDMatchParticipant("p1", "LOCAL", 0, 0, Color.red, "", "", WDMatchParticipantKind.Local),
            new WDMatchParticipant("p2", "OPPONENT", 1, 1, Color.blue, "", "", WDMatchParticipantKind.Remote),
            new WDMatchParticipant("p3", "ALLY", 0, 2, Color.green, "", "", WDMatchParticipantKind.Remote)
        }));
}
