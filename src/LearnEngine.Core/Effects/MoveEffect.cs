using System.Collections.Immutable;
using LearnEngine.Core.Odds;

namespace LearnEngine.Core.Effects;

/// <summary>
/// Where a move made in one state leads by the next learner turn, shared by every learner: the chance of each state
/// after it, such as worked_example from stuck leading to following half of the time. The weight is how many turns of
/// evidence these chances count as before a learner's own counts move them.
/// </summary>
public sealed class MoveEffect
{
    /// <summary>
    /// The id of the state the move is made in, such as "stuck".
    /// </summary>
    public string FromStateId { get; }

    /// <summary>
    /// The id of the move, such as "worked_example", or null for no move, where the odds fade back to the priors.
    /// </summary>
    public string? MoveId { get; }

    /// <summary>
    /// How many turns of evidence the chances count as. Above 0.
    /// </summary>
    public double Weight { get; }

    /// <summary>
    /// The chance of every state after the move, one per registered state, in the order the app registered the states.
    /// They add up to 1.
    /// </summary>
    public ImmutableArray<StateOdds> Chances { get; }

    private MoveEffect(string fromStateId, string? moveId, double weight, ImmutableArray<StateOdds> chances)
    {
        FromStateId = fromStateId;
        MoveId = moveId;
        Weight = weight;
        Chances = chances;
    }

    // Internal so an effect only comes from registration or the effect catalog, which have checked the states, the
    // weight and that the chances add up to 1.
    internal static MoveEffect Create(string fromStateId, string? moveId, double weight, ImmutableArray<StateOdds> chances) =>
        new(fromStateId, moveId, weight, chances);

    /// <summary>
    /// The chance that the move leads to the given state. A state that is not registered has a chance of 0.
    /// </summary>
    public double ChanceOf(string toStateId) =>
        Chances.FirstOrDefault(chance => chance.StateId == toStateId)?.Odds ?? 0;
}
