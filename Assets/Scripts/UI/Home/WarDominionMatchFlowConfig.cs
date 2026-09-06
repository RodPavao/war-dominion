using System;
using System.Collections.Generic;
using UnityEngine;

public enum WDContentAvailability
{
    Available,
    ComingSoon,
    Hidden
}

public enum WDMapCompatibilityPolicy
{
    All,
    AllowList,
    DenyList
}

public static class WDContentAvailabilityRules
{
    public static bool IsAvailable(WDContentAvailability availability) =>
        availability == WDContentAvailability.Available;

    public static bool IsVisible(WDContentAvailability availability) =>
        availability != WDContentAvailability.Hidden;
}

public enum WDMatchFlowDestination
{
    Matchmaking,
    SubmodeSelection,
    TeamFormation
}

public enum WDMapSelectionPolicy
{
    Vote,
    Fixed
}

[Serializable]
public sealed class WDMapModeRule
{
    [SerializeField] private string mapId;
    [SerializeField, Min(0f)] private float votingWeight = 1f;

    public string MapId => mapId;
    public float VotingWeight => votingWeight;

    public WDMapModeRule() { }

    public WDMapModeRule(string newMapId, float newVotingWeight)
    {
        mapId = newMapId;
        votingWeight = newVotingWeight;
    }
}

[Serializable]
public sealed class WDMapAvailabilityDefinition
{
    [SerializeField] private string mapId;
    [SerializeField] private bool enabled = true;

    public string MapId => mapId;
    public bool Enabled => enabled;

    public WDMapAvailabilityDefinition(string mapId, bool enabled)
    {
        this.mapId = mapId ?? string.Empty;
        this.enabled = enabled;
    }
}

[Serializable]
public sealed class WDModeMapCompatibilityDefinition
{
    [SerializeField] private string modeId;
    [SerializeField] private WDMapCompatibilityPolicy policy;
    [SerializeField] private List<string> mapIds = new();

    public string ModeId => modeId;
    public WDMapCompatibilityPolicy Policy => policy;
    public IReadOnlyList<string> MapIds => mapIds;

    public WDModeMapCompatibilityDefinition(
        string modeId,
        WDMapCompatibilityPolicy policy,
        IEnumerable<string> mapIds)
    {
        this.modeId = modeId ?? string.Empty;
        this.policy = policy;
        this.mapIds = mapIds != null ? new List<string>(mapIds) : new List<string>();
    }
}

public sealed class WDContentPolicyService
{
    // ============================================================
    // 01. POLÍTICA CENTRAL COM FALLBACK COMPATÍVEL
    // ============================================================

    private readonly IReadOnlyList<WDMapAvailabilityDefinition> maps;
    private readonly IReadOnlyList<WDModeMapCompatibilityDefinition> rules;

    public WDContentPolicyService(
        IReadOnlyList<WDMapAvailabilityDefinition> maps,
        IReadOnlyList<WDModeMapCompatibilityDefinition> rules)
    {
        this.maps = maps ?? Array.Empty<WDMapAvailabilityDefinition>();
        this.rules = rules ?? Array.Empty<WDModeMapCompatibilityDefinition>();
    }

    public bool IsMapEnabled(string mapId)
    {
        foreach (WDMapAvailabilityDefinition map in maps)
            if (map != null && string.Equals(map.MapId, mapId, StringComparison.Ordinal))
                return map.Enabled;

        // Mapas ainda não cadastrados preservam o comportamento legado.
        return true;
    }

    public bool IsMapAllowedForMode(string modeId, string mapId)
    {
        if (string.IsNullOrWhiteSpace(mapId) || !IsMapEnabled(mapId))
            return false;

        WDModeMapCompatibilityDefinition rule = null;
        foreach (WDModeMapCompatibilityDefinition candidate in rules)
            if (candidate != null &&
                string.Equals(candidate.ModeId, modeId, StringComparison.Ordinal))
            {
                rule = candidate;
                break;
            }

        if (rule == null || rule.Policy == WDMapCompatibilityPolicy.All)
            return true;

        bool listed = false;
        foreach (string candidate in rule.MapIds)
            if (string.Equals(candidate, mapId, StringComparison.Ordinal))
            {
                listed = true;
                break;
            }

        return rule.Policy == WDMapCompatibilityPolicy.AllowList
            ? listed
            : !listed;
    }

    public IReadOnlyList<WDMapModeRule> GetEligibleMaps(
        string modeId,
        IReadOnlyList<WDMapModeRule> candidates)
    {
        var eligible = new List<WDMapModeRule>();
        if (candidates == null)
            return eligible;

        foreach (WDMapModeRule candidate in candidates)
            if (candidate != null &&
                IsMapAllowedForMode(modeId, candidate.MapId))
                eligible.Add(candidate);
        return eligible;
    }
}

[Serializable]
public sealed class WDMatchSubmodeDefinition
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private WDContentAvailability availability;
    [SerializeField] private WDMatchFlowDestination destination;
    [SerializeField] private string cardRuleId;
    [SerializeField] private bool deckSelectionEnabled;
    [SerializeField] private List<WDMapModeRule> eligibleMaps = new();

    public string Id => id;
    public string DisplayName => displayName;
    public string Description => description;
    public WDContentAvailability Availability => availability;
    public WDMatchFlowDestination Destination => destination;
    public string CardRuleId => cardRuleId;
    public bool DeckSelectionEnabled => deckSelectionEnabled;
    public IReadOnlyList<WDMapModeRule> EligibleMaps => eligibleMaps;
}

[Serializable]
public sealed class WDMatchModeDefinition
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private WDContentAvailability availability;
    [SerializeField] private WDMatchFlowDestination destination;
    [SerializeField, Min(1)] private int groupSize = 1;
    [SerializeField, Min(0)] private int matchSize;
    [SerializeField] private bool botsAllowed = true;
    [SerializeField] private WDMapSelectionPolicy mapSelectionPolicy;
    [SerializeField] private string fixedMapId;
    [SerializeField, Min(1)] private int roundLimit = 10;
    [SerializeField] private bool suddenDeathEnabled = true;
    [SerializeField] private List<WDMapModeRule> eligibleMaps = new();
    [SerializeField] private List<WDMatchSubmodeDefinition> submodes = new();

    public string Id => id;
    public string DisplayName => displayName;
    public string Description => description;
    public WDContentAvailability Availability => availability;
    public WDMatchFlowDestination Destination => destination;
    public int GroupSize => groupSize;
    public int MatchSize => matchSize;
    public bool BotsAllowed => botsAllowed;
    public WDMapSelectionPolicy MapSelectionPolicy => mapSelectionPolicy;
    public string FixedMapId => fixedMapId;
    public int RoundLimit => roundLimit;
    public bool SuddenDeathEnabled => suddenDeathEnabled;
    public IReadOnlyList<WDMapModeRule> EligibleMaps => eligibleMaps;
    public IReadOnlyList<WDMatchSubmodeDefinition> Submodes => submodes;
}

public sealed class WDMatchmakingRequest
{
    // ============================================================
    // 01. CONTRATO IMUTÁVEL PARA INTEGRAÇÃO FUTURA
    // ============================================================

    public string ModeId { get; }
    public string SubmodeId { get; }
    public int GroupSize { get; }
    public int MatchSize { get; }
    public string TeamFormation { get; }
    public string CardRuleId { get; }
    public bool DeckSelectionEnabled { get; }
    public bool BotsAllowed { get; }
    public WDMapSelectionPolicy MapSelectionPolicy { get; }
    public string FixedMapId { get; }
    public int RoundLimit { get; }
    public bool SuddenDeathEnabled { get; }
    public IReadOnlyList<WDMapModeRule> EligibleMaps { get; }

    public WDMatchmakingRequest(
        WDMatchModeDefinition mode, WDMatchSubmodeDefinition submode,
        string teamFormation)
    {
        ModeId = mode?.Id ?? string.Empty;
        SubmodeId = submode?.Id ?? string.Empty;
        GroupSize = mode?.GroupSize ?? 1;
        MatchSize = mode?.MatchSize ?? 2;
        TeamFormation = teamFormation ?? string.Empty;
        CardRuleId = submode?.CardRuleId ?? "standard";
        DeckSelectionEnabled = submode != null && submode.DeckSelectionEnabled;
        BotsAllowed = mode?.BotsAllowed ?? false;
        MapSelectionPolicy = mode?.MapSelectionPolicy ?? WDMapSelectionPolicy.Vote;
        FixedMapId = mode?.FixedMapId ?? string.Empty;
        RoundLimit = mode?.RoundLimit ?? 10;
        SuddenDeathEnabled = mode?.SuddenDeathEnabled ?? true;
        EligibleMaps = submode != null && submode.EligibleMaps.Count > 0
            ? submode.EligibleMaps
            : mode?.EligibleMaps ?? Array.Empty<WDMapModeRule>();
    }
}

[CreateAssetMenu(
    fileName = "WarDominionMatchFlowConfig",
    menuName = "War Dominion/UI/Match Flow Config")]
public sealed class WarDominionMatchFlowConfig : ScriptableObject
{
    // ============================================================
    // 02. CATÁLOGO ADMINISTRÁVEL DE MODOS E SUBMODOS
    // ============================================================

    [SerializeField] private List<WDMatchModeDefinition> modes = new();
    [SerializeField] private List<WDMapAvailabilityDefinition> maps = new();
    [SerializeField] private List<WDModeMapCompatibilityDefinition> mapCompatibility = new();

    public IReadOnlyList<WDMatchModeDefinition> Modes => modes;
    public IReadOnlyList<WDMapAvailabilityDefinition> Maps => maps;
    public IReadOnlyList<WDModeMapCompatibilityDefinition> MapCompatibility =>
        mapCompatibility;

    public WDContentPolicyService CreatePolicyService() =>
        new WDContentPolicyService(maps, mapCompatibility);

    public bool IsMapAllowedForMode(string modeId, string mapId) =>
        CreatePolicyService().IsMapAllowedForMode(modeId, mapId);

    public IReadOnlyList<WDMapModeRule> GetEligibleMaps(
        string modeId,
        IReadOnlyList<WDMapModeRule> candidates) =>
        CreatePolicyService().GetEligibleMaps(modeId, candidates);
}
