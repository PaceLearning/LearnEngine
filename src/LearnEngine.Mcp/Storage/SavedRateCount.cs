using System.Text.Json.Serialization;

namespace LearnEngine.Mcp.Storage;

// The student's counts of one signal in one state, in a learner model file.
internal sealed class SavedRateCount
{
    public string SignalId { get; }

    public string StateId { get; }

    public double Shown { get; }

    public double NotShown { get; }

    // Internal, not private, so the source generated JSON reader can call it.
    [JsonConstructor]
    internal SavedRateCount(string signalId, string stateId, double shown, double notShown)
    {
        SignalId = signalId;
        StateId = stateId;
        Shown = shown;
        NotShown = notShown;
    }

    public static SavedRateCount Create(string signalId, string stateId, double shown, double notShown) =>
        new(signalId, stateId, shown, notShown);
}
