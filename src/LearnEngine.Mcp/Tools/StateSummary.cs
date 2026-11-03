namespace LearnEngine.Mcp.Tools;

/// <summary>
/// What the engine believes about the student, for the AI only. The student is never shown it.
/// </summary>
public sealed class StateSummary
{
    /// <summary>
    /// The leading state in a few words, such as "stuck (62%): needs something concrete", or the two states it is between.
    /// </summary>
    public string Summary { get; }

    /// <summary>
    /// "high" or "medium" when the leading state is clear, and "low" when it is not.
    /// </summary>
    public string Confidence { get; }

    private StateSummary(string summary, string confidence)
    {
        Summary = summary;
        Confidence = confidence;
    }

    /// <summary>
    /// Creates the summary with the given text and confidence.
    /// </summary>
    public static StateSummary Create(string summary, string confidence) => new(summary, confidence);
}
