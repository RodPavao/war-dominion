using System.Linq;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class WDProfileTests
{
    // ============================================================
    // 01. DADOS, PRIVACIDADE E IDENTIDADE
    // ============================================================

    [Test]
    public void RepositorySeedsStableLocalIdentityFromHomeData()
    {
        WDProfileRepository repository = Repository();
        Assert.That(repository.LocalProfile.PlayerId, Is.EqualTo("WD-4821"));
        Assert.That(repository.LocalProfile.Nickname, Does.StartWith("Guest"));
    }

    [Test]
    public void PublicProfileExposesAllowedBlocks()
    {
        WDProfileData profile = Repository().Find("WD-2847");
        Assert.That(profile.Privacy.StatisticsVisible, Is.True);
        Assert.That(profile.Privacy.PerformanceVisible, Is.True);
        Assert.That(profile.Privacy.AchievementsVisible, Is.True);
    }

    [Test]
    public void PrivateProfileKeepsIdentityButProtectsAllOptionalBlocks()
    {
        WDProfileData profile = Repository().Find("WD-3906");
        Assert.That(profile.Nickname, Is.Not.Empty);
        Assert.That(profile.PlayerId, Is.Not.Empty);
        Assert.That(profile.Division, Is.Not.Empty);
        Assert.That(profile.Privacy.StatisticsVisible, Is.False);
        Assert.That(profile.Privacy.PerformanceVisible, Is.False);
        Assert.That(profile.Privacy.AchievementsVisible, Is.False);
        Assert.That(profile.Privacy.HistoryVisible, Is.False);
    }

    [Test]
    public void PublicProjectionDoesNotReturnValuesFromPrivateBlocks()
    {
        WDProfilePresentation presentation = Repository().Present("WD-3906");
        Assert.That(presentation.Nickname, Is.EqualTo("Guest3906"));
        Assert.That(presentation.Division, Is.Not.Null);
        Assert.That(presentation.Statistics, Is.Null);
        Assert.That(presentation.ModeStats, Is.Null);
        Assert.That(presentation.History, Is.Null);
        Assert.That(presentation.Achievements, Is.Null);
    }

    [Test]
    public void OwnerProjectionAlwaysExposesOwnBlocks()
    {
        WDProfileRepository repository = Repository();
        WDProfilePresentation presentation = repository.Present(
            repository.LocalProfile.PlayerId, true);
        Assert.That(presentation.Statistics, Is.Not.Null);
        Assert.That(presentation.ModeStats, Is.Not.Null.And.Not.Empty);
        Assert.That(presentation.History, Is.Not.Null.And.Empty);
        Assert.That(presentation.Achievements, Is.Not.Null.And.Empty);
    }

    [Test]
    public void DivisionRemainsPublicWhenEveryOptionalBlockIsPrivate()
    {
        WDProfilePresentation presentation = Repository().Present("WD-3906");
        Assert.That(presentation.Division.Id, Is.EqualTo("legionnaire"));
        Assert.That(presentation.Division.DisplayName, Is.EqualTo("LEGIONNAIRE"));
        Assert.That(presentation.Division.EmblemId, Is.Not.Empty);
    }

    [Test]
    public void DivisionOrderIsCatalogDataRatherThanUiOrdinal()
    {
        var catalog = new WDDivisionCatalog(new[]
        {
            new WDDivisionDefinition("second", "SECOND", "second_badge", 20),
            new WDDivisionDefinition("first", "FIRST", "first_badge", 10)
        });
        Assert.That(catalog.Ordered.Select(item => item.Id),
            Is.EqualTo(new[] { "first", "second" }));
    }

    [Test]
    public void FourPrivacyPreferencesChangeIndependently()
    {
        WDProfileRepository repository = Repository();
        repository.UpdateLocalPrivacy(repository.LocalProfile.Privacy.WithStatistics(false));
        Assert.That(repository.LocalProfile.Privacy.ShowStatistics, Is.False);
        Assert.That(repository.LocalProfile.Privacy.ShowModePerformance, Is.True);
        Assert.That(repository.LocalProfile.Privacy.ShowMatchHistory, Is.True);
        Assert.That(repository.LocalProfile.Privacy.ShowMissionsAchievements, Is.True);

        repository.UpdateLocalPrivacy(repository.LocalProfile.Privacy.WithModePerformance(false));
        Assert.That(repository.LocalProfile.Privacy.ShowStatistics, Is.False);
        Assert.That(repository.LocalProfile.Privacy.ShowModePerformance, Is.False);
        Assert.That(repository.LocalProfile.Privacy.ShowMatchHistory, Is.True);
        Assert.That(repository.LocalProfile.Privacy.ShowMissionsAchievements, Is.True);
    }

    [Test]
    public void PrivacyPersistsThroughTheInjectedAuthority()
    {
        var store = new WDMemoryProfilePrivacyStore();
        var first = new WDProfileRepository(HomeData(), store);
        first.UpdateLocalPrivacy(new WDProfilePrivacy(false, true, false, true));
        var reopened = new WDProfileRepository(HomeData(), store);
        Assert.That(reopened.LocalProfile.Privacy.ShowStatistics, Is.False);
        Assert.That(reopened.LocalProfile.Privacy.ShowModePerformance, Is.True);
        Assert.That(reopened.LocalProfile.Privacy.ShowMatchHistory, Is.True);
        Assert.That(reopened.LocalProfile.Privacy.ShowMissionsAchievements, Is.False);
    }

    [Test]
    public void RestorePrivacyDefaultsUsesTheSameRepositoryAuthority()
    {
        WDProfileRepository repository = Repository(new WDProfilePrivacy(false, false, false, false));
        repository.RestoreLocalPrivacyDefaults();
        Assert.That(repository.LocalProfile.Privacy.ShowStatistics, Is.True);
        Assert.That(repository.LocalProfile.Privacy.ShowModePerformance, Is.True);
        Assert.That(repository.LocalProfile.Privacy.ShowMatchHistory, Is.True);
        Assert.That(repository.LocalProfile.Privacy.ShowMissionsAchievements, Is.True);
    }

    [TestCase("statistics")]
    [TestCase("performance")]
    [TestCase("history")]
    [TestCase("achievements")]
    public void PublicProjectionRespectsEachPrivacyPreference(string hiddenBlock)
    {
        bool statistics = hiddenBlock != "statistics";
        bool performance = hiddenBlock != "performance";
        bool history = hiddenBlock != "history";
        bool achievements = hiddenBlock != "achievements";
        WDProfileRepository repository = Repository();
        repository.UpdateLocalPrivacy(new WDProfilePrivacy(
            statistics, performance, achievements, history));
        WDProfilePresentation presentation = repository.Present(
            repository.LocalProfile.PlayerId, false);
        Assert.That(presentation.CanShowStatistics, Is.EqualTo(statistics));
        Assert.That(presentation.CanShowModePerformance, Is.EqualTo(performance));
        Assert.That(presentation.CanShowMatchHistory, Is.EqualTo(history));
        Assert.That(presentation.CanShowMissionsAchievements, Is.EqualTo(achievements));
        Assert.That(presentation.Nickname, Is.Not.Empty);
        Assert.That(presentation.Division, Is.Not.Null);
    }

    [Test]
    public void EmptyNicknameIsRejectedWithoutMutation()
    {
        WDProfileData profile = Repository().LocalProfile;
        string before = profile.Nickname;
        Assert.That(profile.TryRename("   "), Is.False);
        Assert.That(profile.Nickname, Is.EqualTo(before));
    }

    [Test]
    public void ValidNicknameIsTrimmedAndAppliedLocally()
    {
        WDProfileData profile = Repository().LocalProfile;
        Assert.That(profile.TryRename("  Commander77  "), Is.True);
        Assert.That(profile.Nickname, Is.EqualTo("Commander77"));
    }

    // ============================================================
    // 02. PERSONALIZAÇÃO TEMPORÁRIA
    // ============================================================

    [Test]
    public void CosmeticPreviewDoesNotEquipAutomatically()
    {
        WDProfileRepository repository = Repository();
        var session = new WDProfileCustomizationSession(repository.LocalProfile, repository.Cosmetics);
        session.PreviewItem("avatar_02");
        Assert.That(repository.LocalProfile.Equipped[WDProfileCosmeticCategory.Avatar], Is.EqualTo("avatar_01"));
    }

    [Test]
    public void DiscardedCustomizationSessionLeavesEquippedStateUntouched()
    {
        WDProfileRepository repository = Repository();
        var session = new WDProfileCustomizationSession(repository.LocalProfile, repository.Cosmetics);
        session.PreviewItem("frame_03");
        session = null;
        Assert.That(repository.LocalProfile.Equipped[WDProfileCosmeticCategory.Frame], Is.EqualTo("frame_01"));
    }

    [Test]
    public void SaveAppliesOwnedCosmeticSelection()
    {
        WDProfileRepository repository = Repository();
        var session = new WDProfileCustomizationSession(repository.LocalProfile, repository.Cosmetics);
        session.PreviewItem("skin_04");
        Assert.That(session.Save(), Is.True);
        Assert.That(repository.LocalProfile.Equipped[WDProfileCosmeticCategory.TerritorySkin], Is.EqualTo("skin_04"));
    }

    [Test]
    public void PreviewOfUnownedItemDoesNotGrantOrEquipIt()
    {
        WDProfileRepository repository = Repository();
        var session = new WDProfileCustomizationSession(repository.LocalProfile, repository.Cosmetics);
        Assert.That(session.PreviewItem("effect_08"), Is.True);
        Assert.That(session.Save(), Is.False);
        Assert.That(repository.LocalProfile.Equipped[WDProfileCosmeticCategory.Effect], Is.EqualTo("effect_01"));
        Assert.That(repository.Cosmetics.Single(item => item.Id == "effect_08").Owned, Is.False);
    }

    [Test]
    public void CatalogContainsOnlyTheFourApprovedCategories()
    {
        Assert.That(Repository().Cosmetics.Select(item => item.Category).Distinct(),
            Is.EquivalentTo(new[] { WDProfileCosmeticCategory.Avatar, WDProfileCosmeticCategory.Frame,
                WDProfileCosmeticCategory.TerritorySkin, WDProfileCosmeticCategory.Effect }));
    }

    // ============================================================
    // 03. SOCIAL E HISTÓRICO
    // ============================================================

    [Test]
    public void FriendRequestChangesOnlyStrangerToRequestSent()
    {
        WDProfileData profile = Repository().Find("WD-2847");
        profile.SendFriendRequest();
        Assert.That(profile.SocialState, Is.EqualTo(WDProfileSocialState.RequestSent));
    }

    [Test]
    public void BlockedPlayerCannotReturnToNormalFriendRequestFlow()
    {
        WDProfileData profile = Repository().Find("WD-2847");
        profile.Block();
        profile.SendFriendRequest();
        Assert.That(profile.SocialState, Is.EqualTo(WDProfileSocialState.Blocked));
    }

    [Test]
    public void HistoryIsLimitedToTenLatestMatches()
    {
        WDProfileRepository repository = Repository();
        var progress = new WDLocalMatchProgress();
        for (int i = 0; i < 11; i++)
            progress.history.Add(new WDLocalMatchRecord
            {
                resultId = $"WD-{i}", modeId = "1x1", playedAtUtcTicks = 1
            });
        repository.ApplyLocalMatchProgress(progress);
        var history = WDProfileRepository.FilterHistory(repository.LocalProfile, null);
        Assert.That(history.Count, Is.EqualTo(10));
    }

    [Test]
    public void HistoryFilterReturnsOnlyRequestedMode()
    {
        WDProfileRepository repository = Repository();
        var progress = new WDLocalMatchProgress();
        progress.history.Add(new WDLocalMatchRecord
        {
            resultId = "WD-2x2", modeId = "2x2", playedAtUtcTicks = 1
        });
        progress.history.Add(new WDLocalMatchRecord
        {
            resultId = "WD-1x1", modeId = "1x1", playedAtUtcTicks = 1
        });
        repository.ApplyLocalMatchProgress(progress);
        var rows = WDProfileRepository.FilterHistory(repository.LocalProfile, WDProfileMode.TwoVsTwo);
        Assert.That(rows, Is.Not.Empty);
        Assert.That(rows.All(row => row.Mode == WDProfileMode.TwoVsTwo), Is.True);
    }

    [Test]
    public void PrivateHistoryReturnsNoRows()
    {
        Assert.That(WDProfileRepository.FilterHistory(Repository().Find("WD-3906"), null), Is.Empty);
    }

    [Test]
    public void PrivatePresentationHistoryCannotBeFilteredIntoVisibleRows()
    {
        WDProfilePresentation presentation = Repository().Present("WD-3906");
        Assert.That(WDProfileRepository.FilterHistory(presentation, null), Is.Empty);
        Assert.That(WDProfileRepository.FilterHistory(
            presentation, WDProfileMode.OneVsOne), Is.Empty);
    }

    [Test]
    public void MatchIdsAreStableAndUniqueForReplayEndpoint()
    {
        WDProfileRepository repository = Repository();
        var progress = new WDLocalMatchProgress();
        progress.history.Add(new WDLocalMatchRecord
        {
            resultId = "WD-first", modeId = "1x1", playedAtUtcTicks = 1
        });
        progress.history.Add(new WDLocalMatchRecord
        {
            resultId = "WD-second", modeId = "2x2", playedAtUtcTicks = 1
        });
        repository.ApplyLocalMatchProgress(progress);
        var history = repository.LocalProfile.History;
        Assert.That(history.Select(item => item.MatchId).Distinct().Count(), Is.EqualTo(history.Count));
        Assert.That(history.All(item => item.MatchId.StartsWith("WD-")), Is.True);
    }

    // ============================================================
    // 04. APRESENTAÇÃO E NAVEGAÇÃO CONTEXTUAL
    // ============================================================

    [Test]
    public void MyProfileAndHistoryUseTheSameNavigationStack()
    {
        GameObject root = BuildPanel(out WDHomeProfilePanel panel, out WDNavigationStack navigation);
        try
        {
            panel.OpenMyProfile();
            FindButton(root, "HISTÓRICO DE PARTIDAS >").onClick.Invoke();
            Assert.That(panel.CurrentView, Is.EqualTo(WDProfileView.History));
            Assert.That(navigation.TryBack(), Is.True);
            Assert.That(panel.CurrentView, Is.EqualTo(WDProfileView.MyProfile));
            Assert.That(navigation.TryBack(), Is.True);
            Assert.That(navigation.CurrentId, Is.EqualTo("home"));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void PublicProfileIsReusableAndRestoresItsActualOrigin()
    {
        GameObject root = BuildPanel(out WDHomeProfilePanel panel, out WDNavigationStack navigation);
        try
        {
            navigation.Push(new WDNavigationContext("friends", _ => { }));
            panel.OpenPublicProfile("WD-2847");
            Assert.That(panel.CurrentView, Is.EqualTo(WDProfileView.PublicProfile));
            navigation.TryBack();
            Assert.That(navigation.CurrentId, Is.EqualTo("friends"));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void PrivateProfileRendersExplicitPrivateStates()
    {
        GameObject root = BuildPanel(out WDHomeProfilePanel panel, out _);
        try
        {
            panel.OpenPublicProfile("WD-3906");
            string allText = string.Join("|", root.GetComponentsInChildren<TextMeshProUGUI>(true).Select(item => item.text));
            Assert.That(allText, Does.Contain("ESTATÍSTICAS PRIVADAS"));
            Assert.That(allText, Does.Contain("DESEMPENHO PRIVADO"));
            Assert.That(allText, Does.Contain("MISSÕES E CONQUISTAS PRIVADAS"));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void PrivateHistoryRendersNoRowsIdsResultsOrFilters()
    {
        GameObject root = BuildPanel(out WDHomeProfilePanel panel, out _);
        try
        {
            panel.OpenPublicProfile("WD-3906");
            FindButton(root, "HISTÓRICO DE PARTIDAS >").onClick.Invoke();
            string allText = string.Join("|", root.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Select(item => item.text));
            Assert.That(allText, Does.Contain("HISTÓRICO DE PARTIDAS PRIVADO"));
            Assert.That(allText, Does.Not.Contain("PARTIDA: WD-"));
            Assert.That(root.GetComponentsInChildren<Button>(true).Any(button =>
                button.gameObject.name.StartsWith("Filter_")), Is.False);
            Assert.That(panel.VisibleHistoryCount, Is.Zero);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void PublicProfileUsesRequestedPlayerRatherThanLocalIdentity()
    {
        GameObject root = BuildPanel(out WDHomeProfilePanel panel, out _);
        try
        {
            panel.OpenPublicProfile("WD-2847");
            Assert.That(panel.CurrentProfile.PlayerId, Is.EqualTo("WD-2847"));
            Assert.That(panel.CurrentProfile.IsLocalPlayer, Is.False);
            string allText = string.Join("|", root.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Select(item => item.text));
            Assert.That(allText, Does.Contain("Guest2847"));
            Assert.That(allText, Does.Not.Contain("PERSONALIZAR PERFIL"));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void ReplayButtonIdentifiesMatchWithoutCreatingMatchSetup()
    {
        WDProfileRepository repository = Repository();
        var progress = new WDLocalMatchProgress();
        progress.history.Add(new WDLocalMatchRecord
        {
            resultId = "WD-real-result", modeId = "1x1", playedAtUtcTicks = 1
        });
        repository.ApplyLocalMatchProgress(progress);
        GameObject root = BuildPanel(out WDHomeProfilePanel panel, out _, repository);
        try
        {
            WDMatchSetup before = WDMatchSetupContext.Current;
            panel.OpenMyProfile();
            FindButton(root, "HISTÓRICO DE PARTIDAS >").onClick.Invoke();
            FindButton(root, "VER DETALHES").onClick.Invoke();
            Assert.That(panel.RequestedReplayMatchId, Is.EqualTo("WD-real-result"));
            Assert.That(WDMatchSetupContext.Current, Is.SameAs(before));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void CopyPlayerIdUsesTheDisplayedStableId()
    {
        GameObject root = BuildPanel(out WDHomeProfilePanel panel, out _);
        try
        {
            panel.OpenMyProfile();
            FindButton(root, "COPIAR").onClick.Invoke();
            Assert.That(panel.LastCopiedValue, Is.EqualTo("WD-4821"));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void EscapePriorityClosesCustomizationWithoutPoppingProfile()
    {
        GameObject root = BuildPanel(out WDHomeProfilePanel panel, out WDNavigationStack navigation);
        try
        {
            panel.OpenMyProfile();
            FindButton(root, "PERSONALIZAR PERFIL").onClick.Invoke();
            Assert.That(panel.IsCustomizationOpen, Is.True);
            Assert.That(panel.TryHandleModalBack(), Is.True);
            Assert.That(panel.IsCustomizationOpen, Is.False);
            Assert.That(navigation.CurrentId, Is.EqualTo("profile/me"));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void NicknameCancelClosesEditorWithoutApplyingDraft()
    {
        GameObject root = BuildPanel(out WDHomeProfilePanel panel, out _);
        try
        {
            panel.OpenMyProfile();
            FindButton(root, "PERSONALIZAR PERFIL").onClick.Invoke();
            FindButton(root, "EDITAR NOME").onClick.Invoke();
            Transform editor = root.transform.Find("NicknameEditor");
            editor.GetComponentInChildren<TMP_InputField>(true).text = "UnsavedName";
            FindButton(editor.gameObject, "CANCELAR").onClick.Invoke();
            Assert.That(panel.CurrentProfile.Nickname, Is.EqualTo("Guest4821"));
            Assert.That(panel.IsNicknameEditorOpen, Is.False);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void NicknameEditorHasTheViewportRequiredForPointerSelection()
    {
        GameObject root = BuildPanel(out WDHomeProfilePanel panel, out _);
        try
        {
            panel.OpenMyProfile();
            FindButton(root, "PERSONALIZAR PERFIL").onClick.Invoke();
            FindButton(root, "EDITAR NOME").onClick.Invoke();
            TMP_InputField input = root.transform.Find("NicknameEditor")
                .GetComponentInChildren<TMP_InputField>(true);
            Assert.That(input.textViewport, Is.Not.Null);
            Assert.That(input.textComponent, Is.Not.Null);
            Assert.That(input.textComponent.transform.parent, Is.SameAs(input.textViewport));
            Assert.That(input.lineType, Is.EqualTo(TMP_InputField.LineType.SingleLine));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void NicknameConfirmAppliesValidDraftToLocalProfile()
    {
        GameObject root = BuildPanel(out WDHomeProfilePanel panel, out _);
        try
        {
            panel.OpenMyProfile();
            FindButton(root, "PERSONALIZAR PERFIL").onClick.Invoke();
            FindButton(root, "EDITAR NOME").onClick.Invoke();
            Transform editor = root.transform.Find("NicknameEditor");
            editor.GetComponentInChildren<TMP_InputField>(true).text = "Commander77";
            FindButton(editor.gameObject, "SALVAR").onClick.Invoke();
            Assert.That(panel.CurrentProfile.Nickname, Is.EqualTo("Commander77"));
            Assert.That(panel.IsNicknameEditorOpen, Is.False);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void HomeProfileCardOpensTheSharedMyProfilePresentation()
    {
        const string controllerPath =
            "Assets/Scripts/UI/Home/WarDominionHomeController.cs";
        string source = File.ReadAllText(controllerPath);
        string identityBuilder = SourceSection(
            source, "private void BuildIdentity(", "private void BuildCenter(");
        string rightColumnBuilder = SourceSection(
            source, "private void BuildRight(", "private RectTransform CreateRow(");

        StringAssert.Contains("Rect(\"PlayerIdentityCard\"", identityBuilder);
        StringAssert.Contains("button.onClick.AddListener(OpenMyProfile);", identityBuilder);
        StringAssert.Contains("Rect(\"ProfileCard\"", rightColumnBuilder);
        StringAssert.Contains(
            "\"Identidade e personalização\", OpenMyProfile", rightColumnBuilder);
        Assert.That(CountOccurrences(
            source, "gameObject.AddComponent<WDHomeProfilePanel>()"), Is.EqualTo(1));

        GameObject root = BuildPanel(
            out WDHomeProfilePanel sharedPresentation,
            out WDNavigationStack navigation);
        try
        {
            sharedPresentation.OpenMyProfile();
            Assert.That(sharedPresentation.CurrentView,
                Is.EqualTo(WDProfileView.MyProfile));
            Assert.That(navigation.CurrentId, Is.EqualTo("profile/me"));
            Assert.That(navigation.Count, Is.EqualTo(2));

            Assert.That(navigation.TryBack(), Is.True);
            Assert.That(navigation.CurrentId, Is.EqualTo("home"));
            Assert.That(sharedPresentation.IsOpen, Is.False);
            Assert.That(sharedPresentation.CurrentView, Is.EqualTo(WDProfileView.None));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void SettingsPrivacyReadsAndWritesTheSharedRepository()
    {
        WDProfileRepository repository = Repository();
        GameObject root = BuildSettings(repository, out WDHomeSettingsPanel panel,
            out WDNavigationStack navigation);
        try
        {
            panel.OpenPrivacy();
            Assert.That(panel.CurrentPrivacy.ShowStatistics, Is.True);
            FindNamedButton(root, "Toggle_Statistics").onClick.Invoke();
            Assert.That(repository.LocalProfile.Privacy.ShowStatistics, Is.False);
            Assert.That(panel.CurrentPrivacy.ShowModePerformance, Is.True);
            Assert.That(navigation.CurrentId, Is.EqualTo("settings/privacy"));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void SettingsRestoreDefaultsUpdatesTheCanonicalProfilePrivacy()
    {
        WDProfileRepository repository = Repository(
            new WDProfilePrivacy(false, false, false, false));
        GameObject root = BuildSettings(repository, out WDHomeSettingsPanel panel, out _);
        try
        {
            panel.OpenPrivacy();
            FindNamedButton(root, "RestoreDefaults").onClick.Invoke();
            Assert.That(repository.LocalProfile.Privacy.ShowStatistics, Is.True);
            Assert.That(repository.LocalProfile.Privacy.ShowModePerformance, Is.True);
            Assert.That(repository.LocalProfile.Privacy.ShowMatchHistory, Is.True);
            Assert.That(repository.LocalProfile.Privacy.ShowMissionsAchievements, Is.True);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static string SourceSection(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, System.StringComparison.Ordinal);
        int end = source.IndexOf(endMarker, start, System.StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Missing {startMarker}");
        Assert.That(end, Is.GreaterThan(start), $"Missing {endMarker}");
        return source.Substring(start, end - start);
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(value, index, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }

    private static WarDominionHomeData HomeData() =>
        Resources.Load<WarDominionHomeData>("UI/Home/WarDominionHomeMockData");

    private static WDProfileRepository Repository(WDProfilePrivacy privacy = null) => new(
        HomeData(), new WDMemoryProfilePrivacyStore(privacy));

    private static GameObject BuildPanel(out WDHomeProfilePanel panel, out WDNavigationStack navigation,
        WDProfileRepository profileRepository = null)
    {
        var root = new GameObject("ProfileTest", typeof(RectTransform));
        panel = root.AddComponent<WDHomeProfilePanel>();
        WDHomeProfilePanel builtPanel = panel;
        navigation = new WDNavigationStack();
        navigation.SetRoot(new WDNavigationContext("home", _ => builtPanel.HideFromNavigation()));
        panel.Build(Resources.Load<WarDominionUITheme>("UI/WarDominionUITheme"),
            profileRepository ?? Repository(), navigation, (_, _) => { }, () => { });
        return root;
    }

    private static GameObject BuildSettings(WDProfileRepository repository,
        out WDHomeSettingsPanel panel, out WDNavigationStack navigation)
    {
        var root = new GameObject("SettingsTest", typeof(RectTransform));
        panel = root.AddComponent<WDHomeSettingsPanel>();
        WDHomeSettingsPanel builtPanel = panel;
        navigation = new WDNavigationStack();
        navigation.SetRoot(new WDNavigationContext(
            "home", _ => builtPanel.HideFromNavigation()));
        panel.Build(Resources.Load<WarDominionUITheme>("UI/WarDominionUITheme"),
            repository, navigation);
        return root;
    }

    private static Button FindNamedButton(GameObject root, string name) =>
        root.GetComponentsInChildren<Button>(true).First(button =>
            button.gameObject.name == name);

    private static Button FindButton(GameObject root, string label) =>
        root.GetComponentsInChildren<Button>(true).First(button =>
            button.GetComponentsInChildren<TextMeshProUGUI>(true).Any(text => text.text == label));
}
