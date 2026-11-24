namespace LearnEngine.Web.Feed;

/// <summary>
/// A student message the AI reported and the engine kept, put on the <see cref="EngineFeed"/> so the pages that show its
/// session read it back from the history at once. It holds ids and numbers only, never the student's words.
/// </summary>
public sealed class TurnShown
{
    /// <summary>
    /// The id of the student whose turn it is, such as "me".
    /// </summary>
    public string LearnerId { get; }

    /// <summary>
    /// The id of the session the turn was kept in.
    /// </summary>
    public string SessionId { get; }

    /// <summary>
    /// The turn number the AI reported, counted from 1.
    /// </summary>
    public int Turn { get; }

    /// <summary>
    /// When the turn was kept.
    /// </summary>
    public DateTimeOffset Time { get; }

    private TurnShown(string learnerId, string sessionId, int turn, DateTimeOffset time)
    {
        LearnerId = learnerId;
        SessionId = sessionId;
        Turn = turn;
        Time = time;
    }

    /// <summary>
    /// Creates the turn of the given student's session, kept at the given time.
    /// </summary>
    public static TurnShown Create(string learnerId, string sessionId, int turn, DateTimeOffset time)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(learnerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return new(learnerId, sessionId, turn, time);
    }
}
