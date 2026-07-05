using HumanFortress.App.Runtime;

namespace HumanFortress.App.Input;

internal readonly record struct FortressInputRuntimePortDependencies(
    IFortressRuntimeBuildCatalogAccess BuildCatalog,
    IFortressRuntimeWorkshopPanelQueryAccess WorkshopQueries,
    IFortressRuntimeWorkshopPanelCommandAccess WorkshopCommands,
    IFortressRuntimeNavigationDebugAccess NavigationDebug,
    IFortressRuntimeSimulationControlAccess SimulationControl,
    IFortressRuntimePlacementQueryAccess PlacementQueries,
    IFortressRuntimePlacementCommandAccess PlacementCommands,
    IFortressRuntimeDebugSpawnQueryAccess DebugSpawnQueries,
    IFortressRuntimeDebugSpawnCommandAccess DebugSpawnCommands,
    IFortressRuntimeMapInspectionAccess MapInspection);
