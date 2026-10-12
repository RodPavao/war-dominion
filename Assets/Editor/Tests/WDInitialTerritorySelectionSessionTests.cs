using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class WDInitialTerritorySelectionSessionTests
{
    // ============================================================
    // 01. DUAS ETAPAS E UMA ATRIBUIÇÃO POR JOGADOR
    // ============================================================

    [Test]
    public void SessionHasExactlyTwoStages()
    {
        var session = Session(1);
        Resolve(session, "t1", "t2");
        Assert.That(session.IsComplete, Is.False);
        Resolve(session, "t3", "t4");
        Assert.That(session.IsComplete, Is.True);
    }

    [Test]
    public void EachStageAssignsOneTerritoryPerPlayer()
    {
        WDInitialTerritoryStageResult result = Resolve(Session(2), "t1", "t2");
        Assert.That(result.Assignments, Has.Count.EqualTo(2));
        Assert.That(result.Assignments.Select(item => item.PlayerId),
            Is.EquivalentTo(new[] { "p1", "p2" }));
    }

    [Test]
    public void FinalResultHasTwoTerritoriesPerPlayer()
    {
        var session = Session(3);
        Resolve(session, "t1", "t2");
        Resolve(session, "t3", "t4");
        Assert.That(session.GetAssignments("p1"), Has.Count.EqualTo(2));
        Assert.That(session.GetAssignments("p2"), Has.Count.EqualTo(2));
    }

    [Test]
    public void SecondStageRejectsAlreadyAssignedTerritory()
    {
        var session = Session(4);
        Resolve(session, "t1", "t2");
        Assert.That(session.SubmitIntent("p1", "t1"), Is.False);
    }

    [Test]
    public void PendingSelectionCanBeCancelledWithoutSubmittingIntent()
    {
        var session = Session(40);
        Assert.That(session.SelectPending("p1", "t1"), Is.True);
        Assert.That(session.CancelPending("p1"), Is.True);
        Assert.That(session.GetPending("p1"), Is.Empty);
        Assert.That(session.ReadyToResolve, Is.False);
        Assert.That(session.IsAvailable("t1"), Is.True);
    }

    [Test]
    public void PendingSelectionCanBeReplacedBeforeConfirmation()
    {
        var session = Session(41);
        Assert.That(session.SelectPending("p1", "t1"), Is.True);
        Assert.That(session.SelectPending("p1", "t3"), Is.True);
        Assert.That(session.GetPending("p1"), Is.EqualTo("t3"));
        Assert.That(session.ConfirmPending("p1"), Is.True);
        Assert.That(session.ResolveStage(), Is.Null);
    }

    [Test]
    public void ConfirmedPendingSelectionUsesNormalResolutionPipeline()
    {
        var session = Session(42);
        Assert.That(session.SelectPending("p1", "t1"), Is.True);
        Assert.That(session.ConfirmPending("p1"), Is.True);
        Assert.That(session.SubmitIntent("p2", "t2"), Is.True);
        WDInitialTerritoryStageResult result = session.ResolveStage();
        Assert.That(result.Assignments[0].TerritoryId, Is.EqualTo("t1"));
        Assert.That(session.IsAvailable("t1"), Is.False);
    }

    // ============================================================
    // 02. CONFLITO, PRIORIDADE E FALLBACK
    // ============================================================

    [Test]
    public void SameChoiceGoesToFirstPlayerInRoundOrder()
    {
        WDInitialTerritoryStageResult result = Resolve(Session(5), "t1", "t1");
        Assert.That(result.HadConflict, Is.True);
        Assert.That(result.Assignments[0].PlayerId, Is.EqualTo("p1"));
        Assert.That(result.Assignments[0].TerritoryId, Is.EqualTo("t1"));
    }

    [Test]
    public void ConflictLoserReceivesFallback()
    {
        WDInitialTerritoryStageResult result = Resolve(Session(6), "t1", "t1");
        Assert.That(result.Assignments[1].PlayerId, Is.EqualTo("p2"));
        Assert.That(result.Assignments[1].WasFallback, Is.True);
        Assert.That(result.Assignments[1].TerritoryId, Is.Not.EqualTo("t1"));
    }

    [Test]
    public void FallbackNeverUsesOccupiedTerritory()
    {
        var session = Session(7);
        Resolve(session, "t1", "t1");
        Assert.That(session.IsAvailable("t1"), Is.False);
        Assert.That(session.GetAssignments("p2"), Has.None.EqualTo("t1"));
    }

    [Test]
    public void SameSeedProducesSameFallback()
    {
        string first = Resolve(Session(77), "t1", "t1").Assignments[1].TerritoryId;
        string second = Resolve(Session(77), "t1", "t1").Assignments[1].TerritoryId;
        Assert.That(first, Is.EqualTo(second));
    }

    [Test]
    public void PriorityComesFromProvidedRoundOrder()
    {
        var session = new WDInitialTerritorySelectionSession(
            8, new[] { "p2", "p1" }, Territories());
        session.SubmitIntent("p1", "t1");
        session.SubmitIntent("p2", "t1");
        Assert.That(session.ResolveStage().Assignments[0].PlayerId, Is.EqualTo("p2"));
    }

    [Test]
    public void ReversedPriorityMakesLocalPlayerReceiveFallback()
    {
        var session = new WDInitialTerritorySelectionSession(
            81, new[] { "bot", "local" }, Territories());
        session.SubmitIntent("local", "t1");
        session.SubmitIntent("bot", "t1");

        WDInitialTerritoryStageResult result = session.ResolveStage();

        Assert.That(result.HadConflict, Is.True);
        Assert.That(result.Assignments[0].PlayerId, Is.EqualTo("bot"));
        Assert.That(result.Assignments[0].TerritoryId, Is.EqualTo("t1"));
        Assert.That(result.Assignments[1].PlayerId, Is.EqualTo("local"));
        Assert.That(result.Assignments[1].WasFallback, Is.True);
        Assert.That(result.Assignments[1].TerritoryId, Is.Not.EqualTo("t1"));
    }

    [Test]
    public void ProvidedRoundOrderCanGiveSecondNamedPlayerPriority()
    {
        var session = new WDInitialTerritorySelectionSession(
            83, new[] { "p2", "p1" }, Territories());
        session.SubmitIntent("p1", "t1");
        session.SubmitIntent("p2", "t1");

        WDInitialTerritoryStageResult result = session.ResolveStage();

        Assert.That(result.HadConflict, Is.True);
        Assert.That(result.Assignments[0].PlayerId, Is.EqualTo("p2"));
        Assert.That(result.Assignments[0].TerritoryId, Is.EqualTo("t1"));
        Assert.That(result.Assignments[1].PlayerId, Is.EqualTo("p1"));
        Assert.That(result.Assignments[1].WasFallback, Is.True);
        Assert.That(session.GetAssignments("p1"), Has.Count.EqualTo(1));
        Assert.That(session.GetAssignments("p2"), Has.Count.EqualTo(1));
    }

    [Test]
    public void ForcedConflictInBothStagesEndsWithTwoUniqueTerritoriesPerPlayer()
    {
        var session = Session(82);
        Resolve(session, "t1", "t1");
        string secondConflict = Territories().First(session.IsAvailable);
        Resolve(session, secondConflict, secondConflict);

        string[] allAssignments = session.GetAssignments("p1")
            .Concat(session.GetAssignments("p2")).ToArray();
        Assert.That(session.IsComplete, Is.True);
        Assert.That(session.GetAssignments("p1"), Has.Count.EqualTo(2));
        Assert.That(session.GetAssignments("p2"), Has.Count.EqualTo(2));
        Assert.That(allAssignments.Distinct().Count(), Is.EqualTo(4));
        Assert.That(session.IsAvailable("t1"), Is.False);
        Assert.That(session.IsAvailable(secondConflict), Is.False);
    }

    // ============================================================
    // 03. DISPONIBILIDADE E NÃO DUPLICAÇÃO
    // ============================================================

    [Test]
    public void AssignedTerritoriesLeaveAllOthersAvailable()
    {
        var session = Session(9);
        Resolve(session, "t1", "t2");
        Assert.That(session.IsAvailable("t1"), Is.False);
        Assert.That(session.IsAvailable("t2"), Is.False);
        Assert.That(session.IsAvailable("t3"), Is.True);
    }

    [Test]
    public void StageCannotResolveBeforeBothIntentionsExist()
    {
        var session = Session(10);
        session.SubmitIntent("p1", "t1");
        Assert.That(session.ResolveStage(), Is.Null);
        Assert.That(session.Stage, Is.EqualTo(1));
    }

    [Test]
    public void CompletedSessionRejectsNewIntentions()
    {
        var session = Session(11);
        Resolve(session, "t1", "t2");
        Resolve(session, "t3", "t4");
        Assert.That(session.SubmitIntent("p1", "t5"), Is.False);
    }

    // ============================================================
    // 04. ORDEM E IDENTIDADE DOS MODOS POR EQUIPE
    // ============================================================

    [TestCase(1, TerritorioClique.Dono.Jogador1, TerritorioClique.Dono.Jogador2,
        TerritorioClique.Dono.Jogador3, TerritorioClique.Dono.Jogador4)]
    [TestCase(2, TerritorioClique.Dono.Jogador4, TerritorioClique.Dono.Jogador1,
        TerritorioClique.Dono.Jogador2, TerritorioClique.Dono.Jogador3)]
    [TestCase(3, TerritorioClique.Dono.Jogador3, TerritorioClique.Dono.Jogador4,
        TerritorioClique.Dono.Jogador1, TerritorioClique.Dono.Jogador2)]
    public void TeamRoundOrderInterleavesAndRotates(
        int round, TerritorioClique.Dono first, TerritorioClique.Dono second,
        TerritorioClique.Dono third, TerritorioClique.Dono fourth)
    {
        IReadOnlyList<TerritorioClique.Dono> result =
            GerenciadorRodada.ConstruirPrioridadeJogadores(
                new[]
                {
                    TerritorioClique.Dono.Jogador1,
                    TerritorioClique.Dono.Jogador2,
                    TerritorioClique.Dono.Jogador3,
                    TerritorioClique.Dono.Jogador4
                }, TeamSetup(), round);
        Assert.That(result, Is.EqualTo(new[] { first, second, third, fourth }));
    }

    [Test]
    public void TeamIdentityRecognizesAlliesAndEnemiesIndependentlyFromColor()
    {
        WDMatchSetupContext.Store(TeamSetup());
        try
        {
            Assert.That(EquipesJogadores.SaoAliados(
                TerritorioClique.Dono.Jogador1, TerritorioClique.Dono.Jogador3), Is.True);
            Assert.That(EquipesJogadores.SaoInimigos(
                TerritorioClique.Dono.Jogador1, TerritorioClique.Dono.Jogador2), Is.True);
            Assert.That(EquipesJogadores.ObterEquipe(TerritorioClique.Dono.Jogador1),
                Is.EqualTo(EquipesJogadores.Equipe.Vanguard));
            Assert.That(EquipesJogadores.ObterEquipe(TerritorioClique.Dono.Jogador2),
                Is.EqualTo(EquipesJogadores.Equipe.Sentinel));
        }
        finally
        {
            WDMatchSetupContext.Clear();
        }
    }

    [TestCase(1, TerritorioClique.Dono.Jogador1, TerritorioClique.Dono.Jogador2,
        TerritorioClique.Dono.Jogador3, TerritorioClique.Dono.Jogador4,
        TerritorioClique.Dono.Jogador5, TerritorioClique.Dono.Jogador6)]
    [TestCase(2, TerritorioClique.Dono.Jogador6, TerritorioClique.Dono.Jogador1,
        TerritorioClique.Dono.Jogador2, TerritorioClique.Dono.Jogador3,
        TerritorioClique.Dono.Jogador4, TerritorioClique.Dono.Jogador5)]
    [TestCase(3, TerritorioClique.Dono.Jogador5, TerritorioClique.Dono.Jogador6,
        TerritorioClique.Dono.Jogador1, TerritorioClique.Dono.Jogador2,
        TerritorioClique.Dono.Jogador3, TerritorioClique.Dono.Jogador4)]
    public void ThreeVsThreeRoundOrderInterleavesAndRotates(
        int round, TerritorioClique.Dono first, TerritorioClique.Dono second,
        TerritorioClique.Dono third, TerritorioClique.Dono fourth,
        TerritorioClique.Dono fifth, TerritorioClique.Dono sixth)
    {
        IReadOnlyList<TerritorioClique.Dono> result =
            GerenciadorRodada.ConstruirPrioridadeJogadores(
                new[]
                {
                    TerritorioClique.Dono.Jogador1,
                    TerritorioClique.Dono.Jogador2,
                    TerritorioClique.Dono.Jogador3,
                    TerritorioClique.Dono.Jogador4,
                    TerritorioClique.Dono.Jogador5,
                    TerritorioClique.Dono.Jogador6
                }, ThreeVsThreeSetup(), round);

        Assert.That(result, Is.EqualTo(new[]
        {
            first, second, third, fourth, fifth, sixth
        }));
    }

    [Test]
    public void ThreeVsThreeFindsExactlyTwoAlliesWithoutChangingIdentity()
    {
        WDMatchSetup setup = ThreeVsThreeSetup();
        IReadOnlyList<TerritorioClique.Dono> allies =
            EquipesJogadores.ObterAliados(setup, TerritorioClique.Dono.Jogador1);

        Assert.That(allies, Is.EqualTo(new[]
        {
            TerritorioClique.Dono.Jogador3,
            TerritorioClique.Dono.Jogador5
        }));
        Assert.That(allies.Contains(TerritorioClique.Dono.Jogador1), Is.False);
        Assert.That(allies.Contains(TerritorioClique.Dono.Jogador2), Is.False);
        Assert.That(setup.Participants[0].PlayerId, Is.EqualTo("a1"));
        Assert.That(setup.Participants[0].TeamIndex, Is.EqualTo(0));
    }

    [Test]
    public void TwoVsTwoStillFindsSingleAutomaticAlly()
    {
        Assert.That(EquipesJogadores.ObterAliados(
            TeamSetup(), TerritorioClique.Dono.Jogador1),
            Is.EqualTo(new[] { TerritorioClique.Dono.Jogador3 }));
    }

    [Test]
    public void AttackVisualEventUsesCommittedAmountInsteadOfOriginTotal()
    {
        ResolutionVisualEvent visual = ResolutionVisualEvent.Attack(
            TerritorioClique.Dono.Jogador1, "origin", "target", 4,
            10, 6, 3, 0, true,
            TerritorioClique.Dono.Jogador3, TerritorioClique.Dono.Jogador1);
        Assert.That(visual.Amount, Is.EqualTo(4));
        Assert.That(visual.OriginBefore, Is.EqualTo(10));
        Assert.That(AttackResolutionPresenter.ObterQuantidadeAnimada(visual), Is.EqualTo(4));
    }

    [Test]
    public void AnimatedCounterRepresentationIsDetachedAndShowsOnlyCommittedAmount()
    {
        var source = new GameObject("CounterSource");
        source.transform.localScale = new Vector3(0.67f, 0.72f, 1.21f);
        GameObject copy = null;
        try
        {
            copy = AttackResolutionPresenter.CriarRepresentacaoAnimada(
                source.transform, 4, TerritorioClique.Dono.Jogador1);
            WDArmyCounterView view = copy.GetComponent<WDArmyCounterView>();
            Assert.That(view, Is.Not.Null);
            Assert.That(view.Territorio, Is.Null);
            Assert.That(view.QuantidadeExibida, Is.EqualTo(4));
            Assert.That(copy.GetComponent<ContadorTropas>(), Is.Null);
        }
        finally
        {
            if (copy != null)
                Object.DestroyImmediate(copy);
            Object.DestroyImmediate(source);
        }
    }

    private static WDInitialTerritorySelectionSession Session(int seed) =>
        new(seed, new[] { "p1", "p2" }, Territories());

    private static IEnumerable<string> Territories() =>
        Enumerable.Range(1, 12).Select(index => $"t{index}");

    private static WDInitialTerritoryStageResult Resolve(
        WDInitialTerritorySelectionSession session, string first, string second)
    {
        Assert.That(session.SubmitIntent("p1", first), Is.True);
        Assert.That(session.SubmitIntent("p2", second), Is.True);
        return session.ResolveStage();
    }

    private static WDMatchSetup TeamSetup()
    {
        var participants = new List<WDMatchParticipant>
        {
            Participant("a1", 0, 0), Participant("b1", 1, 1),
            Participant("a2", 0, 2), Participant("b2", 1, 3)
        };
        return new WDMatchSetup("2x2", "normal", "classic", string.Empty,
            string.Empty, 10, true, 1, participants);
    }

    private static WDMatchSetup ThreeVsThreeSetup()
    {
        var participants = new List<WDMatchParticipant>
        {
            Participant("a1", 0, 0), Participant("b1", 1, 1),
            Participant("a2", 0, 2), Participant("b2", 1, 3),
            Participant("a3", 0, 4), Participant("b3", 1, 5)
        };
        return new WDMatchSetup("3x3", "normal", "classic", string.Empty,
            "standard_deck", 10, true, 1, participants);
    }

    private static WDMatchParticipant Participant(string id, int team, int slot) =>
        new(id, id, team, slot, Color.white, string.Empty, string.Empty,
            WDMatchParticipantKind.Remote);
}

// 04. Mesmo planejador chamado pelo distribuidor, sem cenas/UI nem alteração de domínio.
public sealed class WDInitialAutomaticRegionalDistributionTests
{
    [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(6)]
    public void CanonicalMapsPreserveQuotasAndPreventIndividualRegionalMonopoly(int players)
    {
        var state = Random.state;
        try
        {
            var maps = Resources.LoadAll<DefinicaoMapa>("Mapas");
            Assert.That(maps.Length, Is.GreaterThan(0));
            var owners = Enumerable.Range(1, players).Select(i => (TerritorioClique.Dono)i).ToArray();
            foreach (var map in maps)
            for (int seed = 0; seed < 32; seed++)
            {
                var shuffle = new System.Random(seed);
                var ids = map.Territorios.Select(t => t.id).OrderBy(_ => shuffle.Next()).ToArray();
                Random.InitState(seed);
                Assert.That(DistribuidorTerritorios.TentarPlanejar(ids, map.Regioes, owners, out var plan, out var error), Is.True,
                    map.MapaId + ": " + error);
                Random.InitState(seed);
                Assert.That(DistribuidorTerritorios.TentarPlanejar(ids, map.Regioes, owners, out var again, out _), Is.True);
                Assert.That(again, Is.EqualTo(plan), "Seed fixa mantém o resultado.");
                Assert.That(plan.Length, Is.EqualTo(ids.Length));
                var assignments = ids.Select((id, i) => new { id, owner = plan[i] }).ToDictionary(x => x.id, x => x.owner);
                Assert.That(assignments.Count, Is.EqualTo(ids.Length), "Sem duplicação/perda.");
                foreach (var owner in owners)
                    Assert.That(plan.Count(p => p == owner), Is.EqualTo(ids.Length / players +
                        ((int)owner - 1 < ids.Length % players ? 1 : 0)));
                Assert.That(plan.All(owners.Contains), Is.True);
                foreach (var region in map.Regioes)
                    Assert.That(region.territorioIds.Select(id => assignments[id]).Distinct().Count(), Is.GreaterThan(1), region.id);
            }
        }
        finally { Random.state = state; }
    }

    [Test]
    public void SmallRegionsAreConstrainedPerPlayerNotPerTeam()
    {
        var state = Random.state;
        try
        {
            var owners = new[] { TerritorioClique.Dono.Jogador1, TerritorioClique.Dono.Jogador3 }; // Mesmo time possível.
            var ids = new[] { "a", "b", "c", "d", "e", "f", "g" };
            var regions = new[] {
                new DefinicaoRegiaoMapa { id = "pair", territorioIds = new List<string> { "a", "b" } },
                new DefinicaoRegiaoMapa { id = "five", territorioIds = new List<string> { "c", "d", "e", "f", "g" } } };
            for (int seed = 0; seed < 32; seed++)
            {
                Random.InitState(seed);
                Assert.That(DistribuidorTerritorios.TentarPlanejar(ids, regions, owners, out var plan, out _), Is.True);
                Assert.That(plan[0], Is.Not.EqualTo(plan[1]));
                Assert.That(plan.Skip(2).Distinct().Count(), Is.EqualTo(2));
                Assert.That(plan.Count(p => p == owners[0]), Is.EqualTo(4));
                Assert.That(plan.Count(p => p == owners[1]), Is.EqualTo(3));
            }
        }
        finally { Random.state = state; }
    }

    [TestCase(1, 2)] [TestCase(2, 1)]
    public void ImpossibleRegionReturnsNoPartialPlan(int territories, int players)
    {
        var ids = Enumerable.Range(0, territories).Select(i => "t" + i).ToArray();
        var region = new DefinicaoRegiaoMapa { id = "impossible", territorioIds = ids.ToList() };
        var owners = Enumerable.Range(1, players).Select(i => (TerritorioClique.Dono)i).ToArray();
        Assert.That(DistribuidorTerritorios.TentarPlanejar(ids, new[] { region }, owners, out var plan, out var error), Is.False);
        Assert.That(plan, Is.Null); Assert.That(error, Does.Contain("impossible"));
    }

    [Test]
    public void RegionalCapacityPreventsForbiddenRoundRobinWithoutChangingValidPreferences()
    {
        var state = Random.state;
        try
        {
            var ids = new[] { "a", "b", "c", "d" };
            var owners = new[] { TerritorioClique.Dono.Jogador1, TerritorioClique.Dono.Jogador2 };
            var region = new DefinicaoRegiaoMapa { id = "pair", territorioIds = new List<string> { "a", "c" } };
            Assert.That(DistribuidorTerritorios.TentarPlanejar(ids, new[] { region }, owners, out var plan, out _), Is.True);
            Assert.That(plan[0], Is.Not.EqualTo(plan[2]));
            Assert.That(plan.Count(p => p == owners[0]), Is.EqualTo(2));
            region.territorioIds = new List<string> { "a", "b" };
            Assert.That(DistribuidorTerritorios.TentarPlanejar(ids, new[] { region }, owners, out plan, out _), Is.True);
            Assert.That(plan, Is.EqualTo(new[] { owners[0], owners[1], owners[0], owners[1] }));
        }
        finally { Random.state = state; }
    }

    [Test]
    public void WithoutRegionsOriginalRoundRobinIsPreserved()
    {
        var owners = new[] { TerritorioClique.Dono.Jogador1, TerritorioClique.Dono.Jogador2 };
        Assert.That(DistribuidorTerritorios.TentarPlanejar(new[] { "a", "b", "c" }, null, owners, out var plan, out _), Is.True);
        Assert.That(plan, Is.EqualTo(new[] { owners[0], owners[1], owners[0] }));
    }
}
