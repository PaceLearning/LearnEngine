namespace LearnEngine.Core.Signals;

/// <summary>
/// A fact the AI reports about a learner's turn, such as the learner asking how or why something works.
/// </summary>
public sealed record Signal
{
    /// <summary>
    /// The id the AI reports, such as "asks.how_or_why".
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// What the signal means, written for the AI so it knows when to report it.
    /// </summary>
    public string Description { get; }

    private Signal(string id, string description)
    {
        Id = id;
        Description = description;
    }

    /// <summary>
    /// Creates a signal with the given id and description.
    /// </summary>
    public static Signal Create(string id, string description) => new(id, description);
}
