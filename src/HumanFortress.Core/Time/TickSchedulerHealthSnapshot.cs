using System.Collections.ObjectModel;

namespace HumanFortress.Core.Time;

/// <summary>
/// Immutable failure history for the currently configured scheduler pipeline.
/// </summary>
public sealed class TickSchedulerHealthSnapshot
{
    private readonly ReadOnlyCollection<TickSchedulerSystemFailureSnapshot> _systemsWithFailures;

    internal TickSchedulerHealthSnapshot(
        long systemFailureCountTotal,
        IEnumerable<TickSchedulerSystemFailureSnapshot> systemsWithFailures)
    {
        SystemFailureCountTotal = systemFailureCountTotal;
        _systemsWithFailures = Array.AsReadOnly(systemsWithFailures.ToArray());
    }

    public long SystemFailureCountTotal { get; }

    public bool HasAnySystemFailure => SystemFailureCountTotal > 0;

    public int FailingSystemCountCurrent =>
        _systemsWithFailures.Count(static system => system.ConsecutiveFailureCountCurrent > 0);

    public int QuarantinedSystemCountCurrent =>
        _systemsWithFailures.Count(static system => system.IsQuarantinedCurrent);

    public IReadOnlyList<TickSchedulerSystemFailureSnapshot> SystemsWithFailures => _systemsWithFailures;
}

/// <summary>
/// Historical and current failure state for one scheduler system.
/// </summary>
public readonly record struct TickSchedulerSystemFailureSnapshot(
    string SystemId,
    long FailureCountTotal,
    int ConsecutiveFailureCountCurrent,
    bool IsQuarantinedCurrent);
