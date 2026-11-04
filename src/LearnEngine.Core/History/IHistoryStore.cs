using System.Collections.Immutable;
using OneOf;

namespace LearnEngine.Core.History;

/// <summary>
/// Keeps the history of every session, one append-only list of events per learner and session.
/// The engine registers an in-memory store unless the app registers its own first.
/// </summary>
public interface IHistoryStore
{
    /// <summary>
    /// Adds the event to the end of the history of the given learner's session. Events are never changed or removed.
    /// </summary>
    Task Append(string learnerId, string sessionId, HistoryEvent historyEvent, CancellationToken cancellationToken);

    /// <summary>
    /// Loads the history of the given learner's session in the order the events were appended, or an empty history
    /// when nothing was appended to it. A kept event the store cannot read back, such as one written before LearnEngine
    /// changed the event, gives <see cref="BrokenHistory"/> at its position, so the engine answers with it instead of
    /// replaying the rest.
    /// </summary>
    Task<OneOf<ImmutableArray<HistoryEvent>, BrokenHistory>> Load(string learnerId, string sessionId, CancellationToken cancellationToken);
}
