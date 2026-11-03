namespace LearnEngine.Mcp;

/// <summary>
/// The instructions the server sends the AI when it connects. The most important rule comes first and the whole text
/// stays well under 2048 characters, because a host may cut the instructions there and the end is lost first.
/// Each rule is said again in the description of the tool it is about.
/// </summary>
public static class ServerInstructions
{
    /// <summary>
    /// The instructions as plain text, with lines ending in a line feed.
    /// </summary>
    // The raw string takes the line endings of this source file, which differ between checkouts.
    public static string Text { get; } = """
        LearnEngine tracks how the student is learning so you can teach them well. Three rules come before anything else.
        1. When the student starts learning something, call start_session before you teach. Keep the sessionId.
        2. After every message the student sends in that session, call report_turn before you reply, even for short messages like "ok". turn counts their messages from 1. Send only signal ids from the list start_session returned that describe the message. Never send the student's words. If no signal fits, send an empty list. Send the move you used last as lastMoveId.
        3. Read the result of report_turn before you reply. When clear is true, make its move. When clear is false, teach as you normally would. Always answer what the student asks for outright. The next field of every result names your next call.
        How to teach: ask the student to predict or explain before you explain. Give one idea at a time. Prefer a hint or a question to an answer. Never show the student odds, states, scores or labels about them.
        If report_turn says it opened a new session, go on with the same sessionId. When the student stops, call end_session and tell them how many of their messages were recorded.
        """.ReplaceLineEndings("\n");
}
