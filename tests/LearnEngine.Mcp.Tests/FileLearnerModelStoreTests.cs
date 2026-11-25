using LearnEngine.Core.Learning;
using LearnEngine.Mcp.Storage;
using Xunit;

namespace LearnEngine.Mcp.Tests;

/// <summary>Keeping a learner model as a file and reading files written by earlier versions of the store.</summary>
public sealed class FileLearnerModelStoreTests : IDisposable
{
    private readonly string dataFolder = Path.Combine(Path.GetTempPath(), "learnengine-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(dataFolder))
            Directory.Delete(dataFolder, true);
    }

    [Fact]
    public async Task Add_ThenLoad_GivesTheCountsAdded()
    {
        var store = FileLearnerModelStore.Create(ServerSettings.Create(dataFolder, "me"));
        var change = LearnerModelChange.Create(
            [RateCount.Create("says.dont_get_it", "stuck", 1, 2)],
            [EffectCount.Create("stuck", "worked_example", "following", 0.8)],
            [TopicLinkCount.Create("recursion.base_case", "recursion.call_stack", 1)],
            [TopicStateCount.Create("recursion.base_case", "stuck", 0.7)]);

        await store.Add("me", change, CancellationToken.None);
        await store.Add("me", change, CancellationToken.None);
        var model = await store.Load("me", CancellationToken.None);

        Assert.Equal([RateCount.Create("says.dont_get_it", "stuck", 2, 4)], model.Rates.AsEnumerable());
        Assert.Equal([EffectCount.Create("stuck", "worked_example", "following", 1.6)], model.Effects.AsEnumerable());
        Assert.Equal([TopicLinkCount.Create("recursion.base_case", "recursion.call_stack", 2)], model.TopicLinks.AsEnumerable());
        Assert.Equal([TopicStateCount.Create("recursion.base_case", "stuck", 1.4)], model.TopicStates.AsEnumerable());
    }

    [Theory]
    // As the store wrote a model before it used LearnerModelJson.
    [InlineData("""{"rates":[{"signal_id":"says.dont_get_it","state_id":"stuck","shown":1,"not_shown":2}],"effects":[{"from_state_id":"stuck","move_id":null,"to_state_id":"following","count":0.5}],"topic_links":[{"from_topic":"recursion.base_case","to_topic":"recursion.call_stack","count":1}],"topic_states":[{"topic":"recursion.base_case","state_id":"stuck","count":0.7}]}""")]
    // As the store wrote a model before it learned effects and topics.
    [InlineData("""{"rates":[{"signal_id":"says.dont_get_it","state_id":"stuck","shown":1,"not_shown":2}]}""")]
    public async Task Load_FileWrittenByAnEarlierStore_ReadsIt(string json)
    {
        var models = Path.Combine(dataFolder, "models");
        Directory.CreateDirectory(models);
        await File.WriteAllTextAsync(Path.Combine(models, "me.json"), json);

        var model = await FileLearnerModelStore.Create(ServerSettings.Create(dataFolder, "me")).Load("me", CancellationToken.None);

        Assert.Equal([RateCount.Create("says.dont_get_it", "stuck", 1, 2)], model.Rates.AsEnumerable());
    }

    [Fact]
    public async Task Load_DamagedFile_Throws()
    {
        var models = Path.Combine(dataFolder, "models");
        Directory.CreateDirectory(models);
        await File.WriteAllTextAsync(Path.Combine(models, "me.json"), "not json");

        var store = FileLearnerModelStore.Create(ServerSettings.Create(dataFolder, "me"));

        await Assert.ThrowsAsync<InvalidDataException>(() => store.Load("me", CancellationToken.None));
    }
}
