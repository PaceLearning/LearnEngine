using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace LearnEngine.Mcp.Storage;

// The whole of a learner model file: the student's counts of every signal in every state that has some, and of every
// step from a state to a state after a move.
internal sealed class SavedLearnerModel
{
    public ImmutableArray<SavedRateCount> Rates { get; }

    public ImmutableArray<SavedEffectCount> Effects { get; }

    // Internal, not private, so the source generated JSON reader can call it. A file written before effects were
    // learned has none, and reads as none.
    [JsonConstructor]
    internal SavedLearnerModel(ImmutableArray<SavedRateCount> rates, ImmutableArray<SavedEffectCount> effects = default)
    {
        Rates = rates;
        Effects = effects.IsDefault ? [] : effects;
    }

    public static SavedLearnerModel Create(IEnumerable<SavedRateCount> rates, IEnumerable<SavedEffectCount> effects) =>
        new([.. rates], [.. effects]);
}
