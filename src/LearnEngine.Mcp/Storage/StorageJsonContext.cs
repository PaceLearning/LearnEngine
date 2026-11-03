using System.Text.Json.Serialization;

namespace LearnEngine.Mcp.Storage;

// Source generated so the files are written and read without reflection and work under Native AOT.
// Required and non-nullable members are enforced, so a line with a missing or null value is unreadable.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(SavedScore))]
internal sealed partial class StorageJsonContext : JsonSerializerContext;
