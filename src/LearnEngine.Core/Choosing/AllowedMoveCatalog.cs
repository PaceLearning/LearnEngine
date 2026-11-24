using System.Collections.Frozen;
using System.Collections.Immutable;
using LearnEngine.Core.States;
using OneOf;

namespace LearnEngine.Core.Choosing;

/// <summary>
/// Every move the app allowed in each state, built once and looked up by state.
/// </summary>
public sealed class AllowedMoveCatalog
{
    private readonly StateCatalog states;
    private readonly FrozenDictionary<string, ImmutableArray<AllowedMove>> byState;

    /// <summary>
    /// Every allowed move, in the order the app registered them.
    /// </summary>
    public ImmutableArray<AllowedMove> All { get; }

    private AllowedMoveCatalog(StateCatalog states, FrozenDictionary<string, ImmutableArray<AllowedMove>> byState, ImmutableArray<AllowedMove> all)
    {
        this.states = states;
        this.byState = byState;
        All = all;
    }

    // Internal so a catalog only comes from registration, which has already checked every state and move is added once.
    internal static AllowedMoveCatalog Create(StateCatalog states, IReadOnlyList<AllowedMove> allowed)
    {
        // GroupBy keeps the registration order inside each state, which breaks ties when a move is chosen.
        var byState = allowed
            .GroupBy(move => move.StateId, StringComparer.Ordinal)
            .ToFrozenDictionary(group => group.Key, group => group.ToImmutableArray(), StringComparer.Ordinal);
        return new(states, byState, [.. allowed]);
    }

    /// <summary>
    /// Finds the moves allowed in the given state, in the order the app registered them.
    /// A state with no moves gives <see cref="NoMovesInState"/>, and an id that is not a state gives <see cref="UnknownState"/>.
    /// </summary>
    public OneOf<ImmutableArray<AllowedMove>, NoMovesInState, UnknownState> Find(string stateId)
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
