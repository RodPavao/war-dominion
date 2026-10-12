using System;
using System.Collections.Generic;
using System.Linq;

public static class WDTacticalWeaponIds
{
    public const string Minefield = WDSupportWeaponIds.Minefield;
    public const string Blitzkrieg = "blitzkrieg";
    public const string TransferNullifier = "transfer_nullifier";
    public const string Sniper = "sniper";
    public const string Kamikaze = "kamikaze";
    public const string Paratroopers = "paratroopers";
    public const string Radar = "radar";
}

public interface IWDTacticalWeaponContext : IWDSupportWeaponContext
{
    bool TryBlockTransfers(string targetPlayerId, string actionId);
    bool AreTransfersBlocked(string playerId);
    void SetTerrestrialActionLimit(string playerId, int limit);
    int GetTerrestrialActionLimit(string playerId);
    void ConsumeTerrestrialActionSlot(string playerId);
    int GetConsumedTerrestrialActionSlots(string playerId);
}

public static class WDTacticalWeaponTables
{
    // ============================================================
    // 01. VALORES OFICIAIS E REGRAS DE ALCANCE
    // ============================================================

    public static int MineDamage(int level) => From(new[] { 2, 3, 4, 5, 6, 7 }, level);
    public static int BlitzkriegLimit(int level) => From(new[] { 4, 5, 6, 7 }, level);
    public static int KamikazeDamage(int level) => From(new[] { 2, 3, 4, 5, 6, 7 }, level);
    public static int ParatrooperCount(int level) => From(new[] { 2, 3, 4, 5 }, level);
    public const int RadarRevelations = 1;

    public static int Distance(IWDWeaponTerritoryEnumerable board, string originId, string targetId)
    {
        if (originId == targetId) return 0;
        Dictionary<string, WDWeaponTerritoryState> territories = board.Territories
            .ToDictionary(item => item.TerritoryId, StringComparer.Ordinal);
        if (!territories.ContainsKey(originId) || !territories.ContainsKey(targetId)) return int.MaxValue;
        var visited = new HashSet<string>(StringComparer.Ordinal) { originId };
        var queue = new Queue<(string id, int distance)>(); queue.Enqueue((originId, 0));
        while (queue.Count > 0)
        {
            (string id, int distance) = queue.Dequeue();
            foreach (string neighbour in territories[id].Neighbours.OrderBy(value => value, StringComparer.Ordinal))
            {
                if (!visited.Add(neighbour)) continue;
                if (neighbour == targetId) return distance + 1;
                if (territories.ContainsKey(neighbour)) queue.Enqueue((neighbour, distance + 1));
            }
        }
        return int.MaxValue;
    }

    public static int MinimumToConquer(int defense)
    {
        int safeDefense = Math.Max(1, defense);
        return safeDefense <= 10
            ? safeDefense + 1
            : safeDefense + Math.Max(2, (int)Math.Ceiling(safeDefense * .15d));
    }

    private static int From(int[] values, int level) => values[Math.Clamp(level, 1, values.Length) - 1];
}

public sealed class WDRadarRevealResult
{
    public int Round { get; }
    public string TerritoryId { get; }
    public bool HasFlakCoverage { get; }
    public string AuthorPlayerId { get; }
    public string CommandId { get; }

    public WDRadarRevealResult(int round, string territoryId, bool hasFlakCoverage,
        string authorPlayerId, string commandId)
    {
        Round = Math.Max(1, round);
        TerritoryId = territoryId ?? string.Empty;
        HasFlakCoverage = hasFlakCoverage;
        AuthorPlayerId = authorPlayerId ?? string.Empty;
        CommandId = commandId ?? string.Empty;
    }

    public bool CanBeReadBy(string playerId) => AuthorPlayerId == playerId;
}

public enum WDRadarUseStatus
{
    Used,
    Invalid,
    AlreadyProcessed
}

public readonly struct WDRadarUseResult
{
    public WDRadarUseStatus Status { get; }
    public WDRadarRevealResult Reveal { get; }
    public WDRadarUseResult(WDRadarUseStatus status, WDRadarRevealResult reveal = null)
    { Status = status; Reveal = reveal; }
}

public static class WDTacticalWeaponInteractionService
{
    // ============================================================
    // 02. ENTRADA REATIVA, MINA E DISPUTA TERRITORIAL
    // ============================================================

    public static int TriggerMineIfApplicable(WDWeaponTerritoryState target,
        string enteringPlayerId, int incomingTroops)
    {
        WDWeaponInstallationState mine = target.FindInstallation(WDTacticalWeaponIds.Minefield);
        if (mine == null || mine.IsTemporarilyDisabled) return incomingTroops;
        bool triggers = target.State == WDWeaponTerritoryStateKind.Empty ||
            (!string.IsNullOrEmpty(target.OwnerPlayerId) && target.OwnerPlayerId != enteringPlayerId);
        if (!triggers) return incomingTroops;
        mine.Destroy();
        return Math.Max(0, incomingTroops - WDTacticalWeaponTables.MineDamage(mine.Level));
    }

    public static int CrossBunker(WDWeaponTerritoryState target, int incomingTroops)
    {
        WDWeaponInstallationState bunker = target.FindInstallation(WDSupportWeaponIds.Bunker);
        if (bunker == null || bunker.IsTemporarilyDisabled) return incomingTroops;
        int absorbed = Math.Min(incomingTroops, bunker.BarrierRemaining);
        bunker.ApplyBarrierDamage(absorbed);
        if (bunker.BarrierRemaining == 0) bunker.Destroy();
        return incomingTroops - absorbed;
    }

    public static bool ResolveParatrooperArrival(IWDTacticalWeaponContext context,
        WDWeaponTerritoryState target, string playerId, int incomingTroops)
    {
        int survivors = TriggerMineIfApplicable(target, playerId, incomingTroops);
        if (survivors <= 0) return false;
        if (target.State == WDWeaponTerritoryStateKind.Empty)
        { target.Capture(playerId); target.AddTroops(survivors); return true; }
        if (target.OwnerPlayerId == playerId || context.AreAllied(playerId, target.OwnerPlayerId))
        { target.AddTroops(survivors); return true; }

        survivors = CrossBunker(target, survivors);
        if (survivors <= 0) return false;
        int defense = target.Troops;
        if (survivors >= WDTacticalWeaponTables.MinimumToConquer(defense))
        {
            int occupying = survivors - defense;
            target.RemoveTroops(defense); target.Capture(playerId); target.AddTroops(occupying);
            return true;
        }
        target.RemoveTroops(Math.Min(defense - 1, survivors));
        return false;
    }
}

public static class WDTerrestrialActionBudget
{
    // O slot é consumido antes da validação individual e nunca é devolvido.
    public static bool Reserve(IWDTacticalWeaponContext context, string playerId)
    {
        context.ConsumeTerrestrialActionSlot(playerId);
        return context.GetConsumedTerrestrialActionSlots(playerId) <=
            context.GetTerrestrialActionLimit(playerId);
    }
}
