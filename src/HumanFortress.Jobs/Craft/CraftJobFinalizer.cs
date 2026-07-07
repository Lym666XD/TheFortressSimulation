using HumanFortress.Simulation.Placeables;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Craft;

internal sealed class CraftJobFinalizer
{
    private readonly WorldModel _world;
    private readonly CraftWorkshopLocator _workshops;

    internal CraftJobFinalizer(WorldModel world, CraftWorkshopLocator workshops)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _workshops = workshops ?? throw new ArgumentNullException(nameof(workshops));
    }

    internal void Finish(ActiveCraftJob job, CraftJobFinishReason reason)
    {
        _world.Reservations.ReleaseCreature(job.WorkerId);

        if (!_workshops.TryFind(job.WorkshopGuid, out _, out var state) || state == null)
        {
            return;
        }

        state.RegisterJobComplete();
        var entry = state.GetEntry(job.QueueEntryId);
        if (entry == null)
        {
            return;
        }

        entry.ActiveWorkerId = null;
        entry.IsScheduled = false;

        if (reason == CraftJobFinishReason.Completed)
        {
            state.RemoveEntry(entry.EntryId);
            return;
        }

        if (reason == CraftJobFinishReason.WorkerMissing)
        {
            entry.Status = CraftQueueStatus.Pending;
            entry.BlockingReason = null;
        }
    }
}
