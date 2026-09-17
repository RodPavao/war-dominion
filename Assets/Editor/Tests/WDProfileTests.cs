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
        var history = WDProfileRepository.FilterHistory(Repository().LocalProfile, null);
        Assert.That(history.Count, Is.EqualTo(10));
    }

    [Test]
    public void HistoryFilterReturnsOnlyRequestedMode()
    {
        var rows = WDProfileRepository.FilterHistory(Repository().LocalProfile, WDProfileMode.TwoVsTwo);
        Assert.That(rows, Is.Not.Empty);
        Assert.That(rows.All(row => row.Mode == WDProfileMode.TwoVsTwo), Is.True);
    }

    [Test]
    public void PrivateHistoryReturnsNoRows()
    {
        Assert.That(WDProfileRepository.FilterHistory(Repository().Find("WD-3906"), null), Is.Empty);
    }

    [Test]
    public void MatchIdsAreStableAndUniqueForReplayEndpoint()
    {
        var history = Repository().LocalProfile.History;
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
    public void ReplayButtonIdentifiesMatchWithoutCreatingMatchSetup()
    {
        GameObject root = BuildPanel(out WDHomeProfilePanel panel, out _);
        try
        {
            WDMatchSetup before = WDMatchSetupContext.Current;
            panel.OpenMyProfile();
            FindButton(root, "HISTÓRICO DE PARTIDAS >").onClick.Invoke();
            FindButton(root, "VER DETALHES").onClick.Invoke();
            Assert.That(panel.RequestedReplayMatchId, Is.Not.Empty);
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

    private static WDProfileRepository Repository() => new(
        Resources.Load<WarDominionHomeData>("UI/Home/WarDominionHomeMockData"));

    private static GameObject BuildPanel(out WDHomeProfilePanel panel, out WDNavigationStack navigation)
    {
        var root = new GameObject("ProfileTest", typeof(RectTransform));
        panel = root.AddComponent<WDHomeProfilePanel>();
        WDHomeProfilePanel builtPanel = panel;
        navigation = new WDNavigationStack();
        navigation.SetRoot(new WDNavigationContext("home", _ => builtPanel.HideFromNavigation()));
        panel.Build(Resources.Load<WarDominionUITheme>("UI/WarDominionUITheme"),
            Repository(), navigation, (_, _) => { }, () => { });
        return root;
    }

    private static Button FindButton(GameObject root, string label) =>
        root.GetComponentsInChildren<Button>(true).First(button =>
            button.GetComponentsInChildren<TextMeshProUGUI>(true).Any(text => text.text == label));
}
