using System.Text.Json.Serialization;

namespace LearnEngine.Core.History;

/// <summary>
/// A move chosen for the learner in the clear state, such as ask_their_picture in model_clash, with the chance it
/// brings them to a goal state and why it was chosen. It is kept as it was chosen, so a session is read back with the
/// picks of that day.
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

    /// <summary>
    /// The chance, from 0 to 1, that the move brings the learner to a goal state by their next turn, as it was when picked.
    /// </summary>
    public double GoalChance { get; }

    /// <summary>
    /// The sentence that said why the move was picked, such as
    /// "For this learner, worked_example led from stuck to following or eager 6 of 8 times."
    /// </summary>
    public string Why { get; }

    // Internal, not private, so the source generated JSON reader can call it.
    [JsonConstructor]
    internal MovePicked(string stateId, string moveId, double goalChance, string why)
    {
        StateId = stateId;
        MoveId = moveId;
        GoalChance = goalChance;
        Why = why;
    }

    /// <summary>
    /// Creates the pick of the given move for the given clear state, with its goal chance and why.
    /// </summary>
    public static MovePicked Create(string stateId, string moveId, double goalChance, string why) => new(stateId, moveId, goalChance, why);
}
