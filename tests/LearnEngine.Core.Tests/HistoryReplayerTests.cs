using LearnEngine.Core.History;
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

        var afterOne = engine.Apply(engine.Fade(engine.Start(), Fade), [LeansFirst]).AsT0;
        var afterTwo = engine.Apply(engine.Fade(afterOne, Fade), []).AsT0;
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
