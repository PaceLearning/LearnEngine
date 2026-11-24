using System.Text.Json;
using LearnEngine.Core.Topics;
using OneOf;

namespace LearnEngine.Core.History;

/// <summary>
/// Writes history events as one line of JSON each and reads them back, for a store that keeps a history as lines of text.
/// Each line says which event it is, such as {"event":"learner_turn","signal_ids":["reply.minimal"]}.
/// </summary>
public static class HistoryEventJson
{
    /// <summary>
    /// Writes the event as one line of JSON, with no line breaks in it.
    /// </summary>
    public static string Write(HistoryEvent historyEvent)
    {
        ArgumentNullException.ThrowIfNull(historyEvent);
        return JsonSerializer.Serialize(historyEvent, HistoryJsonContext.Default.HistoryEvent);
    }

    /// <summary>
    /// Reads one line of JSON written by <see cref="Write"/>, or says why it is not an event the engine knows.
    /// </summary>
    public static OneOf<HistoryEvent, UnreadableEvent> Read(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        HistoryEvent? historyEvent;
        try
        {
            historyEvent = JsonSerializer.Deserialize(line, HistoryJsonContext.Default.HistoryEvent);
        }
        catch (JsonException exception)
        {
            return UnreadableEvent.Create(line, exception.Message);
        }
        // Thrown when the line does not say which event it is, since an event of no kind cannot be made.
        catch (NotSupportedException exception)
        {
            return UnreadableEvent.Create(line, exception.Message);
        }

        if (historyEvent is null)
            return UnreadableEvent.Create(line, "The line is null, not an event.");

        // Nullable annotations are not enforced inside a list, so a null signal id is caught here.
        var nullSignalId = historyEvent is LearnerTurn turn && turn.SignalIds.Any(signalId => signalId is null);
        if (nullSignalId)
            return UnreadableEvent.Create(line, "The turn has a signal id that is null.");

        var invalidTopic = historyEvent is LearnerTurn { Topic: not null } topicTurn && !TopicIds.IsValid(topicTurn.Topic);
        if (invalidTopic)
            return UnreadableEvent.Create(line, "The turn has a topic that is not a valid topic id.");

        var emptyMove = historyEvent is LearnerTurn { MoveMade: not null } moveTurn && string.IsNullOrWhiteSpace(moveTurn.MoveMade);
        if (emptyMove)
            return UnreadableEvent.Create(line, "The turn has a move made that is empty.");

        return historyEvent;
    }
}
