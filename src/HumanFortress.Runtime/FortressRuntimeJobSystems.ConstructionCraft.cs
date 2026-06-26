using HumanFortress.Navigation;
using HumanFortress.Runtime.Jobs;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeJobSystems
{
    private static ConstructionJobSystem CreateConstruction(
        World world,
        HumanFortress.Core.Simulation.DiffLog diffLog,
        ItemsDiffLog itemsDiffLog,
        FortressRuntimeDependencies dependencies,
        FortressRuntimePlanningSystems planners,
        FortressRuntimeLogging logging)
    {
        var schedulerTunings = dependencies.SchedulerTunings;
        var workshopCompletion = logging.WorkshopCompletion;

        return new ConstructionJobSystem(
            world,
            planners.Construction,
            diffLog,
            itemsDiffLog,
            dependencies.Constructions,
            dependencies.ConstructionTuning,
            dependencies.PlaceableTuning,
            maxPerTick: schedulerTunings.Construction.PlanPerTick,
            log: logging.Log,
            workshopCompletion: workshopCompletion == null
                ? null
                : workshopCompletion.Notify);
    }

    private static CraftJobSystem CreateCraft(
        World world,
        ItemsDiffLog itemsDiffLog,
        NavigationManager navigation,
        FortressRuntimeDependencies dependencies,
        FortressRuntimePlanningSystems planners)
    {
        return new CraftJobSystem(
            world,
            planners.Craft,
            dependencies.CraftRecipes,
            dependencies.Constructions,
            itemsDiffLog,
            navigation,
            dependencies.ProfessionAssignments,
            dependencies.SchedulerTunings.WorkerSelection,
            dependencies.NavigationTuning);
    }
}
