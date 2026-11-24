namespace LearnEngine.Mcp.Tools;

/// <summary>
/// Lets one report_turn run at a time, because the engine reads the history, keeps a turn and picks a move in separate
/// steps, and two turns reported at once would both be kept after the same odds.
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
