namespace LearnEngine.Core.Moves;

/// <summary>
/// The answer to a lookup of an id that is not in the catalog.
/// </summary>
public sealed record UnknownMove
{
    /// <summary>
    /// The id that was looked up.
    /// </summary>
    public string Id { get; }

    private UnknownMove(string id) => Id = id;

    /// <summary>
    /// Creates the answer for an id that is not in the catalog.
    /// </summary>
    public static UnknownMove Create(string id) => new(id);
}
