using LearnEngine.Core.Learning;
using LearnEngine.Core.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace LearnEngine.Core.Tests;

/// <summary>
/// A small vocabulary whose odds can be worked out by hand: two states, two signals that lean one way each, and two
/// signals with the same rate in both states, so they do not move the odds. The first state is the goal, and both
/// moves are allowed in it.
/// </summary>
internal static class TestVocabulary
{
    public const string LeansFirst = "leans.first";
    public const string LeansSecond = "leans.second";
    public const string WentWell = "went.well";
    public const string WentBadly = "went.badly";

    public const string First = "first";
    public const string Second = "second";

    public const double FirstPrior = 0.6;
    public const double SecondPrior = 0.4;

    public const string One = "one";
    public const string Two = "two";

    /// <summary>
    /// Registers the vocabulary and the engine. Anything registered by <paramref name="before"/> comes first, such as a
    /// store kept across two vocabularies. With <paramref name="otherDescription"/> the first signal reads differently,
    /// which gives the same vocabulary another fingerprint, and so does another <paramref name="rateWeight"/>.
    /// Anything registered by <paramref name="after"/> comes last, such as a move effect.
    /// </summary>
    public static ServiceProvider Create(
        Action<IServiceCollection>? before = null,
        string otherDescription = "",
        double rateWeight = LearningOptions.DefaultRateWeight,
        Action<IServiceCollection>? after = null)
    {
        var services = new ServiceCollection();
        before?.Invoke(services);

        var leansFirstDescription = otherDescription.Length > 0 ? otherDescription : "Points to the first state.";
        services
            .AddSignal(LeansFirst, leansFirstDescription)
            .AddSignal(LeansSecond, "Points to the second state.")
            .AddSignal(WentWell, "Shows the last move worked.")
            .AddSignal(WentBadly, "Shows the last move failed.");

        services.AddStates(states => states
            .AddState(First, "The first state.", FirstPrior)
            .AddState(Second, "The second state.", SecondPrior));

        services.AddLikelihoods(rates => rates
            .AddRates(LeansFirst, (First, 0.5), (Second, 0.1))
            .AddRates(LeansSecond, (First, 0.2), (Second, 0.4))
            .AddRates(WentWell, (First, 0.3), (Second, 0.3))
            .AddRates(WentBadly, (First, 0.3), (Second, 0.3)));

        services
            .AddOdds()
            .AddHistory()
            .AddLearning(rateWeight);

        services
            .AddMove(One, "The first move.")
            .AddMove(Two, "The second move.");

        services
            .AddMoveFor(First, One)
            .AddMoveFor(First, Two)
            .AddGoalState(First)
            .AddLearnEngine();

        after?.Invoke(services);
        return services.BuildServiceProvider();
    }
}
