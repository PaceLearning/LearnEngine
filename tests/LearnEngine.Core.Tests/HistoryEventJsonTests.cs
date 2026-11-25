using System.Text.Json;
using LearnEngine.Core.History;
using LearnEngine.Core.Learning;
using LearnEngine.Core.Odds;
using LearnEngine.Core.Prediction;
using LearnEngine.Core.Topics;
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
    public void Write_LearnerTurnWithTopicAndMoveMade_ReadsBackTheSame()
    {
        var line = HistoryEventJson.Write(LearnerTurn.Create(["asks.for_help"], "recursion.base_case", "worked_example"));

        var read = HistoryEventJson.Read(line);

        Assert.Equal("""{"event":"learner_turn","signal_ids":["asks.for_help"],"topic":"recursion.base_case","move_made":"worked_example"}""", line);
        var turn = Assert.IsType<LearnerTurn>(read.AsT0);
        Assert.Equal(["asks.for_help"], turn.SignalIds.ToArray());
        Assert.Equal("recursion.base_case", turn.Topic);
        Assert.Equal("worked_example", turn.MoveMade);
    }

    [Fact]
    public void Read_TurnWithoutTopicOrMoveMade_GivesNullForBoth()
    {
        var read = HistoryEventJson.Read("""{"event":"learner_turn","signal_ids":[]}""");

        var turn = Assert.IsType<LearnerTurn>(read.AsT0);
        Assert.Null(turn.Topic);
        Assert.Null(turn.MoveMade);
    }

    [Theory]
    [InlineData("recursion.base_case", true)]
    [InlineData("a", true)]
    [InlineData("Loops-2_while", true)]
    [InlineData("", false)]
    [InlineData("has space", false)]
    [InlineData("a/b", false)]
    [InlineData("tópico", false)]
    public void TopicIds_IsValid_TakesLettersDigitsDotsUnderscoresAndDashes(string topic, bool valid)
    {
        Assert.Equal(valid, TopicIds.IsValid(topic));
    }

    [Fact]
    public void TopicIds_IsValid_TakesAtMost64Characters()
    {
        Assert.True(TopicIds.IsValid(new string('a', 64)));
        Assert.False(TopicIds.IsValid(new string('a', 65)));
    }

    [Fact]
    public void Create_InvalidTopic_Throws()
    {
        Assert.Throws<ArgumentException>(() => LearnerTurn.Create([], "not valid"));
    }

    [Fact]
    public void Write_TurnLearned_ReadsBackTheSame()
    {
        var learned = TurnLearned.Create(
            [StateOdds.Create("stuck", 0.75), StateOdds.Create("following", 0.25)],
            LearnerModelChange.Create(
                [RateCount.Create("asks.for_help", "stuck", 0.75, 0)],
                [EffectCount.Create("stuck", "worked_example", "following", 0.5), EffectCount.Create("stuck", null, "stuck", 0.25)]));

        var line = HistoryEventJson.Write(learned);
        var read = HistoryEventJson.Read(line);

        Assert.Equal("""{"event":"turn_learned","odds":[{"state_id":"stuck","odds":0.75},{"state_id":"following","odds":0.25}],"added":{"rates":[{"signal_id":"asks.for_help","state_id":"stuck","shown":0.75,"not_shown":0}],"effects":[{"from_state_id":"stuck","move_id":"worked_example","to_state_id":"following","count":0.5},{"from_state_id":"stuck","move_id":null,"to_state_id":"stuck","count":0.25}],"topic_links":[],"topic_states":[]}}""", line);
        var readBack = Assert.IsType<TurnLearned>(read.AsT0);
        Assert.Equal(["stuck", "following"], readBack.Odds.Select(stateOdds => stateOdds.StateId));
        Assert.Equal(0.75, readBack.Odds[0].Odds);
        Assert.Equal(RateCount.Create("asks.for_help", "stuck", 0.75, 0), Assert.Single(readBack.Added.Rates));
        Assert.Equal(
            [EffectCount.Create("stuck", "worked_example", "following", 0.5), EffectCount.Create("stuck", null, "stuck", 0.25)],
            readBack.Added.Effects.AsEnumerable());
    }

    [Fact]
    public void Read_TurnLearnedWithoutEffects_GivesNone()
    {
        var read = HistoryEventJson.Read("""{"event":"turn_learned","odds":[{"state_id":"stuck","odds":1}],"added":{"rates":[]}}""");

        var learned = Assert.IsType<TurnLearned>(read.AsT0);
        Assert.Empty(learned.Added.Effects);
        Assert.Empty(learned.Added.TopicLinks);
        Assert.Empty(learned.Added.TopicStates);
    }

    [Fact]
    public void Write_TurnLearnedWithTopics_ReadsBackTheSame()
    {
        var learned = TurnLearned.Create(
            [StateOdds.Create("stuck", 1)],
            LearnerModelChange.Create(
                [],
                [],
                [TopicLinkCount.Create("recursion.base_case", "recursion.call_stack", 1)],
                [TopicStateCount.Create("recursion.call_stack", "stuck", 1)]));

        var read = HistoryEventJson.Read(HistoryEventJson.Write(learned));

        var readBack = Assert.IsType<TurnLearned>(read.AsT0);
        Assert.Equal(TopicLinkCount.Create("recursion.base_case", "recursion.call_stack", 1), Assert.Single(readBack.Added.TopicLinks));
        Assert.Equal(TopicStateCount.Create("recursion.call_stack", "stuck", 1), Assert.Single(readBack.Added.TopicStates));
    }

    [Fact]
    public void Write_TurnExpected_ReadsBackTheSame()
    {
        var expected = TurnExpected.Create(Expectation.Create(
            "worked_example",
            [StateOdds.Create("stuck", 0.25), StateOdds.Create("following", 0.75)],
            [SignalChance.Create("asks.how_or_why", 0.31, 0.2)],
            [TopicChance.Create("recursion.base_case", 0.55), TopicChance.Create("recursion.call_stack", 0.45)]));

        var line = HistoryEventJson.Write(expected);
        var read = HistoryEventJson.Read(line);

        Assert.Equal("""{"event":"turn_expected","expected":{"move_id":"worked_example","odds":[{"state_id":"stuck","odds":0.25},{"state_id":"following","odds":0.75}],"signals":[{"signal_id":"asks.how_or_why","chance":0.31,"usual":0.2}],"topics":[{"topic":"recursion.base_case","chance":0.55},{"topic":"recursion.call_stack","chance":0.45}]}}""", line);
        var readBack = Assert.IsType<TurnExpected>(read.AsT0).Expected;
        Assert.Equal("worked_example", readBack.MoveId);
        Assert.Equal(expected.Expected.Odds.AsEnumerable(), readBack.Odds.AsEnumerable());
        Assert.Equal(expected.Expected.Signals.AsEnumerable(), readBack.Signals.AsEnumerable());
        Assert.Equal(expected.Expected.Topics.AsEnumerable(), readBack.Topics.AsEnumerable());
    }

    [Theory]
    [InlineData("""{"event":"turn_expected","expected":{"move_id":null,"odds":[],"signals":[],"topics":[{"topic":"not valid","chance":1}]}}""")]
    [InlineData("""{"event":"turn_expected","expected":{"move_id":null,"odds":[],"signals":[null],"topics":[]}}""")]
    [InlineData("""{"event":"turn_learned","odds":[],"added":{"rates":[],"topic_links":[{"from_topic":"a","to_topic":"not valid","count":1}]}}""")]
    public void Read_TopicNotValidOrNullChance_GivesUnreadableEvent(string line)
    {
        Assert.True(HistoryEventJson.Read(line).IsT1);
    }

    [Fact]
    public void Write_MovePicked_ReadsBackTheSame()
    {
        var line = HistoryEventJson.Write(MovePicked.Create("model_clash", "ask_their_picture", 0.75, "For this learner, ask_their_picture led from model_clash to following 3 of 4 times."));

        var read = HistoryEventJson.Read(line);

        Assert.Equal("""{"event":"move_picked","state_id":"model_clash","move_id":"ask_their_picture","goal_chance":0.75,"why":"For this learner, ask_their_picture led from model_clash to following 3 of 4 times."}""", line);
        var picked = Assert.IsType<MovePicked>(read.AsT0);
        Assert.Equal("model_clash", picked.StateId);
        Assert.Equal("ask_their_picture", picked.MoveId);
        Assert.Equal(0.75, picked.GoalChance);
        Assert.Equal("For this learner, ask_their_picture led from model_clash to following 3 of 4 times.", picked.Why);
    }

    [Fact]
    public void Read_MoveJudgedOrPickWithoutGoalChance_GivesUnreadableEvent()
    {
        Assert.True(HistoryEventJson.Read("""{"event":"move_judged","state_id":"following","move_id":"keep_going","worked":false}""").IsT1);
        Assert.True(HistoryEventJson.Read("""{"event":"move_picked","state_id":"following","move_id":"keep_going"}""").IsT1);
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
    [InlineData("""{"event":"learner_turn","signal_ids":[],"topic":"not valid"}""")]
    [InlineData("""{"event":"learner_turn","signal_ids":[],"move_made":""}""")]
    [InlineData("""{"event":"turn_learned","odds":[null],"added":{"rates":[]}}""")]
    [InlineData("""{"event":"turn_learned","odds":[],"added":{"rates":[null]}}""")]
    [InlineData("""{"event":"turn_learned","odds":[]}""")]
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
        HistoryEvent historyEvent = MovePicked.Create("stuck", "worked_example", 0.7, "Why worked_example.");

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
            MovePicked.Create("stuck", "worked_example", 0.7, "Why worked_example."),
            TurnExpected.Create(Expectation.Create(null, [StateOdds.Create("stuck", 1)], [SignalChance.Create("reply.minimal", 0.5, 0.25)], [])),
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
