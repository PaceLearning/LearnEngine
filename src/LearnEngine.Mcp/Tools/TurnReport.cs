using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace LearnEngine.Mcp.Tools;

/// <summary>
/// The answer to report_turn: whether the student's state is clear, the move to make and why or how to teach without
/// one, what the engine expects of the next message and the state, both for the AI only, the turns that were skipped
/// and the next call to make.
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
    /// Why the move was chosen, from what the engine learned about this student, such as
    /// "For this learner, worked_example led from stuck to following 6 of 8 times.". For the AI only. Left out when
    /// there is no move.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Why { get; }

    /// <summary>
    /// What to do when there is no move, such as "Teach as you normally would.". Left out when there is a move.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Do { get; }

    /// <summary>
    /// The signals and topics the engine expects in the student's next message, for the AI only.
    /// </summary>
    public ExpectedNext Expect { get; }

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

    private TurnReport(int turn, bool clear, MoveToMake? move, string? why, string? @do, ExpectedNext expect, StateSummary state, ImmutableArray<int> missedTurns, string next)
    {
        Turn = turn;
        Clear = clear;
        Move = move;
        Why = why;
        Do = @do;
        Expect = expect;
        State = state;
        MissedTurns = missedTurns;
        Next = next;
    }

    /// <summary>
    /// Creates the answer to one reported turn.
    /// </summary>
    public static TurnReport Create(int turn, bool clear, MoveToMake? move, string? why, string? @do, ExpectedNext expect, StateSummary state, ImmutableArray<int> missedTurns, string next) =>
        new(turn, clear, move, why, @do, expect, state, missedTurns, next);
}
