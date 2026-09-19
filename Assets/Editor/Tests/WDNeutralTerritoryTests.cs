using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class WDNeutralTerritoryTests
{
    // ============================================================
    // 01. SEMÂNTICA EXPLÍCITA DE AUSÊNCIA DE PROPRIETÁRIO
    // ============================================================

    [Test]
    public void NeutralTerritoryHasNoOwner()
    {
        TerritorioClique territory = CreateTerritory();
        Assert.That(territory.IsNeutral, Is.True);
        Assert.That(territory.PossuiDono, Is.False);
        Object.DestroyImmediate(territory.gameObject);
    }

    [Test]
    public void NeutralOpeningTerritoryHasOneTroop()
    {
        TerritorioClique territory = CreateTerritoryWithTroops();
        territory.DefinirNeutro();
        Assert.That(territory.Tropas, Is.EqualTo(1));
        Object.DestroyImmediate(territory.gameObject);
    }

    [Test]
    public void EmptyTerritoryHasNoOwnerAndZeroTroops()
    {
        TerritorioClique territory = CreateTerritoryWithTroops();
        territory.DefinirVazio();
        Assert.That(territory.IsEmpty, Is.True);
        Assert.That(territory.PossuiDono, Is.False);
        Assert.That(territory.Tropas, Is.Zero);
        Object.DestroyImmediate(territory.gameObject);
    }

    [Test]
    public void NeutralOwnerDoesNotBelongToTeam()
    {
        Assert.That(EquipesJogadores.ObterEquipe(TerritorioClique.Dono.Neutro),
            Is.EqualTo(EquipesJogadores.Equipe.Nenhuma));
    }

    [Test]
    public void NeutralDoesNotCountAsPlayerTerritory()
    {
        var owners = new[] { TerritorioClique.Dono.Jogador1, TerritorioClique.Dono.Neutro };
        Assert.That(owners.Count(owner => owner == TerritorioClique.Dono.Jogador1), Is.EqualTo(1));
    }

    [Test]
    public void NeutralPreventsCompleteRegionOwnership()
    {
        var owners = new[] { TerritorioClique.Dono.Jogador1, TerritorioClique.Dono.Neutro };
        Assert.That(owners.All(owner => owner == TerritorioClique.Dono.Jogador1), Is.False);
    }

    [Test]
    public void NeutralIsClassifiedAsAttackTarget()
    {
        TerritorioClique territory = CreateTerritory();
        Assert.That(SistemaAcoesTerrestres.ObterTipoEsperado(
            TerritorioClique.Dono.Jogador1, territory), Is.EqualTo("ATAQUE"));
        Object.DestroyImmediate(territory.gameObject);
    }

    [Test]
    public void AssigningOwnerEndsNeutralState()
    {
        TerritorioClique territory = CreateTerritory();
        territory.DefinirDono(TerritorioClique.Dono.Jogador1);
        Assert.That(territory.IsNeutral, Is.False);
        Assert.That(territory.PossuiDono, Is.True);
        Object.DestroyImmediate(territory.gameObject);
    }

    [Test]
    public void NeutralStateDoesNotDependOnRendererColor()
    {
        TerritorioClique territory = CreateTerritory();
        SpriteRenderer renderer = territory.gameObject.AddComponent<SpriteRenderer>();
        renderer.color = Color.magenta;
        Assert.That(territory.IsNeutral, Is.True);
        Object.DestroyImmediate(territory.gameObject);
    }

    [Test]
    public void LegacyOwnerlessOccupiedValueNormalizesToNeutralOnStart()
    {
        TerritorioClique territory = CreateTerritoryWithTroops();
        territory.gameObject.AddComponent<TerritorioFronteiras>();
        SetSerializedState(territory, TerritorioClique.EstadoTerritorial.Ocupado);
        territory.dono = TerritorioClique.Dono.Neutro;
        InvokeStartForEditMode(territory);
        Assert.That(territory.IsNeutral, Is.True);
        Assert.That(territory.Tropas, Is.EqualTo(1));
        Object.DestroyImmediate(territory.gameObject);
    }

    [Test]
    public void ExplicitEmptyStateSurvivesDirectSceneInitialization()
    {
        TerritorioClique territory = CreateTerritoryWithTroops();
        territory.gameObject.AddComponent<TerritorioFronteiras>();
        territory.DefinirVazio();
        InvokeStartForEditMode(territory);
        Assert.That(territory.IsEmpty, Is.True);
        Assert.That(territory.Tropas, Is.Zero);
        Object.DestroyImmediate(territory.gameObject);
    }

    // ============================================================
    // 02. CONTABILIZAÇÃO DAS SEIS TROPAS INICIAIS DO 1x1
    // ============================================================

    [Test]
    public void TwoInitialTerritoriesKeepOneTroopWhenAssigned()
    {
        TerritorioClique first = CreateTerritoryWithTroops();
        TerritorioClique second = CreateTerritoryWithTroops();
        first.DefinirDono(TerritorioClique.Dono.Jogador1);
        second.DefinirDono(TerritorioClique.Dono.Jogador1);
        first.DefinirTropas(1);
        second.DefinirTropas(1);
        Assert.That(new[] { first.Tropas, second.Tropas }, Is.EqualTo(new[] { 1, 1 }));
        Object.DestroyImmediate(first.gameObject);
        Object.DestroyImmediate(second.gameObject);
    }

    [Test]
    public void OccupiedTerritoryHasOwnerAndAtLeastOneTroop()
    {
        TerritorioClique territory = CreateTerritoryWithTroops();
        territory.DefinirDono(TerritorioClique.Dono.Jogador1);
        Assert.That(territory.IsOccupied, Is.True);
        Assert.That(territory.IsEmpty, Is.False);
        Assert.That(territory.PossuiDono, Is.True);
        Assert.That(territory.Tropas, Is.GreaterThanOrEqualTo(1));
        Object.DestroyImmediate(territory.gameObject);
    }

    [Test]
    public void StateIsNotInferredOnlyFromTroopCount()
    {
        TerritorioClique neutral = CreateTerritoryWithTroops();
        TerritorioClique empty = CreateTerritoryWithTroops();
        neutral.DefinirNeutro();
        empty.DefinirVazio();
        Assert.That(neutral.dono, Is.EqualTo(empty.dono));
        Assert.That(neutral.Estado, Is.Not.EqualTo(empty.Estado));
        Object.DestroyImmediate(neutral.gameObject);
        Object.DestroyImmediate(empty.gameObject);
    }

    [Test]
    public void OccupiedToEmptyRemovesOwnerAndTroops()
    {
        TerritorioClique territory = CreateInitializedTerritoryWithTroops();
        territory.DefinirDono(TerritorioClique.Dono.Jogador1);
        territory.DefinirTropas(3);
        Assert.That(territory.RemoverTropasPermitindoEsvaziamento(3), Is.True);
        Assert.That(territory.IsEmpty, Is.True);
        Assert.That(territory.PossuiDono, Is.False);
        Assert.That(territory.dono, Is.EqualTo(TerritorioClique.Dono.Neutro));
        Assert.That(territory.Tropas, Is.Zero);
        Object.DestroyImmediate(territory.gameObject);
    }

    [Test]
    public void NeutralAndEmptyUseSameOwnerlessOverlaySemantics()
    {
        TerritorioClique neutral = CreateTerritoryWithTroops();
        TerritorioClique empty = CreateTerritoryWithTroops();
        neutral.DefinirNeutro();
        empty.DefinirVazio();
        Assert.That(neutral.UsaOverlaySemDono, Is.True);
        Assert.That(empty.UsaOverlaySemDono, Is.True);
        Assert.That(neutral.Estado, Is.Not.EqualTo(empty.Estado));
        Object.DestroyImmediate(neutral.gameObject);
        Object.DestroyImmediate(empty.gameObject);
    }

    [Test]
    public void EmptyOccupationRequiresAtLeastTwoTroopsAtOwnedOrigin()
    {
        TerritorioClique origin = CreateInitializedTerritoryWithTroops();
        TerritorioClique empty = CreateInitializedTerritoryWithTroops();
        origin.DefinirDono(TerritorioClique.Dono.Jogador1);
        empty.DefinirVazio();
        origin.DefinirTropas(1);
        Assert.That(SistemaAcoesTerrestres.PodeOcuparVazio(
            origin, empty, TerritorioClique.Dono.Jogador1), Is.False);
        origin.DefinirTropas(2);
        Assert.That(SistemaAcoesTerrestres.PodeOcuparVazio(
            origin, empty, TerritorioClique.Dono.Jogador1), Is.True);
        Object.DestroyImmediate(origin.gameObject);
        Object.DestroyImmediate(empty.gameObject);
    }

    [Test]
    public void NeutralTerritoryIsNotDirectEmptyOccupationTarget()
    {
        TerritorioClique origin = CreateTerritoryWithTroops();
        TerritorioClique neutral = CreateTerritoryWithTroops();
        origin.DefinirDono(TerritorioClique.Dono.Jogador1);
        origin.DefinirTropas(2);
        neutral.DefinirNeutro();
        Assert.That(SistemaAcoesTerrestres.PodeOcuparVazio(
            origin, neutral, TerritorioClique.Dono.Jogador1), Is.False);
        Assert.That(SistemaAcoesTerrestres.ObterTipoEsperado(
            TerritorioClique.Dono.Jogador1, neutral), Is.EqualTo("ATAQUE"));
        Object.DestroyImmediate(origin.gameObject);
        Object.DestroyImmediate(neutral.gameObject);
    }

    [Test]
    public void EmptyTerritoryIsOccupiedWithoutCombatAndLeavesOneAtOrigin()
    {
        TerritorioClique origin = CreateInitializedTerritoryWithTroops();
        TerritorioClique empty = CreateInitializedTerritoryWithTroops();
        TerritorioFronteiras borders = origin.gameObject.AddComponent<TerritorioFronteiras>();
        borders.Configurar(new[] { empty });
        origin.DefinirDono(TerritorioClique.Dono.Jogador1);
        origin.DefinirTropas(2);
        empty.DefinirVazio();

        var resolverObject = new GameObject("EmptyOccupationResolverTest");
        ResolvedorCombate combat = resolverObject.AddComponent<ResolvedorCombate>();
        ResolvedorAcoesTerrestres resolver =
            resolverObject.AddComponent<ResolvedorAcoesTerrestres>();
        resolver.Inicializar(combat);
        OrdemTerrestre order = new OrdemTerrestre(origin, empty, 1,
            TerritorioClique.Dono.Jogador1, 1);

        ResultadoAcaoTerrestre result = resolver.ResolverOrdem(order);
        Assert.That(result.Executada, Is.True);
        Assert.That(result.Tipo,
            Is.EqualTo(ResultadoAcaoTerrestre.TipoResultado.OcupacaoVazio));
        Assert.That(result.Combate, Is.Null);
        Assert.That(origin.Tropas, Is.EqualTo(1));
        Assert.That(empty.IsOccupied, Is.True);
        Assert.That(empty.dono, Is.EqualTo(TerritorioClique.Dono.Jogador1));
        Assert.That(empty.Tropas, Is.EqualTo(1));

        Object.DestroyImmediate(resolverObject);
        Object.DestroyImmediate(origin.gameObject);
        Object.DestroyImmediate(empty.gameObject);
    }

    [Test]
    public void InitialOneVsOneProvidesEightReinforcements()
    {
        Assert.That(GerenciadorRodada.ReforcosIniciaisUmContraUm, Is.EqualTo(8));
    }

    [Test]
    public void TwoStandardTroopsPlusEightReinforcementsProduceTenEffectiveTroops()
    {
        const int troopsAlreadyOnAssignedTerritories = 2;
        Assert.That(troopsAlreadyOnAssignedTerritories +
            GerenciadorRodada.ReforcosIniciaisUmContraUm, Is.EqualTo(10));
    }

    [Test]
    public void EightReinforcementsAreAddedToExistingTerritoryTroops()
    {
        int[] distributedReinforcements = { 7, 1 };
        int[] finalTerritoryTroops = { 1 + distributedReinforcements[0],
            1 + distributedReinforcements[1] };
        Assert.That(distributedReinforcements.Sum(),
            Is.EqualTo(GerenciadorRodada.ReforcosIniciaisUmContraUm));
        Assert.That(finalTerritoryTroops.Sum(), Is.EqualTo(10));
    }

    [Test]
    public void NeutralTroopsAreExcludedFromPlayerInitialTotal()
    {
        int ownedStandardTroops = 2;
        int neutralTroops = 38;
        int available = GerenciadorRodada.ReforcosIniciaisUmContraUm;
        Assert.That(ownedStandardTroops + available, Is.EqualTo(10));
        Assert.That(ownedStandardTroops + neutralTroops + available,
            Is.Not.EqualTo(10));
    }

    [Test]
    public void TerritoryWithOneTroopCannotSupplyAttackOrMovement()
    {
        TerritorioClique territory = CreateTerritoryWithTroops();
        territory.DefinirDono(TerritorioClique.Dono.Jogador1);
        territory.DefinirTropas(1);
        var queueObject = new GameObject("QueueTest");
        FilaAcoes queue = queueObject.AddComponent<FilaAcoes>();
        Assert.That(queue.TropasDisponiveis(
            territory, TerritorioClique.Dono.Jogador1), Is.Zero);
        Object.DestroyImmediate(queueObject);
        Object.DestroyImmediate(territory.gameObject);
    }

    // ============================================================
    // 03. PISO DE REFORÇOS EXCLUSIVO DA FAMÍLIA 1x1
    // ============================================================

    [TestCase(1, 6)]
    [TestCase(2, 6)]
    [TestCase(5, 6)]
    [TestCase(6, 6)]
    [TestCase(7, 7)]
    public void OneVsOneRegularRoundsApplyMinimumSix(int calculated, int expected)
    {
        Assert.That(GerenciadorRodada.AplicarPisoReforcosUmContraUm(
            calculated, true), Is.EqualTo(expected));
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(5)]
    [TestCase(7)]
    public void OtherModesDoNotReceiveOneVsOneFloor(int calculated)
    {
        Assert.That(GerenciadorRodada.AplicarPisoReforcosUmContraUm(
            calculated, false), Is.EqualTo(calculated));
    }

    [Test]
    public void TwoVsTwoUsesIndividualTerritoryFormulaWithoutFloor()
    {
        Assert.That(GerenciadorRodada.CalcularReforcosPorModo(
            19, 0, false, 2), Is.EqualTo(9));
        Assert.That(GerenciadorRodada.CalcularReforcosPorModo(
            10, 0, false, 2), Is.EqualTo(5));
    }

    [Test]
    public void FfaUsesSameTerritoryFormulaWithoutOneVsOneFloor()
    {
        Assert.That(GerenciadorRodada.CalcularReforcosPorModo(
            9, 0, false, 2), Is.EqualTo(4));
    }

    [Test]
    public void OneVsOneRegularRoundPreservesMinimumSix()
    {
        Assert.That(GerenciadorRodada.CalcularReforcosPorModo(
            2, 0, true, 2), Is.EqualTo(6));
    }

    [Test]
    public void OneVsOneRoundOneProvidesEightReinforcements()
    {
        Assert.That(GerenciadorRodada.CalcularReforcosPorModo(
            2, 0, true, 1), Is.EqualTo(8));
    }

    [Test]
    public void TwoVsTwoRoundOneProvidesEightReinforcementsPerPlayer()
    {
        Assert.That(GerenciadorRodada.CalcularReforcosPorModo(
            10, 0, false, 1), Is.EqualTo(8));
    }

    [Test]
    public void FfaRoundOneProvidesEightReinforcementsPerPlayer()
    {
        Assert.That(GerenciadorRodada.CalcularReforcosPorModo(
            9, 0, false, 1), Is.EqualTo(8));
    }

    [Test]
    public void ThreeVsThreeRoundOneAndRegularRoundsUseIndividualFormula()
    {
        Assert.That(GerenciadorRodada.CalcularReforcosPorModo(
            7, 0, false, 1), Is.EqualTo(8));
        Assert.That(GerenciadorRodada.CalcularReforcosPorModo(
            10, 2, false, 2), Is.EqualTo(7));
        Assert.That(GerenciadorRodada.CalcularReforcosPorModo(
            9, 0, false, 2), Is.EqualTo(4));
    }

    // ============================================================
    // 04. CONTADOR INDEPENDENTE E ATUALIZAÇÃO NUMÉRICA
    // ============================================================

    [Test]
    public void NewCounterShowsZeroWithoutChangingTerritoryState()
    {
        TerritorioClique territory = CreateTerritoryWithTroops();
        territory.DefinirNeutro();
        var counterObject = new GameObject("NewCounterTest");
        counterObject.transform.SetParent(territory.transform);
        WDArmyCounterView view = counterObject.AddComponent<WDArmyCounterView>();
        view.Configurar(territory);
        view.ExibirQuantidade(0);
        Assert.That(view.QuantidadeExibida, Is.Zero);
        Assert.That(view.Territorio, Is.SameAs(territory));
        Assert.That(territory.IsNeutral, Is.True);
        Object.DestroyImmediate(territory.gameObject);
    }

    [Test]
    public void NewCounterTintsManualBodyWithoutTintingTroopText()
    {
        TerritorioClique territory = CreateTerritoryWithTroops();
        try
        {
            var counterObject = new GameObject("NewCounterStructureTest");
            counterObject.transform.SetParent(territory.transform);
            WDArmyCounterView view = counterObject.AddComponent<WDArmyCounterView>();
            view.Configurar(territory);
            view.Exibir(1, new Color(0.1f, 0.4f, 0.8f, 1f));
            Color first = view.BodyRenderer.color;
            view.Exibir(1, new Color(0.8f, 0.2f, 0.1f, 1f));
            Assert.That(view.BodyRenderer.color, Is.Not.EqualTo(first));
            Assert.That(view.TroopText.color, Is.EqualTo(Color.white));
            Assert.That(view.VisualRoot.GetComponentsInChildren<SpriteRenderer>(), Has.Length.EqualTo(1));
        }
        finally { Object.DestroyImmediate(territory.gameObject); }
    }

    // ============================================================
    // 05. PALETA GLOBAL E PREFERÊNCIA DO PERFIL
    // ============================================================

    [Test]
    public void GlobalPlayerPaletteContainsOfficialColors()
    {
        WDPlayerPaletteConfig palette = WDPlayerPaletteConfig.LoadGlobal();
        string[] expected =
        {
            "DE4441", "804BCF", "40D13D", "AA228A", "17B1E2",
            "8A7D7D", "EAD73C", "E8691A", "783434", "996F29"
        };

        Assert.That(palette, Is.Not.Null);
        Assert.That(palette.Colors.Count, Is.EqualTo(expected.Length));
        for (int i = 0; i < expected.Length; i++)
            Assert.That(ColorUtility.ToHtmlStringRGB(palette.Colors[i]),
                Is.EqualTo(expected[i]));
    }

    [Test]
    public void MockProfileResolvesPreferredColorThroughGlobalPalette()
    {
        WarDominionHomeData profile =
            Resources.Load<WarDominionHomeData>("UI/Home/WarDominionHomeMockData");
        Assert.That(profile, Is.Not.Null);
        Assert.That(profile.PreferredColorIndex, Is.EqualTo(4));
        Assert.That(ColorUtility.ToHtmlStringRGB(profile.PlayerColor),
            Is.EqualTo("17B1E2"));
    }

    [Test]
    public void CurrentCounterDisablesLegacyRenderersForOwnedTerritory()
    {
        TerritorioClique territory = CreateTerritoryWithTroops();
        territory.DefinirDono(TerritorioClique.Dono.Jogador1);
        var counterObject = new GameObject("CounterTest");
        counterObject.transform.SetParent(territory.transform);
        ContadorTropas counter = counterObject.AddComponent<ContadorTropas>();
        counter.Configurar(territory);
        WDArmyCounterView currentView = counterObject.GetComponent<WDArmyCounterView>();
        Transform currentRoot = counterObject.transform.Find("ArmyCounterSpriteRoot");
        SpriteRenderer legacyArc = counterObject.transform.Find("Borda")
            .GetComponent<SpriteRenderer>();

        Assert.That(currentView, Is.Not.Null);
        Assert.That(currentView.Territorio, Is.SameAs(territory));
        Assert.That(currentRoot, Is.Not.Null);
        Assert.That(currentRoot.gameObject.activeSelf, Is.True);
        Assert.That(currentRoot.GetComponentsInChildren<SpriteRenderer>(), Has.Length.EqualTo(1));
        Assert.That(currentView.BodyRenderer.enabled, Is.True);
        Assert.That(currentRoot.GetComponentsInChildren<TextMeshPro>(), Has.Length.EqualTo(1));
        Assert.That(counterObject.GetComponents<WDArmyCounterView>(), Has.Length.EqualTo(1));
        Transform oldRoot = counterObject.transform.Find("ArmyCounterRoot");
        Assert.That(oldRoot == null || !oldRoot.gameObject.activeSelf, Is.True);
        Assert.That(legacyArc.enabled, Is.False);
        Assert.That(counterObject.transform.Find("Fundo").GetComponent<SpriteRenderer>().enabled,
            Is.False);
        Assert.That(counterObject.transform.Find("BordaExterna")
            .GetComponent<SpriteRenderer>().enabled, Is.False);
        Assert.That(counterObject.transform.Find("Numero").GetComponent<TextMeshPro>().enabled,
            Is.False);
        Object.DestroyImmediate(territory.gameObject);
    }

    // ============================================================
    // 06. MOVIMENTO ALIADO PREPARADO E RESOLVIDO EM DUAS FASES
    // ============================================================

    [Test]
    public void AlliedMovementDoesNotMutateDuringPreparationAndKeepsOwnerOnResolution()
    {
        TerritorioClique origin = CreateInitializedTerritoryWithTroops();
        TerritorioClique destination = CreateInitializedTerritoryWithTroops();
        var systems = new GameObject("AlliedMovementSystems");
        WDMatchSetupContext.Store(CreateTeamSetup());
        try
        {
            origin.gameObject.AddComponent<TerritorioFronteiras>()
                .Configurar(new[] { destination });
            origin.DefinirDono(TerritorioClique.Dono.Jogador1);
            origin.DefinirTropas(5);
            destination.DefinirDono(TerritorioClique.Dono.Jogador3);
            destination.DefinirTropas(2);

            FilaAcoes queue = systems.AddComponent<FilaAcoes>();
            SistemaAcoesTerrestres preparation = systems.AddComponent<SistemaAcoesTerrestres>();
            preparation.Inicializar(queue);
            Assert.That(preparation.Registrar(origin, destination, 3,
                TerritorioClique.Dono.Jogador1), Is.True);
            Assert.That(origin.Tropas, Is.EqualTo(5));
            Assert.That(destination.Tropas, Is.EqualTo(2));

            ResolvedorAcoesTerrestres resolver = systems.AddComponent<ResolvedorAcoesTerrestres>();
            resolver.Inicializar(systems.AddComponent<ResolvedorCombate>());
            ResultadoAcaoTerrestre result = resolver.ResolverOrdem(queue.Ordens[0]);
            Assert.That(result.Tipo,
                Is.EqualTo(ResultadoAcaoTerrestre.TipoResultado.MovimentoAliado));
            Assert.That(result.QuantidadeEfetiva, Is.EqualTo(3));
            Assert.That(result.TropasOrigemAntes, Is.EqualTo(5));
            Assert.That(result.TropasDestinoAntes, Is.EqualTo(2));
            Assert.That(destination.Tropas, Is.EqualTo(5));
            Assert.That(destination.dono, Is.EqualTo(TerritorioClique.Dono.Jogador3));
        }
        finally
        {
            WDMatchSetupContext.Clear();
            Object.DestroyImmediate(origin.gameObject);
            Object.DestroyImmediate(destination.gameObject);
            Object.DestroyImmediate(systems);
        }
    }

    // ============================================================
    // 07. TRANSFERÊNCIA POR IDENTIDADE EM ROUNDS SUCESSIVOS
    // ============================================================

    [Test]
    public void TransferQueueResetsPerRoundAndDoesNotConflictWithPreparedAttacks()
    {
        var systems = new GameObject("TransferRoundSystems");
        FilaTransferencias transfers = systems.AddComponent<FilaTransferencias>();
        SistemaTransferencias transferSystem = systems.AddComponent<SistemaTransferencias>();
        transferSystem.Inicializar(transfers);
        FilaAcoes actions = systems.AddComponent<FilaAcoes>();
        WDMatchSetupContext.Store(CreateTeamSetup());
        var territories = new List<TerritorioClique>();
        try
        {
            TerritorioClique.Dono[] authors =
            {
                TerritorioClique.Dono.Jogador1,
                TerritorioClique.Dono.Jogador4,
                TerritorioClique.Dono.Jogador3
            };
            TerritorioClique.Dono[] recipients =
            {
                TerritorioClique.Dono.Jogador3,
                TerritorioClique.Dono.Jogador2,
                TerritorioClique.Dono.Jogador1
            };

            for (int round = 0; round < authors.Length; round++)
            {
                TerritorioClique origin = CreateInitializedTerritoryWithTroops();
                TerritorioClique destination = CreateInitializedTerritoryWithTroops();
                TerritorioClique handoff = CreateInitializedTerritoryWithTroops();
                territories.Add(origin);
                territories.Add(destination);
                territories.Add(handoff);
                origin.DefinirDono(authors[round]);
                origin.DefinirTropas(4);
                destination.DefinirDono(recipients[round]);
                destination.DefinirTropas(2);
                handoff.DefinirDono(authors[round]);
                handoff.DefinirTropas(1);

                Assert.That(actions.Adicionar(new OrdemTerrestre(
                    origin, destination, 1, authors[round], 1)), Is.True);
                Assert.That(transferSystem.Registrar(
                    handoff, authors[round], recipients[round], out string reason),
                    Is.True, reason);
                Assert.That(transfers.PossuiPara(authors[round]), Is.True);
                Assert.That(transferSystem.Registrar(
                    handoff, authors[round], recipients[round], out _), Is.False);

                transfers.Limpar();
                actions.Limpar();
            }
        }
        finally
        {
            WDMatchSetupContext.Clear();
            foreach (TerritorioClique territory in territories)
                if (territory != null)
                    Object.DestroyImmediate(territory.gameObject);
            Object.DestroyImmediate(systems);
        }
    }

    [Test]
    public void ThreeVsThreeTransferPreservesTroopsAndUsesChosenAllyWithoutAdjacency()
    {
        var systems = new GameObject("ThreeVsThreeTransferSystems");
        FilaTransferencias queue = systems.AddComponent<FilaTransferencias>();
        SistemaTransferencias transferSystem = systems.AddComponent<SistemaTransferencias>();
        ResolvedorTransferencias resolver = systems.AddComponent<ResolvedorTransferencias>();
        transferSystem.Inicializar(queue);
        TerritorioClique territory = CreateInitializedTerritoryWithTroops();
        WDMatchSetupContext.Store(CreateThreeVsThreeSetup());
        try
        {
            territory.DefinirDono(TerritorioClique.Dono.Jogador1);
            territory.DefinirTropas(5);

            Assert.That(transferSystem.Registrar(
                territory, TerritorioClique.Dono.Jogador1,
                TerritorioClique.Dono.Jogador5, out string reason), Is.True, reason);
            List<ResultadoTransferencia> results = resolver.Resolver(
                queue, new[] { TerritorioClique.Dono.Jogador1 });

            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(territory.dono, Is.EqualTo(TerritorioClique.Dono.Jogador5));
            Assert.That(territory.Tropas, Is.EqualTo(5));
        }
        finally
        {
            WDMatchSetupContext.Clear();
            Object.DestroyImmediate(territory.gameObject);
            Object.DestroyImmediate(systems);
        }
    }

    private static TerritorioClique CreateTerritory()
    {
        var gameObject = new GameObject("NeutralTest");
        return gameObject.AddComponent<TerritorioClique>();
    }

    private static TerritorioClique CreateTerritoryWithTroops()
    {
        var gameObject = new GameObject("InitialOwnedTest");
        gameObject.AddComponent<TerritorioTropas>();
        return gameObject.AddComponent<TerritorioClique>();
    }

    // ============================================================
    // 08. INICIALIZAÇÃO EXPLÍCITA E SEGURA DOS FIXTURES EDITMODE
    // ============================================================

    private static TerritorioClique CreateInitializedTerritoryWithTroops()
    {
        TerritorioClique territory = CreateTerritoryWithTroops();
        TerritorioTropas troops = territory.GetComponent<TerritorioTropas>();
        typeof(TerritorioClique)
            .GetField("territorioTropas", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(territory, troops);
        troops.Inicializar();
        return territory;
    }

    private static void InvokeStartForEditMode(TerritorioClique territory)
    {
        MethodInfo start = typeof(TerritorioClique)
            .GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(start, Is.Not.Null);
        start.Invoke(territory, null);
    }

    private static void SetSerializedState(
        TerritorioClique territory,
        TerritorioClique.EstadoTerritorial state)
    {
        typeof(TerritorioClique)
            .GetField("estadoTerritorial",
                BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(territory, state);
    }

    private static WDMatchSetup CreateTeamSetup()
    {
        var participants = new List<WDMatchParticipant>
        {
            new("a1", "A1", 0, 0, Color.red, "", "", WDMatchParticipantKind.Local),
            new("b1", "B1", 1, 1, Color.blue, "", "", WDMatchParticipantKind.Remote),
            new("a2", "A2", 0, 2, Color.green, "", "", WDMatchParticipantKind.Remote),
            new("b2", "B2", 1, 3, Color.yellow, "", "", WDMatchParticipantKind.Remote)
        };
        return new WDMatchSetup("2x2", "normal", "classic", "", "", 10, true, 1,
            participants);
    }

    private static WDMatchSetup CreateThreeVsThreeSetup()
    {
        var participants = new List<WDMatchParticipant>
        {
            new("a1", "A1", 0, 0, Color.red, "", "", WDMatchParticipantKind.Local),
            new("b1", "B1", 1, 1, Color.blue, "", "", WDMatchParticipantKind.Remote),
            new("a2", "A2", 0, 2, Color.green, "", "", WDMatchParticipantKind.Remote),
            new("b2", "B2", 1, 3, Color.yellow, "", "", WDMatchParticipantKind.Remote),
            new("a3", "A3", 0, 4, Color.cyan, "", "", WDMatchParticipantKind.Remote),
            new("b3", "B3", 1, 5, Color.magenta, "", "", WDMatchParticipantKind.Remote)
        };
        return new WDMatchSetup("3x3", "normal", "classic", "",
            "standard_deck", 10, true, 1, participants);
    }
}
