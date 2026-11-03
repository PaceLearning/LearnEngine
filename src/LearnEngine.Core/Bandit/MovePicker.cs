using System.Collections.Immutable;
using LearnEngine.Core.Moves;
using LearnEngine.Core.States;
using OneOf;

namespace LearnEngine.Core.Bandit;

/// <summary>
/// Picks the move that is most worth making for a learner in a clear state, from the moves the app allowed in it.
/// </summary>
public sealed class MovePicker
{
    private readonly AllowedMoveCatalog allowedMoves;
    private readonly MoveCatalog moves;
    private readonly IScoreStore scores;

    private MovePicker(AllowedMoveCatalog allowedMoves, MoveCatalog moves, IScoreStore scores)
    {
        this.allowedMoves = allowedMoves;
        this.moves = moves;
        this.scores = scores;
    }

    // Internal so a picker only comes from registration, which has already checked every allowed move is a registered move.
    internal static MovePicker Create(AllowedMoveCatalog allowedMoves, MoveCatalog moves, IScoreStore scores) =>
        new(allowedMoves, moves, scores);

    /// <summary>
    /// Picks the move with the highest upper confidence bound among the moves allowed in the given state, using the
    /// learner's own score of each move and the starting score for a move the learner has no score for yet.
    /// Only call it for a clear state, the leader given by <c>Belief.ClearLeader</c>.
    /// The upper confidence bound of a move is the share of its tries that worked, plus a bonus that is larger the less
    /// it was tried: worked divided by tried, plus the square root of 2 times the natural log of the tries of every move
    /// allowed in the state, divided by the tries of this move. So a move that works well is picked, but a move tried
    /// only a few times gets a chance to show it works better. A move never tried has the highest bound of all.
    /// The same scores always give the same pick: when two moves have the same bound, the one allowed first wins.
    /// A state with no moves allowed gives <see cref="NoMovesInState"/>, and an id that is not a state gives <see cref="UnknownState"/>.
    /// </summary>
    public async Task<OneOf<PickedMove, NoMovesInState, UnknownState>> Pick(string learnerId, string stateId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(learnerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateId);

        var found = allowedMoves.Find(stateId);
        if (found.IsT1)
            return found.AsT1;

        if (found.IsT2)
            return found.AsT2;

        var saved = await scores.Load(learnerId, stateId, cancellationToken);
        ImmutableArray<MoveScore> current = [.. found.AsT0.Select(starting =>
            saved.FirstOrDefault(score => score.MoveId == starting.MoveId) ?? starting)];
        var totalTried = current.Sum(score => score.Tried);

        var best = current[0];
        var bestBound = double.NegativeInfinity;
        foreach (var score in current)
        {
            var bound = score.Tried == 0
                ? double.PositiveInfinity
                : (double)score.Worked / score.Tried + Math.Sqrt(2 * Math.Log(totalTried) / score.Tried);

            // Only a strictly higher bound replaces the best, so a tie goes to the move allowed first.
            if (bound > bestBound)
            {
                best = score;
                bestBound = bound;
            }
        }

        // Every allowed move is found, because registration checked it was added with AddMove first.
        return PickedMove.Create(moves.Find(best.MoveId).AsT0, best, bestBound);
    }
}
