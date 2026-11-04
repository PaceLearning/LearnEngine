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
    public Task<MoveScore> Add(string learnerId, MoveScore starting, bool worked, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(learnerId);
        ArgumentNullException.ThrowIfNull(starting);
        cancellationToken.ThrowIfCancellationRequested();

        var countsInRange = starting.Tried >= 0 && starting.Worked >= 0 && starting.Worked <= starting.Tried;
        if (!countsInRange)
            throw new ArgumentOutOfRangeException(nameof(starting), starting, "A score must have been tried 0 or more times and have worked from 0 to the times it was tried.");

        var workedCount = worked ? 1 : 0;

        // The update runs again when another add changed the state's scores first, so every add counts once.
        var updated = scores.AddOrUpdate(
            (learnerId, starting.StateId),
            _ => [MoveScore.Create(starting.StateId, starting.MoveId, starting.Worked + workedCount, starting.Tried + 1)],
            (_, saved) =>
            {
                var current = saved.FirstOrDefault(savedScore => savedScore.MoveId == starting.MoveId) ?? starting;
                var next = MoveScore.Create(starting.StateId, starting.MoveId, current.Worked + workedCount, current.Tried + 1);
                return [.. saved.Where(savedScore => savedScore.MoveId != starting.MoveId), next];
            });
        var score = updated.First(savedScore => savedScore.MoveId == starting.MoveId);
        return Task.FromResult(score);
    }
}
