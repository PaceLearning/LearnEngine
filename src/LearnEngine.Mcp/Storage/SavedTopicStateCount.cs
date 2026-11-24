using System.Text.Json.Serialization;

namespace LearnEngine.Mcp.Storage;

// The student's count of one state on one topic, in a learner model file.
internal sealed class SavedTopicStateCount
{
    public string Topic { get; }

    public string StateId { get; }

    public double Count { get; }

    // Internal, not private, so the source generated JSON reader can call it.
    [JsonConstructor]
    internal SavedTopicStateCount(string topic, string stateId, double count)
    {
        Topic = topic;
        StateId = stateId;
        Count = count;
    }

    public static SavedTopicStateCount Create(string topic, string stateId, double count) => new(topic, stateId, count);
}
