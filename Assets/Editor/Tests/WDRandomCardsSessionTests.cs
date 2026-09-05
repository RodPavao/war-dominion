using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public sealed class WDRandomCardsSessionTests
{
    // ============================================================
    // 01. ABERTURA SIMÉTRICA E SOMENTE COM CARTAS BÁSICAS
    // ============================================================

    [Test]
    public void RoundOne_DealsFourUniqueBasicCardsToBothPlayers()
    {
        WDRandomCardsSession session = Session(10);
        WDRandomCardsRoundResult result = session.ProcessRound(1);

        Assert.That(result.DrawnCards, Has.Count.EqualTo(4));
        Assert.That(result.DrawnCards.Distinct().Count(), Is.EqualTo(4));
        Assert.That(result.DrawnCards, Is.All.Matches<string>(id => id.StartsWith("basic")));
        Assert.That(session.GetHand("a"), Is.EqualTo(session.GetHand("b")));
    }

    [Test]
    public void RoundOne_NeverUsesNonBasicCards()
    {
        WDRandomCardsSession session = Session(11);
        Assert.That(session.ProcessRound(1).DrawnCards,
            Has.None.EqualTo("advanced1"));
    }

    [Test]
    public void SameSeed_ProducesSameCommonSequence()
    {
        WDRandomCardsSession first = Session(42);
        WDRandomCardsSession second = Session(42);
        Assert.That(DrawRounds(first, 1, 7), Is.EqualTo(DrawRounds(second, 1, 7)));
    }

    [Test]
    public void DifferentSeeds_ProduceDifferentSequence()
    {
        Assert.That(DrawRounds(Session(1), 1, 7),
            Is.Not.EqualTo(DrawRounds(Session(2), 1, 7)));
    }

    // ============================================================
    // 02. PROGRESSÃO, CAPACIDADE E PERDA INDIVIDUAL
    // ============================================================

    [Test]
    public void LaterRound_DrawsExactlyOneNewCommonCard()
    {
        WDRandomCardsSession session = Session(12);
        session.ProcessRound(1);
        Assert.That(session.ProcessRound(2).DrawnCards, Has.Count.EqualTo(1));
        Assert.That(session.GetHand("a"), Is.EqualTo(session.GetHand("b")));
    }

    [Test]
    public void DrawnCards_DoNotRepeatAcrossRounds()
    {
        List<string> cards = DrawRounds(Session(13), 1, 8);
        Assert.That(cards.Distinct().Count(), Is.EqualTo(cards.Count));
    }

    [Test]
    public void CardsPersistBetweenRounds()
    {
        WDRandomCardsSession session = Session(14);
        session.ProcessRound(1);
        string first = session.GetHand("a")[0];
        session.ProcessRound(2);
        Assert.That(session.GetHand("a")[0], Is.EqualTo(first));
    }

    [Test]
    public void HandNeverExceedsEightSlots()
    {
        WDRandomCardsSession session = Session(15);
        DrawRounds(session, 1, 12);
        Assert.That(session.GetHand("a").Count(id => !string.IsNullOrEmpty(id)),
            Is.EqualTo(WDRandomCardsSession.HandCapacity));
    }

    [Test]
    public void FullPlayerMissesCardWhileOtherPlayerReceivesIt()
    {
        WDRandomCardsSession session = Session(16);
        DrawRounds(session, 1, 5);
        Assert.That(session.Consume("b", 0), Is.True);

        WDRandomCardsRoundResult result = session.ProcessRound(6);
        Assert.That(result.DeliveredByPlayer["a"], Is.False);
        Assert.That(result.DeliveredByPlayer["b"], Is.True);
    }

    [Test]
    public void MissedCardIsNotDeliveredRetroactively()
    {
        WDRandomCardsSession session = Session(17);
        DrawRounds(session, 1, 5);
        WDRandomCardsRoundResult missed = session.ProcessRound(6);
        string missedCard = missed.DrawnCards.Single();
        session.Consume("a", 0);

        session.ProcessRound(6);
        Assert.That(session.GetHand("a"), Has.None.EqualTo(missedCard));
    }

    [Test]
    public void ConsumingCardOnlyChangesThatPlayersHand()
    {
        WDRandomCardsSession session = Session(18);
        session.ProcessRound(1);
        string card = session.GetHand("a")[0];

        Assert.That(session.Consume("a", 0), Is.True);
        Assert.That(session.GetHand("a")[0], Is.Empty);
        Assert.That(session.GetHand("b")[0], Is.EqualTo(card));
    }

    // ============================================================
    // 03. IDEMPOTÊNCIA E ESGOTAMENTO SEGURO
    // ============================================================

    [Test]
    public void ProcessingSameRoundTwiceIsIdempotent()
    {
        WDRandomCardsSession session = Session(19);
        WDRandomCardsRoundResult first = session.ProcessRound(1);
        WDRandomCardsRoundResult second = session.ProcessRound(1);

        Assert.That(second, Is.SameAs(first));
        Assert.That(session.GetHand("a").Count(id => !string.IsNullOrEmpty(id)), Is.EqualTo(4));
    }

    [Test]
    public void ExhaustedPoolReturnsNoDraw()
    {
        var cards = new List<WDCardPoolEntry>
        {
            new("basic1", true), new("basic2", true),
            new("basic3", true), new("basic4", true)
        };
        var session = new WDRandomCardsSession(20, cards, new[] { "a", "b" });
        session.ProcessRound(1);

        Assert.That(session.ProcessRound(2).DrawnCards, Is.Empty);
    }

    [Test]
    public void InvalidPlayerAndSlotCannotConsumeCards()
    {
        WDRandomCardsSession session = Session(21);
        session.ProcessRound(1);
        Assert.That(session.Consume("unknown", 0), Is.False);
        Assert.That(session.Consume("a", -1), Is.False);
        Assert.That(session.Consume("a", 8), Is.False);
    }

    private static WDRandomCardsSession Session(int seed) =>
        new(seed, Pool(), new[] { "a", "b" });

    private static List<WDCardPoolEntry> Pool()
    {
        var cards = new List<WDCardPoolEntry>();
        for (int i = 1; i <= 8; i++)
            cards.Add(new WDCardPoolEntry($"basic{i}", true));
        for (int i = 1; i <= 12; i++)
            cards.Add(new WDCardPoolEntry($"advanced{i}", false));
        return cards;
    }

    private static List<string> DrawRounds(
        WDRandomCardsSession session, int firstRound, int lastRound)
    {
        var cards = new List<string>();
        for (int round = firstRound; round <= lastRound; round++)
            cards.AddRange(session.ProcessRound(round).DrawnCards);
        return cards;
    }
}
