using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class WDClassicTerritoryArtPassPrototypeTests
{
    private const string RuntimeRoot =
        "Assets/Mapas/Classic/Runtime16x9/";
    private const string RejectedLot1Root =
        "Assets/Mapas/Classic/ArtPass2/Lot1/Territories/";
    private const string StylizedLot1Root =
        "Assets/Mapas/Classic/ArtPass2/Lot1Stylized/Territories/";
    private const string MarbleBRoot =
        "Assets/Mapas/Classic/ArtPass2/MarbleB/Territories/";
    private const string MasterPath =
        "Assets/Mapas/Classic/ArtPass2/MarbleB/Master/" +
        "Classic_MarbleB_Master.png";
    private const string BrazilV1Path =
        "Assets/Mapas/Classic/ArtPass2/Territories/" +
        "Brazil_ArtPass2_Prototype.png";
    private const string BrazilV2Path =
        "Assets/Mapas/Classic/ArtPass2/Territories/" +
        "Brazil_ArtPass2_V2.png";
    private const string SettingsPath =
        "Assets/Resources/Config/WDClassicTerritoryArtPassPrototype.asset";
    private const string TerritoryVisualPath =
        "Assets/Scripts/Territorios/TerritorioVisual.cs";

    private static readonly string[] PilotTerritoryIds =
        { "Brazil", "Argentina", "Peru", "Venezuela" };

    // ============================================================
    // 0. REGISTRO ESPACIAL DERIVADO DA CENA, PIVÔS E IMPORTADORES
    // ============================================================
    private static readonly TerritoryRegistration[] Registrations =
    {
        new TerritoryRegistration("Afghanistan", "Afghanistan", 1177, 287),
        new TerritoryRegistration("Alaska", "Yukon", 319, 131),
        new TerritoryRegistration("Alberta", "Ontario", 451, 218),
        new TerritoryRegistration("Algeria", "Algeria", 867, 432),
        new TerritoryRegistration("Arabia", "Arabia", 1072, 387),
        new TerritoryRegistration("Argentina", "Argentina", 641, 695),
        new TerritoryRegistration("Brazil", "Brazil", 632, 544),
        new TerritoryRegistration("China", "China", 1311, 312),
        new TerritoryRegistration("Congo", "Congo", 996, 586),
        new TerritoryRegistration("EastAustralia", "WestAustralia", 1579, 698),
        new TerritoryRegistration("EastUSA", "EastUSA", 538, 299),
        new TerritoryRegistration("Egypt", "Egypt", 1001, 465),
        new TerritoryRegistration("Ethiopia", "Ethiopia", 1053, 523),
        new TerritoryRegistration("Germany", "Germany", 954, 257),
        new TerritoryRegistration("Greenland", "Greenland", 674, 85),
        new TerritoryRegistration("Iceland", "Iceland", 799, 186),
        new TerritoryRegistration("India", "India", 1258, 421),
        new TerritoryRegistration("Indochina", "Indochina", 1385, 473),
        new TerritoryRegistration("Indonesia", "Indonesia", 1376, 566),
        new TerritoryRegistration("Irkutsk", "Irkutsk", 1389, 193),
        new TerritoryRegistration("Italy", "Italy", 960, 330),
        new TerritoryRegistration("Japan", "Japan", 1523, 261),
        new TerritoryRegistration("Kamchatka", "Kamchatka", 1460, 128),
        new TerritoryRegistration("Madagascar", "Madagascar", 1151, 671),
        new TerritoryRegistration("Mexico", "Mexico", 468, 403),
        new TerritoryRegistration("Mongolia", "Mongolia", 1390, 265),
        new TerritoryRegistration("NewGuinea", "NewGuinea", 1548, 631),
        new TerritoryRegistration("Ontario", "Quebec", 554, 218),
        new TerritoryRegistration("Peru", "Peru", 615, 621),
        new TerritoryRegistration("Quebec", "Alaska", 640, 216),
        new TerritoryRegistration("Russia", "Russia", 1062, 164),
        new TerritoryRegistration("Scandinavia", "Scandinavia", 967, 160),
        new TerritoryRegistration("Siberia", "Siberia", 1258, 97),
        new TerritoryRegistration("SouthAfrica", "SouthAfrica", 1014, 686),
        new TerritoryRegistration("Spain", "Spain", 876, 325),
        new TerritoryRegistration("UnitedKingdom", "UnitedKingdom", 857, 226),
        new TerritoryRegistration("Ural", "Ural", 1195, 126),
        new TerritoryRegistration("Venezuela", "Venezuela", 585, 508),
        new TerritoryRegistration("WestAustralia", "EastAustralia", 1483, 697),
        new TerritoryRegistration("WestUSA", "WestUSA", 447, 299),
        new TerritoryRegistration("Yakutsk", "Yakutsk", 1379, 120),
        new TerritoryRegistration("Yukon", "Alberta", 454, 126),
    };
    private static readonly string[] TerritoryIds =
        Registrations.Select(item => item.TerritoryId).ToArray();
    private static readonly Vector2Int[] GlobalOrigins =
        Registrations.Select(item => item.Origin).ToArray();

    // ============================================================
    // 1. INTEGRIDADE DOS SPRITES HISTÓRICOS E DO LOTE 1
    // ============================================================
    [Test]
    public void BrazilV1AndV2KeepOriginalGeometryAndAlpha()
    {
        AssertDerivedGeometryAndAlpha("Brazil", BrazilV1Path);
        AssertDerivedGeometryAndAlpha("Brazil", BrazilV2Path);
    }

    [Test]
    public void RejectedLot1RemainsIntactAndGeometricallyValid()
    {
        foreach (string territoryId in PilotTerritoryIds)
            AssertDerivedGeometryAndAlpha(
                territoryId, RejectedLot1Path(territoryId));
    }

    [Test]
    public void MarbleBPreservesGeometryAndAlphaForAllTerritories()
    {
        foreach (string territoryId in TerritoryIds)
            AssertDerivedGeometryAndAlpha(
                territoryId, MarbleBPath(territoryId));
    }

    // ============================================================
    // 2. CONFIGURAÇÃO PADRÃO, CATÁLOGO E ISOLAMENTO
    // ============================================================
    [Test]
    public void NewSettingsStartDisabled()
    {
        var settings = ScriptableObject.CreateInstance<
            WDClassicTerritoryArtPassSettings>();
        try
        {
            Assert.That(settings.PrototypeEnabled, Is.False);
            Assert.That(settings.MapId, Is.EqualTo("classic"));
        }
        finally
        {
            Object.DestroyImmediate(settings);
        }
    }

    [Test]
    public void CatalogTargetsOnlyClassicAndContainsAllExpectedSprites()
    {
        WDClassicTerritoryArtPassSettings settings =
            AssetDatabase.LoadAssetAtPath<WDClassicTerritoryArtPassSettings>(
                SettingsPath);

        Assert.That(settings, Is.Not.Null);
        // ============================================================
        // 2.1. PREFERÊNCIA VISUAL NÃO FAZ PARTE DA INTEGRIDADE
        // ============================================================
        // O usuário pode manter o catálogo ligado durante a avaliação. O valor
        // padrão continua coberto por NewSettingsStartDisabled, enquanto este
        // teste valida mapa, cardinalidade e referências independentemente da
        // preferência serializada no asset.
        Assert.That(settings.MapId, Is.EqualTo("classic"));
        Assert.That(settings.TerritorySprites, Has.Length.EqualTo(42));
        Assert.That(settings.TerritorySprites.Select(item => item.TerritoryId),
            Is.EquivalentTo(TerritoryIds));

        foreach (string territoryId in TerritoryIds)
        {
            string expectedPath = MarbleBPath(territoryId);
            WDClassicTerritoryArtPassSettings.TerritorySpriteOverride entry =
                settings.TerritorySprites.Single(
                    item => item.TerritoryId == territoryId);
            Assert.That(entry.DerivedSprite, Is.EqualTo(
                AssetDatabase.LoadAssetAtPath<Sprite>(expectedPath)));

            // O TextureImporter está em Sprite Mode Single. A referência
            // persistente deve apontar para o Sprite principal (21300000),
            // e não para IDs históricos presentes na tabela de recortes.
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                entry.DerivedSprite, out string actualGuid,
                out long actualLocalId), Is.True);
            Assert.That(actualGuid,
                Is.EqualTo(AssetDatabase.AssetPathToGUID(expectedPath)));
            Assert.That(actualLocalId, Is.EqualTo(21300000));
        }
    }

    [Test]
    public void EnabledCatalogResolvesAllClassicTerritories()
    {
        WDClassicTerritoryArtPassSettings settings =
            CreateSettings(true, TerritoryIds);
        try
        {
            foreach (string territoryId in TerritoryIds)
            {
                Sprite original = AssetDatabase.LoadAssetAtPath<Sprite>(
                    OriginalPath(territoryId));
                Sprite derived = AssetDatabase.LoadAssetAtPath<Sprite>(
                    MarbleBPath(territoryId));
                Assert.That(settings.ResolveSprite(
                    "classic", territoryId, original), Is.EqualTo(derived));
                Assert.That(settings.ResolveSprite(
                    "dark-world", territoryId, original), Is.EqualTo(original));
            }

            Sprite other = AssetDatabase.LoadAssetAtPath<Sprite>(
                OriginalPath("Brazil"));
            Assert.That(settings.ResolveSprite("classic", "Unknown", other),
                Is.EqualTo(other));
        }
        finally
        {
            Object.DestroyImmediate(settings);
        }
    }

    [Test]
    public void DisabledCatalogAlwaysReturnsOriginalSprite()
    {
        WDClassicTerritoryArtPassSettings settings =
            CreateSettings(false, TerritoryIds);
        try
        {
            foreach (string territoryId in TerritoryIds)
            {
                Sprite original = AssetDatabase.LoadAssetAtPath<Sprite>(
                    OriginalPath(territoryId));
                Assert.That(settings.ResolveSprite(
                    "classic", territoryId, original), Is.EqualTo(original));
            }
        }
        finally
        {
            Object.DestroyImmediate(settings);
        }
    }

    // ============================================================
    // 3. O GAMEPLAY CONSOME O CATÁLOGO SEM ALTERAR OUTRAS AUTORIDADES
    // ============================================================
    [Test]
    public void TerritoryVisualUsesOnlyTheReversibleSpriteCatalog()
    {
        string productionSource = File.ReadAllText(TerritoryVisualPath);
        Assert.That(productionSource,
            Does.Contain("WDClassicTerritoryArtPassSettings.Load"));
        Assert.That(productionSource,
            Does.Contain("AplicarSpriteExperimentalSeConfigurado"));
        Assert.That(productionSource,
            Does.Contain("spriteTerritorialOriginal"));
    }

    // ============================================================
    // 4. CONTINUIDADE: CADA RGB PROVÉM DA MESMA COORDENADA DO MASTER
    // ============================================================
    [Test]
    public void MarbleBVisiblePixelsMatchGlobalMasterAtCorrectedOrigins()
    {
        PixelData master = LoadPixels(MasterPath);
        Assert.That(master.Width, Is.EqualTo(1920));
        Assert.That(master.Height, Is.EqualTo(1080));

        for (int territoryIndex = 0;
             territoryIndex < TerritoryIds.Length; territoryIndex++)
        {
            string territoryId = TerritoryIds[territoryIndex];
            PixelData derived = LoadPixels(MarbleBPath(territoryId));
            Vector2Int origin = GlobalOrigins[territoryIndex];

            for (int topY = 0; topY < derived.Height; topY++)
            for (int x = 0; x < derived.Width; x++)
            {
                Color32 actual = derived.AtTopLeft(x, topY);
                if (actual.a == 0)
                    continue;

                Color32 expected = master.AtTopLeft(
                    origin.x + x, origin.y + topY);
                Assert.That(actual.r, Is.EqualTo(expected.r),
                    territoryId + " RGB diverge do master em " + x + "," + topY);
                Assert.That(actual.g, Is.EqualTo(expected.g));
                Assert.That(actual.b, Is.EqualTo(expected.b));
            }
        }
    }

    // ============================================================
    // 5. SELEÇÃO, CONQUISTA E COLLIDER USAM O SPRITE ATIVO
    // ============================================================
    [Test]
    public void ActiveSpriteFlowsToSelectionAndConquestWithoutChangingCollider()
    {
        Sprite derived = AssetDatabase.LoadAssetAtPath<Sprite>(
            MarbleBPath("Brazil"));
        var territoryObject = new GameObject("Brazil");
        try
        {
            SpriteRenderer renderer = territoryObject.AddComponent<SpriteRenderer>();
            renderer.sprite = derived;
            PolygonCollider2D collider =
                territoryObject.AddComponent<PolygonCollider2D>();
            Vector2[] path =
            {
                new Vector2(-1f, -1f), new Vector2(1f, -1f),
                new Vector2(1f, 1f), new Vector2(-1f, 1f)
            };
            collider.pathCount = 1;
            collider.SetPath(0, path);
            TerritorioClique territory =
                territoryObject.AddComponent<TerritorioClique>();
            territory.idTerritorio = "Brazil";
            TerritorioVisual visual =
                territoryObject.AddComponent<TerritorioVisual>();

            visual.DestacarSelecao();
            SpriteRenderer selection = territoryObject
                .GetComponentsInChildren<SpriteRenderer>(true)
                .Single(item => item.gameObject.name == "SelectionSprite");
            Assert.That(selection.sprite, Is.EqualTo(derived));

            MethodInfo method = typeof(TerritorioVisual).GetMethod(
                "AnimateConquestGlow",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            IEnumerator animation = (IEnumerator)method.Invoke(
                visual, new object[] { false });
            Assert.That(animation.MoveNext(), Is.True);
            SpriteRenderer conquest = territoryObject
                .GetComponentsInChildren<SpriteRenderer>(true)
                .Single(item => item.gameObject.name == "ConquestGlow");
            Assert.That(conquest.sprite, Is.EqualTo(derived));
            Assert.That(collider.pathCount, Is.EqualTo(1));
            Assert.That(collider.GetPath(0), Is.EqualTo(path));
        }
        finally
        {
            Object.DestroyImmediate(territoryObject);
        }
    }

    private static WDClassicTerritoryArtPassSettings CreateSettings(
        bool enabled, IReadOnlyList<string> territoryIds)
    {
        var settings = ScriptableObject.CreateInstance<
            WDClassicTerritoryArtPassSettings>();
        var serialized = new SerializedObject(settings);
        serialized.FindProperty("prototypeEnabled").boolValue = enabled;
        serialized.FindProperty("mapId").stringValue = "classic";
        SerializedProperty entries = serialized.FindProperty("territorySprites");
        entries.arraySize = territoryIds.Count;
        for (int index = 0; index < territoryIds.Count; index++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("territoryId").stringValue =
                territoryIds[index];
            entry.FindPropertyRelative("derivedSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    MarbleBPath(territoryIds[index]));
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return settings;
    }

    private static string RejectedLot1Path(string territoryId)
    {
        return RejectedLot1Root + territoryId + "_ArtPass2_Lot1.png";
    }

    private static string StylizedLot1Path(string territoryId)
    {
        return StylizedLot1Root + territoryId + "_ArtPass2_Stylized.png";
    }

    private static string MarbleBPath(string territoryId)
    {
        return MarbleBRoot + territoryId + "_ArtPass2_MarbleB.png";
    }

    private static string OriginalPath(string territoryId)
    {
        TerritoryRegistration registration = Registrations.Single(
            item => item.TerritoryId == territoryId);
        return RuntimeRoot + registration.SourceAssetId + ".png";
    }

    private static void AssertDerivedGeometryAndAlpha(
        string territoryId, string derivedPath)
    {
        string originalPath = OriginalPath(territoryId);
        Sprite original = AssetDatabase.LoadAssetAtPath<Sprite>(originalPath);
        Sprite derived = AssetDatabase.LoadAssetAtPath<Sprite>(derivedPath);

        Assert.That(original, Is.Not.Null, originalPath);
        Assert.That(derived, Is.Not.Null, derivedPath);
        Assert.That(derived.texture.width, Is.EqualTo(original.texture.width));
        Assert.That(derived.texture.height, Is.EqualTo(original.texture.height));
        Assert.That(derived.rect, Is.EqualTo(original.rect));
        Assert.That(derived.pivot, Is.EqualTo(original.pivot));
        Assert.That(derived.pixelsPerUnit, Is.EqualTo(original.pixelsPerUnit));

        PixelData originalPixels = LoadPixels(originalPath);
        PixelData derivedPixels = LoadPixels(derivedPath);
        Assert.That(derivedPixels.Pixels.Select(pixel => pixel.a),
            Is.EqualTo(originalPixels.Pixels.Select(pixel => pixel.a)));
    }

    private static PixelData LoadPixels(string assetPath)
    {
        byte[] bytes = File.ReadAllBytes(assetPath);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            Assert.That(ImageConversion.LoadImage(texture, bytes, false), Is.True);
            return new PixelData(
                texture.width, texture.height, texture.GetPixels32());
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    private readonly struct TerritoryRegistration
    {
        public TerritoryRegistration(string territoryId, string sourceAssetId,
            int originX, int originY)
        {
            TerritoryId = territoryId;
            SourceAssetId = sourceAssetId;
            Origin = new Vector2Int(originX, originY);
        }

        public string TerritoryId { get; }
        public string SourceAssetId { get; }
        public Vector2Int Origin { get; }
    }

    private readonly struct PixelData
    {
        public PixelData(int width, int height, Color32[] pixels)
        {
            Width = width;
            Height = height;
            Pixels = pixels;
        }

        public int Width { get; }
        public int Height { get; }
        public Color32[] Pixels { get; }

        public Color32 AtTopLeft(int x, int topY)
        {
            return Pixels[(Height - 1 - topY) * Width + x];
        }
    }
}
