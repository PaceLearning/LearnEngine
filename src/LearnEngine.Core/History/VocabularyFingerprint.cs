using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using LearnEngine.Core.Choosing;
using LearnEngine.Core.Effects;
using LearnEngine.Core.Likelihoods;
using LearnEngine.Core.Moves;
using LearnEngine.Core.Signals;
using LearnEngine.Core.States;

namespace LearnEngine.Core.History;

/// <summary>
/// A short hash of the whole vocabulary the app registered: every signal, state, prior, rate and move, with their descriptions,
/// which moves are allowed in which state, where each move leads, which states are goals, all in the order they
/// were registered, the fade and the rate weight of the learning.
/// Every session is stamped with it, so a history is never replayed under another vocabulary.
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
        MoveCatalog? moves,
        AllowedMoveCatalog? allowedMoves,
        MoveEffectCatalog effects,
        GoalStates? goals,
        double rateWeight)
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

            // A move is only picked where it is allowed, so a history is never continued under other allowed moves.
            // An app with none fingerprints them as an empty list.
            writer.WriteStartArray("allowed_moves");
            foreach (var allowed in allowedMoves?.All ?? [])
            {
                writer.WriteStartArray();
                writer.WriteStringValue(allowed.StateId);
                writer.WriteStringValue(allowed.MoveId);
                writer.WriteEndArray();
            }
            writer.WriteEndArray();

            // Learning changes what a session keeps after each turn, so a session kept before it, or under another
            // weight, is never continued with it.
            writer.WriteNumber("rate_weight", rateWeight);

            // The fade is the effect of no move, so it moves the odds as much as any registered effect does.
            writer.WriteNumber("fade", effects.Fade);

            writer.WriteStartArray("move_effects");
            foreach (var effect in effects.All)
            {
                writer.WriteStartArray();
                writer.WriteStringValue(effect.FromStateId);
                writer.WriteStringValue(effect.MoveId);
                writer.WriteNumberValue(effect.Weight);
                foreach (var chance in effect.Chances)
                {
                    writer.WriteStartArray();
                    writer.WriteStringValue(chance.StateId);
                    writer.WriteNumberValue(chance.Odds);
                    writer.WriteEndArray();
                }
                writer.WriteEndArray();
            }
            writer.WriteEndArray();

            writer.WriteStartArray("goal_states");
            foreach (var stateId in goals?.StateIds ?? [])
                writer.WriteStringValue(stateId);
            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        var hash = SHA256.HashData(buffer.WrittenSpan);
        return new(Convert.ToHexStringLower(hash.AsSpan(0, 8)));
    }
}
