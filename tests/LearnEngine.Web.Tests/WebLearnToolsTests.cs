using LearnEngine.Core.Registration;
using LearnEngine.Mcp.Storage;
using LearnEngine.Teaching;
using LearnEngine.Tests;
using LearnEngine.Web.Feed;
using LearnEngine.Web.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LearnEngine.Web.Tests;

/// <summary>The web app's tools, called as the AI calls them, with files in a temporary folder.</summary>
public sealed class WebLearnToolsTests : IDisposable
{
    private readonly string dataFolder = Path.Combine(Path.GetTempPath(), "learnengine-web-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(dataFolder))
            Directory.Delete(dataFolder, true);
    }

    [Fact]
    public async Task ReportTurn_KeptTurns_PutsEachOnTheFeedInOrder()
    {
        using var provider = Create();
        var tools = provider.GetRequiredService<WebLearnTools>();
        var feed = provider.GetRequiredService<EngineFeed>();
        var heard = new List<TurnShown>();
        feed.Shown += heard.Add;
        var sessionId = ToolAnswers.Read(await tools.StartSession()).GetProperty("sessionId").GetString()!;

        await tools.ReportTurn(sessionId, 1, ["asks.how_or_why"], null, "recursion.base_case");
        await tools.ReportTurn(sessionId, 2, ["reply.minimal"]);

        Assert.Equal([1, 2], heard.Select(shown => shown.Turn));
        Assert.All(heard, shown => Assert.Equal(sessionId, shown.SessionId));
        Assert.All(heard, shown => Assert.Equal("me", shown.LearnerId));
        Assert.Equal(heard, feed.Recent);
    }

    [Fact]
    public async Task ReportTurn_NothingKept_PutsNothingOnTheFeed()
    {
        using var provider = Create();
        var tools = provider.GetRequiredService<WebLearnTools>();
        var feed = provider.GetRequiredService<EngineFeed>();
        var sessionId = ToolAnswers.Read(await tools.StartSession()).GetProperty("sessionId").GetString()!;

        var badTopic = await tools.ReportTurn(sessionId, 1, ["asks.how_or_why"], null, "not a topic");
        var badSignal = await tools.ReportTurn(sessionId, 1, ["says.nothing_known"]);

        Assert.True(badTopic.IsError);
        Assert.True(badSignal.IsError);
        Assert.Empty(feed.Recent);
    }

    [Fact]
    public async Task ReportTurn_GivesTheSameAnswerAsTheStdioServer()
    {
        using var provider = Create();
        var tools = provider.GetRequiredService<WebLearnTools>();
        var sessionId = ToolAnswers.Read(await tools.StartSession()).GetProperty("sessionId").GetString()!;

        var answer = ToolAnswers.Read(await tools.ReportTurn(sessionId, 1, ["says.dont_get_it", "asks.for_help"], null, "recursion.base_case"));

        Assert.Equal(1, answer.GetProperty("turn").GetInt32());
        Assert.True(answer.TryGetProperty("expect", out var expect));
        Assert.Contains(expect.GetProperty("confidence").GetString(), new[] { "high", "medium", "low" });
        Assert.All(expect.GetProperty("signals").EnumerateArray(), signal => Assert.True(signal.GetProperty("vsUsual").GetDouble() >= 1));
        Assert.True(answer.TryGetProperty("next", out _));
    }

    [Fact]
    public void Publish_MoreThanKept_KeepsTheLastOnes()
    {
        var feed = EngineFeed.Create();

        for (var turn = 1; turn <= EngineFeed.Kept + 5; turn++)
            feed.Publish(TurnShown.Create("me", "s", turn, DateTimeOffset.UtcNow));

        Assert.Equal(EngineFeed.Kept, feed.Recent.Length);
        Assert.Equal(6, feed.Recent[0].Turn);
        Assert.Equal(EngineFeed.Kept + 5, feed.Recent[^1].Turn);
    }

    private ServiceProvider Create()
    {
        var services = new ServiceCollection();
        services
            .AddFileStores(ServerSettings.Create(dataFolder, "me"))
            .AddTeachingVocabulary()
            .AddLearnEngine()
            .AddLearnEngineWeb();
        services.AddSingleton<WebLearnTools>();
        return services.BuildServiceProvider();
    }
}
