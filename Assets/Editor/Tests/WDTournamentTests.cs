using System;
using System.Linq;
using NUnit.Framework;

public sealed class WDTournamentTests
{
    private WDProfileRepository profiles;

    [SetUp]
    public void SetUp() => profiles = new WDProfileRepository(null, new WDMemoryProfilePrivacyStore());

    // ============================================================
    // 01. LIFECYCLE E EXCLUSIVIDADE OFICIAL
    // ============================================================

    [TestCase(WDTournamentLifecycle.ComingSoon)]
    [TestCase(WDTournamentLifecycle.RegistrationOpen)]
    [TestCase(WDTournamentLifecycle.InProgress)]
    [TestCase(WDTournamentLifecycle.Closed)]
    public void LifecycleStatesAreRepresentable(WDTournamentLifecycle lifecycle)
    {
        Assert.That(Tournament("T", lifecycle).Lifecycle, Is.EqualTo(lifecycle));
    }

    [Test]
    public void RepositoryExposesExactlyOneCurrentOfficialTournament()
    {
        var repository = new WDTournamentRepository(profiles);
        Assert.That(repository.CurrentOfficialTournament, Is.Not.Null);
        Assert.That(repository.All.Count(item => item.Lifecycle == WDTournamentLifecycle.RegistrationOpen ||
            item.Lifecycle == WDTournamentLifecycle.InProgress), Is.EqualTo(1));
    }

    [Test]
    public void TwoCurrentOfficialTournamentsAreRejected()
    {
        Assert.Throws<InvalidOperationException>(() => new WDTournamentRepository(profiles, new[]
        {
            Tournament("A", WDTournamentLifecycle.RegistrationOpen),
            Tournament("B", WDTournamentLifecycle.InProgress)
        }));
    }

    [Test]
    public void UpcomingAndHistoryAreSeparatedFromCurrent()
    {
        var repository = new WDTournamentRepository(profiles);
        Assert.That(repository.Upcoming, Is.Not.Empty);
        Assert.That(repository.History.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(repository.Upcoming.All(item => item.Lifecycle == WDTournamentLifecycle.ComingSoon), Is.True);
        Assert.That(repository.History.All(item => item.Lifecycle == WDTournamentLifecycle.Closed), Is.True);
    }

    // ============================================================
    // 02. INSCRIÇÃO E FORMATOS
    // ============================================================

    [Test]
    public void OneVsOneAcceptsIndividualRegistration()
    {
        WDTournament tournament = Tournament("solo", WDTournamentLifecycle.RegistrationOpen);
        var repository = new WDTournamentRepository(profiles, new[] { tournament });
        Assert.That(repository.Register(tournament.Id, profiles.LocalProfile.PlayerId),
            Is.EqualTo(WDTournamentRegistrationResult.Success));
        Assert.That(tournament.ParticipationOf(profiles.LocalProfile.PlayerId),
            Is.EqualTo(WDTournamentParticipation.Registered));
    }

    [Test]
    public void PlayerCannotRegisterTwiceInTheCurrentTournament()
    {
        WDTournament tournament = Tournament("solo", WDTournamentLifecycle.RegistrationOpen);
        var repository = new WDTournamentRepository(profiles, new[] { tournament });
        Assert.That(repository.Register(tournament.Id, profiles.LocalProfile.PlayerId),
            Is.EqualTo(WDTournamentRegistrationResult.Success));
        Assert.That(repository.Register(tournament.Id, profiles.LocalProfile.PlayerId),
            Is.EqualTo(WDTournamentRegistrationResult.AlreadyParticipating));
    }

    [TestCase(WDTournamentFormat.TwoVsTwo, 2)]
    [TestCase(WDTournamentFormat.ThreeVsThree, 3)]
    public void TeamTournamentAcceptsOnlyCompletePreformedTeam(WDTournamentFormat format, int size)
    {
        WDTournament tournament = Tournament("team", WDTournamentLifecycle.RegistrationOpen, format);
        var repository = new WDTournamentRepository(profiles, new[] { tournament });
        string[] members = new[] { profiles.LocalProfile.PlayerId }.Concat(profiles.AllProfiles
            .Where(item => !item.IsLocalPlayer).Take(size - 1).Select(item => item.PlayerId)).ToArray();
        Assert.That(repository.Register(tournament.Id, profiles.LocalProfile.PlayerId, members),
            Is.EqualTo(WDTournamentRegistrationResult.Success));
        Assert.That(tournament.Entries[0].MemberPlayerIds.Count, Is.EqualTo(size));
    }

    [TestCase(WDTournamentFormat.TwoVsTwo)]
    [TestCase(WDTournamentFormat.ThreeVsThree)]
    public void IncompleteTeamIsRejectedWithoutMatchmaking(WDTournamentFormat format)
    {
        WDTournament tournament = Tournament("team", WDTournamentLifecycle.RegistrationOpen, format);
        var repository = new WDTournamentRepository(profiles, new[] { tournament });
        Assert.That(repository.Register(tournament.Id, profiles.LocalProfile.PlayerId,
            new[] { profiles.LocalProfile.PlayerId }),
            Is.EqualTo(WDTournamentRegistrationResult.IncompleteTeam));
    }

    [TestCase(WDTournamentLifecycle.ComingSoon)]
    [TestCase(WDTournamentLifecycle.InProgress)]
    [TestCase(WDTournamentLifecycle.Closed)]
    public void RegistrationOutsideOpenWindowIsRejected(WDTournamentLifecycle lifecycle)
    {
        WDTournament tournament = Tournament("closed", lifecycle);
        var repository = new WDTournamentRepository(profiles, new[] { tournament });
        Assert.That(repository.Register(tournament.Id, profiles.LocalProfile.PlayerId),
            Is.EqualTo(WDTournamentRegistrationResult.RegistrationClosed));
    }

    [Test]
    public void ThreeVsThreeRandomCardsIsInvalid()
    {
        Assert.Throws<ArgumentException>(() => new WDTournament("invalid", "INVALID", string.Empty,
            WDTournamentLifecycle.ComingSoon, WDTournamentFormat.ThreeVsThree,
            WDTournamentCardMode.RandomCards, "CLASSIC", 8));
    }

    [Test]
    public void StartedTournamentLocksRegisteredComposition()
    {
        WDTournament tournament = StartedTournament(4);
        Assert.That(tournament.Entries.All(entry => entry.CompositionLocked), Is.True);
    }

    [TestCase(WDTournamentFormat.OneVsOne, WDTournamentCardMode.Normal)]
    [TestCase(WDTournamentFormat.OneVsOne, WDTournamentCardMode.RandomCards)]
    [TestCase(WDTournamentFormat.TwoVsTwo, WDTournamentCardMode.Normal)]
    [TestCase(WDTournamentFormat.TwoVsTwo, WDTournamentCardMode.RandomCards)]
    [TestCase(WDTournamentFormat.ThreeVsThree, WDTournamentCardMode.Normal)]
    public void ValidFormatAndCardModeCombinationsArePreserved(
        WDTournamentFormat format, WDTournamentCardMode mode)
    {
        var tournament = new WDTournament("valid", "VALID", string.Empty,
            WDTournamentLifecycle.ComingSoon, format, mode, "CLASSIC", 8);
        Assert.That(tournament.Format, Is.EqualTo(format));
        Assert.That(tournament.CardMode, Is.EqualTo(mode));
    }

    // ============================================================
    // 03. CHAVEAMENTO, RESULTADOS E JOGADOR LOCAL
    // ============================================================

    [TestCase(4, 3)]
    [TestCase(8, 7)]
    public void BracketSizeIsDerivedFromCapacity(int capacity, int expectedMatches)
    {
        WDTournament tournament = StartedTournament(capacity);
        Assert.That(tournament.Matches.Count, Is.EqualTo(expectedMatches));
        Assert.That(tournament.Matches.Max(item => item.RoundIndex) + 1,
            Is.EqualTo((int)Math.Log(capacity, 2)));
    }

    [Test]
    public void WinnerProgressesAndLoserIsEliminated()
    {
        WDTournament tournament = StartedTournament(4);
        WDTournamentEntry winner = tournament.Entries[0];
        WDTournamentEntry loser = tournament.Entries[1];
        tournament.CompleteMatch("r1-m1", winner.Id, "2-1");
        Assert.That(tournament.ParticipationOf(winner.MemberPlayerIds[0]),
            Is.EqualTo(WDTournamentParticipation.Qualified));
        Assert.That(tournament.ParticipationOf(loser.MemberPlayerIds[0]),
            Is.EqualTo(WDTournamentParticipation.Eliminated));
        Assert.That(tournament.Matches.Single(item => item.Id == "r2-m1").FirstEntryId,
            Is.EqualTo(winner.Id));
    }

    [Test]
    public void FinalWinnerBecomesChampion()
    {
        WDTournament tournament = StartedTournament(4);
        tournament.CompleteMatch("r1-m1", tournament.Entries[0].Id, "2-0");
        tournament.CompleteMatch("r1-m2", tournament.Entries[2].Id, "2-1");
        tournament.CompleteMatch("r2-m1", tournament.Entries[0].Id, "2-1");
        Assert.That(tournament.ChampionEntryId, Is.EqualTo(tournament.Entries[0].Id));
        Assert.That(tournament.ParticipationOf(tournament.Entries[0].MemberPlayerIds[0]),
            Is.EqualTo(WDTournamentParticipation.Champion));
    }

    [Test]
    public void PrototypeCurrentPlayerHasNextMatchAndCompetingState()
    {
        var repository = new WDTournamentRepository(profiles);
        WDTournament current = repository.CurrentOfficialTournament;
        Assert.That(current.ParticipationOf(profiles.LocalProfile.PlayerId),
            Is.EqualTo(WDTournamentParticipation.Competing));
        Assert.That(repository.NextMatchFor(current.Id, profiles.LocalProfile.PlayerId), Is.Not.Null);
    }

    [Test]
    public void ClosedTournamentIsReadOnly()
    {
        WDTournament closed = new WDTournamentRepository(profiles).History[0];
        Assert.That(closed.IsReadOnly, Is.True);
        Assert.Throws<InvalidOperationException>(() =>
            closed.CompleteMatch(closed.Matches[0].Id, closed.Matches[0].FirstEntryId, "1-0"));
    }

    // ============================================================
    // 04. CAPABILITIES E RECOMPENSAS SEM IMPLEMENTAÇÃO FALSA
    // ============================================================

    [Test]
    public void SpectatorCapabilityDoesNotCreateSpectatorImplementation()
    {
        WDTournament tournament = Tournament("future", WDTournamentLifecycle.ComingSoon);
        Assert.That(tournament.SpectatorCapabilityPlanned, Is.True);
        Assert.That(tournament.MatchEntryIntentAvailable, Is.False);
    }

    [Test]
    public void RewardsHaveNoInventedAmountsOrPlacementRules()
    {
        WDTournament current = new WDTournamentRepository(profiles).CurrentOfficialTournament;
        Assert.That(current.Rewards, Is.Not.Empty);
        Assert.That(current.Rewards.All(item => !item.Quantity.HasValue), Is.True);
        Assert.That(current.Rewards.All(item => item.PlacementRule == null), Is.True);
    }

    private WDTournament Tournament(string id, WDTournamentLifecycle lifecycle,
        WDTournamentFormat format = WDTournamentFormat.OneVsOne) =>
        new(id, id.ToUpperInvariant(), string.Empty, lifecycle, format,
            WDTournamentCardMode.Normal, "CLASSIC", 8);

    private WDTournament StartedTournament(int capacity)
    {
        WDTournamentEntry[] entries = profiles.AllProfiles.Take(capacity).Select((profile, index) =>
            new WDTournamentEntry($"e{index}", profile.Nickname, new[] { profile.PlayerId })).ToArray();
        var tournament = new WDTournament("started", "STARTED", string.Empty,
            WDTournamentLifecycle.InProgress, WDTournamentFormat.OneVsOne,
            WDTournamentCardMode.Normal, "CLASSIC", capacity, entries);
        return tournament;
    }
}
