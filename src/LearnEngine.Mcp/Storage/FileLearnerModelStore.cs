using LearnEngine.Core.Learning;

namespace LearnEngine.Mcp.Storage;

/// <summary>
/// Keeps what the engine learned about the student as one JSON file, so it outlives the container.
/// The file is models/{learnerId}.json under the data folder, written by <see cref="LearnerModelJson"/>. Every add
/// writes the whole model to a new file and then puts it in place of the old one, so an add cut short leaves the model
/// as it was before it.
/// </summary>
public sealed class FileLearnerModelStore : ILearnerModelStore
{
    private readonly string folder;
    private readonly SemaphoreSlim writing = new(1, 1);

    private FileLearnerModelStore(string folder) => this.folder = folder;

    /// <summary>
    /// Creates a store that keeps its files in the models folder under the data folder of the given settings.
    /// </summary>
    public static FileLearnerModelStore Create(ServerSettings settings) => new(Path.Combine(settings.DataFolder, "models"));

    /// <inheritdoc/>
    public async Task<LearnerModel> Load(string learnerId, CancellationToken cancellationToken)
    {
        if (!FileNames.IsSafe(learnerId))
            throw new ArgumentException($"The learner id \"{learnerId}\" is not a safe file name.", nameof(learnerId));

        var path = Path.Combine(folder, learnerId + ".json");
        if (!File.Exists(path))
            return LearnerModel.Empty;

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var read = LearnerModelJson.Read(json);
        if (read.IsT1)
            throw new InvalidDataException($"{path} is not a learner model: {read.AsT1.Reason}");

        return read.AsT0;
    }

    /// <inheritdoc/>
    public async Task Add(string learnerId, LearnerModelChange change, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (!FileNames.IsSafe(learnerId))
            throw new ArgumentException($"The learner id \"{learnerId}\" is not a safe file name.", nameof(learnerId));

        // The model is read and the new one written while holding the writing lock, so two adds at once both count.
        await writing.WaitAsync(cancellationToken);
        try
        {
            var model = (await Load(learnerId, cancellationToken)).Add(change);
            var json = LearnerModelJson.Write(model);

            var path = Path.Combine(folder, learnerId + ".json");
            var written = path + ".new";
            Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(written, json, cancellationToken);
            File.Move(written, path, overwrite: true);
        }
        finally
        {
            writing.Release();
        }
    }
}
