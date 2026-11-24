using LearnEngine.Core.Effects;
using LearnEngine.Core.Learning;
using LearnEngine.Core.Likelihoods;
using LearnEngine.Core.Registration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LearnEngine.Core.Tests;

/// <summary>A learner's own rates as counts on top of the shared ones, and the store that keeps them.</summary>
public sealed class LearnerModelTests
{
    private const int Precision = 10;

    [Fact]
    public void Rate_CountsOnTopOfTheSharedRate_MovesItAsInThePlan()
    {
        // The plan's example: shared 0.20 counted as 20 messages, and this learner while stuck shown 1.2, not shown 9.8.
        var shared = Likelihood.Create("says.dont_get_it", "stuck", 0.20);
        var model = LearnerModel.Create([RateCount.Create("says.dont_get_it", "stuck", 1.2, 9.8)], []);

        var rate = model.Rate(shared, 20);

        Assert.Equal((0.20 * 20 + 1.2) / (20 + 1.2 + 9.8), rate, Precision);
        Assert.Equal(0.17, rate, 2);
    }

    [Fact]
    public void Rate_NoCounts_IsTheSharedRate()
    {
        var shared = Likelihood.Create("says.dont_get_it", "stuck", 0.20);

        Assert.Equal(0.20, LearnerModel.Empty.Rate(shared, 20), Precision);
    }

    [Fact]
    public void Add_SameSignalAndState_SumsTheCounts()
    {
        var model = LearnerModel.Empty
            .Add(LearnerModelChange.Create([RateCount.Create("a", "x", 0.5, 0), RateCount.Create("a", "y", 0, 0.5)], []))
            .Add(LearnerModelChange.Create([RateCount.Create("a", "x", 0.25, 0.75)], []));

        var x = Assert.Single(model.Rates, count => count.StateId == "x");
        Assert.Equal(0.75, x.Shown, Precision);
        Assert.Equal(0.75, x.NotShown, Precision);
        Assert.Equal(2, model.Rates.Length);
    }

    [Fact]
    public void Effect_CountsOnTopOfTheSharedEffect_MovesItAsInItsExample()
    {
        // The example of LearnerModel.Effect: shared 0.70 with a weight of 10, and 8 steps of which 6 went to the first state.
        using var provider = TestVocabulary.Create(after: services => services
            .AddMoveEffect(TestVocabulary.Second, TestVocabulary.One, weight: 10, (TestVocabulary.First, 0.70), (TestVocabulary.Second, 0.30)));
        var shared = provider.GetRequiredService<MoveEffectCatalog>().For(TestVocabulary.Second, TestVocabulary.One);
        var model = LearnerModel.Create([], [
            EffectCount.Create(TestVocabulary.Second, TestVocabulary.One, TestVocabulary.First, 6),
            EffectCount.Create(TestVocabulary.Second, TestVocabulary.One, TestVocabulary.Second, 2),
            EffectCount.Create(TestVocabulary.Second, null, TestVocabulary.First, 5),
            EffectCount.Create(TestVocabulary.Second, TestVocabulary.Two, TestVocabulary.First, 5)]);

        var first = model.Effect(shared, TestVocabulary.First);
        var second = model.Effect(shared, TestVocabulary.Second);

        Assert.Equal((0.70 * 10 + 6) / (10 + 8), first, Precision);
        Assert.Equal((0.30 * 10 + 2) / (10 + 8), second, Precision);
        Assert.Equal(1, first + second, Precision);
        Assert.Equal(0.70, LearnerModel.Empty.Effect(shared, TestVocabulary.First), Precision);
    }

    [Fact]
    public void Add_SameStep_SumsTheCountsAndKeepsNoMoveApart()
    {
        var model = LearnerModel.Empty
            .Add(LearnerModelChange.Create([], [EffectCount.Create("x", "m", "y", 0.5), EffectCount.Create("x", null, "y", 0.5)]))
            .Add(LearnerModelChange.Create([], [EffectCount.Create("x", "m", "y", 0.25)]));

        Assert.Equal(0.75, Assert.Single(model.Effects, count => count.MoveId == "m").Count, Precision);
        Assert.Equal(0.5, Assert.Single(model.Effects, count => count.MoveId is null).Count, Precision);
    }

    [Theory]
    [InlineData(-0.1, 0)]
    [InlineData(0, double.NaN)]
    [InlineData(double.PositiveInfinity, 0)]
    public void RateCount_CountBelowZeroOrNotFinite_Throws(double shown, double notShown)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RateCount.Create("a", "x", shown, notShown));
    }

    [Fact]
    public async Task InMemoryStore_TwoAdds_BothCount()
    {
        var store = InMemoryLearnerModelStore.Create();

        var empty = await store.Load("learner", CancellationToken.None);
        await store.Add("learner", LearnerModelChange.Create([RateCount.Create("a", "x", 1, 0)], []), CancellationToken.None);
        await store.Add("learner", LearnerModelChange.Create([RateCount.Create("a", "x", 0, 2)], []), CancellationToken.None);
        var model = await store.Load("learner", CancellationToken.None);

        Assert.Empty(empty.Rates);
        var count = Assert.Single(model.Rates);
        Assert.Equal(1, count.Shown, Precision);
        Assert.Equal(2, count.NotShown, Precision);
        Assert.Empty((await store.Load("another", CancellationToken.None)).Rates);
    }
}
