using System.Collections.Immutable;
using LearnEngine.Core.Bandit;
using LearnEngine.Core.Effects;
using LearnEngine.Core.History;
using LearnEngine.Core.Judging;
using LearnEngine.Core.Learning;
using LearnEngine.Core.Moves;
using LearnEngine.Core.Odds;
using LearnEngine.Core.Signals;
using LearnEngine.Core.Topics;
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
    private readonly MoveEffectCatalog effects;
    private readonly VocabularyFingerprint fingerprint;
    private readonly IHistoryStore histories;
    private readonly HistoryReplayer replayer;
    private readonly SignalCatalog signals;
    private readonly MoveCatalog moves;
    private readonly MoveJudge judge;
    private readonly MovePicker picker;
    private readonly ILearnerModelStore models;

    private SessionEngine(
        OddsEngine engine,
        OddsOptions options,
        MoveEffectCatalog effects,
        VocabularyFingerprint fingerprint,
        IHistoryStore histories,
        HistoryReplayer replayer,
        SignalCatalog signals,
        MoveCatalog moves,
        MoveJudge judge,
        MovePicker picker,
        ILearnerModelStore models)
    {
        this.engine = engine;
        this.options = options;
        this.effects = effects;
        this.fingerprint = fingerprint;
        this.histories = histories;
        this.replayer = replayer;
        this.signals = signals;
        this.moves = moves;
        this.judge = judge;
        this.picker = picker;
        this.models = models;
    }

    // Internal so an engine only comes from registration, with the registered vocabulary, options and stores.
    internal static SessionEngine Create(
        OddsEngine engine,
        OddsOptions options,
        MoveEffectCatalog effects,
        VocabularyFingerprint fingerprint,
        IHistoryStore histories,
        HistoryReplayer replayer,
        SignalCatalog signals,
        MoveCatalog moves,
        MoveJudge judge,
        MovePicker picker,
        ILearnerModelStore models) =>
        new(engine, options, effects, fingerprint, histories, replayer, signals, moves, judge, picker, models);

    /// <summary>
    /// Starts the given learner's session: its history begins with a session start stamped with the registered
    /// vocabulary and the fade and clear margin the app added the odds with. Gives the belief from the priors.
    /// A session that already has a history, even one the store cannot read back, is kept as it is and gives
    /// <see cref="SessionAlreadyStarted"/>.
    /// </summary>
    public async Task<OneOf<SessionBelief, SessionAlreadyStarted>> StartSession(string learnerId, string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(learnerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var loaded = await histories.Load(learnerId, sessionId, cancellationToken);
        var started = loaded.IsT1 || !loaded.AsT0.IsDefaultOrEmpty;
        if (started)
            return SessionAlreadyStarted.Create(learnerId, sessionId);

        var start = SessionStarted.Create(fingerprint.Value, options.Fade, options.ClearMargin);
        await histories.Append(learnerId, sessionId, start, cancellationToken);
        return SessionBelief.Create(engine.Start(), start.ClearMargin);
    }

    /// <summary>
    /// Takes one learner turn with the ids of the signals the AI reported for it, never the learner's words, the id of
    /// the topic it was about and the id of the move the tutor made in the reply before it. The topic and the move made
    /// are null when there is none.
    /// Every id is checked first. Then the odds are predicted from the move made under the learner's own move effects
    /// and moved with the turn's signals under the learner's own rates, the turn and the odds after it are kept in the
    /// session's history, the chance of each state after the turn is added to the learner's counts of every signal in
    /// that state, the chance of each step from a state before the turn to a state after it is added to the learner's
    /// counts of the move made, the last
    /// picked move is judged and its judgment recorded in the learner's score when this turn gives one, and when the
    /// leading state is clear and no move is still being judged, a move is picked for it and kept in the history too.
    /// Report one turn of a session at a time, since two turns reported at once could both be kept before either is judged.
    /// Ids that are not registered give the unknown signals in the order reported, a session with no history gives
    /// <see cref="SessionNotStarted"/>, a history stamped with another vocabulary gives <see cref="OtherVocabulary"/>,
    /// a history that cannot be read back or replayed gives <see cref="BrokenHistory"/>, a topic that is not a valid topic
    /// id gives <see cref="InvalidTopic"/>, and a move made that is not registered gives <see cref="UnknownMove"/>.
    /// Each of these is found before the turn is kept, so nothing is kept.
    /// </summary>
    public async Task<OneOf<TurnResult, ImmutableArray<UnknownSignal>, SessionNotStarted, OtherVocabulary, BrokenHistory, InvalidTopic, UnknownMove>> ReportTurn(
        string learnerId,
        string sessionId,
        IEnumerable<string> signalIds,
        string? topic,
        string? moveMade,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(learnerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(signalIds);

        if (topic is not null && !TopicIds.IsValid(topic))
            return InvalidTopic.Create(topic);

        if (moveMade is not null && moves.Find(moveMade).IsT1)
            return UnknownMove.Create(moveMade);

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

        var loaded = await histories.Load(learnerId, sessionId, cancellationToken);
        if (loaded.IsT1)
            return loaded.AsT1;

        var history = loaded.AsT0;
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

        // The history was judged above, so it replays, begins with a session start and every id in the turn is registered.
        var start = (SessionStarted)history[0];
        var model = await models.Load(learnerId, cancellationToken);
        var previous = replayer.Replay(history).AsT0;
        var predicted = engine.Predict(previous, model, moveMade);
        var belief = engine.Apply(predicted, model, reported).AsT0;

        // The chance the learner was in each state after the turn is added to their counts of every signal in it, as
        // shown when the signal was reported and as not shown when it was not.
        var rates = signals.All.SelectMany(signal => belief.All.Select(stateOdds => reported.Contains(signal.Id)
            ? RateCount.Create(signal.Id, stateOdds.StateId, stateOdds.Odds, 0)
            : RateCount.Create(signal.Id, stateOdds.StateId, 0, stateOdds.Odds)));

        // The chance of each step from a state before the turn to a state after it is added to the effect of the move
        // made. It is the odds before times the learner's effect times the chance of the turn's signals after, rescaled,
        // which is the odds after the turn split over where they came from, as the predicted odds split them.
        // The predicted, belief and previous odds all list the registered states in the same order.
        var steps = previous.All.SelectMany(from => belief.All.Select((to, index) => EffectCount.Create(
            from.StateId,
            moveMade,
            to.StateId,
            predicted.All[index].Odds > 0
                ? from.Odds * model.Effect(effects.For(from.StateId, moveMade), to.StateId) * to.Odds / predicted.All[index].Odds
                : 0)));
        var change = LearnerModelChange.Create(rates, steps);

        // The turn and what was learned go in the history first, so a failure before the model is saved loses one turn
        // of learning instead of counting it twice when the histories are read again.
        var turn = LearnerTurn.Create(reported, topic, moveMade);
        var learned = TurnLearned.Create(belief.All, change);
        await histories.Append(learnerId, sessionId, turn, cancellationToken);
        await histories.Append(learnerId, sessionId, learned, cancellationToken);
        history = history.Add(turn).Add(learned);
        await models.Add(learnerId, change, cancellationToken);

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
    /// <see cref="OtherVocabulary"/>, and a history that cannot be read back or replayed gives <see cref="BrokenHistory"/>.
    /// </summary>
    public async Task<OneOf<SessionBelief, SessionNotStarted, OtherVocabulary, BrokenHistory>> GetBelief(string learnerId, string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(learnerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var loaded = await histories.Load(learnerId, sessionId, cancellationToken);
        if (loaded.IsT1)
            return loaded.AsT1;

        var history = loaded.AsT0;
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
