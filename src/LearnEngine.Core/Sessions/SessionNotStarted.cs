namespace LearnEngine.Core.Sessions;

/// <summary>
/// The answer when a session has no history, so it must be started with StartSession before its turns are reported.
/// </summary>
public sealed record SessionNotStarted
{
    /// <summary>
    /// The id of the learner whose session it is.
    /// </summary>
    public string LearnerId { get; }

    /// <summary>
    /// The id of the session that is not started.
    /// </summary>
    public string SessionId { get; }

    private SessionNotStarted(string learnerId, string sessionId)
    {
        LearnerId = learnerId;
        SessionId = sessionId;
    }

    /// <summary>
    /// Creates the answer for the given learner's session.
    /// </summary>
    public static SessionNotStarted Create(string learnerId, string sessionId) => new(learnerId, sessionId);
}
