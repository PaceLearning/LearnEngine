using LearnEngine.Core.History;
using LearnEngine.Mcp.Storage;
using Xunit;

namespace LearnEngine.Mcp.Tests;

/// <summary>Listing the sessions a learner has history files for.</summary>
public sealed class FileHistoryStoreTests : IDisposable
{
    private readonly string dataFolder = Path.Combine(Path.GetTempPath(), "learnengine-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(dataFolder))
            Directory.Delete(dataFolder, true);
    }

    [Fact]
    public async Task SessionIds_GivesEverySessionNewestFirst()
    {
        var store = FileHistoryStore.Create(ServerSettings.Create(dataFolder, "me"));
        var start = SessionStarted.Create("fingerprint", 0.15, 0.15);

        await store.Append("me", "20261120-091500-aaaaaaa", start, CancellationToken.None);
        await store.Append("me", "20261123-140200-bbbbbbb", start, CancellationToken.None);
        await store.Append("me", "20261122-194000-ccccccc", start, CancellationToken.None);
        await store.Append("someone-else", "20261124-080000-ddddddd", start, CancellationToken.None);

        Assert.Equal(
            ["20261123-140200-bbbbbbb", "20261122-194000-ccccccc", "20261120-091500-aaaaaaa"],
            store.SessionIds("me").AsEnumerable());
    }

    [Fact]
    public void SessionIds_LearnerWithNone_GivesNone() =>
        Assert.Empty(FileHistoryStore.Create(ServerSettings.Create(dataFolder, "me")).SessionIds("me"));
}
