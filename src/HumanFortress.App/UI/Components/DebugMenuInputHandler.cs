using HumanFortress.Contracts.Runtime.Snapshots;
using SadRogue.Primitives;

namespace HumanFortress.App.UI.Components;

internal sealed partial class DebugMenuInputHandler
{
    private readonly UIStateManager _uiStateManager;
    private readonly int _screenWidth;
    private readonly int _screenHeight;
    private readonly Func<SimulationDebugMenuData> _debugMenuProvider;
    private readonly Action<string, ulong> _addToast;

    public DebugMenuInputHandler(
        UIStateManager uiStateManager,
        int screenWidth,
        int screenHeight,
        Func<SimulationDebugMenuData> debugMenuProvider,
        Action<string, ulong> addToast)
    {
        _uiStateManager = uiStateManager ?? throw new ArgumentNullException(nameof(uiStateManager));
        _screenWidth = screenWidth;
        _screenHeight = screenHeight;
        _debugMenuProvider = debugMenuProvider ?? throw new ArgumentNullException(nameof(debugMenuProvider));
        _addToast = addToast ?? throw new ArgumentNullException(nameof(addToast));
    }

    public bool TryHandleClick(Point localPos)
    {
        var ui = _uiStateManager.Store;
        var debugMenu = _debugMenuProvider();
        DebugSelectionPolicy.EnsureValidSelections(ui, debugMenu);
        if (!ui.DebugOpen) return false;

        var win = DebugLayoutCalculator.CalculateWindow(_screenWidth, _screenHeight);
        if (!win.Contains(localPos)) return false;

        if (TryHandleTabClick(ui, win, localPos))
            return true;

        if (ui.DebugMenuTab == 1)
            return HandleCreatureTabClick(ui, debugMenu, win, localPos);

        if (ui.DebugMenuTab != 2)
            return true;

        if (TryHandleItemCategoryClick(ui, debugMenu, win, localPos))
            return true;

        if (TryHandleItemPageClick(ui, debugMenu, win, localPos))
            return true;

        if (TryHandleItemRowClick(ui, debugMenu, win, localPos))
            return true;

        return true;
    }

    public static SimulationDebugMenuData CreateEmptyDebugMenuData()
    {
        return new SimulationDebugMenuData(
            new DebugWorldStatusView(false, 0, 0, 0, 0, 0),
            Array.Empty<DebugItemCategoryView>());
    }

    private bool TryHandleTabClick(UiStore ui, Rectangle win, Point localPos)
    {
        var tabHits = DebugLayoutCalculator.CalculateTabs(win);
        for (int i = 0; i < tabHits.Length; i++)
        {
            if (!tabHits[i].Contains(localPos))
                continue;

            ui.DebugMenuTab = i;
            _addToast($"Tab: {(i == 0 ? "Status" : i == 1 ? "Creatures" : "Items")}", 50);
            return true;
        }

        return false;
    }
}
