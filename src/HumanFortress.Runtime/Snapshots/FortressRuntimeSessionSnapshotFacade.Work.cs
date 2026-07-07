
using HumanFortress.Runtime.Session;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class FortressRuntimeSessionSnapshotFacade
{
    internal static WorkforceDebugData BuildWorkforceSnapshot(FortressRuntimeSession? session)
    {
        return FortressRuntimeSnapshotBuilder.BuildWorkforceSnapshot(Host(session), World(session));
    }

    internal static SimulationWorkshopDebugData BuildWorkshopSnapshot(FortressRuntimeSession? session)
    {
        return FortressRuntimeSnapshotBuilder.BuildWorkshopSnapshot(World(session), Constructions(session));
    }

    internal static WorkshopSummaryView? FindWorkshopSnapshot(FortressRuntimeSession? session, Guid workshopId)
    {
        return FortressRuntimeSnapshotBuilder.FindWorkshopSnapshot(World(session), Constructions(session), workshopId);
    }

    internal static string? GetDefaultRecipeForWorkshop(FortressRuntimeSession? session, string? workshopId)
    {
        return FortressRuntimeSnapshotBuilder.GetDefaultRecipeForWorkshop(Recipes(session), workshopId);
    }
}
