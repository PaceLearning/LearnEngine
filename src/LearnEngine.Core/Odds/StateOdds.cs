using System.Text.Json.Serialization;

namespace LearnEngine.Core.Odds;

/// <summary>
/// The odds that a learner is in one state, such as a 54% chance that they are following.
/// </summary>
public sealed record StateOdds
{
    /// <summary>
    /// The id of the state, such as "following".
    /// </summary>
    public string StateId { get; }

    /// <summary>
    /// The odds that the learner is in the state, from 0 to 1.
    /// </summary>
    public double Odds { get; }

    // Internal, not private, so the source generated JSON reader of the history can call it.
    [JsonConstructor]
    internal StateOdds(string stateId, double odds)
    {
        StateId = stateId;
        Odds = odds;
    }

    /// <summary>
    /// Creates the odds of the given state.
    /// </summary>
    public static StateOdds Create(string stateId, double odds) => new(stateId, odds);
}
