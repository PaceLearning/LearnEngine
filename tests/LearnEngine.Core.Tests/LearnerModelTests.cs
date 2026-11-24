using LearnEngine.Core.Learning;
using LearnEngine.Core.Likelihoods;
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
        var model = LearnerModel.Create([RateCount.Create("says.dont_get_it", "stuck", 1.2, 9.8)]);

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
            .Add(LearnerModelChange.Create([RateCount.Create("a", "x", 0.5, 0), RateCount.Create("a", "y", 0, 0.5)]))
            .Add(LearnerModelChange.Create([RateCount.Create("a", "x", 0.25, 0.75)]));

        var x = Assert.Single(model.Rates, count => count.StateId == "x");
        Assert.Equal(0.75, x.Shown, Precision);
        Assert.Equal(0.75, x.NotShown, Precision);
        Assert.Equal(2, model.Rates.Length);
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
        await store.Add("learner", LearnerModelChange.Create([RateCount.Create("a", "x", 1, 0)]), CancellationToken.None);
        await store.Add("learner", LearnerModelChange.Create([RateCount.Create("a", "x", 0, 2)]), CancellationToken.None);
        var model = await store.Load("learner", CancellationToken.None);

        Assert.Empty(empty.Rates);
        var count = Assert.Single(model.Rates);
        Assert.Equal(1, count.Shown, Precision);
        Assert.Equal(2, count.NotShown, Precision);
        Assert.Empty((await store.Load("another", CancellationToken.None)).Rates);
    }
}
