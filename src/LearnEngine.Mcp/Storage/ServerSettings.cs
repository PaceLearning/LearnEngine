namespace LearnEngine.Mcp.Storage;

/// <summary>
/// Where the server keeps its files and whose they are. One student runs one server, so the learner id is fixed.
/// </summary>
public sealed class ServerSettings
{
    /// <summary>
    /// The folder every history, score and compliance file is kept under, such as /data in the Docker image.
    /// </summary>
    public string DataFolder { get; }

    /// <summary>
    /// The id of the one student this server teaches, such as "me".
    /// </summary>
    public string LearnerId { get; }

    private ServerSettings(string dataFolder, string learnerId)
    {
        DataFolder = dataFolder;
        LearnerId = learnerId;
    }

    /// <summary>
    /// Creates the settings for the given data folder and learner id. The learner id names a folder, so an id that is
    /// not a safe file name throws here and stops the server at startup.
    /// </summary>
    public static ServerSettings Create(string dataFolder, string learnerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataFolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(learnerId);

        if (!FileNames.IsSafe(learnerId))
            throw new ArgumentException($"The learner id \"{learnerId}\" may only have letters, digits, - and _, and at most {FileNames.MaxLength} of them.", nameof(learnerId));

        return new(Path.GetFullPath(dataFolder), learnerId);
    }
}
