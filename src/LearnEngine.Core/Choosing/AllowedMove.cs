namespace LearnEngine.Core.Choosing;

/// <summary>
/// A move the app allows in a state, such as worked_example in stuck. Only an allowed move can be chosen in its state.
/// </summary>
public sealed record AllowedMove
{
    /// <summary>
    /// The id of the state the move is allowed in, such as "stuck".
    /// </summary>
    public string StateId { get; }

    /// <summary>
    /// The id of the move, such as "worked_example".
    /// </summary>
    public string MoveId { get; }

    private AllowedMove(string stateId, string moveId)
    {
        StateId = stateId;
        MoveId = moveId;
    }

    // Internal so an allowed move only comes from registration, which has already checked the state and the move.
    internal static AllowedMove Create(string stateId, string moveId) => new(stateId, moveId);
}
