using System.Collections.Frozen;
using System.Collections.Immutable;
using LearnEngine.Core.Effects;
using LearnEngine.Core.Likelihoods;

namespace LearnEngine.Core.Learning;

/// <summary>
/// What the engine has learned about how one learner learns, kept as soft counts on top of the shared guesses the app
/// registered, with which topic they ask about after which and how they did on each topic. A learner with no counts is read exactly as the shared vocabulary says, and moves away from it as their
/// turns come. A model never changes. Adding a change to it gives a new one.
/// </summary>
public sealed class LearnerModel
{
    private readonly FrozenDictionary<(string SignalId, string StateId), RateCount> rates;
    private readonly FrozenDictionary<(string FromStateId, string? MoveId, string ToStateId), double> effects;
    private readonly FrozenDictionary<(string FromStateId, string? MoveId), double> effectTotals;

    /// <summary>
    /// The model of a learner the engine knows nothing about yet.
    /// </summary>
    public static LearnerModel Empty { get; } = new(
        FrozenDictionary<(string, string), RateCount>.Empty,
        FrozenDictionary<(string, string?, string), double>.Empty,
        FrozenDictionary<(string, string?), double>.Empty,
        [],
        [],
        [],
        []);

    /// <summary>
    /// The learner's counts of every signal in every state that has some, at most one per signal and state.
    /// </summary>
    public ImmutableArray<RateCount> Rates { get; }

    /// <summary>
    /// The learner's counts of every step from one state to another after a move, at most one per state before, move
    /// and state after.
    /// </summary>
    public ImmutableArray<EffectCount> Effects { get; }

    /// <summary>
    /// The learner's counts of every topic followed by another, at most one per pair of topics, in the order first seen.
    /// </summary>
    public ImmutableArray<TopicLinkCount> TopicLinks { get; }

    /// <summary>
    /// The learner's counts of every state on every topic, at most one per topic and state, in the order first seen.
    /// </summary>
    public ImmutableArray<TopicStateCount> TopicStates { get; }

    /// <summary>
    /// The id of every topic the learner has had a turn on, each once, in the order first seen.
    /// </summary>
    public ImmutableArray<string> Topics { get; }

    private LearnerModel(
        FrozenDictionary<(string SignalId, string StateId), RateCount> rates,
        FrozenDictionary<(string FromStateId, string? MoveId, string ToStateId), double> effects,
        FrozenDictionary<(string FromStateId, string? MoveId), double> effectTotals,
        ImmutableArray<RateCount> allRates,
        ImmutableArray<EffectCount> allEffects,
        ImmutableArray<TopicLinkCount> topicLinks,
        ImmutableArray<TopicStateCount> topicStates)
    {
        this.rates = rates;
        this.effects = effects;
        this.effectTotals = effectTotals;
        Rates = allRates;
        Effects = allEffects;
        TopicLinks = topicLinks;
        TopicStates = topicStates;
        Topics = [.. topicStates.Select(count => count.Topic)
            .Concat(topicLinks.SelectMany(count => new[] { count.FromTopic, count.ToTopic }))
            .Distinct(StringComparer.Ordinal)];
    }

    /// <summary>
    /// Creates the model with the given counts, such as a store read back from its file. Counts of the same signal and
    /// state, of the same step, of the same pair of topics or of the same topic and state are summed.
    /// </summary>
    public static LearnerModel Create(
        IEnumerable<RateCount> rates,
        IEnumerable<EffectCount> effects,
        IEnumerable<TopicLinkCount>? topicLinks = null,
        IEnumerable<TopicStateCount>? topicStates = null) =>
        Empty.Add(LearnerModelChange.Create(rates, effects, topicLinks, topicStates));

    /// <summary>
    /// Gives a new model with the counts of the change added to this one's.
    /// </summary>
    public LearnerModel Add(LearnerModelChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        var summed = LearnerModelChange.Create(
            Rates.Concat(change.Rates),
            Effects.Concat(change.Effects),
            TopicLinks.Concat(change.TopicLinks),
            TopicStates.Concat(change.TopicStates));
        return new(
            summed.Rates.ToFrozenDictionary(count => (count.SignalId, count.StateId)),
            summed.Effects.ToFrozenDictionary(count => (count.FromStateId, count.MoveId, count.ToStateId), count => count.Count),
            summed.Effects
                .GroupBy(count => (count.FromStateId, count.MoveId))
                .ToFrozenDictionary(group => group.Key, group => group.Sum(count => count.Count)),
            summed.Rates,
            summed.Effects,
            summed.TopicLinks,
            summed.TopicStates);
    }

    /// <summary>
    /// The learner's rate of the shared likelihood's signal in its state: the shared rate counts as
    /// <paramref name="rateWeight"/> turns, and the learner's own counts are added to it.
    /// Rate = (shared rate * weight + shown) / (weight + shown + not shown). With no counts it is the shared rate.
    /// For example a shared rate of 0.20 with a weight of 20, shown 1.2 and not shown 9.8 gives about 0.17.
    /// </summary>
    public double Rate(Likelihood shared, double rateWeight)
    {
        ArgumentNullException.ThrowIfNull(shared);

        var count = rates.GetValueOrDefault((shared.SignalId, shared.StateId));
        if (count is null)
            return shared.Rate;

        return (shared.Rate * rateWeight + count.Shown) / (rateWeight + count.Shown + count.NotShown);
    }

    /// <summary>
    /// The learner's chance that the shared effect's move, made in its state, leads to the given state: the shared
    /// chances count as the effect's weight in turns, and the learner's own counts of steps from that state after that
    /// move are added to them.
    /// Chance = (shared chance * weight + count to that state) / (weight + count to every state). With no counts it is
    /// the shared chance. For example a shared chance of 0.70 with a weight of 10, and 8 steps of which 6 went to that
    /// state, gives (7 + 6) / 18, about 0.72.
    /// </summary>
    public double Effect(MoveEffect shared, string toStateId)
    {
        ArgumentNullException.ThrowIfNull(shared);
        ArgumentNullException.ThrowIfNull(toStateId);

        var count = effects.GetValueOrDefault((shared.FromStateId, shared.MoveId, toStateId));
        var total = effectTotals.GetValueOrDefault((shared.FromStateId, shared.MoveId));
        return (shared.ChanceOf(toStateId) * shared.Weight + count) / (shared.Weight + total);
    }

    /// <summary>
    /// The learner's soft count of turns that went from the given state to the given state after the given move, or
    /// after no move when it is null. It is 0 when there are none.
    /// </summary>
    public double Steps(string fromStateId, string? moveId, string toStateId)
    {
        ArgumentNullException.ThrowIfNull(fromStateId);
        ArgumentNullException.ThrowIfNull(toStateId);

        return effects.GetValueOrDefault((fromStateId, moveId, toStateId));
    }

    /// <summary>
    /// The learner's soft count of turns that came after the given move made in the given state, or after no move when
    /// it is null, whatever state they went to: how much evidence of their own the learner has of that move there.
    /// It is 0 when there are none.
    /// </summary>
    public double Steps(string fromStateId, string? moveId)
    {
        ArgumentNullException.ThrowIfNull(fromStateId);

        return effectTotals.GetValueOrDefault((fromStateId, moveId));
    }
}
