using System.Collections.Immutable;
using LearnEngine.Core.History;
using LearnEngine.Core.Learning;
using LearnEngine.Core.Odds;
using LearnEngine.Core.Sessions;
using Microsoft.Extensions.DependencyInjection;
using OneOf;
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
        var history = (await histories.Load(Learner, Session, CancellationToken.None)).AsT0;
        var start =Assert.IsType<SessionStarted>(Assert.Single(history));
        Assert.Equal(provider.GetRequiredService<VocabularyFingerprint>().Value, start.Fingerprint);
    }

    [Fact]
    public async Task StartSession_AlreadyStarted_KeepsTheHistory()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        var histories = provider.GetRequiredService<IHistoryStore>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);
        await sessions.ReportTurn(Learner, Session, [LeansFirst], null, null, CancellationToken.None);
        var before = (await histories.Load(Learner, Session, CancellationToken.None)).AsT0;

        var started = await sessions.StartSession(Learner, Session, CancellationToken.None);

        Assert.True(started.IsT1);
        Assert.Equal(SessionAlreadyStarted.Create(Learner, Session), started.AsT1);
        Assert.Equal(before.Length, (await histories.Load(Learner, Session, CancellationToken.None)).AsT0.Length);
    }

    [Fact]
    public async Task ReportTurn_NotStarted_GivesSessionNotStarted()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();

        var reported = await sessions.ReportTurn(Learner, Session, [LeansFirst], null, null, CancellationToken.None);
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

        var reported = await sessions.ReportTurn(Learner, Session, [LeansFirst, "not.a.signal"], null, null, CancellationToken.None);

        Assert.True(reported.IsT1);
        Assert.Equal("not.a.signal", Assert.Single(reported.AsT1).Id);
        Assert.Single((await histories.Load(Learner, Session, CancellationToken.None)).AsT0);
    }

    [Fact]
    public async Task ReportTurn_TopicAndMoveMade_KeepsThemInTheTurn()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        var histories = provider.GetRequiredService<IHistoryStore>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);

        var reported = await sessions.ReportTurn(Learner, Session, [LeansFirst], "recursion.base_case", Two, CancellationToken.None);

        Assert.True(reported.IsT0);
        var history = (await histories.Load(Learner, Session, CancellationToken.None)).AsT0;
        var turn = Assert.IsType<LearnerTurn>(history[1]);
        Assert.Equal("recursion.base_case", turn.Topic);
        Assert.Equal(Two, turn.MoveMade);
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("../up")]
    [InlineData("tópico")]
    public async Task ReportTurn_InvalidTopic_GivesInvalidTopicAndKeepsNothing(string topic)
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        var histories = provider.GetRequiredService<IHistoryStore>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);

        var reported = await sessions.ReportTurn(Learner, Session, [LeansFirst], topic, null, CancellationToken.None);

        Assert.True(reported.IsT5);
        Assert.Equal(topic, reported.AsT5.Topic);
        Assert.Single((await histories.Load(Learner, Session, CancellationToken.None)).AsT0);
    }

    [Fact]
    public async Task ReportTurn_UnknownMoveMade_GivesUnknownMoveAndKeepsNothing()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        var histories = provider.GetRequiredService<IHistoryStore>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);

        var reported = await sessions.ReportTurn(Learner, Session, [LeansFirst], null, "not_a_move", CancellationToken.None);

        Assert.True(reported.IsT6);
        Assert.Equal("not_a_move", reported.AsT6.Id);
        Assert.Single((await histories.Load(Learner, Session, CancellationToken.None)).AsT0);
    }

    [Fact]
    public async Task ReportTurn_ClearState_PicksAMoveAndKeepsIt()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        var histories = provider.GetRequiredService<IHistoryStore>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);

        var reported = await sessions.ReportTurn(Learner, Session, [LeansFirst], null, null, CancellationToken.None);

        Assert.True(reported.IsT0);
        Assert.Equal(First, reported.AsT0.Belief.Leader.StateId);
        Assert.True(reported.AsT0.Belief.IsClear);
        Assert.Equal(One, reported.AsT0.Pick.AsT0.Move.Id);
        Assert.True(reported.AsT0.Judgment.IsT2);
        var history = (await histories.Load(Learner, Session, CancellationToken.None)).AsT0;
        Assert.Collection(history,
            historyEvent => Assert.IsType<SessionStarted>(historyEvent),
            historyEvent => Assert.IsType<LearnerTurn>(historyEvent),
            historyEvent => Assert.IsType<TurnLearned>(historyEvent),
            historyEvent => Assert.Equal(One, Assert.IsType<MovePicked>(historyEvent).MoveId));
    }

    [Fact]
    public async Task ReportTurn_NewLearner_GivesTheOddsOfTheSharedRates()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        var engine = provider.GetRequiredService<OddsEngine>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);

        var reported = await sessions.ReportTurn(Learner, Session, [LeansFirst], null, null, CancellationToken.None);

        var today = engine.Apply(engine.Fade(engine.Start(), OddsOptions.DefaultFade), LearnerModel.Empty, [LeansFirst]).AsT0;
        Assert.Equal(today.All[0].Odds, reported.AsT0.Belief.All[0].Odds, 10);
        Assert.Equal(today.All[1].Odds, reported.AsT0.Belief.All[1].Odds, 10);
    }

    [Fact]
    public async Task ReportTurn_AddsTheOddsAfterTheTurnToTheLearnersCountsAndKeepsThem()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        var histories = provider.GetRequiredService<IHistoryStore>();
        var models = provider.GetRequiredService<ILearnerModelStore>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);

        var reported = await sessions.ReportTurn(Learner, Session, [LeansFirst], null, null, CancellationToken.None);

        var odds = reported.AsT0.Belief.All;
        var model = await models.Load(Learner, CancellationToken.None);
        var shownInFirst = Assert.Single(model.Rates, count => count.SignalId == LeansFirst && count.StateId == First);
        var notShownInSecond = Assert.Single(model.Rates, count => count.SignalId == LeansSecond && count.StateId == Second);
        Assert.Equal(odds[0].Odds, shownInFirst.Shown, 10);
        Assert.Equal(0, shownInFirst.NotShown, 10);
        Assert.Equal(0, notShownInSecond.Shown, 10);
        Assert.Equal(odds[1].Odds, notShownInSecond.NotShown, 10);
        Assert.Equal(4 * 2, model.Rates.Length);

        var history = (await histories.Load(Learner, Session, CancellationToken.None)).AsT0;
        var learned = Assert.IsType<TurnLearned>(history[2]);
        Assert.Equal(odds.Select(stateOdds => stateOdds.Odds), learned.Odds.Select(stateOdds => stateOdds.Odds));
        Assert.Equal(model.Rates.Length, learned.Added.Rates.Length);
    }

    [Fact]
    public async Task ReportTurn_SecondTurn_UsesTheRatesLearnedFromTheFirst()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        var engine = provider.GetRequiredService<OddsEngine>();
        var histories = provider.GetRequiredService<IHistoryStore>();
        var replayer = provider.GetRequiredService<HistoryReplayer>();
        var models = provider.GetRequiredService<ILearnerModelStore>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);
        await sessions.ReportTurn(Learner, Session, [LeansSecond], null, null, CancellationToken.None);
        var model = await models.Load(Learner, CancellationToken.None);
        var afterFirst = replayer.Replay((await histories.Load(Learner, Session, CancellationToken.None)).AsT0).AsT0;

        var second = await sessions.ReportTurn(Learner, Session, [LeansSecond], null, null, CancellationToken.None);
        var replayed = await sessions.GetBelief(Learner, Session, CancellationToken.None);

        var learnt = engine.Apply(engine.Fade(afterFirst, OddsOptions.DefaultFade), model, [LeansSecond]).AsT0;
        var shared = engine.Apply(engine.Fade(afterFirst, OddsOptions.DefaultFade), LearnerModel.Empty, [LeansSecond]).AsT0;
        Assert.Equal(learnt.All[0].Odds, second.AsT0.Belief.All[0].Odds, 10);
        Assert.NotEqual(shared.All[0].Odds, second.AsT0.Belief.All[0].Odds, 10);
        Assert.Equal(second.AsT0.Belief.All[0].Odds, replayed.AsT0.All[0].Odds, 10);
    }

    [Fact]
    public async Task ReportTurn_NoClearLeader_PicksNothing()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);

        // No signals at all puts the two states close together.
        var reported = await sessions.ReportTurn(Learner, Session, [], null, null, CancellationToken.None);

        Assert.False(reported.AsT0.Belief.IsClear);
        Assert.True(reported.AsT0.Pick.IsT1);
    }

    [Fact]
    public async Task ReportTurn_AfterAPick_JudgesItAndUpdatesTheScore()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);
        await sessions.ReportTurn(Learner, Session, [LeansFirst], null, null, CancellationToken.None);

        var worked = await sessions.ReportTurn(Learner, Session, [LeansFirst, WentWell], null, null, CancellationToken.None);

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
        await sessions.ReportTurn(Learner, Session, [LeansFirst], null, null, CancellationToken.None);

        var reported = await sessions.ReportTurn(Learner, Session, [LeansFirst], null, null, CancellationToken.None);

        Assert.Equal(One, reported.AsT0.Pick.AsT2.MoveId);
        Assert.Equal(1, reported.AsT0.Judgment.AsT1.TurnsLeft);
    }

    [Fact]
    public async Task GetBelief_AfterTurns_GivesTheReplayedBelief()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);
        var reported = await sessions.ReportTurn(Learner, Session, [LeansFirst], null, null, CancellationToken.None);

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

        var reported = await sessions.ReportTurn(Learner, Session, [LeansFirst], null, null, CancellationToken.None);
        var belief = await sessions.GetBelief(Learner, Session, CancellationToken.None);

        Assert.True(reported.IsT3);
        Assert.Equal(after.GetRequiredService<VocabularyFingerprint>().Value, reported.AsT3.Expected);
        Assert.Equal(before.GetRequiredService<VocabularyFingerprint>().Value, reported.AsT3.Found);
        Assert.True(belief.IsT2);
        Assert.Single((await store.Load(Learner, Session, CancellationToken.None)).AsT0);
    }

    [Fact]
    public async Task HistoryCannotBeRead_GivesBrokenHistoryAndKeepsNothing()
    {
        var store = UnreadableHistoryStore.Create();
        using var provider = Create(services => services.AddSingleton<IHistoryStore>(store));
        var sessions = provider.GetRequiredService<SessionEngine>();

        var started = await sessions.StartSession(Learner, Session, CancellationToken.None);
        var reported = await sessions.ReportTurn(Learner, Session, [LeansFirst], null, null, CancellationToken.None);
        var belief = await sessions.GetBelief(Learner, Session, CancellationToken.None);

        Assert.True(started.IsT1);
        Assert.True(reported.IsT4);
        Assert.Equal(UnreadableHistoryStore.Broken, reported.AsT4);
        Assert.True(belief.IsT3);
        Assert.Equal(0, store.Appended);
    }

    /// <summary>A store whose kept events can no longer be read back, as after an event changed shape.</summary>
    private sealed class UnreadableHistoryStore : IHistoryStore
    {
        public static readonly BrokenHistory Broken = BrokenHistory.Create(0, "The event cannot be read.");

        private UnreadableHistoryStore() { }

        public int Appended { get; private set; }

        public static UnreadableHistoryStore Create() => new();

        public Task Append(string learnerId, string sessionId, HistoryEvent historyEvent, CancellationToken cancellationToken)
        {
            Appended++;
            return Task.CompletedTask;
        }

        public Task<OneOf<ImmutableArray<HistoryEvent>, BrokenHistory>> Load(string learnerId, string sessionId, CancellationToken cancellationToken) =>
            Task.FromResult<OneOf<ImmutableArray<HistoryEvent>, BrokenHistory>>(Broken);
    }
}
