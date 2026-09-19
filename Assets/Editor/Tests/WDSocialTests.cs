using System;
using NUnit.Framework;

public sealed class WDSocialTests
{
    private static WDProfileRepository Profiles() => new(null, new WDMemoryProfilePrivacyStore());

    private static WDSocialRepository Social(WDProfileRepository profiles,
        string playerId, WDProfileSocialState state) => new(profiles, new[]
        {
            new WDSocialMockSeed(playerId, state, WDSocialPresence.Online)
        });

    // ============================================================
    // 01. TRANSIÇÕES CANÔNICAS
    // ============================================================

    [Test]
    public void StrangerBecomesRequestSent()
    {
        WDProfileRepository profiles = Profiles();
        WDSocialRepository social = Social(profiles, "WD-2847", WDProfileSocialState.Stranger);
        Assert.That(social.SendFriendRequest("WD-2847"), Is.True);
        Assert.That(profiles.Find("WD-2847").SocialState, Is.EqualTo(WDProfileSocialState.RequestSent));
    }

    [Test]
    public void AcceptReceivedCreatesFriendAndReducesCounter()
    {
        WDProfileRepository profiles = Profiles();
        WDSocialRepository social = Social(profiles, "WD-2847", WDProfileSocialState.RequestReceived);
        Assert.That(social.PendingReceivedCount, Is.EqualTo(1));
        Assert.That(social.AcceptReceived("WD-2847"), Is.True);
        Assert.That(social.PendingReceivedCount, Is.Zero);
        Assert.That(profiles.Find("WD-2847").SocialState, Is.EqualTo(WDProfileSocialState.Friend));
    }

    [Test]
    public void DeclineReceivedReturnsToStranger()
    {
        WDProfileRepository profiles = Profiles();
        WDSocialRepository social = Social(profiles, "WD-2847", WDProfileSocialState.RequestReceived);
        social.DeclineReceived("WD-2847");
        Assert.That(profiles.Find("WD-2847").SocialState, Is.EqualTo(WDProfileSocialState.Stranger));
    }

    [Test]
    public void RemoveFriendReturnsToStranger()
    {
        WDProfileRepository profiles = Profiles();
        WDSocialRepository social = Social(profiles, "WD-2847", WDProfileSocialState.Friend);
        social.RemoveFriend("WD-2847");
        Assert.That(profiles.Find("WD-2847").SocialState, Is.EqualTo(WDProfileSocialState.Stranger));
    }

    [TestCase(WDProfileSocialState.Friend)]
    [TestCase(WDProfileSocialState.Stranger)]
    [TestCase(WDProfileSocialState.RequestSent)]
    [TestCase(WDProfileSocialState.RequestReceived)]
    public void BlockAlwaysProducesOnlyBlockedState(WDProfileSocialState initial)
    {
        WDProfileRepository profiles = Profiles();
        WDSocialRepository social = Social(profiles, "WD-2847", initial);
        Assert.That(social.Block("WD-2847"), Is.True);
        Assert.That(profiles.Find("WD-2847").SocialState, Is.EqualTo(WDProfileSocialState.Blocked));
        Assert.That(social.Friends(), Is.Empty);
        Assert.That(social.ReceivedRequests(), Is.Empty);
    }

    [Test]
    public void UnblockReturnsToStrangerWithoutRestoringFriendship()
    {
        WDProfileRepository profiles = Profiles();
        WDSocialRepository social = Social(profiles, "WD-2847", WDProfileSocialState.Blocked);
        social.Unblock("WD-2847");
        Assert.That(profiles.Find("WD-2847").SocialState, Is.EqualTo(WDProfileSocialState.Stranger));
        Assert.That(social.Friends(), Is.Empty);
    }

    [Test]
    public void SentRequestCannotDuplicate()
    {
        WDProfileRepository profiles = Profiles();
        WDSocialRepository social = Social(profiles, "WD-2847", WDProfileSocialState.Stranger);
        Assert.That(social.SendFriendRequest("WD-2847"), Is.True);
        Assert.That(social.SendFriendRequest("WD-2847"), Is.False);
    }

    [Test]
    public void LocalPlayerCannotBeAddedOrBlocked()
    {
        WDProfileRepository profiles = Profiles();
        WDSocialRepository social = new(profiles, Array.Empty<WDSocialMockSeed>());
        Assert.That(social.SendFriendRequest(profiles.LocalProfile.PlayerId), Is.False);
        Assert.That(social.Block(profiles.LocalProfile.PlayerId), Is.False);
        Assert.That(profiles.LocalProfile.SocialState, Is.EqualTo(WDProfileSocialState.Self));
    }

    // ============================================================
    // 02. BUSCAS E ELEGIBILIDADE
    // ============================================================

    [Test]
    public void FriendsSearchFiltersOnlyFriendsByNameOrPublicId()
    {
        WDProfileRepository profiles = Profiles();
        WDSocialRepository social = new(profiles, new[]
        {
            new WDSocialMockSeed("WD-2847", WDProfileSocialState.Friend, WDSocialPresence.Online),
            new WDSocialMockSeed("WD-3906", WDProfileSocialState.Stranger, WDSocialPresence.Offline)
        });
        Assert.That(social.Friends("Guest2847").Count, Is.EqualTo(1));
        Assert.That(social.Friends("WD-2847").Count, Is.EqualTo(1));
        Assert.That(social.Friends("Guest3906"), Is.Empty);
    }

    [Test]
    public void CatalogSearchUsesExactNameOrIdAndDoesNotInventPlayers()
    {
        WDProfileRepository profiles = Profiles();
        WDSocialRepository social = new(profiles, Array.Empty<WDSocialMockSeed>());
        Assert.That(social.FindCatalogResult("Guest2847")?.PlayerId, Is.EqualTo("WD-2847"));
        Assert.That(social.FindCatalogResult("wd-2847")?.PlayerId, Is.EqualTo("WD-2847"));
        Assert.That(social.FindCatalogResult("UnknownPlayer"), Is.Null);
    }

    [TestCase(WDProfileSocialState.Friend, WDAddFriendEligibility.AlreadyFriend)]
    [TestCase(WDProfileSocialState.RequestSent, WDAddFriendEligibility.RequestSent)]
    [TestCase(WDProfileSocialState.Blocked, WDAddFriendEligibility.Blocked)]
    [TestCase(WDProfileSocialState.Stranger, WDAddFriendEligibility.Eligible)]
    public void AddFriendEligibilityReflectsSharedRelationship(
        WDProfileSocialState state, WDAddFriendEligibility expected)
    {
        WDProfileRepository profiles = Profiles();
        WDSocialRepository social = Social(profiles, "WD-2847", state);
        Assert.That(social.Eligibility(profiles.Find("WD-2847")), Is.EqualTo(expected));
    }

    [Test]
    public void LocalProfileEligibilityIsYou()
    {
        WDProfileRepository profiles = Profiles();
        WDSocialRepository social = new(profiles, Array.Empty<WDSocialMockSeed>());
        Assert.That(social.Eligibility(profiles.LocalProfile), Is.EqualTo(WDAddFriendEligibility.You));
    }

    // ============================================================
    // 03. PERFIL PÚBLICO E CONTRATO FUTURO DE CHAT
    // ============================================================

    [Test]
    public void PublicProfileReadsTheSameRequestSentState()
    {
        WDProfileRepository profiles = Profiles();
        WDSocialRepository social = Social(profiles, "WD-2847", WDProfileSocialState.Stranger);
        social.SendFriendRequest("WD-2847");
        Assert.That(profiles.Present("WD-2847").SocialState,
            Is.EqualTo(WDProfileSocialState.RequestSent));
    }

    [Test]
    public void BlockedPlayerCannotStartChatEvenWhenNonFriendsAreAllowed()
    {
        WDProfileRepository profiles = Profiles();
        WDSocialRepository social = Social(profiles, "WD-2847", WDProfileSocialState.Blocked);
        Assert.That(social.CanStartDirectChat("WD-2847", true), Is.False);
    }

    [Test]
    public void FriendCanStartChatWhenNonFriendMessagesAreDisabled()
    {
        WDProfileRepository profiles = Profiles();
        WDSocialRepository social = Social(profiles, "WD-2847", WDProfileSocialState.Friend);
        Assert.That(social.CanStartDirectChat("WD-2847", false), Is.True);
    }

    [Test]
    public void StrangerChatUsesExistingNonFriendPreference()
    {
        WDProfileRepository profiles = Profiles();
        WDSocialRepository social = Social(profiles, "WD-2847", WDProfileSocialState.Stranger);
        Assert.That(social.CanStartDirectChat("WD-2847", false), Is.False);
        Assert.That(social.CanStartDirectChat("WD-2847", true), Is.True);
    }
}
