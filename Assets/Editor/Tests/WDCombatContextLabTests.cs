using NUnit.Framework;
using System.IO;
using UnityEngine;

public sealed class WDCombatContextLabTests
{
    // ============================================================
    // 01. CONTRATO DO LABORATÓRIO — cenários mínimos e caminho isolado
    // ============================================================
    [Test]
    public void ContextLabDefinesAllRequiredScenarios()
    {
        Assert.That(WDCombatContextLabController.ScenarioLabels.Count, Is.EqualTo(8));
        Assert.That(WDCombatContextLabController.ScenarioLabels,
            Does.Contain("ATAQUE CONQUISTADO"));
        Assert.That(WDCombatContextLabController.ScenarioLabels,
            Does.Contain("TRANSFERÊNCIA DE PROPRIEDADE"));
    }

    [Test]
    public void ContextLabUsesDedicatedDevelopmentScene()
    {
        Assert.That(WDCombatContextLabAuthoring.ScenePath,
            Is.EqualTo("Assets/Scenes/Development/WDCombatContextLab.unity"));
    }

    // ============================================================
    // 02. PRESET E DROPDOWN — persistência local e hit-area coincidente
    // ============================================================
    [Test]
    public void CapturedVisualPresetPersistsApprovedPlayModeValues()
    {
        const string path = "UserSettings/WDCombatContextLabVisualPreset.json";
        Assert.That(File.Exists(path), Is.True);
        string json = File.ReadAllText(path);
        Assert.That(json, Does.Contain("\"confirmedBodyThickness\": 12.7"));
        Assert.That(json, Does.Contain("\"confirmedCurveHeight\": 19.0"));
        Assert.That(json, Does.Contain("\"confirmedCurvePosition\": 0.399"));
        Assert.That(json, Does.Contain("\"confirmedCardBackground\": \"060B11F5\""));
    }

    [TestCase(16f, 52f)]
    [TestCase(16f, 16f)]
    public void DropdownLabelsStretchInsideTheirClickableRows(
        float left, float right)
    {
        var go = new GameObject("DropdownLabel", typeof(RectTransform));
        try
        {
            RectTransform rect = go.GetComponent<RectTransform>();
            WDCombatContextLabAuthoring.StretchDropdownLabel(rect, left, right);
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(rect.offsetMin, Is.EqualTo(new Vector2(left, 0f)));
            Assert.That(rect.offsetMax, Is.EqualTo(new Vector2(-right, 0f)));
            Assert.That(rect.anchoredPosition, Is.EqualTo(Vector2.zero));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
