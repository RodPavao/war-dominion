using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public sealed class WDGameWeaponContextTests
{
    private FakeSource source;
    private WDGameWeaponContext context;

    [SetUp]
    public void SetUp()
    {
        source = FakeSource.TeamMatch();
        context = new WDGameWeaponContext(source);
    }

    // ============================================================
    // 01. PARTIDA, IDENTIDADE E ORDEM
    // ============================================================

    [Test]
    public void ReadsCurrentRoundAndActiveMap()
    {
        Assert.That(context.CurrentRound, Is.EqualTo(4));
        Assert.That(context.ActiveMapId, Is.EqualTo("test_map"));
    }

    [Test]
    public void ReadsStableParticipantsAndOfficialOrder()
    {
        Assert.That(context.Players.Select(player => player.PlayerId),
            Is.EqualTo(new[] { "p1", "p2", "p3", "p4" }));
        Assert.That(context.OfficialPlayerOrder,
            Is.EqualTo(new[] { "p3", "p2", "p1", "p4" }));
        Assert.That(context.FindPlayer("p3").SlotIndex, Is.EqualTo(2));
        Assert.That(context.FindPlayer("missing"), Is.Null);
    }

    [Test]
    public void UsesTeamIdentityForAlliesAndOpponents()
    {
        Assert.That(context.AreAllied("p1", "p3"), Is.True);
        Assert.That(context.AreAllied("p1", "p2"), Is.False);
        Assert.That(context.AreAllied("p1", "p1"), Is.False);
        Assert.That(context.AreOpponents("p1", "p2"), Is.True);
        Assert.That(context.AreOpponents("p1", "missing"), Is.False);
    }

    [Test]
    public void FfaParticipantsHaveNoAllies()
    {
        var ffa = new WDGameWeaponContext(FakeSource.Ffa());

        Assert.That(ffa.AreAllied("p1", "p2"), Is.False);
        Assert.That(ffa.AreOpponents("p1", "p2"), Is.True);
    }

    // ============================================================
    // 02. ESTADOS TERRITORIAIS EXPLÍCITOS
    // ============================================================

    [TestCase("occupied", "p1", 5, WDWeaponTerritoryStateKind.Occupied)]
    [TestCase("neutral", "", 1, WDWeaponTerritoryStateKind.Neutral)]
    [TestCase("empty", "", 0, WDWeaponTerritoryStateKind.Empty)]
    public void ReadsOwnerTroopsAndExplicitState(
        string territoryId, string ownerId, int troops,
        WDWeaponTerritoryStateKind expectedState)
    {
        WDGameWeaponTerritoryView territory = context.FindTerritory(territoryId);

        Assert.That(territory.OwnerPlayerId, Is.EqualTo(ownerId));
        Assert.That(territory.Troops, Is.EqualTo(troops));
        Assert.That(territory.State, Is.EqualTo(expectedState));
    }

    [Test]
    public void OccupiedZeroTroopsIsNotReclassifiedAsEmpty()
    {
        Assert.That(context.FindTerritory("occupied_zero").State,
            Is.EqualTo(WDWeaponTerritoryStateKind.Occupied));
    }

    [TestCase(TerritorioClique.EstadoTerritorial.Ocupado,
        WDWeaponTerritoryStateKind.Occupied)]
    [TestCase(TerritorioClique.EstadoTerritorial.Neutro,
        WDWeaponTerritoryStateKind.Neutral)]
    [TestCase(TerritorioClique.EstadoTerritorial.Vazio,
        WDWeaponTerritoryStateKind.Empty)]
    public void MapsExplicitGameplayStateWithoutUsingTroopCount(
        TerritorioClique.EstadoTerritorial gameplayState,
        WDWeaponTerritoryStateKind expected)
    {
        Assert.That(WDGameWeaponStateMapper.ToWeaponState(gameplayState),
            Is.EqualTo(expected));
    }

    [Test]
    public void MissingTerritoryReturnsNull()
    {
        Assert.That(context.FindTerritory("missing"), Is.Null);
    }

    // ============================================================
    // 03. CONEXÕES CANÔNICAS E IMUTABILIDADE
    // ============================================================

    [Test]
    public void ReadsTypedAdjacencyAndNeighbours()
    {
        Assert.That(context.AreConnected("occupied", "neutral",
            TipoConexaoMapa.Terrestre), Is.True);
        Assert.That(context.AreConnected("occupied", "empty",
            TipoConexaoMapa.Especial), Is.True);
        Assert.That(context.GetNeighbours("occupied"),
            Is.EqualTo(new[] { "empty", "neutral" }));
        Assert.That(context.GetNeighbours("occupied", TipoConexaoMapa.Terrestre),
            Is.EqualTo(new[] { "neutral" }));
    }

    [Test]
    public void MissingTerritoryHasNoNeighbours()
    {
        Assert.That(context.GetNeighbours("missing"), Is.Empty);
    }

    [Test]
    public void QueriesDoNotMutateSourceState()
    {
        string before = source.Fingerprint();

        _ = context.Players;
        _ = context.Territories;
        _ = context.AreAllied("p1", "p3");
        _ = context.GetNeighbours("occupied");

        Assert.That(source.Fingerprint(), Is.EqualTo(before));
    }

    private sealed class FakeSource : IWDGameWeaponStateSource
    {
        private readonly List<WDGameWeaponPlayerView> players = new();
        private readonly List<string> order = new();
        private readonly List<WDGameWeaponTerritoryView> territories = new();
        private readonly List<(string Origin, string Target, TipoConexaoMapa Type)> links = new();

        public int CurrentRound { get; private set; }
        public string ActiveMapId { get; private set; }

        public IReadOnlyList<WDGameWeaponPlayerView> GetPlayers() => players;
        public IReadOnlyList<string> GetOfficialPlayerOrder() => order;
        public IReadOnlyList<WDGameWeaponTerritoryView> GetTerritories() => territories;

        public bool AreConnected(string originId, string targetId, TipoConexaoMapa type) =>
            links.Any(link => link.Type == type &&
                (link.Origin == originId && link.Target == targetId ||
                 link.Origin == targetId && link.Target == originId));

        public IReadOnlyList<string> GetNeighbours(string territoryId, TipoConexaoMapa? type) =>
            links.Where(link => !type.HasValue || link.Type == type.Value)
                .Where(link => link.Origin == territoryId || link.Target == territoryId)
                .Select(link => link.Origin == territoryId ? link.Target : link.Origin)
                .Distinct().OrderBy(id => id, StringComparer.Ordinal).ToArray();

        public string Fingerprint() => string.Join("|",
            CurrentRound, ActiveMapId,
            string.Join(",", players.Select(player => player.PlayerId)),
            string.Join(",", order),
            string.Join(",", territories.Select(territory =>
                $"{territory.TerritoryId}:{territory.OwnerPlayerId}:{territory.Troops}:{territory.State}")));

        public static FakeSource TeamMatch()
        {
            var result = new FakeSource { CurrentRound = 4, ActiveMapId = "test_map" };
            result.players.AddRange(new[]
            {
                new WDGameWeaponPlayerView("p1", 0, 0),
                new WDGameWeaponPlayerView("p2", 1, 1),
                new WDGameWeaponPlayerView("p3", 2, 0),
                new WDGameWeaponPlayerView("p4", 3, 1)
            });
            result.order.AddRange(new[] { "p3", "p2", "p1", "p4" });
            result.territories.AddRange(new[]
            {
                new WDGameWeaponTerritoryView("occupied", "p1", 5,
                    WDWeaponTerritoryStateKind.Occupied),
                new WDGameWeaponTerritoryView("neutral", "", 1,
                    WDWeaponTerritoryStateKind.Neutral),
                new WDGameWeaponTerritoryView("empty", "", 0,
                    WDWeaponTerritoryStateKind.Empty),
                new WDGameWeaponTerritoryView("occupied_zero", "p2", 0,
                    WDWeaponTerritoryStateKind.Occupied)
            });
            result.links.Add(("occupied", "neutral", TipoConexaoMapa.Terrestre));
            result.links.Add(("occupied", "empty", TipoConexaoMapa.Especial));
            return result;
        }

        public static FakeSource Ffa()
        {
            FakeSource result = TeamMatch();
            result.players.Clear();
            result.players.AddRange(new[]
            {
                new WDGameWeaponPlayerView("p1", 0, 0),
                new WDGameWeaponPlayerView("p2", 1, 1),
                new WDGameWeaponPlayerView("p3", 2, 2),
                new WDGameWeaponPlayerView("p4", 3, 3)
            });
            return result;
        }
    }
}
