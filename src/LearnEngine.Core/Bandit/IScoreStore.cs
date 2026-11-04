using System.Collections.Immutable;

namespace LearnEngine.Core.Bandit;

/// <summary>
/// Keeps each learner's own score of every move in every state, across all of their sessions.
/// A learner with no score of their own for a move starts from the starting score the app registered with AddMoveFor.
/// The engine registers an in-memory store unless the app registers its own first.
/// </summary>
public interface IScoreStore
{
    /// <summary>
    /// Loads the given learner's own scores in the given state, one per move that has one, in no particular order.
    /// A learner with no scores in the state gives an empty list.
    /// </summary>
    Task<ImmutableArray<MoveScore>> Load(string learnerId, string stateId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds one judgment to the given learner's score of the starting score's move in its state: one more try, and one
    /// more worked when it worked. A learner with no score of their own for the move starts from the starting score.
    /// Gives the learner's new score. The add is one step in the store, so two judgments of the same move added at once,
    /// such as from two sessions of the learner, both count.
    /// A starting score tried fewer than 0 times, or that worked fewer than 0 times or more times than it was tried, throws.
    /// </summary>
    Task<MoveScore> Add(string learnerId, MoveScore starting, bool worked, CancellationToken cancellationToken);
}
