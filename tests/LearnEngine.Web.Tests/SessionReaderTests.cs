using System.Globalization;
using LearnEngine.Core.Choosing;
using LearnEngine.Core.History;
using LearnEngine.Core.Prediction;
using LearnEngine.Core.Registration;
using LearnEngine.Mcp.Storage;
using LearnEngine.Mcp.Tools;
using LearnEngine.Teaching;
using LearnEngine.Tests;
using LearnEngine.Web.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LearnEngine.Web.Tests;

/// <summary>Reading a session back from its history for the Session page.</summary>
public sealed class SessionReaderTests : IDisposable
{
    private const string Learner = "me";
    private const int Precision = 10;

    private readonly string dataFolder = Path.Combine(Path.GetTempPath(), "learnengine-web-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(dataFolder))
            Directory.Delete(dataFolder, true);
    }

    [Fact]
    public async Task Read_ThreeTurns_GivesEachTurnAsTheHistoryKeptIt()
    {
        using var provider = Create();
        var tools = provider.GetRequiredService<LearnTools>();
        var reader = provider.GetRequiredService<SessionReader>();
        var sessionId = ToolAnswers.Read(await tools.StartSession()).GetProperty("sessionId").GetString()!;
        await tools.ReportTurn(sessionId, 1, ["says.dont_get_it", "asks.for_help"], null, "recursion.base_case");
        await tools.ReportTurn(sessionId, 2, ["says.dont_get_it", "asks.for_help"], null, "recursion.base_case");
        await tools.ReportTurn(sessionId, 3, ["says.dont_get_it", "asks.for_help"], null, "recursion.call_stack");

        var view = (await reader.Read(Learner, sessionId, CancellationToken.None)).AsT0;
        var history = (await provider.GetRequiredService<IHistoryStore>().Load(Learner, sessionId, CancellationToken.None)).AsT0;

        Assert.Equal([1, 2, 3], view.Turns.Select(turn => turn.Number));
        Assert.Equal(["recursion.base_case", "recursion.base_case", "recursion.call_stack"], view.Turns.Select(turn => turn.Topic));
        Assert.Equal(history.OfType<TurnLearned>().Select(learned => learned.Odds), view.Turns.Select(turn => turn.Odds));

        // Each turn is measured against what was kept as expected of it: the first against the session start's.
        var kept = history.OfType<TurnExpected>().ToList();
        for (var index = 0; index < 3; index++)
        {
            Assert.Equal(kept[index].Expected.Signals.AsEnumerable(), view.Turns[index].Expected!.Signals.AsEnumerable());
            Assert.Equal(Predictor.Surprise(kept[index].Expected, view.Turns[index].SignalIds), view.Turns[index].Surprise!.Value, Precision);
            Assert.Equal(Predictor.UsualSurprise(kept[index].Expected), view.Turns[index].UsualSurprise!.Value, Precision);
        }

        Assert.Equal(kept[^1].Expected.Signals.AsEnumerable(), view.ExpectedNext!.Signals.AsEnumerable());

        // The usual chances are read back as kept, so the Session page shows how each signal compared with them that day.
        Assert.All(view.ExpectedNext.Signals, chance => Assert.True(chance.Usual > 0));
        Assert.All(view.ExpectedNext.AboveUsual, chance => Assert.True(chance.VsUsual > 1));
        Assert.Equal(view.Turns[^1].Odds.AsEnumerable(), view.Belief.All.AsEnumerable());
    }

    [Fact]
    public async Task Read_ClearLeader_GivesThePickAndEveryAllowedMoveWeighed()
    {
        using var provider = Create();
        var tools = provider.GetRequiredService<LearnTools>();
        var reader = provider.GetRequiredService<SessionReader>();
        var sessionId = ToolAnswers.Read(await tools.StartSession()).GetProperty("sessionId").GetString()!;
        for (var turn = 1; turn <= 3; turn++)
            await tools.ReportTurn(sessionId, turn, ["says.dont_get_it", "asks.for_help"]);

        var view = (await reader.Read(Learner, sessionId, CancellationToken.None)).AsT0;

        Assert.True(view.IsClear);
        Assert.NotNull(view.Pick);
        Assert.Equal(view.Belief.Leader.StateId, view.Pick.StateId);
        var allowed = provider.GetRequiredService<AllowedMoveCatalog>().Find(view.Pick.StateId).AsT0;
        Assert.Equal(allowed.Select(move => move.MoveId), view.Weighed.Select(move => move.MoveId));

        // Nothing changed the learner's model since the pick, so the highest weighed move is the one picked.
        Assert.Equal(view.Pick.MoveId, view.Weighed.MaxBy(move => move.Bound)!.MoveId);
        Assert.Same(view.Turns[^1].Pick, view.Pick);
    }

    [Fact]
    public async Task Read_AfterTheLearnerModelMoved_GivesTheSameTurnsAsBefore()
    {
        using var provider = Create();
        var tools = provider.GetRequiredService<LearnTools>();
        var reader = provider.GetRequiredService<SessionReader>();
        var first = ToolAnswers.Read(await tools.StartSession()).GetProperty("sessionId").GetString()!;
        await tools.ReportTurn(first, 1, ["asks.how_or_why"], null, "recursion.base_case");
        await tools.ReportTurn(first, 2, ["reply.minimal"], null, "recursion.base_case");
        var before = (await reader.Read(Learner, first, CancellationToken.None)).AsT0;

        // Another session teaches the engine a lot about the same learner.
        var second = ToolAnswers.Read(await tools.StartSession()).GetProperty("sessionId").GetString()!;
        for (var turn = 1; turn <= 10; turn++)
            await tools.ReportTurn(second, turn, ["says.dont_get_it"], null, "recursion.call_stack");

        var after = (await reader.Read(Learner, first, CancellationToken.None)).AsT0;

        Assert.Equal(before.Turns.Select(turn => turn.Odds), after.Turns.Select(turn => turn.Odds));
        Assert.Equal(before.Turns.Select(turn => turn.Surprise), after.Turns.Select(turn => turn.Surprise));
        Assert.Equal(before.ExpectedNext!.Signals.AsEnumerable(), after.ExpectedNext!.Signals.AsEnumerable());
    }

    [Fact]
    public async Task Read_NoHistory_GivesSessionNotStarted()
    {
        using var provider = Create();

        var read = await provider.GetRequiredService<SessionReader>().Read(Learner, "20261123-140200-aaaaaaa", CancellationToken.None);

        Assert.True(read.IsT1);
    }

    [Fact]
    public async Task Read_AnotherVocabulary_GivesOtherVocabulary()
    {
        using var provider = Create();
        await provider.GetRequiredService<IHistoryStore>().Append(Learner, "old", SessionStarted.Create("0000000000000000", 0.15, 0.15), CancellationToken.None);

        var read = await provider.GetRequiredService<SessionReader>().Read(Learner, "old", CancellationToken.None);

        Assert.Equal("0000000000000000", read.AsT2.Found);
    }

    [Fact]
    public void For_SessionsOfTodayYesterdayAndBefore_NamesThemByDay()
    {
        var started = SessionLabels.Started("20261123-140200-aaaaaaa")!.Value;
        var time = started.ToString("HH:mm", CultureInfo.InvariantCulture);

        Assert.Equal($"Today {time}", SessionLabels.For("20261123-140200-aaaaaaa", started.AddMinutes(5)));
        Assert.Equal($"Yesterday {time}", SessionLabels.For("20261123-140200-aaaaaaa", started.AddDays(1)));
        Assert.Equal($"{started.ToString("MMM d", CultureInfo.InvariantCulture)}, {time}", SessionLabels.For("20261123-140200-aaaaaaa", started.AddDays(3)));
        Assert.Equal("old", SessionLabels.For("old", started));
    }

    private ServiceProvider Create()
    {
        var services = new ServiceCollection();
        services
            .AddFileStores(ServerSettings.Create(dataFolder, Learner))
            .AddTeachingVocabulary()
            .AddLearnEngine()
            .AddLearnEngineWeb();
        return services.BuildServiceProvider();
    }
}
