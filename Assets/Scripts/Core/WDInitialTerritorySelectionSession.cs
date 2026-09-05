using System;
using System.Collections.Generic;
using System.Linq;

public sealed class WDInitialTerritoryAssignment
{
    public string PlayerId { get; }
    public string TerritoryId { get; }
    public bool WasFallback { get; }
    public WDInitialTerritoryAssignment(string playerId, string territoryId, bool wasFallback)
    {
        PlayerId = playerId; TerritoryId = territoryId; WasFallback = wasFallback;
    }
}

public sealed class WDInitialTerritoryStageResult
{
    public int Stage { get; }
    public IReadOnlyList<WDInitialTerritoryAssignment> Assignments { get; }
    public bool HadConflict { get; }
    public WDInitialTerritoryStageResult(int stage,
        IReadOnlyList<WDInitialTerritoryAssignment> assignments, bool hadConflict)
    {
        Stage = stage; Assignments = assignments; HadConflict = hadConflict;
    }
}

public sealed class WDInitialTerritorySelectionSession
{
    // ============================================================
    // 01. INTENÇÕES E PRIORIDADE DO ROUND 1
    // ============================================================

    private readonly Random random;
    private readonly string[] players;
    private readonly HashSet<string> available;
    private readonly Dictionary<string, string> intents = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> pending = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> assigned = new(StringComparer.Ordinal);
    private readonly Dictionary<int, WDInitialTerritoryStageResult> results = new();
    public int Stage { get; private set; } = 1;
    public bool IsComplete => Stage > 2;
    public bool ReadyToResolve => players.All(intents.ContainsKey);

    public WDInitialTerritorySelectionSession(int seed,
        IReadOnlyList<string> playersInRoundOrder, IEnumerable<string> territoryIds)
    {
        players = playersInRoundOrder?.Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal).Take(2).ToArray() ?? Array.Empty<string>();
        if (players.Length != 2)
            throw new ArgumentException("A abertura 1x1 exige dois jogadores em ordem de prioridade.");
        random = new Random(seed);
        available = new HashSet<string>(territoryIds?.Where(id =>
            !string.IsNullOrWhiteSpace(id)) ?? Array.Empty<string>(), StringComparer.Ordinal);
        foreach (string player in players)
            assigned[player] = new List<string>(2);
    }

    public bool SubmitIntent(string playerId, string territoryId)
    {
        if (IsComplete || !assigned.ContainsKey(playerId ?? string.Empty) ||
            !available.Contains(territoryId ?? string.Empty))
            return false;
        intents[playerId] = territoryId;
        return true;
    }

    // ============================================================
    // 02. ESCOLHA PROVISÓRIA LOCAL, SEM REGISTRAR INTENÇÃO
    // ============================================================

    public bool SelectPending(string playerId, string territoryId)
    {
        if (IsComplete || intents.ContainsKey(playerId ?? string.Empty) ||
            !assigned.ContainsKey(playerId ?? string.Empty) ||
            !available.Contains(territoryId ?? string.Empty))
            return false;

        pending[playerId] = territoryId;
        return true;
    }

    public bool CancelPending(string playerId) =>
        pending.Remove(playerId ?? string.Empty);

    public string GetPending(string playerId) =>
        pending.TryGetValue(playerId ?? string.Empty, out string territoryId)
            ? territoryId
            : string.Empty;

    public bool ConfirmPending(string playerId)
    {
        if (!pending.TryGetValue(playerId ?? string.Empty, out string territoryId) ||
            !SubmitIntent(playerId, territoryId))
            return false;

        pending.Remove(playerId);
        return true;
    }

    // ============================================================
    // 03. CONFLITO E FALLBACK DETERMINÍSTICO
    // ============================================================

    public WDInitialTerritoryStageResult ResolveStage()
    {
        if (results.TryGetValue(Stage, out WDInitialTerritoryStageResult existing))
            return existing;
        if (!ReadyToResolve)
            return null;

        string firstPlayer = players[0];
        string secondPlayer = players.First(player => player != firstPlayer);
        int resolvedStage = Stage;
        string firstChoice = intents[firstPlayer];
        string secondChoice = intents[secondPlayer];
        bool conflict = string.Equals(firstChoice, secondChoice, StringComparison.Ordinal);
        var assignments = new List<WDInitialTerritoryAssignment>(2);
        Assign(firstPlayer, firstChoice, false, assignments);
        Assign(secondPlayer, conflict || !available.Contains(secondChoice)
            ? PickAvailable() : secondChoice, conflict, assignments);
        var result = new WDInitialTerritoryStageResult(
            resolvedStage, assignments.AsReadOnly(), conflict);
        results.Add(resolvedStage, result);
        intents.Clear();
        pending.Clear();
        Stage++;
        return result;
    }

    public string PickMockIntent(string conflictCandidate = null)
    {
        if (!string.IsNullOrEmpty(conflictCandidate) && available.Contains(conflictCandidate) &&
            random.Next(3) == 0)
            return conflictCandidate;
        return PickAvailable();
    }

    public IReadOnlyList<string> GetAssignments(string playerId) =>
        assigned.TryGetValue(playerId ?? string.Empty, out List<string> values)
            ? values.AsReadOnly() : Array.Empty<string>();
    public bool IsAvailable(string territoryId) => available.Contains(territoryId ?? string.Empty);

    private void Assign(string playerId, string territoryId, bool fallback,
        List<WDInitialTerritoryAssignment> output)
    {
        if (string.IsNullOrEmpty(territoryId) || !available.Remove(territoryId))
            throw new InvalidOperationException("Não há território neutro disponível.");
        assigned[playerId].Add(territoryId);
        output.Add(new WDInitialTerritoryAssignment(playerId, territoryId, fallback));
    }

    private string PickAvailable()
    {
        if (available.Count == 0) return string.Empty;
        string[] ordered = available.OrderBy(id => id, StringComparer.Ordinal).ToArray();
        return ordered[random.Next(ordered.Length)];
    }
}
