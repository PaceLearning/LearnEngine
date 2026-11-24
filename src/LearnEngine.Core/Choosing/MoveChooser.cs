using LearnEngine.Core.Effects;
using LearnEngine.Core.Learning;
using LearnEngine.Core.Moves;
using LearnEngine.Core.Odds;
using LearnEngine.Core.States;
using OneOf;

namespace LearnEngine.Core.Choosing;

/// <summary>
/// Chooses the move most likely to bring a learner to a goal state, from the moves the app allowed in their clear state,
/// by where each move leads for this learner.
/// </summary>
public sealed class MoveChooser
{
    private readonly AllowedMoveCatalog allowedMoves;
    private readonly MoveCatalog moves;
    private readonly MoveEffectCatalog effects;
    private readonly GoalStates goals;

    private MoveChooser(AllowedMoveCatalog allowedMoves, MoveCatalog moves, MoveEffectCatalog effects, GoalStates goals)
    {
        this.allowedMoves = allowedMoves;
        this.moves = moves;
        this.effects = effects;
        this.goals = goals;
    }

    // Internal so a chooser only comes from registration, which has already checked every allowed move is a registered
    // move and every goal state is a registered state.
    internal static MoveChooser Create(AllowedMoveCatalog allowedMoves, MoveCatalog moves, MoveEffectCatalog effects, GoalStates goals) =>
        new(allowedMoves, moves, effects, goals);

    /// <summary>
    /// Chooses among the moves allowed in the given state, which should be the clear leader of the belief.
    /// The goal chance of a move is the sum over every state of its odds times the learner's chance that the move,
    /// made there, leads into any goal state, as <see cref="LearnerModel.Effect"/> gives it. To it is added a bonus that
    /// is larger the less this learner has seen the move: the square root of 2 times the natural log of N, divided by
    /// n, where n is the learner's own count of turns after the move in the state plus the weight of its registered
    /// effect, and N is the sum of n over every move allowed in the state. So the move most likely to reach a goal is
    /// chosen, but a move seen less still gets a chance to show it works better for this learner.
    /// The highest wins, and when two are the same the one allowed first wins, so the same belief and model always give
    /// the same move. The answer says why, from the learner's counts.
    /// A state with no moves allowed gives <see cref="NoMovesInState"/>, and an id that is not a state gives <see cref="UnknownState"/>.
    /// </summary>
    public OneOf<PickedMove, NoMovesInState, UnknownState> Choose(Belief belief, LearnerModel model, string stateId)
    {
        ArgumentNullException.ThrowIfNull(belief);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateId);

        var found = allowedMoves.Find(stateId);
        if (found.IsT1)
            return found.AsT1;

        if (found.IsT2)
            return found.AsT2;

        var weighed = found.AsT0
            .Select(allowed => (
                allowed.MoveId,
                GoalChance: belief.All.Sum(from => from.Odds * goals.StateIds.Sum(goal => model.Effect(effects.For(from.StateId, allowed.MoveId), goal))),
                Seen: model.Steps(stateId, allowed.MoveId) + effects.For(stateId, allowed.MoveId).Weight))
            .ToList();
        var totalSeen = weighed.Sum(move => move.Seen);

        var best = weighed[0];
        var bestBound = double.NegativeInfinity;
        foreach (var move in weighed)
        {
            // Every weight is above 0, so Seen is too. A total below 1 has a negative log, and gets no bonus.
            var bound = move.GoalChance + Math.Sqrt(2 * Math.Max(0, Math.Log(totalSeen)) / move.Seen);

            // Only a strictly higher bound replaces the best, so a tie goes to the move allowed first.
            if (bound > bestBound)
            {
                best = move;
                bestBound = bound;
            }
        }

        var goalNames = string.Join(" or ", goals.StateIds);
        var own = Math.Round(model.Steps(stateId, best.MoveId));
        var intoGoal = Math.Round(goals.StateIds.Sum(goal => model.Steps(stateId, best.MoveId, goal)));
        var shared = goals.StateIds.Sum(goal => effects.For(stateId, best.MoveId).ChanceOf(goal));
        var why = $"This learner has not had {best.MoveId} in {stateId} yet, so the shared guess is that it leads to {goalNames} {Math.Round(shared * 100)}% of the time.";
        if (own >= 1)
            why = $"For this learner, {best.MoveId} led from {stateId} to {goalNames} {intoGoal} of {own} times.";

        // Every allowed move is found, because registration checked it was added with AddMove first.
        return PickedMove.Create(moves.Find(best.MoveId).AsT0, best.GoalChance, bestBound, why);
    }
}
