using LearnEngine.Core.Bandit;
using LearnEngine.Core.Registration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static LearnEngine.Core.Tests.TestVocabulary;

namespace LearnEngine.Core.Tests;

/// <summary>The upper confidence bound pick over the moves allowed in a state.</summary>
public sealed class MovePickerTests
{
    private const string Learner = "learner";

    /// <summary>Registers one state with the given moves allowed in it, in the given order, with their starting counts.</summary>
    private static ServiceProvider Picker(params (string MoveId, int Worked, int Tried)[] allowed)
    {
        var services = new ServiceCollection();
        services.AddSignal("any", "Any signal.");
        services.AddStates(states => states
            .AddState("only", "The state every move is allowed in.", 0.7)
            .AddState("empty", "A state with no moves.", 0.3));
        services.AddLikelihoods(rates => rates.AddRates("any", ("only", 0.5), ("empty", 0.5)));
        services.AddOdds().AddHistory();
        foreach (var (moveId, _, _) in allowed)
            services.AddMove(moveId, $"The move {moveId}.");

        foreach (var (moveId, worked, tried) in allowed)
            services.AddMoveFor("only", moveId, worked, tried);

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Pick_UntriedMove_IsPickedWithAnInfiniteBound()
    {
        using var provider = Picker(("good", 9, 10), ("new", 0, 0));
        var picker = provider.GetRequiredService<MovePicker>();

        var picked = await picker.Pick(Learner, "only", CancellationToken.None);

        Assert.True(picked.IsT0);
        Assert.Equal("new", picked.AsT0.Move.Id);
        Assert.Equal(double.PositiveInfinity, picked.AsT0.UpperBound);
    }

    [Fact]
    public async Task Pick_LessTriedMove_WinsOnItsBonus()
    {
        using var provider = Picker(("known", 7, 10), ("rare", 2, 4));
        var picker = provider.GetRequiredService<MovePicker>();

        var picked = await picker.Pick(Learner, "only", CancellationToken.None);

        // 7 of 10 worked against 2 of 4, but the bonus for few tries puts rare ahead.
        var rareBound = 2.0 / 4 + Math.Sqrt(2 * Math.Log(14) / 4);
        var knownBound = 7.0 / 10 + Math.Sqrt(2 * Math.Log(14) / 10);
        Assert.True(rareBound > knownBound);
        Assert.Equal("rare", picked.AsT0.Move.Id);
        Assert.Equal(rareBound, picked.AsT0.UpperBound, 10);
        Assert.Equal(MoveScore.Create("only", "rare", 2, 4), picked.AsT0.Score);
    }

    [Fact]
    public async Task Pick_SameBound_GoesToTheMoveAllowedFirst()
    {
        using var provider = Picker(("second_added", 5, 10), ("first_added", 5, 10));
        var picker = provider.GetRequiredService<MovePicker>();

        var picked = await picker.Pick(Learner, "only", CancellationToken.None);

        Assert.Equal("second_added", picked.AsT0.Move.Id);
    }

    [Fact]
    public async Task Pick_LearnerHasOwnScore_UsesItOverTheStartingScore()
    {
        using var provider = Picker(("one", 5, 10), ("two", 5, 10));
        var picker = provider.GetRequiredService<MovePicker>();
        var scores = provider.GetRequiredService<IScoreStore>();
        await scores.Save(Learner, MoveScore.Create("only", "one", 0, 10), CancellationToken.None);

        var picked = await picker.Pick(Learner, "only", CancellationToken.None);
        var otherLearner = await picker.Pick("someone_else", "only", CancellationToken.None);

        Assert.Equal("two", picked.AsT0.Move.Id);
        Assert.Equal("one", otherLearner.AsT0.Move.Id);
    }

    [Fact]
    public async Task Pick_StateWithNoMoves_GivesNoMovesInState()
    {
        using var provider = Picker(("one", 5, 10));
        var picker = provider.GetRequiredService<MovePicker>();

        var picked = await picker.Pick(Learner, "empty", CancellationToken.None);

        Assert.True(picked.IsT1);
        Assert.Equal("empty", picked.AsT1.StateId);
    }

    [Fact]
    public async Task Pick_UnknownState_GivesUnknownState()
    {
        using var provider = Create();
        var picker = provider.GetRequiredService<MovePicker>();

        var picked = await picker.Pick(Learner, "not_a_state", CancellationToken.None);

        Assert.True(picked.IsT2);
        Assert.Equal("not_a_state", picked.AsT2.Id);
    }
}
