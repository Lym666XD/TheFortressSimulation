using HumanFortress.Contracts.Navigation;
using HumanFortress.Core.Simulation;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Craft;

internal sealed class CraftActiveJobRunner
{
    private readonly WorldModel _world;
    private readonly IPathService _paths;
    private readonly IWorldNavigationView _navView;
    private readonly IMovementExecutor _move;
    private readonly CraftMaterialConsumer _materialConsumer;
    private readonly CraftOutputEmitter _outputEmitter;
    private readonly string _systemId;
    private readonly int _creatureReserveTtlTicks;

    internal CraftActiveJobRunner(
        WorldModel world,
        IPathService paths,
        IWorldNavigationView navView,
        IMovementExecutor move,
        CraftMaterialConsumer materialConsumer,
        CraftOutputEmitter outputEmitter,
        string systemId,
        int creatureReserveTtlTicks)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _navView = navView ?? throw new ArgumentNullException(nameof(navView));
        _move = move ?? throw new ArgumentNullException(nameof(move));
        _materialConsumer = materialConsumer ?? throw new ArgumentNullException(nameof(materialConsumer));
        _outputEmitter = outputEmitter ?? throw new ArgumentNullException(nameof(outputEmitter));
        _systemId = systemId ?? throw new ArgumentNullException(nameof(systemId));
        _creatureReserveTtlTicks = creatureReserveTtlTicks;
    }

    internal bool Run(ActiveCraftJob job, ulong tick, out CraftJobFinishReason finishReason)
    {
        finishReason = default;

        var worker = _world.Creatures.GetInstance(job.WorkerId);
        if (worker == null)
        {
            finishReason = CraftJobFinishReason.WorkerMissing;
            return true;
        }

        ulong entityKey = DiffTargetEncoding.EntityKey(job.WorkerId);
        _world.Reservations.TryReserveCreature(job.WorkerId, _systemId, tick, tick + (ulong)_creatureReserveTtlTicks, jobId: $"craft:{job.RecipeId}");

        if (job.Stage == CraftJobStage.ToWorkshop)
        {
            var update = _move.UpdateMovement(entityKey, _navView);
            if (update.Status == MovementStatus.Arrived)
            {
                if (!_materialConsumer.TryConsumeInputs(job))
                {
                    finishReason = CraftJobFinishReason.InputsUnavailable;
                    return true;
                }

                job.Stage = CraftJobStage.Working;
            }
            else if (update.NeedsReplan)
            {
                Replan(job, entityKey, update.Position);
            }

            return false;
        }

        if (job.Stage == CraftJobStage.Working)
        {
            job.WorkTicksRemaining = Math.Max(0, job.WorkTicksRemaining - 1);
            if (job.WorkTicksRemaining <= 0)
            {
                _outputEmitter.EmitOutputs(job);
                finishReason = CraftJobFinishReason.Completed;
                return true;
            }
        }

        return false;
    }

    private void Replan(ActiveCraftJob job, ulong entityKey, Point3 currentPosition)
    {
        var request = new PathRequest(
            currentPosition,
            new Point3(job.Anchor.X, job.Anchor.Y, job.Z),
            MoveMode.Walk,
            PathFlags.None,
            CraftPathSeed.From(job.WorkerId, job.WorkshopGuid));
        var path = _paths.Solve(in request, in _navView);
        if (path.Kind == PathResultKind.Found)
        {
            _move.BeginMovement(entityKey, request, path);
        }
    }
}
