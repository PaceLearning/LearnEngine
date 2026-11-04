using LearnEngine.Core.Odds;
using LearnEngine.Core.Registration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static LearnEngine.Core.Tests.TestVocabulary;

namespace LearnEngine.Core.Tests;

/// <summary>The odds from the priors, after a turn and after a fade, and the clear leader.</summary>
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

        var applied = engine.Apply(engine.Start(), [LeansFirst]);

        // leans.first showed up and leans.second did not. The two outcome signals have the same rate in both states,
        // so they cancel out when the odds are rescaled.
        var first = FirstPrior * 0.5 * (1 - 0.2);
        var second = SecondPrior * 0.1 * (1 - 0.4);
        Assert.True(applied.IsT0);
        Assert.Equal(first / (first + second), applied.AsT0.All[0].Odds, Precision);
        Assert.Equal(second / (first + second), applied.AsT0.All[1].Odds, Precision);
    }

    [Fact]
    public void Apply_NoSignals_StillMovesTheOdds()
    {
        using var provider = Create();
        var engine = provider.GetRequiredService<OddsEngine>();

        var applied = engine.Apply(engine.Start(), []);

        var first = FirstPrior * (1 - 0.5) * (1 - 0.2);
        var second = SecondPrior * (1 - 0.1) * (1 - 0.4);
        Assert.Equal(first / (first + second), applied.AsT0.All[0].Odds, Precision);
    }

    [Fact]
    public void Apply_SignalReportedTwice_CountsOnce()
    {
        using var provider = Create();
        var engine = provider.GetRequiredService<OddsEngine>();

        var once = engine.Apply(engine.Start(), [LeansFirst]).AsT0;
        var twice = engine.Apply(engine.Start(), [LeansFirst, LeansFirst]).AsT0;

        Assert.Equal(once.All[0].Odds, twice.All[0].Odds, Precision);
    }

    [Fact]
    public void Apply_UnknownSignals_GivesThemInTheOrderReported()
    {
        using var provider = Create();
        var engine = provider.GetRequiredService<OddsEngine>();

        var applied = engine.Apply(engine.Start(), ["not.a.signal", LeansFirst, "another"]);

        Assert.True(applied.IsT1);
        Assert.Equal(["not.a.signal", "another"], applied.AsT1.Select(unknown => unknown.Id));
    }

    [Fact]
    public void Fade_Zero_KeepsTheBelief()
    {
        using var provider = Create();
        var engine = provider.GetRequiredService<OddsEngine>();
        var belief = engine.Apply(engine.Start(), [LeansFirst]).AsT0;

        var faded = engine.Fade(belief, 0);

        Assert.Equal(belief.All[0].Odds, faded.All[0].Odds, Precision);
        Assert.Equal(belief.All[1].Odds, faded.All[1].Odds, Precision);
    }

    [Fact]
    public void Fade_One_GivesThePriors()
    {
        using var provider = Create();
        var engine = provider.GetRequiredService<OddsEngine>();
        var belief = engine.Apply(engine.Start(), [LeansFirst]).AsT0;

        var faded = engine.Fade(belief, 1);

        Assert.Equal(FirstPrior, faded.All[0].Odds, Precision);
        Assert.Equal(SecondPrior, faded.All[1].Odds, Precision);
    }

    [Fact]
    public void Fade_PartWay_MovesEachStateTowardItsPrior()
    {
        using var provider = Create();
        var engine = provider.GetRequiredService<OddsEngine>();
        var belief = engine.Apply(engine.Start(), [LeansFirst]).AsT0;

        var faded = engine.Fade(belief, 0.25);

        Assert.Equal(0.75 * belief.All[0].Odds + 0.25 * FirstPrior, faded.All[0].Odds, Precision);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    public void Fade_OutOfRange_Throws(double fade)
    {
        using var provider = Create();
        var engine = provider.GetRequiredService<OddsEngine>();

        Assert.Throws<ArgumentOutOfRangeException>(() => engine.Fade(engine.Start(), fade));
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
