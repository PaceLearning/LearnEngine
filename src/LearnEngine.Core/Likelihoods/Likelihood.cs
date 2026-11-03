namespace LearnEngine.Core.Likelihoods;

/// <summary>
/// How often a signal shows up on a learner's turn when the learner is in a state, such as how often a learner who is stuck asks for help.
/// On a turn, a signal that showed up multiplies the odds of the state by the rate, and a signal that did not show up multiplies them by 1 minus the rate.
/// </summary>
public sealed record Likelihood
{
    /// <summary>
    /// The lowest rate a likelihood can have. It is above 0 so a signal that shows up can never set the odds of a state to 0.
    /// </summary>
    public const double LowestRate = 0.01;

    /// <summary>
    /// The highest rate a likelihood can have. It is below 1 so a signal that does not show up can never set the odds of a state to 0.
    /// </summary>
    public const double HighestRate = 0.99;

    /// <summary>
    /// The id of the signal, such as "asks.how_or_why".
    /// </summary>
    public string SignalId { get; }

    /// <summary>
    /// The id of the state, such as "stuck".
    /// </summary>
    public string StateId { get; }

    /// <summary>
    /// The chance that the signal shows up on any turn when the learner is in the state, from <see cref="LowestRate"/> to <see cref="HighestRate"/>.
    /// </summary>
    public double Rate { get; }

    private Likelihood(string signalId, string stateId, double rate)
    {
        SignalId = signalId;
        StateId = stateId;
        Rate = rate;
    }

    /// <summary>
    /// Creates the likelihood of the given signal in the given state, with the given rate.
    /// </summary>
    public static Likelihood Create(string signalId, string stateId, double rate) => new(signalId, stateId, rate);
}
