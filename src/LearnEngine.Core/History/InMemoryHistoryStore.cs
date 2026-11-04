using System.Collections.Concurrent;
using System.Collections.Immutable;
using OneOf;

namespace LearnEngine.Core.History;

/// <summary>
/// Keeps every history in memory, so it is lost when the app stops. It is the store the engine registers
/// when the app does not register its own.
/// </summary>
public sealed class InMemoryHistoryStore : IHistoryStore
{
    private readonly ConcurrentDictionary<(string LearnerId, string SessionId), ImmutableArray<HistoryEvent>> histories = new();

    private InMemoryHistoryStore() { }

    /// <summary>
    /// Creates an empty store.
    /// </summary>
    public static InMemoryHistoryStore Create() => new();

    /// <inheritdoc/>
    public Task Append(string learnerId, string sessionId, HistoryEvent historyEvent, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(learnerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(historyEvent);
        cancellationToken.ThrowIfCancellationRequested();

        histories.AddOrUpdate(
            (learnerId, sessionId),
            [historyEvent],
            (_, history) => history.Add(historyEvent));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<OneOf<ImmutableArray<HistoryEvent>, BrokenHistory>> Load(string learnerId, string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(learnerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        cancellationToken.ThrowIfCancellationRequested();

        // Events kept in memory are the events themselves, so they always read back.
        var history = histories.GetValueOrDefault((learnerId, sessionId), []);
        return Task.FromResult<OneOf<ImmutableArray<HistoryEvent>, BrokenHistory>>(history);
    }
}
