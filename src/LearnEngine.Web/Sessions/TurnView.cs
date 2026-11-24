using System.Collections.Immutable;
using LearnEngine.Core.History;
using LearnEngine.Core.Odds;
using LearnEngine.Core.Prediction;

namespace LearnEngine.Web.Sessions;

/// <summary>
/// One student message of a session as its history kept it: the signals, the topic and the move made before it, the
/// odds after it, how expected it was and the move picked after it.
/// </summary>
public sealed class TurnView
{
    /// <summary>
    /// The number of the turn in the session, counted from 1.
    /// </summary>
    public int Number { get; }

    /// <summary>
    /// The signals the AI reported, in the order it reported them.
    /// </summary>
    public ImmutableArray<string> SignalIds { get; }

    /// <summary>
    /// The topic of the turn, or null when the AI sent none.
    /// </summary>
    public string? Topic { get; }

    /// <summary>
    /// The move the AI made in the reply before the turn, or null when it made none.
    /// </summary>
    public string? MoveMade { get; }

    /// <summary>
    /// The odds of every state after the turn, in the order the app registered the states, or empty when the history
    /// did not keep them, as when a report stopped before it.
    /// </summary>
    public ImmutableArray<StateOdds> Odds { get; }

    /// <summary>
    /// The state with the highest odds after the turn, or null when the odds were not kept.
    /// </summary>
    public StateOdds? Leader { get; }

    /// <summary>
    /// How far the leader is ahead of the runner up after the turn.
    /// </summary>
    public double Margin { get; }

    /// <summary>
    /// True when the leader is ahead by at least the session's clear margin.
    /// </summary>
    public bool IsClear { get; }

    /// <summary>
    /// The move picked after the turn, or null when none was.
    /// </summary>
    public MovePicked? Pick { get; }

    /// <summary>
    /// What the engine expected of this turn before it came, or null when the history kept nothing.
    /// </summary>
    public Expectation? Expected { get; }

    /// <summary>
    /// How unexpected the turn's signals were under <see cref="Expected"/>, or null when nothing was expected.
    /// </summary>
    public double? Surprise { get; }

    /// <summary>
    /// The surprise a turn has on average under <see cref="Expected"/>, or null when nothing was expected.
    /// </summary>
    public double? UsualSurprise { get; }

    /// <summary>
    /// The chance <see cref="Expected"/> gave the turn's topic, 0 when it did not list it, or null when the turn has no
    /// topic or nothing was expected.
    /// </summary>
    public double? TopicChance { get; }

    private TurnView(
        int number,
        LearnerTurn turn,
        ImmutableArray<StateOdds> odds,
        StateOdds? leader,
        double margin,
        bool isClear,
        MovePicked? pick,
        Expectation? expected,
        double? surprise,
        double? usualSurprise,
        double? topicChance)
    {
        Number = number;
        SignalIds = turn.SignalIds;
        Topic = turn.Topic;
        MoveMade = turn.MoveMade;
        Odds = odds;
        Leader = leader;
        Margin = margin;
        IsClear = isClear;
        Pick = pick;
        Expected = expected;
        Surprise = surprise;
        UsualSurprise = usualSurprise;
        TopicChance = topicChance;
    }

    /// <summary>
    /// Creates the view of the given turn, with what was kept after it and what was expected of it before it came.
    /// </summary>
    public static TurnView Create(int number, LearnerTurn turn, TurnLearned? learned, MovePicked? pick, Expectation? expected, double clearMargin)
    {
        ArgumentNullException.ThrowIfNull(turn);

        ImmutableArray<StateOdds> odds = learned is null ? [] : learned.Odds;

        // OrderByDescending keeps the registration order of equal odds, as the engine's own leader does.
        var ranked = odds.OrderByDescending(stateOdds => stateOdds.Odds).ToList();
        var leader = ranked.FirstOrDefault();
        var margin = ranked.Count switch
        {
            0 => 0,
            1 => ranked[0].Odds,
            _ => ranked[0].Odds - ranked[1].Odds,
        };

        double? topicChance = null;
        if (turn.Topic is not null && expected is not null)
            topicChance = expected.Topics.FirstOrDefault(chance => chance.Topic == turn.Topic)?.Chance ?? 0;

        return new(
            number,
            turn,
            odds,
            leader,
            margin,
            leader is not null && margin >= clearMargin,
            pick,
            expected,
            expected is null ? null : Predictor.Surprise(expected, turn.SignalIds),
            expected is null ? null : Predictor.UsualSurprise(expected),
            topicChance);
    }
}
