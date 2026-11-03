using System.Collections.Immutable;
using LearnEngine.Core.History;

namespace LearnEngine.Mcp.Storage;

/// <summary>
/// Keeps every session's history as a file of JSON lines, one line per event, so it outlives the container.
/// A session's file is histories/{learnerId}/{sessionId}.jsonl under the data folder, and it is only ever appended to.
/// </summary>
public sealed class FileHistoryStore : IHistoryStore
{
    private readonly string folder;
    private readonly SemaphoreSlim writing = new(1, 1);

    private FileHistoryStore(string folder) => this.folder = folder;

    /// <summary>
    /// Creates a store that keeps its files in the histories folder under the data folder of the given settings.
    /// </summary>
    public static FileHistoryStore Create(ServerSettings settings) => new(Path.Combine(settings.DataFolder, "histories"));

    /// <inheritdoc/>
    public async Task Append(string learnerId, string sessionId, HistoryEvent historyEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(historyEvent);
        if (!FileNames.IsSafe(learnerId) || !FileNames.IsSafe(sessionId))
            throw new ArgumentException($"The learner id \"{learnerId}\" or the session id \"{sessionId}\" is not a safe file name.");

        var learnerFolder = Path.Combine(folder, learnerId);
        var line = HistoryEventJson.Write(historyEvent) + "\n";

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

    /// <inheritdoc/>
    public async Task<ImmutableArray<HistoryEvent>> Load(string learnerId, string sessionId, CancellationToken cancellationToken)
    {
        if (!FileNames.IsSafe(learnerId) || !FileNames.IsSafe(sessionId))
            throw new ArgumentException($"The learner id \"{learnerId}\" or the session id \"{sessionId}\" is not a safe file name.");

        var path = Path.Combine(folder, learnerId, sessionId + ".jsonl");
        if (!File.Exists(path))
            return [];

        var lines = await File.ReadAllLinesAsync(path, cancellationToken);
        var history = ImmutableArray.CreateBuilder<HistoryEvent>(lines.Length);
        for (var index = 0; index < lines.Length; index++)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
                continue;

            // A line the engine cannot read means the file was changed by hand or cut short, and replaying the rest
            // would give a belief the session never had, so the session stops here instead.
            var read = HistoryEventJson.Read(lines[index]);
            if (read.IsT1)
                throw new InvalidDataException($"Line {index + 1} of {path} is not a history event: {read.AsT1.Reason}");

            history.Add(read.AsT0);
        }

        return history.ToImmutable();
    }
}
