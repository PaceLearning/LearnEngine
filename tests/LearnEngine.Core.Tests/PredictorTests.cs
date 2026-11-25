using System.Collections.Immutable;
using LearnEngine.Core.Effects;
using LearnEngine.Core.History;
using LearnEngine.Core.Learning;
using LearnEngine.Core.Likelihoods;
using LearnEngine.Core.Odds;
using LearnEngine.Core.Prediction;
using LearnEngine.Core.Registration;
using LearnEngine.Core.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static LearnEngine.Core.Tests.TestVocabulary;

namespace LearnEngine.Core.Tests;

/// <summary>What the engine expects of the next turn, how unexpected a turn was, and the expectations kept in a history.</summary>
public sealed class PredictorTests
{
    private const string Learner = "learner";
    private const string Session = "session";
    private const int Precision = 10;

    [Fact]
    public void Expect_FromThePriorsWithNoMove_GivesTheHandComputedSumOfEachSignal()
    {
        using var provider = Create();
        var predictor = provider.GetRequiredService<Predictor>();
        var engine = provider.GetRequiredService<OddsEngine>();

        var expected = predictor.Expect(engine.Start(), LearnerModel.Empty, null, null);

        // No move from the priors fades back to the priors, so each chance is the priors times the shared rates.
        Assert.Null(expected.MoveId);
        Assert.Equal([LeansFirst, LeansSecond, WentWell, WentBadly], expected.Signals.Select(chance => chance.SignalId));
        Assert.Equal(0.6 * 0.5 + 0.4 * 0.1, expected.Signals[0].Chance, Precision);
        Assert.Equal(0.6 * 0.2 + 0.4 * 0.4, expected.Signals[1].Chance, Precision);
        Assert.Equal(0.3, expected.Signals[2].Chance, Precision);
        Assert.Equal(FirstPrior, expected.Odds[0].Odds, Precision);
        Assert.Empty(expected.Topics);
    }

    [Fact]
    public void Expect_FromThePriorsWithNoMove_GivesUsualChancesAndNoSignalAboveUsual()
    {
        using var provider = Create();
        var predictor = provider.GetRequiredService<Predictor>();
        var engine = provider.GetRequiredService<OddsEngine>();

        var expected = predictor.Expect(engine.Start(), LearnerModel.Empty, null, null);

        // Nothing is known about where the learner is, so every chance is its usual one.
        Assert.All(expected.Signals, chance => Assert.Equal(chance.Usual, chance.Chance, Precision));
        Assert.Equal(0.6 * 0.5 + 0.4 * 0.1, expected.Signals[0].Usual, Precision);
        Assert.Empty(expected.AboveUsual);
    }

    [Fact]
    public void Expect_LearnerClearlyInTheSecondState_IsHighlyConfidentAndListsOnlySignalsAboveUsual()
    {
        using var provider = Create();
        var predictor = provider.GetRequiredService<Predictor>();
        var engine = provider.GetRequiredService<OddsEngine>();
        var once = engine.Apply(engine.Start(), LearnerModel.Empty, [LeansSecond]).AsT0;
        var twice = engine.Apply(engine.Predict(once, LearnerModel.Empty, null), LearnerModel.Empty, [LeansSecond]).AsT0;

        var expected = predictor.Expect(twice, LearnerModel.Empty, null, null);

        Assert.True(expected.Odds[1].Odds - expected.Odds[0].Odds >= Belief.HighConfidenceMargin);
        Assert.Equal(Confidence.High, expected.Confidence(OddsOptions.DefaultClearMargin));
        Assert.NotEmpty(expected.AboveUsual);
        Assert.All(expected.AboveUsual, chance => Assert.True(chance.VsUsual > 1));
        Assert.Equal(expected.AboveUsual.Select(chance => chance.VsUsual).OrderDescending(), expected.AboveUsual.Select(chance => chance.VsUsual));
        Assert.Equal(LeansSecond, expected.AboveUsual[0].SignalId);
        Assert.Equal(0.6 * 0.2 + 0.4 * 0.4, expected.AboveUsual[0].Usual, Precision);
        Assert.Equal(expected.AboveUsual[0].Chance / expected.AboveUsual[0].Usual, expected.AboveUsual[0].VsUsual, Precision);
        // went.well and went.badly have the same rate in both states, so they are always as likely as usual.
        Assert.Equal([LeansSecond], expected.AboveUsual.Select(chance => chance.SignalId));
    }

    [Fact]
    public void Read_ExpectationKeptBeforeUsualChances_ReadsWithNoSignalAboveUsual()
    {
        const string line = """{"event":"turn_expected","expected":{"move_id":null,"odds":[{"state_id":"first","odds":1}],"signals":[{"signal_id":"leans.first","chance":0.5}],"topics":[]}}""";

        var read = HistoryEventJson.Read(line);

        var expected = Assert.IsType<TurnExpected>(read.AsT0).Expected;
        Assert.Equal(0, Assert.Single(expected.Signals).Usual);
        Assert.Empty(expected.AboveUsual);
    }

    [Fact]
    public async Task ReportTurn_ThirdTurn_ExpectsTheSumOfThePredictedOddsTimesTheLearnersRates()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        var effects = provider.GetRequiredService<MoveEffectCatalog>();
        var likelihoods = provider.GetRequiredService<LikelihoodTable>();
        var models = provider.GetRequiredService<ILearnerModelStore>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);
        await sessions.ReportTurn(Learner, Session, [LeansFirst], null, null, CancellationToken.None);

        var second = await sessions.ReportTurn(Learner, Session, [LeansSecond], null, null, CancellationToken.None);

        // The expectation of the third turn, worked out by hand from the odds after the second, the move picked and the
        // learner's model with both turns added.
        var model = await models.Load(Learner, CancellationToken.None);
        var moveNext = second.AsT0.Pick.IsT0 ? second.AsT0.Pick.AsT0.Move.Id : null;
        var odds = second.AsT0.Belief.All;
        var fromFirst = effects.For(First, moveNext);
        var fromSecond = effects.For(Second, moveNext);
        var predictedFirst = odds[0].Odds * model.Effect(fromFirst, First) + odds[1].Odds * model.Effect(fromSecond, First);
        var predictedSecond = odds[0].Odds * model.Effect(fromFirst, Second) + odds[1].Odds * model.Effect(fromSecond, Second);
        var rateInFirst = model.Rate(likelihoods.Find(LeansFirst, First).AsT0, LearningOptions.DefaultRateWeight);
        var rateInSecond = model.Rate(likelihoods.Find(LeansFirst, Second).AsT0, LearningOptions.DefaultRateWeight);
        Assert.Equal(predictedFirst, second.AsT0.Expected.Odds[0].Odds, Precision);
        Assert.Equal(predictedFirst * rateInFirst + predictedSecond * rateInSecond, second.AsT0.Expected.Signals[0].Chance, Precision);
        Assert.Equal(moveNext, second.AsT0.Expected.MoveId);
    }

    [Fact]
    public void Surprise_ExpectedTurn_IsLowerThanAnOddOne()
    {
        using var provider = Create();
        var predictor = provider.GetRequiredService<Predictor>();
        var engine = provider.GetRequiredService<OddsEngine>();
        var expected = predictor.Expect(engine.Start(), LearnerModel.Empty, null, null);

        var usual = Predictor.Surprise(expected, []);
        var odd = Predictor.Surprise(expected, [LeansSecond, WentWell, WentBadly]);

        Assert.Equal(-Math.Log((1 - 0.34) * (1 - 0.28) * (1 - 0.3) * (1 - 0.3)), usual, Precision);
        Assert.Equal(-Math.Log((1 - 0.34) * 0.28 * 0.3 * 0.3), odd, Precision);
        Assert.True(usual < odd);
    }

    [Fact]
    public void UsualSurprise_IsTheAverageSurpriseOfEverySignal()
    {
        using var provider = Create();
        var predictor = provider.GetRequiredService<Predictor>();
        var engine = provider.GetRequiredService<OddsEngine>();
        var expected = predictor.Expect(engine.Start(), LearnerModel.Empty, null, null);

        var usual = Predictor.UsualSurprise(expected);

        // The same four chances as above, each adding minus p log p minus (1 - p) log (1 - p).
        var byHand = new[] { 0.34, 0.28, 0.3, 0.3 }.Sum(chance => -(chance * Math.Log(chance) + (1 - chance) * Math.Log(1 - chance)));
        Assert.Equal(byHand, usual, Precision);
        Assert.True(Predictor.Surprise(expected, []) < usual);
        Assert.True(Predictor.Surprise(expected, [LeansSecond, WentWell, WentBadly]) > usual);
    }

    [Fact]
    public async Task ReportTurn_SurpriseIsReadAgainstTheKeptExpectation()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        var histories = provider.GetRequiredService<IHistoryStore>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);
        var kept = Assert.IsType<TurnExpected>((await histories.Load(Learner, Session, CancellationToken.None)).AsT0[1]);

        var reported = await sessions.ReportTurn(Learner, Session, [LeansSecond, WentWell], null, null, CancellationToken.None);

        Assert.Equal(Predictor.Surprise(kept.Expected, [LeansSecond, WentWell]), reported.AsT0.Surprise, Precision);
        var history = (await histories.Load(Learner, Session, CancellationToken.None)).AsT0;
        Assert.Equal(reported.AsT0.Expected.Signals.AsEnumerable(), Assert.IsType<TurnExpected>(history[^1]).Expected.Signals.AsEnumerable());
    }

    [Fact]
    public async Task ReportTurn_NoKeptExpectation_ReadsTheTurnAgainstOneWorkedOutNow()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        var histories = provider.GetRequiredService<IHistoryStore>();
        var predictor = provider.GetRequiredService<Predictor>();
        var engine = provider.GetRequiredService<OddsEngine>();
        var start = SessionStarted.Create(provider.GetRequiredService<VocabularyFingerprint>().Value, OddsOptions.DefaultFade, OddsOptions.DefaultClearMargin);
        await histories.Append(Learner, Session, start, CancellationToken.None);

        var reported = await sessions.ReportTurn(Learner, Session, [LeansFirst], null, null, CancellationToken.None);

        var expected = predictor.Expect(engine.Start(), LearnerModel.Empty, null, null);
        Assert.Equal(Predictor.Surprise(expected, [LeansFirst]), reported.AsT0.Surprise, Precision);
    }

    [Fact]
    public async Task ReportTurn_Topics_AddsLinksAndStatesAndExpectsTheLinkedTopicsAndStayingOnTheLastOne()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        var models = provider.GetRequiredService<ILearnerModelStore>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);

        TurnResult? last = null;
        foreach (var topic in new[] { "a", "b", "a", "b", "a" })
            last = (await sessions.ReportTurn(Learner, Session, [LeansFirst], topic, null, CancellationToken.None)).AsT0;

        var model = await models.Load(Learner, CancellationToken.None);
        Assert.Equal(["a", "b"], model.Topics.AsEnumerable());
        Assert.Equal(2, Assert.Single(model.TopicLinks, count => count.FromTopic == "a" && count.ToTopic == "b").Count, Precision);
        Assert.Equal(2, Assert.Single(model.TopicLinks, count => count.FromTopic == "b" && count.ToTopic == "a").Count, Precision);
        Assert.Equal(3, model.TopicStates.Where(count => count.Topic == "a").Sum(count => count.Count), Precision);

        // From a: twice to b, and one for staying on a. Each also gets the come back bonus for its share of turns not
        // in the goal state, which is the first state.
        var bWeight = 2 + Predictor.ComeBackWeight * model.TopicStates.Where(count => count.Topic == "b" && count.StateId == Second).Sum(count => count.Count) / 2;
        var aWeight = 1 + Predictor.ComeBackWeight * model.TopicStates.Where(count => count.Topic == "a" && count.StateId == Second).Sum(count => count.Count) / 3;
        var topics = last!.Expected.Topics;
        Assert.Equal(["b", "a"], topics.Select(chance => chance.Topic));
        Assert.Equal(bWeight / (aWeight + bWeight), topics[0].Chance, Precision);
        Assert.Equal(aWeight / (aWeight + bWeight), topics[1].Chance, Precision);
    }

    [Fact]
    public void Expect_TopicNotInAGoalState_GetsTheComeBackBonus()
    {
        using var provider = Create();
        var predictor = provider.GetRequiredService<Predictor>();
        var engine = provider.GetRequiredService<OddsEngine>();
        var model = LearnerModel.Create([], [], [], [
            TopicStateCount.Create("hard", Second, 1),
            TopicStateCount.Create("easy", First, 1)]);

        var expected = predictor.Expect(engine.Start(), model, null, "easy");

        // easy: 1 for staying and no bonus, since it was in the goal state. hard: half a link, never in the goal state.
        Assert.Equal(["easy", "hard"], expected.Topics.Select(chance => chance.Topic));
        Assert.Equal(1 / 1.5, expected.Topics[0].Chance, Precision);
        Assert.Equal(0.5 / 1.5, expected.Topics[1].Chance, Precision);
    }

    [Fact]
    public async Task GetBelief_AfterTheModelChanged_ReadsBackTheOddsTheHistoryKept()
    {
        using var provider = Create();
        var sessions = provider.GetRequiredService<SessionEngine>();
        var histories = provider.GetRequiredService<IHistoryStore>();
        var replayer = provider.GetRequiredService<HistoryReplayer>();
        var models = provider.GetRequiredService<ILearnerModelStore>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);
        var reported = new List<ImmutableArray<StateOdds>>();
        foreach (var signals in new[] { new[] { LeansSecond }, [LeansSecond], [LeansFirst] })
            reported.Add((await sessions.ReportTurn(Learner, Session, signals, null, null, CancellationToken.None)).AsT0.Belief.All);

        // A model moved far since, which would give other odds if the session were worked out again with it.
        await models.Add(Learner, LearnerModelChange.Create([RateCount.Create(LeansFirst, Second, 500, 0)], []), CancellationToken.None);
        var belief = await sessions.GetBelief(Learner, Session, CancellationToken.None);
        var history = (await histories.Load(Learner, Session, CancellationToken.None)).AsT0;

        Assert.Equal(reported[^1].Select(stateOdds => stateOdds.Odds), belief.AsT0.All.Select(stateOdds => stateOdds.Odds));
        ImmutableArray<int> kept = [.. history.Select((historyEvent, index) => historyEvent is TurnLearned ? index : -1).Where(index => index >= 0)];
        Assert.Equal(3, kept.Length);
        for (var turn = 0; turn < kept.Length; turn++)
            Assert.Equal(reported[turn].Select(stateOdds => stateOdds.Odds), replayer.Replay(history[..(kept[turn] + 1)]).AsT0.All.Select(stateOdds => stateOdds.Odds));
    }

    [Fact]
    public void Replay_TwoExpectationsBeforeATurn_GivesBrokenHistoryAtTheSecond()
    {
        using var provider = Create();
        var replayer = provider.GetRequiredService<HistoryReplayer>();
        var predictor = provider.GetRequiredService<Predictor>();
        var engine = provider.GetRequiredService<OddsEngine>();
        var fingerprint = provider.GetRequiredService<VocabularyFingerprint>().Value;
        var expected = TurnExpected.Create(predictor.Expect(engine.Start(), LearnerModel.Empty, null, null));

        var twice = replayer.Replay([SessionStarted.Create(fingerprint, 0.15, 0.15), expected, expected]);
        var once = replayer.Replay([SessionStarted.Create(fingerprint, 0.15, 0.15), expected, LearnerTurn.Create([]), expected]);

        Assert.Equal(2, twice.AsT2.EventIndex);
        Assert.True(once.IsT0);
    }

    [Fact]
    public void Replay_ExpectationNotOfTheRegisteredStatesOrSignals_GivesBrokenHistory()
    {
        using var provider = Create();
        var replayer = provider.GetRequiredService<HistoryReplayer>();
        var fingerprint = provider.GetRequiredService<VocabularyFingerprint>().Value;
        ImmutableArray<StateOdds> odds = [StateOdds.Create(First, 0.5), StateOdds.Create(Second, 0.5)];
        ImmutableArray<SignalChance> chances = [.. new[] { LeansFirst, LeansSecond, WentWell, WentBadly }.Select(signalId => SignalChance.Create(signalId, 0.5, 0.4))];

        var fits = replayer.Replay([SessionStarted.Create(fingerprint, 0.15, 0.15), TurnExpected.Create(Expectation.Create(One, odds, chances, []))]);
        var usualOutOfRange = replayer.Replay([SessionStarted.Create(fingerprint, 0.15, 0.15), TurnExpected.Create(Expectation.Create(null, odds, chances.SetItem(0, SignalChance.Create(LeansFirst, 0.5, 1.5)), []))]);
        var statesOutOfOrder = replayer.Replay([SessionStarted.Create(fingerprint, 0.15, 0.15), TurnExpected.Create(Expectation.Create(null, odds.Reverse(), chances, []))]);
        var signalMissing = replayer.Replay([SessionStarted.Create(fingerprint, 0.15, 0.15), TurnExpected.Create(Expectation.Create(null, odds, chances.RemoveAt(0), []))]);
        var unknownMove = replayer.Replay([SessionStarted.Create(fingerprint, 0.15, 0.15), TurnExpected.Create(Expectation.Create("not_a_move", odds, chances, []))]);

        Assert.True(fits.IsT0);
        Assert.Equal(1, usualOutOfRange.AsT2.EventIndex);
        Assert.Equal(1, statesOutOfOrder.AsT2.EventIndex);
        Assert.Equal(1, signalMissing.AsT2.EventIndex);
        Assert.Equal(1, unknownMove.AsT2.EventIndex);
    }
}
