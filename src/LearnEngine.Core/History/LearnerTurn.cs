using System.Collections.Immutable;
using System.Text.Json.Serialization;
using LearnEngine.Core.Topics;

namespace LearnEngine.Core.History;

/// <summary>
/// One learner turn: the ids of the signals the AI reported for it, never the learner's words, with the topic it was
/// about and the move the tutor made just before it, when the AI sent them.
/// A turn with no signals is still a turn, because every signal not showing up moves the odds too.
/// </summary>
public sealed class LearnerTurn : HistoryEvent
{
    /// <summary>
    /// The ids of the signals the AI reported for the turn, such as "asks.how_or_why", in the order it reported them.
    /// </summary>
    public ImmutableArray<string> SignalIds { get; }

    /// <summary>
    /// The id of the topic the turn was about, such as "recursion.base_case", or null when the AI sent none.
    /// It is always a valid topic id, as <see cref="TopicIds.IsValid"/> checks.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Topic { get; }

    /// <summary>
    /// The id of the move the tutor made in the reply before this turn, such as "worked_example", or null when no move was made.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MoveMade { get; }

    // Internal, not private, so the source generated JSON reader can call it. The topic and the move made are optional,
    // so a turn written before they were kept still reads.
    [JsonConstructor]
    internal LearnerTurn(ImmutableArray<string> signalIds, string? topic = null, string? moveMade = null)
    {
        SignalIds = signalIds;
        Topic = topic;
        MoveMade = moveMade;
    }

    /// <summary>
    /// Creates a learner turn with the ids of the signals the AI reported for it, the topic it was about and the move
    /// made before it, each null when there is none.
    /// A topic that is not valid throws, so check it with <see cref="TopicIds.IsValid"/> first. An empty move id throws too.
    /// </summary>
    public static LearnerTurn Create(IEnumerable<string> signalIds, string? topic = null, string? moveMade = null)
    {
        if (topic is not null && !TopicIds.IsValid(topic))
            throw new ArgumentException($"The topic \"{topic}\" is not a valid topic id. Check it with TopicIds.IsValid first.", nameof(topic));

        if (moveMade is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(moveMade);

        return new([.. signalIds], topic, moveMade);
    }
}
