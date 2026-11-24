using System.ComponentModel;
using LearnEngine.Mcp.Storage;
using LearnEngine.Mcp.Tools;
using LearnEngine.Web.Feed;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace LearnEngine.Web.Tools;

/// <summary>
/// The tools the AI calls over HTTP, the same as the stdio server's: start a session, report every student message as
/// signal ids and get the move to make next, and end the session. Each one is the stdio server's own tool, so the
/// answers and the files kept are the same, and every turn the engine keeps is put on the <see cref="EngineFeed"/>.
/// </summary>
[McpServerToolType]
public sealed class WebLearnTools(LearnTools tools, EngineFeed feed, ServerSettings settings)
{
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
    [Description(ToolTexts.StartSession)]
    public Task<CallToolResult> StartSession(CancellationToken cancellationToken = default) =>
        tools.StartSession(cancellationToken);

    /// <summary>
    /// Reports one student message as signal ids and gives the move to make next, or says to teach as usual, then puts
    /// the turn on the feed when the engine kept it.
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
    [Description(ToolTexts.ReportTurn)]
    public async Task<CallToolResult> ReportTurn(
        [Description(ToolTexts.SessionId)] string sessionId,
        [Description(ToolTexts.Turn)] int turn,
        [Description(ToolTexts.Signals)] string[] signals,
        [Description(ToolTexts.LastMoveId)] string? lastMoveId = null,
        [Description(ToolTexts.Topic)] string? topic = null,
        CancellationToken cancellationToken = default)
    {
        var answer = await tools.ReportTurn(sessionId, turn, signals, lastMoveId, topic, cancellationToken);

        // An error answer kept nothing, so there is no turn for a page to read back.
        if (answer.IsError is not true)
            feed.Publish(TurnShown.Create(settings.LearnerId, sessionId, turn, DateTimeOffset.UtcNow));

        return answer;
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
    [Description(ToolTexts.EndSession)]
    public Task<CallToolResult> EndSession(
        [Description(ToolTexts.SessionId)] string sessionId,
        CancellationToken cancellationToken = default) =>
        tools.EndSession(sessionId, cancellationToken);
}
