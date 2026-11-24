using LearnEngine.Core.Moves;

namespace LearnEngine.Core.Choosing;

/// <summary>
/// The move chosen for a learner in a clear state, with the chance it brings them to a goal state and why it was chosen.
/// </summary>
public sealed record PickedMove
{
    /// <summary>
    /// The move the tutor makes next, with the description the AI reads to carry it out.
    /// </summary>
    public Move Move { get; }

    /// <summary>
    /// The chance, from 0 to 1, that the move brings this learner to a goal state by their next turn, under their own
    /// move effects and the odds now.
    /// </summary>
    public double GoalChance { get; }

    /// <summary>
    /// The goal chance plus the bonus for how little this learner has seen the move, the highest of every move allowed
    /// in the state.
    /// </summary>
    public double Bound { get; }

    /// <summary>
    /// One plain sentence that says why the move was chosen, from the learner's counts, such as
    /// "For this learner, worked_example led from stuck to following or eager 6 of 8 times."
    /// </summary>
    public string Why { get; }

    private PickedMove(Move move, double goalChance, double bound, string why)
    {
        Move = move;
        GoalChance = goalChance;
        Bound = bound;
        Why = why;
    }

    /// <summary>
    /// Creates the answer for the given move, chosen on the given goal chance and bound, with why.
    /// </summary>
    public static PickedMove Create(Move move, double goalChance, double bound, string why) => new(move, goalChance, bound, why);
}
