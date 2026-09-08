using System;
using System.Collections.Generic;
using System.Linq;

public static class WDSupportWeaponIds
{
    public const string AirStrike = "air_strike";
    public const string Reinforcements = "reinforcements";
    public const string AirTransport = "air_transport";
    public const string FlakTower = "flak_tower";
    public const string Bunker = "bunker";
    public const string ReserveTroops = "reserve_troops";
    public const string TroopRecovery = "troop_recovery";
    public const string Mercenaries = "mercenaries";
    public const string Minefield = "minefield";
}

public sealed class WDWeaponTerritoryState
{
    // ============================================================
    // 01. ESTADO TERRITORIAL AUTORITATIVO PARA RESOLUÇÃO DE ARMAS
    // ============================================================

    private readonly HashSet<string> neighbours = new(StringComparer.Ordinal);
    private readonly List<WDWeaponInstallationState> installations = new();

    public string TerritoryId { get; }
    public string OwnerPlayerId { get; private set; }
    public int Troops { get; private set; }
    public WDWeaponTerritoryStateKind State { get; private set; }
    public bool IsImmobilized { get; private set; }
    public IReadOnlyCollection<string> Neighbours => neighbours;
    public IReadOnlyList<WDWeaponInstallationState> Installations => installations;

    public WDWeaponTerritoryState(string id, string owner, int troops,
        WDWeaponTerritoryStateKind state = WDWeaponTerritoryStateKind.Occupied)
    {
        TerritoryId = id ?? string.Empty;
        OwnerPlayerId = owner ?? string.Empty;
        Troops = Math.Max(0, troops);
        State = state;
    }

    public void AddNeighbour(string id) { if (!string.IsNullOrWhiteSpace(id)) neighbours.Add(id); }
    public void Immobilize() => IsImmobilized = true;
    public void ClearImmobilization() => IsImmobilized = false;
    public void AddTroops(int amount) => Troops = Math.Max(0, Troops + Math.Max(0, amount));
    public int RemoveTroops(int amount)
    {
        int removed = Math.Min(Troops, Math.Max(0, amount));
        Troops -= removed;
        return removed;
    }
    public void AddInstallation(WDWeaponInstallationState installation)
    {
        if (installation != null && !installations.Contains(installation)) installations.Add(installation);
    }
    public void RemoveInstallation(WDWeaponInstallationState installation) => installations.Remove(installation);
    public WDWeaponInstallationState FindInstallation(string weaponId) => installations.FirstOrDefault(
        item => !item.IsDestroyed && item.WeaponId == weaponId);
    public void MakeEmpty()
    {
        Troops = 0;
        OwnerPlayerId = string.Empty;
        State = WDWeaponTerritoryStateKind.Empty;
        foreach (WDWeaponInstallationState installation in installations)
            if (installation.WeaponId == WDSupportWeaponIds.Bunker ||
                installation.WeaponId == WDSupportWeaponIds.FlakTower)
                installation.Destroy();
    }
    public void Capture(string owner)
    {
        OwnerPlayerId = owner ?? string.Empty;
        State = WDWeaponTerritoryStateKind.Occupied;
        foreach (WDWeaponInstallationState installation in installations.Where(item => !item.IsDestroyed))
            if (installation.WeaponId != WDSupportWeaponIds.Minefield) installation.Capture(OwnerPlayerId);
    }
}

public enum WDWeaponTerritoryStateKind { Occupied, Neutral, Empty }

public sealed class WDPreviousRoundTerritoryRecord
{
    public string TerritoryId { get; }
    public string OwnerAtStart { get; }
    public string OwnerAtEnd { get; }
    public int Casualties { get; }
    public bool WasAttacked { get; }
    public WDPreviousRoundTerritoryRecord(string id, string start, string end, int casualties, bool attacked)
    { TerritoryId = id ?? ""; OwnerAtStart = start ?? ""; OwnerAtEnd = end ?? ""; Casualties = Math.Max(0, casualties); WasAttacked = attacked; }
}

public interface IWDWeaponRandomSource { double NextUnit(string stableKey); }

public sealed class WDDeterministicWeaponRandom : IWDWeaponRandomSource
{
    private readonly int seed;
    public WDDeterministicWeaponRandom(int seed) => this.seed = seed;
    public double NextUnit(string stableKey)
    {
        unchecked
        {
            uint hash = (uint)seed;
            foreach (char value in stableKey ?? string.Empty) hash = (hash ^ value) * 16777619u;
            return hash / ((double)uint.MaxValue + 1d);
        }
    }
}

public sealed class WDMercenaryPayment
{
    public string PlayerId { get; }
    public int DueRound { get; }
    public int Amount { get; }
    public bool Paid { get; private set; }
    public WDMercenaryPayment(string playerId, int dueRound, int amount)
    { PlayerId = playerId ?? ""; DueRound = dueRound; Amount = Math.Max(0, amount); }
    public void MarkPaid() => Paid = true;
}

public interface IWDSupportWeaponContext : IWDWeaponResolutionContext
{
    int CurrentRound { get; }
    IWDWeaponRandomSource Random { get; }
    WDWeaponTerritoryState FindTerritory(string territoryId);
    bool AreAllied(string firstPlayerId, string secondPlayerId);
    int GetTerritoriesLostLastRound(string playerId);
    WDPreviousRoundTerritoryRecord FindPreviousRoundRecord(string territoryId);
    void AddReinforcementPool(string playerId, int amount, string sourceWeaponId);
    void AddMercenaryPayment(WDMercenaryPayment payment);
    IEnumerable<WDMercenaryPayment> MercenaryPayments { get; }
}

public static class WDSupportWeaponTables
{
    // ============================================================
    // 02. VALORES DE NÍVEL CANÔNICOS, SEM MAGIC NUMBERS NOS RESOLVERS
    // ============================================================

    public static int AirStrikeDamage(int level) => From(new[] { 2, 3, 4, 5, 6, 7 }, level);
    public static int Reinforcement(int level) => From(new[] { 3, 4, 5, 6, 7, 8 }, level);
    public static int AirTransportCapacity(int level) => From(new[] { 5, 7, 10, 12, 15, 18 }, level);
    public static double FlakChance(int level) => From(new[] { 50, 65, 80, 90 }, level) / 100d;
    public static int BunkerBarrier(int level) => From(new[] { 2, 3, 4, 5 }, level);
    public static int RecoveryLimit(int level) => From(new[] { 4, 5, 6, 7, 8, 9 }, level);
    public static int ReserveTroops(int level, int lost)
    {
        int[,] table = { {1,1,2,4,5,6}, {1,1,2,4,6,7}, {1,2,2,5,6,7}, {1,2,2,5,7,9}, {1,2,3,6,8,10}, {1,2,3,8,10,12} };
        return lost <= 0 ? 0 : table[Math.Clamp(level, 1, 6) - 1, Math.Min(lost, 6) - 1];
    }
    public static int[] MercenaryPayments(int level)
    {
        int[,] table = { {1,1,1}, {2,1,1}, {2,2,1}, {2,2,2}, {3,2,2}, {3,3,2} };
        int row = Math.Clamp(level, 1, 6) - 1;
        return new[] { table[row,0], table[row,1], table[row,2] };
    }
    private static int From(int[] values, int level) => values[Math.Clamp(level, 1, values.Length) - 1];
}

public static class WDSupportWeaponInteractionService
{
    // ============================================================
    // 03. INTERAÇÕES COMUNS DE FLAK, BUNKER E TERRITÓRIO VAZIO
    // ============================================================

    public static WDWeaponInstallationState SelectCoveringFlak(
        IWDSupportWeaponContext context, WDWeaponTerritoryState target)
    {
        return AllTerritories(context).SelectMany(territory => territory.Installations
                .Where(item => !item.IsDestroyed && !item.IsTemporarilyDisabled &&
                    item.WeaponId == WDSupportWeaponIds.FlakTower &&
                    Covers(context, territory, target)))
            .OrderByDescending(item => item.Level)
            .ThenBy(item => item.InstallationId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public static bool TryIntercept(IWDSupportWeaponContext context,
        WDWeaponAction action, WDWeaponTerritoryState target)
    {
        WDWeaponInstallationState flak = SelectCoveringFlak(context, target);
        if (flak == null || flak.OwnerPlayerId == action.AuthorPlayerId ||
            context.AreAllied(flak.OwnerPlayerId, action.AuthorPlayerId))
            return false;
        return context.Random.NextUnit(action.ActionId + ":" + flak.InstallationId) <
            WDSupportWeaponTables.FlakChance(flak.Level);
    }

    public static int ApplyDamage(WDWeaponTerritoryState target, int damage, bool useBunker)
    {
        int remaining = Math.Max(0, damage);
        WDWeaponInstallationState bunker = useBunker ? target.FindInstallation(WDSupportWeaponIds.Bunker) : null;
        if (bunker != null && !bunker.IsTemporarilyDisabled)
        {
            int absorbed = Math.Min(remaining, bunker.BarrierRemaining);
            bunker.ApplyBarrierDamage(absorbed);
            remaining -= absorbed;
            if (bunker.BarrierRemaining == 0) bunker.Destroy();
        }
        int casualties = target.RemoveTroops(remaining);
        if (target.Troops == 0 && target.State == WDWeaponTerritoryStateKind.Occupied) target.MakeEmpty();
        return casualties;
    }

    private static bool Covers(IWDSupportWeaponContext context,
        WDWeaponTerritoryState source, WDWeaponTerritoryState target)
    {
        if (source.TerritoryId == target.TerritoryId) return true;
        if (!source.Neighbours.Contains(target.TerritoryId)) return false;
        return source.OwnerPlayerId == target.OwnerPlayerId ||
            context.AreAllied(source.OwnerPlayerId, target.OwnerPlayerId);
    }

    private static IEnumerable<WDWeaponTerritoryState> AllTerritories(IWDSupportWeaponContext context) =>
        context is IWDWeaponTerritoryEnumerable enumerable
            ? enumerable.Territories : Array.Empty<WDWeaponTerritoryState>();
}

public interface IWDWeaponTerritoryEnumerable { IEnumerable<WDWeaponTerritoryState> Territories { get; } }
