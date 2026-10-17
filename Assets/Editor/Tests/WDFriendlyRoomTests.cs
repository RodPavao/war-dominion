using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class WDFriendlyRoomTests
{
    // ============================================================
    // 01. CRIAÇÃO E VALIDAÇÃO DAS CONFIGURAÇÕES REAIS
    // ============================================================
    [TestCase(WDFriendlyMode.OneVsOne, 2)]
    [TestCase(WDFriendlyMode.TwoVsTwo, 4)]
    [TestCase(WDFriendlyMode.ThreeVsThree, 6)]
    public void CreatesRoomWithCorrectCapacity(WDFriendlyMode mode, int capacity)
    {
        WDFriendlyRoom room = Room(mode);
        Assert.That(room.Participants, Has.Count.EqualTo(1));
        Assert.That(room.AvailableSlots, Is.EqualTo(capacity - 1));
        Assert.That(room.Find("host").IsHost, Is.True);
        Assert.That(room.Find("host").TeamIndex, Is.Zero);
        Assert.That(room.Find("host").PositionIndex, Is.Zero);
    }

    [Test]
    public void RejectsUnapprovedThreeVsThreeRandomCards()
    {
        var configuration = Config(WDFriendlyMode.ThreeVsThree,
            WDFriendlyCardMode.RandomCards);
        Assert.That(WDFriendlyRules.IsSupported(configuration), Is.False);
        Assert.Throws<ArgumentException>(() => new WDFriendlyRoom(
            "room", "host", "Host", configuration));
    }

    [Test]
    public void AdapterUsesExistingCatalogAndRejectsUnsupportedCombination()
    {
        WarDominionMatchFlowConfig catalog = Resources.Load<WarDominionMatchFlowConfig>(
            "UI/Home/WarDominionMatchFlowConfig");
        Assert.That(catalog, Is.Not.Null);
        Assert.That(WDFriendlyRules.TryCreateRequest(Config(WDFriendlyMode.TwoVsTwo),
            catalog, out WDMatchmakingRequest request, out string error), Is.True, error);
        Assert.That(request.ModeId, Is.EqualTo("2x2"));
        Assert.That(request.SubmodeId, Is.EqualTo("normal"));
        Assert.That(request.TeamFormation, Is.EqualTo("friendly_room"));
        Assert.That(WDFriendlyRules.TryCreateRequest(Config(WDFriendlyMode.ThreeVsThree,
            WDFriendlyCardMode.RandomCards), catalog, out _, out _), Is.False);
    }

    // ============================================================
    // 02. PARTICIPANTES, EQUIPES, VAGAS E PROTEÇÃO DO HOST
    // ============================================================
    [Test]
    public void AddsRemovesAndRejectsDuplicateParticipants()
    {
        WDFriendlyRoom room = Room(WDFriendlyMode.TwoVsTwo);
        Assert.That(room.AddParticipant("a", "A"), Is.EqualTo(WDFriendlyRoomOperationResult.Success));
        Assert.That(room.AddParticipant("a", "A"), Is.EqualTo(WDFriendlyRoomOperationResult.DuplicatePlayer));
        Assert.That(room.RemoveParticipant("a"), Is.EqualTo(WDFriendlyRoomOperationResult.Success));
        Assert.That(room.RemoveParticipant("host"), Is.EqualTo(WDFriendlyRoomOperationResult.HostProtected));
    }

    [Test]
    public void EnforcesCapacityAndTeamLimits()
    {
        WDFriendlyRoom room = Room(WDFriendlyMode.OneVsOne);
        Assert.That(room.AddParticipant("a", "A", 0), Is.EqualTo(WDFriendlyRoomOperationResult.TeamFull));
        Assert.That(room.AddParticipant("a", "A", 1), Is.EqualTo(WDFriendlyRoomOperationResult.Success));
        Assert.That(room.AddParticipant("b", "B"), Is.EqualTo(WDFriendlyRoomOperationResult.RoomFull));
    }

    [Test]
    public void MovesRealParticipantWithoutMovingHost()
    {
        WDFriendlyRoom room = Room(WDFriendlyMode.TwoVsTwo);
        room.AddParticipant("a", "A", 1);
        Assert.That(room.MoveParticipant("a", 0), Is.EqualTo(WDFriendlyRoomOperationResult.Success));
        Assert.That(room.Find("a").TeamIndex, Is.Zero);
        Assert.That(room.Find("a").PositionIndex, Is.EqualTo(1));
        Assert.That(room.MoveParticipant("host", 1), Is.EqualTo(WDFriendlyRoomOperationResult.HostProtected));
    }

    // ============================================================
    // 03. DECKS, PRONTIDÃO E CONDIÇÕES DE INÍCIO
    // ============================================================
    [Test]
    public void NormalModeRequiresValidDeckBeforeReady()
    {
        WDFriendlyRoom room = Room(WDFriendlyMode.OneVsOne);
        Assert.That(room.SetReady("host", true), Is.EqualTo(WDFriendlyRoomOperationResult.InvalidDeck));
        Assert.That(room.SetDeck("host", 0, false), Is.EqualTo(WDFriendlyRoomOperationResult.InvalidDeck));
        Assert.That(room.SetDeck("host", 0, true), Is.EqualTo(WDFriendlyRoomOperationResult.Success));
        Assert.That(room.SetReady("host", true), Is.EqualTo(WDFriendlyRoomOperationResult.Success));
    }

    [Test]
    public void RandomCardsDoesNotRequireDeck()
    {
        WDFriendlyRoom room = Room(WDFriendlyMode.OneVsOne, WDFriendlyCardMode.RandomCards);
        Assert.That(room.SetReady("host", true), Is.EqualTo(WDFriendlyRoomOperationResult.Success));
    }

    [Test]
    public void MembershipAndConfigurationChangesInvalidateReady()
    {
        WDFriendlyRoom room = Room(WDFriendlyMode.TwoVsTwo);
        room.SetDeck("host", 0, true);
        room.SetReady("host", true);
        room.AddParticipant("a", "A");
        Assert.That(room.Find("host").IsReady, Is.False);
        room.SetDeck("host", 0, true);
        room.SetReady("host", true);
        Assert.That(room.ChangeConfiguration(new WDFriendlyRoomConfiguration(
            WDFriendlyMode.TwoVsTwo, WDFriendlyCardMode.Normal,
            WDFriendlyMapMode.Random, true, true)),
            Is.EqualTo(WDFriendlyRoomOperationResult.Success));
        Assert.That(room.Participants.All(item => !item.IsReady), Is.True);
    }

    [Test]
    public void CannotStartWithMissingSlotsOrPlayersNotReady()
    {
        WDFriendlyRoom room = Room(WDFriendlyMode.OneVsOne);
        room.SetDeck("host", 0, true);
        room.SetReady("host", true);
        Assert.That(room.CanStart(out string incomplete), Is.False);
        Assert.That(incomplete, Does.Contain("1/2"));
        room.AddParticipant("a", "A", 1);
        room.SetDeck("host", 0, true);
        room.SetDeck("a", 0, true);
        room.SetReady("host", true);
        Assert.That(room.CanStart(out string notReady), Is.False);
        Assert.That(notReady, Does.Contain("prontos"));
        room.SetReady("a", true);
        Assert.That(room.TryBeginStart(out _), Is.True);
        Assert.That(room.State, Is.EqualTo(WDFriendlyRoomState.Starting));
    }

    private static WDFriendlyRoom Room(WDFriendlyMode mode,
        WDFriendlyCardMode cards = WDFriendlyCardMode.Normal) =>
        new("room", "host", "Host", Config(mode, cards));

    private static WDFriendlyRoomConfiguration Config(WDFriendlyMode mode,
        WDFriendlyCardMode cards = WDFriendlyCardMode.Normal) =>
        new(mode, cards, WDFriendlyMapMode.Classic, false, false);
}
