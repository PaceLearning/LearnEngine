namespace LearnEngine.Mcp.Tools;

/// <summary>
/// A signal the engine expects in the student's next message more than usual, with its chance and how many times its
/// usual chance that is. For the AI only.
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

    /// <summary>
    /// The chance over the signal's usual chance for this student, above 1, rounded to two places.
    /// </summary>
    public double VsUsual { get; }

    private ExpectedSignal(string signal, double chance, double vsUsual)
    {
        Signal = signal;
        Chance = chance;
        VsUsual = vsUsual;
    }

    /// <summary>
    /// Creates the expected signal with the given id, chance and chance over its usual one.
    /// </summary>
    public static ExpectedSignal Create(string signal, double chance, double vsUsual) => new(signal, chance, vsUsual);
}
