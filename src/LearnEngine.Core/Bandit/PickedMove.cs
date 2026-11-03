using LearnEngine.Core.Moves;

namespace LearnEngine.Core.Bandit;

/// <summary>
/// The move picked for a learner in a clear state, with the learner's score it was picked on.
/// </summary>
public sealed record PickedMove
{
    /// <summary>
    /// The move the tutor makes next, with the description the AI reads to carry it out.
    /// </summary>
    public Move Move { get; }

    /// <summary>
    /// The learner's score of the move in the state it was picked for, or the starting score when the learner has none yet.
    /// </summary>
    public MoveScore Score { get; }

    /// <summary>
    /// The upper confidence bound the move was picked on, the highest of every move allowed in the state.
    /// It is positive infinity for a move that was never tried.
    /// </summary>
    public double UpperBound { get; }

    private PickedMove(Move move, MoveScore score, double upperBound)
    {
        Move = move;
        Score = score;
        UpperBound = upperBound;
    }

    /// <summary>
    /// Creates the answer for the given move, picked on the given score and upper confidence bound.
    /// </summary>
    public static PickedMove Create(Move move, MoveScore score, double upperBound) => new(move, score, upperBound);
}
