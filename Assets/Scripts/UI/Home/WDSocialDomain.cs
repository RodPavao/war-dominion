using System;
using System.Collections.Generic;
using System.Linq;

public enum WDSocialPresence { Online, InMatch, Offline }
public enum WDAddFriendEligibility
{
    You,
    AlreadyFriend,
    RequestSent,
    Blocked,
    Eligible
}

public sealed class WDSocialMockSeed
{
    public string PlayerId { get; }
    public WDProfileSocialState Relationship { get; }
    public WDSocialPresence Presence { get; }

    public WDSocialMockSeed(string playerId, WDProfileSocialState relationship,
        WDSocialPresence presence)
    {
        PlayerId = playerId;
        Relationship = relationship;
        Presence = presence;
    }
}

public static class WDSocialMockCatalog
{
    // ============================================================
    // 01. DADOS FICTÍCIOS SEPARADOS DA AUTORIDADE SOCIAL
    // ============================================================

    public static IReadOnlyList<WDSocialMockSeed> CreateSeeds() => new[]
    {
        new WDSocialMockSeed("WD-2847", WDProfileSocialState.Friend, WDSocialPresence.Online),
        new WDSocialMockSeed("WD-3906", WDProfileSocialState.Friend, WDSocialPresence.Offline),
        new WDSocialMockSeed("WD-1112", WDProfileSocialState.Friend, WDSocialPresence.InMatch),
        new WDSocialMockSeed("WD-7105", WDProfileSocialState.Friend, WDSocialPresence.Online),
        new WDSocialMockSeed("WD-8150", WDProfileSocialState.Friend, WDSocialPresence.InMatch),
        new WDSocialMockSeed("WD-2924", WDProfileSocialState.RequestReceived, WDSocialPresence.Offline),
        new WDSocialMockSeed("WD-5582", WDProfileSocialState.RequestReceived, WDSocialPresence.Online),
        new WDSocialMockSeed("WD-7331", WDProfileSocialState.RequestReceived, WDSocialPresence.Online),
        new WDSocialMockSeed("WD-1291", WDProfileSocialState.Blocked, WDSocialPresence.Offline),
        new WDSocialMockSeed("WD-6400", WDProfileSocialState.Blocked, WDSocialPresence.Offline)
    };
}

public sealed class WDSocialRepository
{
    // ============================================================
    // 02. AUTORIDADE SOCIAL LOCAL DE SESSÃO
    // ============================================================

    private readonly WDProfileRepository profiles;
    private readonly Dictionary<string, WDSocialPresence> presence =
        new(StringComparer.OrdinalIgnoreCase);

    public event Action Changed;
    public int PendingReceivedCount => profiles.AllProfiles.Count(profile =>
        profile.SocialState == WDProfileSocialState.RequestReceived);

    public WDSocialRepository(WDProfileRepository profileRepository,
        IEnumerable<WDSocialMockSeed> seeds = null)
    {
        profiles = profileRepository ?? throw new ArgumentNullException(nameof(profileRepository));
        profiles.LocalProfile.ApplySocialState(WDProfileSocialState.Self);
        foreach (WDSocialMockSeed seed in seeds ?? WDSocialMockCatalog.CreateSeeds())
        {
            WDProfileData profile = profiles.Find(seed.PlayerId);
            if (profile == null || profile.IsLocalPlayer)
                continue;
            profile.ApplySocialState(seed.Relationship);
            presence[profile.PlayerId] = seed.Presence;
        }
    }

    public IReadOnlyList<WDProfileData> Friends(string query = null) =>
        Filter(WDProfileSocialState.Friend, query);
    public IReadOnlyList<WDProfileData> ReceivedRequests() =>
        Filter(WDProfileSocialState.RequestReceived, null);
    public IReadOnlyList<WDProfileData> BlockedPlayers() =>
        Filter(WDProfileSocialState.Blocked, null);

    public WDSocialPresence PresenceOf(string playerId) =>
        presence.TryGetValue(playerId ?? string.Empty, out WDSocialPresence value)
            ? value : WDSocialPresence.Offline;

    public WDProfileData FindCatalogResult(string query)
    {
        string normalized = query?.Trim();
        if (string.IsNullOrEmpty(normalized))
            return null;
        return profiles.AllProfiles.FirstOrDefault(profile =>
            profile.PlayerId.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
            profile.Nickname.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    public WDAddFriendEligibility Eligibility(WDProfileData profile)
    {
        if (profile == null || profile.IsLocalPlayer)
            return WDAddFriendEligibility.You;
        return profile.SocialState switch
        {
            WDProfileSocialState.Friend => WDAddFriendEligibility.AlreadyFriend,
            WDProfileSocialState.RequestSent => WDAddFriendEligibility.RequestSent,
            WDProfileSocialState.Blocked => WDAddFriendEligibility.Blocked,
            _ => WDAddFriendEligibility.Eligible
        };
    }

    // ============================================================
    // 03. TRANSIÇÕES SEM ESTADOS CONTRADITÓRIOS
    // ============================================================

    public bool SendFriendRequest(string playerId) => Transition(playerId,
        state => state == WDProfileSocialState.Stranger,
        WDProfileSocialState.RequestSent);

    public bool AcceptReceived(string playerId) => Transition(playerId,
        state => state == WDProfileSocialState.RequestReceived,
        WDProfileSocialState.Friend);

    public bool DeclineReceived(string playerId) => Transition(playerId,
        state => state == WDProfileSocialState.RequestReceived,
        WDProfileSocialState.Stranger);

    public bool RemoveFriend(string playerId) => Transition(playerId,
        state => state == WDProfileSocialState.Friend,
        WDProfileSocialState.Stranger);

    public bool Block(string playerId)
    {
        WDProfileData profile = profiles.Find(playerId);
        if (profile == null || profile.IsLocalPlayer || profile.SocialState == WDProfileSocialState.Blocked)
            return false;
        profile.ApplySocialState(WDProfileSocialState.Blocked);
        Changed?.Invoke();
        return true;
    }

    public bool Unblock(string playerId) => Transition(playerId,
        state => state == WDProfileSocialState.Blocked,
        WDProfileSocialState.Stranger);

    public bool CanStartDirectChat(string playerId, bool allowNonFriends)
    {
        WDProfileData profile = profiles.Find(playerId);
        if (profile == null || profile.SocialState == WDProfileSocialState.Blocked)
            return false;
        return profile.SocialState == WDProfileSocialState.Friend || allowNonFriends;
    }

    private IReadOnlyList<WDProfileData> Filter(WDProfileSocialState state, string query)
    {
        string normalized = query?.Trim();
        return profiles.AllProfiles.Where(profile => profile.SocialState == state &&
            (string.IsNullOrEmpty(normalized) ||
             profile.Nickname.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0 ||
             profile.PlayerId.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0))
            .OrderBy(profile => profile.Nickname, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private bool Transition(string playerId, Func<WDProfileSocialState, bool> allowed,
        WDProfileSocialState destination)
    {
        WDProfileData profile = profiles.Find(playerId);
        if (profile == null || profile.IsLocalPlayer || !allowed(profile.SocialState))
            return false;
        profile.ApplySocialState(destination);
        Changed?.Invoke();
        return true;
    }
}
