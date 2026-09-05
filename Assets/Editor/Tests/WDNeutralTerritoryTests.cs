using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

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
    public void NewCounterHasTransparentRevealWithoutOwnerColorDependency()
    {
        TerritorioClique territory = CreateTerritoryWithTroops();
        var counterObject = new GameObject("NewCounterStructureTest");
        counterObject.transform.SetParent(territory.transform);
        WDArmyCounterView view = counterObject.AddComponent<WDArmyCounterView>();
        view.Configurar(territory);
        Transform root = counterObject.transform.Find("WDArmyCounter");
        Assert.That(root.Find("TerritoryReveal"), Is.Not.Null);
        Assert.That(root.Find("TerritoryReveal").GetComponent<SpriteRenderer>(), Is.Null);
        Assert.That(root.Find("OuterFrame"), Is.Not.Null);
        Assert.That(root.Find("CenterBackground"), Is.Not.Null);
        Assert.That(root.Find("TroopNumber"), Is.Not.Null);
        Object.DestroyImmediate(territory.gameObject);
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
        Transform currentRoot = counterObject.transform.Find("WDArmyCounter");
        SpriteRenderer legacyArc = counterObject.transform.Find("Borda")
            .GetComponent<SpriteRenderer>();

        Assert.That(currentView, Is.Not.Null);
        Assert.That(currentView.Territorio, Is.SameAs(territory));
        Assert.That(currentRoot, Is.Not.Null);
        Assert.That(currentRoot.Find("OuterFrame").GetComponent<SpriteRenderer>().enabled,
            Is.True);
        Assert.That(legacyArc.enabled, Is.False);
        Assert.That(counterObject.transform.Find("Fundo").GetComponent<SpriteRenderer>().enabled,
            Is.False);
        Assert.That(counterObject.transform.Find("BordaExterna")
            .GetComponent<SpriteRenderer>().enabled, Is.False);
        Assert.That(counterObject.transform.Find("Numero").GetComponent<TextMeshPro>().enabled,
            Is.False);
        Object.DestroyImmediate(territory.gameObject);
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
    // 06. INICIALIZAÇÃO EXPLÍCITA E SEGURA DOS FIXTURES EDITMODE
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
}
