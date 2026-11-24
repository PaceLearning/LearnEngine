using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace LearnEngine.Core.Learning;

/// <summary>
/// What one learner turn adds to the learner's model: the soft counts added to the rate of every signal in every state,
/// and to the effect of the move made before the turn, from every state to every state.
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

    // Internal, not private, so the source generated JSON reader of the history can call it. The effects may be
    // missing, as in a change kept before effects were learned, and read as none.
    [JsonConstructor]
    internal LearnerModelChange(ImmutableArray<RateCount> rates, ImmutableArray<EffectCount> effects = default)
    {
        Rates = rates;
        Effects = effects.IsDefault ? [] : effects;
    }

    /// <summary>
    /// Creates the change with the given counts added to the rates and to the move effects. Counts of the same signal
    /// and state, or of the same step, are summed.
    /// </summary>
    public static LearnerModelChange Create(IEnumerable<RateCount> rates, IEnumerable<EffectCount> effects)
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
        return new(summedRates, summedEffects);
    }
}
