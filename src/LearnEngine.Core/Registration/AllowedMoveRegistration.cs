using LearnEngine.Core.Choosing;

namespace LearnEngine.Core.Registration;

// A move allowed in a state, kept in the service collection until the catalog is built from all of them.
internal sealed class AllowedMoveRegistration
{
    public AllowedMove Allowed { get; }

    private AllowedMoveRegistration(AllowedMove allowed) => Allowed = allowed;

    public static AllowedMoveRegistration Create(AllowedMove allowed) => new(allowed);
}
