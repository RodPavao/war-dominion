using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class WDMapVoteControllerTests
{
    // ============================================================
    // 01. CANDIDATOS E ELEGIBILIDADE CONFIGURADA
    // ============================================================

    [Test]
    public void SelectCandidates_ReturnsTwoWithoutDuplicates()
    {
        var vote = new WDMapVoteController(10);
        IReadOnlyList<WDMapVoteCandidate> candidates = vote.SelectCandidates(new[]
        {
            Rule("a", 1f), Rule("a", 4f), Rule("b", 1f),
            Rule("c", 1f), Rule("d", 1f)
        });

        Assert.That(candidates, Has.Count.EqualTo(2));
        Assert.That(candidates.Select(item => item.MapId).Distinct().Count(), Is.EqualTo(2));
    }

    [Test]
    public void SelectCandidates_UsesOnlyRulesProvidedAsEligible()
    {
        var vote = new WDMapVoteController(20);
        IReadOnlyList<WDMapVoteCandidate> candidates = vote.SelectCandidates(new[]
        {
            Rule("eligible_a", 1f), Rule("eligible_b", 1f)
        });

        Assert.That(candidates.Select(item => item.MapId),
            Is.SubsetOf(new[] { "eligible_a", "eligible_b" }));
    }

    [Test]
    public void SelectCandidates_IgnoresInvalidWeights()
    {
        var vote = new WDMapVoteController(30);
        IReadOnlyList<WDMapVoteCandidate> candidates = vote.SelectCandidates(new[]
        {
            Rule("zero", 0f), Rule("negative", -2f), Rule("valid", 2f)
        });

        Assert.That(candidates.Select(item => item.MapId), Is.EqualTo(new[] { "valid" }));
    }

    [Test]
    public void SelectCandidates_WithOnlyOneEligibleMap_DoesNotThrow()
    {
        var vote = new WDMapVoteController(40);
        IReadOnlyList<WDMapVoteCandidate> candidates = null;

        Assert.DoesNotThrow(() => candidates = vote.SelectCandidates(new[]
        {
            Rule("a", 1f)
        }));
        Assert.That(candidates, Has.Count.EqualTo(1));
    }

    // ============================================================
    // 02. VOTO ÚNICO, ABSTENÇÃO E RESULTADO
    // ============================================================

    [Test]
    public void SubmitVote_ReplacesPreviousVoteFromSamePlayer()
    {
        WDMapVoteController vote = CreateTwoCandidateVote();

        Assert.That(vote.SubmitVote("player", "a"), Is.True);
        Assert.That(vote.SubmitVote("player", "b"), Is.True);

        Assert.That(vote.GetVote("player"), Is.EqualTo("b"));
        Assert.That(vote.VotesByPlayer, Has.Count.EqualTo(1));
    }

    [Test]
    public void Abstain_RemovesVoteWithoutCreatingAutomaticVote()
    {
        WDMapVoteController vote = CreateTwoCandidateVote();
        vote.SubmitVote("player", "a");
        vote.Abstain("player");

        WDMapVoteResult result = vote.Resolve();

        Assert.That(vote.VotesByPlayer, Is.Empty);
        Assert.That(result.VoteCounts.Values, Is.All.EqualTo(0));
    }

    [Test]
    public void Resolve_SelectsMapWithMostVotes()
    {
        WDMapVoteController vote = CreateTwoCandidateVote();
        vote.SubmitVote("p1", "a");
        vote.SubmitVote("p2", "a");
        vote.SubmitVote("p3", "b");

        Assert.That(vote.Resolve().WinningMapId, Is.EqualTo("a"));
    }

    [Test]
    public void Resolve_TieWinnerBelongsOnlyToTiedMaps()
    {
        WDMapVoteController vote = CreateTwoCandidateVote();
        vote.SubmitVote("p1", "a");
        vote.SubmitVote("p2", "b");

        WDMapVoteResult result = vote.Resolve();

        Assert.That(result.TiedMapIds, Is.EquivalentTo(new[] { "a", "b" }));
        Assert.That(result.WinningMapId == "a" || result.WinningMapId == "b", Is.True);
    }

    private static WDMapVoteController CreateTwoCandidateVote()
    {
        var vote = new WDMapVoteController(50);
        vote.SelectCandidates(new[] { Rule("a", 1f), Rule("b", 1f) });
        return vote;
    }

    private static WDMapModeRule Rule(string mapId, float weight) => new(mapId, weight);

    // ============================================================
    // 03. MATCHMAKING LOCAL E REGRA FIXA DA BATALHA
    // ============================================================

    [Test]
    public void PreMatch_CancelIsAllowedOnlyWhileSearching()
    {
        var flow = new WDPreMatchFlowController();
        flow.BeginSearch();

        Assert.That(flow.CanCancel, Is.True);
        Assert.That(flow.Cancel(), Is.True);
        Assert.That(flow.State, Is.EqualTo(WDPreMatchState.Cancelled));
    }

    [Test]
    public void PreMatch_OpponentFoundBlocksCancellation()
    {
        var flow = new WDPreMatchFlowController();
        flow.BeginSearch();
        flow.Tick(2.5f);

        Assert.That(flow.MarkOpponentFound(), Is.True);
        Assert.That(flow.CanCancel, Is.False);
        Assert.That(flow.Cancel(), Is.False);
        Assert.That(flow.WaitSeconds, Is.EqualTo(2.5f));
    }

    [Test]
    public void Ffa_UsesClassicAsFixedMapWithoutVoting()
    {
        WarDominionMatchFlowConfig config =
            UnityEngine.Resources.Load<WarDominionMatchFlowConfig>(
                "UI/Home/WarDominionMatchFlowConfig");
        WDMatchModeDefinition ffa = config.Modes.Single(mode => mode.Id == "ffa");

        Assert.That(ffa.MapSelectionPolicy, Is.EqualTo(WDMapSelectionPolicy.Fixed));
        Assert.That(ffa.FixedMapId, Is.EqualTo("classic"));
    }

    [Test]
    public void PlayCatalog_UsesOfficialBattleLabelsAndKeepsLegacyTechnicalId()
    {
        WarDominionMatchFlowConfig config = LoadFlowConfig();
        WDMatchModeDefinition battle = config.Modes.Single(mode => mode.Id == "ffa");

        Assert.That(battle.DisplayName, Is.EqualTo("BATALHA"));
        Assert.That(config.Modes.Single(mode => mode.Id == "1x1").DisplayName,
            Is.EqualTo("BATALHA 1V1"));
        Assert.That(config.Modes.Single(mode => mode.Id == "2x2").DisplayName,
            Is.EqualTo("BATALHA 2V2"));
        Assert.That(config.Modes.Single(mode => mode.Id == "3x3").DisplayName,
            Is.EqualTo("BATALHA 3V3"));
        Assert.That(config.Modes.Where(mode => mode.ShowInStandardPlaySelection)
            .All(mode => mode.DisplayName != "FFA"), Is.True);
        Assert.That(battle.MatchSize, Is.EqualTo(4));
        Assert.That(battle.GroupSize, Is.EqualTo(1));
    }

    [Test]
    public void PlayCatalog_KeepsCompetitiveAvailabilityAndThreeVsThreeRules()
    {
        WarDominionMatchFlowConfig config = LoadFlowConfig();

        foreach (string id in new[] { "ffa", "1x1", "2x2" })
        {
            WDMatchModeDefinition mode = config.Modes.Single(item => item.Id == id);
            Assert.That(mode.Availability, Is.EqualTo(WDContentAvailability.Available));
            Assert.That(mode.ShowInStandardPlaySelection, Is.True);
        }

        WDMatchModeDefinition threeVsThree = config.Modes.Single(mode => mode.Id == "3x3");
        Assert.That(threeVsThree.Availability, Is.EqualTo(WDContentAvailability.ComingSoon));
        Assert.That(threeVsThree.ShowInStandardPlaySelection, Is.True);
        Assert.That(threeVsThree.Submodes.Select(submode => submode.Id),
            Is.EqualTo(new[] { "normal" }));
        Assert.That(threeVsThree.Submodes[0].Availability,
            Is.EqualTo(WDContentAvailability.ComingSoon));
        Assert.That(config.Modes.Single(mode => mode.Id == "1x1").Submodes
            .Single(item => item.Id == "deckbuilder").Availability,
            Is.EqualTo(WDContentAvailability.ComingSoon));
        Assert.That(config.Modes.Where(mode => mode.Id == "1x1" || mode.Id == "2x2")
            .SelectMany(mode => mode.Submodes).Where(item => item.Id == "normal" ||
                item.Id == "random_cards").All(item =>
                    item.Availability == WDContentAvailability.Available), Is.True);
        Assert.That(config.Modes.Single(mode => mode.Id == "battle_royale")
            .ShowInStandardPlaySelection, Is.False);
        Assert.That(config.Modes.Any(mode => mode.Id == "friendly" || mode.Id == "amistosos"),
            Is.False, "Amistosos deve permanecer fora do catálogo competitivo.");
    }

    [Test]
    public void PlayPanel_HubRoutesToFourCompetitiveModesAndBackWithoutMutation()
    {
        WarDominionUITheme theme = Resources.Load<WarDominionUITheme>(
            "UI/WarDominionUITheme");
        WarDominionMatchFlowConfig config = LoadFlowConfig();
        WarDominionHomeData profile = LoadProfile();
        WDArmoryCatalogConfig armory = Resources.Load<WDArmoryCatalogConfig>(
            "UI/Home/WDArmoryCatalogConfig");
        var root = new GameObject("PlayPanelTest", typeof(RectTransform));
        try
        {
            WDMatchSetup existingSetup = WDMatchSetupContext.Current;
            var panel = root.AddComponent<WDHomeMatchFlowPanel>();
            panel.Build(theme, config, profile, armory, null);
            panel.Open();

            Assert.That(panel.CurrentScreen, Is.EqualTo(WDHomeMatchFlowScreen.PlayHub));
            Assert.That(VisibleOptionTitles(root), Is.EquivalentTo(new[]
            {
                "TORNEIOS", "PARTIDAS", "AMISTOSOS"
            }));

            FindButton(root, "PARTIDAS").onClick.Invoke();
            Assert.That(panel.CurrentScreen, Is.EqualTo(WDHomeMatchFlowScreen.OfficialMatches));
            Assert.That(VisibleOptionTitles(root), Is.EquivalentTo(new[]
            {
                "BATALHA", "BATALHA 1V1", "BATALHA 2V2", "BATALHA 3V3"
            }));
            Assert.That(VisibleOptionTitles(root), Does.Not.Contain("AMISTOSOS"));
            Assert.That(VisibleOptionTitles(root), Does.Not.Contain("BATTLE ROYALE"));
            Assert.That(panel.IsMatchmakingActive, Is.False);
            Assert.That(WDMatchSetupContext.Current, Is.SameAs(existingSetup));

            Button back = root.transform.Find("MatchFlowPanel/Back")?.GetComponent<Button>();
            Assert.That(back, Is.Not.Null);
            back.onClick.Invoke();
            Assert.That(panel.CurrentScreen, Is.EqualTo(WDHomeMatchFlowScreen.PlayHub));
            Assert.That(VisibleOptionTitles(root), Is.EquivalentTo(new[]
            {
                "TORNEIOS", "PARTIDAS", "AMISTOSOS"
            }));
            Assert.That(WDMatchSetupContext.Current, Is.SameAs(existingSetup));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void PlayPanel_ThreeVsThreeIsVisibleButCannotStartQueue()
    {
        GameObject root = BuildPlayPanel(out WDHomeMatchFlowPanel panel);
        try
        {
            FindButton(root, "PARTIDAS").onClick.Invoke();
            Button threeVsThree = FindButton(root, "BATALHA 3V3");

            Assert.That(threeVsThree, Is.Not.Null);
            Assert.That(threeVsThree.interactable, Is.False);
            Assert.That(panel.IsMatchmakingActive, Is.False);
            Assert.That(panel.CurrentScreen, Is.EqualTo(WDHomeMatchFlowScreen.OfficialMatches));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void CancellingQueueClearsOldSetupAndNextAttemptStartsFresh()
    {
        WDMatchSetup prior = WDMatchSetupContext.Current;
        GameObject root = BuildPlayPanel(out WDHomeMatchFlowPanel panel);
        try
        {
            WDMatchSetupContext.Store(new WDMatchSetup("old", "", "classic", "old-scene",
                "standard", 10, true, 1, System.Array.Empty<WDMatchParticipant>()));
            FindButton(root, "PARTIDAS").onClick.Invoke();
            FindButton(root, "BATALHA 1V1").onClick.Invoke();
            FindButton(root, "NORMAL").onClick.Invoke();

            Assert.That(panel.IsMatchmakingActive, Is.True);
            Assert.That(WDMatchSetupContext.Current, Is.Null);
            InvokeBack(root);
            Assert.That(panel.IsMatchmakingActive, Is.False);
            Assert.That(WDMatchSetupContext.Current, Is.Null);

            FindButton(root, "RANDOM CARDS").onClick.Invoke();
            Assert.That(panel.IsMatchmakingActive, Is.True);
            Assert.That(WDMatchSetupContext.Current, Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(root);
            WDMatchSetupContext.Clear();
            if (prior != null) WDMatchSetupContext.Store(prior);
        }
    }

    [Test]
    public void PlayPanel_FriendlyTreePreservesLocalBackContextAndNeverStartsMatch()
    {
        GameObject root = BuildPlayPanel(out WDHomeMatchFlowPanel panel);
        try
        {
            WDMatchSetup existingSetup = WDMatchSetupContext.Current;
            FindButton(root, "AMISTOSOS").onClick.Invoke();
            Assert.That(panel.CurrentScreen, Is.EqualTo(WDHomeMatchFlowScreen.FriendlyHub));
            Assert.That(VisibleOptionTitles(root), Is.EquivalentTo(new[]
                { "PARTIDAS", "TORNEIOS AMISTOSOS" }));

            FindButton(root, "PARTIDAS").onClick.Invoke();
            Assert.That(panel.CurrentScreen, Is.EqualTo(WDHomeMatchFlowScreen.FriendlyMatchesHub));
            Assert.That(VisibleOptionTitles(root), Is.EquivalentTo(new[]
                { "CRIAR PARTIDA", "SALAS CRIADAS" }));
            InvokeBack(root);
            Assert.That(panel.CurrentScreen, Is.EqualTo(WDHomeMatchFlowScreen.FriendlyHub));

            FindButton(root, "TORNEIOS AMISTOSOS").onClick.Invoke();
            Assert.That(panel.CurrentScreen, Is.EqualTo(WDHomeMatchFlowScreen.FriendlyTournamentsHub));
            Assert.That(VisibleOptionTitles(root), Is.EquivalentTo(new[]
                { "CRIAR TORNEIO", "SALAS DE TORNEIOS" }));
            InvokeBack(root);
            Assert.That(panel.CurrentScreen, Is.EqualTo(WDHomeMatchFlowScreen.FriendlyHub));
            InvokeBack(root);
            Assert.That(panel.CurrentScreen, Is.EqualTo(WDHomeMatchFlowScreen.PlayHub));
            Assert.That(panel.IsMatchmakingActive, Is.False);
            Assert.That(WDMatchSetupContext.Current, Is.SameAs(existingSetup));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void PlayPanel_OfficialTournamentsAndAssistNeverCreatePlayerMatch()
    {
        GameObject root = BuildPlayPanel(out WDHomeMatchFlowPanel panel);
        try
        {
            WDMatchSetup existingSetup = WDMatchSetupContext.Current;
            string currentTournamentName = new WDTournamentRepository(
                new WDProfileRepository(LoadProfile()))
                .CurrentOfficialTournament.Name;

            FindButton(root, "TORNEIOS").onClick.Invoke();
            Assert.That(panel.CurrentScreen, Is.EqualTo(WDHomeMatchFlowScreen.OfficialTournaments));
            Assert.That(VisibleOptionTitles(root), Does.Contain(currentTournamentName));

            FindButton(root, currentTournamentName).onClick.Invoke();
            FindButton(root, "ASSISTIR").onClick.Invoke();
            Assert.That(panel.CurrentScreen, Is.EqualTo(WDHomeMatchFlowScreen.OfficialTournaments));
            Assert.That(root.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Any(text => text.text.Contains("Spectator real não está disponível")), Is.True);
            Assert.That(panel.IsMatchmakingActive, Is.False);
            Assert.That(WDMatchSetupContext.Current, Is.SameAs(existingSetup));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void PlayPanel_CardAndAccessButtonRouteToTheSameSingleDestination()
    {
        GameObject root = BuildPlayPanel(out WDHomeMatchFlowPanel panel);
        try
        {
            Transform playOptions = root.transform.Find("MatchFlowPanel/Options");
            Transform tournaments = playOptions.Find("TORNEIOS");
            tournaments.GetComponent<Button>().onClick.Invoke();
            Assert.That(panel.CurrentScreen, Is.EqualTo(WDHomeMatchFlowScreen.OfficialTournaments));
            string cardDestinationTitle = MatchFlowTitle(root);
            string[] cardDestinationOptions = VisibleOptionTitles(root);

            InvokeBack(root);
            tournaments = root.transform.Find("MatchFlowPanel/Options/TORNEIOS");
            tournaments.Find("Action").GetComponent<Button>().onClick.Invoke();
            Assert.That(panel.CurrentScreen, Is.EqualTo(WDHomeMatchFlowScreen.OfficialTournaments));
            Assert.That(MatchFlowTitle(root), Is.EqualTo(cardDestinationTitle));
            Assert.That(VisibleOptionTitles(root), Is.EqualTo(cardDestinationOptions));
            Assert.That(root.transform.Cast<Transform>()
                .Count(child => child.name == "MatchFlowPanel"), Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void TournamentParticipantPublicProfileBackRestoresSameTournamentParticipantsContext()
    {
        WarDominionUITheme theme = Resources.Load<WarDominionUITheme>(
            "UI/WarDominionUITheme");
        WDArmoryCatalogConfig armory = Resources.Load<WDArmoryCatalogConfig>(
            "UI/Home/WDArmoryCatalogConfig");
        WarDominionHomeData homeData = LoadProfile();
        var profiles = new WDProfileRepository(homeData);
        var navigation = new WDNavigationStack();
        var root = new GameObject("TournamentProfileNavigationTest", typeof(RectTransform));
        RectTransform matchLayer = WDHomeUIFactory.Rect("MatchLayer", root.transform);
        RectTransform profileLayer = WDHomeUIFactory.Rect("ProfileLayer", root.transform);
        var matchPanel = matchLayer.gameObject.AddComponent<WDHomeMatchFlowPanel>();
        var profilePanel = profileLayer.gameObject.AddComponent<WDHomeProfilePanel>();

        try
        {
            navigation.SetRoot(new WDNavigationContext("home", _ =>
            {
                matchPanel.HideFromNavigation();
                profilePanel.HideFromNavigation();
            }));
            profilePanel.Build(theme, profiles, navigation, (_, _) => { }, () => { });
            matchPanel.Build(theme, LoadFlowConfig(), homeData, armory, null,
                navigation, profiles, profilePanel.OpenPublicProfile);

            matchPanel.Open();
            FindButton(root, "TORNEIOS").onClick.Invoke();
            string tournamentName = new WDTournamentRepository(profiles)
                .CurrentOfficialTournament.Name;
            FindButton(root, tournamentName).onClick.Invoke();
            FindButton(root, "PARTICIPANTES").onClick.Invoke();
            FindButton(root, "VER PERFIL").onClick.Invoke();

            Assert.That(profilePanel.CurrentView, Is.EqualTo(WDProfileView.PublicProfile));
            FindButton(root, "<  VOLTAR").onClick.Invoke();

            Assert.That(navigation.CurrentId, Is.EqualTo("play/tournaments/generals-cup"));
            Assert.That(matchPanel.CurrentScreen, Is.EqualTo(WDHomeMatchFlowScreen.OfficialTournaments));
            Assert.That(MatchFlowTitle(matchLayer.gameObject), Is.EqualTo(tournamentName));
            Assert.That(MatchFlowSubtitle(matchLayer.gameObject), Does.StartWith("PARTICIPANTES"));
            Assert.That(matchLayer.GetSiblingIndex(), Is.EqualTo(root.transform.childCount - 1));

            InvokeBack(matchLayer.gameObject);
            Assert.That(navigation.CurrentId, Is.EqualTo("play/tournaments"));
            Assert.That(MatchFlowTitle(matchLayer.gameObject), Is.EqualTo("TORNEIOS OFICIAIS"));

            InvokeBack(matchLayer.gameObject);
            Assert.That(navigation.CurrentId, Is.EqualTo("play"));
            Assert.That(matchPanel.CurrentScreen, Is.EqualTo(WDHomeMatchFlowScreen.PlayHub));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static GameObject BuildPlayPanel(out WDHomeMatchFlowPanel panel)
    {
        WarDominionUITheme theme = Resources.Load<WarDominionUITheme>(
            "UI/WarDominionUITheme");
        WDArmoryCatalogConfig armory = Resources.Load<WDArmoryCatalogConfig>(
            "UI/Home/WDArmoryCatalogConfig");
        var root = new GameObject("PlayPanelTest", typeof(RectTransform));
        panel = root.AddComponent<WDHomeMatchFlowPanel>();
        panel.Build(theme, LoadFlowConfig(), LoadProfile(), armory, null);
        panel.Open();
        return root;
    }

    private static void InvokeBack(GameObject root) =>
        root.transform.Find("MatchFlowPanel/Back").GetComponent<Button>().onClick.Invoke();

    private static string MatchFlowTitle(GameObject root) =>
        root.transform.Find("MatchFlowPanel/Title").GetComponent<TextMeshProUGUI>().text;

    private static string MatchFlowSubtitle(GameObject root) =>
        root.GetComponentsInChildren<TextMeshProUGUI>(true)
            .First(text => text.gameObject.name == "Subtitle").text;

    private static Button FindButton(GameObject root, string title) =>
        root.GetComponentsInChildren<Button>(true).FirstOrDefault(button =>
            button.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Any(text => text.text == title));

    private static string[] VisibleOptionTitles(GameObject root)
    {
        Transform options = root.transform.Find("MatchFlowPanel/Options");
        return options == null
            ? System.Array.Empty<string>()
            : options.Cast<Transform>()
                .Where(child => child.gameObject.activeSelf)
                .Select(child => child.Find("Title")?.GetComponent<TextMeshProUGUI>()?.text)
                .Where(title => !string.IsNullOrEmpty(title))
                .ToArray();
    }

    // ============================================================
    // 04. MATCH SETUP, DECKS, ROUNDS E PARTICIPANTES
    // ============================================================

    [Test]
    public void MatchSetup_FfaUsesClassicDefaultDeckAndSuddenDeath()
    {
        WarDominionMatchFlowConfig config = LoadFlowConfig();
        WarDominionHomeData profile = LoadProfile();
        WDMatchModeDefinition ffa = config.Modes.Single(mode => mode.Id == "ffa");
        WDMatchSetup setup = WDMatchSetupFactory.Create(
            new WDMatchmakingRequest(ffa, null, string.Empty),
            LoadMap("Classic"), profile, 2);

        Assert.That(setup.MapId, Is.EqualTo("classic"));
        Assert.That(setup.Participants, Has.Count.EqualTo(4));
        Assert.That(setup.Participants[0].DeckId,
            Is.EqualTo(profile.GetDeck(profile.DefaultDeckIndex).Id));
        Assert.That(setup.RoundLimit, Is.EqualTo(10));
        Assert.That(setup.SuddenDeathEnabled, Is.True);
    }

    [Test]
    public void MatchSetup_DeckChoiceIsMatchOnlyAndMapWinnerIsPreserved()
    {
        WarDominionMatchFlowConfig config = LoadFlowConfig();
        WarDominionHomeData profile = LoadProfile();
        int defaultBefore = profile.DefaultDeckIndex;
        WDMatchModeDefinition duel = config.Modes.Single(mode => mode.Id == "1x1");
        WDMatchSubmodeDefinition normal = duel.Submodes.Single(mode => mode.Id == "normal");
        WDMatchSetup setup = WDMatchSetupFactory.Create(
            new WDMatchmakingRequest(duel, normal, string.Empty),
            LoadMap("Riachuelo"), profile, 2);

        Assert.That(setup.MapId, Is.EqualTo("riachuelo"));
        Assert.That(setup.Participants[0].DeckId, Is.EqualTo(profile.GetDeck(2).Id));
        Assert.That(profile.DefaultDeckIndex, Is.EqualTo(defaultBefore));
        Assert.That(setup.RoundLimit, Is.EqualTo(10));
        Assert.That(setup.SuddenDeathEnabled, Is.True);
        Assert.That(setup.ScenePath, Is.Not.Empty);
    }

    [Test]
    public void MatchSetup_OneVsOneNormalHasTwoIndependentParticipants()
    {
        WarDominionMatchFlowConfig config = LoadFlowConfig();
        WDMatchModeDefinition duel = config.Modes.Single(mode => mode.Id == "1x1");
        WDMatchSubmodeDefinition normal = duel.Submodes.Single(mode => mode.Id == "normal");
        WDMatchSetup setup = WDMatchSetupFactory.Create(
            new WDMatchmakingRequest(duel, normal, string.Empty),
            LoadMap("Classic"), LoadProfile(), 0);

        Assert.That(setup.Participants, Has.Count.EqualTo(2));
        Assert.That(setup.Participants.Count(item => item.Kind == WDMatchParticipantKind.Local),
            Is.EqualTo(1));
        Assert.That(setup.Participants.Select(item => item.TeamIndex).Distinct().Count(),
            Is.EqualTo(2));
        Assert.That(setup.RoundLimit, Is.EqualTo(10));
        Assert.That(setup.SuddenDeathEnabled, Is.True);
        Assert.That(setup.CardRuleId, Is.EqualTo("standard_deck"));
        Assert.That(setup.Participants.Select(item => item.SlotIndex),
            Is.EquivalentTo(new[] { 0, 1 }));
    }

    [Test]
    public void Profile_ProvidesThreeDecksWithEightMainSlots()
    {
        WarDominionHomeData profile = LoadProfile();

        Assert.That(profile.Decks, Has.Count.EqualTo(3));
        Assert.That(profile.Decks, Has.All.Matches<WDDeckProfile>(
            deck => deck.CardIds.Count == 8));
        Assert.That(profile.DefaultDeckIndex, Is.InRange(0, 2));
    }

    [Test]
    public void MatchSetup_BattleRoyaleUsesFiveRounds()
    {
        WarDominionMatchFlowConfig config = LoadFlowConfig();
        WDMatchModeDefinition mode = config.Modes.Single(item => item.Id == "battle_royale");
        WDMatchSetup setup = WDMatchSetupFactory.Create(
            new WDMatchmakingRequest(mode, null, string.Empty),
            LoadMap("Classic"), LoadProfile(), 0);

        Assert.That(setup.RoundLimit, Is.EqualTo(5));
    }

    [Test]
    public void MatchSetup_TeamModeRepresentsParticipantsAndTeams()
    {
        WarDominionMatchFlowConfig config = LoadFlowConfig();
        WDMatchModeDefinition mode = config.Modes.Single(item => item.Id == "2x2");
        WDMatchSubmodeDefinition submode = mode.Submodes.Single(item => item.Id == "normal");
        WDMatchSetup setup = WDMatchSetupFactory.Create(
            new WDMatchmakingRequest(mode, submode, "random_teammate"),
            LoadMap("Classic"), LoadProfile(), 0);

        Assert.That(setup.Participants, Has.Count.EqualTo(4));
        Assert.That(setup.Participants.Count(item => item.TeamIndex == 0), Is.EqualTo(2));
        Assert.That(setup.Participants.Count(item => item.TeamIndex == 1), Is.EqualTo(2));
        Assert.That(setup.Participants.Select(item => item.SlotIndex).Distinct().Count(), Is.EqualTo(4));
        Assert.That(setup.IsTeamMatch, Is.True);
        Assert.That(setup.RequiresInitialTerritorySelection, Is.False);
    }

    [Test]
    public void MatchSetup_ThreeVsThreeCreatesSixAlternatingParticipants()
    {
        WarDominionMatchFlowConfig config = LoadFlowConfig();
        WDMatchModeDefinition mode = config.Modes.Single(item => item.Id == "3x3");
        WDMatchSubmodeDefinition submode =
            mode.Submodes.Single(item => item.Id == "normal");
        WDMatchSetup setup = WDMatchSetupFactory.Create(
            new WDMatchmakingRequest(mode, submode, "random_teammate"),
            LoadMap("Classic"), LoadProfile(), 0);

        Assert.That(mode.Availability, Is.EqualTo(WDContentAvailability.ComingSoon));
        Assert.That(setup.Participants, Has.Count.EqualTo(6));
        Assert.That(setup.Participants.Select(item => item.TeamIndex),
            Is.EqualTo(new[] { 0, 1, 0, 1, 0, 1 }));
        Assert.That(setup.Participants.Select(item => item.SlotIndex),
            Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 }));
        Assert.That(setup.Participants.Select(item => item.MatchColor).Distinct().Count(),
            Is.EqualTo(6));
        Assert.That(setup.RequiresInitialTerritorySelection, Is.False);
        Assert.That(setup.UsesSymmetricRandomCards, Is.False);
        Assert.That(setup.Participants.Single(item =>
            item.Kind == WDMatchParticipantKind.Local).DeckId, Is.Not.Empty);
        Assert.That(setup.Participants.Where(item =>
            item.Kind == WDMatchParticipantKind.Remote).All(item =>
                item.DeckId == setup.Participants[0].DeckId), Is.True);
    }

    [Test]
    public void ThreeVsThreeNormalUsesVotingAndDoesNotOfferRandomCards()
    {
        WDMatchModeDefinition mode = LoadFlowConfig().Modes.Single(
            item => item.Id == "3x3");

        Assert.That(mode.MapSelectionPolicy, Is.EqualTo(WDMapSelectionPolicy.Vote));
        Assert.That(mode.Submodes.Select(item => item.Id),
            Is.EqualTo(new[] { "normal" }));
        Assert.That(mode.Submodes[0].CardRuleId, Is.EqualTo("standard_deck"));
        Assert.That(mode.Submodes[0].DeckSelectionEnabled, Is.False);
        Assert.That(mode.Submodes[0].EligibleMaps, Is.Not.Empty);
    }

    [Test]
    public void MatchSetup_TeamRandomCardsUsesSymmetricRuleWithoutProfileDecks()
    {
        WarDominionMatchFlowConfig config = LoadFlowConfig();
        WDMatchModeDefinition mode = config.Modes.Single(item => item.Id == "2x2");
        WDMatchSubmodeDefinition submode = mode.Submodes.Single(item => item.Id == "random_cards");
        WDMatchSetup setup = WDMatchSetupFactory.Create(
            new WDMatchmakingRequest(mode, submode, "random_teammate"),
            LoadMap("Classic"), LoadProfile(), 0, 144);

        Assert.That(setup.UsesSymmetricRandomCards, Is.True);
        Assert.That(setup.RequiresInitialTerritorySelection, Is.False);
        Assert.That(setup.Participants.All(participant =>
            string.IsNullOrEmpty(participant.DeckId)), Is.True);
        Assert.That(setup.DeterministicSeed, Is.EqualTo(144));
    }

    [Test]
    public void TeamRandomCardsRuleDoesNotRequireProfileDeckSelection()
    {
        Assert.That(WDMatchSetup.IsRandomCardsRule(
            "symmetric_team_random_cards"), Is.True);
        Assert.That(WDMatchSetup.IsRandomCardsRule(
            "symmetric_random_cards"), Is.True);
        Assert.That(WDMatchSetup.IsRandomCardsRule(
            "standard_deck"), Is.False);
    }

    [Test]
    public void ColorResolver_ChangesOnlyEffectiveDuplicateColors()
    {
        UnityEngine.Color preferred = UnityEngine.Color.red;
        var participants = new List<WDMatchParticipant>
        {
            new("a", "A", 0, 0, preferred, "skin", "deck", WDMatchParticipantKind.Local),
            new("b", string.Empty, 1, 1, preferred, string.Empty, string.Empty, WDMatchParticipantKind.Remote)
        };

        WDMatchColorResolver.Resolve(participants);

        Assert.That(participants[0].ProfileColor, Is.EqualTo(preferred));
        Assert.That(participants[1].ProfileColor, Is.EqualTo(preferred));
        Assert.That(participants[0].MatchColor, Is.Not.EqualTo(participants[1].MatchColor));
    }

    // ============================================================
    // 05. PALETA GLOBAL INDEPENDENTE DE MAPA, MODO E EQUIPE
    // ============================================================

    [Test]
    public void MatchSetupFactoryUsesSameGlobalPaletteAcrossOfficialMaps()
    {
        WarDominionMatchFlowConfig config = LoadFlowConfig();
        WDMatchModeDefinition mode = config.Modes.Single(item => item.Id == "2x2");
        WDMatchSubmodeDefinition submode =
            mode.Submodes.Single(item => item.Id == "normal");
        var request = new WDMatchmakingRequest(mode, submode, "random_teammate");

        WDMatchSetup classic = WDMatchSetupFactory.Create(
            request, LoadMap("Classic"), LoadProfile(), 0, 51);
        WDMatchSetup riachuelo = WDMatchSetupFactory.Create(
            request, LoadMap("Riachuelo"), LoadProfile(), 0, 51);

        Assert.That(classic.Participants.Select(item => item.MatchColor),
            Is.EqualTo(riachuelo.Participants.Select(item => item.MatchColor)));
        IReadOnlyList<UnityEngine.Color> global =
            WDPlayerPaletteConfig.GetGlobalColors();
        Assert.That(classic.Participants.All(participant =>
            global.Any(color => ApproximatelyRgb(color, participant.MatchColor))),
            Is.True);
    }

    [Test]
    public void ColorResolutionDoesNotChangePlayerOrTeamIdentity()
    {
        UnityEngine.Color duplicated =
            WDPlayerPaletteConfig.GetGlobalColor(0);
        var participants = new List<WDMatchParticipant>
        {
            new("player_a", "A", 7, 0, duplicated, "", "",
                WDMatchParticipantKind.Local),
            new("player_b", "B", 3, 1, duplicated, "", "",
                WDMatchParticipantKind.Remote)
        };

        WDMatchColorResolver.Resolve(participants);

        Assert.That(participants[0].PlayerId, Is.EqualTo("player_a"));
        Assert.That(participants[1].PlayerId, Is.EqualTo("player_b"));
        Assert.That(participants[0].TeamIndex, Is.EqualTo(7));
        Assert.That(participants[1].TeamIndex, Is.EqualTo(3));
        Assert.That(participants[0].MatchColor,
            Is.Not.EqualTo(participants[1].MatchColor));
    }

    [Test]
    public void ColorResolverWithoutExplicitAlternativesUsesGlobalConfiguration()
    {
        UnityEngine.Color duplicated =
            WDPlayerPaletteConfig.GetGlobalColor(0);
        var participants = new List<WDMatchParticipant>
        {
            new("a", "A", 0, 0, duplicated, "", "",
                WDMatchParticipantKind.Local),
            new("b", "B", 1, 1, duplicated, "", "",
                WDMatchParticipantKind.Remote)
        };

        WDMatchColorResolver.Resolve(participants);

        Assert.That(WDPlayerPaletteConfig.GetGlobalColors().Any(color =>
            ApproximatelyRgb(color, participants[1].MatchColor)), Is.True);
    }

    private static bool ApproximatelyRgb(
        UnityEngine.Color first,
        UnityEngine.Color second) =>
        UnityEngine.Mathf.Approximately(first.r, second.r) &&
        UnityEngine.Mathf.Approximately(first.g, second.g) &&
        UnityEngine.Mathf.Approximately(first.b, second.b);

    private static WarDominionMatchFlowConfig LoadFlowConfig() =>
        UnityEngine.Resources.Load<WarDominionMatchFlowConfig>(
            "UI/Home/WarDominionMatchFlowConfig");

    private static WarDominionHomeData LoadProfile() =>
        UnityEngine.Resources.Load<WarDominionHomeData>(
            "UI/Home/WarDominionHomeMockData");

    private static DefinicaoMapa LoadMap(string name) =>
        UnityEngine.Resources.Load<DefinicaoMapa>($"Mapas/{name}");
}
