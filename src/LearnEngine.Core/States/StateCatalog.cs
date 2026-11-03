using System.Collections.Frozen;
using System.Collections.Immutable;
using OneOf;

namespace LearnEngine.Core.States;

/// <summary>
/// Every state the app registered, built once and looked up by id.
/// </summary>
public sealed class StateCatalog
{
    private readonly FrozenDictionary<string, State> byId;

    /// <summary>
    /// Every state in the order the app registered them. Their priors add up to 1.
    /// </summary>
    public ImmutableArray<State> All { get; }

    private StateCatalog(FrozenDictionary<string, State> byId, ImmutableArray<State> all)
    {
        this.byId = byId;
        All = all;
    }

    // Internal so a catalog only comes from registration, which has already checked the ids are filled and unique and the priors add up to 1.
    internal static StateCatalog Create(IReadOnlyList<State> states)
    {
        var byId = states.ToFrozenDictionary(state => state.Id, StringComparer.Ordinal);
        return new(byId, [.. states]);
    }

    /// <summary>
    /// Finds the state with the given id, or says the id is unknown.
    /// </summary>
    public OneOf<State, UnknownState> Find(string id)
    {
        var state = byId.GetValueOrDefault(id);
        if (state is null)
            return UnknownState.Create(id);

        return state;
    }
}
