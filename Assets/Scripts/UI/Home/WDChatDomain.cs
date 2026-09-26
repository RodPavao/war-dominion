using System;
using System.Collections.Generic;
using System.Linq;

public enum WDChatChannelType { Private, Clan, MatchTeam }
public enum WDChatMessageKind { Player, System }

public sealed class WDChatMessage
{
    public string SenderId { get; }
    public string Text { get; }
    public DateTimeOffset Timestamp { get; }
    public WDChatMessageKind Kind { get; }

    public WDChatMessage(string senderId, string text, DateTimeOffset timestamp,
        WDChatMessageKind kind = WDChatMessageKind.Player)
    {
        SenderId = senderId ?? string.Empty;
        Text = text?.Trim() ?? string.Empty;
        Timestamp = timestamp;
        Kind = kind;
    }
}

public sealed class WDChatChannel
{
    private readonly List<WDChatMessage> messages = new();
    private readonly HashSet<string> participants;

    public string Id { get; private set; }
    public WDChatChannelType Type { get; }
    public string DisplayName { get; private set; }
    public IReadOnlyCollection<string> Participants => participants;
    public IReadOnlyList<WDChatMessage> Messages => messages;
    public bool IsMuted { get; internal set; }
    public bool IsUnread { get; internal set; }
    public bool IsAvailable { get; internal set; } = true;
    public DateTimeOffset LastActivity { get; internal set; }
    public bool IsRecent => Type == WDChatChannelType.Private && messages.Count > 0;

    internal WDChatChannel(string id, WDChatChannelType type, string displayName,
        IEnumerable<string> participantIds)
    {
        Id = id;
        Type = type;
        DisplayName = displayName;
        participants = new HashSet<string>(participantIds ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);
    }

    internal void Add(WDChatMessage message)
    {
        messages.Add(message);
        LastActivity = message.Timestamp;
    }

    internal void ReplaceParticipants(IEnumerable<string> playerIds)
    {
        participants.Clear();
        foreach (string id in playerIds ?? Array.Empty<string>()) participants.Add(id);
    }

    internal void Rename(string value) => DisplayName = value;
    internal void Rekey(string value) => Id = value;

    public bool CanAccess(string playerId) => IsAvailable && participants.Contains(playerId ?? string.Empty);
}

public sealed class WDChatRepository
{
    // ============================================================
    // 01. AUTORIDADE LOCAL ÚNICA E SUBSTITUÍVEL
    // ============================================================

    private readonly Dictionary<string, WDChatChannel> channels =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly WDSocialRepository social;
    private readonly WDProfileRepository profiles;
    private readonly Func<WDSettingsData> settings;
    private readonly Func<DateTimeOffset> clock;

    public event Action Changed;
    public string LocalPlayerId => profiles.LocalProfile.PlayerId;
    public WDProfileRepository Profiles => profiles;
    public WDSocialRepository Social => social;
    public IReadOnlyList<WDChatChannel> RecentPrivate => channels.Values
        .Where(channel => channel.IsRecent)
        .OrderByDescending(channel => channel.LastActivity).ToArray();
    public IReadOnlyList<WDChatChannel> Channels => channels.Values.ToArray();
    public int ExternalUnreadConversationCount => settings().ChatNotifications
        ? channels.Values.Count(channel => channel.IsUnread && !channel.IsMuted && channel.IsAvailable)
        : 0;
    public bool HasExternalAttention(string channelId) => settings().ChatNotifications &&
        channels.TryGetValue(channelId ?? string.Empty, out WDChatChannel channel) &&
        channel.IsAvailable && channel.IsUnread && !channel.IsMuted;

    public WDChatRepository(WDProfileRepository profileRepository,
        WDSocialRepository socialRepository, Func<WDSettingsData> settingsProvider = null,
        Func<DateTimeOffset> timeProvider = null)
    {
        profiles = profileRepository ?? throw new ArgumentNullException(nameof(profileRepository));
        social = socialRepository ?? throw new ArgumentNullException(nameof(socialRepository));
        settings = settingsProvider ?? WDSettingsRepository.LoadPersisted;
        clock = timeProvider ?? (() => DateTimeOffset.UtcNow);
        social.Changed += RefreshPrivateAvailability;
        EnsureClanChannel(profiles.LocalProfile.Clan);
    }

    // ============================================================
    // 02. PRIVADO, RECENTES E PERMISSÕES SOCIAIS
    // ============================================================

    public WDChatChannel OpenPrivate(string otherPlayerId)
    {
        WDProfileData other = profiles.Find(otherPlayerId);
        if (other == null || other.IsLocalPlayer ||
            !social.CanStartDirectChat(otherPlayerId, settings().AllowNonFriendMessages))
            return null;
        string id = PrivateId(LocalPlayerId, otherPlayerId);
        if (!channels.TryGetValue(id, out WDChatChannel channel))
        {
            channel = new WDChatChannel(id, WDChatChannelType.Private, other.Nickname,
                new[] { LocalPlayerId, otherPlayerId });
            channels[id] = channel;
        }
        return channel;
    }

    public bool Send(string channelId, string senderId, string text)
    {
        if (!channels.TryGetValue(channelId ?? string.Empty, out WDChatChannel channel) ||
            !channel.CanAccess(senderId) || string.IsNullOrWhiteSpace(text))
            return false;
        if (channel.Type == WDChatChannelType.Private)
        {
            string other = channel.Participants.First(id =>
                !id.Equals(LocalPlayerId, StringComparison.OrdinalIgnoreCase));
            if (!social.CanStartDirectChat(other, settings().AllowNonFriendMessages))
                return false;
        }
        channel.Add(new WDChatMessage(senderId, text, clock()));
        channel.IsUnread = !senderId.Equals(LocalPlayerId, StringComparison.OrdinalIgnoreCase);
        Changed?.Invoke();
        return true;
    }

    public bool ReceiveLocalMock(string channelId, string senderId, string text) =>
        Send(channelId, senderId, text);

    public void OpenContent(string channelId)
    {
        if (!channels.TryGetValue(channelId ?? string.Empty, out WDChatChannel channel) ||
            !channel.CanAccess(LocalPlayerId))
            return;
        channel.IsUnread = false;
        Changed?.Invoke();
    }

    public bool SetMuted(string channelId, bool muted)
    {
        if (!channels.TryGetValue(channelId ?? string.Empty, out WDChatChannel channel))
            return false;
        channel.IsMuted = muted;
        Changed?.Invoke();
        return true;
    }

    // ============================================================
    // 03. CLÃ MÍNIMO SEM AUTORIDADE PARALELA DE CARGOS
    // ============================================================

    public WDChatChannel ClanChannel => channels.Values.FirstOrDefault(
        channel => channel.Type == WDChatChannelType.Clan && channel.IsAvailable);

    public WDChatChannel EnsureClanChannel(string clanName)
    {
        if (string.IsNullOrWhiteSpace(clanName))
            return null;
        string id = $"clan:{clanName.Trim().ToLowerInvariant()}";
        if (!channels.TryGetValue(id, out WDChatChannel channel))
        {
            channel = new WDChatChannel(id, WDChatChannelType.Clan, clanName,
                profiles.AllProfiles.Where(profile => profile.Clan == clanName)
                    .Select(profile => profile.PlayerId));
            channel.Add(new WDChatMessage(string.Empty, "Você entrou no clã.", clock(),
                WDChatMessageKind.System));
            channels[id] = channel;
        }
        return channel;
    }

    public void LeaveClan()
    {
        WDChatChannel channel = ClanChannel;
        if (channel == null) return;
        channel.Add(new WDChatMessage(string.Empty, "Você saiu do clã.", clock(),
            WDChatMessageKind.System));
        channel.IsAvailable = false;
        channel.IsUnread = false;
        Changed?.Invoke();
    }

    public void SyncClanMembership(WDClanData clan)
    {
        string nextId = clan == null ? null : $"clan:{clan.Id}";
        // 03. Migra o canal legado por nome sem descartar o histórico da sessão.
        if (nextId != null && !channels.ContainsKey(nextId))
        {
            WDChatChannel legacy = channels.Values.FirstOrDefault(item =>
                item.Type == WDChatChannelType.Clan && item.IsAvailable &&
                item.DisplayName.Equals(clan.Name, StringComparison.OrdinalIgnoreCase));
            if (legacy != null)
            {
                channels.Remove(legacy.Id);
                legacy.Rekey(nextId);
                channels[nextId] = legacy;
            }
        }
        string previousId = ClanChannel?.Id;
        bool reentering = nextId != null && channels.ContainsKey(nextId) && previousId != nextId;
        foreach (WDChatChannel existing in channels.Values.Where(item =>
            item.Type == WDChatChannelType.Clan))
        {
            if (existing.IsAvailable && existing.Id != nextId)
                existing.Add(new WDChatMessage(string.Empty, "Você saiu do clã.", clock(),
                    WDChatMessageKind.System));
            existing.IsAvailable = false;
            existing.IsUnread = false;
        }
        if (clan != null && clan.Member(LocalPlayerId) != null)
        {
            if (!channels.TryGetValue(nextId, out WDChatChannel channel))
            {
                channel = new WDChatChannel(nextId, WDChatChannelType.Clan, clan.Name,
                    clan.Members.Select(item => item.PlayerId));
                channel.Add(new WDChatMessage(string.Empty, "Você entrou no clã.", clock(),
                    WDChatMessageKind.System));
                channels[nextId] = channel;
            }
            if (reentering)
                channel.Add(new WDChatMessage(string.Empty, "Você entrou no clã.", clock(),
                    WDChatMessageKind.System));
            channel.Rename(clan.Name);
            channel.ReplaceParticipants(clan.Members.Select(item => item.PlayerId));
            channel.IsAvailable = true;
        }
        Changed?.Invoke();
    }

    // ============================================================
    // 04. CANAL TEMPORÁRIO E ISOLADO DE EQUIPE
    // ============================================================

    // Adaptação da identidade da sessão local à identidade social, sem novo transporte.
    public WDChatChannel CreateLocalMatchChannel(string matchId, WDMatchSetup setup,
        IEnumerable<MatchUIPlayerState> players, TerritorioClique.Dono localOwner, bool usesTeams)
    {
        if (setup != null)
        {
            var local = setup.Participants.FirstOrDefault(p => p.Kind == WDMatchParticipantKind.Local);
            if (!setup.IsTeamMatch || local == null) return null;
            return CreateMatchTeamChannel(matchId, LocalPlayerId, setup.Participants.Select(p =>
                (p == local ? LocalPlayerId : p.PlayerId, p.TeamIndex)));
        }
        if (!usesTeams) return null;
        return CreateMatchTeamChannel(matchId, LocalPlayerId,
            (players ?? Array.Empty<MatchUIPlayerState>())
            .Where(p => p.Equipe != EquipesJogadores.Equipe.Nenhuma)
            .Select(p => (p.Jogador == localOwner ? LocalPlayerId : $"match:{matchId}:slot:{(int)p.Jogador}", (int)p.Equipe)));
    }

    public WDChatChannel CreateMatchTeamChannel(string matchId, string localPlayerId,
        IEnumerable<(string PlayerId, int Team)> participants)
    {
        var list = (participants ?? Array.Empty<(string, int)>()).ToArray();
        if (string.IsNullOrWhiteSpace(matchId) || string.IsNullOrWhiteSpace(localPlayerId) ||
            !list.Any(item => string.Equals(item.PlayerId, localPlayerId,
                StringComparison.OrdinalIgnoreCase)))
            return null;
        var local = list.First(item => string.Equals(item.PlayerId, localPlayerId,
            StringComparison.OrdinalIgnoreCase));
        string[] allies = list.Where(item => item.Team == local.Team)
            .Select(item => item.PlayerId).ToArray();
        if (allies.Length != 2 && allies.Length != 3)
            return null;
        string id = $"match:{matchId}:team:{local.Team}";
        var channel = new WDChatChannel(id, WDChatChannelType.MatchTeam, "PARTIDA", allies);
        channels[id] = channel;
        Changed?.Invoke();
        return channel;
    }

    public void EndMatch(string matchId)
    {
        foreach (WDChatChannel channel in channels.Values.Where(channel =>
            channel.Type == WDChatChannelType.MatchTeam &&
            channel.Id.StartsWith($"match:{matchId}:", StringComparison.OrdinalIgnoreCase)))
        {
            channel.IsAvailable = false;
            channel.IsUnread = false;
        }
        Changed?.Invoke();
    }

    private void RefreshPrivateAvailability()
    {
        bool changed = false;
        foreach (WDChatChannel channel in channels.Values.Where(channel =>
            channel.Type == WDChatChannelType.Private && channel.IsAvailable))
        {
            string other = channel.Participants.First(id =>
                !id.Equals(LocalPlayerId, StringComparison.OrdinalIgnoreCase));
            WDProfileData profile = profiles.Find(other);
            if (profile?.SocialState != WDProfileSocialState.Blocked)
                continue;
            channel.IsAvailable = false;
            channel.IsUnread = false;
            changed = true;
        }
        if (changed)
            Changed?.Invoke();
    }

    private static string PrivateId(string a, string b) => string.Compare(a, b,
        StringComparison.OrdinalIgnoreCase) < 0 ? $"private:{a}:{b}" : $"private:{b}:{a}";
}

public static class WDChatRuntime
{
    public static WDChatRepository Current { get; private set; }
    public static void Initialize(WDChatRepository repository) => Current = repository;
}
