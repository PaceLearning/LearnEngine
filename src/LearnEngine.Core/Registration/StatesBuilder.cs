using System.Collections.Immutable;
using LearnEngine.Core.States;

namespace LearnEngine.Core.Registration;

/// <summary>
/// Collects every state inside one AddStates call, so the priors can be checked together once the last one is added.
/// </summary>
public sealed class StatesBuilder
{
    private readonly List<State> states = [];

    private StatesBuilder() { }

    internal static StatesBuilder Create() => new();

    internal ImmutableArray<State> States => [.. states];

    /// <summary>
    /// Adds a state the odds engine keeps odds on, with the odds that a learner is in it before anything is known.
    /// An empty id or description, an id added twice, or a prior that is not above 0 and at most 1 throws on this line.
    /// </summary>
    public StatesBuilder AddState(string id, string description, double prior)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        // Written so a NaN prior fails too, which a pair of separate bound checks would let through.
        var inRange = prior > 0 && prior <= 1;
        if (!inRange)
            throw new ArgumentOutOfRangeException(nameof(prior), prior, $"The prior of the state \"{id}\" must be above 0 and at most 1.");

        var taken = states.Any(state => state.Id == id);
        if (taken)
            throw new InvalidOperationException($"The state \"{id}\" is added twice.");

        states.Add(State.Create(id, description, prior));
        return this;
    }
}
