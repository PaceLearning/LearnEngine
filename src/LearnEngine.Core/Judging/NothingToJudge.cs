namespace LearnEngine.Core.Judging;

/// <summary>
/// The answer when no picked move is waiting for a judgment: none was picked, its judgment is already recorded, its state
/// changed first, or two learner turns gave no result. A move can be picked if the state is clear.
/// </summary>
public sealed record NothingToJudge
{
    private NothingToJudge() { }

    /// <summary>
    /// Creates the answer for a history with nothing to judge.
    /// </summary>
    public static NothingToJudge Create() => new();
}
