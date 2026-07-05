
namespace HumanFortress.Runtime.Snapshots;

internal static partial class FortressRuntimeSessionSnapshotFacade
{
    internal static WorkforceDebugData BuildWorkforceSnapshot(HumanFortress.Runtime.Session.SimulationRuntimeSession<HumanFortress.Runtime.Host.SimulationRuntimeHost<HumanFortress.Runtime.Composition.SimulationRuntimeSystems>>? session)
    {
        return FortressRuntimeSnapshotBuilder.BuildWorkforceSnapshot(Host(session), World(session));
    }

    internal static SimulationWorkshopDebugData BuildWorkshopSnapshot(HumanFortress.Runtime.Session.SimulationRuntimeSession<HumanFortress.Runtime.Host.SimulationRuntimeHost<HumanFortress.Runtime.Composition.SimulationRuntimeSystems>>? session)
    {
        return FortressRuntimeSnapshotBuilder.BuildWorkshopSnapshot(World(session), Constructions(session));
    }

    internal static WorkshopSummaryView? FindWorkshopSnapshot(HumanFortress.Runtime.Session.SimulationRuntimeSession<HumanFortress.Runtime.Host.SimulationRuntimeHost<HumanFortress.Runtime.Composition.SimulationRuntimeSystems>>? session, Guid workshopId)
    {
        return FortressRuntimeSnapshotBuilder.FindWorkshopSnapshot(World(session), Constructions(session), workshopId);
    }

    internal static string? GetDefaultRecipeForWorkshop(HumanFortress.Runtime.Session.SimulationRuntimeSession<HumanFortress.Runtime.Host.SimulationRuntimeHost<HumanFortress.Runtime.Composition.SimulationRuntimeSystems>>? session, string? workshopId)
    {
        return FortressRuntimeSnapshotBuilder.GetDefaultRecipeForWorkshop(Recipes(session), workshopId);
    }
}
