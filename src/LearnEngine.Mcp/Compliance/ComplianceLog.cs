using System.Text.Json;
using LearnEngine.Mcp.Storage;

namespace LearnEngine.Mcp.Compliance;

/// <summary>
/// Appends what the AI did with the tools to a log of JSON lines per session, compliance/{learnerId}/{sessionId}.jsonl
/// under the data folder: the turn numbers it reported, the turns it skipped and whether it used the moves it was given.
/// </summary>
public sealed class ComplianceLog
{
    private readonly string folder;
    private readonly SemaphoreSlim writing = new(1, 1);

    private ComplianceLog(string folder) => this.folder = folder;

    /// <summary>
    /// Creates a log that keeps its files in the compliance folder under the data folder of the given settings.
    /// </summary>
    public static ComplianceLog Create(ServerSettings settings) => new(Path.Combine(settings.DataFolder, "compliance"));

    /// <summary>
    /// Adds the entry to the end of the given session's log.
    /// </summary>
    public async Task Append(string learnerId, string sessionId, ComplianceEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!FileNames.IsSafe(learnerId) || !FileNames.IsSafe(sessionId))
            throw new ArgumentException($"The learner id \"{learnerId}\" or the session id \"{sessionId}\" is not a safe file name.");

        var learnerFolder = Path.Combine(folder, learnerId);
        var line = JsonSerializer.Serialize(entry, StorageJsonContext.Default.ComplianceEntry) + "\n";

        await writing.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(learnerFolder);
            await File.AppendAllTextAsync(Path.Combine(learnerFolder, sessionId + ".jsonl"), line, cancellationToken);
        }
        finally
        {
            writing.Release();
        }
    }

    /// <summary>
    /// Gives the last entry of the given session's log, or null when the session has none or its last line cannot be
    /// read. The log only measures the AI, so a broken line never stops a session.
    /// </summary>
    public async Task<ComplianceEntry?> Last(string learnerId, string sessionId, CancellationToken cancellationToken)
    {
        if (!FileNames.IsSafe(learnerId) || !FileNames.IsSafe(sessionId))
            throw new ArgumentException($"The learner id \"{learnerId}\" or the session id \"{sessionId}\" is not a safe file name.");

        var path = Path.Combine(folder, learnerId, sessionId + ".jsonl");
        if (!File.Exists(path))
            return null;

        var lines = await File.ReadAllLinesAsync(path, cancellationToken);
        var last = lines.LastOrDefault(line => !string.IsNullOrWhiteSpace(line));
        if (last is null)
            return null;

        try
        {
            return JsonSerializer.Deserialize(last, StorageJsonContext.Default.ComplianceEntry);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
