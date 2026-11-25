using System.Text.Json;
using LearnEngine.Core.Registration;
using LearnEngine.Mcp;
using LearnEngine.Mcp.Storage;
using LearnEngine.Mcp.Tools;
using LearnEngine.Teaching;
using LearnEngine.Tests;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LearnEngine.Mcp.Tests;

/// <summary>The stdio server's tools answering with predictions, called as the AI calls them, with files in a temporary folder.</summary>
public sealed class LearnToolsTests : IDisposable
{
    private readonly string dataFolder = Path.Combine(Path.GetTempPath(), "learnengine-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(dataFolder))
            Directory.Delete(dataFolder, true);
    }

    [Fact]
    public async Task ReportTurn_ThreeTurns_AnswersWithExpectAndWhyInThePlannedShape()
    {
        using var provider = Create();
        var tools = provider.GetRequiredService<LearnTools>();
        var sessionId = ToolAnswers.Read(await tools.StartSession()).GetProperty("sessionId").GetString()!;

        var first = ToolAnswers.Read(await tools.ReportTurn(sessionId, 1, ["says.dont_get_it", "asks.for_help"], null, "recursion.base_case"));
        var second = ToolAnswers.Read(await tools.ReportTurn(sessionId, 2, ["says.dont_get_it", "asks.for_help"], MoveId(first), "recursion.base_case"));
        var third = ToolAnswers.Read(await tools.ReportTurn(sessionId, 3, ["asks.how_or_why"], MoveId(second), "recursion.call_stack"));

        foreach (var answer in new[] { first, second, third })
        {
            var expect = answer.GetProperty("expect");
            Assert.Contains(expect.GetProperty("confidence").GetString(), new[] { "high", "medium", "low" });
            Assert.Equal(["confidence", "signals", "topics"], expect.EnumerateObject().Select(property => property.Name));
            var signals = expect.GetProperty("signals");
            Assert.InRange(signals.GetArrayLength(), 0, 5);
            var chances = signals.EnumerateArray().Select(signal => signal.GetProperty("chance").GetDouble()).ToList();
            var vsUsual = signals.EnumerateArray().Select(signal => signal.GetProperty("vsUsual").GetDouble()).ToList();
            Assert.Equal(vsUsual.OrderByDescending(value => value), vsUsual);
            Assert.All(vsUsual, value => Assert.True(value >= 1));
            Assert.All(signals.EnumerateArray(), signal => Assert.False(string.IsNullOrEmpty(signal.GetProperty("signal").GetString())));
            Assert.All(chances, chance => Assert.Equal(Math.Round(chance, 2), chance));
            Assert.All(vsUsual, value => Assert.Equal(Math.Round(value, 2), value));
        }

        var topics = third.GetProperty("expect").GetProperty("topics");
        Assert.InRange(topics.GetArrayLength(), 1, 5);
        Assert.Equal("recursion.call_stack", topics[0].GetProperty("topic").GetString());
        Assert.Equal(
            ["recursion.base_case", "recursion.call_stack"],
            topics.EnumerateArray().Select(topic => topic.GetProperty("topic").GetString()).Order(StringComparer.Ordinal));
        Assert.Equal(1, topics.EnumerateArray().Sum(topic => topic.GetProperty("chance").GetDouble()), 1);

        // The plan's example puts why beside the move, and leaves it out when there is no move.
        foreach (var answer in new[] { first, second, third })
        {
            var hasMove = answer.GetProperty("move").ValueKind == JsonValueKind.Object;
            Assert.Equal(hasMove, answer.TryGetProperty("why", out var why));
            if (hasMove)
                Assert.False(string.IsNullOrEmpty(why.GetString()));
        }

        // The fields come in the order of the plan's example, with the ones it does not show in between.
        string[] planned = ["turn", "clear", "move", "why", "expect", "next"];
        foreach (var answer in new[] { first, second, third })
        {
            var names = answer.EnumerateObject().Select(property => property.Name).ToList();
            Assert.Equal(planned.Where(names.Contains), names.Where(planned.Contains));
        }
    }

    [Fact]
    public async Task ReportTurn_ClearStuck_GivesAMoveWithWhy()
    {
        using var provider = Create();
        var tools = provider.GetRequiredService<LearnTools>();
        var sessionId = ToolAnswers.Read(await tools.StartSession()).GetProperty("sessionId").GetString()!;

        JsonElement answer = default;
        for (var turn = 1; turn <= 3; turn++)
            answer = ToolAnswers.Read(await tools.ReportTurn(sessionId, turn, ["says.dont_get_it", "asks.for_help"], null, "recursion.base_case"));

        Assert.True(answer.GetProperty("clear").GetBoolean());
        Assert.Equal(JsonValueKind.Object, answer.GetProperty("move").ValueKind);
        Assert.Contains(answer.GetProperty("move").GetProperty("id").GetString()!, answer.GetProperty("why").GetString()!, StringComparison.Ordinal);
        Assert.False(answer.TryGetProperty("do", out _));
    }

    [Fact]
    public async Task ReportTurn_ClearlyStuck_ExpectsWithTheConfidenceOfWhereTheMoveLeadsAndOnlySignalsAboveUsual()
    {
        using var provider = Create();
        var tools = provider.GetRequiredService<LearnTools>();
        var sessionId = ToolAnswers.Read(await tools.StartSession()).GetProperty("sessionId").GetString()!;

        JsonElement answer = default;
        for (var turn = 1; turn <= 3; turn++)
            answer = ToolAnswers.Read(await tools.ReportTurn(sessionId, turn, ["says.dont_get_it", "asks.for_help"], null, "recursion.base_case"));

        // The state is sure, but the move picked is expected to take the student out of stuck most of the time, so
        // where they will be at the next message is less sure.
        var expect = answer.GetProperty("expect");
        Assert.Equal("high", answer.GetProperty("state").GetProperty("confidence").GetString());
        Assert.Equal("medium", expect.GetProperty("confidence").GetString());
        Assert.NotEqual(0, expect.GetProperty("signals").GetArrayLength());
        Assert.All(expect.GetProperty("signals").EnumerateArray(), signal => Assert.True(signal.GetProperty("vsUsual").GetDouble() > 1));
    }

    [Fact]
    public async Task StartSession_AfterTopics_GivesTheTopicIdsTheLearnerHasHad()
    {
        using var provider = Create();
        var tools = provider.GetRequiredService<LearnTools>();
        var firstSession = ToolAnswers.Read(await tools.StartSession());
        Assert.Equal(0, firstSession.GetProperty("topics").GetArrayLength());

        var sessionId = firstSession.GetProperty("sessionId").GetString()!;
        await tools.ReportTurn(sessionId, 1, ["asks.how_or_why"], null, "recursion.base_case");
        await tools.ReportTurn(sessionId, 2, ["offers.own_answer"], null, "recursion.call_stack");
        await tools.ReportTurn(sessionId, 3, ["answers.correctly"], null, "recursion.base_case");

        var secondSession = ToolAnswers.Read(await tools.StartSession());

        Assert.Equal(
            ["recursion.base_case", "recursion.call_stack"],
            secondSession.GetProperty("topics").EnumerateArray().Select(topic => topic.GetString()));
    }

    [Fact]
    public void ServerInstructions_StayUnderWhatAHostKeeps() =>
        Assert.True(ServerInstructions.Text.Length < 2048, $"The instructions are {ServerInstructions.Text.Length} characters.");

    private ServiceProvider Create()
    {
        var services = new ServiceCollection();
        services
            .AddFileStores(ServerSettings.Create(dataFolder, "me"))
            .AddTeachingVocabulary()
            .AddLearnEngine();
        services.AddSingleton(TurnGate.Create());
        services.AddSingleton<LearnTools>();
        return services.BuildServiceProvider();
    }

    private static string? MoveId(JsonElement answer) =>
        answer.GetProperty("move").ValueKind == JsonValueKind.Object ? answer.GetProperty("move").GetProperty("id").GetString() : null;
}
