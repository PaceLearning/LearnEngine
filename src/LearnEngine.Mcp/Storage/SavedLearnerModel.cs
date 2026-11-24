using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace LearnEngine.Mcp.Storage;

// The whole of a learner model file: the student's counts of every signal in every state that has some.
internal sealed class SavedLearnerModel
{
    public ImmutableArray<SavedRateCount> Rates { get; }

    // Internal, not private, so the source generated JSON reader can call it.
    [JsonConstructor]
    internal SavedLearnerModel(ImmutableArray<SavedRateCount> rates) => Rates = rates;

    public static SavedLearnerModel Create(IEnumerable<SavedRateCount> rates) => new([.. rates]);
}
