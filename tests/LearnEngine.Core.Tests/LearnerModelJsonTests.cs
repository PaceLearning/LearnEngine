using LearnEngine.Core.Learning;
using Xunit;

namespace LearnEngine.Core.Tests;

/// <summary>Writing a learner model as JSON and reading it back.</summary>
public sealed class LearnerModelJsonTests
{
    [Fact]
    public void Write_ModelWithRatesEffectsAndTopics_ReadsBackEqual()
    {
        var model = LearnerModel.Create(
            [RateCount.Create("says.dont_get_it", "stuck", 1.2, 9.8), RateCount.Create("reply.minimal", "following", 0.5, 3)],
            [EffectCount.Create("stuck", "worked_example", "following", 6.2), EffectCount.Create("stuck", null, "stuck", 1.5)],
            [TopicLinkCount.Create("recursion.base_case", "recursion.call_stack", 3)],
            [TopicStateCount.Create("recursion.base_case", "stuck", 2.4)]);

        var json = LearnerModelJson.Write(model);
        var read = LearnerModelJson.Read(json);

        var readBack = Assert.IsType<LearnerModel>(read.Value);
        Assert.Equal(model.Rates.AsEnumerable(), readBack.Rates.AsEnumerable());
        Assert.Equal(model.Effects.AsEnumerable(), readBack.Effects.AsEnumerable());
        Assert.Equal(model.TopicLinks.AsEnumerable(), readBack.TopicLinks.AsEnumerable());
        Assert.Equal(model.TopicStates.AsEnumerable(), readBack.TopicStates.AsEnumerable());
        Assert.Equal(["recursion.base_case", "recursion.call_stack"], readBack.Topics.AsEnumerable());
        Assert.Equal(json, LearnerModelJson.Write(readBack));
    }

    [Fact]
    public void Write_EmptyModel_WritesEmptyLists()
    {
        var json = LearnerModelJson.Write(LearnerModel.Empty);

        Assert.Equal("""{"rates":[],"effects":[],"topic_links":[],"topic_states":[]}""", json);
        Assert.Empty(Assert.IsType<LearnerModel>(LearnerModelJson.Read(json).Value).Rates);
    }

    [Fact]
    public void Read_WrittenBeforeEffectsOrTopics_GivesNone()
    {
        var read = LearnerModelJson.Read("""{"rates":[{"signal_id":"says.dont_get_it","state_id":"stuck","shown":1,"not_shown":2}]}""");

        var model = Assert.IsType<LearnerModel>(read.Value);
        Assert.Equal([RateCount.Create("says.dont_get_it", "stuck", 1, 2)], model.Rates.AsEnumerable());
        Assert.Empty(model.Effects);
        Assert.Empty(model.TopicLinks);
        Assert.Empty(model.TopicStates);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("""{"rates":[null]}""")]
    [InlineData("""{"rates":[{"signal_id":null,"state_id":"stuck","shown":1,"not_shown":2}]}""")]
    [InlineData("""{"rates":[{"signal_id":"says.dont_get_it","state_id":"stuck","shown":-1,"not_shown":2}]}""")]
    [InlineData("""{"rates":[],"effects":[{"from_state_id":"stuck","move_id":"","to_state_id":"following","count":1}]}""")]
    [InlineData("""{"rates":[],"topic_links":[{"from_topic":"a","to_topic":"not valid","count":1}]}""")]
    [InlineData("""{"rates":[],"topic_states":[{"topic":"a","state_id":"","count":1}]}""")]
    public void Read_NotALearnerModel_GivesUnreadableLearnerModel(string json)
    {
        var read = LearnerModelJson.Read(json);

        var unreadable = Assert.IsType<UnreadableLearnerModel>(read.Value);
        Assert.Equal(json, unreadable.Json);
    }
}
