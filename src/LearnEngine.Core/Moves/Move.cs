namespace LearnEngine.Core.Moves;

/// <summary>
/// Something the tutor does next with a learner, such as walking through a worked example.
/// </summary>
public sealed record Move
{
    /// <summary>
    /// The id the engine picks, such as "worked_example".
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// What the tutor does in this move, written for the AI so it knows how to carry it out.
    /// </summary>
    public string Description { get; }

    private Move(string id, string description)
    {
        Id = id;
        Description = description;
    }

    /// <summary>
    /// Creates a move with the given id and description.
    /// </summary>
    public static Move Create(string id, string description) => new(id, description);
}
