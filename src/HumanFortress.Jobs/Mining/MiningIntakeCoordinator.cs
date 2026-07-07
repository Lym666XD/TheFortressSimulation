using HumanFortress.Simulation.Orders;

namespace HumanFortress.Jobs.Mining;

internal sealed class MiningIntakeCoordinator
{
    private readonly MiningSystem _planner;
    private readonly MiningBacklogBuffer _backlog;
    private readonly MiningDeferredStairwellBuffer _deferredStairwells;
    private readonly IMiningJobLogger _logger;
    private readonly int _intakeBudget;

    internal MiningIntakeCoordinator(
        MiningSystem planner,
        MiningBacklogBuffer backlog,
        MiningDeferredStairwellBuffer deferredStairwells,
        IMiningJobLogger? logger,
        int intakeBudget)
    {
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _backlog = backlog ?? throw new ArgumentNullException(nameof(backlog));
        _deferredStairwells = deferredStairwells ?? throw new ArgumentNullException(nameof(deferredStairwells));
        _logger = logger ?? NullMiningJobLogger.Instance;
        _intakeBudget = Math.Max(1, intakeBudget);
    }

    internal int Fill(ulong tick, IList<MiningSystem.PlannedDig> inbox)
    {
        inbox.Clear();
        _planner.DequeuePlannedDigs(_intakeBudget, inbox);
        _backlog.DrainInto(_intakeBudget, inbox);

        int retriedDeferred = _deferredStairwells.RetryInto(tick, _intakeBudget, inbox);
        if (retriedDeferred > 0)
        {
            _logger.Log($"[MINING][{tick}] Retrying {retriedDeferred} deferred stairwell segments");
        }

        return inbox.Count;
    }
}
