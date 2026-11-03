using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace LearnEngine.Mcp.Compliance;

/// <summary>
/// One line of a session's compliance log: what the AI did with the tools, so it can be measured whether it reported
/// every student message and used the moves it was given. It holds numbers and ids only, never the student's words.
/// </summary>
public sealed class ComplianceEntry
{
    /// <summary>
    /// What happened: "session_started", "turn" or "session_ended".
    /// </summary>
    public string Event { get; }

    /// <summary>
    /// When it happened.
    /// </summary>
    public DateTimeOffset Time { get; }

    /// <summary>
    /// The turn number the AI reported, counted from 1, or 0 for a session start. For a session end, the highest turn reported.
    /// </summary>
    public int Turn { get; }

    /// <summary>
    /// The highest turn number reported in the session so far. The next report should be one above it.
    /// </summary>
    public int HighestTurn { get; }

    /// <summary>
    /// The turn numbers skipped between the last report and this one.
    /// </summary>
    public ImmutableArray<int> MissedTurns { get; }

    /// <summary>
    /// How many signals the AI reported for the turn.
    /// </summary>
    public int SignalCount { get; }

    /// <summary>
    /// The move the AI says it used last, or null when it did not say.
    /// </summary>
    public string? LastMoveId { get; }

    /// <summary>
    /// The move the server gave in its answer to the turn before, which <see cref="LastMoveId"/> is compared with, or
    /// null when it gave none.
    /// </summary>
    public string? MoveSent { get; }

    /// <summary>
    /// The move the server gives in its answer to this turn, or null when it gives none.
    /// </summary>
    public string? MoveGiven { get; }

    /// <summary>
    /// True when the turn came for a session that was not open, so the server opened it.
    /// </summary>
    public bool OpenedSession { get; }

    // Internal, not private, so the source generated JSON reader can call it.
    [JsonConstructor]
    internal ComplianceEntry(
        string @event,
        DateTimeOffset time,
        int turn,
        int highestTurn,
        ImmutableArray<int> missedTurns,
        int signalCount,
        string? lastMoveId,
        string? moveSent,
        string? moveGiven,
        bool openedSession)
    {
        Event = @event;
        Time = time;
        Turn = turn;
        HighestTurn = highestTurn;
        MissedTurns = missedTurns;
        SignalCount = signalCount;
        LastMoveId = lastMoveId;
        MoveSent = moveSent;
        MoveGiven = moveGiven;
        OpenedSession = openedSession;
    }

    /// <summary>
    /// Creates the entry for a session started with start_session.
    /// </summary>
    public static ComplianceEntry Started(DateTimeOffset time) =>
        new("session_started", time, 0, 0, [], 0, null, null, null, false);

    /// <summary>
    /// Creates the entry for one report_turn call.
    /// </summary>
    public static ComplianceEntry ForTurn(
        DateTimeOffset time,
        int turn,
        int highestTurn,
        ImmutableArray<int> missedTurns,
        int signalCount,
        string? lastMoveId,
        string? moveSent,
        string? moveGiven,
        bool openedSession) =>
        new("turn", time, turn, highestTurn, missedTurns, signalCount, lastMoveId, moveSent, moveGiven, openedSession);

    /// <summary>
    /// Creates the entry for a session ended with end_session, with the highest turn reported in it.
    /// </summary>
    public static ComplianceEntry Ended(DateTimeOffset time, int highestTurn) =>
        new("session_ended", time, highestTurn, highestTurn, [], 0, null, null, null, false);
}
