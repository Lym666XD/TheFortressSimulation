using Session = HumanFortress.Runtime.SimulationRuntimeSession<HumanFortress.Runtime.SimulationRuntimeHost<HumanFortress.Runtime.SimulationRuntimeSystems>>;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class FortressRuntimeSessionSnapshotFacade
{
    internal static WorkforceDebugData BuildWorkforceSnapshot(Session? session)
    {
        return FortressRuntimeSnapshotBuilder.BuildWorkforceSnapshot(Host(session), World(session));
    }

    internal static SimulationWorkshopDebugData BuildWorkshopSnapshot(Session? session)
    {
        return FortressRuntimeSnapshotBuilder.BuildWorkshopSnapshot(World(session), Constructions(session));
    }

    internal static WorkshopSummaryView? FindWorkshopSnapshot(Session? session, Guid workshopId)
    {
        return FortressRuntimeSnapshotBuilder.FindWorkshopSnapshot(World(session), Constructions(session), workshopId);
    }

    internal static string? GetDefaultRecipeForWorkshop(Session? session, string? workshopId)
    {
        return FortressRuntimeSnapshotBuilder.GetDefaultRecipeForWorkshop(Recipes(session), workshopId);
    }
}
