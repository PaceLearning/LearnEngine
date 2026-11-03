using System.Collections.Frozen;
using System.Collections.Immutable;
using LearnEngine.Core.States;
using OneOf;

namespace LearnEngine.Core.Bandit;

/// <summary>
/// Every move the app allowed in each state, with its starting score across all learners, built once and looked up by state.
/// A learner with no score of their own for a move starts from its starting score.
/// </summary>
public sealed class AllowedMoveCatalog
{
    private readonly StateCatalog states;
    private readonly FrozenDictionary<string, ImmutableArray<MoveScore>> byState;

    /// <summary>
    /// Every allowed move with its starting score, in the order the app registered them.
    /// </summary>
    public ImmutableArray<MoveScore> All { get; }

    private AllowedMoveCatalog(StateCatalog states, FrozenDictionary<string, ImmutableArray<MoveScore>> byState, ImmutableArray<MoveScore> all)
    {
        this.states = states;
        this.byState = byState;
        All = all;
    }

    // Internal so a catalog only comes from registration, which has already checked every state and move is added once.
    internal static AllowedMoveCatalog Create(StateCatalog states, IReadOnlyList<MoveScore> startingScores)
    {
        // GroupBy keeps the registration order inside each state, which breaks ties when a move is picked.
        var byState = startingScores
            .GroupBy(score => score.StateId, StringComparer.Ordinal)
            .ToFrozenDictionary(group => group.Key, group => group.ToImmutableArray(), StringComparer.Ordinal);
        return new(states, byState, [.. startingScores]);
    }

    /// <summary>
    /// Finds the moves allowed in the given state with their starting scores, in the order the app registered them.
    /// A state with no moves gives <see cref="NoMovesInState"/>, and an id that is not a state gives <see cref="UnknownState"/>.
    /// </summary>
    public OneOf<ImmutableArray<MoveScore>, NoMovesInState, UnknownState> Find(string stateId)
    {
        var unknown = states.Find(stateId).IsT1;
        if (unknown)
            return UnknownState.Create(stateId);

        var allowed = byState.GetValueOrDefault(stateId);
        if (allowed.IsDefaultOrEmpty)
            return NoMovesInState.Create(stateId);

        return allowed;
    }
}
