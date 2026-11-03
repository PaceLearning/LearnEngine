namespace LearnEngine.Mcp.Tools;

/// <summary>
/// The move the AI makes in its next reply, with what to do.
/// </summary>
public sealed class MoveToMake
{
    /// <summary>
    /// The id of the move, such as "worked_example". The AI sends it back as lastMoveId once it used the move.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// What the AI does in its next reply, in one line.
    /// </summary>
    public string Do { get; }

    private MoveToMake(string id, string @do)
    {
        Id = id;
        Do = @do;
    }

    /// <summary>
    /// Creates the move with the given id and what to do.
    /// </summary>
    public static MoveToMake Create(string id, string @do) => new(id, @do);
}
