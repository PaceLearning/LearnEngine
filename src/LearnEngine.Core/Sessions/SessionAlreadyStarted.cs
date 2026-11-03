namespace LearnEngine.Core.Sessions;

/// <summary>
/// The answer when a session is started that already has a history, so it is kept as it is instead of being restarted.
/// </summary>
public sealed record SessionAlreadyStarted
{
    /// <summary>
    /// The id of the learner whose session it is.
    /// </summary>
    public string LearnerId { get; }

    /// <summary>
    /// The id of the session that was already started.
    /// </summary>
    public string SessionId { get; }

    private SessionAlreadyStarted(string learnerId, string sessionId)
    {
        LearnerId = learnerId;
        SessionId = sessionId;
    }

    /// <summary>
    /// Creates the answer for the given learner's session.
    /// </summary>
    public static SessionAlreadyStarted Create(string learnerId, string sessionId) => new(learnerId, sessionId);
}
