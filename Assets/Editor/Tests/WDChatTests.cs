using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class WDChatTests
{
    private sealed class Fixture
    {
        public WDProfileRepository Profiles = new(null, new WDMemoryProfilePrivacyStore());
        public WDSocialRepository Social;
        public WDSettingsData Settings = WDSettingsData.Defaults();
        public WDChatRepository Chat;
        private DateTimeOffset now = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

        public Fixture()
        {
            Social = new WDSocialRepository(Profiles, new[]
            {
                new WDSocialMockSeed("WD-2847", WDProfileSocialState.Friend, WDSocialPresence.Online),
                new WDSocialMockSeed("WD-3906", WDProfileSocialState.Stranger, WDSocialPresence.Offline),
                new WDSocialMockSeed("WD-1291", WDProfileSocialState.Blocked, WDSocialPresence.Offline)
            });
            Chat = new WDChatRepository(Profiles, Social, () => Settings,
                () => now = now.AddMinutes(1));
        }
    }

    // 01. A troca Home -> partida não pode manter callbacks de um painel destruído.
    [Test]
    public void DestroyedHubDoesNotReceiveMatchChannelChanges()
    {
        var fixture = new Fixture();
        var hubObject = new GameObject("ChatHubLifecycleTest", typeof(RectTransform));
        try
        {
            hubObject.SetActive(false);
            WDChatHubPanel hub = hubObject.AddComponent<WDChatHubPanel>();
            hub.Build(null, fixture.Chat, fixture.Social, fixture.Profiles);
            // 02. Ativa a inscrição real antes de destruir o painel.
            hubObject.SetActive(true);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(hubObject);
        }

        Assert.DoesNotThrow(() => fixture.Chat.CreateMatchTeamChannel("match-lifecycle",
            fixture.Chat.LocalPlayerId,
            new[] { (fixture.Chat.LocalPlayerId, 0), ("WD-2847", 0) }));
    }

    [Test]
    public void DestroyedFriendsPanelDoesNotReceiveSocialChangesAfterHomeReentry()
    {
        var fixture = new Fixture();
        var theme = ScriptableObject.CreateInstance<WarDominionUITheme>();
        var panelObject = new GameObject("FriendsLifecycleTest", typeof(RectTransform));
        try
        {
            WDHomeFriendsPanel panel = panelObject.AddComponent<WDHomeFriendsPanel>();
            panel.Build(theme, fixture.Social, new WDNavigationStack(), null, null, null);
            panelObject.SetActive(true);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(panelObject);
            UnityEngine.Object.DestroyImmediate(theme);
        }

        Assert.DoesNotThrow(() => fixture.Social.Block("WD-3906"));
    }

    [Test]
    public void DestroyedProfilePanelDoesNotReceiveSocialChangesAfterHomeReentry()
    {
        var fixture = new Fixture();
        var theme = ScriptableObject.CreateInstance<WarDominionUITheme>();
        var panelObject = new GameObject("ProfileLifecycleTest", typeof(RectTransform));
        try
        {
            WDHomeProfilePanel panel = panelObject.AddComponent<WDHomeProfilePanel>();
            panel.Build(theme, fixture.Profiles, fixture.Social, new WDNavigationStack(),
                null, null);
            panelObject.SetActive(true);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(panelObject);
            UnityEngine.Object.DestroyImmediate(theme);
        }

        Assert.DoesNotThrow(() => fixture.Social.Block("WD-3906"));
    }

    // ============================================================
    // 01. PRIVADO, RECENTES, LEITURA E MUTE
    // ============================================================

    [Test]
    public void EmptyPrivateConversationDoesNotBecomeRecent()
    {
        var f = new Fixture();
        Assert.That(f.Chat.OpenPrivate("WD-2847"), Is.Not.Null);
        Assert.That(f.Chat.RecentPrivate, Is.Empty);
    }

    [Test]
    public void FirstMessageCreatesRecentAndActivityReorders()
    {
        var f = new Fixture();
        WDChatChannel first = f.Chat.OpenPrivate("WD-2847");
        WDChatChannel second = f.Chat.OpenPrivate("WD-3906");
        f.Chat.Send(first.Id, f.Chat.LocalPlayerId, "Olá");
        f.Chat.Send(second.Id, f.Chat.LocalPlayerId, "Olá 2");
        Assert.That(f.Chat.RecentPrivate.Select(item => item.Id).ToArray(),
            Is.EqualTo(new[] { second.Id, first.Id }));
        f.Chat.Send(first.Id, f.Chat.LocalPlayerId, "Mais recente");
        Assert.That(f.Chat.RecentPrivate[0].Id, Is.EqualTo(first.Id));
    }

    [Test]
    public void MessageContractPreservesMultilineContentAndRejectsMoreThanFiveHundredCharacters()
    {
        var f = new Fixture();
        WDChatChannel channel = f.Chat.OpenPrivate("WD-2847");
        string multiline = "  primeira linha\nsegunda linha  ";
        Assert.That(f.Chat.Send(channel.Id, f.Chat.LocalPlayerId, multiline), Is.True);
        Assert.That(channel.Messages.Last().Text, Is.EqualTo(multiline));
        Assert.That(f.Chat.Send(channel.Id, f.Chat.LocalPlayerId, new string('x', 500)), Is.True);
        Assert.That(f.Chat.Send(channel.Id, f.Chat.LocalPlayerId, new string('x', 501)), Is.False);
        Assert.That(f.Chat.Send(channel.Id, f.Chat.LocalPlayerId, "   \n  "), Is.False);
    }

    [Test]
    public void HubDoesNotReadUntilSpecificContentOpens()
    {
        var f = new Fixture();
        WDChatChannel channel = f.Chat.OpenPrivate("WD-2847");
        f.Chat.ReceiveLocalMock(channel.Id, "WD-2847", "Nova mensagem");
        Assert.That(channel.IsUnread, Is.True);
        f.Chat.OpenContent(channel.Id);
        Assert.That(channel.IsUnread, Is.False);
    }

    [Test]
    public void MutePreservesUnreadButSuppressesExternalAttention()
    {
        var f = new Fixture();
        WDChatChannel channel = f.Chat.OpenPrivate("WD-2847");
        f.Chat.SetMuted(channel.Id, true);
        f.Chat.ReceiveLocalMock(channel.Id, "WD-2847", "Silenciada");
        Assert.That(channel.IsUnread, Is.True);
        Assert.That(f.Chat.ExternalUnreadConversationCount, Is.Zero);
    }

    [Test]
    public void ExternalAttentionCountsConversationsNotMessages()
    {
        var f = new Fixture();
        WDChatChannel channel = f.Chat.OpenPrivate("WD-2847");
        f.Chat.ReceiveLocalMock(channel.Id, "WD-2847", "1");
        f.Chat.ReceiveLocalMock(channel.Id, "WD-2847", "2");
        Assert.That(f.Chat.ExternalUnreadConversationCount, Is.EqualTo(1));
    }

    [Test]
    public void GlobalNotificationSettingSuppressesAttentionWithoutClearingUnread()
    {
        var f = new Fixture();
        WDChatChannel channel = f.Chat.OpenPrivate("WD-2847");
        f.Chat.ReceiveLocalMock(channel.Id, "WD-2847", "1");
        f.Settings.ChatNotifications = false;
        Assert.That(channel.IsUnread, Is.True);
        Assert.That(f.Chat.ExternalUnreadConversationCount, Is.Zero);
    }

    [Test]
    public void BlockAndNonFriendSettingAreReadFromSocialAndSettings()
    {
        var f = new Fixture();
        Assert.That(f.Chat.OpenPrivate("WD-1291"), Is.Null);
        f.Settings.AllowNonFriendMessages = false;
        Assert.That(f.Chat.OpenPrivate("WD-3906"), Is.Null);
        f.Settings.AllowNonFriendMessages = true;
        Assert.That(f.Chat.OpenPrivate("WD-3906"), Is.Not.Null);
    }

    [Test]
    public void BlockingAnExistingConversationImmediatelyRemovesItsAccess()
    {
        var f = new Fixture();
        WDChatChannel channel = f.Chat.OpenPrivate("WD-2847");
        f.Chat.ReceiveLocalMock(channel.Id, "WD-2847", "Antes do bloqueio");
        Assert.That(f.Social.Block("WD-2847"), Is.True);
        Assert.That(channel.IsAvailable, Is.False);
        Assert.That(channel.IsUnread, Is.False);
        Assert.That(f.Chat.Send(channel.Id, f.Chat.LocalPlayerId, "Depois"), Is.False);
    }

    [Test]
    public void OnlyPrivateParticipantsCanReadOrWrite()
    {
        var f = new Fixture();
        WDChatChannel channel = f.Chat.OpenPrivate("WD-2847");
        Assert.That(channel.CanAccess("WD-2847"), Is.True);
        Assert.That(channel.CanAccess("WD-3906"), Is.False);
        Assert.That(f.Chat.Send(channel.Id, "WD-3906", "Intrusão"), Is.False);
    }

    // ============================================================
    // 02. CLÃ LOCAL MÍNIMO
    // ============================================================

    [Test]
    public void CurrentClanMemberCanAccessAndWriteRegardlessOfRole()
    {
        var f = new Fixture();
        WDChatChannel clan = f.Chat.ClanChannel;
        Assert.That(clan.CanAccess(f.Chat.LocalPlayerId), Is.True);
        Assert.That(f.Chat.Send(clan.Id, f.Chat.LocalPlayerId, "Mensagem do clã"), Is.True);
    }

    [Test]
    public void ClanEntryIsRepresentedAsSystemEvent()
    {
        var f = new Fixture();
        Assert.That(f.Chat.ClanChannel.Messages[0].Kind, Is.EqualTo(WDChatMessageKind.System));
    }

    [Test]
    public void LeavingClanRemovesChannelAndHistoryAccess()
    {
        var f = new Fixture();
        WDChatChannel clan = f.Chat.ClanChannel;
        f.Chat.LeaveClan();
        Assert.That(clan.IsAvailable, Is.False);
        Assert.That(clan.CanAccess(f.Chat.LocalPlayerId), Is.False);
    }

    [Test]
    public void ClanMuteDoesNotEraseMessagesOrUnread()
    {
        var f = new Fixture();
        WDChatChannel clan = f.Chat.ClanChannel;
        f.Chat.SetMuted(clan.Id, true);
        string remote = clan.Participants.First(id => id != f.Chat.LocalPlayerId);
        f.Chat.ReceiveLocalMock(clan.Id, remote, "Clã");
        Assert.That(clan.Messages.Count, Is.GreaterThan(1));
        Assert.That(clan.IsUnread, Is.True);
        Assert.That(f.Chat.ExternalUnreadConversationCount, Is.Zero);
    }

    // ============================================================
    // 03. CANAL TEMPORÁRIO DE EQUIPE
    // ============================================================

    [TestCase(1)]
    [TestCase(4)]
    public void SoloAndFfaDoNotCreateMatchChannel(int teamSize)
    {
        var f = new Fixture();
        var participants = Enumerable.Range(0, teamSize)
            .Select(i => ($"P{i}", i)).ToArray();
        Assert.That(f.Chat.CreateMatchTeamChannel("M1", "P0", participants), Is.Null);
    }

    [Test]
    public void MissingLocalParticipantDoesNotCreateMatchChannel()
    {
        var f = new Fixture();
        Assert.That(f.Chat.CreateMatchTeamChannel("M1", "Missing",
            new[] { ("A1", 0), ("A2", 0) }), Is.Null);
    }

    [TestCase(2)]
    [TestCase(3)]
    public void TwoAndThreePlayerTeamsCreateMatchChannel(int allies)
    {
        var f = new Fixture();
        var participants = Enumerable.Range(0, allies)
            .Select(i => ($"P{i}", 1)).Concat(new[] { ("Enemy", 2) }).ToArray();
        WDChatChannel channel = f.Chat.CreateMatchTeamChannel("M1", "P0", participants);
        Assert.That(channel, Is.Not.Null);
        Assert.That(channel.Participants.Count, Is.EqualTo(allies));
    }

    [Test]
    public void OpposingTeamsAreIsolated()
    {
        var f = new Fixture();
        var participants = new[] { ("A1", 1), ("A2", 1), ("B1", 2), ("B2", 2) };
        WDChatChannel a = f.Chat.CreateMatchTeamChannel("M1", "A1", participants);
        WDChatChannel b = f.Chat.CreateMatchTeamChannel("M1", "B1", participants);
        Assert.That(a.CanAccess("B1"), Is.False);
        Assert.That(b.CanAccess("A1"), Is.False);
    }

    [Test]
    public void EndingMatchClosesChannelAndNeverAddsItToRecents()
    {
        var f = new Fixture();
        WDChatChannel channel = f.Chat.CreateMatchTeamChannel("M1", "A1",
            new[] { ("A1", 1), ("A2", 1), ("B1", 2), ("B2", 2) });
        f.Chat.Send(channel.Id, "A1", "Equipe");
        f.Chat.EndMatch("M1");
        Assert.That(channel.IsAvailable, Is.False);
        Assert.That(f.Chat.RecentPrivate, Is.Empty);
    }

    [Test]
    public void NewMatchChannelStartsUnmuted()
    {
        var f = new Fixture();
        WDChatChannel first = f.Chat.CreateMatchTeamChannel("M1", "A1",
            new[] { ("A1", 1), ("A2", 1) });
        f.Chat.SetMuted(first.Id, true);
        WDChatChannel second = f.Chat.CreateMatchTeamChannel("M2", "A1",
            new[] { ("A1", 1), ("A2", 1) });
        Assert.That(second.IsMuted, Is.False);
    }

    [Test]
    public void MatchUnreadAttentionRespectsMuteAndGlobalSetting()
    {
        var f = new Fixture();
        WDChatChannel channel = f.Chat.CreateMatchTeamChannel("M1", "A1",
            new[] { ("A1", 1), ("A2", 1) });
        f.Chat.ReceiveLocalMock(channel.Id, "A2", "Equipe");
        Assert.That(f.Chat.HasExternalAttention(channel.Id), Is.True);
        f.Chat.SetMuted(channel.Id, true);
        Assert.That(f.Chat.HasExternalAttention(channel.Id), Is.False);
        f.Chat.SetMuted(channel.Id, false);
        f.Settings.ChatNotifications = false;
        Assert.That(f.Chat.HasExternalAttention(channel.Id), Is.False);
    }

    [Test]
    public void ChatDomainContainsNoGlobalChannel()
    {
        CollectionAssert.DoesNotContain(Enum.GetNames(typeof(WDChatChannelType)), "Global");
    }
}
