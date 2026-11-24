using System.Collections.Immutable;

namespace LearnEngine.Mcp.Tools;

/// <summary>
/// The answer to start_session: the new session's id, the signal ids the AI may report, the topic ids the student has
/// had before and what to do first.
/// </summary>
public sealed class SessionOpened
{
    /// <summary>
    /// The id of the new session, sent with every report_turn and end_session.
    /// </summary>
    public string SessionId { get; }

    /// <summary>
    /// Every signal id the AI may report, with what it means.
    /// </summary>
    public ImmutableArray<SignalChoice> Signals { get; }

    /// <summary>
    /// Every topic id this student has had before, in the order first seen, so the AI sends the same id when the
    /// student is on the same topic instead of making a new one.
    /// </summary>
    public ImmutableArray<string> Topics { get; }

    /// <summary>
    /// What to do before the first student message is reported.
    /// </summary>
    public string Do { get; }

    /// <summary>
    /// The next call to make.
    /// </summary>
    public string Next { get; }

    private SessionOpened(string sessionId, ImmutableArray<SignalChoice> signals, ImmutableArray<string> topics, string @do, string next)
    {
        SessionId = sessionId;
        Signals = signals;
        Topics = topics;
        Do = @do;
        Next = next;
    }

    /// <summary>
    /// Creates the answer for the given session.
    /// </summary>
    public static SessionOpened Create(string sessionId, ImmutableArray<SignalChoice> signals, ImmutableArray<string> topics, string @do, string next) =>
        new(sessionId, signals, topics, @do, next);
}
