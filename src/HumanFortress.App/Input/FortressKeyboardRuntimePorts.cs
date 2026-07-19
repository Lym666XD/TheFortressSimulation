using HumanFortress.App.Runtime;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.Input;

internal sealed class FortressKeyboardRuntimePorts
{
    internal FortressKeyboardRuntimePorts(
        IFortressRuntimeBuildCatalogAccess buildCatalog,
        IFortressRuntimeZoneCatalogAccess zoneCatalog,
        IFortressRuntimeWorkshopPanelQueryAccess workshopQueries,
        IFortressRuntimeWorkshopPanelCommandAccess workshopCommands,
        IFortressRuntimeSimulationControlAccess simulationControl,
        IFortressRuntimeUiInputAccess uiInput)
    {
        BuildCatalog = new FortressBuildCatalogRuntimePorts(buildCatalog);
        ZoneCatalog = new FortressZoneCatalogRuntimePorts(zoneCatalog);
        WorkshopPanel = new FortressWorkshopPanelRuntimePorts(workshopQueries, workshopCommands);
        SimulationControl = new FortressSimulationControlRuntimePorts(simulationControl);
        UiInput = new FortressUiInputRuntimePorts(uiInput);
    }

    internal FortressBuildCatalogRuntimePorts BuildCatalog { get; }

    internal FortressZoneCatalogRuntimePorts ZoneCatalog { get; }

    internal FortressWorkshopPanelRuntimePorts WorkshopPanel { get; }

    internal FortressSimulationControlRuntimePorts SimulationControl { get; }

    internal FortressUiInputRuntimePorts UiInput { get; }
}

internal sealed class FortressUiInputRuntimePorts
{
    private readonly IFortressRuntimeUiInputAccess _runtime;

    internal FortressUiInputRuntimePorts(IFortressRuntimeUiInputAccess runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    internal SimulationDebugMenuData GetDebugMenuData() =>
        _runtime.GetDebugMenuData();
}

internal sealed class FortressZoneCatalogRuntimePorts
{
    private readonly IFortressRuntimeZoneCatalogAccess _runtime;

    internal FortressZoneCatalogRuntimePorts(IFortressRuntimeZoneCatalogAccess runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    internal SimulationZoneCatalogData GetZoneCatalogData() =>
        _runtime.GetZoneCatalogData();
}

internal sealed class FortressBuildCatalogRuntimePorts
{
    private readonly IFortressRuntimeBuildCatalogAccess _runtime;

    internal FortressBuildCatalogRuntimePorts(IFortressRuntimeBuildCatalogAccess runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    internal SimulationBuildCatalogData GetBuildCatalogData() =>
        _runtime.GetBuildCatalogData();
}

internal sealed class FortressWorkshopPanelRuntimePorts
{
    private readonly IFortressRuntimeWorkshopPanelQueryAccess _queries;
    private readonly IFortressRuntimeWorkshopPanelCommandAccess _commands;

    internal FortressWorkshopPanelRuntimePorts(
        IFortressRuntimeWorkshopPanelQueryAccess queries,
        IFortressRuntimeWorkshopPanelCommandAccess commands)
    {
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
    }

    internal WorkshopSummaryView? GetWorkshopPanelData(Guid workshopId) =>
        _queries.GetWorkshopPanelData(workshopId);

    internal string? GetDefaultRecipeForWorkshop(string? workshopId) =>
        _queries.GetDefaultRecipeForWorkshop(workshopId);

    internal void QueueAddWorkshopRecipe(Guid workshopId, string recipeId) =>
        _commands.QueueAddWorkshopRecipe(workshopId, recipeId);

    internal void QueueRemoveWorkshopQueueEntry(Guid workshopId, Guid entryId) =>
        _commands.QueueRemoveWorkshopQueueEntry(workshopId, entryId);

    internal void QueueMoveWorkshopQueueEntry(Guid workshopId, Guid entryId, int moveOffset) =>
        _commands.QueueMoveWorkshopQueueEntry(workshopId, entryId, moveOffset);

    internal void QueueSetWorkshopWorkerSlots(Guid workshopId, int workerSlots) =>
        _commands.QueueSetWorkshopWorkerSlots(workshopId, workerSlots);

    internal void QueueToggleWorkshopAutoSupply(Guid workshopId) =>
        _commands.QueueToggleWorkshopAutoSupply(workshopId);

    internal void QueueToggleWorkshopAutoStockpile(Guid workshopId) =>
        _commands.QueueToggleWorkshopAutoStockpile(workshopId);
}

internal sealed class FortressSimulationControlRuntimePorts
{
    private readonly IFortressRuntimeSimulationControlAccess _runtime;

    internal FortressSimulationControlRuntimePorts(IFortressRuntimeSimulationControlAccess runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    internal SimulationStatus ToggleSimulationPause() =>
        _runtime.ToggleSimulationPause();

    internal SimulationStatus CycleSimulationSpeedDown() =>
        _runtime.CycleSimulationSpeedDown();

    internal SimulationStatus CycleSimulationSpeedUp() =>
        _runtime.CycleSimulationSpeedUp();
}
