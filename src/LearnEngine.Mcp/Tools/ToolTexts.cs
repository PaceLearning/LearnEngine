namespace LearnEngine.Mcp.Tools;

/// <summary>
/// The descriptions of the tools and their parameters, in one place, so every host of the tools gives the AI the same
/// words: the stdio server and the web app.
/// </summary>
public static class ToolTexts
{
    /// <summary>
    /// What start_session does and when to call it.
    /// </summary>
    public const string StartSession = "Opens a learning session and returns its sessionId, the signal ids you may report, the topic ids the student has had before, and what to do first. Call it once when the student starts learning something, before you teach. Then call report_turn after every message the student sends.";

    /// <summary>
    /// What report_turn does and when to call it.
    /// </summary>
    public const string ReportTurn = "Reports one student message as signal ids and returns what to do next. Call it after every message the student sends in a session, before you reply, even short ones like \"ok\". signals are ids from start_session that describe the message, never its words; send an empty list when none fit. turn counts the student's messages in this session, starting at 1. Send a topic id for what the message is about and the move you used last as lastMoveId. The result says why its move was chosen and what the next message will likely show and be about, for you only.";

    /// <summary>
    /// What end_session does and when to call it.
    /// </summary>
    public const string EndSession = "Ends a learning session and returns how many of the student's messages were recorded. Call it when the student stops learning for now, and tell them the message it returns.";

    /// <summary>
    /// The sessionId parameter of report_turn and end_session.
    /// </summary>
    public const string SessionId = "The sessionId start_session returned.";

    /// <summary>
    /// The turn parameter of report_turn.
    /// </summary>
    public const string Turn = "The number of this student message in the session, starting at 1.";

    /// <summary>
    /// The signals parameter of report_turn.
    /// </summary>
    public const string Signals = "The ids of every signal that fits this message, from the list start_session returned. An empty list when none fit.";

    /// <summary>
    /// The lastMoveId parameter of report_turn.
    /// </summary>
    public const string LastMoveId = "The id of the move you used in your last reply, if you used one.";

    /// <summary>
    /// The topic parameter of report_turn.
    /// </summary>
    public const string Topic = "A short id for what this message is about, such as \"recursion.base_case\": letters, digits, '.', '_' or '-', at most 64. Use the same id each time the student is on the same topic, and reuse an id from the topics start_session returned when one fits.";
}
