using System.Collections.Immutable;

namespace LearnEngine.Core.History;

/// <summary>
/// One learner turn: the ids of the signals the AI reported for it, never the learner's words.
/// A turn with no signals is still a turn, because every signal not showing up moves the odds too.
/// </summary>
public sealed class LearnerTurn : HistoryEvent
{
    /// <summary>
    /// The ids of the signals the AI reported for the turn, such as "asks.how_or_why", in the order it reported them.
    /// </summary>
    public ImmutableArray<string> SignalIds { get; }

    private LearnerTurn(ImmutableArray<string> signalIds) => SignalIds = signalIds;

    /// <summary>
    /// Creates a learner turn with the ids of the signals the AI reported for it.
    /// </summary>
    public static LearnerTurn Create(IEnumerable<string> signalIds) => new([.. signalIds]);
}
