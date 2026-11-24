using System.Collections.Immutable;
using LearnEngine.Core.Choosing;
using LearnEngine.Core.History;
using LearnEngine.Core.Odds;
using LearnEngine.Core.Prediction;

namespace LearnEngine.Web.Sessions;

/// <summary>
/// A session as the Session page shows it, read back from its history: the odds now, the move picked last and how
/// every allowed move weighs, what is expected of the next turn, and every turn.
/// </summary>
public sealed class SessionView
{
    /// <summary>
    /// The id of the session.
    /// </summary>
    public string SessionId { get; }

    /// <summary>
    /// How far the leader must be ahead of the runner up to be clear in this session.
    /// </summary>
    public double ClearMargin { get; }

    /// <summary>
    /// The belief after the last turn, or the priors before the first.
    /// </summary>
    public Belief Belief { get; }

    /// <summary>
    /// True when the leader of <see cref="Belief"/> is clear under <see cref="ClearMargin"/>.
    /// </summary>
    public bool IsClear { get; }

    /// <summary>
    /// The move picked after the last turn, or null when none was.
    /// </summary>
    public MovePicked? Pick { get; }

    /// <summary>
    /// Every move allowed in the clear leader, weighed with what the engine knows about the learner now, or empty when
    /// the leader is not clear or allows no moves.
    /// </summary>
    public ImmutableArray<WeighedMove> Weighed { get; }

    /// <summary>
    /// What is expected of the next turn, as the history kept it, or null when it kept none.
    /// </summary>
    public Expectation? ExpectedNext { get; }

    /// <summary>
    /// Every turn, oldest first.
    /// </summary>
    public ImmutableArray<TurnView> Turns { get; }

    private SessionView(
        string sessionId,
        double clearMargin,
        Belief belief,
        bool isClear,
        MovePicked? pick,
        ImmutableArray<WeighedMove> weighed,
        Expectation? expectedNext,
        ImmutableArray<TurnView> turns)
    {
        SessionId = sessionId;
        ClearMargin = clearMargin;
        Belief = belief;
        IsClear = isClear;
        Pick = pick;
        Weighed = weighed;
        ExpectedNext = expectedNext;
        Turns = turns;
    }

    /// <summary>
    /// Creates the view of the given session.
    /// </summary>
    public static SessionView Create(
        string sessionId,
        double clearMargin,
        Belief belief,
        MovePicked? pick,
        ImmutableArray<WeighedMove> weighed,
        Expectation? expectedNext,
        ImmutableArray<TurnView> turns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(belief);
        return new(sessionId, clearMargin, belief, belief.ClearLeader(clearMargin).IsT0, pick, weighed, expectedNext, turns);
    }
}
