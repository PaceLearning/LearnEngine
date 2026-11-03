using System.Collections.Immutable;
using LearnEngine.Core.Bandit;
using LearnEngine.Core.History;
using LearnEngine.Core.Odds;
using OneOf;

namespace LearnEngine.Core.Judging;

/// <summary>
/// Judges a picked move by what the learner does next, so the bandit learns which move works for the learner in each state.
/// </summary>
public sealed class MoveJudge
{
    /// <summary>
    /// How many learner turns after a pick can judge the move.
    /// </summary>
    public const int JudgingTurns = 2;

    private readonly HistoryReplayer replayer;
    private readonly OddsEngine engine;
    private readonly OutcomeSignals outcomes;
    private readonly AllowedMoveCatalog allowedMoves;

    private MoveJudge(HistoryReplayer replayer, OddsEngine engine, OutcomeSignals outcomes, AllowedMoveCatalog allowedMoves)
    {
        this.replayer = replayer;
        this.engine = engine;
        this.outcomes = outcomes;
        this.allowedMoves = allowedMoves;
    }

    // Internal so a judge only comes from registration, with the registered vocabulary, outcome signals and allowed moves.
    internal static MoveJudge Create(HistoryReplayer replayer, OddsEngine engine, OutcomeSignals outcomes, AllowedMoveCatalog allowedMoves) =>
        new(replayer, engine, outcomes, allowedMoves);

    /// <summary>
    /// Replays the history and says what is due for the last picked move. It reads nothing but the history and the
    /// registered vocabulary, so the same history always gives the same answer.
    /// A picked move is judged by the next <see cref="JudgingTurns"/> learner turns, and only while the state it was picked
    /// for is still the clear state after the turn. On such a turn it failed when a failure signal shows up, and it worked
    /// when a success signal shows up and no failure signal does. When the state changes before a result, or the turns give
    /// no result, the move is not judged at all.
    /// A judgment that is due and not yet in the history gives the <see cref="MoveJudged"/> to record.
    /// A move still waiting for a result gives <see cref="MoveBeingJudged"/>, and anything else gives <see cref="NothingToJudge"/>.
    /// A history stamped with another vocabulary gives <see cref="OtherVocabulary"/>. A history the replayer cannot replay,
    /// or with a move picked in a state that is not clear, a move not allowed in its state, a move picked while another
    /// is being judged, a judgment that is not recorded before the next event, or a recorded judgment the turns do not
    /// give, gives <see cref="BrokenHistory"/>.
    /// </summary>
    public OneOf<MoveJudged, MoveBeingJudged, NothingToJudge, OtherVocabulary, BrokenHistory> Judge(ImmutableArray<HistoryEvent> history)
    {
        var replayed = replayer.Replay(history);
        if (replayed.IsT1)
            return replayed.AsT1;

        if (replayed.IsT2)
            return replayed.AsT2;

        // The replay checked the history begins with a session start in range and every turn has registered signals.
        var start = (SessionStarted)history[0];
        var belief = engine.Start();
        MovePicked? open = null;
        var turnsSeen = 0;
        MoveJudged? due = null;
        for (var index = 1; index < history.Length; index++)
        {
            var historyEvent = history[index];

            if (historyEvent is LearnerTurn turn)
            {
                if (due is not null)
                    return BrokenHistory.Create(index, $"The judgment of the move \"{due.MoveId}\" in \"{due.StateId}\" is not recorded before the next turn.");

                belief = engine.Apply(engine.Fade(belief, start.Fade), turn.SignalIds).AsT0;
                if (open is null)
                    continue;

                turnsSeen++;

                // The state is checked before the signals, so a turn that changes the state judges nothing.
                var leader = belief.ClearLeader(start.ClearMargin);
                var sameState = leader.IsT0 && leader.AsT0.StateId == open.StateId;
                var failed = turn.SignalIds.Any(outcomes.IsFailure);
                var worked = turn.SignalIds.Any(outcomes.IsSuccess);
                var judged = sameState && (failed || worked);
                if (judged)
                    due = MoveJudged.Create(open.StateId, open.MoveId, !failed);

                var closed = !sameState || judged || turnsSeen == JudgingTurns;
                if (closed)
                    open = null;

                continue;
            }

            if (historyEvent is MovePicked picked)
            {
                if (due is not null)
                    return BrokenHistory.Create(index, $"The move \"{picked.MoveId}\" is picked before the judgment of \"{due.MoveId}\" in \"{due.StateId}\" is recorded.");

                if (open is not null)
                    return BrokenHistory.Create(index, $"The move \"{picked.MoveId}\" is picked while \"{open.MoveId}\" in \"{open.StateId}\" is still being judged.");

                var leader = belief.ClearLeader(start.ClearMargin);
                var clearState = leader.IsT0 && leader.AsT0.StateId == picked.StateId;
                if (!clearState)
                    return BrokenHistory.Create(index, $"The move \"{picked.MoveId}\" is picked in \"{picked.StateId}\", which is not the clear state.");

                var allowed = allowedMoves.Find(picked.StateId);
                var allowedInState = allowed.IsT0 && allowed.AsT0.Any(score => score.MoveId == picked.MoveId);
                if (!allowedInState)
                    return BrokenHistory.Create(index, $"The move \"{picked.MoveId}\" is not allowed in \"{picked.StateId}\".");

                open = picked;
                turnsSeen = 0;
                continue;
            }

            if (historyEvent is MoveJudged recorded)
            {
                var expected = due is not null
                    && due.StateId == recorded.StateId
                    && due.MoveId == recorded.MoveId
                    && due.Worked == recorded.Worked;
                if (!expected)
                    return BrokenHistory.Create(index, $"The judgment of the move \"{recorded.MoveId}\" in \"{recorded.StateId}\" is not the one the turns before it give.");

                due = null;
            }
        }

        if (due is not null)
            return due;

        if (open is not null)
            return MoveBeingJudged.Create(open.StateId, open.MoveId, JudgingTurns - turnsSeen);

        return NothingToJudge.Create();
    }
}
