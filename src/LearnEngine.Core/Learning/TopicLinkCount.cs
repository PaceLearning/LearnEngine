using System.Text.Json.Serialization;
using LearnEngine.Core.Topics;

namespace LearnEngine.Core.Learning;

/// <summary>
/// How many times one learner asked about a topic right after another, such as recursion.call_stack after
/// recursion.base_case 3 times. A turn on the same topic as the turn before counts as a link from that topic to itself.
/// </summary>
public sealed record TopicLinkCount
{
    /// <summary>
    /// The id of the topic of the earlier turn, such as "recursion.base_case".
    /// </summary>
    public string FromTopic { get; }

    /// <summary>
    /// The id of the topic of the turn after it, such as "recursion.call_stack".
    /// </summary>
    public string ToTopic { get; }

    /// <summary>
    /// How many times the learner went from one topic to the other. It is 0 or more.
    /// </summary>
    public double Count { get; }

    // Internal, not private, so the source generated JSON readers of the history and of the learner model can call it.
    [JsonConstructor]
    internal TopicLinkCount(string fromTopic, string toTopic, double count)
    {
        FromTopic = fromTopic;
        ToTopic = toTopic;
        Count = count;
    }

    /// <summary>
    /// Creates the count of the given link. A topic that is not a valid topic id, or a count below 0, infinite or not
    /// a number, throws.
    /// </summary>
    public static TopicLinkCount Create(string fromTopic, string toTopic, double count)
    {
        ArgumentNullException.ThrowIfNull(fromTopic);
        ArgumentNullException.ThrowIfNull(toTopic);
        if (!TopicIds.IsValid(fromTopic))
            throw new ArgumentException($"The topic \"{fromTopic}\" is not a valid topic id.", nameof(fromTopic));

        if (!TopicIds.IsValid(toTopic))
            throw new ArgumentException($"The topic \"{toTopic}\" is not a valid topic id.", nameof(toTopic));

        // Written so a NaN fails too, which a plain bound check would let through.
        var inRange = count >= 0 && double.IsFinite(count);
        if (!inRange)
            throw new ArgumentOutOfRangeException(nameof(count), count, "A count must be 0 or more and finite.");

        return new(fromTopic, toTopic, count);
    }
}
