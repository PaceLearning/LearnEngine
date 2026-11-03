using System.Collections.Immutable;
using System.Text.Json;
using LearnEngine.Core.Bandit;

namespace LearnEngine.Mcp.Storage;

/// <summary>
/// Keeps the student's own score of every move in every state as a file of JSON lines, so it outlives the container.
/// The file is scores/{learnerId}.jsonl under the data folder. Every save appends a line and the last line for a state
/// and move is the score, so a save cut short never loses the scores saved before it.
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

        var path = Path.Combine(folder, learnerId + ".jsonl");
        if (!File.Exists(path))
            return [];

        var lines = await File.ReadAllLinesAsync(path, cancellationToken);
        var latest = new Dictionary<string, MoveScore>(StringComparer.Ordinal);
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

        return [.. latest.Values];
    }

    /// <inheritdoc/>
    public async Task Save(string learnerId, MoveScore score, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(score);
        if (!FileNames.IsSafe(learnerId))
            throw new ArgumentException($"The learner id \"{learnerId}\" is not a safe file name.", nameof(learnerId));

        var countsInRange = score.Tried >= 0 && score.Worked >= 0 && score.Worked <= score.Tried;
        if (!countsInRange)
            throw new ArgumentOutOfRangeException(nameof(score), score, "A score must have been tried 0 or more times and have worked from 0 to the times it was tried.");

        var saved = SavedScore.Create(score.StateId, score.MoveId, score.Worked, score.Tried);
        var line = JsonSerializer.Serialize(saved, StorageJsonContext.Default.SavedScore) + "\n";

        await writing.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(folder);
            await File.AppendAllTextAsync(Path.Combine(folder, learnerId + ".jsonl"), line, cancellationToken);
        }
        finally
        {
            writing.Release();
        }
    }
}
