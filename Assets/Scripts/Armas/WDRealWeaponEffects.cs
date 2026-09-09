using System;
using System.Collections.Generic;
using System.Linq;

public readonly struct WDRealWeaponDamageResult
{
    public bool Intercepted { get; }
    public int BarrierDamage { get; }
    public int TroopDamage { get; }
    public WDRealWeaponDamageResult(bool intercepted, int barrier, int troops)
    { Intercepted = intercepted; BarrierDamage = barrier; TroopDamage = troops; }
}

public sealed class WDWeaponInstallationLifecycle
{
    private readonly WDWeaponInstallationRegistry registry;
    private readonly IWDGameWeaponMutationPort mutations;

    // ============================================================
    // 01. LIFECYCLE CENTRALIZADO POR TRANSIÇÃO TERRITORIAL
    // ============================================================
    public WDWeaponInstallationLifecycle(
        WDWeaponInstallationRegistry registry, IWDGameWeaponMutationPort mutations)
    { this.registry = registry; this.mutations = mutations; }

    public void TerritoryBecameEmpty(string territoryId, string mutationPrefix)
    {
        foreach (WDWeaponInstallationState item in registry.GetByTerritory(territoryId).ToArray())
            if (item.WeaponId == WDSupportWeaponIds.Bunker ||
                item.WeaponId == WDSupportWeaponIds.FlakTower)
                mutations.RemoveInstallation($"{mutationPrefix}:remove:{item.InstallationId}", item.InstallationId);
            else if (item.WeaponId == WDStrategicWeaponIds.EnergyPlant ||
                     item.WeaponId == WDStrategicWeaponIds.Rockets ||
                     item.WeaponId == WDStrategicWeaponIds.Artillery)
                mutations.AbandonInstallation($"{mutationPrefix}:abandon:{item.InstallationId}", item.InstallationId);
    }

    public void TerritoryCaptured(string territoryId, string ownerId, string mutationPrefix)
    {
        foreach (WDWeaponInstallationState item in registry.GetByTerritory(territoryId).ToArray())
            mutations.CaptureInstallation($"{mutationPrefix}:capture:{item.InstallationId}",
                item.InstallationId, ownerId);
    }

    public int AdvanceFlakLifecycle(int round)
    {
        int changed = 0;
        foreach (WDWeaponInstallationState item in registry.GetAll()
            .Where(x => x.WeaponId == WDSupportWeaponIds.FlakTower).ToArray())
        {
            if (item.InstalledRound >= round || !item.TryMarkAutomaticRound(round)) continue;
            item.ConsumeDuration(); changed++;
            if (item.DurationRemaining == 0)
                mutations.RemoveInstallation($"flak:{item.InstallationId}:expire:{round}", item.InstallationId);
        }
        return changed;
    }
}

public sealed class WDRealWeaponDamageService
{
    private readonly WDGameWeaponContext context;
    private readonly IWDGameWeaponMutationPort mutations;
    private readonly WDWeaponInstallationRegistry registry;
    private readonly WDWeaponInstallationLifecycle lifecycle;
    private readonly IWDWeaponRandomSource random;

    // ============================================================
    // 02. FLAK DETERMINÍSTICA E DANO AUTORITATIVO COM BUNKER
    // ============================================================
    public WDRealWeaponDamageService(WDGameWeaponContext context,
        IWDGameWeaponMutationPort mutations, WDWeaponInstallationRegistry registry,
        WDWeaponInstallationLifecycle lifecycle, IWDWeaponRandomSource random)
    { this.context=context; this.mutations=mutations; this.registry=registry;
      this.lifecycle=lifecycle; this.random=random; }

    public WDWeaponInstallationState SelectCoveringFlak(string targetId)
    {
        return registry.GetAll().Where(x => x.WeaponId == WDSupportWeaponIds.FlakTower &&
            !x.IsAbandoned && x.DurationRemaining > 0 && Covers(x, targetId))
            .OrderByDescending(x => x.Level).ThenBy(x => x.InstallationId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public WDRealWeaponDamageResult Apply(string actionId, string targetId, int damage,
        bool canBeIntercepted, bool useBunker)
    {
        WDWeaponInstallationState flak = canBeIntercepted ? SelectCoveringFlak(targetId) : null;
        if (flak != null && random.NextUnit(actionId + ":" + flak.InstallationId) <
            WDSupportWeaponTables.FlakChance(flak.Level))
            return new WDRealWeaponDamageResult(true, 0, 0);

        int remaining = Math.Max(0, damage), barrierDamage = 0;
        WDWeaponInstallationState bunker = useBunker
            ? registry.GetByTerritory(targetId).FirstOrDefault(x =>
                x.WeaponId == WDSupportWeaponIds.Bunker && !x.IsTemporarilyDisabled)
            : null;
        if (bunker != null && remaining > 0)
        {
            barrierDamage = Math.Min(remaining, bunker.BarrierRemaining);
            bunker.ApplyBarrierDamage(barrierDamage); remaining -= barrierDamage;
            if (bunker.BarrierRemaining == 0)
                mutations.RemoveInstallation($"damage:{actionId}:bunker", bunker.InstallationId);
        }
        WDGameWeaponTerritoryView target = context.FindTerritory(targetId);
        int troopDamage = Math.Min(remaining, target?.Troops ?? 0);
        if (troopDamage > 0)
        {
            mutations.RemoveTroops($"damage:{actionId}:troops", targetId, troopDamage);
            if (context.FindTerritory(targetId)?.State == WDWeaponTerritoryStateKind.Empty)
                lifecycle.TerritoryBecameEmpty(targetId, "damage:" + actionId);
        }
        return new WDRealWeaponDamageResult(false, barrierDamage, troopDamage);
    }

    private bool Covers(WDWeaponInstallationState flak, string targetId)
    {
        WDGameWeaponTerritoryView site = context.FindTerritory(flak.TerritoryId);
        WDGameWeaponTerritoryView target = context.FindTerritory(targetId);
        if (site == null || target == null) return false;
        bool protectedOwner = target.OwnerPlayerId == flak.OwnerPlayerId ||
            context.AreAllied(flak.OwnerPlayerId, target.OwnerPlayerId);
        return protectedOwner && (flak.TerritoryId == targetId ||
            context.GetNeighbours(flak.TerritoryId).Contains(targetId));
    }
}
