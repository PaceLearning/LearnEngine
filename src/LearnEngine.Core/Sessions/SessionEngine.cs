using System.Collections.Immutable;
using System.Diagnostics;
using LearnEngine.Core.Choosing;
using LearnEngine.Core.Effects;
using LearnEngine.Core.History;
using LearnEngine.Core.Learning;
using LearnEngine.Core.Moves;
using LearnEngine.Core.Odds;
using LearnEngine.Core.Prediction;
using LearnEngine.Core.Signals;
using LearnEngine.Core.States;
using LearnEngine.Core.Topics;

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
    private readonly MoveChooser chooser;
    private readonly ILearnerModelStore models;
    private readonly Predictor predictor;

    private SessionEngine(
        OddsEngine engine,
        OddsOptions options,
        MoveEffectCatalog effects,
        VocabularyFingerprint fingerprint,
        IHistoryStore histories,
        HistoryReplayer replayer,
        SignalCatalog signals,
        MoveCatalog moves,
        MoveChooser chooser,
        ILearnerModelStore models,
        Predictor predictor)
    {
        this.predictor = predictor;
        this.engine = engine;
        this.options = options;
        this.effects = effects;
        this.fingerprint = fingerprint;
        this.histories = histories;
        this.replayer = replayer;
        this.signals = signals;
        this.moves = moves;
        this.chooser = chooser;
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
        MoveChooser chooser,
        ILearnerModelStore models,
        Predictor predictor) =>
        new(engine, options, effects, fingerprint, histories, replayer, signals, moves, chooser, models, predictor);

    /// <summary>
    /// Starts the given learner's session: its history begins with a session start stamped with the registered
    /// vocabulary and the fade and clear margin the app added the odds with, followed by what is expected of the first
    /// turn from the priors with no move made, under the learner's model. Gives the belief from the priors.
    /// A session that already has a history, even one the store cannot read back, is kept as it is and gives
    /// <see cref="SessionAlreadyStarted"/>.
    /// </summary>
    public async Task<Outcome<SessionBelief, SessionAlreadyStarted>> StartSession(string learnerId, string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(learnerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var loaded = await histories.Load(learnerId, sessionId, cancellationToken);
        var started = loaded is BrokenHistory or ImmutableArray<HistoryEvent> { IsDefaultOrEmpty: false };
        if (started)
            return SessionAlreadyStarted.Create(learnerId, sessionId);

        var start = SessionStarted.Create(fingerprint.Value, options.Fade, options.ClearMargin);
        var model = await models.Load(learnerId, cancellationToken);
        var expected = predictor.Expect(engine.Start(), model, null, null);
        await histories.Append(learnerId, sessionId, start, cancellationToken);
        await histories.Append(learnerId, sessionId, TurnExpected.Create(expected), cancellationToken);
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
    /// counts of the move made, a turn on a topic adds the chance of each state to that topic and one link from the
    /// topic of the last turn that had one, and when the leading state is clear, the move most likely to bring the
    /// learner to a goal state under their model with this turn added is chosen, as <see cref="MoveChooser.Choose"/>
    /// does, and kept in the history with its goal chance and why.
    /// Last, what is expected of the next turn, after the picked move or no move, is kept in the history and given back
    /// with how unexpected this turn was under what was kept as expected of it.
    /// Report one turn of a session at a time, since two turns reported at once would both be kept after the same odds.
    /// Ids that are not registered give the unknown signals in the order reported, a session with no history gives
    /// <see cref="SessionNotStarted"/>, a history stamped with another vocabulary gives <see cref="OtherVocabulary"/>,
    /// a history that cannot be read back or replayed gives <see cref="BrokenHistory"/>, a topic that is not a valid topic
    /// id gives <see cref="InvalidTopic"/>, and a move made that is not registered gives <see cref="UnknownMove"/>.
    /// Each of these is found before the turn is kept, so nothing is kept.
    /// </summary>
    public async Task<Outcome<TurnResult, ImmutableArray<UnknownSignal>, SessionNotStarted, OtherVocabulary, BrokenHistory, InvalidTopic, UnknownMove>> ReportTurn(
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

        if (moveMade is not null && moves.Find(moveMade) is UnknownMove)
            return UnknownMove.Create(moveMade);

        ImmutableArray<string> reported = [.. signalIds];
        var nullSignalId = reported.Any(signalId => signalId is null);
        if (nullSignalId)
            throw new ArgumentException("A signal id is null. Pass only the ids the AI reported.", nameof(signalIds));

        ImmutableArray<UnknownSignal> unknown = [.. reported
            .Distinct(StringComparer.Ordinal)
            .Where(signalId => signals.Find(signalId) is UnknownSignal)
            .Select(UnknownSignal.Create)];
        if (unknown.Length > 0)
            return unknown;

        var loaded = await histories.Load(learnerId, sessionId, cancellationToken);
        if (loaded is BrokenHistory unreadable)
            return unreadable;

        if (loaded is not ImmutableArray<HistoryEvent> history)
            throw new UnreachableException();

        if (history.IsDefaultOrEmpty)
            return SessionNotStarted.Create(learnerId, sessionId);

        var replayed = replayer.Replay(history);
        if (replayed is OtherVocabulary other)
            return other;

        if (replayed is BrokenHistory broken)
            return broken;

        if (replayed is not Belief previous)
            throw new UnreachableException();

        // The replay checked the history begins with a session start.
        var start = (SessionStarted)history[0];
        var model = await models.Load(learnerId, cancellationToken);

        // The turn is measured against what was kept as expected of it, since the model has moved on since. Only a
        // report that stopped before keeping it leaves none, and then it is expected now, as with no move made.
        var lastTopic = history.OfType<LearnerTurn>().LastOrDefault(earlier => earlier.Topic is not null)?.Topic;
        var kept = history.LastOrDefault(historyEvent => historyEvent is LearnerTurn or TurnExpected) as TurnExpected;
        var expectedOfTurn = kept?.Expected ?? predictor.Expect(previous, model, null, lastTopic);
        var surprise = Predictor.Surprise(expectedOfTurn, reported);

        var predicted = engine.Predict(previous, model, moveMade);
        // The signals were checked above, so they all apply.
        if (engine.Apply(predicted, model, reported) is not Belief belief)
            throw new UnreachableException();

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

        // A turn on a topic adds the chance of each state after it to that topic, and one link from the topic of the
        // last turn that had one, which is the same topic when the learner stayed on it.
        IEnumerable<TopicStateCount> topicStates = topic is null
            ? []
            : belief.All.Select(stateOdds => TopicStateCount.Create(topic, stateOdds.StateId, stateOdds.Odds));
        IEnumerable<TopicLinkCount> topicLinks = topic is null || lastTopic is null
            ? []
            : [TopicLinkCount.Create(lastTopic, topic, 1)];
        var change = LearnerModelChange.Create(rates, steps, topicLinks, topicStates);

        // The turn and what was learned go in the history first, so a failure before the model is saved loses one turn
        // of learning instead of counting it twice when the histories are read again.
        var turn = LearnerTurn.Create(reported, topic, moveMade);
        var learned = TurnLearned.Create(belief.All, change);
        await histories.Append(learnerId, sessionId, turn, cancellationToken);
        await histories.Append(learnerId, sessionId, learned, cancellationToken);
        await models.Add(learnerId, change, cancellationToken);

        // The move is chosen and the next turn expected under the model with this turn added.
        var learnt = model.Add(change);
        var leader = belief.ClearLeader(start.ClearMargin);
        Outcome<PickedMove, NoClearLeader, NoMovesInState> pick = NoClearLeader.Create(belief.Margin);
        if (leader is StateOdds clear)
        {
            // The clear leader is always a registered state, so the chooser never answers with an unknown one.
            var chosen = chooser.Choose(belief, learnt, clear.StateId);
            if (chosen is UnknownState)
                throw new UnreachableException();

            if (chosen is NoMovesInState noMoves)
                pick = noMoves;

            if (chosen is PickedMove picked)
            {
                pick = picked;
                await histories.Append(learnerId, sessionId, MovePicked.Create(clear.StateId, picked.Move.Id, picked.GoalChance, picked.Why), cancellationToken);
            }
        }

        var moveNext = pick is PickedMove next ? next.Move.Id : null;
        var expected = predictor.Expect(belief, learnt, moveNext, topic ?? lastTopic);
        await histories.Append(learnerId, sessionId, TurnExpected.Create(expected), cancellationToken);
        return TurnResult.Create(SessionBelief.Create(belief, start.ClearMargin), pick, expected, surprise);
    }

    /// <summary>
    /// Gives what the engine believes about the learner now, rebuilt from the session's history, without reporting a turn.
    /// A session with no history gives <see cref="SessionNotStarted"/>. A history stamped with another vocabulary gives
    /// <see cref="OtherVocabulary"/>, and a history that cannot be read back or replayed gives <see cref="BrokenHistory"/>.
    /// </summary>
    public async Task<Outcome<SessionBelief, SessionNotStarted, OtherVocabulary, BrokenHistory>> GetBelief(string learnerId, string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(learnerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var loaded = await histories.Load(learnerId, sessionId, cancellationToken);
        if (loaded is BrokenHistory unreadable)
            return unreadable;

        if (loaded is not ImmutableArray<HistoryEvent> history)
            throw new UnreachableException();

        if (history.IsDefaultOrEmpty)
            return SessionNotStarted.Create(learnerId, sessionId);

        var replayed = replayer.Replay(history);
        if (replayed is OtherVocabulary other)
            return other;

        if (replayed is BrokenHistory broken)
            return broken;

        if (replayed is not Belief belief)
            throw new UnreachableException();

        // The replay checked the history begins with a session start.
        var start = (SessionStarted)history[0];
        return SessionBelief.Create(belief, start.ClearMargin);
    }
}
