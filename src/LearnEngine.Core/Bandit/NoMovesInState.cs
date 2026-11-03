namespace LearnEngine.Core.Bandit;

/// <summary>
/// The answer when the app allowed no moves in a state, so no move can be picked in it and the tutor keeps going.
/// </summary>
public sealed record NoMovesInState
{
    /// <summary>
    /// The id of the state that has no moves.
    /// </summary>
    public string StateId { get; }

    private NoMovesInState(string stateId) => StateId = stateId;

    /// <summary>
    /// Creates the answer for a state with no moves.
    /// </summary>
    public static NoMovesInState Create(string stateId) => new(stateId);
}
