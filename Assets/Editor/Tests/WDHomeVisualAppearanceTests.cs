using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class WDHomeVisualAppearanceTests
{
    [Test]
    public void VariantsHaveDistinctPremiumSurfacesAndCompactPlayRadius()
    {
        WDHomeVisualPalette chrome = WDHomeVisualPalette.For(WDHomeVisualVariant.ChromePremium);
        WDHomeVisualPalette glass = WDHomeVisualPalette.For(WDHomeVisualVariant.SapphireGlass);
        Assert.That(chrome.Surface, Is.Not.EqualTo(glass.Surface));
        Assert.That(chrome.Border.a, Is.GreaterThan(glass.Border.a));
        Assert.That(chrome.PlayOuterRadius, Is.LessThan(67));
        Assert.That(glass.PlayOuterRadius, Is.LessThan(67));
    }

    [Test]
    public void ApplyingVariantsPreservesGeometryAndBackground()
    {
        var root = new GameObject("HomeContent", typeof(RectTransform));
        var background = new GameObject("Background", typeof(RectTransform), typeof(RawImage));
        background.transform.SetParent(root.transform, false);
        var backgroundTexture = new Texture2D(2, 2);
        background.GetComponent<RawImage>().texture = backgroundTexture;
        background.GetComponent<RawImage>().color = Color.magenta;
        var medallion = new GameObject("MedallionGold", typeof(RectTransform), typeof(RawImage));
        medallion.transform.SetParent(root.transform, false);
        var medallionTexture = new Texture2D(2, 2);
        medallion.GetComponent<RawImage>().texture = medallionTexture;
        medallion.GetComponent<RawImage>().color = new Color(0.9f, 0.7f, 0.2f, 1f);
        var emblem = new GameObject("Emblem", typeof(RectTransform), typeof(RawImage));
        emblem.transform.SetParent(medallion.transform, false);
        var emblemTexture = new Texture2D(2, 2);
        emblem.GetComponent<RawImage>().texture = emblemTexture;
        emblem.GetComponent<RawImage>().color = Color.white;
        var play = new GameObject("MainPlayButton", typeof(RectTransform), typeof(Image));
        play.transform.SetParent(root.transform, false);
        RectTransform playRect = (RectTransform)play.transform;
        playRect.sizeDelta = new Vector2(520f, 135f);
        playRect.anchoredPosition = new Vector2(12f, -18f);
        var controller = root.AddComponent<WDHomeVisualAppearanceController>();
        controller.Initialize(root.transform);
        Vector2 originalPlayPosition = playRect.anchoredPosition;
        controller.Apply(WDHomeVisualVariant.SapphireGlass, false);
        Assert.That(playRect.sizeDelta, Is.EqualTo(new Vector2(520f, 135f)));
        Assert.That(playRect.anchoredPosition, Is.EqualTo(originalPlayPosition));
        Assert.That(background.GetComponent<RawImage>().color, Is.EqualTo(Color.magenta));
        Assert.That(background.GetComponent<RawImage>().texture, Is.SameAs(backgroundTexture));
        Assert.That(medallion.GetComponent<RawImage>().color,
            Is.EqualTo(new Color(0.9f, 0.7f, 0.2f, 1f)));
        Assert.That(medallion.GetComponent<RawImage>().texture, Is.SameAs(medallionTexture));
        Assert.That(emblem.GetComponent<RawImage>().color, Is.EqualTo(Color.white));
        Assert.That(emblem.GetComponent<RawImage>().texture, Is.SameAs(emblemTexture));
        Object.DestroyImmediate(backgroundTexture);
        Object.DestroyImmediate(medallionTexture);
        Object.DestroyImmediate(emblemTexture);
        Object.DestroyImmediate(root);
    }
}
