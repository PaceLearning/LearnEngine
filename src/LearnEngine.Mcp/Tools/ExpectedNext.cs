using System.Collections.Immutable;

namespace LearnEngine.Mcp.Tools;

/// <summary>
/// What the engine expects of the student's next message, so the AI can be ready for it: how sure it is, the signals
/// likelier than usual and the most likely topics, at most five of each. For the AI only.
/// </summary>
public sealed class ExpectedNext
{
    /// <summary>
    /// "high", "medium" or "low": how sure the engine is of where the student will be at the next message, by the same
    /// rule as the confidence of the state.
    /// </summary>
    public string Confidence { get; }

    /// <summary>
    /// The signals of the next message likelier than usual for this student, at most five, furthest above usual first.
    /// Empty when no signal is likelier than usual.
    /// </summary>
    public ImmutableArray<ExpectedSignal> Signals { get; }

    /// <summary>
    /// The most likely topics of the next message, at most five, most likely first. Empty when the engine knows no topic to expect.
    /// </summary>
    public ImmutableArray<ExpectedTopic> Topics { get; }

    private ExpectedNext(string confidence, ImmutableArray<ExpectedSignal> signals, ImmutableArray<ExpectedTopic> topics)
    {
        Confidence = confidence;
        Signals = signals;
        Topics = topics;
    }

    /// <summary>
    /// Creates what is expected of the next message with the given confidence, signals and topics.
    /// </summary>
    public static ExpectedNext Create(string confidence, ImmutableArray<ExpectedSignal> signals, ImmutableArray<ExpectedTopic> topics) =>
        new(confidence, signals, topics);
}
