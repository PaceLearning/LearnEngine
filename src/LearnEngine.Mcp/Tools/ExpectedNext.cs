using System.Collections.Immutable;

namespace LearnEngine.Mcp.Tools;

/// <summary>
/// What the engine expects of the student's next message, so the AI can be ready for it: the most likely signals and
/// topics, at most five of each, most likely first. For the AI only.
/// </summary>
public sealed class ExpectedNext
{
    /// <summary>
    /// The most likely signals of the next message, at most five, most likely first.
    /// </summary>
    public ImmutableArray<ExpectedSignal> Signals { get; }

    /// <summary>
    /// The most likely topics of the next message, at most five, most likely first. Empty when the engine knows no topic to expect.
    /// </summary>
    public ImmutableArray<ExpectedTopic> Topics { get; }

    private ExpectedNext(ImmutableArray<ExpectedSignal> signals, ImmutableArray<ExpectedTopic> topics)
    {
        Signals = signals;
        Topics = topics;
    }

    /// <summary>
    /// Creates what is expected of the next message with the given signals and topics.
    /// </summary>
    public static ExpectedNext Create(ImmutableArray<ExpectedSignal> signals, ImmutableArray<ExpectedTopic> topics) => new(signals, topics);
}
