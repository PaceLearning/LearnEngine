using LearnEngine.Core.Bandit;

namespace LearnEngine.Core.Sessions;

/// <summary>
/// The judgment of the last picked move made on a learner turn: whether it worked, with the learner's new score of
/// the move in the state it was picked for.
/// </summary>
public sealed record MoveJudgment
{
    /// <summary>
    /// True when the move worked, and false when it failed.
    /// </summary>
    public bool Worked { get; }

    /// <summary>
    /// The learner's score of the move in its state after the judgment, such as keep_going in following at 8 out of 11.
    /// </summary>
    public MoveScore Score { get; }

    private MoveJudgment(bool worked, MoveScore score)
    {
        Worked = worked;
        Score = score;
    }

    /// <summary>
    /// Creates the judgment of a move that worked or failed, with the learner's new score of it.
    /// </summary>
    public static MoveJudgment Create(bool worked, MoveScore score) => new(worked, score);
}
