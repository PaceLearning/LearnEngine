using System.Collections.Immutable;
using System.Text.Json.Serialization;
using LearnEngine.Core.Odds;

namespace LearnEngine.Core.Prediction;

/// <summary>
/// What the engine expects of the next learner turn, assuming the tutor makes the given move, or none: the odds of
/// every state at that turn, the chance of every signal and the chance of each topic it may be about.
/// </summary>
public sealed class Expectation
{
    /// <summary>
    /// The id of the move the expectation assumes the tutor makes before the next turn, such as "worked_example", or
    /// null when it assumes no move.
    /// </summary>
    public string? MoveId { get; }

    /// <summary>
    /// The predicted odds of every state at the next turn, before its signals, in the order the app registered the
    /// states. They add up to 1.
    /// </summary>
    public ImmutableArray<StateOdds> Odds { get; }

    /// <summary>
    /// The chance of every registered signal at the next turn, in the order the app registered the signals.
    /// </summary>
    public ImmutableArray<SignalChance> Signals { get; }

    /// <summary>
    /// The signals likelier than usual at the next turn, those with <see cref="SignalChance.VsUsual"/> above 1, highest
    /// first. Signals as far above usual keep the order the app registered them in.
    /// </summary>
    // Worked out on read, not in the constructor, so a kept line with a null signal reaches the reader's checks.
    // OrderByDescending keeps the registration order of equal values.
    [JsonIgnore]
    public ImmutableArray<SignalChance> AboveUsual => [.. Signals.Where(signal => signal.VsUsual > 1).OrderByDescending(signal => signal.VsUsual)];

    /// <summary>
    /// The chance of each topic the next turn may be about, most likely first. They add up to 1, or the list is empty
    /// when the engine knows no topic to expect.
    /// </summary>
    public ImmutableArray<TopicChance> Topics { get; }

    // Internal, not private, so the source generated JSON reader of the history can call it.
    [JsonConstructor]
    internal Expectation(string? moveId, ImmutableArray<StateOdds> odds, ImmutableArray<SignalChance> signals, ImmutableArray<TopicChance> topics)
    {
        MoveId = moveId;
        Odds = odds;
        Signals = signals;
        Topics = topics;
    }

    /// <summary>
    /// How sure the engine is of the state the learner is predicted to be in at the next turn, under the given clear
    /// margin: the rule of <see cref="Belief.Confidence"/> applied to <see cref="Odds"/>.
    /// </summary>
    public Confidence Confidence(double clearMargin) => Belief.Create(Odds).Confidence(clearMargin);

    /// <summary>
    /// Creates the expectation of the next turn after the given move, or none, with the predicted odds, the chance of
    /// every signal and the chance of each topic.
    /// </summary>
    public static Expectation Create(string? moveId, IEnumerable<StateOdds> odds, IEnumerable<SignalChance> signals, IEnumerable<TopicChance> topics)
    {
        ArgumentNullException.ThrowIfNull(odds);
        ArgumentNullException.ThrowIfNull(signals);
        ArgumentNullException.ThrowIfNull(topics);
        return new(moveId, [.. odds], [.. signals], [.. topics]);
    }
}
