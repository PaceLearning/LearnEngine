using System.Collections.Immutable;
using System.ComponentModel;
using LearnEngine.Core.History;
using LearnEngine.Core.Moves;
using LearnEngine.Core.Sessions;
using LearnEngine.Core.Signals;
using LearnEngine.Core.States;
using LearnEngine.Mcp.Compliance;
using LearnEngine.Mcp.Storage;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace LearnEngine.Mcp.Tools;

/// <summary>
/// The tools the AI calls while it teaches: start a session, report every student message as signal ids and get the
/// move to make next, and end the session with how many messages were recorded.
/// </summary>
[McpServerToolType]
public sealed class LearnTools(
    SessionEngine engine,
    SignalCatalog signalCatalog,
    MoveCatalog moves,
    StateCatalog states,
    IHistoryStore histories,
    ComplianceLog compliance,
    TurnGate gate,
    ServerSettings settings)
{
    private const string TeachNormally = "Teach as you normally would.";

    /// <summary>
    /// Opens a new session and gives its id, the signal ids the AI may report and the next call to make.
    /// </summary>
    [McpServerTool(
        Name = "start_session",
        Title = "Start learning session",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(SessionOpened))]
    [McpMeta("anthropic/alwaysLoad", true)]
    [Description("Opens a learning session and returns its sessionId, the signal ids you may report, and what to do first. Call it once when the student starts learning something, before you teach. Then call report_turn after every message the student sends.")]
    public async Task<CallToolResult> StartSession(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var sessionId = $"{now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..22];

        var started = await engine.StartSession(settings.LearnerId, sessionId, cancellationToken);
        if (started.IsT1)
            return ToolResults.Error("A session with the same id was already open. Call start_session again.");

        await compliance.Append(settings.LearnerId, sessionId, ComplianceEntry.Started(now), cancellationToken);

        var opened = SessionOpened.Create(
            sessionId,
            [.. signalCatalog.All.Select(signal => SignalChoice.Create(signal.Id, signal.Description))],
            TeachNormally,
            $"After the student's next message, call report_turn with sessionId \"{sessionId}\" and turn 1, before you reply.");
        return ToolResults.Structured(opened);
    }

    /// <summary>
    /// Reports one student message as signal ids and gives the move to make next, or says to teach as usual.
    /// A session that is not open is opened, and the answer says so.
    /// </summary>
    [McpServerTool(
        Name = "report_turn",
        Title = "Report student message",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(TurnReport))]
    [McpMeta("anthropic/alwaysLoad", true)]
    [Description("Reports one student message as signal ids and returns what to do next. Call it after every message the student sends in a session, before you reply, even short ones like \"ok\". signals are ids from start_session that describe the message, never its words; send an empty list when none fit. turn counts the student's messages in this session, starting at 1.")]
    public async Task<CallToolResult> ReportTurn(
        [Description("The sessionId start_session returned.")] string sessionId,
        [Description("The number of this student message in the session, starting at 1.")] int turn,
        [Description("The ids of every signal that fits this message, from the list start_session returned. An empty list when none fit.")] string[] signals,
        [Description("The id of the move you used in your last reply, if you used one.")] string? lastMoveId = null,
        CancellationToken cancellationToken = default)
    {
        if (!FileNames.IsSafe(sessionId))
            return ToolResults.Error($"The sessionId \"{sessionId}\" is not valid. Use the sessionId start_session returned.");

        if (turn < 1)
            return ToolResults.Error($"turn {turn} is not valid. turn counts the student's messages in this session, starting at 1.");

        ImmutableArray<string> unknown = [.. signals
            .Select(signalId => signalId ?? "null")
            .Where(signalId => signalCatalog.Find(signalId).IsT1)
            .Distinct(StringComparer.Ordinal)];
        if (unknown.Length > 0)
            return ToolResults.Error($"Unknown signal ids: {string.Join(", ", unknown)}. Nothing was recorded. Call report_turn again for turn {turn} with ids from this list only: {string.Join(", ", signalCatalog.All.Select(signal => signal.Id))}.");

        // Kept as the AI sent it only when it names a move, so the log never holds free text.
        string? usedMove = null;
        if (lastMoveId is not null)
            usedMove = "unknown";

        if (lastMoveId is not null && moves.Find(lastMoveId).IsT0)
            usedMove = lastMoveId;

        await gate.Enter(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var last = await compliance.Last(settings.LearnerId, sessionId, cancellationToken);
            var highestBefore = last?.HighestTurn ?? 0;
            var moveSent = last?.MoveGiven;

            var reported = await engine.ReportTurn(settings.LearnerId, sessionId, signals, cancellationToken);
            var opened = reported.IsT2;
            if (opened)
            {
                await engine.StartSession(settings.LearnerId, sessionId, cancellationToken);
                await compliance.Append(settings.LearnerId, sessionId, ComplianceEntry.Started(now), cancellationToken);
                reported = await engine.ReportTurn(settings.LearnerId, sessionId, signals, cancellationToken);

                // The turns before this one went to no session, so none of them counts as skipped here.
                highestBefore = turn - 1;
                moveSent = null;
            }

            if (reported.IsT1)
                return ToolResults.Error($"Unknown signal ids: {string.Join(", ", reported.AsT1.Select(signal => signal.Id))}. Nothing was recorded. Call report_turn again with ids from the list start_session returned.");

            if (reported.IsT2)
                return ToolResults.Error("The session could not be opened. Call start_session and report this message as turn 1 of the new session.");

            if (reported.IsT3)
                return ToolResults.Error("This session was kept under an older vocabulary and cannot go on. Call start_session and report this message as turn 1 of the new session.");

            if (reported.IsT4)
                return ToolResults.Error("This session's history cannot be read, so it cannot go on. Call start_session and report this message as turn 1 of the new session.");

            var result = reported.AsT0;
            var belief = result.Belief;

            // A skipped stretch is listed up to 100 turns, so a wild turn number cannot fill the answer or the log.
            ImmutableArray<int> missed = [];
            if (turn > highestBefore + 1)
                missed = [.. Enumerable.Range(highestBefore + 1, Math.Min(turn - highestBefore - 1, 100))];

            var repeated = turn <= highestBefore;
            var highestAfter = Math.Max(highestBefore, turn);

            // A move still being judged stays the move to make, since the engine picks no other while it waits for a result.
            MoveToMake? move = null;
            if (belief.IsClear && result.Pick.IsT0)
                move = MoveToMake.Create(result.Pick.AsT0.Move.Id, result.Pick.AsT0.Move.Description);

            // The engine only picks registered moves, so the move being judged is always found.
            if (belief.IsClear && result.Pick.IsT2)
                move = MoveToMake.Create(result.Pick.AsT2.MoveId, moves.Find(result.Pick.AsT2.MoveId).AsT0.Description);

            var leader = belief.Leader;
            var runnerUp = belief.RunnerUp;
            var summary = $"unclear: {leader.StateId} {Math.Round(leader.Odds * 100)}% or {runnerUp?.StateId} {Math.Round((runnerUp?.Odds ?? 0) * 100)}%";
            if (belief.IsClear)
                summary = $"{leader.StateId} {Math.Round(leader.Odds * 100)}%: {states.Find(leader.StateId).AsT0.Description}";

            var confidence = "low";
            if (belief.IsClear)
                confidence = "medium";

            if (belief.IsClear && belief.Margin >= 0.4)
                confidence = "high";

            var next = $"After the student's next message, call report_turn with sessionId \"{sessionId}\" and turn {highestAfter + 1}, before you reply.";
            if (missed.Length == 1)
                next = $"Turn {missed[0]} was not reported. Report every student message. {next}";

            if (missed.Length == 2)
                next = $"Turns {missed[0]} and {missed[1]} were not reported. Report every student message. {next}";

            if (missed.Length > 2)
                next = $"Turns {missed[0]} to {missed[^1]} were not reported. Report every student message. {next}";

            if (repeated)
                next = $"Turn {turn} was already reported, so this message was recorded as another one. Count on from the highest turn. {next}";

            if (opened)
                next = $"No session was open with this sessionId, so a new one was opened and this message was recorded in it. {next}";

            await compliance.Append(
                settings.LearnerId,
                sessionId,
                ComplianceEntry.ForTurn(now, turn, highestAfter, missed, signals.Length, usedMove, moveSent, move?.Id, opened),
                cancellationToken);

            string? teach = null;
            if (move is null)
                teach = TeachNormally;

            var report = TurnReport.Create(turn, belief.IsClear, move, teach, StateSummary.Create(summary, confidence), missed, next);
            return ToolResults.Structured(report);
        }
        finally
        {
            gate.Leave();
        }
    }

    /// <summary>
    /// Ends a session and gives how many of the student's messages were recorded, to tell the student.
    /// </summary>
    [McpServerTool(
        Name = "end_session",
        Title = "End learning session",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(SessionEnded))]
    [Description("Ends a learning session and returns how many of the student's messages were recorded. Call it when the student stops learning for now, and tell them the message it returns.")]
    public async Task<CallToolResult> EndSession(
        [Description("The sessionId start_session returned.")] string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (!FileNames.IsSafe(sessionId))
            return ToolResults.Error($"The sessionId \"{sessionId}\" is not valid. Use the sessionId start_session returned.");

        await gate.Enter(cancellationToken);
        try
        {
            var history = await histories.Load(settings.LearnerId, sessionId, cancellationToken);
            var recorded = history.Count(historyEvent => historyEvent is LearnerTurn);
            var last = await compliance.Last(settings.LearnerId, sessionId, cancellationToken);
            var sent = Math.Max(last?.HighestTurn ?? 0, recorded);

            var message = $"PACE recorded {recorded} of your {sent} messages in this session.";
            if (sent == 0)
                message = "PACE recorded none of your messages in this session.";

            if (sent == 1 && recorded == 1)
                message = "PACE recorded your one message in this session.";

            if (!history.IsDefaultOrEmpty)
                await compliance.Append(settings.LearnerId, sessionId, ComplianceEntry.Ended(DateTimeOffset.UtcNow, sent), cancellationToken);

            return ToolResults.Structured(SessionEnded.Create(sessionId, recorded, sent, message));
        }
        finally
        {
            gate.Leave();
        }
    }
}
