using System.Collections.Immutable;
using System.Text.Json;
using LearnEngine.Core.Bandit;

namespace LearnEngine.Mcp.Storage;

/// <summary>
/// Keeps the student's own score of every move in every state as a file of JSON lines, so it outlives the container.
/// The file is scores/{learnerId}.jsonl under the data folder. Every added judgment appends a line with the new score and
/// the last line for a state and move is the score, so an add cut short never loses the scores saved before it.
/// </summary>
public sealed class FileScoreStore : IScoreStore
{
    private readonly string folder;
    private readonly SemaphoreSlim writing = new(1, 1);

    private FileScoreStore(string folder) => this.folder = folder;

    /// <summary>
    /// Creates a store that keeps its files in the scores folder under the data folder of the given settings.
    /// </summary>
    public static FileScoreStore Create(ServerSettings settings) => new(Path.Combine(settings.DataFolder, "scores"));

    /// <inheritdoc/>
    public async Task<ImmutableArray<MoveScore>> Load(string learnerId, string stateId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateId);
        if (!FileNames.IsSafe(learnerId))
            throw new ArgumentException($"The learner id \"{learnerId}\" is not a safe file name.", nameof(learnerId));

        var latest = await ReadLatest(learnerId, stateId, cancellationToken);
        return [.. latest.Values];
    }

    /// <inheritdoc/>
    public async Task<MoveScore> Add(string learnerId, MoveScore starting, bool worked, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(starting);
        if (!FileNames.IsSafe(learnerId))
            throw new ArgumentException($"The learner id \"{learnerId}\" is not a safe file name.", nameof(learnerId));

        var countsInRange = starting.Tried >= 0 && starting.Worked >= 0 && starting.Worked <= starting.Tried;
        if (!countsInRange)
            throw new ArgumentOutOfRangeException(nameof(starting), starting, "A score must have been tried 0 or more times and have worked from 0 to the times it was tried.");

        var workedCount = worked ? 1 : 0;

        // The score is read and the new one appended while holding the writing lock, so two adds at once both count.
        await writing.WaitAsync(cancellationToken);
        try
        {
            var latest = await ReadLatest(learnerId, starting.StateId, cancellationToken);
            var current = latest.GetValueOrDefault(starting.MoveId, starting);
            var updated = MoveScore.Create(starting.StateId, starting.MoveId, current.Worked + workedCount, current.Tried + 1);
            var saved = SavedScore.Create(updated.StateId, updated.MoveId, updated.Worked, updated.Tried);
            var line = JsonSerializer.Serialize(saved, StorageJsonContext.Default.SavedScore) + "\n";

            Directory.CreateDirectory(folder);
            await File.AppendAllTextAsync(Path.Combine(folder, learnerId + ".jsonl"), line, cancellationToken);
            return updated;
        }
        finally
        {
            writing.Release();
        }
    }

    private async Task<Dictionary<string, MoveScore>> ReadLatest(string learnerId, string stateId, CancellationToken cancellationToken)
    {
        var latest = new Dictionary<string, MoveScore>(StringComparer.Ordinal);
        var path = Path.Combine(folder, learnerId + ".jsonl");
        if (!File.Exists(path))
            return latest;

        var lines = await File.ReadAllLinesAsync(path, cancellationToken);
        for (var index = 0; index < lines.Length; index++)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
                continue;

            SavedScore? saved;
            try
            {
                saved = JsonSerializer.Deserialize(lines[index], StorageJsonContext.Default.SavedScore);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"Line {index + 1} of {path} is not a score: {exception.Message}", exception);
            }

            if (saved is null)
                throw new InvalidDataException($"Line {index + 1} of {path} is null, not a score.");

            if (saved.StateId == stateId)
                latest[saved.MoveId] = MoveScore.Create(saved.StateId, saved.MoveId, saved.Worked, saved.Tried);
        }

        return latest;
    }
}
