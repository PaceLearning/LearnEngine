using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace LearnEngine.Core.Bandit;

/// <summary>
/// Keeps every learner's scores in memory, so they are lost when the app stops. It is the store the engine registers
/// when the app does not register its own.
/// </summary>
public sealed class InMemoryScoreStore : IScoreStore
{
    private readonly ConcurrentDictionary<(string LearnerId, string StateId), ImmutableArray<MoveScore>> scores = new();

    private InMemoryScoreStore() { }

    /// <summary>
    /// Creates an empty store.
    /// </summary>
    public static InMemoryScoreStore Create() => new();

    /// <inheritdoc/>
    public Task<ImmutableArray<MoveScore>> Load(string learnerId, string stateId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(learnerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateId);
        cancellationToken.ThrowIfCancellationRequested();

        var saved = scores.GetValueOrDefault((learnerId, stateId), []);
        return Task.FromResult(saved);
    }

    /// <inheritdoc/>
    public Task Save(string learnerId, MoveScore score, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(learnerId);
        ArgumentNullException.ThrowIfNull(score);
        cancellationToken.ThrowIfCancellationRequested();

        var countsInRange = score.Tried >= 0 && score.Worked >= 0 && score.Worked <= score.Tried;
        if (!countsInRange)
            throw new ArgumentOutOfRangeException(nameof(score), score, "A score must have been tried 0 or more times and have worked from 0 to the times it was tried.");

        scores.AddOrUpdate(
            (learnerId, score.StateId),
            [score],
            (_, saved) => [.. saved.Where(savedScore => savedScore.MoveId != score.MoveId), score]);
        return Task.CompletedTask;
    }
}
