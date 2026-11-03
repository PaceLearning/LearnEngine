namespace LearnEngine.Mcp.Tools;

/// <summary>
/// The answer to end_session: how many of the student's messages were recorded, to tell the student.
/// </summary>
public sealed class SessionEnded
{
    /// <summary>
    /// The id of the session that ended.
    /// </summary>
    public string SessionId { get; }

    /// <summary>
    /// How many student messages were reported with report_turn and kept.
    /// </summary>
    public int RecordedMessages { get; }

    /// <summary>
    /// The highest turn number the AI reported, which is how many messages the student sent as far as the server knows.
    /// </summary>
    public int LastTurn { get; }

    /// <summary>
    /// The sentence to pass on to the student, such as "PACE recorded 7 of your 9 messages in this session.".
    /// </summary>
    public string Message { get; }

    private SessionEnded(string sessionId, int recordedMessages, int lastTurn, string message)
    {
        SessionId = sessionId;
        RecordedMessages = recordedMessages;
        LastTurn = lastTurn;
        Message = message;
    }

    /// <summary>
    /// Creates the answer for the given session.
    /// </summary>
    public static SessionEnded Create(string sessionId, int recordedMessages, int lastTurn, string message) =>
        new(sessionId, recordedMessages, lastTurn, message);
}
