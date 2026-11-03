using LearnEngine.Core.History;
using LearnEngine.Core.Odds;
using OneOf;

namespace LearnEngine.Core.Sessions;

/// <summary>
/// The one service an app or an MCP host calls: it starts a learner's session, takes each learner turn and says what
/// the engine believes about the learner and which move to make next.
/// </summary>
public sealed class SessionEngine
{
    private readonly OddsEngine engine;
    private readonly OddsOptions options;
    private readonly VocabularyFingerprint fingerprint;
    private readonly IHistoryStore histories;

    private SessionEngine(OddsEngine engine, OddsOptions options, VocabularyFingerprint fingerprint, IHistoryStore histories)
    {
        this.engine = engine;
        this.options = options;
        this.fingerprint = fingerprint;
        this.histories = histories;
    }

    // Internal so an engine only comes from registration, with the registered vocabulary, options and stores.
    internal static SessionEngine Create(OddsEngine engine, OddsOptions options, VocabularyFingerprint fingerprint, IHistoryStore histories) =>
        new(engine, options, fingerprint, histories);

    /// <summary>
    /// Starts the given learner's session: its history begins with a session start stamped with the registered
    /// vocabulary and the fade and clear margin the app added the odds with. Gives the belief from the priors.
    /// A session that already has a history is kept as it is and gives <see cref="SessionAlreadyStarted"/>.
    /// </summary>
    public async Task<OneOf<SessionBelief, SessionAlreadyStarted>> StartSession(string learnerId, string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(learnerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var history = await histories.Load(learnerId, sessionId, cancellationToken);
        if (!history.IsDefaultOrEmpty)
            return SessionAlreadyStarted.Create(learnerId, sessionId);

        var start = SessionStarted.Create(fingerprint.Value, options.Fade, options.ClearMargin);
        await histories.Append(learnerId, sessionId, start, cancellationToken);
        return SessionBelief.Create(engine.Start(), start.ClearMargin);
    }
}
