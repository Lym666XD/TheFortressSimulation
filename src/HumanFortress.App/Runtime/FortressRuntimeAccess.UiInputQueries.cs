using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.Runtime;

internal sealed partial class FortressRuntimeAccess
{
    internal SimulationDebugMenuData GetDebugMenuData()
    {
        return TryGetCommittedFrame(out var committed)
            && committed.Frame.UiOverlay.DebugMenu is { } debugMenu
                ? debugMenu
                : new SimulationDebugMenuData(
                    new DebugWorldStatusView(false, 0, 0, 0, 0, 0),
                    Array.Empty<DebugItemCategoryView>());
    }

    internal WorkforceDebugData GetWorkforceInputData()
    {
        return TryGetCommittedFrame(out var committed)
            && committed.Frame.UiOverlay.WorkDrawer is { } workDrawer
                ? workDrawer.Workforce
                : SimulationWorkDrawerData.Empty.Workforce;
    }

    SimulationDebugMenuData IFortressRuntimeUiInputAccess.GetDebugMenuData() => GetDebugMenuData();

    WorkforceDebugData IFortressRuntimeUiInputAccess.GetWorkforceInputData() => GetWorkforceInputData();
}
