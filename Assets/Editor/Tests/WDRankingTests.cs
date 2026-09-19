using System;
using System.Linq;
using NUnit.Framework;

public sealed class WDRankingTests
{
    private static WDRankingRepository Repository() => new(
        new WDProfileRepository(null, new WDMemoryProfilePrivacyStore()),
        now: new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero));

    // ============================================================
    // 01. GERAL, TEMPORADAS E ENTIDADES
    // ============================================================

    [Test]
    public void GeneralRankingIsIndependentFromSelectedSeason()
    {
        WDRankingRepository repository = Repository();
        var first = new WDRankingQuery { Scope = WDRankingScope.General, Season = repository.Seasons[0] };
        var second = new WDRankingQuery { Scope = WDRankingScope.General, Season = repository.Seasons[2] };
        CollectionAssert.AreEqual(repository.Query(first).Entries.Select(item => item.Points),
            repository.Query(second).Entries.Select(item => item.Points));
    }

    [Test]
    public void SeasonsAreMonthlyCurrentAndHistorical()
    {
        WDRankingRepository repository = Repository();
        Assert.That(repository.Seasons.Count, Is.EqualTo(3));
        Assert.That(repository.Seasons[0], Is.EqualTo(new WDSeasonId(2026, 9)));
        Assert.That(repository.Seasons[1], Is.EqualTo(new WDSeasonId(2026, 8)));
        Assert.That(repository.Seasons[2], Is.EqualTo(new WDSeasonId(2026, 7)));
    }

    [Test]
    public void HistoricalSeasonsKeepDistinctPlayerData()
    {
        WDRankingRepository repository = Repository();
        var august = new WDRankingQuery { Scope = WDRankingScope.Season, Season = repository.Seasons[1] };
        var july = new WDRankingQuery { Scope = WDRankingScope.Season, Season = repository.Seasons[2] };
        Assert.That(repository.Query(august).Entries[0].Points,
            Is.Not.EqualTo(repository.Query(july).Entries[0].Points));
    }

    [Test]
    public void PlayersAndClansAreSeparateRankings()
    {
        WDRankingRepository repository = Repository();
        var players = repository.Query(new WDRankingQuery { Entity = WDRankingEntity.Players });
        var clans = repository.Query(new WDRankingQuery { Entity = WDRankingEntity.Clans });
        Assert.That(players.Entries.All(item => item.PublicPlayerId != null), Is.True);
        Assert.That(clans.Entries.All(item => item.PublicPlayerId == null), Is.True);
    }

    [Test]
    public void RankingEntityContainsNoFriendsOption() =>
        CollectionAssert.DoesNotContain(Enum.GetNames(typeof(WDRankingEntity)), "Friends");

    [Test]
    public void GeneralPointsRemainWhenSeasonChanges()
    {
        WDRankingRepository repository = Repository();
        int before = repository.Query(new WDRankingQuery()).Entries.Sum(item => item.Points);
        repository.Query(new WDRankingQuery
        {
            Scope = WDRankingScope.Season, Season = repository.Seasons[2]
        });
        int after = repository.Query(new WDRankingQuery()).Entries.Sum(item => item.Points);
        Assert.That(after, Is.EqualTo(before));
    }

    // ============================================================
    // 02. DIVISÕES, BUSCA E IDENTIDADE
    // ============================================================

    [Test]
    public void DivisionFilterUsesProfileCatalogDefinition()
    {
        WDRankingRepository repository = Repository();
        WDDivisionDefinition division = repository.Divisions.Find("centurion");
        WDRankingResult result = repository.Query(new WDRankingQuery { DivisionId = division.Id });
        Assert.That(result.Entries, Is.Not.Empty);
        Assert.That(result.Entries.All(item => ReferenceEquals(item.Division, division)), Is.True);
    }

    [Test]
    public void ClearingDivisionReturnsAllPlayerDivisions()
    {
        WDRankingRepository repository = Repository();
        int filtered = repository.Query(new WDRankingQuery { DivisionId = "centurion" }).Entries.Count;
        int all = repository.Query(new WDRankingQuery { DivisionId = null }).Entries.Count;
        Assert.That(all, Is.GreaterThan(filtered));
    }

    [Test]
    public void DivisionOrderComesDirectlyFromSharedCatalog()
    {
        WDRankingRepository repository = Repository();
        Assert.That(repository.Divisions.Ordered.Select(item => item.DisplayOrder), Is.Ordered);
        Assert.That(repository.Divisions.Ordered.Last().HasOpenUpperLimit, Is.True);
    }

    [Test]
    public void PlayerSearchSupportsNameAndPublicId()
    {
        WDRankingRepository repository = Repository();
        WDRankingEntry target = repository.Query(new WDRankingQuery()).Entries
            .First(item => item.PublicPlayerId == "WD-5582");
        Assert.That(repository.Query(new WDRankingQuery { Search = target.DisplayName }).Entries
            .Count(item => item.PublicPlayerId == target.PublicPlayerId), Is.EqualTo(1));
        Assert.That(repository.Query(new WDRankingQuery { Search = "WD-5582" }).Entries
            .Count(item => item.PublicPlayerId == target.PublicPlayerId), Is.EqualTo(1));
    }

    [Test]
    public void ClanSearchUsesClanNameWithinCurrentScope()
    {
        WDRankingRepository repository = Repository();
        WDRankingResult result = repository.Query(new WDRankingQuery
        {
            Entity = WDRankingEntity.Clans, Search = "Sentinel"
        });
        Assert.That(result.Entries.Count, Is.EqualTo(1));
        Assert.That(result.Entries[0].DisplayName, Is.EqualTo("Sentinel Order"));
    }

    [Test]
    public void SearchWithoutResultProducesInlineEmptyState()
    {
        WDRankingResult result = Repository().Query(new WDRankingQuery { Search = "NO-SUCH-PLAYER" });
        Assert.That(result.State, Is.EqualTo(WDRankingResultState.Empty));
    }

    [Test]
    public void LocalPlayerUsesSharedProfileIdentityAndHasRankedPosition()
    {
        WDRankingRepository repository = Repository();
        WDRankingResult result = repository.Query(new WDRankingQuery());
        Assert.That(result.LocalPlayer, Is.Not.Null);
        Assert.That(result.LocalPlayer.PublicPlayerId, Is.EqualTo(repository.Profiles.LocalProfile.PlayerId));
        Assert.That(result.LocalPlayer.DisplayName, Is.EqualTo(repository.Profiles.LocalProfile.Nickname));
        Assert.That(result.LocalPlayer.Position, Is.GreaterThan(0));
    }

    // ============================================================
    // 03. RECOMPENSAS E CONTRATOS DE INTEGRAÇÃO
    // ============================================================

    [Test]
    public void SeasonRewardsExposeOnlyApprovedFutureTypesWithoutAmounts()
    {
        WDRankingRepository repository = Repository();
        CollectionAssert.AreEquivalent(new[]
        {
            WDRankingRewardType.WeaponTokens,
            WDRankingRewardType.Emeralds,
            WDRankingRewardType.Coins
        }, repository.SeasonRewards.Select(item => item.Type));
        Assert.That(repository.SeasonRewards.All(item => !item.Quantity.HasValue), Is.True);
        Assert.That(repository.SeasonRewards.All(item => item.EligibilityRule == null), Is.True);
    }

    [Test]
    public void PlayerSelectionCarriesStablePublicProfileId()
    {
        WDRankingEntry player = Repository().Query(new WDRankingQuery()).Entries
            .First(item => !item.IsLocalPlayer);
        Assert.That(player.PublicPlayerId, Is.Not.Empty);
    }

    [Test]
    public void QueryClonePreservesRankingContextWithoutSharingMutableState()
    {
        var query = new WDRankingQuery
        {
            Scope = WDRankingScope.Season, Entity = WDRankingEntity.Clans,
            Season = new WDSeasonId(2026, 8), DivisionId = "elite", Search = "Atlas"
        };
        WDRankingQuery clone = query.Clone();
        clone.Search = "Other";
        Assert.That(query.Search, Is.EqualTo("Atlas"));
        Assert.That(clone.Scope, Is.EqualTo(query.Scope));
        Assert.That(clone.Entity, Is.EqualTo(query.Entity));
        Assert.That(clone.Season, Is.EqualTo(query.Season));
        Assert.That(clone.DivisionId, Is.EqualTo(query.DivisionId));
    }
}
