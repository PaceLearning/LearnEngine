using System.Collections.Frozen;
using System.Collections.Immutable;
using LearnEngine.Core.Signals;
using LearnEngine.Core.States;
using OneOf;

namespace LearnEngine.Core.Likelihoods;

/// <summary>
/// The rate of every signal in every state the app registered, built once and looked up by signal and state.
/// </summary>
public sealed class LikelihoodTable
{
    private readonly FrozenDictionary<string, FrozenDictionary<string, Likelihood>> bySignal;

    /// <summary>
    /// Every likelihood in the order the app registered them. Every signal has exactly one in every state.
    /// </summary>
    public ImmutableArray<Likelihood> All { get; }

    private LikelihoodTable(FrozenDictionary<string, FrozenDictionary<string, Likelihood>> bySignal, ImmutableArray<Likelihood> all)
    {
        this.bySignal = bySignal;
        All = all;
    }

    // Internal so a table only comes from registration, which has already checked every signal has exactly one rate in every state.
    internal static LikelihoodTable Create(IReadOnlyList<Likelihood> likelihoods)
    {
        var bySignal = likelihoods
            .GroupBy(likelihood => likelihood.SignalId, StringComparer.Ordinal)
            .ToFrozenDictionary(
                group => group.Key,
                group => group.ToFrozenDictionary(likelihood => likelihood.StateId, StringComparer.Ordinal),
                StringComparer.Ordinal);
        return new(bySignal, [.. likelihoods]);
    }

    /// <summary>
    /// Finds the likelihood of the given signal in the given state, or says which of the two ids is unknown.
    /// When both are unknown, the signal is the one reported.
    /// </summary>
    public OneOf<Likelihood, UnknownSignal, UnknownState> Find(string signalId, string stateId)
    {
        var byState = bySignal.GetValueOrDefault(signalId);
        if (byState is null)
            return UnknownSignal.Create(signalId);

        var likelihood = byState.GetValueOrDefault(stateId);
        if (likelihood is null)
            return UnknownState.Create(stateId);

        return likelihood;
    }
}
