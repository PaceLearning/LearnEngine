using System.ComponentModel;
using ModelContextProtocol.Server;

namespace LearnEngine.Mcp.Prompts;

/// <summary>
/// The prompt the student picks from their AI's prompt menu to start learning. It is a message in the student's voice,
/// so the AI treats the rules in it as the student's own request.
/// </summary>
[McpServerPromptType]
public sealed class LearnPrompts
{
    /// <summary>
    /// Starts a learning session on the topic, with the rules for reporting every message in the student's words.
    /// </summary>
    [McpServerPrompt(Name = "start_learning", Title = "Start learning")]
    [Description("Start learning a topic with your AI as a tutor that adapts to how you learn as you go.")]
    public static string StartLearning([Description("What you want to learn, such as recursion or the French past tense.")] string topic)
    {
        var about = string.IsNullOrWhiteSpace(topic) ? "something new" : topic.Trim();
        return $"""
            I want to learn {about} with you as my tutor.

            1. Call start_session now, before you teach me anything, and keep the sessionId.
            2. After every message I send, call report_turn before you reply, even when I only say "ok". Count my messages from 1 as turn, and send only signal ids from the list start_session gave you. Never send my words.
            3. Read the result before you reply. When clear is true, make its move. When it is false, teach as you normally would. Always answer what I ask for outright.
            4. Never show me odds, states, scores or labels about me.
            5. When I say I am done, call end_session and tell me how many of my messages were recorded.

            Teach me one idea at a time, and ask me to predict or explain before you explain. Start by asking what I already know about {about}.
            """;
    }
}
