using System.Collections.Immutable;
using LearnEngine.Core.Likelihoods;
using LearnEngine.Core.States;

namespace LearnEngine.Core.Registration;

/// <summary>
/// Collects the rates of every signal inside one AddLikelihoods call, so the table can be checked for a missing signal once the last one is added.
/// </summary>
public sealed class LikelihoodsBuilder
{
    private readonly ImmutableArray<string> signalIds;
    private readonly StateCatalog states;
    private readonly List<Likelihood> likelihoods = [];

    private LikelihoodsBuilder(ImmutableArray<string> signalIds, StateCatalog states)
    {
        this.signalIds = signalIds;
        this.states = states;
    }

    internal static LikelihoodsBuilder Create(ImmutableArray<string> signalIds, StateCatalog states) => new(signalIds, states);

    internal ImmutableArray<Likelihood> Likelihoods => [.. likelihoods];

    /// <summary>
    /// Adds the rate of a signal in every state: the chance that the signal shows up on any turn when the learner is in that state.
    /// Each rate is written as a state id and the rate, such as ("stuck", 0.40), and must be from
    /// <see cref="Likelihood.LowestRate"/> to <see cref="Likelihood.HighestRate"/>.
    /// A signal or state that was not added, a signal added twice, a state given twice, a rate out of range,
    /// or a state left without a rate throws on this line.
    /// </summary>
    public LikelihoodsBuilder AddRates(string signalId, params ReadOnlySpan<(string StateId, double Rate)> rates)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signalId);

        var knownSignal = signalIds.Contains(signalId);
        if (!knownSignal)
            throw new InvalidOperationException($"The signal \"{signalId}\" is not added. Add it with AddSignal before the likelihoods.");

        var taken = likelihoods.Any(likelihood => likelihood.SignalId == signalId);
        if (taken)
            throw new InvalidOperationException($"The rates of the signal \"{signalId}\" are added twice.");

        var line = new List<Likelihood>();
        foreach (var (stateId, rate) in rates)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(stateId);

            var knownState = states.Find(stateId).IsT0;
            if (!knownState)
                throw new InvalidOperationException($"The state \"{stateId}\" in the rates of the signal \"{signalId}\" is not added. Add it with AddStates before the likelihoods.");

            // Written so a NaN rate fails too, which a pair of separate bound checks would let through.
            var inRange = rate >= Likelihood.LowestRate && rate <= Likelihood.HighestRate;
            if (!inRange)
                throw new ArgumentOutOfRangeException(nameof(rates), rate, $"The rate of the signal \"{signalId}\" in the state \"{stateId}\" must be from {Likelihood.LowestRate} to {Likelihood.HighestRate}.");

            var given = line.Any(likelihood => likelihood.StateId == stateId);
            if (given)
                throw new InvalidOperationException($"The signal \"{signalId}\" has two rates in the state \"{stateId}\".");

            line.Add(Likelihood.Create(signalId, stateId, rate));
        }

        var missing = states.All
            .Where(state => line.All(likelihood => likelihood.StateId != state.Id))
            .Select(state => state.Id)
            .ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"The signal \"{signalId}\" has no rate in these states: {string.Join(", ", missing)}. Every signal needs a rate in every state.");

        likelihoods.AddRange(line);
        return this;
    }
}
