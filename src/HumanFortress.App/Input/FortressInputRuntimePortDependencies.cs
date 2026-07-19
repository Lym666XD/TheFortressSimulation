using HumanFortress.App.Runtime;

namespace HumanFortress.App.Input;

internal readonly record struct FortressInputRuntimePortDependencies(
    IFortressRuntimeBuildCatalogAccess BuildCatalog,
    IFortressRuntimeZoneCatalogAccess ZoneCatalog,
    IFortressRuntimeWorkshopPanelQueryAccess WorkshopQueries,
    IFortressRuntimeWorkshopPanelCommandAccess WorkshopCommands,
    IFortressRuntimeSimulationControlAccess SimulationControl,
    IFortressRuntimeUiInputAccess UiInput,
    IFortressRuntimePlacementQueryAccess PlacementQueries,
    IFortressRuntimePlacementCommandAccess PlacementCommands,
    IFortressRuntimeDebugSpawnQueryAccess DebugSpawnQueries,
    IFortressRuntimeDebugSpawnCommandAccess DebugSpawnCommands,
    IFortressRuntimeMapInspectionAccess MapInspection);
