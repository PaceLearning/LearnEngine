using System.Text.Json.Serialization;

namespace LearnEngine.Core.Learning;

// Source generated so a learner model is written and read without reflection and works under Native AOT.
// A model is kept as the change that gives it when added to an empty model, since a change holds the same four lists
// of counts and can be read back. Required and non-nullable members are enforced, so JSON with a missing or null value
// is unreadable.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(LearnerModelChange))]
internal sealed partial class LearnerModelJsonContext : JsonSerializerContext;
