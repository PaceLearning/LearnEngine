namespace LearnEngine.Core.Learning;

/// <summary>
/// The answer when JSON is not a learner model the engine can read, such as a damaged models file.
/// </summary>
public sealed record UnreadableLearnerModel
{
    /// <summary>
    /// The JSON that was read.
    /// </summary>
    public string Json { get; }

    /// <summary>
    /// Why the JSON could not be read, written for the person reading the app's logs.
    /// </summary>
    public string Reason { get; }

    private UnreadableLearnerModel(string json, string reason)
    {
        Json = json;
        Reason = reason;
    }

    /// <summary>
    /// Creates the answer for the given JSON, with why it could not be read.
    /// </summary>
    public static UnreadableLearnerModel Create(string json, string reason) => new(json, reason);
}
