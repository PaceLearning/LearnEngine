using LearnEngine.Core.Effects;
using LearnEngine.Core.History;
using LearnEngine.Core.Likelihoods;
using LearnEngine.Core.Moves;
using LearnEngine.Core.Odds;
using LearnEngine.Core.Registration;
using LearnEngine.Core.Signals;
using LearnEngine.Core.States;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static LearnEngine.Core.Tests.TestVocabulary;

namespace LearnEngine.Core.Tests;

/// <summary>A wrong vocabulary stopping the app at startup, and lookups of ids that are not registered.</summary>
public sealed class RegistrationTests
{
    /// <summary>Two signals and two states, ready for the likelihoods.</summary>
    private static ServiceCollection SignalsAndStates()
    {
        var services = new ServiceCollection();
        services
            .AddSignal("a", "Signal a.")
            .AddSignal("b", "Signal b.");
        services.AddStates(states => states
            .AddState("x", "State x.", 0.5)
            .AddState("y", "State y.", 0.5));
        return services;
    }

    [Fact]
    public void AddLikelihoods_SignalWithoutRates_Throws()
    {
        var services = SignalsAndStates();

        var thrown = Assert.Throws<InvalidOperationException>(() =>
            services.AddLikelihoods(rates => rates.AddRates("a", ("x", 0.5), ("y", 0.5))));

        Assert.Contains("b", thrown.Message);
    }

    [Fact]
    public void AddRates_StateLeftWithoutARate_Throws()
    {
        var services = SignalsAndStates();

        var thrown = Assert.Throws<InvalidOperationException>(() =>
            services.AddLikelihoods(rates => rates
                .AddRates("a", ("x", 0.5))
                .AddRates("b", ("x", 0.5), ("y", 0.5))));

        Assert.Contains("y", thrown.Message);
    }

    [Fact]
    public void AddRates_UnknownSignal_Throws()
    {
        var services = SignalsAndStates();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddLikelihoods(rates => rates.AddRates("c", ("x", 0.5), ("y", 0.5))));
    }

    [Fact]
    public void AddRates_UnknownState_Throws()
    {
        var services = SignalsAndStates();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddLikelihoods(rates => rates.AddRates("a", ("x", 0.5), ("y", 0.5), ("z", 0.5))));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(double.NaN)]
    public void AddRates_RateOutOfRange_Throws(double rate)
    {
        var services = SignalsAndStates();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            services.AddLikelihoods(rates => rates.AddRates("a", ("x", rate), ("y", 0.5))));
    }

    [Fact]
    public void AddStates_PriorsNotAddingUpToOne_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() => services.AddStates(states => states
            .AddState("x", "State x.", 0.5)
            .AddState("y", "State y.", 0.4)));
    }

    [Fact]
    public void AddSignal_AfterTheLikelihoods_Throws()
    {
        var services = SignalsAndStates();
        services.AddLikelihoods(rates => rates
            .AddRates("a", ("x", 0.5), ("y", 0.5))
            .AddRates("b", ("x", 0.5), ("y", 0.5)));

        Assert.Throws<InvalidOperationException>(() => services.AddSignal("c", "Signal c."));
    }

    [Fact]
    public void AddMoveFor_UnknownStateOrMove_Throws()
    {
        var services = SignalsAndStates();
        services.AddMove("m", "Move m.");

        Assert.Throws<InvalidOperationException>(() => services.AddMoveFor("z", "m", 1, 2));
        Assert.Throws<InvalidOperationException>(() => services.AddMoveFor("x", "n", 1, 2));
    }

    [Fact]
    public void AddMoveEffect_WrongEffect_Throws()
    {
        var services = SignalsAndStates();
        services.AddMove("m", "Move m.");

        Assert.Throws<InvalidOperationException>(() => services.AddMoveEffect("z", "m", 10, ("x", 1)));
        Assert.Throws<InvalidOperationException>(() => services.AddMoveEffect("x", "n", 10, ("x", 1)));
        Assert.Throws<InvalidOperationException>(() => services.AddMoveEffect("x", "m", 10, ("z", 1)));
        Assert.Throws<InvalidOperationException>(() => services.AddMoveEffect("x", "m", 10, ("x", 0.5), ("x", 0.5)));
        Assert.Throws<InvalidOperationException>(() => services.AddMoveEffect("x", "m", 10, ("x", 0.5), ("y", 0.4)));
        Assert.Throws<ArgumentOutOfRangeException>(() => services.AddMoveEffect("x", "m", 10, ("x", 1.5), ("y", -0.5)));
        Assert.Throws<ArgumentOutOfRangeException>(() => services.AddMoveEffect("x", "m", 0, ("x", 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => services.AddMoveEffect("x", "m", double.NaN, ("x", 1)));

        services.AddMoveEffect("x", "m", 10, ("y", 1));
        Assert.Throws<InvalidOperationException>(() => services.AddMoveEffect("x", "m", 10, ("y", 1)));
    }

    [Fact]
    public void AddGoalState_UnknownOrTwice_Throws()
    {
        var services = SignalsAndStates();

        Assert.Throws<InvalidOperationException>(() => services.AddGoalState("z"));
        services.AddGoalState("x");
        Assert.Throws<InvalidOperationException>(() => services.AddGoalState("x"));
    }

    [Fact]
    public void MoveEffectCatalog_StateNotGiven_HasAChanceOfZeroAndAMoveWithoutEffectFades()
    {
        using var provider = Create(after: services => services
            .AddMoveEffect(First, One, weight: 4, (Second, 1)));
        var effects = provider.GetRequiredService<MoveEffectCatalog>();

        var registered = effects.For(First, One);
        var notRegistered = effects.For(Second, One);
        var noMove = effects.For(Second, null);

        Assert.Equal(0, registered.ChanceOf(First));
        Assert.Equal(4, registered.Weight);
        Assert.Equal(MoveEffectCatalog.NoMoveWeight, notRegistered.Weight);
        Assert.Equal(One, notRegistered.MoveId);
        Assert.Equal(noMove.Chances, notRegistered.Chances);
        Assert.Equal(1 - OddsOptions.DefaultFade + OddsOptions.DefaultFade * SecondPrior, noMove.ChanceOf(Second), 10);
        Assert.Equal(OddsOptions.DefaultFade * FirstPrior, noMove.ChanceOf(First), 10);
    }

    [Fact]
    public void VocabularyFingerprint_AnEffectOrAGoalState_ChangesIt()
    {
        using var plain = Create();
        using var withEffect = Create(after: services => services.AddMoveEffect(First, One, weight: 10, (Second, 1)));
        using var withGoal = Create(after: services => services.AddGoalState(First));

        var fingerprint = plain.GetRequiredService<VocabularyFingerprint>().Value;

        Assert.NotEqual(fingerprint, withEffect.GetRequiredService<VocabularyFingerprint>().Value);
        Assert.NotEqual(fingerprint, withGoal.GetRequiredService<VocabularyFingerprint>().Value);
    }

    [Fact]
    public void AddSuccessSignal_AlsoAFailureSignal_Throws()
    {
        var services = new ServiceCollection();
        services.AddSignal("a", "Signal a.");
        services.AddStates(states => states.AddState("x", "State x.", 1));
        services.AddLikelihoods(rates => rates.AddRates("a", ("x", 0.5)));
        services.AddOdds().AddHistory();
        services.AddMove("m", "Move m.").AddMoveFor("x", "m", 1, 2);
        services.AddFailureSignal("a");

        Assert.Throws<InvalidOperationException>(() => services.AddSuccessSignal("a"));
    }

    [Fact]
    public void AddLearnEngine_NoOutcomeSignals_Throws()
    {
        var services = new ServiceCollection();
        services.AddSignal("a", "Signal a.");
        services.AddStates(states => states.AddState("x", "State x.", 1));
        services.AddLikelihoods(rates => rates.AddRates("a", ("x", 0.5)));
        services.AddOdds().AddHistory();

        Assert.Throws<InvalidOperationException>(() => services.AddLearnEngine());
    }

    [Fact]
    public void AddLearnEngine_NoLearning_Throws()
    {
        var services = new ServiceCollection();
        services.AddSignal("a", "Signal a.").AddSignal("b", "Signal b.");
        services.AddStates(states => states.AddState("x", "State x.", 1));
        services.AddLikelihoods(rates => rates.AddRates("a", ("x", 0.5)).AddRates("b", ("x", 0.5)));
        services.AddOdds().AddHistory();
        services.AddMove("m", "Move m.").AddMoveFor("x", "m", 1, 2);
        services.AddSuccessSignal("a").AddFailureSignal("b");

        Assert.Throws<InvalidOperationException>(() => services.AddLearnEngine());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void AddLearning_RateWeightNotAboveZeroOrNotFinite_Throws(double rateWeight)
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentOutOfRangeException>(() => services.AddLearning(rateWeight));
    }

    [Fact]
    public void AddLearning_Twice_Throws()
    {
        var services = new ServiceCollection();
        services.AddLearning();

        Assert.Throws<InvalidOperationException>(() => services.AddLearning());
    }

    [Fact]
    public void Find_UnknownIds_GiveTheUnknownAnswers()
    {
        using var provider = Create();

        var signal = provider.GetRequiredService<SignalCatalog>().Find("not.a.signal");
        var state = provider.GetRequiredService<StateCatalog>().Find("not_a_state");
        var move = provider.GetRequiredService<MoveCatalog>().Find("not_a_move");
        var rateOfUnknownSignal = provider.GetRequiredService<LikelihoodTable>().Find("not.a.signal", First);
        var rateInUnknownState = provider.GetRequiredService<LikelihoodTable>().Find(LeansFirst, "not_a_state");

        Assert.Equal(UnknownSignal.Create("not.a.signal"), signal.AsT1);
        Assert.Equal(UnknownState.Create("not_a_state"), state.AsT1);
        Assert.Equal(UnknownMove.Create("not_a_move"), move.AsT1);
        Assert.Equal(UnknownSignal.Create("not.a.signal"), rateOfUnknownSignal.AsT1);
        Assert.Equal(UnknownState.Create("not_a_state"), rateInUnknownState.AsT2);
    }

    [Fact]
    public void Find_RegisteredIds_GiveWhatWasRegistered()
    {
        using var provider = Create();

        var state = provider.GetRequiredService<StateCatalog>().Find(Second);
        var rate = provider.GetRequiredService<LikelihoodTable>().Find(LeansSecond, Second);

        Assert.Equal(SecondPrior, state.AsT0.Prior);
        Assert.Equal(0.4, rate.AsT0.Rate);
    }
}
