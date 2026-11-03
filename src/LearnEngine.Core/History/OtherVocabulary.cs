namespace LearnEngine.Core.History;

/// <summary>
/// The answer when a history was kept under another vocabulary than the one the app registers now, so replaying it
/// would give a belief the session never had.
/// </summary>
public sealed record OtherVocabulary
{
    /// <summary>
    /// The fingerprint of the vocabulary the app registers now.
    /// </summary>
    public string Expected { get; }

    /// <summary>
    /// The fingerprint the history was stamped with when its session started.
    /// </summary>
    public string Found { get; }

    private OtherVocabulary(string expected, string found)
    {
        Expected = expected;
        Found = found;
    }

    /// <summary>
    /// Creates the answer for a history stamped with the found fingerprint when the expected one was registered.
    /// </summary>
    public static OtherVocabulary Create(string expected, string found) => new(expected, found);
}
