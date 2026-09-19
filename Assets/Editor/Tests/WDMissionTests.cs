using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class WDMissionTests
{
    private static WDMissionDefinition Definition(bool secret = false, bool repeatable = false,
        int target = 1, WDMissionRewardDefinition reward = null) =>
        new("test", "TÍTULO INTERNO", "DESCRIÇÃO INTERNA", "CONDIÇÃO INTERNA",
            WDMissionCategory.Prototype, secret, repeatable, target, reward);

    private static WDMissionRepository Repository(WDMissionDefinition definition,
        WDMemoryMissionRewardProcessor rewards = null) =>
        new(new[] { definition }, Array.Empty<WDMissionProgress>(), rewards);

    // ============================================================
    // 01. CATÁLOGO, DESCOBERTA E APRESENTAÇÃO SEGURA
    // ============================================================

    [Test]
    public void PrototypeCatalogContainsThirtyNinePermanentMissions()
    {
        var repository = new WDMissionRepository();
        Assert.That(repository.TotalCount, Is.EqualTo(39));
        Assert.That(repository.Catalog.All(item => item.IsPermanent), Is.True);
    }

    [Test]
    public void PrototypeDiscoveryProgressComesFromRepository()
    {
        var repository = new WDMissionRepository();
        Assert.That(repository.DiscoveredCount, Is.EqualTo(7));
    }

    [Test]
    public void NormalMissionIsKnownFromTheBeginning()
    {
        var repository = Repository(Definition());
        Assert.That(repository.DiscoveredCount, Is.EqualTo(1));
    }

    [Test]
    public void UndiscoveredSecretDoesNotCountAsDiscovered()
    {
        var repository = Repository(Definition(secret: true));
        Assert.That(repository.DiscoveredCount, Is.Zero);
    }

    [Test]
    public void UndiscoveredSecretPresentationExposesNoSensitiveDefinitionData()
    {
        WDMissionPresentation presentation = Repository(Definition(secret: true)).Present("test");
        Assert.That(presentation.Title, Is.EqualTo("???"));
        Assert.That(presentation.Description, Is.EqualTo("MISSÃO SECRETA"));
        Assert.That(presentation.Condition, Is.EqualTo("NÃO DESCOBERTA"));
        Assert.That(presentation.Current, Is.Null);
        Assert.That(presentation.Target, Is.Null);
        Assert.That(presentation.Reward, Is.Empty);
        Assert.That(presentation.Category, Is.Empty);
    }

    [Test]
    public void SecretBecomesDiscoveredWhenItsCompletionIsRegistered()
    {
        var repository = Repository(Definition(secret: true));
        repository.AddProgress("test");
        Assert.That(repository.DiscoveredCount, Is.EqualTo(1));
        Assert.That(repository.Present("test").Title, Is.EqualTo("TÍTULO INTERNO"));
    }

    // ============================================================
    // 02. CONCLUSÕES ÚNICAS, REPETÍVEIS E RECOMPENSAS
    // ============================================================

    [Test]
    public void UniqueMissionCompletesOnce()
    {
        var repository = Repository(Definition(target: 2));
        Assert.That(repository.AddProgress("test", 2), Is.EqualTo(1));
        Assert.That(repository.Progress("test").CompletionCount, Is.EqualTo(1));
    }

    [Test]
    public void UniqueMissionRejectsFurtherCompletions()
    {
        var repository = Repository(Definition());
        repository.AddProgress("test");
        Assert.That(repository.AddProgress("test", 10), Is.Zero);
        Assert.That(repository.Progress("test").CompletionCount, Is.EqualTo(1));
    }

    [Test]
    public void UniqueMissionRewardIsProcessedOnlyOnce()
    {
        var sink = new WDMemoryMissionRewardProcessor();
        var repository = Repository(Definition(reward:
            new WDMissionRewardDefinition(WDMissionRewardType.Coins)), sink);
        repository.AddProgress("test"); repository.AddProgress("test");
        Assert.That(sink.ProcessedCount, Is.EqualTo(1));
        Assert.That(repository.Progress("test").ProcessedRewardCount, Is.EqualTo(1));
    }

    [Test]
    public void RepeatableMissionIncrementsCompletionCount()
    {
        var repository = Repository(Definition(repeatable: true, target: 2));
        repository.AddProgress("test", 2);
        Assert.That(repository.Progress("test").CompletionCount, Is.EqualTo(1));
    }

    [Test]
    public void RepeatableMissionCanCompleteMoreThanOneCycle()
    {
        var repository = Repository(Definition(repeatable: true, target: 2));
        Assert.That(repository.AddProgress("test", 5), Is.EqualTo(2));
        Assert.That(repository.Progress("test").CompletionCount, Is.EqualTo(2));
        Assert.That(repository.Progress("test").Current, Is.EqualTo(1));
    }

    [Test]
    public void RepeatableRewardRunsOncePerValidCompletion()
    {
        var sink = new WDMemoryMissionRewardProcessor();
        var repository = Repository(Definition(repeatable: true, target: 2,
            reward: new WDMissionRewardDefinition(WDMissionRewardType.WeaponTokens)), sink);
        repository.AddProgress("test", 4);
        Assert.That(sink.ProcessedCount, Is.EqualTo(2));
        Assert.That(repository.Progress("test").ProcessedRewardCount, Is.EqualTo(2));
    }

    [Test]
    public void RequeryingPresentationDoesNotProcessRewardAgain()
    {
        var sink = new WDMemoryMissionRewardProcessor();
        var repository = Repository(Definition(reward:
            new WDMissionRewardDefinition(WDMissionRewardType.Emeralds)), sink);
        repository.AddProgress("test");
        repository.Present("test"); repository.PresentAll(); repository.Present("test");
        Assert.That(sink.ProcessedCount, Is.EqualTo(1));
    }

    [Test]
    public void CompletedMissionRemainsInCollection()
    {
        var repository = Repository(Definition());
        repository.AddProgress("test");
        WDMissionPresentation completed = repository.PresentAll()
            .SingleOrDefault(item => item.MissionId == "test");
        Assert.That(completed, Is.Not.Null);
        Assert.That(completed.Status, Is.EqualTo(WDMissionStatus.Completed));
        Assert.That(completed.CompletionCount, Is.EqualTo(1));
    }

    [Test]
    public void PrototypeRewardsDeclareTypesWithoutInventedAmounts()
    {
        var rewards = new WDMissionRepository().Catalog.Where(item => item.Reward != null)
            .Select(item => item.Reward).ToArray();
        Assert.That(rewards, Is.Not.Empty);
        Assert.That(rewards.All(item => !item.Quantity.HasValue), Is.True);
    }

    // ============================================================
    // 03. PAINEL, HOME E NAVEGAÇÃO CONTEXTUAL
    // ============================================================

    [Test]
    public void MissionsPanelUsesTheSharedRepositoryAuthority()
    {
        GameObject root = BuildPanel(out WDHomeMissionsPanel panel,
            out WDMissionRepository repository, out _);
        try { Assert.That(panel.Repository, Is.SameAs(repository)); }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void HomeMissionCardCallbackOpensExactlyOneMissionsDestination()
    {
        GameObject root = BuildPanel(out WDHomeMissionsPanel panel, out _, out WDNavigationStack navigation);
        try
        {
            RectTransform cardRect = WDHomeUIFactory.Rect("MissionsCard", root.transform);
            cardRect.gameObject.AddComponent<WDHomeCard>().Build(Theme(), "M", "MISSÕES",
                "COLEÇÃO PERMANENTE", panel.Open);
            FindButton(root, "MISSÕES").onClick.Invoke();
            Assert.That(panel.IsOpen, Is.True);
            Assert.That(navigation.CurrentId, Is.EqualTo("missions"));
            Assert.That(root.GetComponentsInChildren<WDHomeMissionsPanel>(true), Has.Length.EqualTo(1));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void MissionsBackReturnsToActualHomeOrigin()
    {
        GameObject root = BuildPanel(out WDHomeMissionsPanel panel, out _, out WDNavigationStack navigation);
        try
        {
            panel.Open();
            FindButton(root, "<  VOLTAR").onClick.Invoke();
            Assert.That(navigation.CurrentId, Is.EqualTo("home"));
            Assert.That(panel.IsOpen, Is.False);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void MissionDetailDoesNotCreateGlobalNavigationLevel()
    {
        GameObject root = BuildPanel(out WDHomeMissionsPanel panel, out _, out WDNavigationStack navigation);
        try
        {
            panel.Open(); int before = navigation.Count;
            FindButton(root, "PRIMEIRO TRIUNFO").onClick.Invoke();
            Assert.That(panel.IsDetailOpen, Is.True);
            Assert.That(navigation.Count, Is.EqualTo(before));
            FindButton(root, "VOLTAR À COLEÇÃO").onClick.Invoke();
            Assert.That(panel.IsDetailOpen, Is.False);
            Assert.That(navigation.Count, Is.EqualTo(before));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void SecretMissionDetailKeepsItsDefinitionHidden()
    {
        GameObject root = BuildPanel(out WDHomeMissionsPanel panel, out _, out _);
        try
        {
            panel.Open(); FindButton(root, "???").onClick.Invoke();
            string detailText = string.Join("|", root.transform.Find("Panel/MissionDetail")
                .GetComponentsInChildren<TextMeshProUGUI>(true).Select(item => item.text));
            Assert.That(detailText, Does.Contain("MISSÃO SECRETA"));
            Assert.That(detailText, Does.Not.Contain("Placeholder secreto interno"));
            Assert.That(detailText, Does.Not.Contain("Condição interna a definir"));
            Assert.That(detailText, Does.Not.Contain("RECOMPENSA"));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void MissionProgressPortDoesNotCreateGameplayOrMatchAuthorities()
    {
        var repository = Repository(Definition(target: 3));
        WDMatchSetup existingSetup = WDMatchSetupContext.Current;
        repository.AddProgress("test", 2);
        Assert.That(repository.Progress("test").Current, Is.EqualTo(2));
        Assert.That(WDMatchSetupContext.Current, Is.SameAs(existingSetup));
    }

    [Test]
    public void UniversalUnavailableStateRendersInlineWithoutChangingNavigation()
    {
        GameObject root = BuildPanel(out WDHomeMissionsPanel panel,
            out WDMissionRepository repository, out WDNavigationStack navigation);
        try
        {
            repository.State = WDMissionCollectionState.Unavailable;
            panel.Open();
            Assert.That(navigation.CurrentId, Is.EqualTo("missions"));
            Assert.That(root.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Any(item => item.text.Contains("INDISPONÍVEIS / OFFLINE")), Is.True);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static GameObject BuildPanel(out WDHomeMissionsPanel panel,
        out WDMissionRepository repository, out WDNavigationStack navigation)
    {
        var root = new GameObject("MissionPanelTest", typeof(RectTransform));
        RectTransform layer = WDHomeUIFactory.Rect("Panel", root.transform);
        panel = layer.gameObject.AddComponent<WDHomeMissionsPanel>();
        repository = new WDMissionRepository();
        navigation = new WDNavigationStack();
        WDHomeMissionsPanel captured = panel;
        navigation.SetRoot(new WDNavigationContext("home", _ => captured.HideFromNavigation()));
        panel.Build(Theme(), repository, navigation);
        return root;
    }

    private static WarDominionUITheme Theme() =>
        Resources.Load<WarDominionUITheme>("UI/WarDominionUITheme");

    private static Button FindButton(GameObject root, string label) =>
        root.GetComponentsInChildren<Button>(true).First(button =>
            button.GetComponentsInChildren<TextMeshProUGUI>(true).Any(text => text.text == label));
}
