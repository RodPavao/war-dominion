using System;
using System.Collections.Generic;
using System.Linq;

public enum WDProfileMode { Battle, OneVsOne, TwoVsTwo, ThreeVsThree }
public enum WDProfileCosmeticCategory { Avatar, Frame, TerritorySkin, Effect }
public enum WDProfileSocialState { Stranger, RequestSent, Friend, Blocked }

[Serializable]
public sealed class WDProfileStats
{
    public int Matches { get; }
    public int Wins { get; }
    public int Losses => Math.Max(0, Matches - Wins);
    public float WinRate => Matches == 0 ? 0f : Wins * 100f / Matches;

    public WDProfileStats(int matches, int wins)
    {
        Matches = Math.Max(0, matches);
        Wins = Math.Max(0, Math.Min(Matches, wins));
    }
}

[Serializable]
public sealed class WDProfilePrivacy
{
    public bool StatisticsVisible { get; }
    public bool PerformanceVisible { get; }
    public bool AchievementsVisible { get; }
    public bool HistoryVisible { get; }

    public WDProfilePrivacy(bool statistics = true, bool performance = true,
        bool achievements = true, bool history = true)
    {
        StatisticsVisible = statistics;
        PerformanceVisible = performance;
        AchievementsVisible = achievements;
        HistoryVisible = history;
    }
}

[Serializable]
public sealed class WDProfileCosmetic
{
    public string Id { get; }
    public string Name { get; }
    public WDProfileCosmeticCategory Category { get; }
    public bool Owned { get; }

    public WDProfileCosmetic(string id, string name,
        WDProfileCosmeticCategory category, bool owned)
    {
        Id = id;
        Name = name;
        Category = category;
        Owned = owned;
    }
}

[Serializable]
public sealed class WDProfileMatchRecord
{
    public string MatchId { get; }
    public WDProfileMode Mode { get; }
    public string MapName { get; }
    public string Result { get; }
    public string Participants { get; }
    public string PlayedAt { get; }

    public WDProfileMatchRecord(string matchId, WDProfileMode mode, string mapName,
        string result, string participants, string playedAt)
    {
        MatchId = matchId;
        Mode = mode;
        MapName = mapName;
        Result = result;
        Participants = participants;
        PlayedAt = playedAt;
    }
}

public sealed class WDProfileData
{
    private readonly Dictionary<WDProfileMode, WDProfileStats> modeStats;
    private readonly Dictionary<WDProfileCosmeticCategory, string> equipped;

    public string PlayerId { get; }
    public string Nickname { get; private set; }
    public string Division { get; }
    public int DivisionPoints { get; }
    public string Clan { get; }
    public bool IsLocalPlayer { get; }
    public WDProfilePrivacy Privacy { get; }
    public WDProfileSocialState SocialState { get; private set; }
    public WDProfileStats Statistics { get; }
    public IReadOnlyDictionary<WDProfileMode, WDProfileStats> ModeStats => modeStats;
    public IReadOnlyList<WDProfileMatchRecord> History { get; }
    public IReadOnlyDictionary<WDProfileCosmeticCategory, string> Equipped => equipped;

    public WDProfileData(string playerId, string nickname, string division,
        int divisionPoints, string clan, bool isLocalPlayer, WDProfilePrivacy privacy,
        WDProfileSocialState socialState, WDProfileStats statistics,
        IDictionary<WDProfileMode, WDProfileStats> stats,
        IEnumerable<WDProfileMatchRecord> history,
        IDictionary<WDProfileCosmeticCategory, string> equippedItems)
    {
        PlayerId = playerId;
        Nickname = nickname;
        Division = division;
        DivisionPoints = divisionPoints;
        Clan = clan;
        IsLocalPlayer = isLocalPlayer;
        Privacy = privacy ?? new WDProfilePrivacy();
        SocialState = socialState;
        Statistics = statistics ?? new WDProfileStats(0, 0);
        modeStats = new Dictionary<WDProfileMode, WDProfileStats>(stats ??
            new Dictionary<WDProfileMode, WDProfileStats>());
        History = (history ?? Array.Empty<WDProfileMatchRecord>()).Take(10).ToArray();
        equipped = new Dictionary<WDProfileCosmeticCategory, string>(equippedItems ??
            new Dictionary<WDProfileCosmeticCategory, string>());
    }

    public bool TryRename(string value)
    {
        string normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return false;
        Nickname = normalized;
        return true;
    }

    public void SendFriendRequest()
    {
        if (SocialState == WDProfileSocialState.Stranger)
            SocialState = WDProfileSocialState.RequestSent;
    }

    public void Block() => SocialState = WDProfileSocialState.Blocked;

    public void Equip(WDProfileCosmeticCategory category, string itemId) =>
        equipped[category] = itemId;
}

public sealed class WDProfileCustomizationSession
{
    private readonly WDProfileData profile;
    private readonly IReadOnlyDictionary<string, WDProfileCosmetic> catalog;
    private readonly Dictionary<WDProfileCosmeticCategory, string> preview;

    public IReadOnlyDictionary<WDProfileCosmeticCategory, string> Preview => preview;

    public WDProfileCustomizationSession(WDProfileData profile,
        IEnumerable<WDProfileCosmetic> cosmetics)
    {
        this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
        catalog = cosmetics.ToDictionary(item => item.Id, StringComparer.Ordinal);
        preview = new Dictionary<WDProfileCosmeticCategory, string>(profile.Equipped);
    }

    public bool PreviewItem(string itemId)
    {
        if (!catalog.TryGetValue(itemId ?? string.Empty, out WDProfileCosmetic item))
            return false;
        preview[item.Category] = item.Id;
        return true;
    }

    public bool Save()
    {
        if (preview.Any(pair => !catalog.TryGetValue(pair.Value, out WDProfileCosmetic item) ||
            !item.Owned || item.Category != pair.Key))
            return false;
        foreach (var pair in preview)
            profile.Equip(pair.Key, pair.Value);
        return true;
    }
}

public sealed class WDProfileRepository
{
    private readonly Dictionary<string, WDProfileData> profiles = new(StringComparer.Ordinal);
    public WDProfileData LocalProfile { get; }
    public IReadOnlyList<WDProfileCosmetic> Cosmetics { get; }

    public WDProfileRepository(WarDominionHomeData homeData)
    {
        string nickname = homeData != null && !string.IsNullOrWhiteSpace(homeData.Nickname)
            ? homeData.Nickname : "Guest4821";
        if (!nickname.StartsWith("Guest", StringComparison.OrdinalIgnoreCase) && nickname == "PLAYER")
            nickname = "Guest4821";
        string division = homeData != null && !string.IsNullOrWhiteSpace(homeData.League)
            ? homeData.League.Replace("Liga", "DIVISÃO").ToUpperInvariant() : "LEGIONNAIRE";
        LocalProfile = CreateProfile("WD-4821", nickname, division, true,
            new WDProfilePrivacy(), WDProfileSocialState.Friend);
        profiles[LocalProfile.PlayerId] = LocalProfile;
        profiles["WD-2847"] = CreateProfile("WD-2847", "Guest2847", "LEGIONNAIRE", false,
            new WDProfilePrivacy(), WDProfileSocialState.Stranger);
        profiles["WD-3906"] = CreateProfile("WD-3906", "Guest3906", "LEGIONNAIRE", false,
            new WDProfilePrivacy(false, false, false, false), WDProfileSocialState.Stranger);
        Cosmetics = CreateCosmetics();
    }

    public WDProfileData Find(string playerId) =>
        profiles.TryGetValue(playerId ?? string.Empty, out WDProfileData profile) ? profile : null;

    public static IReadOnlyList<WDProfileMatchRecord> FilterHistory(
        WDProfileData profile, WDProfileMode? mode) =>
        profile == null || (!profile.IsLocalPlayer && !profile.Privacy.HistoryVisible)
            ? Array.Empty<WDProfileMatchRecord>()
            : profile.History.Where(item => !mode.HasValue || item.Mode == mode.Value).Take(10).ToArray();

    private static WDProfileData CreateProfile(string id, string nickname, string division,
        bool local, WDProfilePrivacy privacy, WDProfileSocialState social) =>
        new(id, nickname, division, 1840, "Vanguard Elite", local, privacy, social,
            new WDProfileStats(284, 167),
            new Dictionary<WDProfileMode, WDProfileStats>
            {
                [WDProfileMode.Battle] = new(142, 89), [WDProfileMode.OneVsOne] = new(62, 37),
                [WDProfileMode.TwoVsTwo] = new(48, 25), [WDProfileMode.ThreeVsThree] = new(32, 16)
            }, CreateHistory(nickname),
            new Dictionary<WDProfileCosmeticCategory, string>
            {
                [WDProfileCosmeticCategory.Avatar] = "avatar_01",
                [WDProfileCosmeticCategory.Frame] = "frame_01",
                [WDProfileCosmeticCategory.TerritorySkin] = "skin_01",
                [WDProfileCosmeticCategory.Effect] = "effect_01"
            });

    private static IReadOnlyList<WDProfileMatchRecord> CreateHistory(string nickname)
    {
        var result = new List<WDProfileMatchRecord>();
        WDProfileMode[] modes = { WDProfileMode.OneVsOne, WDProfileMode.TwoVsTwo,
            WDProfileMode.Battle, WDProfileMode.ThreeVsThree };
        string[] maps = { "CLASSIC", "PEARL HARBOR", "DARK WORLD", "RIACHUELO" };
        for (int i = 0; i < 10; i++)
            result.Add(new WDProfileMatchRecord($"WD-{84 + i:00}C{i + 1:X}A",
                modes[i % modes.Length], maps[i % maps.Length],
                i % 3 == 0 ? "VITÓRIA" : i % 3 == 1 ? "DERROTA" : "2º LUGAR",
                $"{nickname} vs Guest{2847 + i}", $"{14 - i:00}/09/2026 · {21 - i:00}:42"));
        return result;
    }

    private static IReadOnlyList<WDProfileCosmetic> CreateCosmetics()
    {
        var items = new List<WDProfileCosmetic>();
        foreach (WDProfileCosmeticCategory category in Enum.GetValues(typeof(WDProfileCosmeticCategory)))
            for (int i = 1; i <= 8; i++)
                items.Add(new WDProfileCosmetic($"{CategoryPrefix(category)}_{i:00}",
                    $"{CategoryLabel(category)} {i:00}", category, i <= 4));
        return items;
    }

    private static string CategoryPrefix(WDProfileCosmeticCategory value) => value switch
    { WDProfileCosmeticCategory.Avatar => "avatar", WDProfileCosmeticCategory.Frame => "frame",
      WDProfileCosmeticCategory.TerritorySkin => "skin", _ => "effect" };
    public static string CategoryLabel(WDProfileCosmeticCategory value) => value switch
    { WDProfileCosmeticCategory.Avatar => "AVATAR", WDProfileCosmeticCategory.Frame => "MOLDURA",
      WDProfileCosmeticCategory.TerritorySkin => "SKIN DE TERRITÓRIO", _ => "EFEITOS" };
}
