using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace LearnEngine.Mcp.Storage;

// The whole of a learner model file: the student's counts of every signal in every state that has some, of every
// step from a state to a state after a move, of every topic followed by another and of every state on every topic.
internal sealed class SavedLearnerModel
{
    public ImmutableArray<SavedRateCount> Rates { get; }

    public ImmutableArray<SavedEffectCount> Effects { get; }

    public ImmutableArray<SavedTopicLinkCount> TopicLinks { get; }

    public ImmutableArray<SavedTopicStateCount> TopicStates { get; }

    // Internal, not private, so the source generated JSON reader can call it. A file written before effects or topics
    // were learned has none, and reads as none.
    [JsonConstructor]
    internal SavedLearnerModel(
        ImmutableArray<SavedRateCount> rates,
        ImmutableArray<SavedEffectCount> effects = default,
        ImmutableArray<SavedTopicLinkCount> topicLinks = default,
        ImmutableArray<SavedTopicStateCount> topicStates = default)
    {
        Rates = rates;
        Effects = effects.IsDefault ? [] : effects;
        TopicLinks = topicLinks.IsDefault ? [] : topicLinks;
        TopicStates = topicStates.IsDefault ? [] : topicStates;
    }

    public static SavedLearnerModel Create(
        IEnumerable<SavedRateCount> rates,
        IEnumerable<SavedEffectCount> effects,
        IEnumerable<SavedTopicLinkCount> topicLinks,
        IEnumerable<SavedTopicStateCount> topicStates) =>
        new([.. rates], [.. effects], [.. topicLinks], [.. topicStates]);
}
