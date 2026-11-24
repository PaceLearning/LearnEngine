using System.Text.Json.Serialization;

namespace LearnEngine.Mcp.Storage;

// The student's count of one topic followed by another, in a learner model file.
internal sealed class SavedTopicLinkCount
{
    public string FromTopic { get; }

    public string ToTopic { get; }

    public double Count { get; }

    // Internal, not private, so the source generated JSON reader can call it.
    [JsonConstructor]
    internal SavedTopicLinkCount(string fromTopic, string toTopic, double count)
    {
        FromTopic = fromTopic;
        ToTopic = toTopic;
        Count = count;
    }

    public static SavedTopicLinkCount Create(string fromTopic, string toTopic, double count) => new(fromTopic, toTopic, count);
}
