namespace LearnEngine.Core.History;

/// <summary>
/// The answer when a history cannot be replayed because one of its events does not fit, such as a history that does
/// not begin with a session start or a turn with a signal that is not registered.
/// </summary>
public sealed record BrokenHistory
{
    /// <summary>
    /// The position in the history of the event that does not fit, counted from 0.
    /// </summary>
    public int EventIndex { get; }

    /// <summary>
    /// Why the event does not fit, written for the person reading the app's logs.
    /// </summary>
    public string Reason { get; }

    private BrokenHistory(int eventIndex, string reason)
    {
        EventIndex = eventIndex;
        Reason = reason;
    }

    /// <summary>
    /// Creates the answer for the event at the given position, with why it does not fit.
    /// </summary>
    public static BrokenHistory Create(int eventIndex, string reason) => new(eventIndex, reason);
}
