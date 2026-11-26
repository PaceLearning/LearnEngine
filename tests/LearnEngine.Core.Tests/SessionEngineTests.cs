using System.Collections.Immutable;
using LearnEngine.Core.Choosing;
using LearnEngine.Core.History;
using LearnEngine.Core.Learning;
using LearnEngine.Core.Moves;
using LearnEngine.Core.Odds;
using LearnEngine.Core.Registration;
using LearnEngine.Core.Sessions;
using LearnEngine.Core.Signals;
using LearnEngine.Core.Topics;
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

        var belief = Assert.IsType<SessionBelief>(started.Value);
        Assert.Equal(FirstPrior, belief.All[0].Odds, 10);
        Assert.True(belief.IsClear);
        var history = Assert.IsType<ImmutableArray<HistoryEvent>>((await histories.Load(Learner, Session, CancellationToken.None)).Value);
        Assert.Collection(history,
            historyEvent => Assert.Equal(provider.GetRequiredService<VocabularyFingerprint>().Value, Assert.IsType<SessionStarted>(historyEvent).Fingerprint),
            historyEvent => Assert.Null(Assert.IsType<TurnExpected>(historyEvent).Expected.MoveId));
    }

    [Fact]
    public async Task StartSession_AlreadyStarted_KeepsTheHistory()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        var histories = provider.GetRequiredService<IHistoryStore>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);
        await sessions.ReportTurn(Learner, Session, [LeansFirst], null, null, CancellationToken.None);
        var before = Assert.IsType<ImmutableArray<HistoryEvent>>((await histories.Load(Learner, Session, CancellationToken.None)).Value);

        var started = await sessions.StartSession(Learner, Session, CancellationToken.None);

        Assert.Equal(SessionAlreadyStarted.Create(Learner, Session), Assert.IsType<SessionAlreadyStarted>(started.Value));
        Assert.Equal(before.Length, Assert.IsType<ImmutableArray<HistoryEvent>>((await histories.Load(Learner, Session, CancellationToken.None)).Value).Length);
    }

    [Fact]
    public async Task ReportTurn_NotStarted_GivesSessionNotStarted()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();

        var reported = await sessions.ReportTurn(Learner, Session, [LeansFirst], null, null, CancellationToken.None);
        var belief = await sessions.GetBelief(Learner, Session, CancellationToken.None);

        Assert.Equal(SessionNotStarted.Create(Learner, Session), Assert.IsType<SessionNotStarted>(reported.Value));
        Assert.Equal(SessionNotStarted.Create(Learner, Session), Assert.IsType<SessionNotStarted>(belief.Value));
    }

    [Fact]
    public async Task ReportTurn_UnknownSignal_GivesItAndKeepsNothing()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        var histories = provider.GetRequiredService<IHistoryStore>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);

        var reported = await sessions.ReportTurn(Learner, Session, [LeansFirst, "not.a.signal"], null, null, CancellationToken.None);

        var unknownSignals = Assert.IsType<ImmutableArray<UnknownSignal>>(reported.Value);
        Assert.Equal("not.a.signal", Assert.Single(unknownSignals).Id);
        Assert.DoesNotContain(Assert.IsType<ImmutableArray<HistoryEvent>>((await histories.Load(Learner, Session, CancellationToken.None)).Value), historyEvent => historyEvent is LearnerTurn);
    }

    [Fact]
    public async Task ReportTurn_TopicAndMoveMade_KeepsThemInTheTurn()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        var histories = provider.GetRequiredService<IHistoryStore>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);

        var reported = await sessions.ReportTurn(Learner, Session, [LeansFirst], "recursion.base_case", Two, CancellationToken.None);

        Assert.True(reported is TurnResult);
        var history = Assert.IsType<ImmutableArray<HistoryEvent>>((await histories.Load(Learner, Session, CancellationToken.None)).Value);
        var turn = Assert.IsType<LearnerTurn>(history[2]);
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

        Assert.Equal(topic, Assert.IsType<InvalidTopic>(reported.Value).Topic);
        Assert.DoesNotContain(Assert.IsType<ImmutableArray<HistoryEvent>>((await histories.Load(Learner, Session, CancellationToken.None)).Value), historyEvent => historyEvent is LearnerTurn);
    }

    [Fact]
    public async Task ReportTurn_UnknownMoveMade_GivesUnknownMoveAndKeepsNothing()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        var histories = provider.GetRequiredService<IHistoryStore>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);

        var reported = await sessions.ReportTurn(Learner, Session, [LeansFirst], null, "not_a_move", CancellationToken.None);

        Assert.Equal("not_a_move", Assert.IsType<UnknownMove>(reported.Value).Id);
        Assert.DoesNotContain(Assert.IsType<ImmutableArray<HistoryEvent>>((await histories.Load(Learner, Session, CancellationToken.None)).Value), historyEvent => historyEvent is LearnerTurn);
    }

    [Fact]
    public async Task ReportTurn_ClearState_PicksAMoveAndKeepsIt()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        var histories = provider.GetRequiredService<IHistoryStore>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);

        var reported = await sessions.ReportTurn(Learner, Session, [LeansFirst], null, null, CancellationToken.None);

        var result = Assert.IsType<TurnResult>(reported.Value);
        Assert.Equal(First, result.Belief.Leader.StateId);
        Assert.True(result.Belief.IsClear);
        var pick = Assert.IsType<PickedMove>(result.Pick.Value);
        Assert.Equal(One, pick.Move.Id);
        var history = Assert.IsType<ImmutableArray<HistoryEvent>>((await histories.Load(Learner, Session, CancellationToken.None)).Value);
        Assert.Collection(history,
            historyEvent => Assert.IsType<SessionStarted>(historyEvent),
            historyEvent => Assert.IsType<TurnExpected>(historyEvent),
            historyEvent => Assert.IsType<LearnerTurn>(historyEvent),
            historyEvent => Assert.IsType<TurnLearned>(historyEvent),
            historyEvent =>
            {
                var picked = Assert.IsType<MovePicked>(historyEvent);
                Assert.Equal(One, picked.MoveId);
                Assert.Equal(pick.GoalChance, picked.GoalChance, 10);
                Assert.Equal(pick.Why, picked.Why);
            },
            historyEvent => Assert.Equal(One, Assert.IsType<TurnExpected>(historyEvent).Expected.MoveId));
    }

    [Fact]
    public async Task ReportTurn_NewLearner_GivesTheOddsOfTheSharedRates()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        var engine = provider.GetRequiredService<OddsEngine>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);

        var reported = await sessions.ReportTurn(Learner, Session, [LeansFirst], null, null, CancellationToken.None);

        var today = Assert.IsType<Belief>(engine.Apply(engine.Predict(engine.Start(), LearnerModel.Empty, null), LearnerModel.Empty, [LeansFirst]).Value);
        var result = Assert.IsType<TurnResult>(reported.Value);
        Assert.Equal(today.All[0].Odds, result.Belief.All[0].Odds, 10);
        Assert.Equal(today.All[1].Odds, result.Belief.All[1].Odds, 10);
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

        var odds = Assert.IsType<TurnResult>(reported.Value).Belief.All;
        var model = await models.Load(Learner, CancellationToken.None);
        var shownInFirst = Assert.Single(model.Rates, count => count.SignalId == LeansFirst && count.StateId == First);
        var notShownInSecond = Assert.Single(model.Rates, count => count.SignalId == LeansSecond && count.StateId == Second);
        Assert.Equal(odds[0].Odds, shownInFirst.Shown, 10);
        Assert.Equal(0, shownInFirst.NotShown, 10);
        Assert.Equal(0, notShownInSecond.Shown, 10);
        Assert.Equal(odds[1].Odds, notShownInSecond.NotShown, 10);
        Assert.Equal(4 * 2, model.Rates.Length);

        var history = Assert.IsType<ImmutableArray<HistoryEvent>>((await histories.Load(Learner, Session, CancellationToken.None)).Value);
        var learned = Assert.IsType<TurnLearned>(history[3]);
        Assert.Equal(odds.Select(stateOdds => stateOdds.Odds), learned.Odds.Select(stateOdds => stateOdds.Odds));
        Assert.Equal(model.Rates.Length, learned.Added.Rates.Length);
    }

    [Fact]
    public async Task ReportTurn_MoveMade_AddsEveryStepUnderItAndTheStepsAddUpToTheOddsAfter()
    {
        using var provider = Create(after: services => services
            .AddMoveEffect(First, One, weight: 10, (First, 0.2), (Second, 0.8)));
        var sessions = provider.GetRequiredService<SessionEngine>();
        var models = provider.GetRequiredService<ILearnerModelStore>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);

        var reported = await sessions.ReportTurn(Learner, Session, [LeansSecond], null, One, CancellationToken.None);

        var model = await models.Load(Learner, CancellationToken.None);
        Assert.Equal(2 * 2, model.Effects.Length);
        Assert.All(model.Effects, count => Assert.Equal(One, count.MoveId));
        Assert.Equal(1, model.Effects.Sum(count => count.Count), 10);
        Assert.Equal(
            Assert.IsType<TurnResult>(reported.Value).Belief.All[1].Odds,
            model.Effects.Where(count => count.ToStateId == Second).Sum(count => count.Count),
            10);

        // Where the learner was before splits as the priors and what the step from each state says of the turn's signals.
        var fromFirst = FirstPrior * 0.8;
        var fromSecond = SecondPrior * (1 - OddsOptions.DefaultFade + OddsOptions.DefaultFade * SecondPrior);
        var intoSecond = model.Effects.Where(count => count.ToStateId == Second).ToList();
        Assert.Equal(
            fromFirst / (fromFirst + fromSecond),
            intoSecond.Single(count => count.FromStateId == First).Count / intoSecond.Sum(count => count.Count),
            10);
    }

    [Fact]
    public async Task ReportTurn_NoMoveMade_GivesTheOddsOfTodaysFadeForANewLearner()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        var models = provider.GetRequiredService<ILearnerModelStore>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);

        var reported = await sessions.ReportTurn(Learner, Session, [LeansFirst], null, null, CancellationToken.None);

        var fadedFirst = FirstPrior;
        var first = fadedFirst * 0.5 * 0.8;
        var second = (1 - fadedFirst) * 0.1 * 0.6;
        Assert.Equal(first / (first + second), Assert.IsType<TurnResult>(reported.Value).Belief.All[0].Odds, 10);
        var model = await models.Load(Learner, CancellationToken.None);
        Assert.All(model.Effects, count => Assert.Null(count.MoveId));
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
        var history = Assert.IsType<ImmutableArray<HistoryEvent>>((await histories.Load(Learner, Session, CancellationToken.None)).Value);
        var afterFirst = Assert.IsType<Belief>(replayer.Replay(history).Value);

        var second = Assert.IsType<TurnResult>((await sessions.ReportTurn(Learner, Session, [LeansSecond], null, null, CancellationToken.None)).Value);
        var replayed = Assert.IsType<SessionBelief>((await sessions.GetBelief(Learner, Session, CancellationToken.None)).Value);

        var learnt = Assert.IsType<Belief>(engine.Apply(engine.Predict(afterFirst, model, null), model, [LeansSecond]).Value);
        var shared = Assert.IsType<Belief>(engine.Apply(engine.Predict(afterFirst, LearnerModel.Empty, null), LearnerModel.Empty, [LeansSecond]).Value);
        Assert.Equal(learnt.All[0].Odds, second.Belief.All[0].Odds, 10);
        Assert.NotEqual(shared.All[0].Odds, second.Belief.All[0].Odds, 10);
        Assert.Equal(second.Belief.All[0].Odds, replayed.All[0].Odds, 10);
    }

    [Fact]
    public async Task ReportTurn_NoClearLeader_PicksNothing()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);

        // No signals at all puts the two states close together.
        var reported = await sessions.ReportTurn(Learner, Session, [], null, null, CancellationToken.None);

        var result = Assert.IsType<TurnResult>(reported.Value);
        Assert.False(result.Belief.IsClear);
        Assert.True(result.Pick is NoClearLeader);
    }

    [Fact]
    public async Task ReportTurn_EveryTurnInAClearState_PicksAMove()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);
        await sessions.ReportTurn(Learner, Session, [LeansFirst], null, null, CancellationToken.None);

        var reported = await sessions.ReportTurn(Learner, Session, [LeansFirst], null, One, CancellationToken.None);

        Assert.True(Assert.IsType<TurnResult>(reported.Value).Pick is PickedMove);
    }

    [Fact]
    public async Task GetBelief_AfterTurns_GivesTheReplayedBelief()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);
        var reported = await sessions.ReportTurn(Learner, Session, [LeansFirst], null, null, CancellationToken.None);

        var belief = await sessions.GetBelief(Learner, Session, CancellationToken.None);

        Assert.Equal(Assert.IsType<TurnResult>(reported.Value).Belief.All[0].Odds, Assert.IsType<SessionBelief>(belief.Value).All[0].Odds, 10);
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

        var other = Assert.IsType<OtherVocabulary>(reported.Value);
        Assert.Equal(after.GetRequiredService<VocabularyFingerprint>().Value, other.Expected);
        Assert.Equal(before.GetRequiredService<VocabularyFingerprint>().Value, other.Found);
        Assert.True(belief is OtherVocabulary);
        Assert.DoesNotContain(Assert.IsType<ImmutableArray<HistoryEvent>>((await store.Load(Learner, Session, CancellationToken.None)).Value), historyEvent => historyEvent is LearnerTurn);
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

        Assert.True(started is SessionAlreadyStarted);
        Assert.Equal(UnreadableHistoryStore.Broken, Assert.IsType<BrokenHistory>(reported.Value));
        Assert.True(belief is BrokenHistory);
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

        public Task<Outcome<ImmutableArray<HistoryEvent>, BrokenHistory>> Load(string learnerId, string sessionId, CancellationToken cancellationToken) =>
            Task.FromResult<Outcome<ImmutableArray<HistoryEvent>, BrokenHistory>>(Broken);
    }
}
