namespace LearnEngine.Core.Bandit;

/// <summary>
/// How often a move worked and how often it was tried in one state, such as ask_their_picture working 7 times out of 10
/// when the learner held a different picture.
/// </summary>
public sealed record MoveScore
{
    /// <summary>
    /// The id of the state the move was tried in, such as "model_clash".
    /// </summary>
    public string StateId { get; }

    /// <summary>
    /// The id of the move, such as "ask_their_picture".
    /// </summary>
    public string MoveId { get; }

    /// <summary>
    /// How many times the move worked in the state. It is never more than <see cref="Tried"/>.
    /// </summary>
    public int Worked { get; }

    /// <summary>
    /// How many times the move was tried in the state.
    /// </summary>
    public int Tried { get; }

    private MoveScore(string stateId, string moveId, int worked, int tried)
    {
        StateId = stateId;
        MoveId = moveId;
        Worked = worked;
        Tried = tried;
    }

    /// <summary>
    /// Creates the score of the given move in the given state, with how many times it worked and was tried.
    /// </summary>
    public static MoveScore Create(string stateId, string moveId, int worked, int tried) => new(stateId, moveId, worked, tried);
}
