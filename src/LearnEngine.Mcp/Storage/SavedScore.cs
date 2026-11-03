using System.Text.Json.Serialization;

namespace LearnEngine.Mcp.Storage;

// One line of a score file: the student's score of a move in a state after one judgment.
internal sealed class SavedScore
{
    public string StateId { get; }

    public string MoveId { get; }

    public int Worked { get; }

    public int Tried { get; }

    // Internal, not private, so the source generated JSON reader can call it.
    [JsonConstructor]
    internal SavedScore(string stateId, string moveId, int worked, int tried)
    {
        StateId = stateId;
        MoveId = moveId;
        Worked = worked;
        Tried = tried;
    }

    public static SavedScore Create(string stateId, string moveId, int worked, int tried) => new(stateId, moveId, worked, tried);
}
