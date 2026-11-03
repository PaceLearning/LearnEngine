namespace LearnEngine.Core.Odds;

/// <summary>
/// How the app wants the odds kept: how far the belief fades back to the priors before every turn, and how far
/// the leading state must be ahead of the runner up to be clear.
/// </summary>
public sealed record OddsOptions
{
    /// <summary>
    /// The fade used when the app does not give one.
    /// </summary>
    public const double DefaultFade = 0.15;

    /// <summary>
    /// The clear margin used when the app does not give one.
    /// </summary>
    public const double DefaultClearMargin = 0.15;

    /// <summary>
    /// How far the belief moves back to the priors before every learner turn, from 0 to 1.
    /// </summary>
    public double Fade { get; }

    /// <summary>
    /// How far the leading state must be ahead of the runner up to be clear, from 0 to 1. Only a clear state lets a move be picked.
    /// </summary>
    public double ClearMargin { get; }

    private OddsOptions(double fade, double clearMargin)
    {
        Fade = fade;
        ClearMargin = clearMargin;
    }

    /// <summary>
    /// Creates the options with the given fade and clear margin.
    /// </summary>
    public static OddsOptions Create(double fade, double clearMargin) => new(fade, clearMargin);
}
