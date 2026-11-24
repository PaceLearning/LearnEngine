using LearnEngine.Core.History;
using LearnEngine.Core.Learning;
using LearnEngine.Core.Odds;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static LearnEngine.Core.Tests.TestVocabulary;

namespace LearnEngine.Core.Tests;

/// <summary>Rebuilding the belief from a history, and the histories that cannot be replayed.</summary>
public sealed class HistoryReplayerTests
{
    private const double Fade = 0.15;

    [Fact]
    public void Replay_Turns_FadesBeforeEachTurnAndSkipsOtherEvents()
    {
        using var provider = Create();
        var replayer = provider.GetRequiredService<HistoryReplayer>();
        var engine = provider.GetRequiredService<OddsEngine>();
        var fingerprint = provider.GetRequiredService<VocabularyFingerprint>().Value;

        var replayed = replayer.Replay([
            SessionStarted.Create(fingerprint, Fade, 0.15),
            LearnerTurn.Create([LeansFirst]),
            MovePicked.Create(First, One),
            LearnerTurn.Create([]),
            MoveJudged.Create(First, One, true)]);

        var afterOne = engine.Apply(engine.Fade(engine.Start(), Fade), LearnerModel.Empty, [LeansFirst]).AsT0;
        var afterTwo = engine.Apply(engine.Fade(afterOne, Fade), LearnerModel.Empty, []).AsT0;
        Assert.True(replayed.IsT0);
        Assert.Equal(afterTwo.All[0].Odds, replayed.AsT0.All[0].Odds, 10);
        Assert.Equal(afterTwo.All[1].Odds, replayed.AsT0.All[1].Odds, 10);
    }

    [Fact]
    public void Replay_OnlyTheStart_GivesThePriors()
    {
        using var provider = Create();
        var replayer = provider.GetRequiredService<HistoryReplayer>();
        var fingerprint = provider.GetRequiredService<VocabularyFingerprint>().Value;

        var replayed = replayer.Replay([SessionStarted.Create(fingerprint, Fade, 0.15)]);

        Assert.Equal(FirstPrior, replayed.AsT0.All[0].Odds, 10);
    }

    [Fact]
    public void Replay_Empty_GivesBrokenHistory()
    {
        using var provider = Create();
        var replayer = provider.GetRequiredService<HistoryReplayer>();

        var replayed = replayer.Replay([]);

        Assert.True(replayed.IsT2);
        Assert.Equal(0, replayed.AsT2.EventIndex);
    }

    [Fact]
    public void Replay_NoSessionStartFirst_GivesBrokenHistory()
    {
        using var provider = Create();
        var replayer = provider.GetRequiredService<HistoryReplayer>();

        var replayed = replayer.Replay([LearnerTurn.Create([LeansFirst])]);

        Assert.True(replayed.IsT2);
        Assert.Equal(0, replayed.AsT2.EventIndex);
    }

    [Fact]
    public void Replay_SecondSessionStart_GivesBrokenHistoryAtIt()
    {
        using var provider = Create();
        var replayer = provider.GetRequiredService<HistoryReplayer>();
        var start = SessionStarted.Create(provider.GetRequiredService<VocabularyFingerprint>().Value, Fade, 0.15);

        var replayed = replayer.Replay([start, LearnerTurn.Create([]), start]);

        Assert.True(replayed.IsT2);
        Assert.Equal(2, replayed.AsT2.EventIndex);
    }

    [Fact]
    public void Replay_TurnWithUnknownSignal_GivesBrokenHistoryAtIt()
    {
        using var provider = Create();
        var replayer = provider.GetRequiredService<HistoryReplayer>();
        var start = SessionStarted.Create(provider.GetRequiredService<VocabularyFingerprint>().Value, Fade, 0.15);

        var replayed = replayer.Replay([start, LearnerTurn.Create([LeansFirst]), LearnerTurn.Create(["not.a.signal"])]);

        Assert.True(replayed.IsT2);
        Assert.Equal(2, replayed.AsT2.EventIndex);
        Assert.Contains("not.a.signal", replayed.AsT2.Reason);
    }

    [Fact]
    public void Replay_TurnLearnedAfterATurn_GivesTheKeptOdds()
    {
        using var provider = Create();
        var replayer = provider.GetRequiredService<HistoryReplayer>();
        var start = SessionStarted.Create(provider.GetRequiredService<VocabularyFingerprint>().Value, Fade, 0.15);

        var replayed = replayer.Replay([
            start,
            LearnerTurn.Create([LeansFirst]),
            TurnLearned.Create([StateOdds.Create(First, 0.3), StateOdds.Create(Second, 0.7)], LearnerModelChange.Create([]))]);

        Assert.Equal(0.3, replayed.AsT0.All[0].Odds, 10);
        Assert.Equal(Second, replayed.AsT0.Leader.StateId);
    }

    [Fact]
    public void Replay_TurnLearnedNotRightAfterATurn_GivesBrokenHistoryAtIt()
    {
        using var provider = Create();
        var replayer = provider.GetRequiredService<HistoryReplayer>();
        var start = SessionStarted.Create(provider.GetRequiredService<VocabularyFingerprint>().Value, Fade, 0.15);

        var replayed = replayer.Replay([
            start,
            TurnLearned.Create([StateOdds.Create(First, 0.3), StateOdds.Create(Second, 0.7)], LearnerModelChange.Create([]))]);

        Assert.True(replayed.IsT2);
        Assert.Equal(1, replayed.AsT2.EventIndex);
    }

    [Theory]
    [InlineData(0.3, 0.6)]
    [InlineData(-0.1, 1.1)]
    [InlineData(double.NaN, 0.5)]
    public void Replay_TurnLearnedOddsOutOfRangeOrNotAddingUpToOne_GivesBrokenHistoryAtIt(double first, double second)
    {
        using var provider = Create();
        var replayer = provider.GetRequiredService<HistoryReplayer>();
        var start = SessionStarted.Create(provider.GetRequiredService<VocabularyFingerprint>().Value, Fade, 0.15);

        var replayed = replayer.Replay([
            start,
            LearnerTurn.Create([]),
            TurnLearned.Create([StateOdds.Create(First, first), StateOdds.Create(Second, second)], LearnerModelChange.Create([]))]);

        Assert.True(replayed.IsT2);
        Assert.Equal(2, replayed.AsT2.EventIndex);
    }

    [Fact]
    public void Replay_TurnLearnedWithStatesOutOfOrderOrACountOfAnUnknownSignal_GivesBrokenHistory()
    {
        using var provider = Create();
        var replayer = provider.GetRequiredService<HistoryReplayer>();
        var start = SessionStarted.Create(provider.GetRequiredService<VocabularyFingerprint>().Value, Fade, 0.15);

        var outOfOrder = replayer.Replay([
            start,
            LearnerTurn.Create([]),
            TurnLearned.Create([StateOdds.Create(Second, 0.5), StateOdds.Create(First, 0.5)], LearnerModelChange.Create([]))]);
        var unknownSignal = replayer.Replay([
            start,
            LearnerTurn.Create([]),
            TurnLearned.Create(
                [StateOdds.Create(First, 0.5), StateOdds.Create(Second, 0.5)],
                LearnerModelChange.Create([RateCount.Create("not.a.signal", First, 0.5, 0)]))]);

        Assert.True(outOfOrder.IsT2);
        Assert.True(unknownSignal.IsT2);
        Assert.Contains("not.a.signal", unknownSignal.AsT2.Reason);
    }

    [Fact]
    public void Replay_TurnWithUnknownMoveMade_GivesBrokenHistoryAtIt()
    {
        using var provider = Create();
        var replayer = provider.GetRequiredService<HistoryReplayer>();
        var start = SessionStarted.Create(provider.GetRequiredService<VocabularyFingerprint>().Value, Fade, 0.15);

        var replayed = replayer.Replay([start, LearnerTurn.Create([LeansFirst], "a.topic", One), LearnerTurn.Create([], null, "not_a_move")]);

        Assert.True(replayed.IsT2);
        Assert.Equal(2, replayed.AsT2.EventIndex);
        Assert.Contains("not_a_move", replayed.AsT2.Reason);
    }

    [Theory]
    [InlineData(1.5, 0.15)]
    [InlineData(0.15, -0.5)]
    public void Replay_FadeOrClearMarginOutOfRange_GivesBrokenHistory(double fade, double clearMargin)
    {
        using var provider = Create();
        var replayer = provider.GetRequiredService<HistoryReplayer>();
        var fingerprint = provider.GetRequiredService<VocabularyFingerprint>().Value;

        var replayed = replayer.Replay([SessionStarted.Create(fingerprint, fade, clearMargin)]);

        Assert.True(replayed.IsT2);
    }

    [Fact]
    public void Replay_StampedWithAnotherVocabulary_GivesOtherVocabulary()
    {
        using var provider = Create();
        var replayer = provider.GetRequiredService<HistoryReplayer>();
        var fingerprint = provider.GetRequiredService<VocabularyFingerprint>().Value;

        var replayed = replayer.Replay([SessionStarted.Create("0000000000000000", Fade, 0.15)]);

        Assert.True(replayed.IsT1);
        Assert.Equal(fingerprint, replayed.AsT1.Expected);
        Assert.Equal("0000000000000000", replayed.AsT1.Found);
    }

    [Fact]
    public void Fingerprint_AnotherRateWeight_ChangesIt()
    {
        using var provider = Create();
        using var other = Create(rateWeight: 30);

        Assert.NotEqual(
            provider.GetRequiredService<VocabularyFingerprint>().Value,
            other.GetRequiredService<VocabularyFingerprint>().Value);
    }

    [Fact]
    public void Fingerprint_SameVocabulary_IsTheSameAndAnotherDescriptionChangesIt()
    {
        using var provider = Create();
        using var same = Create();
        using var other = Create(otherDescription: "Reads differently.");

        var fingerprint = provider.GetRequiredService<VocabularyFingerprint>().Value;

        Assert.Equal(16, fingerprint.Length);
        Assert.Equal(fingerprint, same.GetRequiredService<VocabularyFingerprint>().Value);
        Assert.NotEqual(fingerprint, other.GetRequiredService<VocabularyFingerprint>().Value);
    }
}
