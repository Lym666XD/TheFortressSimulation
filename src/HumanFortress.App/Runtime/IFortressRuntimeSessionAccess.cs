namespace HumanFortress.App.Runtime;

internal interface IFortressRuntimeSessionAccess :
    IFortressRuntimeReadAccess,
    IFortressRuntimeMapInspectionAccess,
    IFortressRuntimeDebugSpawnAccess,
    IFortressRuntimeBuildCatalogAccess,
    IFortressRuntimeWorkshopPanelAccess,
    IFortressRuntimeNavigationDebugAccess,
    IFortressRuntimeSimulationControlAccess,
    IFortressRuntimeUiInputAccess,
    IFortressRuntimePlacementAccess,
    IFortressRuntimeBootstrapAccess,
    IFortressRuntimeSaveAccess
{
}
