namespace LearnEngine.Core.Learning;

/// <summary>
/// How the app wants the engine to learn each learner: how many turns of evidence the shared rate of a signal counts
/// as, before the learner's own counts move it.
/// </summary>
public sealed record LearningOptions
{
    /// <summary>
    /// The rate weight used when the app does not give one.
    /// </summary>
    public const double DefaultRateWeight = 20;

    /// <summary>
    /// How many turns the shared rate of a signal in a state counts as. Above 0. A higher weight makes a learner's own
    /// counts move the rate more slowly.
    /// </summary>
    public double RateWeight { get; }

    private LearningOptions(double rateWeight) => RateWeight = rateWeight;

    /// <summary>
    /// Creates the options with the given rate weight.
    /// </summary>
    public static LearningOptions Create(double rateWeight) => new(rateWeight);
}
