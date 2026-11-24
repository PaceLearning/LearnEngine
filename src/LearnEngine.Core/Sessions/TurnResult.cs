using LearnEngine.Core.Bandit;
using LearnEngine.Core.Judging;
using LearnEngine.Core.Odds;
using LearnEngine.Core.Prediction;
using OneOf;

namespace LearnEngine.Core.Sessions;

/// <summary>
/// What the engine says after a learner turn: what it believes about the learner now, the move to make next or why
/// there is none, what became of the last picked move, what it expects of the next turn and how unexpected this one was.
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

    /// <summary>
    /// What the engine expects of the next turn, assuming the picked move is made, or no move when none was picked:
    /// the predicted odds, the chance of every signal and the chance of each topic.
    /// </summary>
    public Expectation Expected { get; }

    /// <summary>
    /// How unexpected this turn's signals were under what was expected of it before it came, as
    /// <see cref="Predictor.Surprise"/> gives it. It is 0 for a turn that was certain and grows the more unexpected it was.
    /// </summary>
    public double Surprise { get; }

    private TurnResult(
        SessionBelief belief,
        OneOf<PickedMove, NoClearLeader, MoveBeingJudged, NoMovesInState> pick,
        OneOf<MoveJudgment, MoveBeingJudged, NothingToJudge> judgment,
        Expectation expected,
        double surprise)
    {
        Belief = belief;
        Pick = pick;
        Judgment = judgment;
        Expected = expected;
        Surprise = surprise;
    }

    // Internal so a turn result only comes from the engine, which has kept the turn, the judgment, the pick and what
    // it expects next in the history.
    internal static TurnResult Create(
        SessionBelief belief,
        OneOf<PickedMove, NoClearLeader, MoveBeingJudged, NoMovesInState> pick,
        OneOf<MoveJudgment, MoveBeingJudged, NothingToJudge> judgment,
        Expectation expected,
        double surprise) =>
        new(belief, pick, judgment, expected, surprise);
}
