namespace LearnEngine.Core.Topics;

/// <summary>
/// Checks a topic id the AI sends with a turn, such as "recursion.base_case". Topics are not registered, so any id
/// that passes this check is a topic.
/// </summary>
public static class TopicIds
{
    /// <summary>
    /// The most characters a topic id may have.
    /// </summary>
    public const int MaxLength = 64;

    /// <summary>
    /// True when the id has 1 to <see cref="MaxLength"/> characters, each an ASCII letter, a digit, '.', '_' or '-'.
    /// </summary>
    public static bool IsValid(string topic) =>
        topic.Length > 0
        && topic.Length <= MaxLength
        && topic.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');
}
