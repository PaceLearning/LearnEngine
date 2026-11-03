namespace LearnEngine.Mcp.Tools;

/// <summary>
/// A signal id the AI may report for a student message, with what it means.
/// </summary>
public sealed class SignalChoice
{
    /// <summary>
    /// The id to report, such as "asks.how_or_why".
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// When to report it, in one line.
    /// </summary>
    public string Meaning { get; }

    private SignalChoice(string id, string meaning)
    {
        Id = id;
        Meaning = meaning;
    }

    /// <summary>
    /// Creates the choice with the given id and meaning.
    /// </summary>
    public static SignalChoice Create(string id, string meaning) => new(id, meaning);
}
