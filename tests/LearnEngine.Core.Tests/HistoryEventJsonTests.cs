using System.Text.Json;
using LearnEngine.Core.History;
using Xunit;

namespace LearnEngine.Core.Tests;

/// <summary>Writing history events as JSON and reading them back, through the engine and through a host's own serializer.</summary>
public sealed class HistoryEventJsonTests
{
    [Fact]
    public void Write_SessionStarted_ReadsBackTheSame()
    {
        var line = HistoryEventJson.Write(SessionStarted.Create("0123456789abcdef", 0.15, 0.2));

        var read = HistoryEventJson.Read(line);

        Assert.Equal("""{"event":"session_started","fingerprint":"0123456789abcdef","fade":0.15,"clear_margin":0.2}""", line);
        var start = Assert.IsType<SessionStarted>(read.AsT0);
        Assert.Equal("0123456789abcdef", start.Fingerprint);
        Assert.Equal(0.15, start.Fade);
        Assert.Equal(0.2, start.ClearMargin);
    }

    [Fact]
    public void Write_LearnerTurn_ReadsBackTheSame()
    {
        var line = HistoryEventJson.Write(LearnerTurn.Create(["asks.how_or_why", "reply.minimal"]));

        var read = HistoryEventJson.Read(line);

        Assert.Equal("""{"event":"learner_turn","signal_ids":["asks.how_or_why","reply.minimal"]}""", line);
        var turn = Assert.IsType<LearnerTurn>(read.AsT0);
        Assert.Equal(["asks.how_or_why", "reply.minimal"], turn.SignalIds.ToArray());
    }

    [Fact]
    public void Write_MovePicked_ReadsBackTheSame()
    {
        var line = HistoryEventJson.Write(MovePicked.Create("model_clash", "ask_their_picture"));

        var read = HistoryEventJson.Read(line);

        Assert.Equal("""{"event":"move_picked","state_id":"model_clash","move_id":"ask_their_picture"}""", line);
        var picked = Assert.IsType<MovePicked>(read.AsT0);
        Assert.Equal("model_clash", picked.StateId);
        Assert.Equal("ask_their_picture", picked.MoveId);
    }

    [Fact]
    public void Write_MoveJudged_ReadsBackTheSame()
    {
        var line = HistoryEventJson.Write(MoveJudged.Create("following", "keep_going", false));

        var read = HistoryEventJson.Read(line);

        Assert.Equal("""{"event":"move_judged","state_id":"following","move_id":"keep_going","worked":false}""", line);
        var judged = Assert.IsType<MoveJudged>(read.AsT0);
        Assert.Equal("following", judged.StateId);
        Assert.Equal("keep_going", judged.MoveId);
        Assert.False(judged.Worked);
    }

    [Fact]
    public void Read_EventNameAfterTheValues_ReadsTheEvent()
    {
        var read = HistoryEventJson.Read("""{"signal_ids":["reply.minimal"],"event":"learner_turn"}""");

        var turn = Assert.IsType<LearnerTurn>(read.AsT0);
        Assert.Equal(["reply.minimal"], turn.SignalIds.ToArray());
    }

    [Theory]
    [InlineData("""{"event":"something_else"}""")]
    [InlineData("""{"signal_ids":["reply.minimal"]}""")]
    [InlineData("""{"event":"move_picked","state_id":"following"}""")]
    [InlineData("""{"event":"learner_turn","signal_ids":[null]}""")]
    [InlineData("""{"event":"move_picked","state_id":null,"move_id":"keep_going"}""")]
    [InlineData("not json")]
    [InlineData("null")]
    public void Read_NotAnEvent_GivesUnreadableEvent(string line)
    {
        var read = HistoryEventJson.Read(line);

        Assert.True(read.IsT1);
        Assert.Equal(line, read.AsT1.Line);
    }

    [Fact]
    public void Context_UsedByAHost_WritesAndReadsTheSameJsonAsTheEngine()
    {
        HistoryEvent historyEvent = MovePicked.Create("stuck", "worked_example");

        var json = JsonSerializer.Serialize(historyEvent, HistoryJsonContext.Default.HistoryEvent);
        var read = JsonSerializer.Deserialize(json, HistoryJsonContext.Default.HistoryEvent);

        Assert.Equal(HistoryEventJson.Write(historyEvent), json);
        var picked = Assert.IsType<MovePicked>(read);
        Assert.Equal("worked_example", picked.MoveId);
    }

    [Fact]
    public void Context_OptionsGivenToAHost_WriteAndReadEveryEventAsTheEngineDoes()
    {
        // A host that takes options rather than a type info, such as a JSON column mapping, is given the context's own
        // options. Options of the host's own with the context in their resolver chain would drop the snake case names.
        var options = HistoryJsonContext.Default.Options;
        HistoryEvent[] events =
        [
            SessionStarted.Create("0123456789abcdef", 0.15, 0.15),
            LearnerTurn.Create(["reply.minimal"]),
            MovePicked.Create("stuck", "worked_example"),
            MoveJudged.Create("stuck", "worked_example", true),
        ];

        foreach (var historyEvent in events)
        {
            var json = JsonSerializer.Serialize(historyEvent, options);
            var read = JsonSerializer.Deserialize<HistoryEvent>(json, options);

            Assert.Equal(HistoryEventJson.Write(historyEvent), json);
            Assert.NotNull(read);
            Assert.Equal(json, HistoryEventJson.Write(read));
        }
    }
}
