using System.Collections.Immutable;
using System.Text.Json.Serialization;
using LearnEngine.Core.Learning;
using LearnEngine.Core.Odds;

namespace LearnEngine.Core.History;

/// <summary>
/// What the engine made of the learner turn just before it: the odds of every state after the turn, worked out with
/// the learner's model as it was then, and the counts the turn added to that model.
/// The learner's model keeps changing, so a session is read back from these odds instead of worked out again with
/// today's model.
/// </summary>
public sealed class TurnLearned : HistoryEvent
{
    /// <summary>
    /// The odds of every state after the turn, in the order the app registered the states. They add up to 1.
    /// </summary>
    public ImmutableArray<StateOdds> Odds { get; }

    /// <summary>
    /// The counts the turn added to the learner's model.
    /// </summary>
    public LearnerModelChange Added { get; }

    // Internal, not private, so the source generated JSON reader can call it.
    [JsonConstructor]
    internal TurnLearned(ImmutableArray<StateOdds> odds, LearnerModelChange added)
    {
        Odds = odds;
        Added = added;
    }

    /// <summary>
    /// Creates what was learned from a turn: the odds after it and the counts it added to the learner's model.
    /// </summary>
    public static TurnLearned Create(IEnumerable<StateOdds> odds, LearnerModelChange added)
    {
        ArgumentNullException.ThrowIfNull(odds);
        ArgumentNullException.ThrowIfNull(added);
        return new([.. odds], added);
    }
}
