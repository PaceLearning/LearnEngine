using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using LearnEngine.Core.Likelihoods;
using LearnEngine.Core.Moves;
using LearnEngine.Core.Signals;
using LearnEngine.Core.States;

namespace LearnEngine.Core.History;

/// <summary>
/// A short hash of the whole vocabulary the app registered: every signal, state, prior, rate and move, with their descriptions
/// and in the order they were registered. Every session is stamped with it, so a history is never replayed under another vocabulary.
/// </summary>
public sealed class VocabularyFingerprint
{
    /// <summary>
    /// The fingerprint as 16 lowercase hex characters. It stays the same across runs and machines while the vocabulary does.
    /// </summary>
    public string Value { get; }

    private VocabularyFingerprint(string value) => Value = value;

    // Internal so a fingerprint only comes from registration, which has already checked the vocabulary.
    internal static VocabularyFingerprint Create(
        SignalCatalog signals,
        StateCatalog states,
        LikelihoodTable likelihoods,
        MoveCatalog? moves)
    {
        // Written as JSON so ids and descriptions are escaped and numbers are written the same way in every culture,
        // so no two vocabularies can give the same text.
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();

            writer.WriteStartArray("signals");
            foreach (var signal in signals.All)
            {
                writer.WriteStartArray();
                writer.WriteStringValue(signal.Id);
                writer.WriteStringValue(signal.Description);
                writer.WriteEndArray();
            }
            writer.WriteEndArray();

            writer.WriteStartArray("states");
            foreach (var state in states.All)
            {
                writer.WriteStartArray();
                writer.WriteStringValue(state.Id);
                writer.WriteStringValue(state.Description);
                writer.WriteNumberValue(state.Prior);
                writer.WriteEndArray();
            }
            writer.WriteEndArray();

            writer.WriteStartArray("likelihoods");
            foreach (var likelihood in likelihoods.All)
            {
                writer.WriteStartArray();
                writer.WriteStringValue(likelihood.SignalId);
                writer.WriteStringValue(likelihood.StateId);
                writer.WriteNumberValue(likelihood.Rate);
                writer.WriteEndArray();
            }
            writer.WriteEndArray();

            // An app with no moves has no move catalog, and fingerprints as an empty list of moves.
            writer.WriteStartArray("moves");
            foreach (var move in moves?.All ?? [])
            {
                writer.WriteStartArray();
                writer.WriteStringValue(move.Id);
                writer.WriteStringValue(move.Description);
                writer.WriteEndArray();
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        var hash = SHA256.HashData(buffer.WrittenSpan);
        return new(Convert.ToHexStringLower(hash.AsSpan(0, 8)));
    }
}
