using System;
using System.Collections.Generic;
using System.Linq;

public enum WDRankingScope { General, Season }
public enum WDRankingEntity { Players, Clans }
public enum WDRankingRewardType { WeaponTokens, Emeralds, Coins }
public enum WDRankingResultState { Ready, Loading, Empty, Unavailable, Error }

// ============================================================
// 01. TEMPORADAS, CONSULTA E RECOMPENSAS FUTURAS
// ============================================================

public readonly struct WDSeasonId : IEquatable<WDSeasonId>
{
    public int Year { get; }
    public int Month { get; }
    public string Id => $"{Year:D4}-{Month:D2}";
    public string DisplayName => $"{MonthName(Month)}/{Year}";

    public WDSeasonId(int year, int month)
    {
        if (month < 1 || month > 12) throw new ArgumentOutOfRangeException(nameof(month));
        Year = year; Month = month;
    }

    public bool Equals(WDSeasonId other) => Year == other.Year && Month == other.Month;
    public override bool Equals(object obj) => obj is WDSeasonId other && Equals(other);
    public override int GetHashCode() => (Year * 397) ^ Month;
    public override string ToString() => Id;

    private static string MonthName(int month) => new[]
        { "JAN", "FEV", "MAR", "ABR", "MAI", "JUN", "JUL", "AGO", "SET", "OUT", "NOV", "DEZ" }[month - 1];
}

public sealed class WDRankingRewardDefinition
{
    public WDRankingRewardType Type { get; }
    public int? Quantity { get; }
    public string EligibilityRule { get; }

    public WDRankingRewardDefinition(WDRankingRewardType type, int? quantity = null,
        string eligibilityRule = null)
    {
        Type = type;
        Quantity = quantity;
        EligibilityRule = eligibilityRule;
    }
}

public sealed class WDRankingQuery
{
    public WDRankingScope Scope { get; set; } = WDRankingScope.General;
    public WDRankingEntity Entity { get; set; } = WDRankingEntity.Players;
    public WDSeasonId Season { get; set; }
    public string DivisionId { get; set; }
    public string Search { get; set; }

    public WDRankingQuery Clone() => new()
    {
        Scope = Scope, Entity = Entity, Season = Season,
        DivisionId = DivisionId, Search = Search
    };
}

// ============================================================
// 02. ENTRADAS E RESULTADO DATA-DRIVEN
// ============================================================

public sealed class WDRankingEntry
{
    public int Position { get; internal set; }
    public string EntityId { get; }
    public string DisplayName { get; }
    public string PublicPlayerId { get; }
    public string ClanName { get; }
    public WDDivisionDefinition Division { get; }
    public int Points { get; }
    public bool IsLocalPlayer { get; }

    internal WDRankingEntry(string entityId, string displayName, int points,
        string publicPlayerId = null, string clanName = null,
        WDDivisionDefinition division = null, bool isLocalPlayer = false)
    {
        EntityId = entityId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        Points = Math.Max(0, points);
        PublicPlayerId = publicPlayerId;
        ClanName = clanName;
        Division = division;
        IsLocalPlayer = isLocalPlayer;
    }
}

public sealed class WDRankingResult
{
    public WDRankingResultState State { get; }
    public IReadOnlyList<WDRankingEntry> Entries { get; }
    public WDRankingEntry LocalPlayer { get; }

    internal WDRankingResult(IReadOnlyList<WDRankingEntry> entries, WDRankingEntry localPlayer,
        WDRankingResultState state = WDRankingResultState.Ready)
    {
        Entries = entries ?? Array.Empty<WDRankingEntry>();
        LocalPlayer = localPlayer;
        State = state == WDRankingResultState.Ready && Entries.Count == 0
            ? WDRankingResultState.Empty : state;
    }
}

public interface IWDRankingDataSource
{
    WDRankingResultState State { get; }
    IReadOnlyList<WDSeasonId> Seasons { get; }
    IReadOnlyList<WDRankingRewardDefinition> SeasonRewards { get; }
    IReadOnlyDictionary<string, int> PlayerPoints(WDRankingScope scope, WDSeasonId season);
    IReadOnlyList<WDRankingEntry> ClanEntries(WDRankingScope scope, WDSeasonId season);
}

// ============================================================
// 03. FONTE LOCAL/MOCK SUBSTITUÍVEL
// ============================================================

public sealed class WDLocalRankingDataSource : IWDRankingDataSource
{
    private readonly Dictionary<string, IReadOnlyDictionary<string, int>> seasonalPlayers = new();
    private readonly Dictionary<string, IReadOnlyList<WDRankingEntry>> seasonalClans = new();
    private readonly IReadOnlyDictionary<string, int> generalPlayers;
    private readonly IReadOnlyList<WDRankingEntry> generalClans;

    public IReadOnlyList<WDSeasonId> Seasons { get; }
    public WDRankingResultState State => WDRankingResultState.Ready;
    public IReadOnlyList<WDRankingRewardDefinition> SeasonRewards { get; } = new[]
    {
        new WDRankingRewardDefinition(WDRankingRewardType.WeaponTokens),
        new WDRankingRewardDefinition(WDRankingRewardType.Emeralds),
        new WDRankingRewardDefinition(WDRankingRewardType.Coins)
    };

    public WDLocalRankingDataSource(DateTimeOffset now, IEnumerable<string> playerIds)
    {
        Seasons = Enumerable.Range(0, 3).Select(offset => now.AddMonths(-offset))
            .Select(date => new WDSeasonId(date.Year, date.Month)).ToArray();
        string[] ids = (playerIds ?? Array.Empty<string>()).ToArray();
        generalPlayers = Points(ids, 3100, 83);
        generalClans = Clans(9200);
        for (int i = 0; i < Seasons.Count; i++)
        {
            seasonalPlayers[Seasons[i].Id] = Points(ids.Reverse().ToArray(), 1450 - i * 120, 47);
            seasonalClans[Seasons[i].Id] = Clans(4100 - i * 260);
        }
    }

    public IReadOnlyDictionary<string, int> PlayerPoints(WDRankingScope scope, WDSeasonId season) =>
        scope == WDRankingScope.General ? generalPlayers :
        seasonalPlayers.TryGetValue(season.Id, out IReadOnlyDictionary<string, int> value)
            ? value : new Dictionary<string, int>();

    public IReadOnlyList<WDRankingEntry> ClanEntries(WDRankingScope scope, WDSeasonId season) =>
        scope == WDRankingScope.General ? generalClans :
        seasonalClans.TryGetValue(season.Id, out IReadOnlyList<WDRankingEntry> value)
            ? value : Array.Empty<WDRankingEntry>();

    private static IReadOnlyDictionary<string, int> Points(string[] ids, int top, int step) =>
        ids.Select((id, index) => (id, points: Math.Max(0, top - index * step)))
            .ToDictionary(item => item.id, item => item.points, StringComparer.Ordinal);

    private static IReadOnlyList<WDRankingEntry> Clans(int top) => new[]
    {
        new WDRankingEntry("clan-vanguard", "Vanguard Elite", top),
        new WDRankingEntry("clan-iron", "Iron Dominion", top - 180),
        new WDRankingEntry("clan-sentinel", "Sentinel Order", top - 360),
        new WDRankingEntry("clan-shadow", "Shadow Legion", top - 540),
        new WDRankingEntry("clan-atlas", "Atlas Command", top - 720)
    };
}

// ============================================================
// 04. AUTORIDADE ÚNICA DE CONSULTA
// ============================================================

public sealed class WDRankingRepository
{
    private readonly WDProfileRepository profiles;
    private readonly IWDRankingDataSource source;

    public WDProfileRepository Profiles => profiles;
    public WDDivisionCatalog Divisions => profiles.Divisions;
    public IReadOnlyList<WDSeasonId> Seasons => source.Seasons;
    public IReadOnlyList<WDRankingRewardDefinition> SeasonRewards => source.SeasonRewards;
    public WDSeasonId CurrentSeason => Seasons[0];

    public WDRankingRepository(WDProfileRepository profileRepository,
        IWDRankingDataSource dataSource = null, DateTimeOffset? now = null)
    {
        profiles = profileRepository ?? throw new ArgumentNullException(nameof(profileRepository));
        source = dataSource ?? new WDLocalRankingDataSource(now ?? DateTimeOffset.Now,
            profiles.AllProfiles.OrderBy(profile => profile.IsLocalPlayer ? 1 : 0)
                .Select(profile => profile.PlayerId));
    }

    public WDRankingResult Query(WDRankingQuery query)
    {
        if (query == null) throw new ArgumentNullException(nameof(query));
        if (source.State != WDRankingResultState.Ready)
            return new WDRankingResult(Array.Empty<WDRankingEntry>(), null, source.State);
        WDSeasonId season = query.Scope == WDRankingScope.General ? CurrentSeason : query.Season;
        List<WDRankingEntry> ranked = query.Entity == WDRankingEntity.Players
            ? BuildPlayers(query.Scope, season) : source.ClanEntries(query.Scope, season).ToList();
        ranked = ranked.OrderByDescending(entry => entry.Points)
            .ThenBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        for (int i = 0; i < ranked.Count; i++) ranked[i].Position = i + 1;

        WDRankingEntry local = ranked.FirstOrDefault(entry => entry.IsLocalPlayer);
        IEnumerable<WDRankingEntry> filtered = ranked;
        if (query.Entity == WDRankingEntity.Players && !string.IsNullOrWhiteSpace(query.DivisionId))
            filtered = filtered.Where(entry => entry.Division?.Id == query.DivisionId);
        string search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
            filtered = filtered.Where(entry => entry.DisplayName.IndexOf(search,
                StringComparison.OrdinalIgnoreCase) >= 0 ||
                (entry.PublicPlayerId?.IndexOf(search, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                entry.EntityId.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
        WDRankingEntry[] entries = filtered.ToArray();
        if (local != null && !entries.Contains(local)) local = null;
        return new WDRankingResult(entries, local);
    }

    private List<WDRankingEntry> BuildPlayers(WDRankingScope scope, WDSeasonId season)
    {
        IReadOnlyDictionary<string, int> points = source.PlayerPoints(scope, season);
        return profiles.AllProfiles.Where(profile => profile.IsLocalPlayer ||
                points.ContainsKey(profile.PlayerId))
            .Select(profile => new WDRankingEntry(profile.PlayerId, profile.Nickname,
                profile.IsLocalPlayer ? profile.DivisionPoints :
                    points.GetValueOrDefault(profile.PlayerId),
                profile.PlayerId, profile.Clan,
                profile.DivisionDefinition, profile.IsLocalPlayer)).ToList();
    }
}
