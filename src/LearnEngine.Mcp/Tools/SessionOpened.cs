using System.Collections.Immutable;

namespace LearnEngine.Mcp.Tools;

/// <summary>
/// The answer to start_session: the new session's id, the signal ids the AI may report and what to do first.
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
    /// What to do before the first student message is reported.
    /// </summary>
    public string Do { get; }

    /// <summary>
    /// The next call to make.
    /// </summary>
    public string Next { get; }

    private SessionOpened(string sessionId, ImmutableArray<SignalChoice> signals, string @do, string next)
    {
        SessionId = sessionId;
        Signals = signals;
        Do = @do;
        Next = next;
    }

    /// <summary>
    /// Creates the answer for the given session.
    /// </summary>
    public static SessionOpened Create(string sessionId, ImmutableArray<SignalChoice> signals, string @do, string next) =>
        new(sessionId, signals, @do, next);
}
