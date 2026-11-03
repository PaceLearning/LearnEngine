using System.Collections.Frozen;
using System.Collections.Immutable;

namespace LearnEngine.Core.Judging;

/// <summary>
/// Which signals show that a picked move worked, such as "offers.own_answer", and which show that it failed, such as
/// "says.overwhelmed", built once from the app's registrations. A signal is in one list at most.
/// </summary>
public sealed class OutcomeSignals
{
    private readonly FrozenSet<string> success;
    private readonly FrozenSet<string> failure;

    /// <summary>
    /// The ids of the signals that show a move worked, in the order the app registered them.
    /// </summary>
    public ImmutableArray<string> Success { get; }

    /// <summary>
    /// The ids of the signals that show a move failed, in the order the app registered them.
    /// </summary>
    public ImmutableArray<string> Failure { get; }

    private OutcomeSignals(ImmutableArray<string> successIds, ImmutableArray<string> failureIds)
    {
        success = successIds.ToFrozenSet(StringComparer.Ordinal);
        failure = failureIds.ToFrozenSet(StringComparer.Ordinal);
        Success = successIds;
        Failure = failureIds;
    }

    // Internal so the lists only come from registration, which has already checked every id is a signal in one list once.
    internal static OutcomeSignals Create(ImmutableArray<string> successIds, ImmutableArray<string> failureIds) =>
        new(successIds, failureIds);

    /// <summary>
    /// Says whether the signal with the given id shows that a move worked.
    /// </summary>
    public bool IsSuccess(string signalId) => success.Contains(signalId);

    /// <summary>
    /// Says whether the signal with the given id shows that a move failed.
    /// </summary>
    public bool IsFailure(string signalId) => failure.Contains(signalId);
}
