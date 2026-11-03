using LearnEngine.Core.Bandit;
using LearnEngine.Core.Judging;
using LearnEngine.Core.Odds;
using OneOf;

namespace LearnEngine.Core.Sessions;

/// <summary>
/// What the engine says after a learner turn: what it believes about the learner now, the move to make next or why
/// there is none, and what became of the last picked move.
/// </summary>
public sealed class TurnResult
{
    /// <summary>
    /// The odds of every state after the turn, with the leader, the runner up, the margin and whether the leader is clear.
    /// </summary>
    public SessionBelief Belief { get; }

    /// <summary>
    /// The move picked for the clear state, or why none was picked: the leader is not clear, the last picked move is
    /// still being judged, or the app allowed no moves in the clear state. With no pick the tutor keeps going and still
    /// answers anything the learner asks for outright.
    /// </summary>
    public OneOf<PickedMove, NoClearLeader, MoveBeingJudged, NoMovesInState> Pick { get; }

    /// <summary>
    /// The judgment of the last picked move when this turn judged it, the move still waiting for a result, or nothing
    /// to judge: no move was picked, its state changed first or its turns gave no result.
    /// </summary>
    public OneOf<MoveJudgment, MoveBeingJudged, NothingToJudge> Judgment { get; }

    private TurnResult(
        SessionBelief belief,
        OneOf<PickedMove, NoClearLeader, MoveBeingJudged, NoMovesInState> pick,
        OneOf<MoveJudgment, MoveBeingJudged, NothingToJudge> judgment)
    {
        Belief = belief;
        Pick = pick;
        Judgment = judgment;
    }

    // Internal so a turn result only comes from the engine, which has kept the turn, the judgment and the pick in the history.
    internal static TurnResult Create(
        SessionBelief belief,
        OneOf<PickedMove, NoClearLeader, MoveBeingJudged, NoMovesInState> pick,
        OneOf<MoveJudgment, MoveBeingJudged, NothingToJudge> judgment) =>
        new(belief, pick, judgment);
}
