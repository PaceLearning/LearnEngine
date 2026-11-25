using System.Text.Json.Serialization;

namespace LearnEngine.Core.Learning;

/// <summary>
/// How many of one learner's turns in a state showed a signal and how many did not, such as says.dont_get_it shown on
/// 1.2 and not shown on 9.8 turns while stuck. The counts are soft: a turn adds the chance the learner was in the state,
/// not a whole turn.
/// </summary>
public sealed record RateCount
{
    /// <summary>
    /// The id of the signal, such as "says.dont_get_it".
    /// </summary>
    public string SignalId { get; }

    /// <summary>
    /// The id of the state, such as "stuck".
    /// </summary>
    public string StateId { get; }

    /// <summary>
    /// The soft count of turns in the state that showed the signal. It is 0 or more.
    /// </summary>
    public double Shown { get; }

    /// <summary>
    /// The soft count of turns in the state that did not show the signal. It is 0 or more.
    /// </summary>
    public double NotShown { get; }

    // Internal, not private, so the source generated JSON readers of the history and of the learner model can call it.
    [JsonConstructor]
    internal RateCount(string signalId, string stateId, double shown, double notShown)
    {
        SignalId = signalId;
        StateId = stateId;
        Shown = shown;
        NotShown = notShown;
    }

    /// <summary>
    /// Creates the counts of the given signal in the given state. An empty id, or a count below 0, infinite or not a number, throws.
    /// </summary>
    public static RateCount Create(string signalId, string stateId, double shown, double notShown)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateId);

        // Written so a NaN fails too, which a plain bound check would let through.
        var shownInRange = shown >= 0 && double.IsFinite(shown);
        if (!shownInRange)
            throw new ArgumentOutOfRangeException(nameof(shown), shown, "A count must be 0 or more and finite.");

        var notShownInRange = notShown >= 0 && double.IsFinite(notShown);
        if (!notShownInRange)
            throw new ArgumentOutOfRangeException(nameof(notShown), notShown, "A count must be 0 or more and finite.");

        return new(signalId, stateId, shown, notShown);
    }
}
