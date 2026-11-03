using System.Collections.Immutable;
using LearnEngine.Core.Odds;
using OneOf;

namespace LearnEngine.Core.History;

/// <summary>
/// Rebuilds the belief about a learner from a session's history, since the belief itself is never stored.
/// </summary>
public sealed class HistoryReplayer
{
    private readonly OddsEngine engine;
    private readonly VocabularyFingerprint fingerprint;

    private HistoryReplayer(OddsEngine engine, VocabularyFingerprint fingerprint)
    {
        this.engine = engine;
        this.fingerprint = fingerprint;
    }

    // Internal so a replayer only comes from registration, with the engine and fingerprint of the registered vocabulary.
    internal static HistoryReplayer Create(OddsEngine engine, VocabularyFingerprint fingerprint) => new(engine, fingerprint);

    /// <summary>
    /// Replays the history from the priors: before every learner turn the belief fades by the fade the session was
    /// started with, then the turn's signals are applied. Events that are not learner turns leave the belief as it is.
    /// The same history always gives the same belief.
    /// A history stamped with another vocabulary gives <see cref="OtherVocabulary"/>. A history that does not begin with
    /// a session start, has a second one, has a fade or clear margin outside 0 to 1, or has a turn with a signal that is not registered
    /// gives <see cref="BrokenHistory"/>.
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
        for (var index = 1; index < history.Length; index++)
        {
            var historyEvent = history[index];
            if (historyEvent is SessionStarted)
                return BrokenHistory.Create(index, "A session start comes after the first event.");

            if (historyEvent is not LearnerTurn turn)
                continue;

            var applied = engine.Apply(engine.Fade(belief, start.Fade), turn.SignalIds);
            if (applied.IsT1)
                return BrokenHistory.Create(index, $"The turn has signals that are not registered: {string.Join(", ", applied.AsT1.Select(unknown => unknown.Id))}.");

            belief = applied.AsT0;
        }

        return belief;
    }
}
