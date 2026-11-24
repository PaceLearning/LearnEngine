namespace LearnEngine.Mcp.Tools;

/// <summary>
/// A topic the engine expects the student's next message to be about, with its chance. For the AI only.
/// </summary>
public sealed class ExpectedTopic
{
    /// <summary>
    /// The topic id, such as "recursion.base_case".
    /// </summary>
    public string Topic { get; }

    /// <summary>
    /// The chance that the next message is about the topic, from 0 to 1, rounded to two places.
    /// </summary>
    public double Chance { get; }

    private ExpectedTopic(string topic, double chance)
    {
        Topic = topic;
        Chance = chance;
    }

    /// <summary>
    /// Creates the expected topic with the given id and chance.
    /// </summary>
    public static ExpectedTopic Create(string topic, double chance) => new(topic, chance);
}
