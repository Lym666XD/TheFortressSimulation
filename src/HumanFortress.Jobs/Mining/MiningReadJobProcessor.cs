using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Orders;
using SadRogue.Primitives;

namespace HumanFortress.Jobs.Mining;

internal sealed class MiningReadJobProcessor
{
    private readonly MiningSystem _planner;
    private readonly MiningBacklogBuffer _backlog;
    private readonly MiningTileReservationTracker _reservedTiles;
    private readonly MiningAdjacencyFinder _adjacencyFinder;
    private readonly MiningStairwellGate _stairwellGate;
    private readonly IMiningAssignmentHandler _assignmentHandler;
    private readonly IMiningJobLogger _logger;

    public MiningReadJobProcessor(
        MiningSystem planner,
        MiningBacklogBuffer backlog,
        MiningTileReservationTracker reservedTiles,
        MiningAdjacencyFinder adjacencyFinder,
        MiningStairwellGate stairwellGate,
        IMiningAssignmentHandler assignmentHandler,
        IMiningJobLogger? logger)
    {
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _backlog = backlog ?? throw new ArgumentNullException(nameof(backlog));
        _reservedTiles = reservedTiles ?? throw new ArgumentNullException(nameof(reservedTiles));
        _adjacencyFinder = adjacencyFinder ?? throw new ArgumentNullException(nameof(adjacencyFinder));
        _stairwellGate = stairwellGate ?? throw new ArgumentNullException(nameof(stairwellGate));
        _assignmentHandler = assignmentHandler ?? throw new ArgumentNullException(nameof(assignmentHandler));
        _logger = logger ?? NullMiningJobLogger.Instance;
    }

    public void Process(
        in MiningSystem.PlannedDig dig,
        IReadOnlyList<CreatureInstance> creatures,
        HashSet<Guid> busy,
        IList<ActiveMiningJob> active,
        ulong tick)
    {
        if (_planner.IsTileCanceled(dig.Cell.X, dig.Cell.Y, dig.Z))
        {
            _logger.Log($"[MINING][{tick}] Drop canceled target=({dig.Cell.X},{dig.Cell.Y},{dig.Z}) id={dig.DesignationId}");
            return;
        }

        if (!_stairwellGate.ShouldProcess(dig, tick, out bool middleAlreadySatisfied))
        {
            return;
        }

        if (_reservedTiles.Contains(dig.Cell, dig.Z))
        {
            _logger.Log($"[MINING][{tick}] Tile ({dig.Cell.X},{dig.Cell.Y},{dig.Z}) id={dig.DesignationId} already reserved");
            return;
        }

        var adjacent = _adjacencyFinder.FindForAction(dig.Action, dig.Cell.X, dig.Cell.Y, dig.Z);
        if (adjacent == null)
        {
            RequeueIfActive(dig, tick);
            _logger.Log($"[MINING][{tick}] No adjacency for target=({dig.Cell.X},{dig.Cell.Y},{dig.Z}) id={dig.DesignationId}; requeue");
            return;
        }

        var job = _assignmentHandler.TryAssign(dig, new Point(adjacent.Value.X, adjacent.Value.Y), creatures, busy, tick, middleAlreadySatisfied);
        if (job != null)
        {
            active.Add(job);
            return;
        }

        RequeueIfActive(dig, tick);
        _logger.Log($"[MINING][{tick}] No worker for target=({dig.Cell.X},{dig.Cell.Y},{dig.Z}) id={dig.DesignationId}");
    }

    private void RequeueIfActive(in MiningSystem.PlannedDig dig, ulong tick)
    {
        if (!_planner.IsTileCanceled(dig.Cell.X, dig.Cell.Y, dig.Z))
        {
            _backlog.Enqueue(dig, tick);
        }
    }
}
