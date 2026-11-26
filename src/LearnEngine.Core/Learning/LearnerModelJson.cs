using System.Text.Json;
using LearnEngine.Core.Topics;

namespace LearnEngine.Core.Learning;

/// <summary>
/// Writes a learner model as JSON and reads it back, for a store that keeps one model per learner, such as a file or a
/// JSON database column. The JSON holds the counts of the model, such as
/// {"rates":[{"signal_id":"says.dont_get_it","state_id":"stuck","shown":1.2,"not_shown":9.8}],"effects":[],"topic_links":[],"topic_states":[]}.
/// </summary>
public static class LearnerModelJson
{
    /// <summary>
    /// Writes the model as JSON with its rates, effects, topic links and topic states.
    /// </summary>
    public static string Write(LearnerModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var counts = LearnerModelChange.Create(model.Rates, model.Effects, model.TopicLinks, model.TopicStates);
        return JsonSerializer.Serialize(counts, LearnerModelJsonContext.Default.LearnerModelChange);
    }

    /// <summary>
    /// Reads JSON written by <see cref="Write"/>, or says why it is not a learner model. JSON written before effects or
    /// topics were learned has none of them, and reads as none.
    /// </summary>
    public static Outcome<LearnerModel, UnreadableLearnerModel> Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        LearnerModelChange? counts;
        try
        {
            counts = JsonSerializer.Deserialize(json, LearnerModelJsonContext.Default.LearnerModelChange);
        }
        catch (JsonException exception)
        {
            return UnreadableLearnerModel.Create(json, exception.Message);
        }

        if (counts is null)
            return UnreadableLearnerModel.Create(json, "The JSON is null, not a learner model.");

        // Nullable annotations are not enforced inside a list, and the counts are checked again as the model is made,
        // so every count is checked here first and a bad one is unreadable instead of throwing.
        var badRate = counts.Rates.Any(count =>
            count is null
            || string.IsNullOrWhiteSpace(count.SignalId)
            || string.IsNullOrWhiteSpace(count.StateId)
            || !(count.Shown >= 0 && double.IsFinite(count.Shown))
            || !(count.NotShown >= 0 && double.IsFinite(count.NotShown)));
        if (badRate)
            return UnreadableLearnerModel.Create(json, "A rate count is null, has an empty id or is not 0 or more.");

        var badEffect = counts.Effects.Any(count =>
            count is null
            || string.IsNullOrWhiteSpace(count.FromStateId)
            || string.IsNullOrWhiteSpace(count.ToStateId)
            || (count.MoveId is not null && string.IsNullOrWhiteSpace(count.MoveId))
            || !(count.Count >= 0 && double.IsFinite(count.Count)));
        if (badEffect)
            return UnreadableLearnerModel.Create(json, "An effect count is null, has an empty id or is not 0 or more.");

        var badLink = counts.TopicLinks.Any(count =>
            count is null
            || !TopicIds.IsValid(count.FromTopic)
            || !TopicIds.IsValid(count.ToTopic)
            || !(count.Count >= 0 && double.IsFinite(count.Count)));
        if (badLink)
            return UnreadableLearnerModel.Create(json, "A topic link count is null, has a topic that is not valid or is not 0 or more.");

        var badTopicState = counts.TopicStates.Any(count =>
            count is null
            || !TopicIds.IsValid(count.Topic)
            || string.IsNullOrWhiteSpace(count.StateId)
            || !(count.Count >= 0 && double.IsFinite(count.Count)));
        if (badTopicState)
            return UnreadableLearnerModel.Create(json, "A topic state count is null, has a topic or state that is not valid or is not 0 or more.");

        return LearnerModel.Create(counts.Rates, counts.Effects, counts.TopicLinks, counts.TopicStates);
    }
}
