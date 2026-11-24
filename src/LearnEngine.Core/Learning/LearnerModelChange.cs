using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace LearnEngine.Core.Learning;

/// <summary>
/// What one learner turn adds to the learner's model: the soft counts added to the rate of every signal in every state.
/// A change is only ever added, so two sessions of the same learner can add theirs at once.
/// </summary>
public sealed class LearnerModelChange
{
    /// <summary>
    /// The counts added to the rates, at most one per signal and state.
    /// </summary>
    public ImmutableArray<RateCount> Rates { get; }

    // Internal, not private, so the source generated JSON reader of the history can call it.
    [JsonConstructor]
    internal LearnerModelChange(ImmutableArray<RateCount> rates) => Rates = rates;

    /// <summary>
    /// Creates the change with the given counts added to the rates. Counts of the same signal and state are summed.
    /// </summary>
    public static LearnerModelChange Create(IEnumerable<RateCount> rates)
    {
        ArgumentNullException.ThrowIfNull(rates);

        ImmutableArray<RateCount> summed = [.. rates
            .GroupBy(count => (count.SignalId, count.StateId))
            .Select(group => RateCount.Create(
                group.Key.SignalId,
                group.Key.StateId,
                group.Sum(count => count.Shown),
                group.Sum(count => count.NotShown)))];
        return new(summed);
    }
}
