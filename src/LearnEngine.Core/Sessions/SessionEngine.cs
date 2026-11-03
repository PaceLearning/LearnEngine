using System.Collections.Immutable;
using LearnEngine.Core.Bandit;
using LearnEngine.Core.History;
using LearnEngine.Core.Judging;
using LearnEngine.Core.Odds;
using LearnEngine.Core.Signals;
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
    private readonly HistoryReplayer replayer;
    private readonly SignalCatalog signals;
    private readonly MoveJudge judge;
    private readonly MovePicker picker;

    private SessionEngine(
        OddsEngine engine,
        OddsOptions options,
        VocabularyFingerprint fingerprint,
        IHistoryStore histories,
        HistoryReplayer replayer,
        SignalCatalog signals,
        MoveJudge judge,
        MovePicker picker)
    {
        this.engine = engine;
        this.options = options;
        this.fingerprint = fingerprint;
        this.histories = histories;
        this.replayer = replayer;
        this.signals = signals;
        this.judge = judge;
        this.picker = picker;
    }

    // Internal so an engine only comes from registration, with the registered vocabulary, options and stores.
    internal static SessionEngine Create(
        OddsEngine engine,
        OddsOptions options,
        VocabularyFingerprint fingerprint,
        IHistoryStore histories,
        HistoryReplayer replayer,
        SignalCatalog signals,
        MoveJudge judge,
        MovePicker picker) =>
        new(engine, options, fingerprint, histories, replayer, signals, judge, picker);

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

    /// <summary>
    /// Takes one learner turn with the ids of the signals the AI reported for it, never the learner's words.
    /// Every id is checked first. Then the turn is kept in the session's history, the odds are rebuilt from it, the last
    /// picked move is judged and its judgment recorded in the learner's score when this turn gives one, and when the
    /// leading state is clear and no move is still being judged, a move is picked for it and kept in the history too.
    /// Report one turn of a session at a time, since two turns reported at once could both be kept before either is judged.
    /// Ids that are not registered give the unknown signals in the order reported, a session with no history gives
    /// <see cref="SessionNotStarted"/>, a history stamped with another vocabulary gives <see cref="OtherVocabulary"/>,
    /// and a history that cannot be replayed gives <see cref="BrokenHistory"/>. Each of these is found before the turn is
    /// kept, so nothing is kept.
    /// </summary>
    public async Task<OneOf<TurnResult, ImmutableArray<UnknownSignal>, SessionNotStarted, OtherVocabulary, BrokenHistory>> ReportTurn(
        string learnerId,
        string sessionId,
        IEnumerable<string> signalIds,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(learnerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(signalIds);

        ImmutableArray<string> reported = [.. signalIds];
        var nullSignalId = reported.Any(signalId => signalId is null);
        if (nullSignalId)
            throw new ArgumentException("A signal id is null. Pass only the ids the AI reported.", nameof(signalIds));

        ImmutableArray<UnknownSignal> unknown = [.. reported
            .Distinct(StringComparer.Ordinal)
            .Where(signalId => signals.Find(signalId).IsT1)
            .Select(UnknownSignal.Create)];
        if (unknown.Length > 0)
            return unknown;

        var history = await histories.Load(learnerId, sessionId, cancellationToken);
        if (history.IsDefaultOrEmpty)
            return SessionNotStarted.Create(learnerId, sessionId);

        var before = judge.Judge(history);
        if (before.IsT3)
            return before.AsT3;

        if (before.IsT4)
            return before.AsT4;

        // A judgment is due before the turn only when an earlier report stopped between keeping its turn and recording
        // the judgment. It is recorded now, because a turn kept before it would break the history.
        if (before.IsT0)
        {
            await judge.Record(learnerId, sessionId, before.AsT0, cancellationToken);
            history = history.Add(before.AsT0);
        }

        var turn = LearnerTurn.Create(reported);
        await histories.Append(learnerId, sessionId, turn, cancellationToken);
        history = history.Add(turn);

        // The history was judged above and the turn has only registered signals, so it still replays.
        var belief = replayer.Replay(history).AsT0;
        var start = (SessionStarted)history[0];
        var sessionBelief = SessionBelief.Create(belief, start.ClearMargin);

        // One more turn of registered signals cannot break a history judged whole above, but if it ever does, the
        // answer is given instead of hidden.
        var after = judge.Judge(history);
        if (after.IsT3)
            return after.AsT3;

        if (after.IsT4)
            return after.AsT4;

        OneOf<MoveJudgment, MoveBeingJudged, NothingToJudge> judgment = NothingToJudge.Create();
        if (after.IsT1)
            judgment = after.AsT1;

        if (after.IsT0)
        {
            var score = await judge.Record(learnerId, sessionId, after.AsT0, cancellationToken);
            judgment = MoveJudgment.Create(after.AsT0.Worked, score);
        }

        var leader = belief.ClearLeader(start.ClearMargin);
        if (leader.IsT1)
            return TurnResult.Create(sessionBelief, leader.AsT1, judgment);

        if (after.IsT1)
            return TurnResult.Create(sessionBelief, after.AsT1, judgment);

        var picked = await picker.Pick(learnerId, leader.AsT0.StateId, cancellationToken);
        if (picked.IsT1)
            return TurnResult.Create(sessionBelief, picked.AsT1, judgment);

        // The clear leader is always a registered state, so the picker never answers with an unknown one.
        await histories.Append(learnerId, sessionId, MovePicked.Create(leader.AsT0.StateId, picked.AsT0.Move.Id), cancellationToken);
        return TurnResult.Create(sessionBelief, picked.AsT0, judgment);
    }

    /// <summary>
    /// Gives what the engine believes about the learner now, rebuilt from the session's history, without reporting a turn.
    /// A session with no history gives <see cref="SessionNotStarted"/>. A history stamped with another vocabulary gives
    /// <see cref="OtherVocabulary"/>, and a history that cannot be replayed gives <see cref="BrokenHistory"/>.
    /// </summary>
    public async Task<OneOf<SessionBelief, SessionNotStarted, OtherVocabulary, BrokenHistory>> GetBelief(string learnerId, string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(learnerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var history = await histories.Load(learnerId, sessionId, cancellationToken);
        if (history.IsDefaultOrEmpty)
            return SessionNotStarted.Create(learnerId, sessionId);

        var replayed = replayer.Replay(history);
        if (replayed.IsT1)
            return replayed.AsT1;

        if (replayed.IsT2)
            return replayed.AsT2;

        // The replay checked the history begins with a session start.
        var start = (SessionStarted)history[0];
        return SessionBelief.Create(replayed.AsT0, start.ClearMargin);
    }
}
