namespace LearnEngine.Core.Topics;

/// <summary>
/// The answer when a turn comes with a topic id that is not valid, so nothing is kept.
/// A topic id has 1 to <see cref="TopicIds.MaxLength"/> characters, each an ASCII letter, a digit, '.', '_' or '-'.
/// </summary>
public sealed record InvalidTopic
{
    /// <summary>
    /// The topic id that was sent.
    /// </summary>
    public string Topic { get; }

    private InvalidTopic(string topic) => Topic = topic;

    /// <summary>
    /// Creates the answer for the given topic id.
    /// </summary>
    public static InvalidTopic Create(string topic) => new(topic);
}
