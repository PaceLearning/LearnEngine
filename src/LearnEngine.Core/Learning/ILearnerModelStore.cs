namespace LearnEngine.Core.Learning;

/// <summary>
/// Keeps what the engine has learned about each learner, across all of their sessions.
/// The engine registers an in-memory store unless the app registers its own first.
/// </summary>
public interface ILearnerModelStore
{
    /// <summary>
    /// Loads the given learner's model. A learner with nothing kept gives <see cref="LearnerModel.Empty"/>.
    /// </summary>
    Task<LearnerModel> Load(string learnerId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds the change to the given learner's model. The add is one step in the store, so two changes added at once,
    /// such as from two sessions of the learner, both count.
    /// </summary>
    Task Add(string learnerId, LearnerModelChange change, CancellationToken cancellationToken);
}
