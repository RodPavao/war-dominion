using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class WDClanTests
{
    private const string General = "WD-4821";
    private const string Officer = "WD-2847";
    private const string Member = "WD-7105";
    private const string Stranger = "WD-3906";
    private const string OtherFounder = "WD-5582";

    private WDProfileRepository profiles;
    private WDArmoryCatalogConfig catalog;
    private MemoryArmory armory;
    private WDClanRepository clans;
    private WDClanData home;
    private DateTimeOffset now;

    [SetUp]
    public void SetUp()
    {
        profiles = new WDProfileRepository(null, new WDMemoryProfilePrivacyStore());
        catalog = ScriptableObject.CreateInstance<WDArmoryCatalogConfig>();
        catalog.Configure(new[] { new WDWeaponCategoryDefinition("support", "Apoio", true) },
            new[] { new WDWeaponDefinition("w0", "Radar", "support",
                new[] { new WDWeaponLevelDefinition(2, 10, 20) }) });
        armory = new MemoryArmory();
        foreach (string id in new[] { General, Officer, Member, Stranger, OtherFounder })
        {
            WDArmoryProfile profile = armory.Load(id);
            profile.SetCurrencies(0, 100);
            profile.AddProgress(new WDWeaponProgress("w0", true, 2,
                id == General ? 30 : 4));
        }
        now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        clans = new WDClanRepository(profiles, catalog, armory, () => now,
            seedMocks: false);
        Assert.That(clans.Create(General, "Vanguard Elite", "VGD", "Teste",
            WDClanAdmission.Open, out home), Is.EqualTo(WDClanResult.Success));
        Assert.That(clans.Join(home.Id, Officer), Is.EqualTo(WDClanResult.Success));
        Assert.That(clans.Join(home.Id, Member), Is.EqualTo(WDClanResult.Success));
        Assert.That(clans.Promote(home.Id, General, Officer),
            Is.EqualTo(WDClanResult.Success));
    }

    [TearDown]
    public void TearDown() => UnityEngine.Object.DestroyImmediate(catalog);

    // ============================================================
    // 01. CRIAÇÃO, ECONOMIA E MEMBERSHIP
    // ============================================================
    [Test]
    public void FounderCreatesClanAsTheOnlyGeneralAndPaysTenEmeralds()
    {
        Assert.That(clans.CreationPolicy.EmeraldCost,
            Is.EqualTo(WDClanCreationPolicy.CanonicalEmeraldCost));
        Assert.That(home.Member(General).Role, Is.EqualTo(WDClanRole.General));
        Assert.That(home.Members.Count(item => item.Role == WDClanRole.General),
            Is.EqualTo(1));
        Assert.That(armory.Load(General).Emeralds, Is.EqualTo(90));
    }

    [Test]
    public void CreationFailsWithoutEmeraldsAndDoesNotCreatePartialClan()
    {
        armory.Load(Stranger).SetCurrencies(0, 9);
        Assert.That(clans.Create(Stranger, "Novo Clã", "NOV", "",
            WDClanAdmission.Open, out WDClanData created),
            Is.EqualTo(WDClanResult.InsufficientEmeralds));
        Assert.That(created, Is.Null);
        Assert.That(clans.ClanOf(Stranger), Is.Null);
        Assert.That(armory.Load(Stranger).Emeralds, Is.EqualTo(9));
    }

    [Test]
    public void CreationWithoutEconomyAuthorityIsExplicitlyUnavailable()
    {
        WDClanRepository repository = new(profiles, catalog,
            new ArmoryWithoutEconomy(), () => now, seedMocks: false);
        Assert.That(repository.Create(Stranger, "Novo Clã", "NOV", "",
            WDClanAdmission.Open, out _), Is.EqualTo(WDClanResult.Unavailable));
    }

    [Test]
    public void ClanIdentityUsesCanonicalLengthContracts()
    {
        Assert.That(clans.Create(Stranger, "AB", "OK", "", WDClanAdmission.Open, out _),
            Is.EqualTo(WDClanResult.Invalid));
        Assert.That(clans.Create(Stranger, new string('N', 31), "OK", "",
            WDClanAdmission.Open, out _), Is.EqualTo(WDClanResult.Invalid));
        Assert.That(clans.Create(Stranger, "Nome", "X", "", WDClanAdmission.Open, out _),
            Is.EqualTo(WDClanResult.Invalid));
        Assert.That(clans.Create(Stranger, "Nome", new string('T', 11), "",
            WDClanAdmission.Open, out _), Is.EqualTo(WDClanResult.Invalid));
    }

    [Test]
    public void APlayerBelongsToOnlyOneClan()
    {
        WDClanData other = CreateOther();
        Assert.That(clans.Join(other.Id, Member), Is.EqualTo(WDClanResult.AlreadyMember));
        Assert.That(clans.Create(Member, "Terceiro", "TRC", "",
            WDClanAdmission.Open, out _), Is.EqualTo(WDClanResult.AlreadyMember));
        Assert.That(clans.ClanOf(Member), Is.SameAs(home));
    }

    // ============================================================
    // 02. PERMISSÕES E ENTRADA
    // ============================================================
    [TestCase(WDClanRole.General, true)]
    [TestCase(WDClanRole.Commander, true)]
    [TestCase(WDClanRole.Officer, false)]
    [TestCase(WDClanRole.Member, false)]
    public void JoinReviewPermissionMatchesCanonicalMatrix(WDClanRole role, bool expected)
    {
        Assert.That(WDClanPermissions.Can(role,
            WDClanPermission.ReviewJoinRequests), Is.EqualTo(expected));
    }

    [TestCase(WDClanRole.General, true)]
    [TestCase(WDClanRole.Commander, true)]
    [TestCase(WDClanRole.Officer, true)]
    [TestCase(WDClanRole.Member, false)]
    public void MessageOfDayPermissionMatchesCanonicalMatrix(WDClanRole role, bool expected)
    {
        Assert.That(WDClanPermissions.Can(role,
            WDClanPermission.EditMessageOfDay), Is.EqualTo(expected));
    }

    [Test]
    public void GeneralCommanderAndOfficerEditMessageButMemberCannot()
    {
        Assert.That(clans.SetMessageOfDay(home.Id, General, "G"),
            Is.EqualTo(WDClanResult.Success));
        PromoteOfficerToCommander(Officer);
        Assert.That(clans.SetMessageOfDay(home.Id, Officer, "C"),
            Is.EqualTo(WDClanResult.Success));
        clans.Promote(home.Id, General, Member);
        Assert.That(clans.SetMessageOfDay(home.Id, Member, "O"),
            Is.EqualTo(WDClanResult.Success));
        clans.Demote(home.Id, General, Member);
        Assert.That(clans.SetMessageOfDay(home.Id, Member, "M"),
            Is.EqualTo(WDClanResult.Forbidden));
        Assert.That(home.MessageOfDay, Is.EqualTo("O"));
    }

    [Test]
    public void GeneralAndCommanderReviewJoinRequests()
    {
        WDClanData approval = CreateOther(WDClanAdmission.Approval);
        clans.Join(approval.Id, Stranger);
        string request = approval.JoinRequests.Single().Id;
        Assert.That(clans.DecideJoin(approval.Id, OtherFounder, request, false),
            Is.EqualTo(WDClanResult.Success));

        PromoteOfficerToCommander(Officer);
        clans.SetAdmission(home.Id, General, WDClanAdmission.Approval);
        clans.Join(home.Id, Stranger);
        Assert.That(clans.DecideJoin(home.Id, Officer,
            home.JoinRequests.Single().Id, true), Is.EqualTo(WDClanResult.Success));
    }

    [Test]
    public void OfficerAndMemberCannotReviewJoinRequests()
    {
        clans.SetAdmission(home.Id, General, WDClanAdmission.Approval);
        clans.Join(home.Id, Stranger);
        string request = home.JoinRequests.Single().Id;
        Assert.That(clans.DecideJoin(home.Id, Officer, request, true),
            Is.EqualTo(WDClanResult.Forbidden));
        Assert.That(clans.DecideJoin(home.Id, Member, request, true),
            Is.EqualTo(WDClanResult.Forbidden));
    }

    // ============================================================
    // 03. COMMANDERS, ANTIGUIDADE E LIMITE
    // ============================================================
    [Test]
    public void GeneralPromotesOfficerAndRecordsCommanderSinceUtc()
    {
        Assert.That(clans.PromoteToCommander(home.Id, General, Officer),
            Is.EqualTo(WDClanResult.Success));
        Assert.That(home.Member(Officer).Role, Is.EqualTo(WDClanRole.Commander));
        Assert.That(home.Member(Officer).CommanderSinceUtc, Is.EqualTo(now));
    }

    [Test]
    public void ThirdCommanderIsRejectedWithoutReplacingAnyone()
    {
        PromoteOfficerToCommander(Officer);
        clans.Promote(home.Id, General, Member);
        now = now.AddMinutes(1);
        Assert.That(clans.PromoteToCommander(home.Id, General, Member),
            Is.EqualTo(WDClanResult.Success));
        clans.Join(home.Id, Stranger);
        clans.Promote(home.Id, General, Stranger);
        Assert.That(clans.PromoteToCommander(home.Id, General, Stranger),
            Is.EqualTo(WDClanResult.CommanderLimit));
        Assert.That(home.Commanders.Count, Is.EqualTo(2));
        Assert.That(home.Member(Stranger).Role, Is.EqualTo(WDClanRole.Officer));
    }

    [Test]
    public void CommanderCannotPromoteOrManageAnotherCommanderOrGeneral()
    {
        PromoteOfficerToCommander(Officer);
        clans.Promote(home.Id, General, Member);
        Assert.That(clans.PromoteToCommander(home.Id, Officer, Member),
            Is.EqualTo(WDClanResult.Forbidden));
        Assert.That(clans.DemoteCommander(home.Id, Officer, Officer),
            Is.EqualTo(WDClanResult.Forbidden));
        Assert.That(clans.Kick(home.Id, Officer, General),
            Is.EqualTo(WDClanResult.Invalid));
    }

    [Test]
    public void GeneralCanDemoteAndRemoveCommander()
    {
        PromoteOfficerToCommander(Officer);
        Assert.That(clans.DemoteCommander(home.Id, General, Officer),
            Is.EqualTo(WDClanResult.Success));
        Assert.That(home.Member(Officer).Role, Is.EqualTo(WDClanRole.Officer));
        Assert.That(home.Member(Officer).CommanderSinceUtc, Is.Null);
        PromoteOfficerToCommander(Officer);
        Assert.That(clans.Kick(home.Id, General, Officer),
            Is.EqualTo(WDClanResult.Success));
        Assert.That(home.Member(Officer), Is.Null);
    }

    [Test]
    public void RePromotedCommanderReceivesNewSeniority()
    {
        PromoteOfficerToCommander(Officer);
        DateTimeOffset first = home.Member(Officer).CommanderSinceUtc.Value;
        clans.DemoteCommander(home.Id, General, Officer);
        now = now.AddHours(4);
        PromoteOfficerToCommander(Officer);
        Assert.That(home.Member(Officer).CommanderSinceUtc, Is.EqualTo(now));
        Assert.That(home.Member(Officer).CommanderSinceUtc, Is.GreaterThan(first));
    }

    // ============================================================
    // 04. TRANSFERÊNCIA E SUCESSÃO
    // ============================================================
    [Test]
    public void ManualTransferAcceptsOnlyCommanderAndRemovesOldGeneral()
    {
        Assert.That(clans.TransferLeadership(home.Id, General, Officer),
            Is.EqualTo(WDClanResult.Invalid));
        PromoteOfficerToCommander(Officer);
        Assert.That(clans.TransferLeadership(home.Id, General, Officer),
            Is.EqualTo(WDClanResult.Success));
        Assert.That(home.GeneralId, Is.EqualTo(Officer));
        Assert.That(home.Members.Count(item => item.Role == WDClanRole.General),
            Is.EqualTo(1));
        Assert.That(home.Member(General), Is.Null);
        Assert.That(clans.ClanOf(General), Is.Null);
    }

    [Test]
    public void AutomaticSuccessionChoosesOldestCommanderByCommanderSinceUtc()
    {
        clans.Promote(home.Id, General, Member);
        PromoteOfficerToCommander(Member);
        now = now.AddDays(1);
        PromoteOfficerToCommander(Officer);
        Assert.That(clans.LeaveAsGeneral(home.Id, General, out string successor),
            Is.EqualTo(WDClanResult.Success));
        Assert.That(successor, Is.EqualTo(Member));
        Assert.That(home.GeneralId, Is.EqualTo(Member));
        Assert.That(home.Member(Officer).Role, Is.EqualTo(WDClanRole.Commander));
        Assert.That(home.Member(General), Is.Null);
    }

    [Test]
    public void EqualSeniorityUsesDocumentedDeterministicFallback()
    {
        PromoteOfficerToCommander(Officer);
        clans.Promote(home.Id, General, Member);
        PromoteOfficerToCommander(Member);
        string expected = new[] { Officer, Member }.OrderBy(id => id,
            StringComparer.Ordinal).First();
        Assert.That(clans.OldestCommander(home).PlayerId, Is.EqualTo(expected));
    }

    [Test]
    public void GeneralCannotLeaveWithoutCommanderOrBypassSuccession()
    {
        Assert.That(clans.Leave(General), Is.EqualTo(WDClanResult.RequiresSuccessor));
        Assert.That(clans.LeaveAsGeneral(home.Id, General, out string successor),
            Is.EqualTo(WDClanResult.RequiresSuccessor));
        Assert.That(successor, Is.Null);
        Assert.That(home.GeneralId, Is.EqualTo(General));
    }

    [Test]
    public void NonGeneralLeavesNormally()
    {
        Assert.That(clans.Leave(Member), Is.EqualTo(WDClanResult.Success));
        Assert.That(clans.ClanOf(Member), Is.Null);
        Assert.That(profiles.Find(Member).Clan, Is.Empty);
    }

    // ============================================================
    // 05. EXPULSÃO E EXCLUSÃO
    // ============================================================
    [Test]
    public void OfficerRequestsRemovalAndCommanderApproves()
    {
        Assert.That(clans.RequestKick(home.Id, Officer, Member),
            Is.EqualTo(WDClanResult.Success));
        PromoteOfficerToCommander(Officer);
        Assert.That(clans.DecideKick(home.Id, Officer,
            home.KickRequests.Single().Id, true), Is.EqualTo(WDClanResult.Success));
        Assert.That(home.Member(Member), Is.Null);
    }

    [Test]
    public void MemberCannotRequestRemoval()
    {
        Assert.That(clans.RequestKick(home.Id, Member, Officer),
            Is.EqualTo(WDClanResult.Forbidden));
    }

    [Test]
    public void OnlyGeneralDeletesClan()
    {
        PromoteOfficerToCommander(Officer);
        Assert.That(clans.Delete(home.Id, Officer), Is.EqualTo(WDClanResult.Forbidden));
        Assert.That(clans.Delete(home.Id, General), Is.EqualTo(WDClanResult.Success));
        Assert.That(clans.Find(home.Id), Is.Null);
        Assert.That(clans.ClanOf(Member), Is.Null);
    }

    // ============================================================
    // 06. TOKENS DE ARMAS SEM LIMITE DIÁRIO INVENTADO
    // ============================================================
    [Test]
    public void TokenRequestsHaveNoMandatoryDailyLimit()
    {
        Assert.That(clans.RequestTokens(Member, "w0", 1, out _),
            Is.EqualTo(WDClanResult.Success));
        Assert.That(clans.RequestTokens(Member, "w0", 1, out _),
            Is.EqualTo(WDClanResult.Success));
        Assert.That(clans.RequestTokens(Member, "w0", 1, out _),
            Is.EqualTo(WDClanResult.Success));
    }

    [Test]
    public void MultipleDonationsOnSameDayRemainAllowed()
    {
        clans.RequestTokens(Member, "w0", 1, out WDClanTokenRequest first);
        clans.RequestTokens(Officer, "w0", 1, out WDClanTokenRequest second);
        Assert.That(clans.Donate(General, first.Id), Is.EqualTo(WDClanResult.Success));
        Assert.That(clans.Donate(General, second.Id), Is.EqualTo(WDClanResult.Success));
        Assert.That(armory.Load(General).GetProgress("w0").Tokens, Is.EqualTo(28));
    }

    [Test]
    public void DonationMovesOnlyCanonicalWeaponTokens()
    {
        WDArmoryProfile donor = armory.Load(General);
        donor.GetDeck(0).SetMain(0, "w0");
        clans.RequestTokens(Member, "w0", 3, out WDClanTokenRequest request);
        Assert.That(clans.Donate(General, request.Id), Is.EqualTo(WDClanResult.Success));
        Assert.That(donor.GetProgress("w0").Tokens, Is.EqualTo(27));
        Assert.That(armory.Load(Member).GetProgress("w0").Tokens, Is.EqualTo(7));
        Assert.That(donor.GetProgress("w0").Level, Is.EqualTo(2));
        Assert.That(donor.GetDeck(0).GetMain(0), Is.EqualTo("w0"));
    }

    // ============================================================
    // 07. INTEGRAÇÕES E APRESENTAÇÃO EXISTENTE
    // ============================================================
    [Test]
    public void ClanChatClosesWhenFormerGeneralLeavesThroughSuccession()
    {
        PromoteOfficerToCommander(Officer);
        WDSocialRepository social = new(profiles, Array.Empty<WDSocialMockSeed>());
        WDChatRepository chat = new(profiles, social);
        chat.SyncClanMembership(home);
        clans.LeaveAsGeneral(home.Id, General, out _);
        chat.SyncClanMembership(clans.LocalClan);
        Assert.That(chat.ClanChannel, Is.Null);
        Assert.That(chat.Channels.Single(item => item.Id == $"clan:{home.Id}")
            .CanAccess(General), Is.False);
    }

    [Test]
    public void ClanPanelKeepsOneNavigationDestination()
    {
        GameObject root = BuildPanel(out WDHomeClanPanel panel,
            out WDNavigationStack navigation);
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

    [Test]
    public void ClanPanelWithoutMembershipShowsDiscoveryAndEmeraldCreation()
    {
        PromoteOfficerToCommander(Officer);
        clans.TransferLeadership(home.Id, General, Officer);
        GameObject root = BuildPanel(out WDHomeClanPanel panel, out _);
        try
        {
            panel.Open();
            Assert.That(panel.CurrentTab, Is.EqualTo(WDClanTab.Discover));
            Transform surface = root.transform.Find("ClanLayer/ClanContent/Surface");
            Assert.That(surface.Find("Search"), Is.Not.Null);
            Assert.That(surface.Find("CreateClan"), Is.Not.Null);
            Assert.That(surface.Find("CreateTitle").GetComponent<TMP_Text>().text,
                Does.Contain("ESMERALDAS"));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    // ============================================================
    // 08. HELPERS E PORTAS DE TESTE
    // ============================================================
    private void PromoteOfficerToCommander(string playerId)
    {
        Assert.That(clans.PromoteToCommander(home.Id, General, playerId),
            Is.EqualTo(WDClanResult.Success));
    }

    private WDClanData CreateOther(WDClanAdmission mode = WDClanAdmission.Open)
    {
        Assert.That(clans.Create(OtherFounder, "Iron Dominion", "IRON", "", mode,
            out WDClanData other), Is.EqualTo(WDClanResult.Success));
        return other;
    }

    private GameObject BuildPanel(out WDHomeClanPanel panel,
        out WDNavigationStack navigation)
    {
        GameObject root = new("ClanTestRoot", typeof(RectTransform));
        RectTransform layer = WDHomeUIFactory.Rect("ClanLayer", root.transform);
        panel = layer.gameObject.AddComponent<WDHomeClanPanel>();
        navigation = new WDNavigationStack();
        WDHomeClanPanel captured = panel;
        navigation.SetRoot(new WDNavigationContext("home",
            _ => captured.HideFromNavigation()));
        panel.Build(Theme(), clans, profiles,
            new WDSocialRepository(profiles, Array.Empty<WDSocialMockSeed>()),
            navigation, playerId => { }, playerId => { },
            (title, body, action) => { });
        return root;
    }

    private static void Click(GameObject root, string name) =>
        root.GetComponentsInChildren<Button>(true).First(button =>
            button.name == name).onClick.Invoke();

    private static WarDominionUITheme Theme() =>
        Resources.Load<WarDominionUITheme>("UI/WarDominionUITheme");

    private sealed class MemoryArmory : IWDClanArmoryProfiles, IWDClanEconomy
    {
        private readonly Dictionary<string, WDArmoryProfile> values = new();
        public WDArmoryProfile Load(string playerId)
        {
            if (!values.TryGetValue(playerId, out WDArmoryProfile value))
            { value = new WDArmoryProfile(); values[playerId] = value; }
            return value;
        }

        public void Save(string playerId, WDArmoryProfile value) => values[playerId] = value;

        public bool TrySpendEmeralds(string playerId, int amount)
        {
            WDArmoryProfile value = Load(playerId);
            if (!value.SpendEmeralds(amount)) return false;
            Save(playerId, value); return true;
        }

        public void RefundEmeralds(string playerId, int amount)
        {
            WDArmoryProfile value = Load(playerId);
            value.SetCurrencies(value.Coins, value.Emeralds + amount);
            Save(playerId, value);
        }
    }

    private sealed class ArmoryWithoutEconomy : IWDClanArmoryProfiles
    {
        public WDArmoryProfile Load(string playerId) => new();
        public void Save(string playerId, WDArmoryProfile profile) { }
    }
}
