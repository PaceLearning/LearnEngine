using System.Text.Json.Serialization;

namespace LearnEngine.Core.Learning;

/// <summary>
/// How many of one learner's turns went from one state to another after a move, such as from stuck to following after
/// worked_example on 6.2 turns. The count is soft: a turn adds the chance the learner made that step, not a whole turn.
/// </summary>
public sealed record EffectCount
{
    /// <summary>
    /// The id of the state before the turn, such as "stuck".
    /// </summary>
    public string FromStateId { get; }

    /// <summary>
    /// The id of the move made before the turn, such as "worked_example", or null when no move was made.
    /// </summary>
    public string? MoveId { get; }

    /// <summary>
    /// The id of the state after the turn, such as "following".
    /// </summary>
    public string ToStateId { get; }

    /// <summary>
    /// The soft count of turns that made this step. It is 0 or more.
    /// </summary>
    public double Count { get; }

    // Internal, not private, so the source generated JSON readers of the history and of the learner model can call it.
    [JsonConstructor]
    internal EffectCount(string fromStateId, string? moveId, string toStateId, double count)
    {
        FromStateId = fromStateId;
        MoveId = moveId;
        ToStateId = toStateId;
        Count = count;
    }

    /// <summary>
    /// Creates the count of the given step. An empty state id, an empty move id that is not null, or a count below 0,
    /// infinite or not a number, throws.
    /// </summary>
    public static EffectCount Create(string fromStateId, string? moveId, string toStateId, double count)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fromStateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toStateId);
        if (moveId is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(moveId);

        // Written so a NaN fails too, which a plain bound check would let through.
        var inRange = count >= 0 && double.IsFinite(count);
        if (!inRange)
            throw new ArgumentOutOfRangeException(nameof(count), count, "A count must be 0 or more and finite.");

        return new(fromStateId, moveId, toStateId, count);
    }
}
