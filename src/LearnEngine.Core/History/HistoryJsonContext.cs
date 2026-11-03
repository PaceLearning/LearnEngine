using System.Text.Json.Serialization;

namespace LearnEngine.Core.History;

// Source generated so writing and reading events needs no reflection and works under Native AOT.
// Required and non-nullable members are enforced, so a line with a missing or null value is unreadable instead of
// giving an event that breaks the replay. The event name may come after the other values, because a store such as
// a JSON database column can reorder them.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    AllowOutOfOrderMetadataProperties = true)]
[JsonSerializable(typeof(HistoryEvent))]
internal sealed partial class HistoryJsonContext : JsonSerializerContext;
