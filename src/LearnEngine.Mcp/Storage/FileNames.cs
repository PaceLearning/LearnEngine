namespace LearnEngine.Mcp.Storage;

/// <summary>
/// Checks an id before it names a file or folder, so an id sent by the AI can never reach outside the data folder.
/// </summary>
public static class FileNames
{
    /// <summary>
    /// The most characters an id may have.
    /// </summary>
    public const int MaxLength = 64;

    /// <summary>
    /// True when the id has 1 to <see cref="MaxLength"/> characters, each an ASCII letter, a digit, - or _.
    /// </summary>
    public static bool IsSafe(string id) =>
        id.Length > 0
        && id.Length <= MaxLength
        && id.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
}
