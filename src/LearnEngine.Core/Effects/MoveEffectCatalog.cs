using System.Collections.Frozen;
using System.Collections.Immutable;
using LearnEngine.Core.Odds;
using LearnEngine.Core.States;

namespace LearnEngine.Core.Effects;

/// <summary>
/// Every move effect the app registered, and the effect of no move, built once and looked up by state and move.
/// No move is the fade of today: the learner stays in their state with 1 minus the fade, and with the fade they are in
/// each state as often as its prior says. A move with no effect registered in a state is read as no move.
/// </summary>
public sealed class MoveEffectCatalog
{
    /// <summary>
    /// How many turns of evidence the effect of no move counts as, and the effect of a move with none registered.
    /// </summary>
    public const double NoMoveWeight = 10;

    private readonly FrozenDictionary<(string FromStateId, string MoveId), MoveEffect> registered;
    private readonly FrozenDictionary<string, MoveEffect> noMove;

    /// <summary>
    /// Every registered effect, in the order the app registered them.
    /// </summary>
    public ImmutableArray<MoveEffect> All { get; }

    /// <summary>
    /// How far the belief moves back to the priors when no move is made, from 0 to 1.
    /// </summary>
    public double Fade { get; }

    private MoveEffectCatalog(
        FrozenDictionary<(string FromStateId, string MoveId), MoveEffect> registered,
        FrozenDictionary<string, MoveEffect> noMove,
        ImmutableArray<MoveEffect> all,
        double fade)
    {
        this.registered = registered;
        this.noMove = noMove;
        All = all;
        Fade = fade;
    }

    // Internal so a catalog only comes from registration, which has already checked every effect and the fade.
    internal static MoveEffectCatalog Create(StateCatalog states, double fade, IReadOnlyList<MoveEffect> effects)
    {
        var noMove = states.All.ToFrozenDictionary(
            from => from.Id,
            from => MoveEffect.Create(from.Id, null, NoMoveWeight, [.. states.All.Select(to => StateOdds.Create(
                to.Id,
                (to.Id == from.Id ? 1 - fade : 0) + fade * to.Prior))]),
            StringComparer.Ordinal);
        var registered = effects.ToFrozenDictionary(effect => (effect.FromStateId, effect.MoveId ?? ""));
        return new(registered, noMove, [.. effects], fade);
    }

    /// <summary>
    /// The shared effect of the given move made in the given state. A null move gives the effect of no move. A move
    /// with no effect registered in the state gives the chances of no move, under its own id, with <see cref="NoMoveWeight"/>.
    /// A state that is not registered throws, because every state in a belief is registered.
    /// </summary>
    public MoveEffect For(string fromStateId, string? moveId)
    {
        ArgumentNullException.ThrowIfNull(fromStateId);

        var stay = noMove.GetValueOrDefault(fromStateId)
            ?? throw new ArgumentException($"The state \"{fromStateId}\" is not registered.", nameof(fromStateId));
        if (moveId is null)
            return stay;

        return registered.GetValueOrDefault((fromStateId, moveId))
            ?? MoveEffect.Create(fromStateId, moveId, NoMoveWeight, stay.Chances);
    }
}
