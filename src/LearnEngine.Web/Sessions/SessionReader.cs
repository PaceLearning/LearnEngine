using System.Collections.Immutable;
using System.Diagnostics;
using LearnEngine.Core;
using LearnEngine.Core.Choosing;
using LearnEngine.Core.History;
using LearnEngine.Core.Learning;
using LearnEngine.Core.Odds;
using LearnEngine.Core.Sessions;

namespace LearnEngine.Web.Sessions;

/// <summary>
/// Reads a session back from its history for the Session page. Every number of a turn is the one the history kept that
/// day, never worked out again with the learner's model of today. Only the weighing of the moves allowed now uses
/// today's model, since the history keeps the move picked, not the ones it was picked over.
/// </summary>
public sealed class SessionReader
{
    private readonly IHistoryStore histories;
    private readonly HistoryReplayer replayer;
    private readonly ILearnerModelStore models;
    private readonly MoveChooser chooser;

    private SessionReader(IHistoryStore histories, HistoryReplayer replayer, ILearnerModelStore models, MoveChooser chooser)
    {
        this.histories = histories;
        this.replayer = replayer;
        this.models = models;
        this.chooser = chooser;
    }

    /// <summary>
    /// Creates a reader over the given stores, with the engine's own replayer and chooser.
    /// </summary>
    public static SessionReader Create(IHistoryStore histories, HistoryReplayer replayer, ILearnerModelStore models, MoveChooser chooser) =>
        new(histories, replayer, models, chooser);

    /// <summary>
    /// Reads the given learner's session. A session with no history gives <see cref="SessionNotStarted"/>, a history
    /// kept under another vocabulary gives <see cref="OtherVocabulary"/>, and one that cannot be read back or replayed
    /// gives <see cref="BrokenHistory"/>.
    /// </summary>
    public async Task<Outcome<SessionView, SessionNotStarted, OtherVocabulary, BrokenHistory>> Read(string learnerId, string sessionId, CancellationToken cancellationToken)
    {
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
        ImmutableArray<int> turnIndexes = [.. Enumerable.Range(0, history.Length).Where(index => history[index] is LearnerTurn)];

        // What was expected of a turn is kept after the turn before it, and what it learned and picked right after it.
        var turns = ImmutableArray.CreateBuilder<TurnView>(turnIndexes.Length);
        for (var position = 0; position < turnIndexes.Length; position++)
        {
            var index = turnIndexes[position];
            var begin = position > 0 ? turnIndexes[position - 1] + 1 : 1;
            var end = position + 1 < turnIndexes.Length ? turnIndexes[position + 1] : history.Length;
            var before = history[begin..index];
            var after = history[(index + 1)..end];
            turns.Add(TurnView.Create(
                position + 1,
                (LearnerTurn)history[index],
                after.OfType<TurnLearned>().FirstOrDefault(),
                after.OfType<MovePicked>().FirstOrDefault(),
                before.OfType<TurnExpected>().LastOrDefault()?.Expected,
                start.ClearMargin));
        }

        var leader = belief.ClearLeader(start.ClearMargin);
        ImmutableArray<WeighedMove> weighed = [];
        if (leader is StateOdds clear)
        {
            var model = await models.Load(learnerId, cancellationToken);
            var found = chooser.Weigh(belief, model, clear.StateId);
            if (found is ImmutableArray<WeighedMove> allowedNow)
                weighed = allowedNow;
        }

        var expectedNext = (history.LastOrDefault(historyEvent => historyEvent is LearnerTurn or TurnExpected) as TurnExpected)?.Expected;
        var lastPick = turnIndexes.IsEmpty ? null : turns[^1].Pick;
        return SessionView.Create(sessionId, start.ClearMargin, belief, lastPick, weighed, expectedNext, turns.MoveToImmutable());
    }
}
