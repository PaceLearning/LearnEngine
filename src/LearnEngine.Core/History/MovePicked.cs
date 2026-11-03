using System.Text.Json.Serialization;

namespace LearnEngine.Core.History;

/// <summary>
/// A move the bandit picked for the learner in the clear state, such as ask_their_picture in model_clash.
/// The move is judged by the learner turns that follow it.
/// </summary>
public sealed class MovePicked : HistoryEvent
{
    /// <summary>
    /// The id of the clear state the move was picked for, such as "model_clash".
    /// </summary>
    public string StateId { get; }

    /// <summary>
    /// The id of the move that was picked, such as "ask_their_picture".
    /// </summary>
    public string MoveId { get; }

    // Internal, not private, so the source generated JSON reader can call it.
    [JsonConstructor]
    internal MovePicked(string stateId, string moveId)
    {
        StateId = stateId;
        MoveId = moveId;
    }

    /// <summary>
    /// Creates the pick of the given move for the given clear state.
    /// </summary>
    public static MovePicked Create(string stateId, string moveId) => new(stateId, moveId);
}
