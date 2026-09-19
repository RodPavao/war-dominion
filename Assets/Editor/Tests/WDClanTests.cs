using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class WDClanTests
{
    private WDProfileRepository profiles;
    private WDArmoryCatalogConfig catalog;
    private MemoryArmory armory;
    private WDClanRepository clans;
    private WDClanData home;
    private DateTimeOffset now;
    private const string Local = "WD-4821";
    private const string Officer = "WD-2847";
    private const string Member = "WD-7105";
    private const string Stranger = "WD-3906";

    [SetUp]
    public void SetUp()
    {
        profiles = new WDProfileRepository(null, new WDMemoryProfilePrivacyStore());
        catalog = ScriptableObject.CreateInstance<WDArmoryCatalogConfig>();
        catalog.Configure(new[] { new WDWeaponCategoryDefinition("support", "Apoio", true) },
            new[] { new WDWeaponDefinition("w0", "Radar", "support",
                new[] { new WDWeaponLevelDefinition(2, 10, 20) }) });
        armory = new MemoryArmory();
        foreach (string id in new[] { Local, Officer, Member, Stranger, "WD-5582" })
        {
            WDArmoryProfile value = armory.Load(id);
            value.AddProgress(new WDWeaponProgress("w0", true, 2, id == Local ? 30 : 4));
        }
        now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        clans = new WDClanRepository(profiles, catalog, armory, () => now,
            seedMocks: false);
        Assert.That(clans.Create(Local, "Vanguard Elite", "VGD", "Teste",
            WDClanAdmission.Open, out home), Is.EqualTo(WDClanResult.Success));
        Assert.That(clans.Join(home.Id, Officer), Is.EqualTo(WDClanResult.Success));
        Assert.That(clans.Join(home.Id, Member), Is.EqualTo(WDClanResult.Success));
        Assert.That(clans.Promote(home.Id, Local, Officer), Is.EqualTo(WDClanResult.Success));
    }

    [TearDown]
    public void TearDown() => UnityEngine.Object.DestroyImmediate(catalog);

    // ============================================================
    // 01. MEMBERSHIP, CRIAÇÃO E ADMISSÃO
    // ============================================================
    [Test] public void CreationIsCurrentlyFreeAndDoesNotSpendCoins()
    {
        Assert.That(clans.CreationPolicy.CoinCost, Is.Zero);
        Assert.That(armory.Load(Local).Coins, Is.Zero);
        Assert.That(clans.Create(Stranger, "Novo", "NVO", "", WDClanAdmission.Open, out _),
            Is.EqualTo(WDClanResult.Success));
        Assert.That(armory.Load(Stranger).Coins, Is.Zero);
    }

    [Test] public void APlayerCanBelongToOnlyOneClanOrCreateAnother()
    {
        WDClanData other = CreateOther();
        Assert.That(clans.Join(other.Id, Member), Is.EqualTo(WDClanResult.AlreadyMember));
        Assert.That(clans.Create(Member, "Terceiro", "TRC", "", WDClanAdmission.Open,
            out _), Is.EqualTo(WDClanResult.AlreadyMember));
        Assert.That(clans.ClanOf(Member), Is.SameAs(home));
    }

    [Test] public void OpenClanJoinsImmediatelyAsMemberAndProjectsPublicProfile()
    {
        Assert.That(clans.Join(home.Id, Stranger), Is.EqualTo(WDClanResult.Success));
        Assert.That(home.Member(Stranger).Role, Is.EqualTo(WDClanRole.Member));
        Assert.That(profiles.Find(Stranger).Clan, Is.EqualTo(home.Name));
    }

    [Test] public void ApprovalClanCreatesOnePendingRequestWithoutImmediateMembership()
    {
        WDClanData other = CreateOther(WDClanAdmission.Approval);
        Assert.That(clans.Join(other.Id, Stranger), Is.EqualTo(WDClanResult.Success));
        Assert.That(clans.Join(other.Id, Stranger), Is.EqualTo(WDClanResult.AlreadyPending));
        Assert.That(clans.ClanOf(Stranger), Is.Null);
        Assert.That(other.JoinRequests.Count, Is.EqualTo(1));
    }

    [Test] public void CommanderAcceptsApprovalRequestAndPendingStatesAreNormalized()
    {
        WDClanData other = CreateOther(WDClanAdmission.Approval);
        clans.Join(other.Id, Stranger);
        WDClanJoinRequest request = other.JoinRequests.Single();
        Assert.That(clans.DecideJoin(other.Id, "WD-5582", request.Id, true),
            Is.EqualTo(WDClanResult.Success));
        Assert.That(other.Member(Stranger).Role, Is.EqualTo(WDClanRole.Member));
        Assert.That(other.JoinRequests, Is.Empty);
    }

    [Test] public void RejectedRequestDoesNotAddPlayer()
    {
        WDClanData other = CreateOther(WDClanAdmission.Approval);
        clans.Join(other.Id, Stranger);
        Assert.That(clans.DecideJoin(other.Id, "WD-5582", other.JoinRequests.Single().Id, false),
            Is.EqualTo(WDClanResult.Success));
        Assert.That(clans.ClanOf(Stranger), Is.Null);
    }

    [Test] public void RequestCannotBeAcceptedAfterPlayerJoinedAnotherClan()
    {
        WDClanData other = CreateOther(WDClanAdmission.Approval);
        clans.Join(other.Id, Stranger);
        string requestId = other.JoinRequests.Single().Id;
        clans.Join(home.Id, Stranger);
        Assert.That(clans.DecideJoin(other.Id, "WD-5582", requestId, true),
            Is.EqualTo(WDClanResult.NotFound));
        Assert.That(other.Member(Stranger), Is.Null);
    }

    [Test] public void AdmissionChangesOnAuthorityAndDoesNotAutoAcceptOldRequests()
    {
        WDClanData other = CreateOther(WDClanAdmission.Approval);
        clans.Join(other.Id, Stranger);
        Assert.That(clans.SetAdmission(other.Id, "WD-5582", WDClanAdmission.Open),
            Is.EqualTo(WDClanResult.Success));
        Assert.That(clans.Find(other.Id).Admission, Is.EqualTo(WDClanAdmission.Open));
        Assert.That(other.Member(Stranger), Is.Null);
        Assert.That(other.JoinRequests.Count, Is.EqualTo(1));
    }

    [Test] public void SearchUsesNameAndTagFromSameClanAuthority()
    {
        Assert.That(clans.Search("vanguard").Single(), Is.SameAs(home));
        Assert.That(clans.Search("vgd").Single(), Is.SameAs(home));
    }

    [Test] public void FutureCreationCostIsExplicitlyUnavailableWithoutEconomyAuthority()
    {
        WDClanRepository paid = new(profiles, catalog, armory, () => now,
            new WDClanCreationPolicy(100), seedMocks: false);
        Assert.That(paid.Create(Stranger, "Novo", "NVO", "", WDClanAdmission.Open, out _),
            Is.EqualTo(WDClanResult.Unavailable));
        Assert.That(paid.Clans, Is.Empty);
        Assert.That(armory.Load(Stranger).Coins, Is.Zero);
    }

    [Test] public void AvailabilityStatesNeverMasqueradeAsLiveClanSearch()
    {
        clans.SetAvailability(WDClanAvailability.Loading);
        Assert.That(clans.Search("Vanguard"), Is.Empty);
        clans.SetAvailability(WDClanAvailability.Offline);
        Assert.That(clans.Search("Vanguard"), Is.Empty);
        clans.SetAvailability(WDClanAvailability.Error);
        Assert.That(clans.Search("Vanguard"), Is.Empty);
        clans.SetAvailability(WDClanAvailability.Available);
        Assert.That(clans.Search("Vanguard").Count, Is.EqualTo(1));
    }

    // ============================================================
    // 02. CARGOS, TRANSFERÊNCIA, SAÍDA E EXCLUSÃO
    // ============================================================
    [Test] public void MemberAndOfficerCannotPerformCommanderAdministration()
    {
        Assert.That(clans.Promote(home.Id, Member, Stranger), Is.EqualTo(WDClanResult.Forbidden));
        Assert.That(clans.Promote(home.Id, Officer, Member), Is.EqualTo(WDClanResult.Forbidden));
        Assert.That(clans.SetAdmission(home.Id, Member, WDClanAdmission.Approval),
            Is.EqualTo(WDClanResult.Forbidden));
        Assert.That(clans.Edit(home.Id, Officer, "X", "X", "", ""),
            Is.EqualTo(WDClanResult.Forbidden));
    }

    [Test] public void CommanderPromotesMemberAndDemotesOfficer()
    {
        Assert.That(clans.Promote(home.Id, Local, Member), Is.EqualTo(WDClanResult.Success));
        Assert.That(home.Member(Member).Role, Is.EqualTo(WDClanRole.Officer));
        Assert.That(clans.Demote(home.Id, Local, Member), Is.EqualTo(WDClanResult.Success));
        Assert.That(home.Member(Member).Role, Is.EqualTo(WDClanRole.Member));
        Assert.That(clans.Promote(home.Id, Local, Local), Is.EqualTo(WDClanResult.Invalid));
    }

    [Test] public void CommandTransferIsAtomicAndOldCommanderBecomesOfficer()
    {
        Assert.That(clans.TransferCommand(home.Id, Local, Member), Is.EqualTo(WDClanResult.Success));
        Assert.That(home.Members.Count(item => item.Role == WDClanRole.Commander), Is.EqualTo(1));
        Assert.That(home.CommanderId, Is.EqualTo(Member));
        Assert.That(home.Member(Local).Role, Is.EqualTo(WDClanRole.Officer));
        Assert.That(home.Log.Last().Text, Does.Contain("Comando transferido"));
    }

    [TestCase(Member)]
    [TestCase(Officer)]
    public void NonCommanderCanLeaveWithoutPenalty(string playerId)
    {
        Assert.That(clans.Leave(playerId), Is.EqualTo(WDClanResult.Success));
        Assert.That(clans.ClanOf(playerId), Is.Null);
        Assert.That(profiles.Find(playerId).Clan, Is.Empty);
    }

    [Test] public void CommanderCannotLeaveEvenWhenAloneWithoutTransferOrDeletion()
    {
        Assert.That(clans.Leave(Local), Is.EqualTo(WDClanResult.Forbidden));
        clans.Kick(home.Id, Local, Officer); clans.Kick(home.Id, Local, Member);
        Assert.That(clans.Leave(Local), Is.EqualTo(WDClanResult.Forbidden));
        Assert.That(home.CommanderId, Is.EqualTo(Local));
    }

    [Test] public void OnlyCommanderCanDeleteAndDeletionClearsPrivateState()
    {
        clans.SetAdmission(home.Id, Local, WDClanAdmission.Approval);
        clans.Join(home.Id, Stranger);
        clans.RequestKick(home.Id, Officer, Member);
        clans.RequestTokens(Member, "w0", 1, out _);
        Assert.That(clans.Delete(home.Id, Officer), Is.EqualTo(WDClanResult.Forbidden));
        Assert.That(clans.Delete(home.Id, Local), Is.EqualTo(WDClanResult.Success));
        Assert.That(clans.Find(home.Id), Is.Null);
        Assert.That(clans.ClanOf(Member), Is.Null);
        Assert.That(home.JoinRequests, Is.Empty);
        Assert.That(home.KickRequests, Is.Empty);
        Assert.That(home.TokenRequests, Is.Empty);
        Assert.That(profiles.Find(Local).Clan, Is.Empty);
    }

    // ============================================================
    // 03. EXPULSÃO DIRETA E SOLICITADA
    // ============================================================
    [Test] public void CommanderCanKickEligibleMemberButNotSelf()
    {
        Assert.That(clans.Kick(home.Id, Local, Local), Is.EqualTo(WDClanResult.Invalid));
        Assert.That(clans.Kick(home.Id, Local, Member), Is.EqualTo(WDClanResult.Success));
        Assert.That(clans.ClanOf(Member), Is.Null);
        Assert.That(home.Log.Last().Text, Does.Contain("expulso"));
    }

    [Test] public void OfficerCannotKickDirectlyButCanRequestMemberKick()
    {
        Assert.That(clans.Kick(home.Id, Officer, Member), Is.EqualTo(WDClanResult.Forbidden));
        Assert.That(clans.RequestKick(home.Id, Officer, Member), Is.EqualTo(WDClanResult.Success));
        Assert.That(home.Member(Member), Is.Not.Null);
        Assert.That(home.KickRequests.Single().OfficerId, Is.EqualTo(Officer));
    }

    [TestCase(Local)]
    [TestCase(Officer)]
    public void OfficerCannotRequestKickOfCommanderOrAnotherOfficer(string target)
    {
        Assert.That(clans.RequestKick(home.Id, Officer, target), Is.EqualTo(WDClanResult.Invalid));
        Assert.That(home.KickRequests, Is.Empty);
    }

    [Test] public void DuplicateActiveKickRequestIsBlocked()
    {
        clans.RequestKick(home.Id, Officer, Member);
        Assert.That(clans.RequestKick(home.Id, Officer, Member), Is.EqualTo(WDClanResult.AlreadyPending));
        Assert.That(home.KickRequests.Count, Is.EqualTo(1));
    }

    [Test] public void CommanderApprovalRemovesTargetAndLogsOutcome()
    {
        clans.RequestKick(home.Id, Officer, Member);
        Assert.That(clans.DecideKick(home.Id, Local, home.KickRequests.Single().Id, true),
            Is.EqualTo(WDClanResult.Success));
        Assert.That(home.Member(Member), Is.Null);
        Assert.That(home.Log.Last().Text, Does.Contain("aprovada"));
    }

    [Test] public void CommanderRejectionKeepsTargetAndLogsOutcome()
    {
        clans.RequestKick(home.Id, Officer, Member);
        Assert.That(clans.DecideKick(home.Id, Local, home.KickRequests.Single().Id, false),
            Is.EqualTo(WDClanResult.Success));
        Assert.That(home.Member(Member), Is.Not.Null);
        Assert.That(home.Log.Last().Text, Does.Contain("rejeitada"));
    }

    // ============================================================
    // 04. TROCAS: TOKEN CANÔNICO, LIMITES UTC E CONSISTÊNCIA
    // ============================================================
    [Test] public void RequestAcceptsOnlyCanonicalWeaponTokens()
    {
        Assert.That(clans.RequestTokens(Member, "coins", 1, out _), Is.EqualTo(WDClanResult.Invalid));
        Assert.That(clans.RequestTokens(Member, "w0", 0, out _), Is.EqualTo(WDClanResult.Invalid));
        Assert.That(clans.RequestTokens(Member, "w0", 5, out _), Is.EqualTo(WDClanResult.Success));
    }

    [Test] public void RequestLimitIsPerPlayerPerUtcDayAndResetsNextDay()
    {
        Assert.That(clans.RequestTokens(Member, "w0", 2, out _), Is.EqualTo(WDClanResult.Success));
        Assert.That(clans.RequestTokens(Member, "w0", 2, out _), Is.EqualTo(WDClanResult.DailyLimit));
        Assert.That(clans.RequestTokens(Officer, "w0", 2, out _), Is.EqualTo(WDClanResult.Success));
        now = now.AddDays(1);
        Assert.That(clans.RequestTokens(Member, "w0", 2, out _), Is.EqualTo(WDClanResult.Success));
    }

    [Test] public void DonationTransfersOnlyTokensAndPreservesOwnershipLevelAndDeck()
    {
        WDArmoryProfile donor = armory.Load(Local);
        donor.GetDeck(0).SetMain(0, "w0");
        clans.RequestTokens(Member, "w0", 5, out WDClanTokenRequest request);
        Assert.That(clans.Donate(Local, request.Id), Is.EqualTo(WDClanResult.Success));
        Assert.That(donor.GetProgress("w0").Tokens, Is.EqualTo(25));
        Assert.That(armory.Load(Member).GetProgress("w0").Tokens, Is.EqualTo(9));
        Assert.That(donor.GetProgress("w0").Level, Is.EqualTo(2));
        Assert.That(armory.Load(Member).GetProgress("w0").Level, Is.EqualTo(2));
        Assert.That(donor.GetDeck(0).GetMain(0), Is.EqualTo("w0"));
        Assert.That(request.Fulfilled, Is.True);
        Assert.That(home.Log.Last().Text, Does.Contain("Doação concluída"));
    }

    [Test] public void DonationLimitIsIndependentFromRequestAndResetsNextUtcDay()
    {
        clans.RequestTokens(Member, "w0", 1, out WDClanTokenRequest first);
        Assert.That(clans.Donate(Local, first.Id), Is.EqualTo(WDClanResult.Success));
        Assert.That(clans.RequestTokens(Local, "w0", 1, out _), Is.EqualTo(WDClanResult.Success));
        clans.RequestTokens(Officer, "w0", 1, out WDClanTokenRequest second);
        Assert.That(clans.Donate(Local, second.Id), Is.EqualTo(WDClanResult.DailyLimit));
        now = now.AddDays(1);
        Assert.That(clans.Donate(Local, second.Id), Is.EqualTo(WDClanResult.Success));
    }

    [Test] public void DonorAndRecipientMustStillShareClan()
    {
        clans.RequestTokens(Member, "w0", 1, out WDClanTokenRequest request);
        Assert.That(clans.Donate(Stranger, request.Id), Is.EqualTo(WDClanResult.Forbidden));
        clans.Leave(Member);
        Assert.That(clans.Donate(Local, request.Id), Is.Not.EqualTo(WDClanResult.Success));
        Assert.That(armory.Load(Local).GetProgress("w0").Tokens, Is.EqualTo(30));
    }

    [Test] public void InsufficientTokensDoesNotConsumeDailyDonation()
    {
        clans.RequestTokens(Member, "w0", 31, out WDClanTokenRequest tooLarge);
        Assert.That(clans.Donate(Local, tooLarge.Id), Is.EqualTo(WDClanResult.InsufficientTokens));
        clans.RequestTokens(Officer, "w0", 1, out WDClanTokenRequest valid);
        Assert.That(clans.Donate(Local, valid.Id), Is.EqualTo(WDClanResult.Success));
        Assert.That(armory.Load(Local).GetProgress("w0").Tokens, Is.EqualTo(29));
    }

    [Test] public void RepeatedDonationCannotTransferTwiceOrConsumeTwice()
    {
        clans.RequestTokens(Member, "w0", 2, out WDClanTokenRequest request);
        Assert.That(clans.Donate(Local, request.Id), Is.EqualTo(WDClanResult.Success));
        int remaining = armory.Load(Local).GetProgress("w0").Tokens;
        Assert.That(clans.Donate(Local, request.Id), Is.EqualTo(WDClanResult.NotFound));
        Assert.That(armory.Load(Local).GetProgress("w0").Tokens, Is.EqualTo(remaining));
    }

    [Test] public void ClanChatUsesCurrentMembershipAndClosesOnExit()
    {
        WDSocialRepository social = new(profiles, Array.Empty<WDSocialMockSeed>());
        WDChatRepository chat = new(profiles, social);
        chat.SyncClanMembership(home);
        Assert.That(chat.ClanChannel.Id, Is.EqualTo($"clan:{home.Id}"));
        Assert.That(chat.ClanChannel.CanAccess(Local), Is.True);
        clans.TransferCommand(home.Id, Local, Officer);
        clans.Leave(Local);
        chat.SyncClanMembership(clans.LocalClan);
        Assert.That(chat.ClanChannel, Is.Null);
        Assert.That(chat.Channels.Single(item => item.Id == $"clan:{home.Id}").CanAccess(Local), Is.False);
    }

    [Test] public void ClanMembershipDoesNotBypassPrivateChatSocialRules()
    {
        WDSocialRepository social = new(profiles, Array.Empty<WDSocialMockSeed>());
        WDSettingsData settings = WDSettingsData.Defaults();
        settings.AllowNonFriendMessages = false;
        WDChatRepository chat = new(profiles, social, () => settings);
        chat.SyncClanMembership(home);
        Assert.That(home.Member(Member), Is.Not.Null);
        Assert.That(chat.OpenPrivate(Member), Is.Null);
    }

    [Test] public void InvalidDonationCannotChangeTokensOrDailyQuota()
    {
        clans.RequestTokens(Member, "w0", 3, out WDClanTokenRequest request);
        int donorBefore = armory.Load(Local).GetProgress("w0").Tokens;
        Assert.That(clans.Donate(Member, request.Id), Is.EqualTo(WDClanResult.Forbidden));
        Assert.That(armory.Load(Local).GetProgress("w0").Tokens, Is.EqualTo(donorBefore));
        Assert.That(clans.Donate(Local, request.Id), Is.EqualTo(WDClanResult.Success));
        Assert.That(armory.Load(Local).GetProgress("w0").Tokens, Is.EqualTo(donorBefore - 3));
    }

    // ============================================================
    // 05. APRESENTAÇÃO, CONFIRMAÇÕES E NAVEGAÇÃO COMPARTILHADA
    // ============================================================
    [Test] public void ClanPanelHasOneHomeDestinationAndTabsDoNotPushNavigation()
    {
        GameObject root = BuildPanel(out WDHomeClanPanel panel, out WDNavigationStack navigation);
        try
        {
            panel.Open();
            Assert.That(navigation.CurrentId, Is.EqualTo("clan"));
            Assert.That(navigation.Count, Is.EqualTo(2));
            Click(root, "Tab_Members"); Click(root, "Tab_Trades"); Click(root, "Tab_Log");
            Assert.That(navigation.Count, Is.EqualTo(2));
            Click(root, "Back");
            Assert.That(navigation.CurrentId, Is.EqualTo("home"));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test] public void ClanPanelWithoutMembershipShowsDiscoveryAndCreation()
    {
        clans.TransferCommand(home.Id, Local, Officer);
        clans.Leave(Local);
        GameObject root = BuildPanel(out WDHomeClanPanel panel, out _);
        try
        {
            panel.Open();
            Assert.That(panel.CurrentTab, Is.EqualTo(WDClanTab.Discover));
            Assert.That(root.transform.Find("ClanLayer/ClanContent/Surface/Search"), Is.Not.Null);
            Assert.That(root.transform.Find("ClanLayer/ClanContent/Surface/CreateClan"), Is.Not.Null);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test] public void ClanPanelShowsInlineErrorWithRetryAndOfflineState()
    {
        GameObject root = BuildPanel(out WDHomeClanPanel panel, out _);
        try
        {
            panel.Open();
            clans.SetAvailability(WDClanAvailability.Error);
            Assert.That(root.transform.Find("ClanLayer/ClanContent/Surface/Retry"), Is.Not.Null);
            Click(root, "Retry");
            Assert.That(clans.Availability, Is.EqualTo(WDClanAvailability.Available));
            clans.SetAvailability(WDClanAvailability.Offline);
            Assert.That(root.transform.Find("ClanLayer/ClanContent/Surface/Availability"), Is.Not.Null);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test] public void SearchAndSelectedClanInfoReturnToSameDiscoveryQuery()
    {
        WDClanData other = CreateOther();
        clans.TransferCommand(home.Id, Local, Officer); clans.Leave(Local);
        GameObject root = BuildPanel(out WDHomeClanPanel panel, out _);
        try
        {
            panel.Open();
            TMP_InputField search = root.transform.Find("ClanLayer/ClanContent/Surface/Search")
                .GetComponent<TMP_InputField>();
            search.text = "IRON"; Click(root, "SearchButton");
            Assert.That(panel.SearchQuery, Is.EqualTo("IRON"));
            Click(root, $"Info_{other.Id}");
            Assert.That(panel.SelectedClanId, Is.EqualTo(other.Id));
            Assert.That(panel.TryHandleLocalBack(), Is.True);
            Assert.That(panel.SearchQuery, Is.EqualTo("IRON"));
            Assert.That(panel.SelectedClanId, Is.Null);
            Assert.That(root.transform.Find("ClanLayer/ClanContent/Surface/Search")
                .GetComponent<TMP_InputField>().text, Is.EqualTo("IRON"));
            Assert.That(root.transform.Find($"ClanLayer/ClanContent/Surface/Info_{other.Id}"),
                Is.Not.Null);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test] public void DiscoveryCanCreateAFreeClanThroughTheSharedRepository()
    {
        clans.TransferCommand(home.Id, Local, Officer); clans.Leave(Local);
        GameObject root = BuildPanel(out WDHomeClanPanel panel, out _);
        try
        {
            panel.Open();
            root.transform.Find("ClanLayer/ClanContent/Surface/CreateName")
                .GetComponent<TMP_InputField>().text = "Novo Clã";
            root.transform.Find("ClanLayer/ClanContent/Surface/CreateTag")
                .GetComponent<TMP_InputField>().text = "NOV";
            Click(root, "CreateClan");
            Assert.That(clans.LocalClan.Name, Is.EqualTo("Novo Clã"));
            Assert.That(clans.LocalClan.Member(Local).Role, Is.EqualTo(WDClanRole.Commander));
            Assert.That(panel.CurrentTab, Is.EqualTo(WDClanTab.Overview));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test] public void ApprovalJoinActionBecomesSentWithoutGrantingMembership()
    {
        WDClanData approval = CreateOther(WDClanAdmission.Approval);
        clans.TransferCommand(home.Id, Local, Officer); clans.Leave(Local);
        GameObject root = BuildPanel(out WDHomeClanPanel panel, out _);
        try
        {
            panel.Open(); Click(root, $"Info_{approval.Id}"); Click(root, "JoinClan");
            Assert.That(clans.LocalClan, Is.Null);
            Assert.That(approval.JoinRequests.Any(item => item.PlayerId == Local), Is.True);
            Assert.That(root.transform.Find("ClanLayer/ClanContent/Surface/JoinClan")
                .GetComponentInChildren<Button>().interactable, Is.False);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test] public void TradeDonationButtonUsesConfirmationAndCanonicalTokenAuthority()
    {
        clans.RequestTokens(Member, "w0", 2, out WDClanTokenRequest request);
        Action pending = null;
        GameObject root = BuildPanel(out WDHomeClanPanel panel, out _,
            (title, body, action) => pending = action);
        try
        {
            panel.Open(); Click(root, "Tab_Trades");
            Assert.That(root.transform.Find("ClanLayer/ClanContent/Surface/RequestTokens"), Is.Not.Null);
            Click(root, $"Donate_{request.Id}");
            Assert.That(armory.Load(Local).GetProgress("w0").Tokens, Is.EqualTo(30));
            pending();
            Assert.That(armory.Load(Local).GetProgress("w0").Tokens, Is.EqualTo(28));
            Assert.That(armory.Load(Member).GetProgress("w0").Tokens, Is.EqualTo(6));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test] public void CommanderActionsAppearButOfficerNeverGetsDirectKick()
    {
        GameObject root = BuildPanel(out WDHomeClanPanel panel, out _);
        try
        {
            panel.Open(); Click(root, "Tab_Members");
            Assert.That(root.transform.Find("ClanLayer/ClanContent/Surface/Kick_WD-7105"), Is.Not.Null);
            clans.TransferCommand(home.Id, Local, Officer);
            Assert.That(root.transform.Find("ClanLayer/ClanContent/Surface/Kick_WD-7105"), Is.Null);
            Assert.That(root.transform.Find("ClanLayer/ClanContent/Surface/RequestKick_WD-7105"), Is.Not.Null);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test] public void MemberDoesNotSeeAdministrativeActions()
    {
        clans.TransferCommand(home.Id, Local, Officer);
        clans.Demote(home.Id, Officer, Local);
        GameObject root = BuildPanel(out WDHomeClanPanel panel, out _);
        try
        {
            panel.Open(); Click(root, "Tab_Members");
            Assert.That(root.transform.Find("ClanLayer/ClanContent/Surface/Kick_WD-7105"), Is.Null);
            Assert.That(root.transform.Find("ClanLayer/ClanContent/Surface/RequestKick_WD-7105"), Is.Null);
            Assert.That(root.transform.Find("ClanLayer/ClanContent/Surface/Profile_WD-7105"), Is.Not.Null);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test] public void PublicProfileBackRestoresSameMembersTabAndTopVisualLayer()
    {
        GameObject root = BuildPanel(out WDHomeClanPanel panel, out WDNavigationStack navigation,
            out WDHomeProfilePanel profilePanel);
        try
        {
            panel.Open(); Click(root, "Tab_Members");
            Click(root, "Profile_WD-7105");
            Assert.That(profilePanel.CurrentView, Is.EqualTo(WDProfileView.PublicProfile));
            Assert.That(navigation.CurrentId, Is.EqualTo("profile/public/WD-7105"));
            navigation.TryBack();
            Assert.That(navigation.CurrentId, Is.EqualTo("clan"));
            Assert.That(panel.CurrentTab, Is.EqualTo(WDClanTab.Members));
            Assert.That(panel.transform.GetSiblingIndex(),
                Is.EqualTo(panel.transform.parent.childCount - 1));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test] public void LeaveAndDeleteNeedExplicitConfirmationBeforeMutation()
    {
        Action pending = null;
        clans.TransferCommand(home.Id, Local, Officer);
        GameObject root = BuildPanel(out WDHomeClanPanel panel, out _,
            (title, body, action) => pending = action);
        try
        {
            panel.Open(); Click(root, "LeaveClan");
            Assert.That(clans.LocalClan, Is.Not.Null);
            Assert.That(pending, Is.Not.Null);
            pending();
            Assert.That(clans.LocalClan, Is.Null);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test] public void CommanderDeletionRequiresTwoSeparateConfirmations()
    {
        Action pending = null;
        GameObject root = BuildPanel(out WDHomeClanPanel panel, out _,
            (title, body, action) => pending = action);
        try
        {
            panel.Open(); Click(root, "DeleteClan");
            Assert.That(clans.LocalClan, Is.Not.Null);
            Action first = pending; first();
            Assert.That(clans.LocalClan, Is.Not.Null);
            Assert.That(pending, Is.Not.SameAs(first));
            pending();
            Assert.That(clans.LocalClan, Is.Null);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private WDClanData CreateOther(WDClanAdmission mode = WDClanAdmission.Open)
    {
        Assert.That(clans.Create("WD-5582", "Iron Dominion", "IRON", "", mode,
            out WDClanData other), Is.EqualTo(WDClanResult.Success));
        return other;
    }

    private GameObject BuildPanel(out WDHomeClanPanel panel, out WDNavigationStack navigation,
        Action<string, string, Action> confirmation = null)
    {
        GameObject root = new("ClanTestRoot", typeof(RectTransform));
        RectTransform layer = WDHomeUIFactory.Rect("ClanLayer", root.transform);
        panel = layer.gameObject.AddComponent<WDHomeClanPanel>();
        navigation = new WDNavigationStack();
        WDHomeClanPanel captured = panel;
        navigation.SetRoot(new WDNavigationContext("home", _ => captured.HideFromNavigation()));
        panel.Build(Theme(), clans, profiles,
            new WDSocialRepository(profiles, Array.Empty<WDSocialMockSeed>()),
            navigation, playerId => { }, playerId => { },
            confirmation ?? ((title, body, action) => { }));
        return root;
    }

    private GameObject BuildPanel(out WDHomeClanPanel panel, out WDNavigationStack navigation,
        out WDHomeProfilePanel profilePanel)
    {
        GameObject root = new("ClanTestRoot", typeof(RectTransform));
        RectTransform clanLayer = WDHomeUIFactory.Rect("ClanLayer", root.transform);
        RectTransform profileLayer = WDHomeUIFactory.Rect("ProfileLayer", root.transform);
        panel = clanLayer.gameObject.AddComponent<WDHomeClanPanel>();
        profilePanel = profileLayer.gameObject.AddComponent<WDHomeProfilePanel>();
        navigation = new WDNavigationStack();
        WDHomeClanPanel captured = panel;
        navigation.SetRoot(new WDNavigationContext("home", _ => captured.HideFromNavigation()));
        WDSocialRepository social = new(profiles, Array.Empty<WDSocialMockSeed>());
        profilePanel.Build(Theme(), profiles, social, navigation,
            (playerId, matchId) => { }, () => { });
        panel.Build(Theme(), clans, profiles, social, navigation,
            profilePanel.OpenPublicProfile, playerId => { },
            (title, body, action) => { });
        return root;
    }

    private static void Click(GameObject root, string name) =>
        root.GetComponentsInChildren<Button>(true).First(button => button.name == name).onClick.Invoke();
    private static WarDominionUITheme Theme() =>
        Resources.Load<WarDominionUITheme>("UI/WarDominionUITheme");

    private sealed class MemoryArmory : IWDClanArmoryProfiles
    {
        private readonly Dictionary<string, WDArmoryProfile> profiles = new();
        public WDArmoryProfile Load(string playerId)
        {
            if (!profiles.TryGetValue(playerId, out WDArmoryProfile value))
            { value = new WDArmoryProfile(); profiles[playerId] = value; }
            return value;
        }
        public void Save(string playerId, WDArmoryProfile value) => profiles[playerId] = value;
    }
}
