namespace LearnEngine.Core.Registration;

// A signal that shows a move worked, kept in the service collection until the outcome signals are built from all of them.
internal sealed class SuccessSignalRegistration
{
    public string SignalId { get; }

    private SuccessSignalRegistration(string signalId) => SignalId = signalId;

    public static SuccessSignalRegistration Create(string signalId) => new(signalId);
}
