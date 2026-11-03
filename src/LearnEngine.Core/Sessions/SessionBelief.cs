using System.Collections.Immutable;
using LearnEngine.Core.Odds;

namespace LearnEngine.Core.Sessions;

/// <summary>
/// What the engine believes about a learner in a session: the odds of every state, the leader, the runner up and the
/// margin between them, and whether the leader is clear under the clear margin the session was started with.
/// </summary>
public sealed class SessionBelief
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

    /// <summary>
    /// How far the leader must be ahead of the runner up to be clear, as stamped on the session when it started.
    /// </summary>
    public double ClearMargin { get; }

    /// <summary>
    /// True when the leader is ahead of the runner up by at least <see cref="ClearMargin"/>. Only a clear leader lets a move be picked.
    /// </summary>
    public bool IsClear { get; }

    private SessionBelief(ImmutableArray<StateOdds> all, StateOdds leader, StateOdds? runnerUp, double margin, double clearMargin, bool isClear)
    {
        All = all;
        Leader = leader;
        RunnerUp = runnerUp;
        Margin = margin;
        ClearMargin = clearMargin;
        IsClear = isClear;
    }

    // Internal so a session belief only comes from the engine, with the clear margin stamped on the session.
    internal static SessionBelief Create(Belief belief, double clearMargin) =>
        new(belief.All, belief.Leader, belief.RunnerUp, belief.Margin, clearMargin, belief.ClearLeader(clearMargin).IsT0);
}
