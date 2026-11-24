using System.Collections.Immutable;
using LearnEngine.Core.Learning;
using LearnEngine.Core.Odds;
using LearnEngine.Core.Registration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static LearnEngine.Core.Tests.TestVocabulary;

namespace LearnEngine.Core.Tests;

/// <summary>The odds from the priors, after a turn and as predicted from a move or no move, and the clear leader.</summary>
public sealed class OddsEngineTests
{
    private const int Precision = 10;

    [Fact]
    public void Start_NothingKnown_GivesThePriors()
    {
        using var provider = Create();
        var engine = provider.GetRequiredService<OddsEngine>();

        var belief = engine.Start();

        Assert.Equal([First, Second], belief.All.Select(stateOdds => stateOdds.StateId));
        Assert.Equal(FirstPrior, belief.All[0].Odds, Precision);
        Assert.Equal(SecondPrior, belief.All[1].Odds, Precision);
        Assert.Equal(First, belief.Leader.StateId);
        Assert.Equal(Second, belief.RunnerUp?.StateId);
        Assert.Equal(FirstPrior - SecondPrior, belief.Margin, Precision);
    }

    [Fact]
    public void Apply_OneSignal_MultipliesByTheRateOfEverySignalAndRescales()
    {
        using var provider = Create();
        var engine = provider.GetRequiredService<OddsEngine>();

        var applied = engine.Apply(engine.Start(), LearnerModel.Empty, [LeansFirst]);

        // leans.first showed up and leans.second did not. The two outcome signals have the same rate in both states,
        // so they cancel out when the odds are rescaled.
        var first = FirstPrior * 0.5 * (1 - 0.2);
        var second = SecondPrior * 0.1 * (1 - 0.4);
        Assert.True(applied.IsT0);
        Assert.Equal(first / (first + second), applied.AsT0.All[0].Odds, Precision);
        Assert.Equal(second / (first + second), applied.AsT0.All[1].Odds, Precision);
    }

    [Fact]
    public void Apply_LearnerCounts_UsesTheLearnersRates()
    {
        using var provider = Create();
        var engine = provider.GetRequiredService<OddsEngine>();

        // 20 turns in the first state that did not show leans.first, on top of its shared 0.5 counted as 20 turns.
        var model = LearnerModel.Create([RateCount.Create(LeansFirst, First, 0, 20)], []);
        var learnerRate = (0.5 * LearningOptions.DefaultRateWeight + 0) / (LearningOptions.DefaultRateWeight + 0 + 20);

        var applied = engine.Apply(engine.Start(), model, [LeansFirst]);

        var first = FirstPrior * learnerRate * (1 - 0.2);
        var second = SecondPrior * 0.1 * (1 - 0.4);
        Assert.Equal(0.25, learnerRate, Precision);
        Assert.Equal(first / (first + second), applied.AsT0.All[0].Odds, Precision);
    }

    [Fact]
    public void Apply_NoSignals_StillMovesTheOdds()
    {
        using var provider = Create();
        var engine = provider.GetRequiredService<OddsEngine>();

        var applied = engine.Apply(engine.Start(), LearnerModel.Empty, []);

        var first = FirstPrior * (1 - 0.5) * (1 - 0.2);
        var second = SecondPrior * (1 - 0.1) * (1 - 0.4);
        Assert.Equal(first / (first + second), applied.AsT0.All[0].Odds, Precision);
    }

    [Fact]
    public void Apply_SignalReportedTwice_CountsOnce()
    {
        using var provider = Create();
        var engine = provider.GetRequiredService<OddsEngine>();

        var once = engine.Apply(engine.Start(), LearnerModel.Empty, [LeansFirst]).AsT0;
        var twice = engine.Apply(engine.Start(), LearnerModel.Empty, [LeansFirst, LeansFirst]).AsT0;

        Assert.Equal(once.All[0].Odds, twice.All[0].Odds, Precision);
    }

    [Fact]
    public void Apply_UnknownSignals_GivesThemInTheOrderReported()
    {
        using var provider = Create();
        var engine = provider.GetRequiredService<OddsEngine>();

        var applied = engine.Apply(engine.Start(), LearnerModel.Empty, ["not.a.signal", LeansFirst, "another"]);

        Assert.True(applied.IsT1);
        Assert.Equal(["not.a.signal", "another"], applied.AsT1.Select(unknown => unknown.Id));
    }

    [Fact]
    public void Predict_NoMoveOverSeveralTurns_GivesTheOddsOfTodaysFade()
    {
        using var provider = Create();
        var engine = provider.GetRequiredService<OddsEngine>();
        const double fade = OddsOptions.DefaultFade;
        ImmutableArray<ImmutableArray<string>> turns = [[LeansFirst], [LeansSecond], [], [LeansFirst, WentWell], [LeansSecond]];

        var predicted = engine.Start();
        var today = FirstPrior;
        foreach (var turn in turns)
        {
            predicted = engine.Apply(engine.Predict(predicted, LearnerModel.Empty, null), LearnerModel.Empty, turn).AsT0;

            // The fade the engine did before every turn until now, then the turn's rates, worked out by hand. The two
            // outcome signals have the same rate in both states, so they cancel out.
            var fadedFirst = (1 - fade) * today + fade * FirstPrior;
            var first = fadedFirst * 0.5 * (turn.Contains(LeansSecond) ? 0.2 : 0.8);
            var second = (1 - fadedFirst) * (turn.Contains(LeansFirst) ? 0.1 : 0.9) * (turn.Contains(LeansSecond) ? 0.4 : 0.6);
            today = first / (first + second);

            Assert.Equal(today, predicted.All[0].Odds, Precision);
            Assert.Equal(1 - today, predicted.All[1].Odds, Precision);
        }
    }

    [Fact]
    public void Predict_MoveWithAnEffect_LeadsWhereTheEffectSays()
    {
        using var provider = Create(after: services => services
            .AddMoveEffect(First, One, weight: 10, (First, 0.2), (Second, 0.8)));
        var engine = provider.GetRequiredService<OddsEngine>();

        var predicted = engine.Predict(engine.Start(), LearnerModel.Empty, One);

        // From the first state the move leads to the second with 0.8. From the second it has no effect, so it fades.
        const double fade = OddsOptions.DefaultFade;
        var second = FirstPrior * 0.8 + SecondPrior * ((1 - fade) + fade * SecondPrior);
        Assert.Equal(second, predicted.All[1].Odds, Precision);
        Assert.Equal(1, predicted.All.Sum(stateOdds => stateOdds.Odds), Precision);
    }

    [Fact]
    public void Predict_LearnerCounts_UsesTheLearnersEffect()
    {
        using var provider = Create(after: services => services
            .AddMoveEffect(First, One, weight: 10, (First, 0.2), (Second, 0.8)));
        var engine = provider.GetRequiredService<OddsEngine>();
        var certain = engine.Apply(engine.Start(), LearnerModel.Empty, [LeansFirst]).AsT0;

        // 10 steps from the first state that stayed there, on top of the shared 0.2 counted as 10.
        var model = LearnerModel.Create([], [EffectCount.Create(First, One, First, 10)]);
        var learnt = engine.Predict(certain, model, One);
        var shared = engine.Predict(certain, LearnerModel.Empty, One);

        Assert.True(learnt.All[0].Odds > shared.All[0].Odds);
        Assert.Equal(1, learnt.All.Sum(stateOdds => stateOdds.Odds), Precision);
    }

    [Fact]
    public void ClearLeader_MarginAtLeastAskedFor_GivesTheLeader()
    {
        using var provider = Create();
        var engine = provider.GetRequiredService<OddsEngine>();

        var belief = engine.Start();

        // The priors put the first state about 0.2 ahead. A margin of exactly that much is clear.
        var exactly = belief.ClearLeader(belief.Margin);
        var below = belief.ClearLeader(0.15);

        Assert.Equal(First, exactly.AsT0.StateId);
        Assert.Equal(First, below.AsT0.StateId);
    }

    [Fact]
    public void ClearLeader_MarginBelowAskedFor_GivesNoClearLeaderWithTheMargin()
    {
        using var provider = Create();
        var engine = provider.GetRequiredService<OddsEngine>();

        var leader = engine.Start().ClearLeader(0.25);

        Assert.True(leader.IsT1);
        Assert.Equal(FirstPrior - SecondPrior, leader.AsT1.Margin, Precision);
    }

    [Fact]
    public void Leader_SameOdds_IsTheStateRegisteredFirst()
    {
        var services = new ServiceCollection();
        services.AddSignal("any", "Any signal.");
        services.AddStates(states => states
            .AddState("later", "Registered first.", 0.5)
            .AddState("earlier", "Registered second.", 0.5));
        services.AddLikelihoods(rates => rates.AddRates("any", ("later", 0.5), ("earlier", 0.5)));
        services.AddOdds();
        using var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<OddsEngine>();

        var belief = engine.Start();

        Assert.Equal("later", belief.Leader.StateId);
        Assert.Equal(0, belief.Margin, Precision);
        Assert.True(belief.ClearLeader(0).IsT0);
        Assert.True(belief.ClearLeader(0.01).IsT1);
    }
}
