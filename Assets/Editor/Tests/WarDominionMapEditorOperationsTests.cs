using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class WarDominionMapEditorOperationsTests
{
    private readonly List<Object> disposable = new List<Object>();

    // ============================================================
    // 01. CONEXÕES BIDIRECIONAIS, REMOÇÃO E DUPLICAÇÃO
    // ============================================================

    [Test]
    public void AddConnectionCreatesOneBidirectionalRecord()
    {
        DefinicaoMapa map = CreateMap("map", true, 1);
        Assert.That(WarDominionMapEditorOperations.AddBidirectionalConnection(
            map, "a", "b", TipoConexaoMapa.Especial), Is.True);
        Assert.That(map.Conexoes, Has.Count.EqualTo(1));
        Assert.That(map.Conexoes[0].bidirecional, Is.True);
        Assert.That(WarDominionMapEditorOperations.ContainsConnection(map, "b", "a"), Is.True);
    }

    [Test]
    public void DuplicateConnectionIsNotAddedAndRemovalWorksFromEitherDirection()
    {
        DefinicaoMapa map = CreateMap("map", true, 1);
        WarDominionMapEditorOperations.AddBidirectionalConnection(map, "a", "b",
            TipoConexaoMapa.Terrestre);
        Assert.That(WarDominionMapEditorOperations.AddBidirectionalConnection(
            map, "b", "a", TipoConexaoMapa.Terrestre), Is.False);
        Assert.That(map.Conexoes, Has.Count.EqualTo(1));
        Assert.That(WarDominionMapEditorOperations.RemoveConnection(map, "b", "a"), Is.True);
        Assert.That(map.Conexoes, Is.Empty);
    }

    // ============================================================
    // 02. VALIDAÇÃO PURA E TROCA SEGURA DE ARTE
    // ============================================================

    [Test]
    public void ValidationDetectsMissingRegionIsolatedTerritoryAndDuplicateMapId()
    {
        DefinicaoMapa first = CreateMap("duplicate", true, 1);
        DefinicaoMapa second = CreateMap("duplicate", true, 1);
        first.Territorios[0].regiaoId = string.Empty;
        WDMapEditorValidationReport report = WarDominionMapEditorOperations.Validate(
            first, new[] { first, second });
        string[] codes = report.Issues.Select(issue => issue.Codigo).ToArray();
        Assert.That(codes, Does.Contain("TERRITORIO_SEM_REGIAO"));
        Assert.That(codes, Does.Contain("TERRITORIO_ISOLADO"));
        Assert.That(codes, Does.Contain("MAPA_ID_DUPLICADO"));
    }

    [Test]
    public void ValidationDetectsMissingArtAndRegionBonus()
    {
        DefinicaoMapa map = CreateMap("map", false, 0);
        string[] codes = WarDominionMapEditorOperations.Validate(map, new[] { map })
            .Issues.Select(issue => issue.Codigo).ToArray();
        Assert.That(codes, Does.Contain("ARTE_AUSENTE"));
        Assert.That(codes, Does.Contain("REGIAO_SEM_BONUS"));
    }

    [Test]
    public void ReplacingArtPreservesTerritoriesRegionsAndConnections()
    {
        DefinicaoMapa map = CreateMap("map", true, 1);
        WarDominionMapEditorOperations.AddBidirectionalConnection(map, "a", "b",
            TipoConexaoMapa.Terrestre);
        int territories = map.Territorios.Count;
        int regions = map.Regioes.Count;
        int connections = map.Conexoes.Count;
        Sprite replacement = CreateSprite();
        Assert.That(WarDominionMapEditorOperations.ReplaceMainArt(map, replacement), Is.True);
        Assert.That(map.ArteBase, Is.SameAs(replacement));
        Assert.That(map.Territorios, Has.Count.EqualTo(territories));
        Assert.That(map.Regioes, Has.Count.EqualTo(regions));
        Assert.That(map.Conexoes, Has.Count.EqualTo(connections));
    }

    // ============================================================
    // 03. ROUND-TRIP ESPACIAL E SELEÇÃO MÚLTIPLA
    // ============================================================

    [TestCase(1365f, 1024f, 100f, 50f)]
    [TestCase(2048f, 1024f, -350f, 210f)]
    [TestCase(900f, 1600f, 42f, -515f)]
    public void ViewportTransformRoundTripPreservesMapPosition(float width,
        float height, float x, float y)
    {
        var bounds = new Bounds(new Vector3(37f, -21f), new Vector3(width, height));
        var transform = new WDMapViewportTransform(new Rect(120f, 80f, 777f, 431f), bounds);
        Vector2 original = new Vector2(x, y);
        Vector2 roundTrip = transform.ViewportToMap(transform.MapToViewport(original));
        Assert.That(roundTrip.x, Is.EqualTo(original.x).Within(0.001f));
        Assert.That(roundTrip.y, Is.EqualTo(original.y).Within(0.001f));
    }

    [Test]
    public void SelectionSupportsSimpleClickControlToggleAndExactPair()
    {
        var selection = new WDMapEditorSelection();
        selection.Click(2, false);
        Assert.That(selection.Indices, Is.EqualTo(new[] { 2 }));
        selection.Click(5, true);
        Assert.That(selection.HasPair, Is.True);
        selection.Click(5, true);
        Assert.That(selection.Indices, Is.EqualTo(new[] { 2 }));
        selection.Click(7, true);
        selection.Click(9, true);
        Assert.That(selection.Indices.Count, Is.EqualTo(3));
        Assert.That(selection.HasPair, Is.False);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (Object item in disposable)
            if (item != null) Object.DestroyImmediate(item);
        disposable.Clear();
    }

    private DefinicaoMapa CreateMap(string id, bool withArt, int bonus)
    {
        DefinicaoMapa map = ScriptableObject.CreateInstance<DefinicaoMapa>();
        disposable.Add(map);
        Sprite art = withArt ? CreateSprite() : null;
        var territories = new List<DefinicaoTerritorioMapa>
        {
            new DefinicaoTerritorioMapa { id = "a", nomeExibido = "A", regiaoId = "r" },
            new DefinicaoTerritorioMapa { id = "b", nomeExibido = "B", regiaoId = "r" }
        };
        var regions = new List<DefinicaoRegiaoMapa>
        {
            new DefinicaoRegiaoMapa
            {
                id = "r", nomeExibido = "Region", bonus = bonus,
                territorioIds = new List<string> { "a", "b" }
            }
        };
        map.ConfigurarNoEditor(id, id, id, string.Empty, "default", art,
            Vector2.zero, 5f, territories, regions, new List<DefinicaoConexaoMapa>());
        return map;
    }

    private Sprite CreateSprite()
    {
        Texture2D texture = new Texture2D(4, 4);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4),
            new Vector2(0.5f, 0.5f), 1f);
        disposable.Add(sprite);
        disposable.Add(texture);
        return sprite;
    }
}
