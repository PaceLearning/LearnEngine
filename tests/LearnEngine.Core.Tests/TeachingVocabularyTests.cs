using LearnEngine.Core.Bandit;
using LearnEngine.Core.Effects;
using LearnEngine.Core.Learning;
using LearnEngine.Core.Registration;
using LearnEngine.Core.Sessions;
using LearnEngine.Teaching;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LearnEngine.Core.Tests;

/// <summary>The teaching vocabulary LearnEngine ships with, and a learner learning one move's effect under it.</summary>
public sealed class TeachingVocabularyTests
{
    [Fact]
    public void AddTeachingVocabulary_EveryAllowedMove_HasAnEffect()
    {
        var services = new ServiceCollection();
        services.AddTeachingVocabulary().AddLearnEngine();
        using var provider = services.BuildServiceProvider();
        var effects = provider.GetRequiredService<MoveEffectCatalog>();

        var allowed = provider.GetRequiredService<AllowedMoveCatalog>().All;

        Assert.All(allowed, score => Assert.Contains(effects.All, effect => effect.FromStateId == score.StateId && effect.MoveId == score.MoveId));
        Assert.Equal(allowed.Length, effects.All.Length);
        Assert.Equal(["following", "eager"], provider.GetRequiredService<GoalStates>().StateIds.AsEnumerable());
    }

    [Fact]
    public async Task ReportTurn_WorkedExampleKeepsLeadingToFollowing_LearnsAHigherEffectThanTheSharedOne()
    {
        var services = new ServiceCollection();
        services.AddTeachingVocabulary().AddLearnEngine();
        using var provider = services.BuildServiceProvider();
        var sessions = provider.GetRequiredService<SessionEngine>();
        var models = provider.GetRequiredService<ILearnerModelStore>();
        var shared = provider.GetRequiredService<MoveEffectCatalog>().For("stuck", "worked_example");
        await sessions.StartSession("learner", "session", CancellationToken.None);

        // The learner is stuck, the tutor makes a worked example, and the learner follows. Ten times over.
        for (var round = 0; round < 10; round++)
        {
            await sessions.ReportTurn("learner", "session", ["says.dont_get_it", "asks.for_help"], "recursion.base_case", null, CancellationToken.None);
            await sessions.ReportTurn("learner", "session", ["answers.correctly", "says.it_clicked"], "recursion.base_case", "worked_example", CancellationToken.None);
        }

        var model = await models.Load("learner", CancellationToken.None);
        var learnt = model.Effect(shared, "following");

        Assert.Equal(0.55, shared.ChanceOf("following"), 10);
        Assert.True(learnt > shared.ChanceOf("following") + 0.1, $"The learned effect into following is {learnt}.");
    }
}
