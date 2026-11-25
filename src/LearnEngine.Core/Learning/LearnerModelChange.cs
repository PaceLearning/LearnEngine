using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace LearnEngine.Core.Learning;

/// <summary>
/// What one learner turn adds to the learner's model: the soft counts added to the rate of every signal in every state,
/// to the effect of the move made before the turn, from every state to every state, to the link from the topic before
/// to the topic of the turn, and to the states on the topic of the turn.
/// A change is only ever added, so two sessions of the same learner can add theirs at once.
/// </summary>
public sealed class LearnerModelChange
{
    /// <summary>
    /// The counts added to the rates, at most one per signal and state.
    /// </summary>
    public ImmutableArray<RateCount> Rates { get; }

    /// <summary>
    /// The counts added to the move effects, at most one per state before, move and state after.
    /// </summary>
    public ImmutableArray<EffectCount> Effects { get; }

    /// <summary>
    /// The counts added to the links from one topic to the next, at most one per pair of topics.
    /// </summary>
    public ImmutableArray<TopicLinkCount> TopicLinks { get; }

    /// <summary>
    /// The counts added to the states on each topic, at most one per topic and state.
    /// </summary>
    public ImmutableArray<TopicStateCount> TopicStates { get; }

    // Internal, not private, so the source generated JSON readers of the history and of the learner model can call it.
    // The effects and the topic counts may be missing, as in a change or model kept before they were learned, and read
    // as none.
    [JsonConstructor]
    internal LearnerModelChange(
        ImmutableArray<RateCount> rates,
        ImmutableArray<EffectCount> effects = default,
        ImmutableArray<TopicLinkCount> topicLinks = default,
        ImmutableArray<TopicStateCount> topicStates = default)
    {
        Rates = rates;
        Effects = effects.IsDefault ? [] : effects;
        TopicLinks = topicLinks.IsDefault ? [] : topicLinks;
        TopicStates = topicStates.IsDefault ? [] : topicStates;
    }

    /// <summary>
    /// Creates the change with the given counts added to the rates, the move effects and, when given, the topic links
    /// and the states on each topic. Counts of the same signal and state, of the same step, of the same pair of topics
    /// or of the same topic and state are summed.
    /// </summary>
    public static LearnerModelChange Create(
        IEnumerable<RateCount> rates,
        IEnumerable<EffectCount> effects,
        IEnumerable<TopicLinkCount>? topicLinks = null,
        IEnumerable<TopicStateCount>? topicStates = null)
    {
        ArgumentNullException.ThrowIfNull(rates);
        ArgumentNullException.ThrowIfNull(effects);

        ImmutableArray<RateCount> summedRates = [.. rates
            .GroupBy(count => (count.SignalId, count.StateId))
            .Select(group => RateCount.Create(
                group.Key.SignalId,
                group.Key.StateId,
                group.Sum(count => count.Shown),
                group.Sum(count => count.NotShown)))];
        ImmutableArray<EffectCount> summedEffects = [.. effects
            .GroupBy(count => (count.FromStateId, count.MoveId, count.ToStateId))
            .Select(group => EffectCount.Create(
                group.Key.FromStateId,
                group.Key.MoveId,
                group.Key.ToStateId,
                group.Sum(count => count.Count)))];
        ImmutableArray<TopicLinkCount> summedLinks = [.. (topicLinks ?? [])
            .GroupBy(count => (count.FromTopic, count.ToTopic))
            .Select(group => TopicLinkCount.Create(group.Key.FromTopic, group.Key.ToTopic, group.Sum(count => count.Count)))];
        ImmutableArray<TopicStateCount> summedTopicStates = [.. (topicStates ?? [])
            .GroupBy(count => (count.Topic, count.StateId))
            .Select(group => TopicStateCount.Create(group.Key.Topic, group.Key.StateId, group.Sum(count => count.Count)))];
        return new(summedRates, summedEffects, summedLinks, summedTopicStates);
    }
}
