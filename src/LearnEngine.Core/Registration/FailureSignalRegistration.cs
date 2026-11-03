namespace LearnEngine.Core.Registration;

// A signal that shows a move failed, kept in the service collection until the outcome signals are built from all of them.
internal sealed class FailureSignalRegistration
{
    public string SignalId { get; }

    private FailureSignalRegistration(string signalId) => SignalId = signalId;

    public static FailureSignalRegistration Create(string signalId) => new(signalId);
}
