namespace LearnEngine.Core.States;

/// <summary>
/// A state a learner can be in, such as overloaded or following, that the odds engine keeps odds on.
/// </summary>
public sealed record State
{
    /// <summary>
    /// The id of the state, such as "overloaded".
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// What the state means, written for the AI so it knows what the learner is going through.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// The odds that a learner is in this state before anything is known about them, above 0 and at most 1.
    /// The priors of every state add up to 1.
    /// </summary>
    public double Prior { get; }

    private State(string id, string description, double prior)
    {
        Id = id;
        Description = description;
        Prior = prior;
    }

    /// <summary>
    /// Creates a state with the given id, description and prior.
    /// </summary>
    public static State Create(string id, string description, double prior) => new(id, description, prior);
}
