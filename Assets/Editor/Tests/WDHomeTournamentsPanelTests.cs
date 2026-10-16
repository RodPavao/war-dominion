using System.Linq;
using NUnit.Framework;

public sealed class WDHomeTournamentsPanelTests
{
    // ============================================================
    // 01. FILTROS DA APRESENTAÇÃO NÃO ALTERAM O DOMÍNIO
    // ============================================================

    [Test]
    public void TabsPartitionExistingRepositoryByLifecycle()
    {
        var profiles = new WDProfileRepository(null, new WDMemoryProfilePrivacyStore());
        var repository = new WDTournamentRepository(profiles);

        var active = WDHomeTournamentsPanel.FilterTournaments(
            repository.All, WDTournamentListTab.Active);
        var coming = WDHomeTournamentsPanel.FilterTournaments(
            repository.All, WDTournamentListTab.ComingSoon);
        var closed = WDHomeTournamentsPanel.FilterTournaments(
            repository.All, WDTournamentListTab.Closed);

        Assert.That(active.All(item => item.Lifecycle == WDTournamentLifecycle.RegistrationOpen ||
            item.Lifecycle == WDTournamentLifecycle.InProgress), Is.True);
        Assert.That(coming.All(item => item.Lifecycle == WDTournamentLifecycle.ComingSoon), Is.True);
        Assert.That(closed.All(item => item.Lifecycle == WDTournamentLifecycle.Closed), Is.True);
        Assert.That(active.Count + coming.Count + closed.Count, Is.EqualTo(repository.All.Count));
    }

    [Test]
    public void EmptySourceProducesEmptyTabs()
    {
        Assert.That(WDHomeTournamentsPanel.FilterTournaments(null,
            WDTournamentListTab.Active), Is.Empty);
    }
}
