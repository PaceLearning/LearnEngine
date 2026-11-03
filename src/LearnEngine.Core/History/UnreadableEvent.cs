namespace LearnEngine.Core.History;

/// <summary>
/// The answer when a line of JSON is not a history event the engine knows, such as a damaged line in a stored history.
/// </summary>
public sealed record UnreadableEvent
{
    /// <summary>
    /// The line that was read.
    /// </summary>
    public string Line { get; }

    /// <summary>
    /// Why the line could not be read, written for the person reading the app's logs.
    /// </summary>
    public string Reason { get; }

    private UnreadableEvent(string line, string reason)
    {
        Line = line;
        Reason = reason;
    }

    /// <summary>
    /// Creates the answer for the given line, with why it could not be read.
    /// </summary>
    public static UnreadableEvent Create(string line, string reason) => new(line, reason);
}
