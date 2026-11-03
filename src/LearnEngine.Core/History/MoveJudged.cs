using System.Text.Json.Serialization;

namespace LearnEngine.Core.History;

/// <summary>
/// A picked move judged by the learner turns after it: it worked or it failed in the state it was picked for.
/// A move with no result is not judged, so it has no event.
/// </summary>
public sealed class MoveJudged : HistoryEvent
{
    /// <summary>
    /// The id of the state the move was picked for and judged in, such as "following".
    /// </summary>
    public string StateId { get; }

    /// <summary>
    /// The id of the move that was judged, such as "keep_going".
    /// </summary>
    public string MoveId { get; }

    /// <summary>
    /// True when the move worked, and false when it failed.
    /// </summary>
    public bool Worked { get; }

    // Internal, not private, so the source generated JSON reader can call it.
    [JsonConstructor]
    internal MoveJudged(string stateId, string moveId, bool worked)
    {
        StateId = stateId;
        MoveId = moveId;
        Worked = worked;
    }

    /// <summary>
    /// Creates the judgment of the given move in the given state, true when it worked and false when it failed.
    /// </summary>
    public static MoveJudged Create(string stateId, string moveId, bool worked) => new(stateId, moveId, worked);
}
