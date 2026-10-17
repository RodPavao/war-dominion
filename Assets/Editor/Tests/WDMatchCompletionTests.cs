using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class WDMatchCompletionTests
{
    private sealed class MemoryStore : IWDLocalMatchProgressStore
    {
        private string json;
        public WDLocalMatchProgress Load() => string.IsNullOrEmpty(json)
            ? new WDLocalMatchProgress() : JsonUtility.FromJson<WDLocalMatchProgress>(json);
        public void Save(WDLocalMatchProgress progress) => json = JsonUtility.ToJson(progress);
    }

    // 01. Recompensa canônica por modo, equipe e colocação.
    [Test]
    public void NewPlayerStartsWithZeroTrophiesAndNoMatches()
    {
        var service = new WDLocalMatchResultService(new MemoryStore());
        var profiles = new WDProfileRepository(null, new WDMemoryProfilePrivacyStore());
        profiles.ApplyLocalMatchProgress(service.Progress);
        Assert.That(service.Progress.trophies, Is.Zero);
        Assert.That(service.Progress.matches, Is.Zero);
        Assert.That(service.Progress.history, Is.Empty);
        Assert.That(profiles.LocalProfile.DivisionPoints, Is.Zero);
        Assert.That(new WDRankingRepository(profiles).Query(new WDRankingQuery())
            .LocalPlayer.Points, Is.Zero);
    }

    [TestCase("normal")]
    [TestCase("random_cards")]
    public void OneVsOneWinnerGetsFortyRegardlessOfCardRule(string submode)
    {
        WDMatchCompletion result = Complete("1x1", submode, 0, 0);
        Assert.That(result.LocalTrophies, Is.EqualTo(40));
        Assert.That(result.IsLocalWinner, Is.True);
        Assert.That(result.TrophiesFor(Participant(1, 1, false)), Is.Zero);
    }

    [Test]
    public void OneVsOneDefeatGrantsZero()
    {
        WDMatchCompletion result = Complete("1x1", "normal", 1, 0);
        Assert.That(result.LocalTrophies, Is.Zero);
        Assert.That(result.IsLocalWinner, Is.False);
    }

    [Test]
    public void FinalDrawGrantsZeroAndDoesNotCountAsDefeat()
    {
        var service = new WDLocalMatchResultService(new MemoryStore());
        WDMatchCompletion draw = Complete("1x1", "normal", 0, 0, true);
        Assert.That(service.TryApply(draw), Is.True);
        var profiles = new WDProfileRepository(null, new WDMemoryProfilePrivacyStore());
        profiles.ApplyLocalMatchProgress(service.Progress);
        Assert.That(service.Progress.trophies, Is.Zero);
        Assert.That(profiles.LocalProfile.Statistics.Matches, Is.EqualTo(1));
        Assert.That(profiles.LocalProfile.Statistics.Draws, Is.EqualTo(1));
        Assert.That(profiles.LocalProfile.Statistics.Losses, Is.Zero);
        Assert.That(profiles.LocalProfile.History.Single().Result, Is.EqualTo("EMPATE"));
    }

    [TestCase("normal")]
    [TestCase("random_cards")]
    public void TwoVsTwoRewardsEveryWinnerAndNoLoser(string submode)
    {
        WDMatchCompletion result = Complete("2x2", submode, 0, 0);
        Assert.That(result.TrophiesFor(Participant(0, 0, true)), Is.EqualTo(40));
        Assert.That(result.TrophiesFor(Participant(2, 0, false)), Is.EqualTo(40));
        Assert.That(result.TrophiesFor(Participant(1, 1, false)), Is.Zero);
        Assert.That(result.TrophiesFor(Participant(3, 1, false)), Is.Zero);
    }

    [Test]
    public void TwoVsTwoLocalDefeatGrantsZero()
    {
        Assert.That(Complete("2x2", "normal", 1, 0).LocalTrophies, Is.Zero);
    }

    [TestCase(0, 1, 40)]
    [TestCase(1, 2, 0)]
    [TestCase(2, 3, 0)]
    [TestCase(3, 4, 0)]
    public void FfaPreservesPlacementAndRewardsOnlyFirst(int localSlot,
        int expectedPlacement, int expectedTrophies)
    {
        WDMatchCompletion result = Complete("ffa", "normal", localSlot, 0);
        Assert.That(result.Placement, Is.EqualTo(expectedPlacement));
        Assert.That(result.LocalTrophies, Is.EqualTo(expectedTrophies));
    }

    // 02. Uma publicação por identidade; histórico limitado e estatísticas acumuladas.
    [Test]
    public void TwoWinsAccumulateAndLaterDefeatDoesNotSubtract()
    {
        var service = new WDLocalMatchResultService(new MemoryStore());
        Assert.That(service.TryApply(Complete("1x1", "normal", 0, 0)), Is.True);
        Assert.That(service.TryApply(Complete("1x1", "normal", 0, 0)), Is.True);
        Assert.That(service.Progress.trophies, Is.EqualTo(80));
        Assert.That(service.TryApply(Complete("1x1", "normal", 1, 0)), Is.True);
        Assert.That(service.Progress.trophies, Is.EqualTo(80));
        Assert.That(service.Progress.matches, Is.EqualTo(3));
        Assert.That(service.Progress.wins, Is.EqualTo(2));
        Assert.That(service.Progress.matches - service.Progress.wins, Is.EqualTo(1));
        Assert.That(new WDProfileStats(service.Progress.matches, service.Progress.wins).WinRate,
            Is.EqualTo(200f / 3f).Within(0.01f));
    }

    [Test]
    public void RepeatedResultDoesNotDuplicateTrophiesHistoryOrMissionAfterReopen()
    {
        var store = new MemoryStore();
        var first = new WDLocalMatchResultService(store);
        WDMatchCompletion result = Complete("1x1", "normal", 0, 0);
        Assert.That(first.TryApply(result), Is.True);
        Assert.That(first.TryApply(result), Is.False);
        var reopened = new WDLocalMatchResultService(store);
        Assert.That(reopened.TryApply(result), Is.False);
        Assert.That(reopened.Progress.trophies, Is.EqualTo(40));
        Assert.That(reopened.Progress.history, Has.Count.EqualTo(1));
        Assert.That(reopened.Progress.processedResultIds, Has.Count.EqualTo(1));
        Assert.That(reopened.Missions.Progress("first-win").CompletionCount, Is.EqualTo(1));
        Assert.That(reopened.Missions.Progress("mode-wins").Current, Is.Zero);
    }

    [Test]
    public void ClearingTransientSetupCannotReapplyAlreadyPersistedResult()
    {
        WDMatchSetup previous = WDMatchSetupContext.Current;
        try
        {
            var store = new MemoryStore();
            WDMatchCompletion result = Complete("1x1", "normal", 0, 0);
            Assert.That(new WDLocalMatchResultService(store).TryApply(result), Is.True);
            WDMatchSetupContext.Clear();
            var reopened = new WDLocalMatchResultService(store);
            Assert.That(reopened.TryApply(result), Is.False);
            Assert.That(reopened.Progress.trophies, Is.EqualTo(40));
        }
        finally
        {
            if (previous == null) WDMatchSetupContext.Clear();
            else WDMatchSetupContext.Store(previous);
        }
    }

    [Test]
    public void EleventhMatchDiscardsOldestButKeepsIdempotencyAndAggregateStats()
    {
        var service = new WDLocalMatchResultService(new MemoryStore());
        WDMatchCompletion oldest = Complete("1x1", "normal", 0, 0);
        service.TryApply(oldest);
        for (int i = 0; i < 10; i++)
            service.TryApply(Complete("1x1", "normal", 1, 0));
        Assert.That(service.Progress.history, Has.Count.EqualTo(10));
        Assert.That(service.Progress.history.Any(item => item.resultId == oldest.ResultId), Is.False);
        Assert.That(service.Progress.matches, Is.EqualTo(11));
        Assert.That(service.Progress.wins, Is.EqualTo(1));
        Assert.That(service.TryApply(oldest), Is.False);
    }

    [Test]
    public void FfaPlacementSurvivesHistoryPersistence()
    {
        var store = new MemoryStore();
        new WDLocalMatchResultService(store).TryApply(Complete("ffa", "normal", 2, 0));
        WDLocalMatchRecord record = new WDLocalMatchResultService(store).Progress.history.Single();
        Assert.That(record.placement, Is.EqualTo(3));
        Assert.That(record.victory, Is.False);
        Assert.That(record.trophiesEarned, Is.Zero);
    }

    // 03. Perfil e Ranking consomem a mesma projeção; apenas missão comprovável avança.
    [Test]
    public void ProfileAndGeneralRankingReflectPersistedTrophiesAndHistory()
    {
        var store = new MemoryStore();
        var service = new WDLocalMatchResultService(store);
        service.TryApply(Complete("1x1", "normal", 0, 0));
        WDProfileRepository profiles = new WDProfileRepository(null,
            new WDMemoryProfilePrivacyStore());
        profiles.ApplyLocalMatchProgress(new WDLocalMatchResultService(store).Progress);
        Assert.That(profiles.LocalProfile.DivisionPoints, Is.EqualTo(40));
        Assert.That(profiles.LocalProfile.Statistics.Matches, Is.EqualTo(1));
        Assert.That(profiles.LocalProfile.Statistics.Wins, Is.EqualTo(1));
        Assert.That(profiles.LocalProfile.History.Count, Is.EqualTo(1));
        var rankingRepository = new WDRankingRepository(profiles);
        WDRankingResult ranking = rankingRepository.Query(new WDRankingQuery());
        Assert.That(ranking.LocalPlayer.Points, Is.EqualTo(40));
        Assert.That(rankingRepository.Query(new WDRankingQuery
        {
            Scope = WDRankingScope.Season,
            Season = rankingRepository.CurrentSeason
        }).LocalPlayer.Points, Is.EqualTo(40));
    }

    // 04. A origem da partida protege ranking, missões e recompensas.
    [Test]
    public void FriendlyCompletionNeverAwardsCompetitiveProgress()
    {
        var store = new MemoryStore();
        var service = new WDLocalMatchResultService(store);
        WDMatchCompletion result = Complete("1x1", "normal", 0, 0, false,
            WDMatchOrigin.Friendly);
        Assert.That(result, Is.Not.Null);
        Assert.That(result.AwardsCompetitiveProgress, Is.False);
        Assert.That(result.LocalTrophies, Is.Zero);
        Assert.That(result.TrophiesFor(Participant(0, 0, true)), Is.Zero);
        Assert.That(service.TryApply(result), Is.False);
        Assert.That(service.Progress.trophies, Is.Zero);
        Assert.That(service.Progress.matches, Is.Zero);
        Assert.That(service.Progress.history, Is.Empty);
        Assert.That(service.Progress.processedResultIds, Is.Empty);
        Assert.That(service.Missions.Progress("first-win").Current, Is.Zero);
    }

    [Test]
    public void StandardCompletionPreservesExistingRewards()
    {
        var service = new WDLocalMatchResultService(new MemoryStore());
        WDMatchCompletion result = Complete("1x1", "normal", 0, 0);
        Assert.That(result.AwardsCompetitiveProgress, Is.True);
        Assert.That(result.LocalTrophies, Is.EqualTo(40));
        Assert.That(service.TryApply(result), Is.True);
        Assert.That(service.Progress.trophies, Is.EqualTo(40));
        Assert.That(service.Progress.matches, Is.EqualTo(1));
    }

    private static WDMatchCompletion Complete(string mode, string submode,
        int localSlot, int winnerSlot, bool draw = false,
        WDMatchOrigin origin = WDMatchOrigin.Standard)
    {
        int count = mode == "1x1" ? 2 : 4;
        var participants = new List<WDMatchParticipant>();
        for (int slot = 0; slot < count; slot++)
            participants.Add(Participant(slot, mode == "2x2" ? slot % 2 : slot,
                slot == localSlot));
        WDMatchSetup setup = new(mode, submode, "classic", "Assets/WarGame.unity",
            submode == "random_cards" ? "symmetric_random_cards" : "standard_deck",
            10, true, 0, participants, origin: origin);
        ResultadoPartida final = draw
            ? ResultadoPartida.CriarEmpateFinal(10, 40)
            : mode == "2x2"
            ? ResultadoPartida.CriarVitoriaEquipe(EquipesJogadores.Equipe.Vanguard, 40, 10, false)
            : ResultadoPartida.CriarVitoriaJogador(
                (TerritorioClique.Dono)(winnerSlot + 1), 40, 10, false);
        var territories = Enumerable.Range(0, count).ToDictionary(slot => slot,
            slot => 40 - slot * 10);
        return WDMatchCompletion.Create(setup, final, territories,
            new DateTime(2026, 9, 19, 12, 0, 0, DateTimeKind.Utc).Ticks);
    }

    private static WDMatchParticipant Participant(int slot, int team, bool local) =>
        new($"p{slot}", $"Player {slot}", team, slot, Color.white, "", "deck_1",
            local ? WDMatchParticipantKind.Local : WDMatchParticipantKind.Remote);
}
