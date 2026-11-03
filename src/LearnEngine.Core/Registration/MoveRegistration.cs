using LearnEngine.Core.Moves;

namespace LearnEngine.Core.Registration;

// A move kept in the service collection until the catalog is built from all of them.
internal sealed class MoveRegistration
{
    public Move Move { get; }

    private MoveRegistration(Move move) => Move = move;

    public static MoveRegistration Create(Move move) => new(move);
}
