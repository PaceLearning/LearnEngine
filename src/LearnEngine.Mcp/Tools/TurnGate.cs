namespace LearnEngine.Mcp.Tools;

/// <summary>
/// Lets one report_turn run at a time, because the engine keeps a turn and judges the last move in separate steps and
/// two turns reported at once could both be kept before either is judged.
/// </summary>
public sealed class TurnGate
{
    private readonly SemaphoreSlim gate = new(1, 1);

    private TurnGate() { }

    /// <summary>
    /// Creates an open gate.
    /// </summary>
    public static TurnGate Create() => new();

    /// <summary>
    /// Waits until no other turn is being reported, then lets this one through.
    /// </summary>
    public Task Enter(CancellationToken cancellationToken) => gate.WaitAsync(cancellationToken);

    /// <summary>
    /// Lets the next turn through. Call it once for every <see cref="Enter"/>, in a finally block.
    /// </summary>
    public void Leave() => gate.Release();
}
