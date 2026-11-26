using System.Diagnostics;
using LearnEngine.Core.Effects;
using LearnEngine.Core.Learning;
using LearnEngine.Core.Likelihoods;
using LearnEngine.Core.Odds;
using LearnEngine.Core.Signals;

namespace LearnEngine.Core.Prediction;

/// <summary>
/// Says what the engine expects of a learner's next turn, before it comes, and how far a turn that came was from what
/// was expected. The engine learns from that miss.
/// </summary>
public sealed class Predictor
{
    /// <summary>
    /// How much a topic the learner was not in a goal state on counts towards coming back to it, as a share of one
    /// link: a topic where the learner was never in a goal state counts as half a link.
    /// </summary>
    public const double ComeBackWeight = 0.5;

    private readonly OddsEngine engine;
    private readonly SignalCatalog signals;
    private readonly LikelihoodTable likelihoods;
    private readonly GoalStates? goals;
    private readonly double rateWeight;

    private Predictor(OddsEngine engine, SignalCatalog signals, LikelihoodTable likelihoods, GoalStates? goals, double rateWeight)
    {
        this.engine = engine;
        this.signals = signals;
        this.likelihoods = likelihoods;
        this.goals = goals;
        this.rateWeight = rateWeight;
    }

    // Internal so a predictor only comes from registration, with the registered vocabulary and rate weight. An app with
    // no goal states has none, and gives no topic a come back bonus.
    internal static Predictor Create(OddsEngine engine, SignalCatalog signals, LikelihoodTable likelihoods, GoalStates? goals, double rateWeight) =>
        new(engine, signals, likelihoods, goals, rateWeight);

    /// <summary>
    /// Gives what is expected of the next turn when the learner is believed to be as now and the tutor makes the given
    /// move, or none, first.
    /// The odds of the next turn are predicted with the learner's move effects, as <see cref="OddsEngine.Predict"/> does.
    /// The chance of each signal is the sum over every state of its predicted odds times the learner's rate of the signal
    /// in it. Its usual chance is the same sum over the priors of the states in place of the predicted odds. The chance of each topic is the count of the learner's links from the last topic to it, plus 1 when it is
    /// the last topic itself, since a learner often stays on a topic, plus <see cref="ComeBackWeight"/> times the share
    /// of the learner's turns on it that were not in a goal state, since a learner may come back to a topic they had
    /// trouble with. These are rescaled to add up to 1, most likely first, and a topic with none of them is left out.
    /// The last topic is the topic of the last turn that had one, or null when no turn had one.
    /// </summary>
    public Expectation Expect(Belief belief, LearnerModel model, string? moveId, string? lastTopic)
    {
        ArgumentNullException.ThrowIfNull(belief);
        ArgumentNullException.ThrowIfNull(model);

        var predicted = engine.Predict(belief, model, moveId);
        var priors = engine.Start();

        // Every signal has a rate in every state, because registration checked the table is whole.
        var expectedSignals = signals.All.Select(signal => SignalChance.Create(
            signal.Id,
            predicted.All.Sum(stateOdds => stateOdds.Odds * model.Rate(likelihoods.Find(signal.Id, stateOdds.StateId) is Likelihood likelihood ? likelihood : throw new UnreachableException(), rateWeight)),
            priors.All.Sum(stateOdds => stateOdds.Odds * model.Rate(likelihoods.Find(signal.Id, stateOdds.StateId) is Likelihood likelihood ? likelihood : throw new UnreachableException(), rateWeight))));

        var candidates = (lastTopic is null ? model.Topics : model.Topics.Add(lastTopic)).Distinct(StringComparer.Ordinal);
        var weighed = candidates
            .Select(topic => (
                Topic: topic,
                Linked: model.TopicLinks.Where(count => count.FromTopic == lastTopic && count.ToTopic == topic).Sum(count => count.Count),
                Stays: topic == lastTopic ? 1.0 : 0,
                Turns: model.TopicStates.Where(count => count.Topic == topic).Sum(count => count.Count),
                NotInGoal: goals is null ? 0 : model.TopicStates.Where(count => count.Topic == topic && !goals.Contains(count.StateId)).Sum(count => count.Count)))
            .Select(topic => (topic.Topic, Weight: topic.Linked + topic.Stays + (topic.Turns > 0 ? ComeBackWeight * topic.NotInGoal / topic.Turns : 0)))
            .Where(topic => topic.Weight > 0)
            .ToList();
        var total = weighed.Sum(topic => topic.Weight);
        var expectedTopics = weighed
            .Select(topic => TopicChance.Create(topic.Topic, topic.Weight / total))
            .OrderByDescending(chance => chance.Chance)
            .ThenBy(chance => chance.Topic, StringComparer.Ordinal);

        return Expectation.Create(moveId, predicted.All, expectedSignals, expectedTopics);
    }

    /// <summary>
    /// How unexpected a turn's signals were: minus the natural log of the chance of exactly that set of signals, each
    /// shown or not shown, under the expectation, taking each signal's chance on its own. It is 0 for a turn that was
    /// certain and grows the more unexpected the turn was. A signal the expectation does not list is left out.
    /// </summary>
    public static double Surprise(Expectation expected, IEnumerable<string> signalIds)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(signalIds);

        var reported = signalIds.ToHashSet(StringComparer.Ordinal);
        return -expected.Signals.Sum(signal => Math.Log(reported.Contains(signal.SignalId) ? signal.Chance : 1 - signal.Chance));
    }

    /// <summary>
    /// The surprise a turn has on average under the expectation, so a turn's own <see cref="Surprise"/> can be read
    /// against it: a turn at or below it came as expected. For each signal with chance p it adds
    /// minus p times the natural log of p, minus 1 minus p times the natural log of 1 minus p. A signal with a chance
    /// of 0 or 1 adds 0.
    /// </summary>
    public static double UsualSurprise(Expectation expected)
    {
        ArgumentNullException.ThrowIfNull(expected);

        return expected.Signals.Sum(signal => signal.Chance <= 0 || signal.Chance >= 1
            ? 0
            : -(signal.Chance * Math.Log(signal.Chance) + (1 - signal.Chance) * Math.Log(1 - signal.Chance)));
    }
}
