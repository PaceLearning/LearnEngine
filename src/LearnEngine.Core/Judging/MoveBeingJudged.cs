namespace LearnEngine.Core.Judging;

/// <summary>
/// The answer when a picked move has no result yet and its state is still clear, so the next learner turn may judge it.
/// No other move is picked while it is being judged.
/// </summary>
public sealed record MoveBeingJudged
{
    /// <summary>
    /// The id of the state the move was picked for, such as "following".
    /// </summary>
    public string StateId { get; }

    /// <summary>
    /// The id of the move being judged, such as "keep_going".
    /// </summary>
    public string MoveId { get; }

    /// <summary>
    /// How many more learner turns can judge the move, 1 or 2.
    /// </summary>
    public int TurnsLeft { get; }

    private MoveBeingJudged(string stateId, string moveId, int turnsLeft)
    {
        StateId = stateId;
        MoveId = moveId;
        TurnsLeft = turnsLeft;
    }

    /// <summary>
    /// Creates the answer for the given move in the given state, with how many more learner turns can judge it.
    /// </summary>
    public static MoveBeingJudged Create(string stateId, string moveId, int turnsLeft) => new(stateId, moveId, turnsLeft);
}
