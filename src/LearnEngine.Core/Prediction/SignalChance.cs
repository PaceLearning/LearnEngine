using System.Text.Json.Serialization;

namespace LearnEngine.Core.Prediction;

/// <summary>
/// The chance that a signal shows up on the next learner turn, such as a 31% chance of asks.how_or_why.
/// </summary>
public sealed record SignalChance
{
    /// <summary>
    /// The id of the signal, such as "asks.how_or_why".
    /// </summary>
    public string SignalId { get; }

    /// <summary>
    /// The chance that the signal shows up, from 0 to 1.
    /// </summary>
    public double Chance { get; }

    // Internal, not private, so the source generated JSON reader of the history can call it.
    [JsonConstructor]
    internal SignalChance(string signalId, double chance)
    {
        SignalId = signalId;
        Chance = chance;
    }

    /// <summary>
    /// Creates the chance of the given signal.
    /// </summary>
    public static SignalChance Create(string signalId, double chance) => new(signalId, chance);
}
