using System.Text.Json.Serialization;

namespace LearnEngine.Core.History;

/// <summary>
/// The source generated JSON of history events, for a host that writes them with System.Text.Json itself, such as
/// a store that keeps each event in a JSON database column. Write and read an event with
/// <c>HistoryJsonContext.Default.HistoryEvent</c>, or give <c>HistoryJsonContext.Default.Options</c> to a host API that
/// takes options, so every event is written as <see cref="HistoryEventJson"/> writes it.
/// Do not put this context in the resolver chain of other options: those options do not have its snake case names
/// or its checks for missing and null values. A host cannot generate this JSON in its own context either, because
/// the event constructors are internal.
/// </summary>
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
public sealed partial class HistoryJsonContext : JsonSerializerContext;
