using LearnEngine.Core.Effects;

namespace LearnEngine.Core.Registration;

// A move effect kept in the service collection until the effect catalog is built from all of them.
internal sealed class MoveEffectRegistration
{
    public MoveEffect Effect { get; }

    private MoveEffectRegistration(MoveEffect effect) => Effect = effect;

    public static MoveEffectRegistration Create(MoveEffect effect) => new(effect);
}
