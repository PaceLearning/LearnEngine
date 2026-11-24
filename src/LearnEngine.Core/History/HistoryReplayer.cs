using System.Collections.Immutable;
using LearnEngine.Core.Learning;
using LearnEngine.Core.Moves;
using LearnEngine.Core.Odds;
using LearnEngine.Core.Signals;
using OneOf;

namespace LearnEngine.Core.History;

/// <summary>
/// Reads the belief about a learner back from a session's history, since the belief itself is only kept as part of it.
/// </summary>
public sealed class HistoryReplayer
{
    private readonly OddsEngine engine;
    private readonly VocabularyFingerprint fingerprint;
    private readonly SignalCatalog signals;
    private readonly MoveCatalog? moves;

    private HistoryReplayer(OddsEngine engine, VocabularyFingerprint fingerprint, SignalCatalog signals, MoveCatalog? moves)
    {
        this.engine = engine;
        this.fingerprint = fingerprint;
        this.signals = signals;
        this.moves = moves;
    }

    // Internal so a replayer only comes from registration, with the engine, fingerprint, signals and moves of the
    // registered vocabulary. An app with no moves has no move catalog.
    internal static HistoryReplayer Create(OddsEngine engine, VocabularyFingerprint fingerprint, SignalCatalog signals, MoveCatalog? moves) =>
        new(engine, fingerprint, signals, moves);

    /// <summary>
    /// Reads the belief after the last event of the history, starting from the priors.
    /// A learner turn followed by a <see cref="TurnLearned"/> gives the odds kept in it, which were worked out with the
    /// learner's model of that day. A learner turn with none after it, as when a report stopped before keeping it, is
    /// worked out with the shared rates and effects: the belief is predicted from the move made before the turn, which
    /// with no move is the fade, then the turn's signals are applied. Other events leave the belief as it is. The same
    /// history always gives the same belief.
    /// A history stamped with another vocabulary gives <see cref="OtherVocabulary"/>. A history that does not begin with
    /// a session start, has a second one, has a fade or clear margin outside 0 to 1, has a turn with a signal or a move
    /// made that is not registered, has what a turn learned that is not right after a turn, does not give one odds
    /// per registered state adding up to 1, or adds counts that are not registered or below 0, or has what was expected
    /// of a turn kept twice before it, or not with one odds per registered state adding up to 1, one chance per
    /// registered signal and a registered move, or has a move picked that is not registered, in a state that is not
    /// registered or with a goal chance outside 0 to 1, gives <see cref="BrokenHistory"/>.
    /// </summary>
    public OneOf<Belief, OtherVocabulary, BrokenHistory> Replay(ImmutableArray<HistoryEvent> history)
    {
        if (history.IsDefaultOrEmpty)
            return BrokenHistory.Create(0, "The history is empty, so it has no session start.");

        if (history[0] is not SessionStarted start)
            return BrokenHistory.Create(0, "The history does not begin with a session start.");

        var sameVocabulary = start.Fingerprint == fingerprint.Value;
        if (!sameVocabulary)
            return OtherVocabulary.Create(fingerprint.Value, start.Fingerprint);

        // Written so a NaN fade fails too, which a pair of separate bound checks would let through.
        var fadeInRange = start.Fade >= 0 && start.Fade <= 1;
        if (!fadeInRange)
            return BrokenHistory.Create(0, $"The session start has a fade of {start.Fade}, which is not from 0 to 1.");

        var clearMarginInRange = start.ClearMargin >= 0 && start.ClearMargin <= 1;
        if (!clearMarginInRange)
            return BrokenHistory.Create(0, $"The session start has a clear margin of {start.ClearMargin}, which is not from 0 to 1.");

        var belief = engine.Start();
        ImmutableArray<string> stateIds = [.. belief.All.Select(stateOdds => stateOdds.StateId)];
        var expectedSinceTurn = false;
        for (var index = 1; index < history.Length; index++)
        {
            var historyEvent = history[index];
            if (historyEvent is SessionStarted)
                return BrokenHistory.Create(index, "A session start comes after the first event.");

            if (historyEvent is TurnLearned learned)
            {
                if (history[index - 1] is not LearnerTurn)
                    return BrokenHistory.Create(index, "What a turn learned does not come right after the turn.");

                var badCount = learned.Added.Rates.FirstOrDefault(count =>
                    signals.Find(count.SignalId).IsT1
                    || !stateIds.Contains(count.StateId)
                    || !(count.Shown >= 0 && double.IsFinite(count.Shown))
                    || !(count.NotShown >= 0 && double.IsFinite(count.NotShown)));
                if (badCount is not null)
                    return BrokenHistory.Create(index, $"What the turn learned adds a count of \"{badCount.SignalId}\" in \"{badCount.StateId}\" that is not registered or not 0 or more.");

                var badStep = learned.Added.Effects.FirstOrDefault(count =>
                    !stateIds.Contains(count.FromStateId)
                    || !stateIds.Contains(count.ToStateId)
                    || (count.MoveId is not null && (moves is null || moves.Find(count.MoveId).IsT1))
                    || !(count.Count >= 0 && double.IsFinite(count.Count)));
                if (badStep is not null)
                    return BrokenHistory.Create(index, $"What the turn learned adds a count from \"{badStep.FromStateId}\" to \"{badStep.ToStateId}\" after \"{badStep.MoveId}\" that is not registered or not 0 or more.");

                var badLink = learned.Added.TopicLinks.FirstOrDefault(count => !(count.Count >= 0 && double.IsFinite(count.Count)));
                if (badLink is not null)
                    return BrokenHistory.Create(index, $"What the turn learned adds a count from \"{badLink.FromTopic}\" to \"{badLink.ToTopic}\" that is not 0 or more.");

                var badTopicState = learned.Added.TopicStates.FirstOrDefault(count =>
                    !stateIds.Contains(count.StateId)
                    || !(count.Count >= 0 && double.IsFinite(count.Count)));
                if (badTopicState is not null)
                    return BrokenHistory.Create(index, $"What the turn learned adds a count of \"{badTopicState.StateId}\" on \"{badTopicState.Topic}\" that is not registered or not 0 or more.");
            }

            if (historyEvent is TurnExpected expected)
            {
                if (expectedSinceTurn)
                    return BrokenHistory.Create(index, "What was expected of the next turn is kept twice before the turn.");

                var unknownExpectedMove = expected.Expected.MoveId is not null && (moves is null || moves.Find(expected.Expected.MoveId).IsT1);
                if (unknownExpectedMove)
                    return BrokenHistory.Create(index, $"What was expected of the next turn assumes a move that is not registered: {expected.Expected.MoveId}.");

                var oddsFit = expected.Expected.Odds.Select(stateOdds => stateOdds.StateId).SequenceEqual(stateIds)
                    && expected.Expected.Odds.All(stateOdds => stateOdds.Odds >= 0 && stateOdds.Odds <= 1)
                    && Math.Abs(expected.Expected.Odds.Sum(stateOdds => stateOdds.Odds) - 1) <= 1e-6;
                if (!oddsFit)
                    return BrokenHistory.Create(index, "What was expected of the next turn does not have one odds from 0 to 1 per registered state, in the order they were registered, adding up to 1.");

                var signalsFit = expected.Expected.Signals.Select(chance => chance.SignalId).SequenceEqual(signals.All.Select(signal => signal.Id))
                    && expected.Expected.Signals.All(chance => chance.Chance >= 0 && chance.Chance <= 1);
                if (!signalsFit)
                    return BrokenHistory.Create(index, "What was expected of the next turn does not have one chance from 0 to 1 per registered signal, in the order they were registered.");

                var topicsFit = expected.Expected.Topics.All(chance => chance.Chance >= 0 && chance.Chance <= 1);
                if (!topicsFit)
                    return BrokenHistory.Create(index, "What was expected of the next turn has a topic chance that is not from 0 to 1.");

                expectedSinceTurn = true;
            }

            if (historyEvent is MovePicked picked)
            {
                var unknownPick = !stateIds.Contains(picked.StateId) || moves is null || moves.Find(picked.MoveId).IsT1;
                if (unknownPick)
                    return BrokenHistory.Create(index, $"The move \"{picked.MoveId}\" is picked in \"{picked.StateId}\", and the move or the state is not registered.");

                // Written so a NaN fails too, which a pair of separate bound checks would let through.
                var goalChanceInRange = picked.GoalChance >= 0 && picked.GoalChance <= 1 + 1e-6;
                if (!goalChanceInRange)
                    return BrokenHistory.Create(index, $"The move \"{picked.MoveId}\" is picked with a goal chance of {picked.GoalChance}, which is not from 0 to 1.");
            }

            if (historyEvent is LearnerTurn turn)
            {
                expectedSinceTurn = false;

                var unknownMove = turn.MoveMade is not null && (moves is null || moves.Find(turn.MoveMade).IsT1);
                if (unknownMove)
                    return BrokenHistory.Create(index, $"The turn has a move made that is not registered: {turn.MoveMade}.");

                // Applied even when the odds were kept, so a turn with a signal that is not registered is always found.
                var applied = engine.Apply(engine.Predict(belief, LearnerModel.Empty, turn.MoveMade), LearnerModel.Empty, turn.SignalIds);
                if (applied.IsT1)
                    return BrokenHistory.Create(index, $"The turn has signals that are not registered: {string.Join(", ", applied.AsT1.Select(unknown => unknown.Id))}.");

                belief = applied.AsT0;

                var kept = index + 1 < history.Length ? history[index + 1] as TurnLearned : null;
                if (kept is not null)
                {
                    var sameStates = kept.Odds.Select(stateOdds => stateOdds.StateId).SequenceEqual(stateIds);
                    if (!sameStates)
                        return BrokenHistory.Create(index + 1, "What the turn learned does not have one odds per registered state, in the order they were registered.");

                    var oddsInRange = kept.Odds.All(stateOdds => stateOdds.Odds >= 0 && stateOdds.Odds <= 1);
                    var addsUpToOne = Math.Abs(kept.Odds.Sum(stateOdds => stateOdds.Odds) - 1) <= 1e-6;
                    if (!oddsInRange || !addsUpToOne)
                        return BrokenHistory.Create(index + 1, "What the turn learned has odds that are not from 0 to 1 or do not add up to 1.");

                    belief = Belief.Create(kept.Odds);
                }
            }
        }

        return belief;
    }
}
