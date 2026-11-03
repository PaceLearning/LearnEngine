using LearnEngine.Core.Bandit;

namespace LearnEngine.Core.Registration;

// A move allowed in a state, kept in the service collection with its starting score until the catalog is built from all of them.
internal sealed class AllowedMoveRegistration
{
    public MoveScore StartingScore { get; }

    private AllowedMoveRegistration(MoveScore startingScore) => StartingScore = startingScore;

    public static AllowedMoveRegistration Create(MoveScore startingScore) => new(startingScore);
}
