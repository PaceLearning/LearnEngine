using System.Collections.Frozen;
using System.Collections.Immutable;
using OneOf;

namespace LearnEngine.Core.Moves;

/// <summary>
/// Every move the app registered, built once and looked up by id.
/// </summary>
public sealed class MoveCatalog
{
    private readonly FrozenDictionary<string, Move> byId;

    /// <summary>
    /// Every move in the order the app registered them.
    /// </summary>
    public ImmutableArray<Move> All { get; }

    private MoveCatalog(FrozenDictionary<string, Move> byId, ImmutableArray<Move> all)
    {
        this.byId = byId;
        All = all;
    }

    // Internal so a catalog only comes from registration, which has already checked the ids are filled and unique.
    internal static MoveCatalog Create(IReadOnlyList<Move> moves)
    {
        var byId = moves.ToFrozenDictionary(move => move.Id, StringComparer.Ordinal);
        return new(byId, [.. moves]);
    }

    /// <summary>
    /// Finds the move with the given id, or says the id is unknown.
    /// </summary>
    public OneOf<Move, UnknownMove> Find(string id)
    {
        var move = byId.GetValueOrDefault(id);
        if (move is null)
            return UnknownMove.Create(id);

        return move;
    }
}
