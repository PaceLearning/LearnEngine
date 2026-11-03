using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace LearnEngine.Mcp.Tools;

/// <summary>
/// Builds the results every tool gives: a structured result with the same JSON as text for hosts that only read text,
/// and an error result that tells the AI what to fix.
/// </summary>
public static class ToolResults
{
    /// <summary>
    /// The JSON options the tools are registered with and their results are written with: the tool contracts first,
    /// then the MCP protocol contracts. Quotes and apostrophes are written as they are, not as escapes, so the text the
    /// AI reads stays short.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new(McpJsonUtilities.DefaultOptions)
    {
        TypeInfoResolver = JsonTypeInfoResolver.Combine(ToolJsonContext.Default, McpJsonUtilities.DefaultOptions.TypeInfoResolver),
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        RespectNullableAnnotations = true,
    };

    /// <summary>
    /// Gives the value as structured content and as its JSON text.
    /// </summary>
    public static CallToolResult Structured<T>(T value)
    {
        // The type info comes from the source generated contracts in the options, so no reflection is used.
        var typeInfo = (JsonTypeInfo<T>)Options.GetTypeInfo(typeof(T));
        var element = JsonSerializer.SerializeToElement(value, typeInfo);
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = element.GetRawText() }],
            StructuredContent = element,
        };
    }

    /// <summary>
    /// Gives an error result with the message, which says what the AI did wrong and how to call again.
    /// </summary>
    public static CallToolResult Error(string message) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = message }],
    };
}
