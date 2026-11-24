using System.Collections.Immutable;

namespace LearnEngine.Core.Effects;

/// <summary>
/// The states the engine wants to bring the learner to, such as following and eager. A move is worth more the more
/// likely it leads to one of them.
/// </summary>
public sealed class GoalStates
{
    /// <summary>
    /// The ids of the goal states, in the order the app registered them.
    /// </summary>
    public ImmutableArray<string> StateIds { get; }

    private GoalStates(ImmutableArray<string> stateIds) => StateIds = stateIds;

    // Internal so the goal states only come from registration, which has already checked every state is registered once.
    internal static GoalStates Create(IEnumerable<string> stateIds) => new([.. stateIds]);

    /// <summary>
    /// Says whether the given state is a goal state.
    /// </summary>
    public bool Contains(string stateId) => StateIds.Contains(stateId);
}
