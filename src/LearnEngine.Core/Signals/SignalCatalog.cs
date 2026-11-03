using System.Collections.Frozen;
using System.Collections.Immutable;
using OneOf;

namespace LearnEngine.Core.Signals;

/// <summary>
/// Every signal the app registered, built once and looked up by id.
/// </summary>
public sealed class SignalCatalog
{
    private readonly FrozenDictionary<string, Signal> byId;

    /// <summary>
    /// Every signal in the order the app registered them.
    /// </summary>
    public ImmutableArray<Signal> All { get; }

    private SignalCatalog(FrozenDictionary<string, Signal> byId, ImmutableArray<Signal> all)
    {
        this.byId = byId;
        All = all;
    }

    // Internal so a catalog only comes from registration, which has already checked the ids are filled and unique.
    internal static SignalCatalog Create(IReadOnlyList<Signal> signals)
    {
        var byId = signals.ToFrozenDictionary(signal => signal.Id, StringComparer.Ordinal);
        return new(byId, [.. signals]);
    }

    /// <summary>
    /// Finds the signal with the given id, or says the id is unknown.
    /// </summary>
    public OneOf<Signal, UnknownSignal> Find(string id)
    {
        var signal = byId.GetValueOrDefault(id);
        if (signal is null)
            return UnknownSignal.Create(id);

        return signal;
    }
}
