namespace LearnEngine.Mcp.Tools;

/// <summary>
/// A signal the engine expects in the student's next message, with its chance. For the AI only.
/// </summary>
public sealed class ExpectedSignal
{
    /// <summary>
    /// The id of the signal, such as "asks.how_or_why".
    /// </summary>
    public string Signal { get; }

    /// <summary>
    /// The chance that the next message shows the signal, from 0 to 1, rounded to two places.
    /// </summary>
    public double Chance { get; }

    private ExpectedSignal(string signal, double chance)
    {
        Signal = signal;
        Chance = chance;
    }

    /// <summary>
    /// Creates the expected signal with the given id and chance.
    /// </summary>
    public static ExpectedSignal Create(string signal, double chance) => new(signal, chance);
}
