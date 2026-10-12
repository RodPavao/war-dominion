using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum WDProfileMode { Battle, OneVsOne, TwoVsTwo, ThreeVsThree }
public enum WDProfileCosmeticCategory { Avatar, Frame, TerritorySkin, Effect }
public enum WDProfileSocialState { Self, Stranger, RequestSent, RequestReceived, Friend, Blocked }

// ============================================================
// 01. DIVISÕES CONFIGURÁVEIS E IDENTIDADE COMPETITIVA
// ============================================================

[Serializable]
public sealed class WDDivisionDefinition
{
    public string Id { get; }
    public string DisplayName { get; }
    public string EmblemId { get; }
    public int DisplayOrder { get; }
    public bool HasOpenUpperLimit { get; }

    public WDDivisionDefinition(string id, string displayName, string emblemId,
        int displayOrder, bool hasOpenUpperLimit = false)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        EmblemId = emblemId ?? string.Empty;
        DisplayOrder = displayOrder;
        HasOpenUpperLimit = hasOpenUpperLimit;
    }
}

public sealed class WDDivisionCatalog
{
    private readonly IReadOnlyList<WDDivisionDefinition> ordered;
    private readonly Dictionary<string, WDDivisionDefinition> byId;

    public IReadOnlyList<WDDivisionDefinition> Ordered => ordered;

    public WDDivisionCatalog(IEnumerable<WDDivisionDefinition> definitions)
    {
        ordered = (definitions ?? Array.Empty<WDDivisionDefinition>())
            .OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        byId = ordered.ToDictionary(item => item.Id, StringComparer.Ordinal);
    }

    public WDDivisionDefinition Find(string id) =>
        byId.TryGetValue(id ?? string.Empty, out WDDivisionDefinition value) ? value : null;

    public static WDDivisionCatalog CreatePrototype() => new(new[]
    {
        new WDDivisionDefinition("recruit", "RECRUIT", "division_recruit", 10),
        new WDDivisionDefinition("soldier", "SOLDIER", "division_soldier", 20),
        new WDDivisionDefinition("elite", "ELITE", "division_elite", 30),
        new WDDivisionDefinition("legionnaire", "LEGIONNAIRE", "division_legionnaire", 40),
        new WDDivisionDefinition("centurion", "CENTURION", "division_centurion", 50),
        new WDDivisionDefinition("commander", "COMMANDER", "division_commander", 60),
        new WDDivisionDefinition("warlord", "WARLORD", "division_warlord", 70),
        new WDDivisionDefinition("conqueror", "CONQUEROR", "division_conqueror", 80),
        new WDDivisionDefinition("supreme_commander", "SUPREME COMMANDER", "division_supreme_commander", 90),
        new WDDivisionDefinition("dominator", "DOMINATOR", "division_dominator", 100),
        new WDDivisionDefinition("veteran", "VETERAN", "division_veteran", 110, true)
    });
}

[Serializable]
public sealed class WDProfileStats
{
    public int Matches { get; }
    public int Wins { get; }
    public int Draws { get; }
    public int Losses => Math.Max(0, Matches - Wins - Draws);
    public float WinRate => Matches == 0 ? 0f : Wins * 100f / Matches;

    public WDProfileStats(int matches, int wins, int draws = 0)
    {
        Matches = Math.Max(0, matches);
        Wins = Math.Max(0, Math.Min(Matches, wins));
        Draws = Math.Max(0, Math.Min(Matches - Wins, draws));
    }
}

[Serializable]
public sealed class WDProfilePrivacy
{
    public bool StatisticsVisible { get; }
    public bool PerformanceVisible { get; }
    public bool AchievementsVisible { get; }
    public bool HistoryVisible { get; }
    public bool ShowStatistics => StatisticsVisible;
    public bool ShowModePerformance => PerformanceVisible;
    public bool ShowMatchHistory => HistoryVisible;
    public bool ShowMissionsAchievements => AchievementsVisible;

    public WDProfilePrivacy(bool statistics = true, bool performance = true,
        bool achievements = true, bool history = true)
    {
        StatisticsVisible = statistics;
        PerformanceVisible = performance;
        AchievementsVisible = achievements;
        HistoryVisible = history;
    }

    public WDProfilePrivacy WithStatistics(bool value) =>
        new(value, PerformanceVisible, AchievementsVisible, HistoryVisible);
    public WDProfilePrivacy WithModePerformance(bool value) =>
        new(StatisticsVisible, value, AchievementsVisible, HistoryVisible);
    public WDProfilePrivacy WithMatchHistory(bool value) =>
        new(StatisticsVisible, PerformanceVisible, AchievementsVisible, value);
    public WDProfilePrivacy WithMissionsAchievements(bool value) =>
        new(StatisticsVisible, PerformanceVisible, value, HistoryVisible);

    public static WDProfilePrivacy Defaults => new();
}

public interface IWDProfilePrivacyStore
{
    WDProfilePrivacy Load();
    void Save(WDProfilePrivacy privacy);
    void Reset();
}

public sealed class WDMemoryProfilePrivacyStore : IWDProfilePrivacyStore
{
    private WDProfilePrivacy value;

    public WDMemoryProfilePrivacyStore(WDProfilePrivacy initial = null) =>
        value = initial ?? WDProfilePrivacy.Defaults;

    public WDProfilePrivacy Load() => value;
    public void Save(WDProfilePrivacy privacy) => value = privacy;
    public void Reset() => value = WDProfilePrivacy.Defaults;
}

public sealed class WDPlayerPrefsProfilePrivacyStore : IWDProfilePrivacyStore
{
    private const string StorageKey = "WarDominion.Profile.Privacy.v1";

    [Serializable]
    private sealed class Data
    {
        public bool statistics = true;
        public bool modePerformance = true;
        public bool matchHistory = true;
        public bool missionsAchievements = true;
    }

    public WDProfilePrivacy Load()
    {
        if (!PlayerPrefs.HasKey(StorageKey))
            return WDProfilePrivacy.Defaults;
        Data data = JsonUtility.FromJson<Data>(PlayerPrefs.GetString(StorageKey));
        return data == null ? WDProfilePrivacy.Defaults : new WDProfilePrivacy(
            data.statistics, data.modePerformance,
            data.missionsAchievements, data.matchHistory);
    }

    public void Save(WDProfilePrivacy privacy)
    {
        var data = new Data
        {
            statistics = privacy.ShowStatistics,
            modePerformance = privacy.ShowModePerformance,
            matchHistory = privacy.ShowMatchHistory,
            missionsAchievements = privacy.ShowMissionsAchievements
        };
        PlayerPrefs.SetString(StorageKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    public void Reset()
    {
        PlayerPrefs.DeleteKey(StorageKey);
        PlayerPrefs.Save();
    }
}

public interface IWDProfileIdentityStore
{
    WDProfileIdentityState Load();
    void Save(WDProfileIdentityState state);
}

[Serializable]
public sealed class WDProfileIdentityState
{
    public string nickname;
    public string avatarId = "avatar_01";
    public string skinId = "skin_02";
}

public sealed class WDMemoryProfileIdentityStore : IWDProfileIdentityStore
{
    private WDProfileIdentityState state;

    public WDMemoryProfileIdentityStore(WDProfileIdentityState initial = null) =>
        state = Copy(initial ?? new WDProfileIdentityState());

    public WDProfileIdentityState Load() => Copy(state);
    public void Save(WDProfileIdentityState value) => state = Copy(value);

    private static WDProfileIdentityState Copy(WDProfileIdentityState value) => new()
    {
        nickname = value?.nickname,
        avatarId = value?.avatarId ?? "avatar_01",
        skinId = value?.skinId ?? "skin_02"
    };
}

public sealed class WDPlayerPrefsProfileIdentityStore : IWDProfileIdentityStore
{
    private const string StorageKey = "WarDominion.Profile.Identity.v1";

    public WDProfileIdentityState Load()
    {
        if (!PlayerPrefs.HasKey(StorageKey))
            return new WDProfileIdentityState();
        WDProfileIdentityState state = JsonUtility.FromJson<WDProfileIdentityState>(
            PlayerPrefs.GetString(StorageKey));
        return state ?? new WDProfileIdentityState();
    }

    public void Save(WDProfileIdentityState state)
    {
        PlayerPrefs.SetString(StorageKey, JsonUtility.ToJson(
            state ?? new WDProfileIdentityState()));
        PlayerPrefs.Save();
    }
}

[Serializable]
public sealed class WDProfileAchievement
{
    public string Id { get; }
    public string Title { get; }
    public string Progress { get; }

    public WDProfileAchievement(string id, string title, string progress)
    {
        Id = id ?? string.Empty;
        Title = title ?? string.Empty;
        Progress = progress ?? string.Empty;
    }
}

[Serializable]
public sealed class WDProfileCosmetic
{
    public string Id { get; }
    public string Name { get; }
    public WDProfileCosmeticCategory Category { get; }
    public bool Owned { get; private set; }

    public WDProfileCosmetic(string id, string name,
        WDProfileCosmeticCategory category, bool owned)
    {
        Id = id;
        Name = name;
        Category = category;
        Owned = owned;
    }

    internal void GrantOwnership() => Owned = true;
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
    public string SubmodeId { get; }
    public int Placement { get; }
    public int TrophiesEarned { get; }

    public WDProfileMatchRecord(string matchId, WDProfileMode mode, string mapName,
        string result, string participants, string playedAt,
        string submodeId = "", int placement = 0, int trophiesEarned = 0)
    {
        MatchId = matchId;
        Mode = mode;
        MapName = mapName;
        Result = result;
        Participants = participants;
        PlayedAt = playedAt;
        SubmodeId = submodeId ?? string.Empty;
        Placement = placement;
        TrophiesEarned = trophiesEarned;
    }
}

public sealed class WDProfileData
{
    private readonly Dictionary<WDProfileMode, WDProfileStats> modeStats;
    private readonly Dictionary<WDProfileCosmeticCategory, string> equipped;

    public string PlayerId { get; }
    public string Nickname { get; private set; }
    public WDDivisionDefinition DivisionDefinition { get; }
    public string DivisionId => DivisionDefinition.Id;
    public string Division => DivisionDefinition.DisplayName;
    public int DivisionPoints { get; private set; }
    public string Clan { get; private set; }
    public bool IsLocalPlayer { get; }
    public WDProfilePrivacy Privacy { get; private set; }
    public WDProfileSocialState SocialState { get; private set; }
    public WDProfileStats Statistics { get; private set; }
    public IReadOnlyDictionary<WDProfileMode, WDProfileStats> ModeStats => modeStats;
    public IReadOnlyList<WDProfileMatchRecord> History { get; private set; }
    public IReadOnlyList<WDProfileAchievement> Achievements { get; }
    public IReadOnlyDictionary<WDProfileCosmeticCategory, string> Equipped => equipped;

    public WDProfileData(string playerId, string nickname, WDDivisionDefinition division,
        int divisionPoints, string clan, bool isLocalPlayer, WDProfilePrivacy privacy,
        WDProfileSocialState socialState, WDProfileStats statistics,
        IDictionary<WDProfileMode, WDProfileStats> stats,
        IEnumerable<WDProfileMatchRecord> history, IEnumerable<WDProfileAchievement> achievements,
        IDictionary<WDProfileCosmeticCategory, string> equippedItems)
    {
        PlayerId = playerId;
        Nickname = nickname;
        DivisionDefinition = division ?? throw new ArgumentNullException(nameof(division));
        DivisionPoints = divisionPoints;
        Clan = clan;
        IsLocalPlayer = isLocalPlayer;
        Privacy = privacy ?? new WDProfilePrivacy();
        SocialState = socialState;
        Statistics = statistics ?? new WDProfileStats(0, 0);
        modeStats = new Dictionary<WDProfileMode, WDProfileStats>(stats ??
            new Dictionary<WDProfileMode, WDProfileStats>());
        History = (history ?? Array.Empty<WDProfileMatchRecord>()).Take(10).ToArray();
        Achievements = (achievements ?? Array.Empty<WDProfileAchievement>()).ToArray();
        equipped = new Dictionary<WDProfileCosmeticCategory, string>(equippedItems ??
            new Dictionary<WDProfileCosmeticCategory, string>());
    }

    public bool TryRename(string value)
    {
        if (!WDUIContentContracts.IsValidNickname(value))
            return false;
        // Espaços são identidade visual válida e devem permanecer exatamente
        // como digitados; PlayerId continua sendo a identidade estável.
        Nickname = value;
        return true;
    }

    public void SendFriendRequest()
    {
        if (SocialState == WDProfileSocialState.Stranger)
            SocialState = WDProfileSocialState.RequestSent;
    }

    public void Block() => SocialState = WDProfileSocialState.Blocked;

    internal void ApplySocialState(WDProfileSocialState value) => SocialState = value;

    internal void ApplyClan(string value) => Clan = value ?? string.Empty;

    public void Equip(WDProfileCosmeticCategory category, string itemId) =>
        equipped[category] = itemId;

    internal void ApplyPrivacy(WDProfilePrivacy privacy) =>
        Privacy = privacy ?? WDProfilePrivacy.Defaults;

    // 04. Projeção somente do jogador local; a autoridade é o ledger persistido.
    internal void ApplyMatchProgress(WDLocalMatchProgress progress)
    {
        if (!IsLocalPlayer || progress == null) return;
        DivisionPoints = progress.trophies;
        Statistics = new WDProfileStats(progress.matches, progress.wins, progress.draws);
        foreach (WDProfileMode mode in Enum.GetValues(typeof(WDProfileMode)))
        {
            WDLocalModeCounter counter = progress.modes.FirstOrDefault(item =>
                MatchMode(item.modeId) == mode);
            modeStats[mode] = new WDProfileStats(counter?.matches ?? 0,
                counter?.wins ?? 0, counter?.draws ?? 0);
        }
        History = progress.history.Where(item => item != null).Take(10).Select(item =>
            new WDProfileMatchRecord(item.resultId, MatchMode(item.modeId), item.mapId,
                item.draw ? "EMPATE" : item.modeId == "ffa" && item.placement > 0
                    ? $"{item.placement}º LUGAR" : item.victory ? "VITÓRIA" : "DERROTA",
                string.Empty, new DateTime(item.playedAtUtcTicks, DateTimeKind.Utc)
                    .ToString("dd/MM/yyyy · HH:mm"), item.submodeId, item.placement,
                item.trophiesEarned)).ToArray();
    }

    private static WDProfileMode MatchMode(string modeId) => modeId switch
    {
        "1x1" => WDProfileMode.OneVsOne,
        "2x2" => WDProfileMode.TwoVsTwo,
        _ => WDProfileMode.Battle
    };
}

// ============================================================
// 02. PROJEÇÃO DE VISIBILIDADE PARA MEU PERFIL E PERFIL PÚBLICO
// ============================================================

public sealed class WDProfilePresentation
{
    internal WDProfileData Profile { get; }
    public bool IsOwner { get; }
    public string PlayerId => Profile.PlayerId;
    public string Nickname => Profile.Nickname;
    public string Clan => Profile.Clan;
    public int DivisionPoints => Profile.DivisionPoints;
    public WDProfileSocialState SocialState => Profile.SocialState;
    public IReadOnlyDictionary<WDProfileCosmeticCategory, string> Equipped => Profile.Equipped;
    public WDDivisionDefinition Division => Profile.DivisionDefinition;
    public WDProfileStats Statistics { get; }
    public IReadOnlyDictionary<WDProfileMode, WDProfileStats> ModeStats { get; }
    public IReadOnlyList<WDProfileMatchRecord> History { get; }
    public IReadOnlyList<WDProfileAchievement> Achievements { get; }

    public bool CanShowStatistics => Statistics != null;
    public bool CanShowModePerformance => ModeStats != null;
    public bool CanShowMatchHistory => History != null;
    public bool CanShowMissionsAchievements => Achievements != null;

    public WDProfilePresentation(WDProfileData profile, bool isOwner)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        IsOwner = isOwner;
        Statistics = isOwner || profile.Privacy.ShowStatistics ? profile.Statistics : null;
        ModeStats = isOwner || profile.Privacy.ShowModePerformance ? profile.ModeStats : null;
        History = isOwner || profile.Privacy.ShowMatchHistory ? profile.History : null;
        Achievements = isOwner || profile.Privacy.ShowMissionsAchievements
            ? profile.Achievements : null;
    }
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
    private readonly IWDProfilePrivacyStore privacyStore;
    private readonly IWDProfileIdentityStore identityStore;
    public WDProfileData LocalProfile { get; }
    public IReadOnlyList<WDProfileData> AllProfiles => profiles.Values.ToArray();
    public IReadOnlyList<WDProfileCosmetic> Cosmetics { get; }

    // 03. O catálogo de Personalização projeta aquisições do perfil persistido compartilhado.
    public void ApplyPurchasedOwnership(IEnumerable<string> cosmeticIds)
    {
        var ids = new HashSet<string>(cosmeticIds ?? Array.Empty<string>(), StringComparer.Ordinal);
        foreach (WDProfileCosmetic item in Cosmetics)
            if (ids.Contains(item.Id)) item.GrantOwnership();
    }
    public WDDivisionCatalog Divisions { get; }

    public WDProfileRepository(WarDominionHomeData homeData,
        IWDProfilePrivacyStore newPrivacyStore = null,
        IWDProfileIdentityStore newIdentityStore = null)
    {
        privacyStore = newPrivacyStore ?? new WDPlayerPrefsProfilePrivacyStore();
        identityStore = newIdentityStore ?? new WDPlayerPrefsProfileIdentityStore();
        Divisions = WDDivisionCatalog.CreatePrototype();
        WDProfileIdentityState persistedIdentity = identityStore.Load();
        string nickname = WDUIContentContracts.IsValidNickname(persistedIdentity.nickname)
            ? persistedIdentity.nickname
            : homeData != null && WDUIContentContracts.IsValidNickname(homeData.Nickname)
            ? homeData.Nickname : "Guest4821";
        if (!nickname.StartsWith("Guest", StringComparison.OrdinalIgnoreCase) && nickname == "PLAYER")
            nickname = "Guest4821";
        LocalProfile = CreateProfile("WD-4821", nickname, "legionnaire", true,
            privacyStore.Load(), WDProfileSocialState.Self);
        LocalProfile.Equip(WDProfileCosmeticCategory.Avatar,
            NormalizeAvatarId(persistedIdentity.avatarId));
        LocalProfile.Equip(WDProfileCosmeticCategory.TerritorySkin,
            WDProfileSkinCatalog.Find(persistedIdentity.skinId)?.Id ?? WDProfileSkinCatalog.Default.Id);
        profiles[LocalProfile.PlayerId] = LocalProfile;
        profiles["WD-2847"] = CreateProfile("WD-2847", "Guest2847", "legionnaire", false,
            new WDProfilePrivacy(), WDProfileSocialState.Stranger);
        profiles["WD-3906"] = CreateProfile("WD-3906", "Guest3906", "legionnaire", false,
            new WDProfilePrivacy(false, false, false, false), WDProfileSocialState.Stranger);
        AddPrototypeProfile("WD-1112", "PlayerXXXX", "dominator", 2840);
        AddPrototypeProfile("WD-2924", "Guest2924", "recruit", 920);
        AddPrototypeProfile("WD-7105", "SergeantWolf", "elite", 1460);
        AddPrototypeProfile("WD-8150", "AlphaStrike", "centurion", 2050);
        AddPrototypeProfile("WD-5582", "CommanderZ", "supreme_commander", 2795);
        AddPrototypeProfile("WD-7331", "ShadowStalker", "conqueror", 2731);
        AddPrototypeProfile("WD-1291", "Guest1291", "soldier", 1180);
        AddPrototypeProfile("WD-6400", "RogueDominion", "warlord", 2610);
        AddPrototypeProfile("WD-2044", "IronValkyrie", "elite", 1580);
        AddPrototypeProfile("WD-3178", "AegisPrime", "centurion", 2140);
        AddPrototypeProfile("WD-4263", "FrostMarshal", "commander", 2385);
        AddPrototypeProfile("WD-5319", "CrimsonHawk", "legionnaire", 1840);
        AddPrototypeProfile("WD-6472", "TitanWard", "warlord", 2530);
        AddPrototypeProfile("WD-7526", "RavenCipher", "conqueror", 2710);
        AddPrototypeProfile("WD-8641", "StormBanner", "dominator", 2870);
        AddPrototypeProfile("WD-9085", "EmberLegion", "soldier", 1240);
        AddPrototypeProfile("WD-9734", "AtlasSentinel", "supreme_commander", 2960);
        Cosmetics = CreateCosmetics();
    }

    public bool TryRenameLocal(string value)
    {
        if (!LocalProfile.TryRename(value))
            return false;
        SaveLocalIdentity();
        return true;
    }

    public void SaveLocalIdentity()
    {
        identityStore.Save(new WDProfileIdentityState
        {
            nickname = LocalProfile.Nickname,
            avatarId = WDProfileIdentityVisuals.EquippedCosmeticId(
                LocalProfile, WDProfileCosmeticCategory.Avatar),
            skinId = WDProfileIdentityVisuals.EquippedCosmeticId(
                LocalProfile, WDProfileCosmeticCategory.TerritorySkin)
        });
    }

    private static string NormalizeAvatarId(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && value.StartsWith("avatar_", StringComparison.Ordinal) &&
            int.TryParse(value.Substring("avatar_".Length), out int index) && index >= 1 && index <= 10)
            return $"avatar_{index:00}";
        return "avatar_01";
    }

    public WDProfileData Find(string playerId) =>
        profiles.TryGetValue(playerId ?? string.Empty, out WDProfileData profile) ? profile : null;

    public WDProfilePresentation Present(string playerId, bool asOwner = false)
    {
        WDProfileData profile = Find(playerId);
        return profile == null ? null : new WDProfilePresentation(
            profile, asOwner && ReferenceEquals(profile, LocalProfile));
    }

    public void UpdateLocalPrivacy(WDProfilePrivacy privacy)
    {
        LocalProfile.ApplyPrivacy(privacy);
        privacyStore.Save(LocalProfile.Privacy);
    }

    public void RestoreLocalPrivacyDefaults()
    {
        privacyStore.Reset();
        LocalProfile.ApplyPrivacy(WDProfilePrivacy.Defaults);
    }

    public void ApplyLocalMatchProgress(WDLocalMatchProgress progress) =>
        LocalProfile.ApplyMatchProgress(progress);

    private void AddPrototypeProfile(string id, string nickname, string divisionId,
        int divisionPoints)
    {
        profiles[id] = CreateProfile(id, nickname, divisionId, false,
            new WDProfilePrivacy(), WDProfileSocialState.Stranger, divisionPoints);
    }

    public static IReadOnlyList<WDProfileMatchRecord> FilterHistory(
        WDProfileData profile, WDProfileMode? mode) =>
        profile == null || (!profile.IsLocalPlayer && !profile.Privacy.HistoryVisible)
            ? Array.Empty<WDProfileMatchRecord>()
            : profile.History.Where(item => !mode.HasValue || item.Mode == mode.Value).Take(10).ToArray();

    public static IReadOnlyList<WDProfileMatchRecord> FilterHistory(
        WDProfilePresentation presentation, WDProfileMode? mode) =>
        presentation?.History == null
            ? Array.Empty<WDProfileMatchRecord>()
            : presentation.History.Where(item => !mode.HasValue || item.Mode == mode.Value)
                .Take(10).ToArray();

    private WDProfileData CreateProfile(string id, string nickname, string divisionId,
        bool local, WDProfilePrivacy privacy, WDProfileSocialState social,
        int divisionPoints = 1840) =>
        new(id, nickname, Divisions.Find(divisionId), local ? 0 : divisionPoints, "Vanguard Elite", local, privacy, social,
            local ? new WDProfileStats(0, 0) : new WDProfileStats(284, 167),
            local ? Enum.GetValues(typeof(WDProfileMode)).Cast<WDProfileMode>()
                .ToDictionary(mode => mode, _ => new WDProfileStats(0, 0)) :
            new Dictionary<WDProfileMode, WDProfileStats>
            {
                [WDProfileMode.Battle] = new(142, 89), [WDProfileMode.OneVsOne] = new(62, 37),
                [WDProfileMode.TwoVsTwo] = new(48, 25), [WDProfileMode.ThreeVsThree] = new(32, 16)
            }, local ? Array.Empty<WDProfileMatchRecord>() : CreateHistory(nickname),
            local ? Array.Empty<WDProfileAchievement>() : CreateAchievements(),
            new Dictionary<WDProfileCosmeticCategory, string>
            {
                [WDProfileCosmeticCategory.Avatar] = "avatar_01",
                [WDProfileCosmeticCategory.Frame] = "frame_01",
                [WDProfileCosmeticCategory.TerritorySkin] = "skin_02",
                [WDProfileCosmeticCategory.Effect] = "effect_01"
            });

    private static IReadOnlyList<WDProfileAchievement> CreateAchievements() => new[]
    {
        new WDProfileAchievement("first_win", "PRIMEIRA DE MUITAS", "1/1"),
        new WDProfileAchievement("supreme_tactician", "TÁTICO SUPREMO", "15/20 VITÓRIAS"),
        new WDProfileAchievement("loyal_legionnaire", "LEGIONÁRIO FIEL", "284/500"),
        new WDProfileAchievement("war_veteran", "VETERANO DE GUERRA", "CONCLUÍDA")
    };

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
        for (int i = 1; i <= 10; i++)
            items.Add(new WDProfileCosmetic($"avatar_{i:00}", string.Empty,
                WDProfileCosmeticCategory.Avatar, true));
        foreach (WDProfileSkinDefinition skin in WDProfileSkinCatalog.All)
            items.Add(new WDProfileCosmetic(skin.Id, skin.DisplayName,
                WDProfileCosmeticCategory.TerritorySkin, true));
        for (int i = 1; i <= 8; i++)
        {
            items.Add(new WDProfileCosmetic($"frame_{i:00}", $"MOLDURA {i:00}",
                WDProfileCosmeticCategory.Frame, i <= 4));
            items.Add(new WDProfileCosmetic($"effect_{i:00}", $"EFEITO {i:00}",
                WDProfileCosmeticCategory.Effect, i <= 4));
        }
        return items;
    }

    private static string CategoryPrefix(WDProfileCosmeticCategory value) => value switch
    { WDProfileCosmeticCategory.Avatar => "avatar", WDProfileCosmeticCategory.Frame => "frame",
      WDProfileCosmeticCategory.TerritorySkin => "skin", _ => "effect" };
    public static string CategoryLabel(WDProfileCosmeticCategory value) => value switch
    { WDProfileCosmeticCategory.Avatar => "AVATAR", WDProfileCosmeticCategory.Frame => "MOLDURA",
      WDProfileCosmeticCategory.TerritorySkin => "SKIN", _ => "EFEITOS" };
}

public sealed class WDProfileSkinDefinition
{
    public string Id { get; }
    public string DisplayName { get; }
    public Color Color { get; }

    public WDProfileSkinDefinition(string id, string displayName, string hex)
    {
        Id = id;
        DisplayName = displayName;
        ColorUtility.TryParseHtmlString(hex, out Color color);
        Color = color;
    }
}

public static class WDProfileSkinCatalog
{
    public static readonly IReadOnlyList<WDProfileSkinDefinition> All = new[]
    {
        new WDProfileSkinDefinition("skin_01", "VERMELHO COMANDO", "#A0443A"),
        new WDProfileSkinDefinition("skin_02", "AZUL AÇO", "#466A8A"),
        new WDProfileSkinDefinition("skin_03", "VERDE MILITAR", "#587D62"),
        new WDProfileSkinDefinition("skin_04", "ÂMBAR TÁTICO", "#B99A4C"),
        new WDProfileSkinDefinition("skin_05", "CIANO OPERACIONAL", "#3E929C"),
        new WDProfileSkinDefinition("skin_06", "VIOLETA ESTRATÉGICO", "#625078"),
        new WDProfileSkinDefinition("skin_07", "GRAFITE", "#50545A"),
        new WDProfileSkinDefinition("skin_08", "BRANCO ÁRTICO", "#D7DAD4"),
        new WDProfileSkinDefinition("skin_09", "BRONZE", "#95704E"),
        new WDProfileSkinDefinition("skin_10", "BORDÔ", "#632E4B")
    };

    public static WDProfileSkinDefinition Default => All[1];
    public static WDProfileSkinDefinition Find(string id) =>
        All.FirstOrDefault(item => item.Id == id);
}

// ============================================================
// 04. RESOLUÇÃO VISUAL DA IDENTIDADE CANÔNICA DO JOGADOR
// ============================================================

public static class WDDivisionVisuals
{
    public const string Root = "UI/Home/FigmaPhase1A/Canonical/";
    public const float CalibrationReferenceSide = 160f;

    public readonly struct EmblemCalibration
    {
        public float Scale { get; }
        public float OffsetX { get; }
        public float OffsetY { get; }

        public EmblemCalibration(float scale, float offsetX, float offsetY)
        {
            Scale = scale;
            OffsetX = offsetX;
            OffsetY = offsetY;
        }
    }

    private static readonly IReadOnlyDictionary<string, EmblemCalibration> Calibrations =
        new Dictionary<string, EmblemCalibration>(StringComparer.Ordinal)
        {
            ["division_recruit"] = new(.760f, -2f, 0f),
            ["division_soldier"] = new(.980f, 0f, 0f),
            ["division_elite"] = new(.860f, -8f, 10f),
            ["division_legionnaire"] = new(.900f, 0f, -10f),
            ["division_centurion"] = new(.720f, -8f, 0f),
            ["division_commander"] = new(.960f, 0f, 0f),
            ["division_warlord"] = new(.980f, -2f, 0f),
            ["division_conqueror"] = new(.880f, -4f, 6f),
            ["division_supreme_commander"] = new(.940f, -2f, -2f),
            ["division_dominator"] = new(.960f, -2f, 4f),
            ["division_veteran"] = new(.980f, -6f, 6f)
        };

    public static Texture2D ResolveEmblem(WDDivisionDefinition division)
    {
        if (string.IsNullOrWhiteSpace(division?.EmblemId))
            return null;
        return Resources.Load<Texture2D>(Root + division.EmblemId);
    }

    public static Texture2D ResolveMedallion() =>
        Resources.Load<Texture2D>(Root + "HomeDivisionMedallionGold");

    public static EmblemCalibration ResolveCalibration(WDDivisionDefinition division)
    {
        return division != null &&
            Calibrations.TryGetValue(division.EmblemId, out EmblemCalibration calibration)
                ? calibration
                : new EmblemCalibration(1f, 0f, 0f);
    }

    public static void ApplyCalibration(RectTransform symbol,
        WDDivisionDefinition division, float compositionSide)
    {
        ApplyCalibration(symbol, ResolveCalibration(division), compositionSide);
    }

    public static void ApplyCalibration(RectTransform symbol,
        EmblemCalibration calibration, float compositionSide)
    {
        if (symbol == null)
            return;

        float canonicalSide = Mathf.Max(0f, compositionSide);
        float sizeRatio = canonicalSide / CalibrationReferenceSide;
        symbol.anchorMin = symbol.anchorMax = symbol.pivot = new Vector2(.5f, .5f);
        symbol.sizeDelta = new Vector2(canonicalSide, canonicalSide);
        symbol.anchoredPosition = new Vector2(
            calibration.OffsetX * sizeRatio,
            calibration.OffsetY * sizeRatio);
        symbol.localScale = Vector3.one * calibration.Scale;
    }
}

public static class WDProfileIdentityVisuals
{
    public const string Root = "UI/Profile/FigmaPhase6/Canonical/";
    private const string CommonLockPath = "UI/Common/Icons/icon_lock";

    public static string EquippedCosmeticId(
        WDProfileData profile, WDProfileCosmeticCategory category)
    {
        if (profile != null && profile.Equipped.TryGetValue(category, out string id))
            return id;
        return string.Empty;
    }

    public static Texture2D ResolveAvatar(WDProfileData profile) =>
        ResolveAvatar(EquippedCosmeticId(profile, WDProfileCosmeticCategory.Avatar));

    public static Texture2D ResolveAvatar(string avatarId)
    {
        if (!string.IsNullOrWhiteSpace(avatarId))
        {
            Texture2D texture = Resources.Load<Texture2D>(Root + "Avatars/" + avatarId);
            if (texture != null)
                return texture;
        }
        return Resources.Load<Texture2D>(Root + "Avatars/avatar_01");
    }

    public static Texture2D ResolveFactionAvatar(string factionId)
    {
        if (string.IsNullOrWhiteSpace(factionId))
            return null;
        return Resources.Load<Texture2D>(
            $"UI/Factions/Canonical/{factionId}/{factionId}");
    }

    public static Texture2D ResolveIcon(string iconId) =>
        Resources.Load<Texture2D>(Root + "Icons/" + iconId);

    public static Texture2D ResolveLockIcon()
    {
        Texture2D texture = Resources.Load<Texture2D>(CommonLockPath);
        if (texture != null)
            return texture;
        Sprite sprite = Resources.Load<Sprite>(CommonLockPath);
        if (sprite != null)
            return sprite.texture;
        Sprite[] sprites = Resources.LoadAll<Sprite>(CommonLockPath);
        return sprites.Length > 0 ? sprites[0].texture : null;
    }

    public static Texture2D ResolveAchievement(string achievementId)
    {
        string asset = achievementId switch
        {
            "first_win" => "achievement_trophy",
            "supreme_tactician" => "achievement_medal",
            "loyal_legionnaire" => "achievement_shield",
            "war_veteran" => "achievement_swords",
            _ => string.Empty
        };
        return string.IsNullOrEmpty(asset) ? null :
            Resources.Load<Texture2D>(Root + "Achievements/" + asset);
    }

    public static Texture2D ResolveCosmeticThumbnail(
        WDProfileCosmeticCategory category, string cosmeticId)
    {
        if (string.IsNullOrWhiteSpace(cosmeticId))
            return null;
        string folder = category switch
        {
            WDProfileCosmeticCategory.Avatar => "Avatars/",
            WDProfileCosmeticCategory.Frame => "Frames/",
            WDProfileCosmeticCategory.TerritorySkin => "TerritorySkins/",
            _ => string.Empty
        };
        return string.IsNullOrEmpty(folder) ? null :
            Resources.Load<Texture2D>(Root + folder + cosmeticId);
    }
}
