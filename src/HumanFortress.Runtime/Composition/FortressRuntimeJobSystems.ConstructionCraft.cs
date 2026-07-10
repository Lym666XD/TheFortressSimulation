using HumanFortress.Navigation.Implementation;
using HumanFortress.Runtime.Navigation;
using HumanFortress.Runtime.Jobs;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Stockpile;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Composition;

internal sealed partial class FortressRuntimeJobSystems
{
    private static ConstructionJobSystem CreateConstruction(
        World world,
        HumanFortress.Core.Simulation.DiffLog diffLog,
        ItemsDiffLog itemsDiffLog,
        StockpileDiffLog stockpileDiffLog,
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
                : workshopCompletion.Notify,
            stockpileDiffLog: stockpileDiffLog);
    }

    private static CraftJobSystem CreateCraft(
        World world,
        ItemsDiffLog itemsDiffLog,
        StockpileDiffLog stockpileDiffLog,
        NavigationManager navigation,
        RuntimeNavigationServices navigationServices,
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
            dependencies.NavigationTuning,
            navigationServices,
            stockpileDiffLog);
    }
}
