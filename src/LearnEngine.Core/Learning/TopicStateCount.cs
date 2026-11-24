using System.Text.Json.Serialization;
using LearnEngine.Core.Topics;

namespace LearnEngine.Core.Learning;

/// <summary>
/// How many of one learner's turns on a topic were in a state, such as 2.4 turns stuck on recursion.base_case.
/// The count is soft: a turn adds the chance the learner was in the state after it, not a whole turn.
/// </summary>
public sealed record TopicStateCount
{
    /// <summary>
    /// The id of the topic, such as "recursion.base_case".
    /// </summary>
    public string Topic { get; }

    /// <summary>
    /// The id of the state, such as "stuck".
    /// </summary>
    public string StateId { get; }

    /// <summary>
    /// The soft count of turns on the topic in the state. It is 0 or more.
    /// </summary>
    public double Count { get; }

    // Internal, not private, so the source generated JSON reader of the history can call it.
    [JsonConstructor]
    internal TopicStateCount(string topic, string stateId, double count)
    {
        Topic = topic;
        StateId = stateId;
        Count = count;
    }

    /// <summary>
    /// Creates the count of the given state on the given topic. A topic that is not a valid topic id, an empty state id,
    /// or a count below 0, infinite or not a number, throws.
    /// </summary>
    public static TopicStateCount Create(string topic, string stateId, double count)
    {
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateId);
        if (!TopicIds.IsValid(topic))
            throw new ArgumentException($"The topic \"{topic}\" is not a valid topic id.", nameof(topic));

        // Written so a NaN fails too, which a plain bound check would let through.
        var inRange = count >= 0 && double.IsFinite(count);
        if (!inRange)
            throw new ArgumentOutOfRangeException(nameof(count), count, "A count must be 0 or more and finite.");

        return new(topic, stateId, count);
    }
}
