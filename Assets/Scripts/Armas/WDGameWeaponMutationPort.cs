using System;
using System.Collections.Generic;
using System.Linq;

public enum WDGameWeaponMutationReason
{
    Success,
    DuplicateMutation,
    PlayerNotFound,
    TerritoryNotFound,
    WeaponNotFound,
    InvalidState,
    InvalidOwner,
    InvalidAmount,
    InsufficientTroops,
    InsufficientEnergy,
    InvalidInstallation,
    DuplicateInstallation,
    InstallationNotFound
}

public readonly struct WDGameWeaponMutationResult
{
    public WDGameWeaponMutationReason Reason { get; }
    public int Amount { get; }
    public bool Succeeded => Reason == WDGameWeaponMutationReason.Success;

    public WDGameWeaponMutationResult(
        WDGameWeaponMutationReason reason, int amount = 0)
    {
        Reason = reason;
        Amount = Math.Max(0, amount);
    }
}

public interface IWDGameWeaponTerritoryMutationPort
{
    WDGameWeaponMutationResult AddTroops(string mutationId, string territoryId, int amount);
    WDGameWeaponMutationResult RemoveTroops(string mutationId, string territoryId, int amount);
    WDGameWeaponMutationResult MakeOccupied(
        string mutationId, string territoryId, string playerId, int troops);
    WDGameWeaponMutationResult MakeNeutral(string mutationId, string territoryId);
    WDGameWeaponMutationResult MakeNeutral(
        string mutationId, string territoryId, int troops);
    WDGameWeaponMutationResult MakeEmpty(string mutationId, string territoryId);
    WDGameWeaponMutationResult TransferTerritory(
        string mutationId, string territoryId, string newOwnerPlayerId);
}

public interface IWDGameWeaponRuntimeMutationPort
{
    WDGameWeaponMutationResult AddEnergy(string mutationId, string playerId, int amount);
    WDGameWeaponMutationResult ConsumeEnergy(string mutationId, string playerId, int amount);
    WDGameWeaponMutationResult StartCooldown(
        string mutationId, string playerId, string weaponId, int rounds);
    WDGameWeaponMutationResult RegisterUse(
        string mutationId, string playerId, string weaponId);
}

public interface IWDGameWeaponInstallationMutationPort
{
    WDGameWeaponMutationResult Install(
        string mutationId, WDWeaponInstallationState installation);
    WDGameWeaponMutationResult RemoveInstallation(
        string mutationId, string installationId);
    WDGameWeaponMutationResult AbandonInstallation(
        string mutationId, string installationId);
    WDGameWeaponMutationResult CaptureInstallation(
        string mutationId, string installationId, string newOwnerPlayerId);
    WDGameWeaponMutationResult MoveInstallation(
        string mutationId, string installationId, string targetTerritoryId);
    WDGameWeaponMutationResult MoveInstallation(
        string mutationId, string installationId, string targetTerritoryId,
        string newOwnerPlayerId);
}

public interface IWDGameWeaponMutationPort :
    IWDGameWeaponTerritoryMutationPort,
    IWDGameWeaponRuntimeMutationPort,
    IWDGameWeaponInstallationMutationPort
{
}

public interface IWDGameWeaponMutableStateSource
{
    WDGameWeaponTerritoryView FindTerritory(string territoryId);
    bool PlayerExists(string playerId);
    bool AreAllied(string playerAId, string playerBId);
    bool TryApplyTerritory(
        string territoryId, string ownerPlayerId, int troops,
        WDWeaponTerritoryStateKind state);
}

public sealed class WDWeaponInstallationRegistry
{
    private readonly Dictionary<string, WDWeaponInstallationState> installations =
        new(StringComparer.Ordinal);

    // ============================================================
    // 01. FONTE RUNTIME ÚNICA DAS INSTALAÇÕES DA PARTIDA
    // ============================================================

    public WDWeaponInstallationState Find(string installationId) =>
        installationId != null && installations.TryGetValue(
            installationId, out WDWeaponInstallationState installation)
            ? installation : null;

    public IReadOnlyList<WDWeaponInstallationState> GetByTerritory(string territoryId) =>
        installations.Values.Where(item => item.TerritoryId == territoryId && !item.IsDestroyed)
            .OrderBy(item => item.WeaponId, StringComparer.Ordinal)
            .ThenBy(item => item.InstallationId, StringComparer.Ordinal).ToArray();

    public IReadOnlyList<WDWeaponInstallationState> GetAll() =>
        installations.Values.Where(item => !item.IsDestroyed)
            .OrderBy(item => item.TerritoryId, StringComparer.Ordinal)
            .ThenBy(item => item.WeaponId, StringComparer.Ordinal)
            .ThenBy(item => item.InstallationId, StringComparer.Ordinal).ToArray();

    internal WDGameWeaponMutationReason Add(WDWeaponInstallationState installation)
    {
        if (installation == null || string.IsNullOrWhiteSpace(installation.InstallationId) ||
            string.IsNullOrWhiteSpace(installation.WeaponId) ||
            string.IsNullOrWhiteSpace(installation.TerritoryId))
            return WDGameWeaponMutationReason.InvalidInstallation;
        if (installations.ContainsKey(installation.InstallationId))
            return WDGameWeaponMutationReason.DuplicateInstallation;
        if (installations.Values.Any(item => !item.IsDestroyed &&
            item.TerritoryId == installation.TerritoryId &&
            item.WeaponId == installation.WeaponId))
            return WDGameWeaponMutationReason.DuplicateInstallation;
        installations.Add(installation.InstallationId, installation);
        return WDGameWeaponMutationReason.Success;
    }

    internal bool Remove(string installationId) => installations.Remove(installationId);

    public static bool IsAirTransportAllowed(string weaponId) =>
        weaponId == WDSupportWeaponIds.FlakTower ||
        weaponId == WDStrategicWeaponIds.EnergyPlant ||
        weaponId == WDStrategicWeaponIds.Artillery ||
        weaponId == WDStrategicWeaponIds.Rockets ||
        weaponId == WDSpecialWeaponIds.Drone ||
        weaponId == WDTacticalWeaponIds.Radar;

    internal void ApplyTerritoryLifecycle(
        string territoryId, string ownerPlayerId, WDWeaponTerritoryStateKind state)
    {
        foreach (WDWeaponInstallationState item in installations.Values
            .Where(x => !x.IsDestroyed && x.TerritoryId == territoryId).ToArray())
        {
            if (state == WDWeaponTerritoryStateKind.Empty)
            {
                if (item.WeaponId == WDSupportWeaponIds.Bunker ||
                    item.WeaponId == WDSupportWeaponIds.FlakTower)
                    installations.Remove(item.InstallationId);
                else if (item.WeaponId == WDStrategicWeaponIds.EnergyPlant ||
                         item.WeaponId == WDStrategicWeaponIds.Rockets ||
                         item.WeaponId == WDStrategicWeaponIds.Artillery ||
                         item.WeaponId == WDSpecialWeaponIds.Drone ||
                         item.WeaponId == WDTacticalWeaponIds.Radar)
                    item.Abandon();
            }
            else if (state == WDWeaponTerritoryStateKind.Neutral)
            {
                // Mina preserva o instalador original; todas as demais ficam abandonadas.
                if (item.WeaponId != WDSupportWeaponIds.Minefield)
                    item.Abandon();
            }
            else if (state == WDWeaponTerritoryStateKind.Occupied &&
                     !string.IsNullOrEmpty(ownerPlayerId) &&
                     item.WeaponId != WDSupportWeaponIds.Minefield)
                item.Capture(ownerPlayerId);
        }
    }
}

public sealed class WDGameWeaponMutationPort : IWDGameWeaponMutationPort
{
    private readonly IWDGameWeaponMutableStateSource source;
    private readonly WDWeaponMatchRuntime runtime;
    private readonly WDArmoryCatalogConfig catalog;
    private readonly WDWeaponInstallationRegistry registry;
    private readonly WDWeaponRoundRuntime roundRuntime;
    private readonly HashSet<string> appliedMutationIds = new(StringComparer.Ordinal);

    // ============================================================
    // 02. CONSTRUÇÃO SOBRE FONTES AUTORITATIVAS EXISTENTES
    // ============================================================

    public WDGameWeaponMutationPort(
        IWDGameWeaponMutableStateSource source, WDWeaponMatchRuntime runtime,
        WDArmoryCatalogConfig catalog, WDWeaponInstallationRegistry registry,
        WDWeaponRoundRuntime roundRuntime = null)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        this.roundRuntime = roundRuntime;
    }

    // ============================================================
    // 03. TRANSIÇÕES TERRITORIAIS COMPLETAS E VALIDADAS
    // ============================================================

    public WDGameWeaponMutationResult AddTroops(
        string mutationId, string territoryId, int amount) => Execute(mutationId, () =>
    {
        WDGameWeaponTerritoryView territory = source.FindTerritory(territoryId);
        if (territory == null) return Fail(WDGameWeaponMutationReason.TerritoryNotFound);
        if (amount <= 0) return Fail(WDGameWeaponMutationReason.InvalidAmount);
        if (territory.State != WDWeaponTerritoryStateKind.Occupied ||
            string.IsNullOrEmpty(territory.OwnerPlayerId))
            return Fail(WDGameWeaponMutationReason.InvalidState);
        return Apply(territoryId, territory.OwnerPlayerId, territory.Troops + amount,
            WDWeaponTerritoryStateKind.Occupied, amount);
    });

    public WDGameWeaponMutationResult RemoveTroops(
        string mutationId, string territoryId, int amount) => Execute(mutationId, () =>
    {
        WDGameWeaponTerritoryView territory = source.FindTerritory(territoryId);
        if (territory == null) return Fail(WDGameWeaponMutationReason.TerritoryNotFound);
        if (amount <= 0) return Fail(WDGameWeaponMutationReason.InvalidAmount);
        if (amount > territory.Troops)
            return Fail(WDGameWeaponMutationReason.InsufficientTroops);
        int remaining = territory.Troops - amount;
        return remaining == 0
            ? Apply(territoryId, string.Empty, 0, WDWeaponTerritoryStateKind.Empty, amount)
            : Apply(territoryId, territory.OwnerPlayerId, remaining, territory.State, amount);
    });

    public WDGameWeaponMutationResult MakeOccupied(
        string mutationId, string territoryId, string playerId, int troops) =>
        Execute(mutationId, () =>
        {
            if (source.FindTerritory(territoryId) == null)
                return Fail(WDGameWeaponMutationReason.TerritoryNotFound);
            if (!source.PlayerExists(playerId))
                return Fail(WDGameWeaponMutationReason.PlayerNotFound);
            if (troops < 1) return Fail(WDGameWeaponMutationReason.InvalidState);
            return Apply(territoryId, playerId, troops,
                WDWeaponTerritoryStateKind.Occupied, troops);
        });

    public WDGameWeaponMutationResult MakeNeutral(string mutationId, string territoryId) =>
        MakeNeutral(mutationId, territoryId, 1);

    public WDGameWeaponMutationResult MakeNeutral(
        string mutationId, string territoryId, int troops) =>
        Execute(mutationId, () => source.FindTerritory(territoryId) == null
            ? Fail(WDGameWeaponMutationReason.TerritoryNotFound)
            : troops < 1
                ? Fail(WDGameWeaponMutationReason.InvalidAmount)
                : Apply(territoryId, string.Empty, troops,
                    WDWeaponTerritoryStateKind.Neutral, troops));

    public WDGameWeaponMutationResult MakeEmpty(string mutationId, string territoryId) =>
        Execute(mutationId, () => source.FindTerritory(territoryId) == null
            ? Fail(WDGameWeaponMutationReason.TerritoryNotFound)
            : Apply(territoryId, string.Empty, 0, WDWeaponTerritoryStateKind.Empty));

    public WDGameWeaponMutationResult TransferTerritory(
        string mutationId, string territoryId, string newOwnerPlayerId) =>
        Execute(mutationId, () =>
        {
            WDGameWeaponTerritoryView territory = source.FindTerritory(territoryId);
            if (territory == null) return Fail(WDGameWeaponMutationReason.TerritoryNotFound);
            if (!source.PlayerExists(newOwnerPlayerId))
                return Fail(WDGameWeaponMutationReason.PlayerNotFound);
            if (territory.State != WDWeaponTerritoryStateKind.Occupied ||
                territory.Troops < 1)
                return Fail(WDGameWeaponMutationReason.InvalidState);
            if (territory.OwnerPlayerId == newOwnerPlayerId ||
                !source.AreAllied(territory.OwnerPlayerId, newOwnerPlayerId))
                return Fail(WDGameWeaponMutationReason.InvalidOwner);
            return Apply(territoryId, newOwnerPlayerId, territory.Troops,
                WDWeaponTerritoryStateKind.Occupied, territory.Troops);
        });

    // ============================================================
    // 04. ENERGY, COOLDOWN E USOS NO RUNTIME EXISTENTE
    // ============================================================

    public WDGameWeaponMutationResult AddEnergy(
        string mutationId, string playerId, int amount) => Execute(mutationId, () =>
    {
        if (runtime.FindPlayer(playerId) == null)
            return Fail(WDGameWeaponMutationReason.PlayerNotFound);
        if (amount <= 0) return Fail(WDGameWeaponMutationReason.InvalidAmount);
        return runtime.AddEnergy(playerId, amount)
            ? Ok(amount) : Fail(WDGameWeaponMutationReason.InvalidAmount);
    });

    public WDGameWeaponMutationResult ConsumeEnergy(
        string mutationId, string playerId, int amount) => Execute(mutationId, () =>
    {
        WDWeaponPlayerRuntimeState player = runtime.FindPlayer(playerId);
        if (player == null) return Fail(WDGameWeaponMutationReason.PlayerNotFound);
        if (amount <= 0) return Fail(WDGameWeaponMutationReason.InvalidAmount);
        return runtime.TrySpendEnergy(playerId, amount)
            ? Ok(amount) : Fail(WDGameWeaponMutationReason.InsufficientEnergy);
    });

    public WDGameWeaponMutationResult StartCooldown(
        string mutationId, string playerId, string weaponId, int rounds) =>
        Execute(mutationId, () =>
        {
            WDWeaponRuntimeEntry weapon = FindWeapon(playerId, weaponId, out var error);
            if (weapon == null) return Fail(error);
            if (rounds < 0) return Fail(WDGameWeaponMutationReason.InvalidAmount);
            weapon.StartCooldown(rounds);
            return Ok(rounds);
        });

    public WDGameWeaponMutationResult RegisterUse(
        string mutationId, string playerId, string weaponId) => Execute(mutationId, () =>
    {
        WDWeaponRuntimeEntry weapon = FindWeapon(playerId, weaponId, out var error);
        if (weapon == null) return Fail(error);
        weapon.RecordUse();
        return Ok(1);
    });

    // ============================================================
    // 05. REGISTRO ÚNICO DE INSTALAÇÕES, SEM GAMEOBJECTS
    // ============================================================

    public WDGameWeaponMutationResult Install(
        string mutationId, WDWeaponInstallationState installation) => Execute(mutationId, () =>
    {
        if (installation == null || catalog.FindWeapon(installation.WeaponId) == null)
            return Fail(WDGameWeaponMutationReason.InvalidInstallation);
        WDGameWeaponTerritoryView territory = source.FindTerritory(installation.TerritoryId);
        if (territory == null) return Fail(WDGameWeaponMutationReason.TerritoryNotFound);
        if (!source.PlayerExists(installation.OwnerPlayerId) ||
            territory.State != WDWeaponTerritoryStateKind.Occupied ||
            territory.OwnerPlayerId != installation.OwnerPlayerId)
            return Fail(WDGameWeaponMutationReason.InvalidOwner);
        return new WDGameWeaponMutationResult(registry.Add(installation));
    });

    public WDGameWeaponMutationResult RemoveInstallation(
        string mutationId, string installationId) => Execute(mutationId, () =>
        registry.Remove(installationId)
            ? Ok() : Fail(WDGameWeaponMutationReason.InstallationNotFound));

    public WDGameWeaponMutationResult AbandonInstallation(
        string mutationId, string installationId) => Execute(mutationId, () =>
    {
        WDWeaponInstallationState installation = registry.Find(installationId);
        if (installation == null) return Fail(WDGameWeaponMutationReason.InstallationNotFound);
        installation.Abandon();
        return Ok();
    });

    public WDGameWeaponMutationResult CaptureInstallation(
        string mutationId, string installationId, string newOwnerPlayerId) =>
        Execute(mutationId, () =>
        {
            WDWeaponInstallationState installation = registry.Find(installationId);
            if (installation == null)
                return Fail(WDGameWeaponMutationReason.InstallationNotFound);
            if (!source.PlayerExists(newOwnerPlayerId))
                return Fail(WDGameWeaponMutationReason.PlayerNotFound);
            installation.Capture(newOwnerPlayerId);
            return Ok();
        });

    public WDGameWeaponMutationResult MoveInstallation(
        string mutationId, string installationId, string targetTerritoryId) =>
        MoveInstallation(mutationId, installationId, targetTerritoryId, null);

    public WDGameWeaponMutationResult MoveInstallation(
        string mutationId, string installationId, string targetTerritoryId,
        string newOwnerPlayerId) =>
        Execute(mutationId, () =>
        {
            WDWeaponInstallationState installation = registry.Find(installationId);
            if (installation == null)
                return Fail(WDGameWeaponMutationReason.InstallationNotFound);
            if (!WDWeaponInstallationRegistry.IsAirTransportAllowed(installation.WeaponId))
                return Fail(WDGameWeaponMutationReason.InvalidInstallation);
            if (source.FindTerritory(targetTerritoryId) == null)
                return Fail(WDGameWeaponMutationReason.TerritoryNotFound);
            if (registry.GetByTerritory(targetTerritoryId).Any(
                item => item.WeaponId == installation.WeaponId))
                return Fail(WDGameWeaponMutationReason.DuplicateInstallation);
            if (!string.IsNullOrEmpty(newOwnerPlayerId))
            {
                if (!source.PlayerExists(newOwnerPlayerId))
                    return Fail(WDGameWeaponMutationReason.PlayerNotFound);
                WDGameWeaponTerritoryView target = source.FindTerritory(targetTerritoryId);
                if (target.State != WDWeaponTerritoryStateKind.Occupied ||
                    target.OwnerPlayerId != newOwnerPlayerId)
                    return Fail(WDGameWeaponMutationReason.InvalidOwner);
            }
            installation.MoveTo(targetTerritoryId);
            if (!string.IsNullOrEmpty(newOwnerPlayerId))
                installation.Capture(newOwnerPlayerId);
            return Ok();
        });

    // ============================================================
    // 06. IDEMPOTÊNCIA E PRÉ-VALIDAÇÃO SEM ESTADO PARCIAL
    // ============================================================

    private WDGameWeaponMutationResult Execute(
        string mutationId, Func<WDGameWeaponMutationResult> operation)
    {
        if (!string.IsNullOrWhiteSpace(mutationId) &&
            appliedMutationIds.Contains(mutationId))
            return Fail(WDGameWeaponMutationReason.DuplicateMutation);
        WDGameWeaponMutationResult result = operation();
        if (result.Succeeded && !string.IsNullOrWhiteSpace(mutationId))
            appliedMutationIds.Add(mutationId);
        return result;
    }

    private WDGameWeaponMutationResult Apply(
        string territoryId, string ownerId, int troops,
        WDWeaponTerritoryStateKind state, int amount = 0)
    {
        string previousOwner = source.FindTerritory(territoryId)?.OwnerPlayerId ?? string.Empty;
        if (!source.TryApplyTerritory(territoryId, ownerId, troops, state))
            return Fail(WDGameWeaponMutationReason.InvalidState);
        roundRuntime?.RecordOwnershipChange(roundRuntime.ActiveRound, territoryId,
            previousOwner, ownerId);
        registry.ApplyTerritoryLifecycle(territoryId, ownerId, state);
        return Ok(amount);
    }

    private WDWeaponRuntimeEntry FindWeapon(
        string playerId, string weaponId, out WDGameWeaponMutationReason error)
    {
        WDWeaponPlayerRuntimeState player = runtime.FindPlayer(playerId);
        if (player == null)
        {
            error = WDGameWeaponMutationReason.PlayerNotFound;
            return null;
        }
        WDWeaponRuntimeEntry weapon = player.GetWeapon(weaponId);
        error = weapon == null
            ? WDGameWeaponMutationReason.WeaponNotFound
            : WDGameWeaponMutationReason.Success;
        return weapon;
    }

    private static WDGameWeaponMutationResult Ok(int amount = 0) =>
        new(WDGameWeaponMutationReason.Success, amount);
    private static WDGameWeaponMutationResult Fail(WDGameWeaponMutationReason reason) =>
        new(reason);
}

internal sealed class WDUnityGameWeaponMutableStateSource : IWDGameWeaponMutableStateSource
{
    private readonly GameManager gameManager;
    private readonly WDGameWeaponContext context;

    // ============================================================
    // 07. ADAPTADOR UNITY DIRETO PARA O TABULEIRO REAL
    // ============================================================

    public WDUnityGameWeaponMutableStateSource(GameManager gameManager)
    {
        this.gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
        context = new WDGameWeaponContext(gameManager);
    }

    public WDGameWeaponTerritoryView FindTerritory(string territoryId) =>
        context.FindTerritory(territoryId);

    public bool PlayerExists(string playerId) => context.FindPlayer(playerId) != null;

    public bool AreAllied(string playerAId, string playerBId) =>
        context.AreAllied(playerAId, playerBId);

    public bool TryApplyTerritory(
        string territoryId, string ownerPlayerId, int troops,
        WDWeaponTerritoryStateKind state)
    {
        TerritorioClique territory = MapaAtivo.ObterTerritoriosOuCena()
            .FirstOrDefault(item => item != null && item.idTerritorio == territoryId);
        if (territory == null)
            return false;

        TerritorioClique.Dono owner = TerritorioClique.Dono.Neutro;
        if (!string.IsNullOrEmpty(ownerPlayerId))
        {
            WDMatchParticipant participant = gameManager.MatchSetupAtual?.Participants
                .FirstOrDefault(item => item.PlayerId == ownerPlayerId);
            int ownerValue = participant != null ? participant.SlotIndex + 1 : -1;
            if (!Enum.IsDefined(typeof(TerritorioClique.Dono), ownerValue))
                return false;
            owner = (TerritorioClique.Dono)ownerValue;
        }

        TerritorioClique.EstadoTerritorial gameplayState = state switch
        {
            WDWeaponTerritoryStateKind.Occupied => TerritorioClique.EstadoTerritorial.Ocupado,
            WDWeaponTerritoryStateKind.Neutral => TerritorioClique.EstadoTerritorial.Neutro,
            WDWeaponTerritoryStateKind.Empty => TerritorioClique.EstadoTerritorial.Vazio,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };
        return territory.TentarAplicarEstadoAutoritativo(owner, troops, gameplayState);
    }
}
