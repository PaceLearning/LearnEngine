namespace LearnEngine.Core.Odds;

/// <summary>
/// How sure the engine is of the leading state, as <see cref="Belief.Confidence"/> gives it: low when the leader is not
/// clear, medium when it is clear, and high when it is clear and far ahead.
/// </summary>
public enum Confidence
{
    /// <summary>
    /// The leader is not clear.
    /// </summary>
    Low,

    /// <summary>
    /// The leader is clear.
    /// </summary>
    Medium,

    /// <summary>
    /// The leader is clear and ahead of the runner up by at least <see cref="Belief.HighConfidenceMargin"/>.
    /// </summary>
    High,
}
