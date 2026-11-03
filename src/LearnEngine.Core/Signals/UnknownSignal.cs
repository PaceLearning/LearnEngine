namespace LearnEngine.Core.Signals;

/// <summary>
/// The answer to a lookup of an id that is not in the catalog.
/// </summary>
public sealed record UnknownSignal
{
    /// <summary>
    /// The id that was looked up.
    /// </summary>
    public string Id { get; }

    private UnknownSignal(string id) => Id = id;

    /// <summary>
    /// Creates the answer for an id that is not in the catalog.
    /// </summary>
    public static UnknownSignal Create(string id) => new(id);
}
