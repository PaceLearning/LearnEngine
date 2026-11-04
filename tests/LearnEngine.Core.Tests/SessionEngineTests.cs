using LearnEngine.Core.History;
using LearnEngine.Core.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static LearnEngine.Core.Tests.TestVocabulary;

namespace LearnEngine.Core.Tests;

/// <summary>Starting a session, reporting its turns and reading its belief, as a host does.</summary>
public sealed class SessionEngineTests
{
    private const string Learner = "learner";
    private const string Session = "session";

    [Fact]
    public async Task StartSession_New_GivesThePriorsAndStampsTheHistory()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        var histories = provider.GetRequiredService<IHistoryStore>();

        var started = await sessions.StartSession(Learner, Session, CancellationToken.None);

        Assert.True(started.IsT0);
        Assert.Equal(FirstPrior, started.AsT0.All[0].Odds, 10);
        Assert.True(started.AsT0.IsClear);
        var history = await histories.Load(Learner, Session, CancellationToken.None);
        var start = Assert.IsType<SessionStarted>(Assert.Single(history));
        Assert.Equal(provider.GetRequiredService<VocabularyFingerprint>().Value, start.Fingerprint);
    }

    [Fact]
    public async Task StartSession_AlreadyStarted_KeepsTheHistory()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        var histories = provider.GetRequiredService<IHistoryStore>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);
        await sessions.ReportTurn(Learner, Session, [LeansFirst], CancellationToken.None);
        var before = await histories.Load(Learner, Session, CancellationToken.None);

        var started = await sessions.StartSession(Learner, Session, CancellationToken.None);

        Assert.True(started.IsT1);
        Assert.Equal(SessionAlreadyStarted.Create(Learner, Session), started.AsT1);
        Assert.Equal(before.Length, (await histories.Load(Learner, Session, CancellationToken.None)).Length);
    }

    [Fact]
    public async Task ReportTurn_NotStarted_GivesSessionNotStarted()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();

        var reported = await sessions.ReportTurn(Learner, Session, [LeansFirst], CancellationToken.None);
        var belief = await sessions.GetBelief(Learner, Session, CancellationToken.None);

        Assert.Equal(SessionNotStarted.Create(Learner, Session), reported.AsT2);
        Assert.Equal(SessionNotStarted.Create(Learner, Session), belief.AsT1);
    }

    [Fact]
    public async Task ReportTurn_UnknownSignal_GivesItAndKeepsNothing()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        var histories = provider.GetRequiredService<IHistoryStore>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);

        var reported = await sessions.ReportTurn(Learner, Session, [LeansFirst, "not.a.signal"], CancellationToken.None);

        Assert.True(reported.IsT1);
        Assert.Equal("not.a.signal", Assert.Single(reported.AsT1).Id);
        Assert.Single(await histories.Load(Learner, Session, CancellationToken.None));
    }

    [Fact]
    public async Task ReportTurn_ClearState_PicksAMoveAndKeepsIt()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        var histories = provider.GetRequiredService<IHistoryStore>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);

        var reported = await sessions.ReportTurn(Learner, Session, [LeansFirst], CancellationToken.None);

        Assert.True(reported.IsT0);
        Assert.Equal(First, reported.AsT0.Belief.Leader.StateId);
        Assert.True(reported.AsT0.Belief.IsClear);
        Assert.Equal(One, reported.AsT0.Pick.AsT0.Move.Id);
        Assert.True(reported.AsT0.Judgment.IsT2);
        var history = await histories.Load(Learner, Session, CancellationToken.None);
        Assert.Collection(history,
            historyEvent => Assert.IsType<SessionStarted>(historyEvent),
            historyEvent => Assert.IsType<LearnerTurn>(historyEvent),
            historyEvent => Assert.Equal(One, Assert.IsType<MovePicked>(historyEvent).MoveId));
    }

    [Fact]
    public async Task ReportTurn_NoClearLeader_PicksNothing()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);

        // No signals at all puts the two states close together.
        var reported = await sessions.ReportTurn(Learner, Session, [], CancellationToken.None);

        Assert.False(reported.AsT0.Belief.IsClear);
        Assert.True(reported.AsT0.Pick.IsT1);
    }

    [Fact]
    public async Task ReportTurn_AfterAPick_JudgesItAndUpdatesTheScore()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);
        await sessions.ReportTurn(Learner, Session, [LeansFirst], CancellationToken.None);

        var worked = await sessions.ReportTurn(Learner, Session, [LeansFirst, WentWell], CancellationToken.None);

        var judgment = worked.AsT0.Judgment.AsT0;
        Assert.True(judgment.Worked);
        Assert.Equal(First, judgment.Score.StateId);
        Assert.Equal(One, judgment.Score.MoveId);
        Assert.Equal(6, judgment.Score.Worked);
        Assert.Equal(11, judgment.Score.Tried);
        // one at 6 of 11 is still ahead of two at 5 of 10, even with the bonus two gets for fewer tries.
        Assert.Equal(One, worked.AsT0.Pick.AsT0.Move.Id);
        Assert.Equal(6, worked.AsT0.Pick.AsT0.Score.Worked);
    }

    [Fact]
    public async Task ReportTurn_WhileAMoveIsBeingJudged_PicksNothingNew()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);
        await sessions.ReportTurn(Learner, Session, [LeansFirst], CancellationToken.None);

        var reported = await sessions.ReportTurn(Learner, Session, [LeansFirst], CancellationToken.None);

        Assert.Equal(One, reported.AsT0.Pick.AsT2.MoveId);
        Assert.Equal(1, reported.AsT0.Judgment.AsT1.TurnsLeft);
    }

    [Fact]
    public async Task GetBelief_AfterTurns_GivesTheReplayedBelief()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);
        var reported = await sessions.ReportTurn(Learner, Session, [LeansFirst], CancellationToken.None);

        var belief = await sessions.GetBelief(Learner, Session, CancellationToken.None);

        Assert.Equal(reported.AsT0.Belief.All[0].Odds, belief.AsT0.All[0].Odds, 10);
    }

    [Fact]
    public async Task ReportTurn_StartedUnderAnotherVocabulary_GivesOtherVocabulary()
    {
        var store = InMemoryHistoryStore.Create();
        using var before = Create(services => services.AddSingleton<IHistoryStore>(store));
        using var after = Create(services => services.AddSingleton<IHistoryStore>(store), otherDescription: "Reads differently.");
        await before.GetRequiredService<SessionEngine>().StartSession(Learner, Session, CancellationToken.None);
        var sessions = after.GetRequiredService<SessionEngine>();

        var reported = await sessions.ReportTurn(Learner, Session, [LeansFirst], CancellationToken.None);
        var belief = await sessions.GetBelief(Learner, Session, CancellationToken.None);

        Assert.True(reported.IsT3);
        Assert.Equal(after.GetRequiredService<VocabularyFingerprint>().Value, reported.AsT3.Expected);
        Assert.Equal(before.GetRequiredService<VocabularyFingerprint>().Value, reported.AsT3.Found);
        Assert.True(belief.IsT2);
        Assert.Single(await store.Load(Learner, Session, CancellationToken.None));
    }
}
