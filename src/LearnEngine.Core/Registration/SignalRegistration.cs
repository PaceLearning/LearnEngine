using LearnEngine.Core.Signals;

namespace LearnEngine.Core.Registration;

// A signal kept in the service collection until the catalog is built from all of them.
internal sealed class SignalRegistration
{
    public Signal Signal { get; }

    private SignalRegistration(Signal signal) => Signal = signal;

    public static SignalRegistration Create(Signal signal) => new(signal);
}
