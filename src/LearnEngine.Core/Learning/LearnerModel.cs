using System.Collections.Frozen;
using System.Collections.Immutable;
using LearnEngine.Core.Likelihoods;

namespace LearnEngine.Core.Learning;

/// <summary>
/// What the engine has learned about how one learner learns, kept as soft counts on top of the shared guesses the app
/// registered. A learner with no counts is read exactly as the shared vocabulary says, and moves away from it as their
/// turns come. A model never changes. Adding a change to it gives a new one.
/// </summary>
public sealed class LearnerModel
{
    private readonly FrozenDictionary<(string SignalId, string StateId), RateCount> rates;

    /// <summary>
    /// The model of a learner the engine knows nothing about yet.
    /// </summary>
    public static LearnerModel Empty { get; } = new(FrozenDictionary<(string, string), RateCount>.Empty, []);

    /// <summary>
    /// The learner's counts of every signal in every state that has some, at most one per signal and state.
    /// </summary>
    public ImmutableArray<RateCount> Rates { get; }

    private LearnerModel(FrozenDictionary<(string SignalId, string StateId), RateCount> rates, ImmutableArray<RateCount> all)
    {
        this.rates = rates;
        Rates = all;
    }

    /// <summary>
    /// Creates the model with the given counts, such as a store read back from its file. Counts of the same signal and state are summed.
    /// </summary>
    public static LearnerModel Create(IEnumerable<RateCount> rates) => Empty.Add(LearnerModelChange.Create(rates));

    /// <summary>
    /// Gives a new model with the counts of the change added to this one's.
    /// </summary>
    public LearnerModel Add(LearnerModelChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        ImmutableArray<RateCount> all = [.. Rates
            .Concat(change.Rates)
            .GroupBy(count => (count.SignalId, count.StateId))
            .Select(group => RateCount.Create(
                group.Key.SignalId,
                group.Key.StateId,
                group.Sum(count => count.Shown),
                group.Sum(count => count.NotShown)))];
        return new(all.ToFrozenDictionary(count => (count.SignalId, count.StateId)), all);
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
}
