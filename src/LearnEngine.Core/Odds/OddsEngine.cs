using LearnEngine.Core.States;

namespace LearnEngine.Core.Odds;

/// <summary>
/// Keeps the odds on the state a learner is in: starts from the priors and moves the odds with every learner turn.
/// </summary>
public sealed class OddsEngine
{
    private readonly StateCatalog states;

    private OddsEngine(StateCatalog states) => this.states = states;

    // Internal so an engine only comes from registration, which has already checked the states.
    internal static OddsEngine Create(StateCatalog states) => new(states);

    /// <summary>
    /// Gives the belief before anything is known about the learner: the prior of every state.
    /// </summary>
    public Belief Start() =>
        Belief.Create([.. states.All.Select(state => StateOdds.Create(state.Id, state.Prior))]);

    /// <summary>
    /// Moves the belief part way back to the priors, as is done before every learner turn so old turns count for less.
    /// The odds of each state become (1 minus fade) times its odds plus fade times its prior.
    /// A fade of 0 keeps the belief and a fade of 1 gives the priors. A fade outside 0 to 1 throws.
    /// </summary>
    public Belief Fade(Belief belief, double fade)
    {
        ArgumentNullException.ThrowIfNull(belief);

        // Written so a NaN fade fails too, which a pair of separate bound checks would let through.
        var inRange = fade >= 0 && fade <= 1;
        if (!inRange)
            throw new ArgumentOutOfRangeException(nameof(fade), fade, "The fade must be from 0 to 1.");

        // Every state in a belief is found, because a belief only holds the registered states.
        return Belief.Create([.. belief.All.Select(stateOdds => StateOdds.Create(
            stateOdds.StateId,
            (1 - fade) * stateOdds.Odds + fade * states.Find(stateOdds.StateId).AsT0.Prior))]);
    }
}
