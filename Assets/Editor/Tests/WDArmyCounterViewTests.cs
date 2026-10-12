using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class WDArmyCounterViewTests
{
    [TestCase(1)]
    [TestCase(9)]
    [TestCase(10)]
    [TestCase(99)]
    [TestCase(100)]
    [TestCase(999)]
    [TestCase(2147483647)]
    public void TroopValueUpdatesTheSameTmpWithoutOverflow(int value)
    {
        GameObject root = new("PremiumCounterValueTest");
        try
        {
            WDArmyCounterView view = root.AddComponent<WDArmyCounterView>();
            view.ExibirQuantidade(1);
            TextMeshPro originalText = view.TroopText;
            Transform originalRoot = view.VisualRoot;

            view.ExibirQuantidade(value);

            Assert.That(view.QuantidadeExibida, Is.EqualTo(value));
            Assert.That(view.TroopText.text, Is.EqualTo(value.ToString()));
            Assert.That(view.TroopText, Is.SameAs(originalText));
            Assert.That(view.VisualRoot, Is.SameAs(originalRoot));
            Assert.That(view.TroopText.overflowMode, Is.EqualTo(TextOverflowModes.Truncate));
            Assert.That(view.TroopText.enableAutoSizing, Is.True);
            Assert.That(view.TroopText.fontSizeMin, Is.GreaterThanOrEqualTo(1.1f));
            Assert.That(view.TroopText.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(view.TroopText.rectTransform.sizeDelta.x,
                Is.EqualTo(WDArmyCounterView.NumberWidth));
            Assert.That(WDArmyCounterView.NumberWidth, Is.LessThan(WDArmyCounterView.FrameWidth));
            Assert.That(view.TroopText.GetRenderedValues(false).x,
                Is.LessThanOrEqualTo(view.TroopText.rectTransform.sizeDelta.x));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void OwnerAndNeutralStatesUpdateOnlyTheRingPalette()
    {
        GameObject root = new("PremiumCounterColorTest");
        try
        {
            WDArmyCounterView view = root.AddComponent<WDArmyCounterView>();
            Color owner = new(0.17f, 0.64f, 0.91f, 0.4f);
            Color neutral = PaletaJogadores.ObterCorNeutraAtiva();
            view.Exibir(9, owner, WDArmyCounterView.VisualState.Normal);
            Assert.That(view.CorDoAro, Is.EqualTo(new Color(owner.r, owner.g, owner.b, 1f)));

            view.Exibir(1, neutral, WDArmyCounterView.VisualState.Neutral);
            neutral.a = 1f;
            Assert.That(view.CorDoAro, Is.EqualTo(neutral));
            Assert.That(view.EstadoVisual, Is.EqualTo(WDArmyCounterView.VisualState.Neutral));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void EmptyKeepsLastOwnerIdentityAndUsesTheSameHierarchy()
    {
        GameObject root = new("PremiumCounterEmptyTest");
        try
        {
            WDArmyCounterView view = root.AddComponent<WDArmyCounterView>();
            Color owner = new(0.82f, 0.18f, 0.12f, 1f);
            view.Exibir(4, owner, WDArmyCounterView.VisualState.Normal);
            Transform hierarchy = view.VisualRoot;
            view.Exibir(0, Color.clear, WDArmyCounterView.VisualState.Empty);

            Assert.That(view.CorDoAro, Is.EqualTo(owner));
            Assert.That(view.QuantidadeExibida, Is.Zero);
            Assert.That(view.VisualRoot, Is.SameAs(hierarchy));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void SelectionReinforcementAndDamageReuseTheSameComponent()
    {
        GameObject root = new("PremiumCounterStatesTest");
        try
        {
            WDArmyCounterView view = root.AddComponent<WDArmyCounterView>();
            view.ExibirQuantidade(15);
            Assert.That(view.TroopText.text, Is.EqualTo("15"));
            Transform hierarchy = view.VisualRoot;
            view.MostrarSelecionado(true);
            Assert.That(view.EstadoVisual, Is.EqualTo(WDArmyCounterView.VisualState.Selected));
            view.PulsoReforco();
            Assert.That(view.EstadoVisual, Is.EqualTo(WDArmyCounterView.VisualState.Reinforcement));
            view.PulsoDano();
            Assert.That(view.EstadoVisual, Is.EqualTo(WDArmyCounterView.VisualState.Damage));
            Assert.That(view.VisualRoot, Is.SameAs(hierarchy));
            Assert.That(root.GetComponents<WDArmyCounterView>(), Has.Length.EqualTo(1));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void ManualSpriteCompositionHasOneBodyAndOnePrincipalTmp()
    {
        GameObject root = new("ManualCounterStructureTest");
        try
        {
            WDArmyCounterView view = root.AddComponent<WDArmyCounterView>();
            view.ExibirQuantidade(99);
            Transform visual = view.VisualRoot;
            Assert.That(visual.GetComponentsInChildren<SpriteRenderer>(), Has.Length.EqualTo(1));
            Assert.That(visual.GetComponentsInChildren<TextMeshPro>(), Has.Length.EqualTo(1));
            Assert.That(visual.GetComponentsInChildren<Canvas>(), Is.Empty);
            Assert.That(visual.GetComponentsInChildren<Image>(), Is.Empty);
            Assert.That(view.BodyRenderer.sprite, Is.SameAs(
                AssetDatabase.LoadAllAssetsAtPath("Assets/UI/Match/Counters/WD_ArmyCounter_NEW.png")
                    .OfType<Sprite>().Single()));
            Assert.That(view.TroopText.sortingOrder, Is.GreaterThan(view.BodyRenderer.sortingOrder));
            Assert.That(view.TroopText.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(view.TroopText.color, Is.EqualTo(Color.white));
            Assert.That(view.TroopText.fontStyle.HasFlag(FontStyles.Bold), Is.True);
            Assert.That(view.TroopText.alignment, Is.EqualTo(TextAlignmentOptions.Center));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void ManualBodyAndTextAreReusedWithoutOldUiLayers()
    {
        GameObject root = new("ManualCounterReuseTest");
        try
        {
            WDArmyCounterView view = root.AddComponent<WDArmyCounterView>();
            view.ExibirQuantidade(15);
            SpriteRenderer body = view.BodyRenderer;
            TextMeshPro text = view.TroopText;
            view.ExibirQuantidade(999);
            Assert.That(view.BodyRenderer, Is.SameAs(body));
            Assert.That(view.TroopText, Is.SameAs(text));
            Assert.That(root.transform.Cast<Transform>()
                .Count(child => child.name == "ArmyCounterSpriteRoot"), Is.EqualTo(1));
            foreach (string name in new[] { "OuterBorder", "SkinFrame", "InnerBackground", "PlayerAccent" })
                Assert.That(view.VisualRoot.Find(name), Is.Null);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void LegacyCounterUsesOnePremiumViewAndCanonicalTroopValue()
    {
        GameObject territoryObject = new("CanonicalTerritoryCounterTest");
        try
        {
            territoryObject.AddComponent<TerritorioTropas>();
            territoryObject.AddComponent<TerritorioFronteiras>();
            PolygonCollider2D polygon = territoryObject.AddComponent<PolygonCollider2D>();
            polygon.SetPath(0, new[]
            {
                new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(0f, 1f)
            });
            TerritorioClique territory = territoryObject.AddComponent<TerritorioClique>();
            MethodInfo start = typeof(TerritorioClique).GetMethod(
                "Start", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(start, Is.Not.Null);
            start.Invoke(territory, null);

            territory.DefinirDono(TerritorioClique.Dono.Jogador1);
            territory.DefinirTropas(99);

            ContadorTropas counter = territoryObject.GetComponentInChildren<ContadorTropas>();
            Assert.That(counter, Is.Not.Null);
            GameObject counterObject = counter.gameObject;

            WDArmyCounterView[] views = counterObject.GetComponents<WDArmyCounterView>();
            Assert.That(views, Has.Length.EqualTo(1));
            Assert.That(views[0].QuantidadeExibida, Is.EqualTo(territory.Tropas));
            Assert.That(views[0].TroopText.text, Is.EqualTo("99"));

            WDArmyCounterView originalView = views[0];
            Transform originalHierarchy = originalView.VisualRoot;
            territory.DefinirTropas(100);
            Assert.That(counterObject.GetComponents<WDArmyCounterView>(), Has.Length.EqualTo(1));
            Assert.That(counterObject.GetComponent<WDArmyCounterView>(), Is.SameAs(originalView));
            Assert.That(originalView.VisualRoot, Is.SameAs(originalHierarchy));
            Assert.That(originalView.TroopText.text, Is.EqualTo("100"));
        }
        finally { Object.DestroyImmediate(territoryObject); }
    }
}
