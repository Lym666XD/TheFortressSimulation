using HumanFortress.App.Runtime;

namespace HumanFortress.App.States;

internal sealed class FortressStateRuntimePorts
{
    internal FortressStateRuntimePorts(
        IFortressRuntimeReadAccess read,
        IFortressRuntimeUiInputAccess uiInput,
        IFortressRuntimeBootstrapAccess bootstrap,
        IFortressRuntimeBuildCatalogAccess buildCatalog,
        IFortressRuntimeWorkshopPanelAccess workshopPanel,
        IFortressRuntimeNavigationDebugAccess navigationDebug,
        IFortressRuntimeSimulationControlAccess simulationControl,
        IFortressRuntimePlacementAccess placement,
        IFortressRuntimeDebugSpawnAccess debugSpawn,
        IFortressRuntimeMapInspectionAccess mapInspection)
    {
        Read = read ?? throw new ArgumentNullException(nameof(read));
        UiInput = uiInput ?? throw new ArgumentNullException(nameof(uiInput));
        Bootstrap = bootstrap ?? throw new ArgumentNullException(nameof(bootstrap));
        BuildCatalog = buildCatalog ?? throw new ArgumentNullException(nameof(buildCatalog));
        WorkshopPanel = workshopPanel ?? throw new ArgumentNullException(nameof(workshopPanel));
        NavigationDebug = navigationDebug ?? throw new ArgumentNullException(nameof(navigationDebug));
        SimulationControl = simulationControl ?? throw new ArgumentNullException(nameof(simulationControl));
        Placement = placement ?? throw new ArgumentNullException(nameof(placement));
        DebugSpawn = debugSpawn ?? throw new ArgumentNullException(nameof(debugSpawn));
        MapInspection = mapInspection ?? throw new ArgumentNullException(nameof(mapInspection));
    }

    internal IFortressRuntimeReadAccess Read { get; }

    internal IFortressRuntimeUiInputAccess UiInput { get; }

    internal IFortressRuntimeBootstrapAccess Bootstrap { get; }

    internal IFortressRuntimeBuildCatalogAccess BuildCatalog { get; }

    internal IFortressRuntimeWorkshopPanelAccess WorkshopPanel { get; }

    internal IFortressRuntimeNavigationDebugAccess NavigationDebug { get; }

    internal IFortressRuntimeSimulationControlAccess SimulationControl { get; }

    internal IFortressRuntimePlacementAccess Placement { get; }

    internal IFortressRuntimeDebugSpawnAccess DebugSpawn { get; }

    internal IFortressRuntimeMapInspectionAccess MapInspection { get; }

    internal static FortressStateRuntimePorts From(IFortressRuntimeSessionAccess runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        return new FortressStateRuntimePorts(
            runtime,
            runtime,
            runtime,
            runtime,
            runtime,
            runtime,
            runtime,
            runtime,
            runtime,
            runtime);
    }
}
