using System.Collections.Concurrent;

namespace LearnEngine.Core.Learning;

/// <summary>
/// Keeps every learner's model in memory, so it is lost when the app stops. It is the store the engine registers when
/// the app does not register its own.
/// </summary>
public sealed class InMemoryLearnerModelStore : ILearnerModelStore
{
    private readonly ConcurrentDictionary<string, LearnerModel> models = new(StringComparer.Ordinal);

    private InMemoryLearnerModelStore() { }

    /// <summary>
    /// Creates an empty store.
    /// </summary>
    public static InMemoryLearnerModelStore Create() => new();

    /// <inheritdoc/>
    public Task<LearnerModel> Load(string learnerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(learnerId);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(models.GetValueOrDefault(learnerId, LearnerModel.Empty));
    }

    /// <inheritdoc/>
    public Task Add(string learnerId, LearnerModelChange change, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(learnerId);
        ArgumentNullException.ThrowIfNull(change);
        cancellationToken.ThrowIfCancellationRequested();

        // The update runs again when another add changed the model first, so every add counts once.
        models.AddOrUpdate(learnerId, _ => LearnerModel.Empty.Add(change), (_, saved) => saved.Add(change));
        return Task.CompletedTask;
    }
}
