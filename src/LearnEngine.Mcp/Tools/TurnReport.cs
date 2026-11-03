using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace LearnEngine.Mcp.Tools;

/// <summary>
/// The answer to report_turn: whether the student's state is clear, the move to make or how to teach without one,
/// the state for the AI only, the turns that were skipped and the next call to make.
/// </summary>
public sealed class TurnReport
{
    /// <summary>
    /// The turn number the AI reported.
    /// </summary>
    public int Turn { get; }

    /// <summary>
    /// True when the engine is clear about the state the student is in, so the move can be trusted.
    /// </summary>
    public bool Clear { get; }

    /// <summary>
    /// The move to make in the next reply, or null when there is none. Written even when null, so the AI sees there is none.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public MoveToMake? Move { get; }

    /// <summary>
    /// What to do when there is no move, such as "Teach as you normally would.". Left out when there is a move.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Do { get; }

    /// <summary>
    /// What the engine believes about the student, for the AI only.
    /// </summary>
    public StateSummary State { get; }

    /// <summary>
    /// The turn numbers skipped between the last report and this one.
    /// </summary>
    public ImmutableArray<int> MissedTurns { get; }

    /// <summary>
    /// The next call to make, with anything the AI must fix first.
    /// </summary>
    public string Next { get; }

    private TurnReport(int turn, bool clear, MoveToMake? move, string? @do, StateSummary state, ImmutableArray<int> missedTurns, string next)
    {
        Turn = turn;
        Clear = clear;
        Move = move;
        Do = @do;
        State = state;
        MissedTurns = missedTurns;
        Next = next;
    }

    /// <summary>
    /// Creates the answer to one reported turn.
    /// </summary>
    public static TurnReport Create(int turn, bool clear, MoveToMake? move, string? @do, StateSummary state, ImmutableArray<int> missedTurns, string next) =>
        new(turn, clear, move, @do, state, missedTurns, next);
}
