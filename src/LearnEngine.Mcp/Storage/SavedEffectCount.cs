using System.Text.Json.Serialization;

namespace LearnEngine.Mcp.Storage;

// The student's count of one step from a state to a state after a move, or after no move, in a learner model file.
internal sealed class SavedEffectCount
{
    public string FromStateId { get; }

    public string? MoveId { get; }

    public string ToStateId { get; }

    public double Count { get; }

    // Internal, not private, so the source generated JSON reader can call it.
    [JsonConstructor]
    internal SavedEffectCount(string fromStateId, string? moveId, string toStateId, double count)
    {
        FromStateId = fromStateId;
        MoveId = moveId;
        ToStateId = toStateId;
        Count = count;
    }

    public static SavedEffectCount Create(string fromStateId, string? moveId, string toStateId, double count) =>
        new(fromStateId, moveId, toStateId, count);
}
