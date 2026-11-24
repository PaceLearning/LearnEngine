using System.Text.Json;
using ModelContextProtocol.Protocol;
using Xunit;

namespace LearnEngine.Tests;

/// <summary>Reads what a tool answered, for the tests of the stdio server and of the web app.</summary>
internal static class ToolAnswers
{
    /// <summary>
    /// Checks the call did not fail and gave its answer once as text and once as structured content, then gives the answer.
    /// </summary>
    public static JsonElement Read(CallToolResult result)
    {
        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Equal(text, result.StructuredContent!.Value.GetRawText());
        return JsonDocument.Parse(text).RootElement.Clone();
    }
}
