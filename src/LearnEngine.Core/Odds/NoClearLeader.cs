namespace LearnEngine.Core.Odds;

/// <summary>
/// The answer when the leading state is not far enough ahead of the runner up to be clear, so no move should be picked.
/// </summary>
public sealed record NoClearLeader
{
    /// <summary>
    /// How far the leader is ahead of the runner up, which is less than the margin asked for.
    /// </summary>
    public double Margin { get; }

    private NoClearLeader(double margin) => Margin = margin;

    /// <summary>
    /// Creates the answer for a leader that is ahead by the given margin only.
    /// </summary>
    public static NoClearLeader Create(double margin) => new(margin);
}
