using System.Collections.Immutable;

namespace LearnEngine.Core.Odds;

/// <summary>
/// What the odds engine believes about a learner after a number of turns: the odds of every state, adding up to 1.
/// A belief never changes. Fading it or applying a turn to it gives a new one.
/// </summary>
public sealed class Belief
{
    /// <summary>
    /// The odds of every state, in the order the app registered the states. They add up to 1.
    /// </summary>
    public ImmutableArray<StateOdds> All { get; }

    /// <summary>
    /// The state with the highest odds. When two states have the same odds, the one registered first leads.
    /// </summary>
    public StateOdds Leader { get; }

    /// <summary>
    /// The state with the second highest odds, or null when the app registered only one state.
    /// </summary>
    public StateOdds? RunnerUp { get; }

    /// <summary>
    /// How far the leader is ahead of the runner up. With only one state, it is the odds of the leader.
    /// </summary>
    public double Margin { get; }

    private Belief(ImmutableArray<StateOdds> all, StateOdds leader, StateOdds? runnerUp, double margin)
    {
        All = all;
        Leader = leader;
        RunnerUp = runnerUp;
        Margin = margin;
    }

    // Internal so a belief only comes from the odds engine, which keeps one odds per registered state, adding up to 1.
    internal static Belief Create(ImmutableArray<StateOdds> all)
    {
        // OrderByDescending keeps the registration order of equal odds, so a tie always gives the same leader.
        var ranked = all.OrderByDescending(stateOdds => stateOdds.Odds).ToList();
        var leader = ranked[0];
        var runnerUp = ranked.Count > 1 ? ranked[1] : null;
        var margin = leader.Odds - (runnerUp?.Odds ?? 0);
        return new(all, leader, runnerUp, margin);
    }
}
