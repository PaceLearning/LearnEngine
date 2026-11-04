using LearnEngine.Core.Bandit;
using LearnEngine.Core.History;
using LearnEngine.Core.Judging;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static LearnEngine.Core.Tests.TestVocabulary;

namespace LearnEngine.Core.Tests;

/// <summary>Judging a picked move by the learner turns after it, and recording the judgment in the learner's score.</summary>
public sealed class MoveJudgeTests
{
    private const string Learner = "learner";
    private const string Session = "session";

    [Fact]
    public void Judge_NoTurnAfterThePick_GivesTheMoveBeingJudged()
    {
        using var provider = Create();
        var judge = provider.GetRequiredService<MoveJudge>();
        var start = Start(provider);

        var judged = judge.Judge([start, MovePicked.Create(First, One)]);

        Assert.True(judged.IsT1);
        Assert.Equal(MoveBeingJudged.Create(First, One, MoveJudge.JudgingTurns), judged.AsT1);
    }

    [Fact]
    public void Judge_SuccessSignalInTheSameState_GivesWorked()
    {
        using var provider = Create();
        var judge = provider.GetRequiredService<MoveJudge>();
        var start = Start(provider);

        var judged = judge.Judge([start, MovePicked.Create(First, One), LearnerTurn.Create([LeansFirst, WentWell])]);

        Assert.True(judged.IsT0);
        Assert.Equal(First, judged.AsT0.StateId);
        Assert.Equal(One, judged.AsT0.MoveId);
        Assert.True(judged.AsT0.Worked);
    }

    [Fact]
    public void Judge_FailureSignalWithASuccessSignal_GivesFailed()
    {
        using var provider = Create();
        var judge = provider.GetRequiredService<MoveJudge>();
        var start = Start(provider);

        var judged = judge.Judge([start, MovePicked.Create(First, One), LearnerTurn.Create([LeansFirst, WentWell, WentBadly])]);

        Assert.True(judged.IsT0);
        Assert.False(judged.AsT0.Worked);
    }

    [Fact]
    public void Judge_ResultOnTheSecondTurn_StillJudges()
    {
        using var provider = Create();
        var judge = provider.GetRequiredService<MoveJudge>();
        var start = Start(provider);

        var afterOne = judge.Judge([start, MovePicked.Create(First, One), LearnerTurn.Create([LeansFirst])]);
        var afterTwo = judge.Judge([start, MovePicked.Create(First, One), LearnerTurn.Create([LeansFirst]), LearnerTurn.Create([LeansFirst, WentBadly])]);

        Assert.Equal(MoveBeingJudged.Create(First, One, 1), afterOne.AsT1);
        Assert.False(afterTwo.AsT0.Worked);
    }

    [Fact]
    public void Judge_TwoTurnsWithNoResult_GivesNothingToJudge()
    {
        using var provider = Create();
        var judge = provider.GetRequiredService<MoveJudge>();
        var start = Start(provider);

        var judged = judge.Judge([start, MovePicked.Create(First, One), LearnerTurn.Create([LeansFirst]), LearnerTurn.Create([LeansFirst])]);

        Assert.True(judged.IsT2);
    }

    [Fact]
    public void Judge_StateChangesOnTheTurn_GivesNothingToJudge()
    {
        using var provider = Create();
        var judge = provider.GetRequiredService<MoveJudge>();
        var start = Start(provider);

        // leans.second puts the second state ahead, so the success signal on the same turn judges nothing.
        var judged = judge.Judge([start, MovePicked.Create(First, One), LearnerTurn.Create([LeansSecond, WentWell])]);

        Assert.True(judged.IsT2);
    }

    [Fact]
    public void Judge_JudgmentAlreadyRecorded_GivesNothingToJudge()
    {
        using var provider = Create();
        var judge = provider.GetRequiredService<MoveJudge>();
        var start = Start(provider);

        var judged = judge.Judge([start, MovePicked.Create(First, One), LearnerTurn.Create([LeansFirst, WentWell]), MoveJudged.Create(First, One, true)]);

        Assert.True(judged.IsT2);
    }

    [Fact]
    public void Judge_RecordedJudgmentTheTurnsDoNotGive_GivesBrokenHistory()
    {
        using var provider = Create();
        var judge = provider.GetRequiredService<MoveJudge>();
        var start = Start(provider);

        var judged = judge.Judge([start, MovePicked.Create(First, One), LearnerTurn.Create([LeansFirst, WentWell]), MoveJudged.Create(First, One, false)]);

        Assert.True(judged.IsT4);
        Assert.Equal(3, judged.AsT4.EventIndex);
    }

    [Fact]
    public void Judge_MovePickedInAStateThatIsNotClear_GivesBrokenHistory()
    {
        using var provider = Create();
        var judge = provider.GetRequiredService<MoveJudge>();
        var start = Start(provider);

        var judged = judge.Judge([start, LearnerTurn.Create([LeansSecond]), MovePicked.Create(First, One)]);

        Assert.True(judged.IsT4);
        Assert.Equal(2, judged.AsT4.EventIndex);
    }

    [Fact]
    public async Task Record_Worked_AddsATryAndAWorkToTheStartingScore()
    {
        using var provider = Create();
        var judge = provider.GetRequiredService<MoveJudge>();
        var histories = provider.GetRequiredService<IHistoryStore>();

        var score = await judge.Record(Learner, Session, MoveJudged.Create(First, One, true), CancellationToken.None);

        Assert.Equal(MoveScore.Create(First, One, 6, 11), score);
        var history = (await histories.Load(Learner, Session, CancellationToken.None)).AsT0;
        var recorded = Assert.IsType<MoveJudged>(Assert.Single(history));
        Assert.True(recorded.Worked);
    }

    [Fact]
    public async Task Record_FailedAfterWorked_AddsATryToTheLearnersOwnScore()
    {
        using var provider = Create();
        var judge = provider.GetRequiredService<MoveJudge>();
        var scores = provider.GetRequiredService<IScoreStore>();

        await judge.Record(Learner, Session, MoveJudged.Create(First, One, true), CancellationToken.None);
        var score = await judge.Record(Learner, Session, MoveJudged.Create(First, One, false), CancellationToken.None);

        Assert.Equal(MoveScore.Create(First, One, 6, 12), score);
        var saved = await scores.Load(Learner, First, CancellationToken.None);
        Assert.Equal([score], saved.ToArray());
    }

    [Fact]
    public async Task Record_ManyAtOnce_CountsEveryJudgment()
    {
        using var provider = Create();
        var judge = provider.GetRequiredService<MoveJudge>();
        var scores = provider.GetRequiredService<IScoreStore>();

        var records = Enumerable.Range(0, 50)
            .Select(index => Task.Run(() => judge.Record(Learner, $"session_{index}", MoveJudged.Create(First, One, true), CancellationToken.None)));
        await Task.WhenAll(records);

        var saved = await scores.Load(Learner, First, CancellationToken.None);
        Assert.Equal(MoveScore.Create(First, One, 55, 60), Assert.Single(saved));
    }

    [Fact]
    public async Task Record_MoveNotAllowedInTheState_Throws()
    {
        using var provider = Create();
        var judge = provider.GetRequiredService<MoveJudge>();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            judge.Record(Learner, Session, MoveJudged.Create(Second, One, true), CancellationToken.None));
    }

    /// <summary>The session start stamped with the vocabulary of the provider and the default fade and clear margin.</summary>
    private static SessionStarted Start(ServiceProvider provider) =>
        SessionStarted.Create(provider.GetRequiredService<VocabularyFingerprint>().Value, 0.15, 0.15);
}
