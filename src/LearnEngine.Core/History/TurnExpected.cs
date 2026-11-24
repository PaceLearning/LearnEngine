using System.Text.Json.Serialization;
using LearnEngine.Core.Prediction;

namespace LearnEngine.Core.History;

/// <summary>
/// What the engine expected of the next learner turn, kept when the session started and after every turn: the
/// predicted odds, the chance of every signal and the chance of each topic, assuming the move picked then, or no move.
/// The learner's model keeps changing, so how unexpected the next turn was is read against this, not worked out again.
/// </summary>
public sealed class TurnExpected : HistoryEvent
{
    /// <summary>
    /// What was expected of the next turn.
    /// </summary>
    public Expectation Expected { get; }

    // Internal, not private, so the source generated JSON reader can call it.
    [JsonConstructor]
    internal TurnExpected(Expectation expected) => Expected = expected;

    /// <summary>
    /// Creates the event that keeps what was expected of the next turn.
    /// </summary>
    public static TurnExpected Create(Expectation expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        return new(expected);
    }
}
