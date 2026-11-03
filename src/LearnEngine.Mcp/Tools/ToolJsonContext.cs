using System.Text.Json;
using System.Text.Json.Serialization;

namespace LearnEngine.Mcp.Tools;

/// <summary>
/// The source generated JSON contracts of every tool parameter and result, so the tools need no reflection to read
/// their arguments, write their results or describe their output schema, and work under Native AOT.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, RespectNullableAnnotations = true)]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(SessionOpened))]
[JsonSerializable(typeof(TurnReport))]
[JsonSerializable(typeof(SessionEnded))]
public sealed partial class ToolJsonContext : JsonSerializerContext;
