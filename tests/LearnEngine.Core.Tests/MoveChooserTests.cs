using System.Collections.Immutable;
using LearnEngine.Core.Choosing;
using LearnEngine.Core.History;
using LearnEngine.Core.Learning;
using LearnEngine.Core.Odds;
using LearnEngine.Core.Registration;
using LearnEngine.Core.Sessions;
using LearnEngine.Core.States;
using LearnEngine.Teaching;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static LearnEngine.Core.Tests.TestVocabulary;

namespace LearnEngine.Core.Tests;

/// <summary>Choosing the move most likely to bring a learner to a goal state, and why.</summary>
public sealed class MoveChooserTests
{
    private const string Learner = "learner";
    private const string Session = "session";

    [Fact]
    public void Choose_NewLearner_PicksTheMoveWithTheHighestSharedGoalChance()
    {
        using var provider = Create(after: services => services.AddMoveEffect(First, Two, weight: 10, (First, 1)));
        var chooser = provider.GetRequiredService<MoveChooser>();
        var engine = provider.GetRequiredService<OddsEngine>();

        var chosen = chooser.Choose(engine.Start(), LearnerModel.Empty, First);

        // From second, two has no effect of its own and fades: it stays in first only by the fade times its prior.
        var fromSecond = OddsOptions.DefaultFade * FirstPrior;
        var picked = Assert.IsType<PickedMove>(chosen.Value);
        Assert.Equal(Two, picked.Move.Id);
        Assert.Equal(FirstPrior * 1 + SecondPrior * fromSecond, picked.GoalChance, 10);
        Assert.Equal("This learner has not had two in first yet, so the shared guess is that it leads to first 100% of the time.", picked.Why);
    }

    [Fact]
    public void Choose_SameGoalChanceAndEvidence_PicksTheMoveAllowedFirst()
    {
        using var provider = Create();
        var chooser = provider.GetRequiredService<MoveChooser>();
        var engine = provider.GetRequiredService<OddsEngine>();

        var chosen = chooser.Choose(engine.Start(), LearnerModel.Empty, First);
        var again = chooser.Choose(engine.Start(), LearnerModel.Empty, First);

        var picked = Assert.IsType<PickedMove>(chosen.Value);
        Assert.Equal(One, picked.Move.Id);
        Assert.Equal(picked, Assert.IsType<PickedMove>(again.Value));
    }

    [Fact]
    public void Choose_LearnersCounts_SayWhyFromThem()
    {
        using var provider = Create();
        var chooser = provider.GetRequiredService<MoveChooser>();
        var engine = provider.GetRequiredService<OddsEngine>();
        var model = LearnerModel.Create([], [
            EffectCount.Create(First, Two, First, 6),
            EffectCount.Create(First, Two, Second, 2),
            EffectCount.Create(First, One, Second, 8)]);

        var chosen = chooser.Choose(engine.Start(), model, First);

        var picked = Assert.IsType<PickedMove>(chosen.Value);
        Assert.Equal(Two, picked.Move.Id);
        Assert.Equal("For this learner, two led from first to first 6 of 8 times.", picked.Why);
    }

    [Fact]
    public void Weigh_GivesEveryAllowedMoveInOrder_AndChooseTakesTheHighestBound()
    {
        using var provider = Create();
        var chooser = provider.GetRequiredService<MoveChooser>();
        var engine = provider.GetRequiredService<OddsEngine>();
        var model = LearnerModel.Create([], [
            EffectCount.Create(First, Two, First, 6),
            EffectCount.Create(First, Two, Second, 2)]);

        var weighed = Assert.IsType<ImmutableArray<WeighedMove>>(chooser.Weigh(engine.Start(), model, First).Value);
        var chosen = Assert.IsType<PickedMove>(chooser.Choose(engine.Start(), model, First).Value);

        Assert.Equal([One, Two], weighed.Select(move => move.MoveId));
        Assert.Equal([0, 8], weighed.Select(move => move.OwnSteps));
        Assert.All(weighed, move => Assert.Equal(move.OwnSteps + 10, move.Evidence));
        var total = weighed.Sum(move => move.Evidence);
        Assert.All(weighed, move => Assert.Equal(Math.Sqrt(2 * Math.Log(total) / move.Evidence), move.Bonus, 10));
        var best = weighed.MaxBy(move => move.Bound)!;
        Assert.Equal(best.MoveId, chosen.Move.Id);
        Assert.Equal(best.Bound, chosen.Bound);
        Assert.Equal(best.GoalChance, chosen.GoalChance);
    }

    [Fact]
    public void Weigh_StateWithNoMoves_GivesNoMovesInState()
    {
        using var provider = Create();
        var chooser = provider.GetRequiredService<MoveChooser>();
        var engine = provider.GetRequiredService<OddsEngine>();

        var weighed = chooser.Weigh(engine.Start(), LearnerModel.Empty, Second);

        Assert.Equal(Second, Assert.IsType<NoMovesInState>(weighed.Value).StateId);
    }

    [Fact]
    public void Choose_SeenLess_GetsTheLargerBonus()
    {
        using var provider = Create();
        var chooser = provider.GetRequiredService<MoveChooser>();
        var engine = provider.GetRequiredService<OddsEngine>();

        // One has led into first as often as the fade says, but over many more turns, so two gets the larger bonus.
        var stay = 1 - OddsOptions.DefaultFade + OddsOptions.DefaultFade * FirstPrior;
        var model = LearnerModel.Create([], [
            EffectCount.Create(First, One, First, 100 * stay),
            EffectCount.Create(First, One, Second, 100 * (1 - stay))]);

        var chosen = chooser.Choose(engine.Start(), model, First);

        var total = 110 + 10;
        var picked = Assert.IsType<PickedMove>(chosen.Value);
        Assert.Equal(Two, picked.Move.Id);
        Assert.Equal(picked.GoalChance + Math.Sqrt(2 * Math.Log(total) / 10), picked.Bound, 10);
    }

    [Fact]
    public void Choose_StateWithNoMovesOrNotAState_SaysSo()
    {
        using var provider = Create();
        var chooser = provider.GetRequiredService<MoveChooser>();
        var engine = provider.GetRequiredService<OddsEngine>();

        var noMoves = chooser.Choose(engine.Start(), LearnerModel.Empty, Second);
        var unknown = chooser.Choose(engine.Start(), LearnerModel.Empty, "not_a_state");

        Assert.Equal(NoMovesInState.Create(Second), Assert.IsType<NoMovesInState>(noMoves.Value));
        Assert.Equal("not_a_state", Assert.IsType<UnknownState>(unknown.Value).Id);
    }

    [Fact]
    public async Task ReportTurn_SameTurns_GiveTheSamePicks()
    {
        using var first = new ServiceCollection().AddTeachingVocabulary().AddLearnEngine().BuildServiceProvider();
        using var second = new ServiceCollection().AddTeachingVocabulary().AddLearnEngine().BuildServiceProvider();

        var firstPicks = await Simulate(first, 30);
        var secondPicks = await Simulate(second, 30);

        var firstHistory = Assert.IsType<ImmutableArray<HistoryEvent>>((await first.GetRequiredService<IHistoryStore>().Load(Learner, Session, CancellationToken.None)).Value);
        var secondHistory = Assert.IsType<ImmutableArray<HistoryEvent>>((await second.GetRequiredService<IHistoryStore>().Load(Learner, Session, CancellationToken.None)).Value);
        Assert.Equal(firstPicks, secondPicks);
        Assert.Contains(firstHistory, historyEvent => historyEvent is MovePicked);
        Assert.Equal(firstHistory.Select(HistoryEventJson.Write), secondHistory.Select(HistoryEventJson.Write));
    }

    [Fact]
    public async Task ReportTurn_MoveThatKeepsLeadingToAGoal_IsPickedMoreThanAtTheStart()
    {
        using var provider = new ServiceCollection().AddTeachingVocabulary().AddLearnEngine().BuildServiceProvider();

        var picks = await Simulate(provider, 50);

        // The learner only follows after a diagram, which the shared guesses rank below worked_example when stuck.
        var atStart = picks.Take(25).Count(pick => pick == "diagram");
        var atEnd = picks.Skip(25).Count(pick => pick == "diagram");
        Assert.True(atEnd > atStart, $"diagram was picked {atStart} times in the first 25 turns and {atEnd} times in the last 25: {string.Join(", ", picks)}");
    }

    // A learner who follows after a diagram and is stuck after anything else. Each turn makes the move picked on the
    // turn before, and gives the move picked on every turn, or null when none was.
    private static async Task<List<string?>> Simulate(ServiceProvider provider, int turns)
    {
        var sessions = provider.GetRequiredService<SessionEngine>();
        await sessions.StartSession(Learner, Session, CancellationToken.None);

        var picks = new List<string?>();
        string? moveMade = null;
        for (var turn = 0; turn < turns; turn++)
        {
            string[] signals = moveMade == "diagram"
                ? ["answers.correctly", "says.it_clicked"]
                : ["says.dont_get_it", "asks.for_help"];
            var reported = await sessions.ReportTurn(Learner, Session, signals, "recursion.base_case", moveMade, CancellationToken.None);
            moveMade = Assert.IsType<TurnResult>(reported.Value).Pick is PickedMove picked ? picked.Move.Id : null;
            picks.Add(moveMade);
        }

        return picks;
    }
}
