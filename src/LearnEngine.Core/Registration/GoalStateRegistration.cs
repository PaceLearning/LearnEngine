namespace LearnEngine.Core.Registration;

// A goal state kept in the service collection until the goal states are built from all of them.
internal sealed class GoalStateRegistration
{
    public string StateId { get; }

    private GoalStateRegistration(string stateId) => StateId = stateId;

    public static GoalStateRegistration Create(string stateId) => new(stateId);
}
