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
    /// Saves the given learner's score of a move in a state, replacing the one saved before for the same state and move.
    /// A score tried fewer than 0 times, or that worked fewer than 0 times or more times than it was tried, throws.
    /// </summary>
    Task Save(string learnerId, MoveScore score, CancellationToken cancellationToken);
}
