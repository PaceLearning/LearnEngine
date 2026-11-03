using System.Collections.Immutable;
using LearnEngine.Core.Likelihoods;
using LearnEngine.Core.Signals;
using LearnEngine.Core.States;
using OneOf;

namespace LearnEngine.Core.Odds;

/// <summary>
/// Keeps the odds on the state a learner is in: starts from the priors and moves the odds with every learner turn.
/// </summary>
public sealed class OddsEngine
{
    private readonly StateCatalog states;
    private readonly SignalCatalog signals;
    private readonly LikelihoodTable likelihoods;

    private OddsEngine(StateCatalog states, SignalCatalog signals, LikelihoodTable likelihoods)
    {
        this.states = states;
        this.signals = signals;
        this.likelihoods = likelihoods;
    }

    // Internal so an engine only comes from registration, which has already checked the states, the signals and the rates.
    internal static OddsEngine Create(StateCatalog states, SignalCatalog signals, LikelihoodTable likelihoods) =>
        new(states, signals, likelihoods);

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

    /// <summary>
    /// Applies the signals of one learner turn to the belief. Fade the belief first, since this does not.
    /// Every registered signal counts: a signal that showed up multiplies the odds of each state by its rate in that state,
    /// and a signal that did not show up multiplies them by 1 minus its rate. The odds are then rescaled to add up to 1.
    /// A turn with no signals still moves the belief, because every signal not showing up says something too.
    /// A signal reported twice counts once. Ids that are not registered give the unknown signals, in the order reported,
    /// and leave the belief as it was.
    /// </summary>
    public OneOf<Belief, ImmutableArray<UnknownSignal>> Apply(Belief belief, IEnumerable<string> signalIds)
    {
        ArgumentNullException.ThrowIfNull(belief);
        ArgumentNullException.ThrowIfNull(signalIds);

        var reported = signalIds.Distinct(StringComparer.Ordinal).ToList();

        ImmutableArray<UnknownSignal> unknown = [.. reported
            .Where(signalId => signals.Find(signalId).IsT1)
            .Select(UnknownSignal.Create)];
        if (unknown.Length > 0)
            return unknown;

        var multiplied = new List<StateOdds>();
        foreach (var stateOdds in belief.All)
        {
            var odds = stateOdds.Odds;
            foreach (var signal in signals.All)
            {
                // Every signal has a rate in every state, because registration checked the table is whole.
                var rate = likelihoods.Find(signal.Id, stateOdds.StateId).AsT0.Rate;
                var showedUp = reported.Contains(signal.Id);
                odds *= showedUp ? rate : 1 - rate;
            }

            multiplied.Add(StateOdds.Create(stateOdds.StateId, odds));
        }

        var total = multiplied.Sum(stateOdds => stateOdds.Odds);
        return Belief.Create([.. multiplied.Select(stateOdds => StateOdds.Create(stateOdds.StateId, stateOdds.Odds / total))]);
    }
}
