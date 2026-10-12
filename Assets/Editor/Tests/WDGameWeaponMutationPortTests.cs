using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class WDGameWeaponMutationPortTests
{
    private WDArmoryCatalogConfig catalog;
    private MutableSource source;
    private WDGameWeaponContext readContext;
    private WDWeaponMatchRuntime runtime;
    private WDWeaponInstallationRegistry registry;
    private WDGameWeaponMutationPort port;

    [SetUp]
    public void SetUp()
    {
        catalog = Resources.Load<WDArmoryCatalogConfig>(
            "UI/Home/WDArmoryCatalogConfig");
        Assert.That(catalog, Is.Not.Null);
        source = MutableSource.Create();
        readContext = new WDGameWeaponContext(source);
        var loadouts = new AllWeaponsLoadout(catalog);
        runtime = new WDWeaponMatchRuntime(
            catalog, new[] { "p1", "p2", "p3" }, loadouts, loadouts);
        runtime.RefreshLoadouts();
        runtime.AdvanceCooldownsForRound(2);
        registry = new WDWeaponInstallationRegistry();
        port = new WDGameWeaponMutationPort(source, runtime, catalog, registry);
    }

    // ============================================================
    // 01. TROPA E TRANSIÇÕES TERRITORIAIS CANÔNICAS
    // ============================================================

    [Test]
    public void AddsAndRemovesTroopsOnOccupiedTerritory()
    {
        Assert.That(port.AddTroops("add-1", "owned", 3).Succeeded, Is.True);
        Assert.That(readContext.FindTerritory("owned").Troops, Is.EqualTo(8));
        Assert.That(port.RemoveTroops("remove-1", "owned", 2).Succeeded, Is.True);
        Assert.That(readContext.FindTerritory("owned").Troops, Is.EqualTo(6));
    }

    [Test]
    public void RejectsNegativeOrExcessiveTroopMutationWithoutPartialChange()
    {
        string before = source.Fingerprint();
        Assert.That(port.AddTroops("bad-add", "owned", -1).Reason,
            Is.EqualTo(WDGameWeaponMutationReason.InvalidAmount));
        Assert.That(port.RemoveTroops("bad-remove", "owned", 6).Reason,
            Is.EqualTo(WDGameWeaponMutationReason.InsufficientTroops));
        Assert.That(source.Fingerprint(), Is.EqualTo(before));
    }

    [Test]
    public void ZeroingOccupiedTerritoryMakesItEmpty()
    {
        Assert.That(port.RemoveTroops("damage-owned", "owned", 5).Succeeded, Is.True);
        AssertEmpty("owned");
    }

    [Test]
    public void ZeroingNeutralTerritoryMakesItEmpty()
    {
        Assert.That(port.RemoveTroops("damage-neutral", "neutral", 1).Succeeded, Is.True);
        AssertEmpty("neutral");
    }

    [Test]
    public void ExplicitStateTransitionsEnforceCanonicalTriplet()
    {
        Assert.That(port.MakeNeutral("neutralize", "owned").Succeeded, Is.True);
        AssertTerritory("owned", "", 1, WDWeaponTerritoryStateKind.Neutral);
        Assert.That(port.MakeEmpty("empty", "owned").Succeeded, Is.True);
        AssertEmpty("owned");
        Assert.That(port.MakeOccupied("occupy", "owned", "p2", 4).Succeeded, Is.True);
        AssertTerritory("owned", "p2", 4, WDWeaponTerritoryStateKind.Occupied);
    }

    [Test]
    public void ImpossibleOccupiedStateAndUnknownIdsAreRejected()
    {
        Assert.That(port.MakeOccupied("zero", "owned", "p2", 0).Reason,
            Is.EqualTo(WDGameWeaponMutationReason.InvalidState));
        Assert.That(port.MakeOccupied("player", "owned", "missing", 2).Reason,
            Is.EqualTo(WDGameWeaponMutationReason.PlayerNotFound));
        Assert.That(port.MakeEmpty("territory", "missing").Reason,
            Is.EqualTo(WDGameWeaponMutationReason.TerritoryNotFound));
        AssertTerritory("owned", "p1", 5, WDWeaponTerritoryStateKind.Occupied);
    }

    [Test]
    public void RealTerritoryAtomicApiRejectsImpossibleStatesBeforeMutation()
    {
        var gameObject = new GameObject("MutationTerritory");
        try
        {
            var territory = gameObject.AddComponent<TerritorioClique>();
            gameObject.AddComponent<TerritorioTropas>();
            territory.idTerritorio = "atomic";
            Assert.That(territory.TentarAplicarEstadoAutoritativo(
                TerritorioClique.Dono.Jogador1, 0,
                TerritorioClique.EstadoTerritorial.Ocupado), Is.False);
            Assert.That(territory.TentarAplicarEstadoAutoritativo(
                TerritorioClique.Dono.Jogador1, 3,
                TerritorioClique.EstadoTerritorial.Ocupado), Is.True);
            Assert.That(territory.dono, Is.EqualTo(TerritorioClique.Dono.Jogador1));
            Assert.That(territory.Tropas, Is.EqualTo(3));
            Assert.That(territory.Estado,
                Is.EqualTo(TerritorioClique.EstadoTerritorial.Ocupado));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    // ============================================================
    // 02. TRANSFERÊNCIA INTEGRAL SEM DEPENDÊNCIA DE ADJACÊNCIA
    // ============================================================

    [Test]
    public void TransferChangesOwnerAndPreservesTroopsAndTerritoryId()
    {
        WDGameWeaponMutationResult result = port.TransferTerritory(
            "transfer", "owned", "p3");

        Assert.That(result.Succeeded, Is.True);
        AssertTerritory("owned", "p3", 5, WDWeaponTerritoryStateKind.Occupied);
    }

    [Test]
    public void TransferRejectsUnknownOwnerAndImpossibleSource()
    {
        Assert.That(port.TransferTerritory("unknown", "owned", "missing").Reason,
            Is.EqualTo(WDGameWeaponMutationReason.PlayerNotFound));
        Assert.That(port.TransferTerritory("neutral-transfer", "neutral", "p2").Reason,
            Is.EqualTo(WDGameWeaponMutationReason.InvalidState));
    }

    [Test]
    public void TransferDoesNotConsultGraphAdjacency()
    {
        Assert.That(source.GetNeighbours("owned", null), Is.Empty);
        Assert.That(port.TransferTerritory("remote-transfer", "owned", "p3").Succeeded,
            Is.True);
    }

    [Test]
    public void TransferRejectsOpponentEvenWhenPlayerExists()
    {
        Assert.That(port.TransferTerritory("enemy-transfer", "owned", "p2").Reason,
            Is.EqualTo(WDGameWeaponMutationReason.InvalidOwner));
    }

    // ============================================================
    // 03. ENERGY, COOLDOWN E USOS NO RUNTIME ÚNICO
    // ============================================================

    [Test]
    public void AddsAndConsumesEnergyWithoutNegativeBalance()
    {
        Assert.That(port.AddEnergy("energy-add", "p1", 30).Succeeded, Is.True);
        Assert.That(port.ConsumeEnergy("energy-spend", "p1", 20).Succeeded, Is.True);
        Assert.That(runtime.FindPlayer("p1").Energy, Is.EqualTo(10));
        Assert.That(port.ConsumeEnergy("energy-fail", "p1", 11).Reason,
            Is.EqualTo(WDGameWeaponMutationReason.InsufficientEnergy));
        Assert.That(runtime.FindPlayer("p1").Energy, Is.EqualTo(10));
    }

    [Test]
    public void StartsCooldownAndRegistersUseOnExistingRuntimeEntry()
    {
        Assert.That(port.StartCooldown("cooldown", "p1", "artillery", 3).Succeeded,
            Is.True);
        Assert.That(port.RegisterUse("use", "p1", "artillery").Succeeded, Is.True);
        WDWeaponRuntimeEntry weapon = runtime.FindPlayer("p1").GetWeapon("artillery");
        Assert.That(weapon.CooldownRemaining, Is.EqualTo(3));
        Assert.That(weapon.Uses, Is.EqualTo(1));
    }

    [Test]
    public void RuntimeMutationsRejectUnknownPlayerWeaponAndInvalidAmount()
    {
        Assert.That(port.AddEnergy("missing-player", "missing", 2).Reason,
            Is.EqualTo(WDGameWeaponMutationReason.PlayerNotFound));
        Assert.That(port.StartCooldown("missing-weapon", "p1", "missing", 2).Reason,
            Is.EqualTo(WDGameWeaponMutationReason.WeaponNotFound));
        Assert.That(port.AddEnergy("zero-energy", "p1", 0).Reason,
            Is.EqualTo(WDGameWeaponMutationReason.InvalidAmount));
    }

    [Test]
    public void MutationIdPreventsRuntimeMutationFromApplyingTwice()
    {
        Assert.That(port.AddEnergy("same-id", "p1", 10).Succeeded, Is.True);
        Assert.That(port.AddEnergy("same-id", "p1", 10).Reason,
            Is.EqualTo(WDGameWeaponMutationReason.DuplicateMutation));
        Assert.That(runtime.FindPlayer("p1").Energy, Is.EqualTo(10));
    }

    // ============================================================
    // 04. REGISTRO AUTORITATIVO DE INSTALAÇÕES
    // ============================================================

    [Test]
    public void InstallsAndPreservesRuntimeInstallationData()
    {
        WDWeaponInstallationState installation = Installation("bunker-1", "bunker", "owned");

        Assert.That(port.Install("install", installation).Succeeded, Is.True);
        WDWeaponInstallationState stored = registry.Find("bunker-1");
        Assert.That(stored, Is.SameAs(installation));
        Assert.That(stored.BarrierRemaining, Is.EqualTo(7));
        Assert.That(stored.DurationRemaining, Is.EqualTo(4));
        Assert.That(stored.ChargesRemaining, Is.EqualTo(3));
        Assert.That(stored.PaymentsRemaining, Is.EqualTo(2));
    }

    [Test]
    public void RejectsDuplicateInstallationIdAndTypePerTerritory()
    {
        Assert.That(port.Install("first", Installation("bunker-1", "bunker", "owned"))
            .Succeeded, Is.True);
        Assert.That(port.Install("duplicate-id",
            Installation("bunker-1", "flak_tower", "owned")).Reason,
            Is.EqualTo(WDGameWeaponMutationReason.DuplicateInstallation));
        Assert.That(port.Install("duplicate-type",
            Installation("bunker-2", "bunker", "owned")).Reason,
            Is.EqualTo(WDGameWeaponMutationReason.DuplicateInstallation));
    }

    [Test]
    public void RemovesInstallationFromOnlyAuthoritativeRegistry()
    {
        port.Install("install-remove", Installation("bunker-1", "bunker", "owned"));
        Assert.That(port.RemoveInstallation("remove-install", "bunker-1").Succeeded,
            Is.True);
        Assert.That(registry.Find("bunker-1"), Is.Null);
    }

    [Test]
    public void AbandonsAndCapturesInstallationWithCanonicalPlayerId()
    {
        port.Install("install-owner", Installation("bunker-1", "bunker", "owned"));
        Assert.That(port.AbandonInstallation("abandon", "bunker-1").Succeeded, Is.True);
        Assert.That(registry.Find("bunker-1").IsAbandoned, Is.True);
        Assert.That(port.CaptureInstallation("capture", "bunker-1", "p2").Succeeded,
            Is.True);
        Assert.That(registry.Find("bunker-1").OwnerPlayerId, Is.EqualTo("p2"));
    }

    [Test]
    public void MovesInstallationAndKeepsTerritoryIndexesIsolated()
    {
        port.Install("install-move", Installation("flak-1", "flak_tower", "owned"));
        Assert.That(port.MoveInstallation("move", "flak-1", "enemy").Succeeded,
            Is.True);
        Assert.That(registry.GetByTerritory("owned"), Is.Empty);
        Assert.That(registry.GetByTerritory("enemy").Single().InstallationId,
            Is.EqualTo("flak-1"));
    }

    [TestCase(WDSupportWeaponIds.Minefield)]
    [TestCase(WDSupportWeaponIds.Bunker)]
    public void AirTransportRejectsNonTransportableInstallationTypes(string weaponId)
    {
        WDWeaponInstallationState item = Installation("blocked-" + weaponId,
            weaponId, "owned");
        Assert.That(port.Install("install-blocked-" + weaponId, item).Succeeded, Is.True);
        Assert.That(port.MoveInstallation("move-blocked-" + weaponId,
            item.InstallationId, "enemy").Reason,
            Is.EqualTo(WDGameWeaponMutationReason.InvalidInstallation));
    }

    [Test]
    public void DroneStatePreservesSequentialChargesAndLastOperationRound()
    {
        WDWeaponInstallationState drone = new("drone-1", WDSpecialWeaponIds.Drone,
            "owned", "p1", 0, 0, 2, 0, 5, true, 3);

        Assert.That(drone.InitialCharges, Is.EqualTo(2));
        Assert.That(drone.NextChargeSequenceNumber, Is.EqualTo(1));
        Assert.That(drone.TryMarkAutomaticRound(3), Is.True);
        drone.ConsumeCharge();

        Assert.That(drone.ChargesRemaining, Is.EqualTo(1));
        Assert.That(drone.NextChargeSequenceNumber, Is.EqualTo(2));
        Assert.That(drone.LastAutomaticRound, Is.EqualTo(3));
    }

    [Test]
    public void EmptyLifecycleDestroysDefencesAndAbandonsPersistentInstallations()
    {
        string[] destroyed = { WDSupportWeaponIds.Bunker, WDSupportWeaponIds.FlakTower };
        string[] abandoned = { WDStrategicWeaponIds.EnergyPlant, WDStrategicWeaponIds.Artillery,
            WDStrategicWeaponIds.Rockets, WDSpecialWeaponIds.Drone };
        foreach (string id in destroyed.Concat(abandoned))
            Assert.That(port.Install("install-" + id,
                Installation("i-" + id, id, "owned")).Succeeded, Is.True, id);
        WDWeaponInstallationState mine = Installation(
            "i-mine", WDSupportWeaponIds.Minefield, "owned");
        Assert.That(port.Install("install-mine-empty", mine).Succeeded, Is.True);

        Assert.That(port.MakeEmpty("empty-lifecycle", "owned").Succeeded, Is.True);

        Assert.That(registry.Find(mine.InstallationId), Is.SameAs(mine));
        Assert.That(mine.OwnerPlayerId, Is.EqualTo("p1"));
        Assert.That(mine.OriginalInstallerPlayerId, Is.EqualTo("p1"));
        foreach (string id in destroyed)
            Assert.That(registry.Find("i-" + id), Is.Null, id);
        foreach (string id in abandoned)
            Assert.That(registry.Find("i-" + id).IsAbandoned, Is.True, id);
    }

    [Test]
    public void NeutralLifecyclePreservesMineAndAbandonsEveryOtherInstallation()
    {
        string[] abandoned = { WDSupportWeaponIds.Bunker, WDSupportWeaponIds.FlakTower,
            WDStrategicWeaponIds.EnergyPlant, WDStrategicWeaponIds.Artillery,
            WDStrategicWeaponIds.Rockets, WDSpecialWeaponIds.Drone };
        Assert.That(port.Install("install-mine", Installation("i-mine",
            WDSupportWeaponIds.Minefield, "owned")).Succeeded, Is.True);
        foreach (string id in abandoned)
            Assert.That(port.Install("install-neutral-" + id,
                Installation("n-" + id, id, "owned")).Succeeded, Is.True, id);

        Assert.That(port.MakeNeutral("neutral-lifecycle", "owned").Succeeded, Is.True);

        Assert.That(registry.Find("i-mine").OwnerPlayerId, Is.EqualTo("p1"));
        foreach (string id in abandoned)
            Assert.That(registry.Find("n-" + id).IsAbandoned, Is.True, id);

        Assert.That(port.MakeOccupied("occupy-after-neutral", "owned", "p2", 2)
            .Succeeded, Is.True);
        Assert.That(registry.Find("i-mine").OwnerPlayerId, Is.EqualTo("p1"));
        foreach (string id in abandoned)
            Assert.That(registry.Find("n-" + id).OwnerPlayerId, Is.EqualTo("p2"), id);
    }

    [TestCase(WDSupportWeaponIds.FlakTower)]
    [TestCase(WDStrategicWeaponIds.EnergyPlant)]
    [TestCase(WDStrategicWeaponIds.Artillery)]
    [TestCase(WDStrategicWeaponIds.Rockets)]
    [TestCase(WDSpecialWeaponIds.Drone)]
    public void AlliedTransportTransfersInstallationWithoutResettingState(string weaponId)
    {
        WDWeaponInstallationState installation = Installation(
            "move-" + weaponId, weaponId, "owned");
        Assert.That(port.Install("install-move-" + weaponId, installation).Succeeded,
            Is.True);
        installation.ConsumeDuration();
        installation.ApplyBarrierDamage(2);
        installation.ConsumeCharge();
        installation.ConsumePayment();
        Assert.That(installation.TryMarkAutomaticRound(4), Is.True);

        Assert.That(port.MoveInstallation("move-to-ally-" + weaponId,
            installation.InstallationId,
            "ally", "p3").Succeeded, Is.True);

        Assert.That(registry.Find(installation.InstallationId), Is.SameAs(installation));
        AssertInstallationState(installation, weaponId, "ally", "p3",
            duration: 3, barrier: 5, charges: 2, payments: 1,
            level: 4, installedRound: 2, lastAutomaticRound: 4);
    }

    [TestCase(WDSupportWeaponIds.FlakTower)]
    [TestCase(WDStrategicWeaponIds.EnergyPlant)]
    [TestCase(WDStrategicWeaponIds.Artillery)]
    [TestCase(WDStrategicWeaponIds.Rockets)]
    [TestCase(WDSpecialWeaponIds.Drone)]
    public void CapturePreservesInstallationStateWithoutWeaponRuntimeCost(string weaponId)
    {
        WDWeaponInstallationState installation = Installation(
            "capture-" + weaponId, weaponId, "owned");
        Assert.That(port.Install("install-capture-" + weaponId, installation).Succeeded,
            Is.True);
        installation.ConsumeDuration();
        installation.ApplyBarrierDamage(2);
        installation.ConsumeCharge();
        installation.ConsumePayment();
        Assert.That(installation.TryMarkAutomaticRound(4), Is.True);
        Assert.That(port.AddEnergy("energy-before-capture-" + weaponId, "p1", 50).Succeeded,
            Is.True);
        Assert.That(port.StartCooldown("cooldown-before-capture-" + weaponId,
            "p1", weaponId, 2).Succeeded, Is.True);
        Assert.That(port.RegisterUse("use-before-capture-" + weaponId,
            "p1", weaponId).Succeeded, Is.True);

        Assert.That(port.MakeNeutral("neutral-before-capture-" + weaponId, "owned")
            .Succeeded, Is.True);
        Assert.That(port.MakeOccupied("capture-territory-" + weaponId,
            "owned", "p2", 2).Succeeded, Is.True);

        Assert.That(registry.Find(installation.InstallationId), Is.SameAs(installation));
        AssertInstallationState(installation, weaponId, "owned", "p2",
            duration: 3, barrier: 5, charges: 2, payments: 1,
            level: 4, installedRound: 2, lastAutomaticRound: 4);
        WDWeaponPlayerRuntimeState player = runtime.FindPlayer("p1");
        Assert.That(player.Energy, Is.EqualTo(50));
        Assert.That(player.GetWeapon(weaponId).CooldownRemaining, Is.EqualTo(2));
        Assert.That(player.GetWeapon(weaponId).Uses, Is.EqualTo(1));
    }

    [Test]
    public void RadarCannotEnterTerritorialInstallationRegistry()
    {
        WDWeaponInstallationState radar = Installation(
            "invalid-radar", WDTacticalWeaponIds.Radar, "owned");

        Assert.That(port.Install("reject-radar", radar).Reason,
            Is.EqualTo(WDGameWeaponMutationReason.InvalidInstallation));
        Assert.That(registry.Find(radar.InstallationId), Is.Null);
        Assert.That(WDWeaponInstallationRegistry.IsAirTransportAllowed(
            WDTacticalWeaponIds.Radar), Is.False);
    }

    [Test]
    public void InstallationQueriesAreDeterministicAndDoNotMutateRegistry()
    {
        port.Install("install-z", Installation("z", "bunker", "owned"));
        port.Install("install-a", Installation("a", "flak_tower", "owned"));

        string[] first = registry.GetAll().Select(item => item.InstallationId).ToArray();
        string[] second = registry.GetAll().Select(item => item.InstallationId).ToArray();

        Assert.That(first, Is.EqualTo(new[] { "z", "a" }));
        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void InvalidInstallationDoesNotLeavePartialRegistryState()
    {
        WDWeaponInstallationState invalid = Installation(
            "invalid", "bunker", "enemy", "p1");
        Assert.That(port.Install("bad-install", invalid).Reason,
            Is.EqualTo(WDGameWeaponMutationReason.InvalidOwner));
        Assert.That(registry.GetAll(), Is.Empty);
    }

    // ============================================================
    // 05. LEITURA PÓS-MUTAÇÃO E HELPERS
    // ============================================================

    [Test]
    public void ReadOnlyContextReflectsMutationWithoutRefreshOrCopy()
    {
        Assert.That(readContext.FindTerritory("owned").OwnerPlayerId, Is.EqualTo("p1"));
        WDGameWeaponMutationResult result = port.TransferTerritory(
            "read-after", "owned", "p3");
        Assert.That(result.Succeeded, Is.True);
        Assert.That(readContext.FindTerritory("owned").OwnerPlayerId, Is.EqualTo("p3"));
    }

    private void AssertEmpty(string territoryId) => AssertTerritory(
        territoryId, "", 0, WDWeaponTerritoryStateKind.Empty);

    private void AssertTerritory(
        string id, string owner, int troops, WDWeaponTerritoryStateKind state)
    {
        WDGameWeaponTerritoryView territory = readContext.FindTerritory(id);
        Assert.That(territory.OwnerPlayerId, Is.EqualTo(owner));
        Assert.That(territory.Troops, Is.EqualTo(troops));
        Assert.That(territory.State, Is.EqualTo(state));
    }

    private static WDWeaponInstallationState Installation(
        string installationId, string weaponId, string territoryId,
        string owner = "p1") =>
        new(installationId, weaponId, territoryId, owner,
            duration: 4, barrier: 7, charges: 3, payments: 2,
            level: 4, isTransportable: true, installedRound: 2);

    private static void AssertInstallationState(
        WDWeaponInstallationState installation, string weaponId,
        string territoryId, string ownerPlayerId, int duration, int barrier,
        int charges, int payments, int level, int installedRound,
        int lastAutomaticRound)
    {
        Assert.That(installation.WeaponId, Is.EqualTo(weaponId));
        Assert.That(installation.TerritoryId, Is.EqualTo(territoryId));
        Assert.That(installation.OwnerPlayerId, Is.EqualTo(ownerPlayerId));
        Assert.That(installation.OriginalInstallerPlayerId, Is.EqualTo("p1"));
        Assert.That(installation.DurationRemaining, Is.EqualTo(duration));
        Assert.That(installation.BarrierRemaining, Is.EqualTo(barrier));
        Assert.That(installation.ChargesRemaining, Is.EqualTo(charges));
        Assert.That(installation.InitialCharges, Is.EqualTo(3));
        Assert.That(installation.NextChargeSequenceNumber, Is.EqualTo(2));
        Assert.That(installation.PaymentsRemaining, Is.EqualTo(payments));
        Assert.That(installation.Level, Is.EqualTo(level));
        Assert.That(installation.InstalledRound, Is.EqualTo(installedRound));
        Assert.That(installation.LastAutomaticRound, Is.EqualTo(lastAutomaticRound));
        Assert.That(installation.IsDestroyed, Is.False);
    }

    private sealed class AllWeaponsLoadout :
        IWDMatchWeaponLoadoutSource, IWDWeaponLevelSource
    {
        private readonly WDArmoryCatalogConfig catalog;
        public AllWeaponsLoadout(WDArmoryCatalogConfig catalog) => this.catalog = catalog;
        public bool TryGetWeaponIds(string playerId, out IReadOnlyList<string> weaponIds)
        {
            weaponIds = catalog.Weapons.Select(item => item.Id).ToArray();
            return true;
        }
        public bool TryGetLevel(string playerId, string weaponId, out int level)
        {
            WDWeaponDefinition weapon = catalog.FindWeapon(weaponId);
            level = weapon?.MaximumLevel ?? 0;
            return weapon != null;
        }
    }

    private sealed class MutableSource :
        IWDGameWeaponMutableStateSource, IWDGameWeaponStateSource
    {
        private sealed class TerritoryData
        {
            public string Id;
            public string Owner;
            public int Troops;
            public WDWeaponTerritoryStateKind State;
        }

        private readonly List<WDGameWeaponPlayerView> players = new();
        private readonly Dictionary<string, TerritoryData> territories =
            new(StringComparer.Ordinal);

        public int CurrentRound => 2;
        public string ActiveMapId => "mutation_test";
        public IReadOnlyList<WDGameWeaponPlayerView> GetPlayers() => players;
        public IReadOnlyList<string> GetOfficialPlayerOrder() =>
            players.Select(item => item.PlayerId).ToArray();
        public IReadOnlyList<WDGameWeaponTerritoryView> GetTerritories() =>
            territories.Values.OrderBy(item => item.Id, StringComparer.Ordinal)
                .Select(ToView).ToArray();
        public WDGameWeaponTerritoryView FindTerritory(string territoryId) =>
            territoryId != null && territories.TryGetValue(territoryId, out TerritoryData data)
                ? ToView(data) : null;
        public bool PlayerExists(string playerId) =>
            players.Any(item => item.PlayerId == playerId);
        public bool AreAllied(string playerAId, string playerBId)
        {
            WDGameWeaponPlayerView a = players.FirstOrDefault(item => item.PlayerId == playerAId);
            WDGameWeaponPlayerView b = players.FirstOrDefault(item => item.PlayerId == playerBId);
            return a != null && b != null && a.PlayerId != b.PlayerId &&
                players.Count(item => item.TeamIndex == a.TeamIndex) > 1 &&
                a.TeamIndex == b.TeamIndex;
        }
        public bool AreConnected(string originId, string targetId, TipoConexaoMapa type) => false;
        public IReadOnlyList<string> GetNeighbours(
            string territoryId, TipoConexaoMapa? type) => Array.Empty<string>();

        public bool TryApplyTerritory(
            string territoryId, string ownerPlayerId, int troops,
            WDWeaponTerritoryStateKind state)
        {
            if (!territories.TryGetValue(territoryId, out TerritoryData data)) return false;
            bool valid = state switch
            {
                WDWeaponTerritoryStateKind.Occupied =>
                    PlayerExists(ownerPlayerId) && troops >= 1,
                WDWeaponTerritoryStateKind.Neutral =>
                    string.IsNullOrEmpty(ownerPlayerId) && troops == 1,
                WDWeaponTerritoryStateKind.Empty =>
                    string.IsNullOrEmpty(ownerPlayerId) && troops == 0,
                _ => false
            };
            if (!valid) return false;
            data.Owner = ownerPlayerId;
            data.Troops = troops;
            data.State = state;
            return true;
        }

        public string Fingerprint() => string.Join("|", territories.Values
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .Select(item => $"{item.Id}:{item.Owner}:{item.Troops}:{item.State}"));

        public static MutableSource Create()
        {
            var result = new MutableSource();
            result.players.AddRange(new[]
            {
                new WDGameWeaponPlayerView("p1", 0, 0),
                new WDGameWeaponPlayerView("p2", 1, 1),
                new WDGameWeaponPlayerView("p3", 2, 0)
            });
            result.Add("owned", "p1", 5, WDWeaponTerritoryStateKind.Occupied);
            result.Add("enemy", "p2", 3, WDWeaponTerritoryStateKind.Occupied);
            result.Add("ally", "p3", 3, WDWeaponTerritoryStateKind.Occupied);
            result.Add("neutral", "", 1, WDWeaponTerritoryStateKind.Neutral);
            result.Add("empty", "", 0, WDWeaponTerritoryStateKind.Empty);
            return result;
        }

        private void Add(
            string id, string owner, int troops, WDWeaponTerritoryStateKind state) =>
            territories.Add(id, new TerritoryData
            {
                Id = id, Owner = owner, Troops = troops, State = state
            });

        private static WDGameWeaponTerritoryView ToView(TerritoryData data) =>
            new(data.Id, data.Owner, data.Troops, data.State);
    }
}
