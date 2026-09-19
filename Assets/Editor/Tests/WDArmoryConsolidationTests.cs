using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class WDArmoryConsolidationTests
{
    private WDArmoryCatalogConfig catalog;
    private WDArmoryProfile profile;
    private WDArmoryService service;

    [SetUp]
    public void SetUp()
    {
        catalog = ScriptableObject.CreateInstance<WDArmoryCatalogConfig>();
        catalog.Configure(new[]
        {
            new WDWeaponCategoryDefinition("support", "Apoio", true),
            new WDWeaponCategoryDefinition("tactical", "Tática", false)
        }, Enumerable.Range(0, 12).Select(i => new WDWeaponDefinition($"w{i}", $"Arma {i}",
            i < 4 ? "support" : "tactical", Enumerable.Range(2, i == 0 ? 3 : 5)
                .Select(level => new WDWeaponLevelDefinition(level, 10, 20)))));
        profile = new WDArmoryProfile(false, 100, 0);
        foreach (WDWeaponDefinition weapon in catalog.Weapons)
            profile.AddProgress(new WDWeaponProgress(weapon.Id, true, 1, 20));
        service = new WDArmoryService(catalog, profile);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(catalog);

    // ============================================================
    // 01. CATÁLOGO E EVOLUÇÃO CANÔNICOS
    // ============================================================

    [Test]
    public void AllAndCategoryFilterDeriveFromCanonicalCatalog()
    {
        Assert.That(service.Catalog.Weapons.Count, Is.EqualTo(12));
        Assert.That(service.Catalog.Weapons.Count(w => w.CategoryId == "support"), Is.EqualTo(4));
        Assert.That(service.Catalog.Categories.Select(c => c.Id), Does.Contain("tactical"));
    }

    [Test]
    public void EachWeaponUsesItsOwnMaximumRatherThanSix()
    {
        Assert.That(catalog.FindWeapon("w0").MaximumLevel, Is.EqualTo(4));
        Assert.That(catalog.FindWeapon("w1").MaximumLevel, Is.EqualTo(6));
        profile.GetProgress("w0").SetProgress(true, 4, 100);
        Assert.That(service.Upgrade("w0"), Is.EqualTo(WDArmoryEditResult.AlreadyMaxLevel));
    }

    [Test]
    public void UpgradeConsumesOnlySelectedWeaponTokensAndCanonicalCoinsOnce()
    {
        Assert.That(service.Upgrade("w0"), Is.EqualTo(WDArmoryEditResult.Success));
        Assert.That(profile.GetProgress("w0").Level, Is.EqualTo(2));
        Assert.That(profile.GetProgress("w0").Tokens, Is.EqualTo(10));
        Assert.That(profile.GetProgress("w1").Tokens, Is.EqualTo(20));
        Assert.That(profile.Coins, Is.EqualTo(80));
        Assert.That(profile.GetProgress("w0").Level, Is.EqualTo(2));
        Assert.That(profile.Coins, Is.EqualTo(80));
    }

    [Test]
    public void InsufficientSelectedTokensCannotBorrowAnotherWeaponsTokens()
    {
        profile.GetProgress("w0").SetProgress(true, 1, 0);
        Assert.That(service.Upgrade("w0"), Is.EqualTo(WDArmoryEditResult.InsufficientTokens));
        Assert.That(profile.GetProgress("w1").Tokens, Is.EqualTo(20));
    }

    [Test]
    public void InsufficientCoinsDoNotConsumeWeaponTokens()
    {
        profile.SetCurrencies(0, 0);
        Assert.That(service.Upgrade("w0"), Is.EqualTo(WDArmoryEditResult.InsufficientCoins));
        Assert.That(profile.GetProgress("w0").Tokens, Is.EqualTo(20));
    }

    // ============================================================
    // 02. COMPOSIÇÃO E RESTAURAÇÃO SEGURA DO DECK
    // ============================================================

    [Test]
    public void EightGeneralAndTwoSupportSlotsAllowTenDistinctWeapons()
    {
        Assert.That(WDSavedWeaponDeck.MainSlotCount, Is.EqualTo(8));
        Assert.That(WDSavedWeaponDeck.SupportSlotCount, Is.EqualTo(2));
        for (int i = 0; i < 8; i++)
            Assert.That(service.AssignMain(0, i, $"w{i + 2}"), Is.EqualTo(WDArmoryEditResult.Success));
        Assert.That(service.AssignSupport(0, 0, "w0"), Is.EqualTo(WDArmoryEditResult.Success));
        Assert.That(service.AssignSupport(0, 1, "w1"), Is.EqualTo(WDArmoryEditResult.Success));
        Assert.That(profile.GetDeck(0).MainWeaponIds.Count(id => !string.IsNullOrEmpty(id)) +
            profile.GetDeck(0).SupportWeaponIds.Count(id => !string.IsNullOrEmpty(id)), Is.EqualTo(10));
    }

    [Test]
    public void ExtraSlotsRejectNonSupportEvenThoughGeneralSlotsAcceptIt()
    {
        Assert.That(service.AssignMain(0, 0, "w4"), Is.EqualTo(WDArmoryEditResult.Success));
        Assert.That(service.AssignSupport(0, 0, "w5"), Is.EqualTo(WDArmoryEditResult.NotSupportEligible));
        Assert.That(service.AssignSupport(0, 2, "w0"), Is.EqualTo(WDArmoryEditResult.InvalidSlot));
    }

    [Test]
    public void RestoredDeckCannotBypassSupportOwnershipAndDuplicateRules()
    {
        WDSavedWeaponDeck deck = profile.GetDeck(0);
        deck.SetMain(0, "w0"); deck.SetMain(1, "w0");
        deck.SetSupport(0, "w4"); deck.SetSupport(1, "w1");
        profile.GetProgress("w1").SetProgress(false, 1, 20);
        new WDArmoryService(catalog, profile);
        Assert.That(deck.GetMain(0), Is.EqualTo("w0"));
        Assert.That(deck.GetMain(1), Is.Empty);
        Assert.That(deck.GetSupport(0), Is.Empty);
        Assert.That(deck.GetSupport(1), Is.Empty);
    }

    [Test]
    public void ActiveDeckSurvivesNormalizationAndVipLockReturnsToFreeDeck()
    {
        profile.SetVip(true);
        Assert.That(profile.SetActiveDeck(4), Is.True);
        profile.Normalize();
        Assert.That(profile.ActiveDeckIndex, Is.EqualTo(4));
        profile.SetVip(false); profile.Normalize();
        Assert.That(profile.ActiveDeckIndex, Is.EqualTo(0));
        Assert.That(profile.SetActiveDeck(4), Is.False);
    }

    [Test]
    public void OnlyFirstTwoDecksAreFreeAndThreeThroughFiveRequireVip()
    {
        Assert.That(Enumerable.Range(0, 5).Select(profile.IsDeckUnlocked),
            Is.EqualTo(new[] { true, true, false, false, false }));
    }

    // ============================================================
    // 03. ABAS DA APRESENTAÇÃO E AUTORIDADE COMPARTILHADA
    // ============================================================

    [Test]
    public void WeaponsAndDecksTabsShareOnePanelAndPreserveWeaponSelection()
    {
        GameObject root = BuildPanel(out WDHomeArmoryPanel panel);
        try
        {
            panel.Open();
            root.transform.Find("ArmoryPanel/WeaponsView/WeaponCatalogue/Viewport/Content/Weapon_w1")
                .GetComponent<Button>().onClick.Invoke();
            root.transform.Find("ArmoryPanel/DecksTab").GetComponentInChildren<Button>().onClick.Invoke();
            Assert.That(panel.ShowingDecks, Is.True);
            root.transform.Find("ArmoryPanel/WeaponsTab").GetComponentInChildren<Button>().onClick.Invoke();
            Assert.That(panel.ShowingDecks, Is.False);
            Assert.That(panel.SelectedWeaponId, Is.EqualTo("w1"));
            Assert.That(root.GetComponentsInChildren<WDHomeArmoryPanel>(true).Length, Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void CategorySelectionFiltersCanonicalWeaponCards()
    {
        GameObject root = BuildPanel(out WDHomeArmoryPanel panel);
        try
        {
            panel.Open();
            root.transform.Find("ArmoryPanel/WeaponsView/WeaponFilters/support")
                .GetComponentInChildren<Button>().onClick.Invoke();
            Assert.That(panel.CategoryFilter, Is.EqualTo("support"));
            Assert.That(root.transform.Find("ArmoryPanel/WeaponsView/WeaponCatalogue/Viewport/Content")
                .childCount, Is.EqualTo(4));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void HomeArmoryUsesOneNavigationLevelAndTabsDoNotPush()
    {
        var navigation = new WDNavigationStack();
        GameObject root = BuildPanel(out WDHomeArmoryPanel panel, navigation);
        try
        {
            navigation.SetRoot(new WDNavigationContext("home", _ => panel.HideFromNavigation()));
            panel.Open();
            Assert.That(navigation.CurrentId, Is.EqualTo("armory"));
            Assert.That(navigation.Count, Is.EqualTo(2));
            root.transform.Find("ArmoryPanel/DecksTab").GetComponentInChildren<Button>().onClick.Invoke();
            Assert.That(navigation.Count, Is.EqualTo(2));
            panel.Close();
            Assert.That(navigation.CurrentId, Is.EqualTo("home"));
            Assert.That(panel.IsOpen, Is.False);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void SavingAndActivatingDeckUseSameLocalProfileAuthority()
    {
        var store = new MemoryRepository(profile);
        var root = new GameObject("ArmoryTest", typeof(RectTransform));
        WDHomeArmoryPanel panel = root.AddComponent<WDHomeArmoryPanel>();
        try
        {
            panel.Build(Resources.Load<WarDominionUITheme>("UI/WarDominionUITheme"),
                catalog, store, () => { });
            panel.Open();
            root.transform.Find("ArmoryPanel/DecksTab").GetComponentInChildren<Button>().onClick.Invoke();
            root.transform.Find("ArmoryPanel/DecksView/DeckTabs/Deck_2")
                .GetComponentInChildren<Button>().onClick.Invoke();
            root.transform.Find("ArmoryPanel/DecksView/ActivateDeck")
                .GetComponentInChildren<Button>().onClick.Invoke();
            root.transform.Find("ArmoryPanel/DecksView/SaveDeck")
                .GetComponentInChildren<Button>().onClick.Invoke();
            Assert.That(store.Load(catalog).ActiveDeckIndex, Is.EqualTo(1));
            Assert.That(store.SaveCount, Is.GreaterThanOrEqualTo(2));
        }
        finally { Object.DestroyImmediate(root); }
    }

    // ============================================================
    // 04. CONTRATO VISÍVEL DA ABA DECKS E ENTITLEMENT SEGURO
    // ============================================================

    [Test]
    public void DeckTabsShowTwoFreeAndThreeExplicitlyLockedVipPositions()
    {
        GameObject root = BuildPanel(out WDHomeArmoryPanel panel);
        try
        {
            panel.Open();
            OpenDecks(root);
            for (int i = 1; i <= 5; i++)
            {
                Transform tab = root.transform.Find($"ArmoryPanel/DecksView/DeckTabs/Deck_{i}");
                Assert.That(tab, Is.Not.Null);
                string label = tab.GetComponentInChildren<TextMeshProUGUI>(true).text;
                Assert.That(label.Contains("VIP"), Is.EqualTo(i >= 3), $"Deck {i}");
                Assert.That(label.Contains("VER VIP"), Is.EqualTo(i >= 3), $"Deck {i}");
            }
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void LockedVipTabOpensRequirementWithoutSelectingEditingOrGrantingVip()
    {
        int requirementCalls = 0;
        var store = new MemoryRepository(profile);
        var root = new GameObject("ArmoryTest", typeof(RectTransform));
        WDHomeArmoryPanel panel = root.AddComponent<WDHomeArmoryPanel>();
        try
        {
            panel.Build(Resources.Load<WarDominionUITheme>("UI/WarDominionUITheme"),
                catalog, store, () => requirementCalls++);
            panel.Open();
            OpenDecks(root);
            for (int i = 3; i <= 5; i++)
                root.transform.Find($"ArmoryPanel/DecksView/DeckTabs/Deck_{i}")
                    .GetComponentInChildren<Button>().onClick.Invoke();

            Assert.That(requirementCalls, Is.EqualTo(3));
            Assert.That(panel.SelectedDeckIndex, Is.EqualTo(0));
            Assert.That(profile.ActiveDeckIndex, Is.EqualTo(0));
            Assert.That(profile.IsVip, Is.False);
            Assert.That(store.SaveCount, Is.Zero);
            for (int i = 2; i < 5; i++)
            {
                Assert.That(profile.GetDeck(i).MainWeaponIds.All(string.IsNullOrEmpty), Is.True);
                Assert.That(profile.IsDeckUnlocked(i), Is.False);
            }
            Assert.That(service.AssignMain(2, 0, "w0"), Is.Not.EqualTo(WDArmoryEditResult.Success));
            Assert.That(profile.SetActiveDeck(2), Is.False);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void SaveDeckIsVisibleAboveContentAndPersistsFreeDeckComposition()
    {
        var store = new MemoryRepository(profile);
        var root = new GameObject("ArmoryTest", typeof(RectTransform));
        WDHomeArmoryPanel panel = root.AddComponent<WDHomeArmoryPanel>();
        try
        {
            panel.Build(Resources.Load<WarDominionUITheme>("UI/WarDominionUITheme"),
                catalog, store, () => { });
            panel.Open();
            OpenDecks(root);
            RectTransform save = (RectTransform)root.transform.Find("ArmoryPanel/DecksView/SaveDeck");
            RectTransform composition = (RectTransform)root.transform.Find("ArmoryPanel/DecksView/DeckComposition");
            RectTransform collection = (RectTransform)root.transform.Find("ArmoryPanel/DecksView/Collection");
            Assert.That(save.gameObject.activeInHierarchy, Is.True);
            Assert.That(save.GetComponentInChildren<Button>().interactable, Is.True);
            Assert.That(save.GetComponentInChildren<TextMeshProUGUI>(true).text, Is.EqualTo("SALVAR DECK"));
            Assert.That(composition.anchorMax.y, Is.LessThan(save.anchorMin.y));
            Assert.That(collection.anchorMax.y, Is.LessThan(save.anchorMin.y));

            root.transform.Find("ArmoryPanel/DecksView/DeckComposition/MainSlots/Main_1")
                .GetComponentInChildren<Button>().onClick.Invoke();
            root.transform.Find("ArmoryPanel/DecksView/Collection/Viewport/Content/w0/Equip")
                .GetComponentInChildren<Button>().onClick.Invoke();
            int savesBeforeClick = store.SaveCount;
            save.GetComponentInChildren<Button>().onClick.Invoke();
            Assert.That(store.SaveCount, Is.EqualTo(savesBeforeClick + 1));
            Assert.That(store.LastSaved.GetDeck(0).GetMain(0), Is.EqualTo("w0"));

            root.transform.Find("ArmoryPanel/DecksView/DeckTabs/Deck_2")
                .GetComponentInChildren<Button>().onClick.Invoke();
            root.transform.Find("ArmoryPanel/DecksView/DeckTabs/Deck_1")
                .GetComponentInChildren<Button>().onClick.Invoke();
            Assert.That(profile.GetDeck(0).GetMain(0), Is.EqualTo("w0"));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void PersistedDevelopmentVipIsRevokedWithoutDiscardingDeckData()
    {
        const string key = "war_dominion.armory.profile.v1";
        bool existed = PlayerPrefs.HasKey(key);
        string previous = existed ? PlayerPrefs.GetString(key) : null;
        try
        {
            profile.SetVip(true);
            profile.GetDeck(2).SetMain(0, "w0");
            Assert.That(profile.SetActiveDeck(2), Is.True);
            PlayerPrefs.SetString(key, JsonUtility.ToJson(profile));

            WDArmoryProfile loaded = new WDPlayerPrefsArmoryRepository().Load(catalog);
            Assert.That(loaded.IsVip, Is.False);
            Assert.That(loaded.ActiveDeckIndex, Is.EqualTo(0));
            Assert.That(loaded.GetDeck(2).GetMain(0), Is.EqualTo("w0"));
            Assert.That(loaded.IsDeckUnlocked(2), Is.False);
            Assert.That(JsonUtility.FromJson<WDArmoryProfile>(PlayerPrefs.GetString(key)).IsVip, Is.False);
        }
        finally
        {
            if (existed) PlayerPrefs.SetString(key, previous);
            else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }

    private static void OpenDecks(GameObject root) =>
        root.transform.Find("ArmoryPanel/DecksTab").GetComponentInChildren<Button>().onClick.Invoke();

    private GameObject BuildPanel(out WDHomeArmoryPanel panel, WDNavigationStack navigation = null)
    {
        var root = new GameObject("ArmoryTest", typeof(RectTransform));
        panel = root.AddComponent<WDHomeArmoryPanel>();
        panel.Build(Resources.Load<WarDominionUITheme>("UI/WarDominionUITheme"), catalog,
            new MemoryRepository(profile), () => { }, navigation);
        return root;
    }

    private sealed class MemoryRepository : IWDArmoryRepository
    {
        private readonly WDArmoryProfile profile;
        public int SaveCount { get; private set; }
        public WDArmoryProfile LastSaved { get; private set; }
        public MemoryRepository(WDArmoryProfile value) => profile = value;
        public WDArmoryProfile Load(WDArmoryCatalogConfig _) => profile;
        public void Save(WDArmoryProfile value)
        {
            LastSaved = JsonUtility.FromJson<WDArmoryProfile>(JsonUtility.ToJson(value));
            SaveCount++;
        }
    }
}
