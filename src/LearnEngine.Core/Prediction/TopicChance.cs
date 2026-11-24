using System.Text.Json.Serialization;

namespace LearnEngine.Core.Prediction;

/// <summary>
/// The chance that the next learner turn is about a topic, such as a 55% chance of recursion.base_case.
/// </summary>
public sealed record TopicChance
{
    /// <summary>
    /// The id of the topic, such as "recursion.base_case".
    /// </summary>
    public string Topic { get; }

    /// <summary>
    /// The chance that the next turn is about the topic, from 0 to 1.
    /// </summary>
    public double Chance { get; }

    // Internal, not private, so the source generated JSON reader of the history can call it.
    [JsonConstructor]
    internal TopicChance(string topic, double chance)
    {
        Topic = topic;
        Chance = chance;
    }

    /// <summary>
    /// Creates the chance of the given topic.
    /// </summary>
    public static TopicChance Create(string topic, double chance) => new(topic, chance);
}
