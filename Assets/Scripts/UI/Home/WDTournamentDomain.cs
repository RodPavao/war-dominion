using System;
using System.Collections.Generic;
using System.Linq;

public enum WDTournamentLifecycle { ComingSoon, RegistrationOpen, InProgress, Closed }
public enum WDTournamentParticipation { NotRegistered, Registered, Competing, Qualified, Eliminated, Champion }
public enum WDTournamentFormat { OneVsOne = 1, TwoVsTwo = 2, ThreeVsThree = 3 }
public enum WDTournamentCardMode { Normal, RandomCards }
public enum WDTournamentMatchState { Pending, Next, InProgress, Completed }
public enum WDTournamentRewardType { Coins, Emeralds, WeaponTokens, Cosmetic }
public enum WDTournamentRegistrationResult
{
    Success, TournamentNotFound, RegistrationClosed, AlreadyParticipating,
    IncompleteTeam, InvalidMember, CapacityReached
}

// ============================================================
// 01. PARTICIPANTES, PARTIDAS E RECOMPENSAS FUTURAS
// ============================================================

public sealed class WDTournamentEntry
{
    public string Id { get; }
    public string DisplayName { get; }
    public IReadOnlyList<string> MemberPlayerIds { get; }
    public bool CompositionLocked { get; internal set; }

    public WDTournamentEntry(string id, string displayName, IEnumerable<string> memberPlayerIds)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        DisplayName = displayName ?? string.Empty;
        MemberPlayerIds = (memberPlayerIds ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray();
    }
}

public sealed class WDTournamentMatch
{
    public string Id { get; }
    public int RoundIndex { get; }
    public int SlotIndex { get; }
    public string FirstEntryId { get; internal set; }
    public string SecondEntryId { get; internal set; }
    public WDTournamentMatchState State { get; internal set; }
    public string WinnerEntryId { get; internal set; }
    public string Score { get; internal set; }
    public string EntryWindow { get; internal set; }

    internal WDTournamentMatch(string id, int roundIndex, int slotIndex,
        string firstEntryId, string secondEntryId)
    {
        Id = id; RoundIndex = roundIndex; SlotIndex = slotIndex;
        FirstEntryId = firstEntryId; SecondEntryId = secondEntryId;
        State = string.IsNullOrEmpty(firstEntryId) || string.IsNullOrEmpty(secondEntryId)
            ? WDTournamentMatchState.Pending : WDTournamentMatchState.Next;
    }
}

public sealed class WDTournamentRewardDefinition
{
    public WDTournamentRewardType Type { get; }
    public int? Quantity { get; }
    public string PlacementRule { get; }
    public WDTournamentRewardDefinition(WDTournamentRewardType type, int? quantity = null,
        string placementRule = null)
    { Type = type; Quantity = quantity; PlacementRule = placementRule; }
}

// ============================================================
// 02. TORNEIO OFICIAL E INVARIANTES DE CONFIGURAÇÃO
// ============================================================

public sealed class WDTournament
{
    private readonly List<WDTournamentEntry> entries;
    private readonly List<WDTournamentMatch> matches;
    private readonly Dictionary<string, WDTournamentParticipation> participation =
        new(StringComparer.Ordinal);

    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public WDTournamentLifecycle Lifecycle { get; internal set; }
    public WDTournamentFormat Format { get; }
    public WDTournamentCardMode CardMode { get; }
    public string MapRule { get; }
    public int Capacity { get; }
    public string Period { get; }
    public string Rules { get; }
    public bool SpectatorCapabilityPlanned { get; }
    public bool DeckSelectionIntentAvailable => CardMode == WDTournamentCardMode.Normal;
    public bool MatchEntryIntentAvailable => Lifecycle == WDTournamentLifecycle.InProgress;
    public IReadOnlyList<WDTournamentEntry> Entries => entries;
    public IReadOnlyList<WDTournamentMatch> Matches => matches;
    public IReadOnlyList<WDTournamentRewardDefinition> Rewards { get; }
    public string ChampionEntryId { get; internal set; }
    public bool IsReadOnly => Lifecycle == WDTournamentLifecycle.Closed;
    public int TeamSize => (int)Format;

    public WDTournament(string id, string name, string description,
        WDTournamentLifecycle lifecycle, WDTournamentFormat format,
        WDTournamentCardMode cardMode, string mapRule, int capacity,
        IEnumerable<WDTournamentEntry> initialEntries = null, string period = null,
        string rules = null, bool spectatorCapabilityPlanned = true,
        IEnumerable<WDTournamentRewardDefinition> rewards = null)
    {
        if (format == WDTournamentFormat.ThreeVsThree && cardMode == WDTournamentCardMode.RandomCards)
            throw new ArgumentException("3V3 não permite Random Cards.", nameof(cardMode));
        if (capacity < 2 || (capacity & (capacity - 1)) != 0)
            throw new ArgumentException("A capacidade deve ser uma potência de dois.", nameof(capacity));
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? string.Empty; Description = description ?? string.Empty;
        Lifecycle = lifecycle; Format = format; CardMode = cardMode;
        MapRule = mapRule ?? string.Empty; Capacity = capacity;
        Period = period ?? string.Empty; Rules = rules ?? string.Empty;
        SpectatorCapabilityPlanned = spectatorCapabilityPlanned;
        entries = (initialEntries ?? Array.Empty<WDTournamentEntry>()).ToList();
        if (entries.Any(entry => entry.MemberPlayerIds.Count != TeamSize))
            throw new ArgumentException("A composição inicial não corresponde ao formato.", nameof(initialEntries));
        if (entries.Count > capacity)
            throw new ArgumentException("Participantes excedem a capacidade.", nameof(initialEntries));
        matches = lifecycle == WDTournamentLifecycle.RegistrationOpen || entries.Count == 0
            ? new List<WDTournamentMatch>() : BuildBracket(entries, capacity);
        Rewards = (rewards ?? Array.Empty<WDTournamentRewardDefinition>()).ToArray();
        if (lifecycle == WDTournamentLifecycle.InProgress || lifecycle == WDTournamentLifecycle.Closed)
            foreach (WDTournamentEntry entry in entries) entry.CompositionLocked = true;
    }

    public WDTournamentParticipation ParticipationOf(string playerId) =>
        participation.TryGetValue(playerId ?? string.Empty, out WDTournamentParticipation state)
            ? state : WDTournamentParticipation.NotRegistered;

    internal void SetParticipation(string playerId, WDTournamentParticipation state) =>
        participation[playerId] = state;

    internal bool AddEntry(WDTournamentEntry entry)
    {
        if (entries.Count >= Capacity || entry.MemberPlayerIds.Count != TeamSize) return false;
        entries.Add(entry); return true;
    }

    public void CompleteMatch(string matchId, string winnerEntryId, string score)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Torneio encerrado é somente leitura.");
        WDTournamentMatch match = matches.FirstOrDefault(item => item.Id == matchId)
            ?? throw new ArgumentException("Confronto não encontrado.", nameof(matchId));
        if (match.State == WDTournamentMatchState.Completed ||
            (winnerEntryId != match.FirstEntryId && winnerEntryId != match.SecondEntryId))
            throw new InvalidOperationException("Resultado inválido para o confronto.");
        match.State = WDTournamentMatchState.Completed;
        match.WinnerEntryId = winnerEntryId; match.Score = score ?? string.Empty;
        string loser = winnerEntryId == match.FirstEntryId ? match.SecondEntryId : match.FirstEntryId;
        SetEntryMembers(loser, WDTournamentParticipation.Eliminated);
        if (match.RoundIndex == RoundCount(Capacity) - 1)
        {
            ChampionEntryId = winnerEntryId;
            SetEntryMembers(winnerEntryId, WDTournamentParticipation.Champion);
            return;
        }
        SetEntryMembers(winnerEntryId, WDTournamentParticipation.Qualified);
        WDTournamentMatch next = matches.First(item => item.RoundIndex == match.RoundIndex + 1 &&
            item.SlotIndex == match.SlotIndex / 2);
        if (match.SlotIndex % 2 == 0) next.FirstEntryId = winnerEntryId;
        else next.SecondEntryId = winnerEntryId;
        if (!string.IsNullOrEmpty(next.FirstEntryId) && !string.IsNullOrEmpty(next.SecondEntryId))
            next.State = WDTournamentMatchState.Next;
    }

    internal void SetEntryMembers(string entryId, WDTournamentParticipation state)
    {
        WDTournamentEntry entry = entries.FirstOrDefault(item => item.Id == entryId);
        if (entry == null) return;
        foreach (string playerId in entry.MemberPlayerIds) SetParticipation(playerId, state);
    }

    private static List<WDTournamentMatch> BuildBracket(IReadOnlyList<WDTournamentEntry> entrants,
        int capacity)
    {
        var result = new List<WDTournamentMatch>();
        int rounds = RoundCount(capacity);
        for (int round = 0; round < rounds; round++)
        {
            int matchCount = capacity >> (round + 1);
            for (int slot = 0; slot < matchCount; slot++)
            {
                string first = round == 0 && slot * 2 < entrants.Count ? entrants[slot * 2].Id : null;
                string second = round == 0 && slot * 2 + 1 < entrants.Count ? entrants[slot * 2 + 1].Id : null;
                result.Add(new WDTournamentMatch($"r{round + 1}-m{slot + 1}", round, slot, first, second));
            }
        }
        return result;
    }

    private static int RoundCount(int capacity)
    { int rounds = 0; while (capacity > 1) { capacity >>= 1; rounds++; } return rounds; }
}

// ============================================================
// 03. AUTORIDADE ÚNICA, INSCRIÇÃO E PROGRESSÃO
// ============================================================

public sealed class WDTournamentRepository
{
    private readonly List<WDTournament> tournaments;
    private readonly WDProfileRepository profiles;

    public IReadOnlyList<WDTournament> All => tournaments;
    public WDTournament CurrentOfficialTournament => tournaments.SingleOrDefault(IsCurrent);
    public IReadOnlyList<WDTournament> Upcoming => tournaments.Where(item =>
        item.Lifecycle == WDTournamentLifecycle.ComingSoon).ToArray();
    public IReadOnlyList<WDTournament> History => tournaments.Where(item =>
        item.Lifecycle == WDTournamentLifecycle.Closed).ToArray();

    public WDTournamentRepository(WDProfileRepository profileRepository,
        IEnumerable<WDTournament> data = null)
    {
        profiles = profileRepository ?? throw new ArgumentNullException(nameof(profileRepository));
        tournaments = (data ?? CreatePrototype(profiles)).ToList();
        if (tournaments.Count(IsCurrent) > 1)
            throw new InvalidOperationException("Somente um Torneio Oficial pode estar vigente.");
    }

    public WDTournament Find(string id) => tournaments.FirstOrDefault(item => item.Id == id);

    public WDTournamentRegistrationResult Register(string tournamentId, string localPlayerId,
        IEnumerable<string> formedTeamPlayerIds = null)
    {
        WDTournament tournament = Find(tournamentId);
        if (tournament == null) return WDTournamentRegistrationResult.TournamentNotFound;
        if (tournament.Lifecycle != WDTournamentLifecycle.RegistrationOpen)
            return WDTournamentRegistrationResult.RegistrationClosed;
        if (tournaments.Any(item => IsCurrent(item) &&
            item.ParticipationOf(localPlayerId) != WDTournamentParticipation.NotRegistered))
            return WDTournamentRegistrationResult.AlreadyParticipating;
        string[] members = tournament.TeamSize == 1
            ? new[] { localPlayerId }
            : (formedTeamPlayerIds ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray();
        if (members.Length != tournament.TeamSize || !members.Contains(localPlayerId))
            return WDTournamentRegistrationResult.IncompleteTeam;
        if (members.Any(id => profiles.Find(id) == null))
            return WDTournamentRegistrationResult.InvalidMember;
        var entry = new WDTournamentEntry($"entry-{tournament.Entries.Count + 1}",
            tournament.TeamSize == 1 ? profiles.Find(localPlayerId).Nickname :
            string.Join(" / ", members.Select(id => profiles.Find(id).Nickname)), members);
        if (!tournament.AddEntry(entry)) return WDTournamentRegistrationResult.CapacityReached;
        tournament.SetParticipation(localPlayerId, WDTournamentParticipation.Registered);
        return WDTournamentRegistrationResult.Success;
    }

    public void CompleteMatch(string tournamentId, string matchId, string winnerEntryId, string score) =>
        Find(tournamentId)?.CompleteMatch(matchId, winnerEntryId, score);

    public WDTournamentMatch NextMatchFor(string tournamentId, string playerId)
    {
        WDTournament tournament = Find(tournamentId);
        string entryId = tournament?.Entries.FirstOrDefault(entry =>
            entry.MemberPlayerIds.Contains(playerId))?.Id;
        return tournament?.Matches.FirstOrDefault(match => match.State != WDTournamentMatchState.Completed &&
            (match.FirstEntryId == entryId || match.SecondEntryId == entryId));
    }

    private static bool IsCurrent(WDTournament item) =>
        item.Lifecycle == WDTournamentLifecycle.RegistrationOpen ||
        item.Lifecycle == WDTournamentLifecycle.InProgress;

    // ============================================================
    // 04. DATASET LOCAL/MOCK SUBSTITUÍVEL
    // ============================================================

    private static IEnumerable<WDTournament> CreatePrototype(WDProfileRepository profiles)
    {
        string local = profiles.LocalProfile.PlayerId;
        string[] ids = profiles.AllProfiles.Select(profile => profile.PlayerId).ToArray();
        WDTournamentEntry[] teams = Enumerable.Range(0, 4).Select(index =>
            new WDTournamentEntry($"team-{index + 1}", new[] { "VANGUARD", "IRON WOLVES", "NIGHT RAIDERS", "SENTINEL" }[index],
                new[] { ids[index * 2 % ids.Length], ids[(index * 2 + 1) % ids.Length] })).ToArray();
        var current = new WDTournament("generals-cup", "COPA DOS GENERAIS",
            "Torneio Oficial local de demonstração.", WDTournamentLifecycle.InProgress,
            WDTournamentFormat.TwoVsTwo, WDTournamentCardMode.Normal, "CLASSIC", 4, teams,
            "SET/2026", "Eliminação simples. Composição travada após as inscrições.", true,
            new[] { new WDTournamentRewardDefinition(WDTournamentRewardType.Coins),
                new WDTournamentRewardDefinition(WDTournamentRewardType.Emeralds),
                new WDTournamentRewardDefinition(WDTournamentRewardType.WeaponTokens) });
        current.SetEntryMembers("team-1", WDTournamentParticipation.Competing);
        current.Matches[0].State = WDTournamentMatchState.Completed;
        current.Matches[0].WinnerEntryId = "team-1"; current.Matches[0].Score = "2-1";
        current.SetEntryMembers("team-2", WDTournamentParticipation.Eliminated);
        current.Matches[1].State = WDTournamentMatchState.Completed;
        current.Matches[1].WinnerEntryId = "team-3"; current.Matches[1].Score = "2-0";
        current.SetEntryMembers("team-4", WDTournamentParticipation.Eliminated);
        WDTournamentMatch final = current.Matches.First(match => match.RoundIndex == 1);
        final.FirstEntryId = "team-1"; final.SecondEntryId = "team-3";
        final.State = WDTournamentMatchState.Next; final.EntryWindow = "18H RESTANTES";

        yield return current;
        yield return new WDTournament("frontier-cup", "COPA DA FRONTEIRA",
            "Torneio individual anunciado.", WDTournamentLifecycle.ComingSoon,
            WDTournamentFormat.OneVsOne, WDTournamentCardMode.RandomCards, "MAPA A DEFINIR", 8,
            period: "OUT/2026");
        yield return Closed("legacy-cup", "COPA DA LEGACIA", ids, "AGO/2026");
        yield return Closed("dominion-cup", "CAMPEONATO DOMINION", ids.Reverse().ToArray(), "JUL/2026");
    }

    private static WDTournament Closed(string id, string name, string[] ids, string period)
    {
        WDTournamentEntry[] entries = Enumerable.Range(0, 4).Select(index =>
            new WDTournamentEntry($"{id}-p{index + 1}", $"COMPETIDOR {index + 1}",
                new[] { ids[index % ids.Length] })).ToArray();
        var tournament = new WDTournament(id, name, "Histórico local somente leitura.",
            WDTournamentLifecycle.InProgress, WDTournamentFormat.OneVsOne,
            WDTournamentCardMode.Normal, "CLASSIC", 4, entries, period,
            "Eliminação simples.");
        tournament.CompleteMatch("r1-m1", entries[0].Id, "2-0");
        tournament.CompleteMatch("r1-m2", entries[2].Id, "2-1");
        tournament.CompleteMatch("r2-m1", entries[0].Id, "2-1");
        tournament.Lifecycle = WDTournamentLifecycle.Closed;
        return tournament;
    }
}
