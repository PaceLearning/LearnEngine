namespace LearnEngine.Core.Choosing;

/// <summary>
/// One move allowed in a state, weighed for a learner as <see cref="MoveChooser"/> weighs it: the chance it brings them
/// to a goal state, how much evidence there is for it and the bonus that evidence gives it.
/// </summary>
public sealed record WeighedMove
{
    /// <summary>
    /// The id of the move, such as "worked_example".
    /// </summary>
    public string MoveId { get; }

    /// <summary>
    /// The chance, from 0 to 1, that the move brings this learner to a goal state by their next turn, under their own
    /// move effects and the odds now.
    /// </summary>
    public double GoalChance { get; }

    /// <summary>
    /// The learner's own soft count of turns after the move in the state.
    /// </summary>
    public double OwnSteps { get; }

    /// <summary>
    /// The evidence for the move: <see cref="OwnSteps"/> plus the weight of its registered effect. Above 0.
    /// </summary>
    public double Evidence { get; }

    /// <summary>
    /// The bonus for how little evidence there is: the square root of 2 times the natural log of the evidence of every
    /// move allowed in the state, divided by <see cref="Evidence"/>.
    /// </summary>
    public double Bonus { get; }

    /// <summary>
    /// The goal chance plus the bonus. The move with the highest is chosen.
    /// </summary>
    public double Bound => GoalChance + Bonus;

    private WeighedMove(string moveId, double goalChance, double ownSteps, double evidence, double bonus)
    {
        MoveId = moveId;
        GoalChance = goalChance;
        OwnSteps = ownSteps;
        Evidence = evidence;
        Bonus = bonus;
    }

    // Internal so a weighed move only comes from the chooser.
    internal static WeighedMove Create(string moveId, double goalChance, double ownSteps, double evidence, double bonus) =>
        new(moveId, goalChance, ownSteps, evidence, bonus);
}
