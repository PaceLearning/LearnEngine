using System.Collections.Immutable;
using LearnEngine.Core.Effects;
using LearnEngine.Core.Learning;
using LearnEngine.Core.Likelihoods;
using LearnEngine.Core.Signals;
using LearnEngine.Core.States;
using OneOf;

namespace LearnEngine.Core.Odds;

/// <summary>
/// Keeps the odds on the state a learner is in: starts from the priors, predicts where the move made leads, and moves
/// the odds with the signals of every learner turn.
/// </summary>
public sealed class OddsEngine
{
    private readonly StateCatalog states;
    private readonly SignalCatalog signals;
    private readonly LikelihoodTable likelihoods;
    private readonly MoveEffectCatalog effects;
    private readonly double rateWeight;

    private OddsEngine(StateCatalog states, SignalCatalog signals, LikelihoodTable likelihoods, MoveEffectCatalog effects, double rateWeight)
    {
        this.states = states;
        this.signals = signals;
        this.likelihoods = likelihoods;
        this.effects = effects;
        this.rateWeight = rateWeight;
    }

    // Internal so an engine only comes from registration, which has already checked the states, the signals, the rates,
    // the move effects and the rate weight.
    internal static OddsEngine Create(StateCatalog states, SignalCatalog signals, LikelihoodTable likelihoods, MoveEffectCatalog effects, double rateWeight) =>
        new(states, signals, likelihoods, effects, rateWeight);

    /// <summary>
    /// Gives the belief before anything is known about the learner: the prior of every state.
    /// </summary>
    public Belief Start() =>
        Belief.Create([.. states.All.Select(state => StateOdds.Create(state.Id, state.Prior))]);

    /// <summary>
    /// Predicts the odds of every state at the next learner turn, from the odds now and the move the tutor made in
    /// between: for each state after, the sum over every state now of its odds times the learner's chance that the move
    /// leads from it there, as <see cref="LearnerModel.Effect"/> gives it. A null move is no move, whose effect is the
    /// fade: each state keeps 1 minus the fade of its odds and gets the fade times its prior. So with
    /// <see cref="LearnerModel.Empty"/> and no move, this is the fade the engine did before every turn until now.
    /// A move with no effect registered in a state is read there as no move. Apply the turn's signals after this.
    /// </summary>
    public Belief Predict(Belief belief, LearnerModel model, string? moveMade)
    {
        ArgumentNullException.ThrowIfNull(belief);
        ArgumentNullException.ThrowIfNull(model);

        // Every state in a belief is registered, so the catalog has an effect from each of them.
        return Belief.Create([.. states.All.Select(to => StateOdds.Create(
            to.Id,
            belief.All.Sum(from => from.Odds * model.Effect(effects.For(from.StateId, moveMade), to.Id))))]);
    }

    /// <summary>
    /// Applies the signals of one learner turn to the belief, with the learner's own rates. Predict the belief first, since this does not.
    /// Every registered signal counts: a signal that showed up multiplies the odds of each state by the learner's rate of
    /// it in that state, and a signal that did not show up multiplies them by 1 minus that rate. The odds are then
    /// rescaled to add up to 1. The learner's rate is the shared rate moved by the learner's counts, as
    /// <see cref="LearnerModel.Rate"/> gives it, so <see cref="LearnerModel.Empty"/> applies the shared rates.
    /// A turn with no signals still moves the belief, because every signal not showing up says something too.
    /// A signal reported twice counts once. Ids that are not registered give the unknown signals, in the order reported,
    /// and leave the belief as it was.
    /// </summary>
    public OneOf<Belief, ImmutableArray<UnknownSignal>> Apply(Belief belief, LearnerModel model, IEnumerable<string> signalIds)
    {
        ArgumentNullException.ThrowIfNull(belief);
        ArgumentNullException.ThrowIfNull(model);
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
                var rate = model.Rate(likelihoods.Find(signal.Id, stateOdds.StateId).AsT0, rateWeight);
                var showedUp = reported.Contains(signal.Id);
                odds *= showedUp ? rate : 1 - rate;
            }

            multiplied.Add(StateOdds.Create(stateOdds.StateId, odds));
        }

        var total = multiplied.Sum(stateOdds => stateOdds.Odds);
        return Belief.Create([.. multiplied.Select(stateOdds => StateOdds.Create(stateOdds.StateId, stateOdds.Odds / total))]);
    }
}
