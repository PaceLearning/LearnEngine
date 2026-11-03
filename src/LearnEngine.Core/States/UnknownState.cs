namespace LearnEngine.Core.States;

/// <summary>
/// The answer to a lookup of an id that is not in the catalog.
/// </summary>
public sealed record UnknownState
{
    /// <summary>
    /// The id that was looked up.
    /// </summary>
    public string Id { get; }

    private UnknownState(string id) => Id = id;

    /// <summary>
    /// Creates the answer for an id that is not in the catalog.
    /// </summary>
    public static UnknownState Create(string id) => new(id);
}
