using System.Text.Json.Serialization;

namespace LearnEngine.Core.Prediction;

/// <summary>
/// The chance that a signal shows up on the next learner turn, such as a 31% chance of asks.how_or_why, and its usual
/// chance for the learner, so it can be read against the usual.
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

    /// <summary>
    /// The usual chance of the signal for the learner, from 0 to 1: its chance with the learner's own rates and the
    /// states weighed by their priors, as if nothing were known about where the learner is. It is 0 in an expectation
    /// kept before usual chances were kept.
    /// </summary>
    public double Usual { get; }

    /// <summary>
    /// How many times its usual chance the signal is expected: <see cref="Chance"/> over <see cref="Usual"/>. Above 1
    /// the signal is likelier than usual. It is 0 when the usual chance is 0.
    /// </summary>
    [JsonIgnore]
    public double VsUsual => Usual > 0 ? Chance / Usual : 0;

    // Internal, not private, so the source generated JSON reader of the history can call it. The usual chance is
    // optional so an expectation kept before it was kept still reads.
    [JsonConstructor]
    internal SignalChance(string signalId, double chance, double usual = 0)
    {
        SignalId = signalId;
        Chance = chance;
        Usual = usual;
    }

    /// <summary>
    /// Creates the chance of the given signal with its usual chance for the learner.
    /// </summary>
    public static SignalChance Create(string signalId, double chance, double usual) => new(signalId, chance, usual);
}
