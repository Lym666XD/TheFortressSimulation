using HumanFortress.App.UI.Commands;
using SadRogue.Primitives;

namespace HumanFortress.App.UI.Components;

internal sealed partial class DrawerMouseInputHandler
{
    private readonly UIStateManager _uiStateManager;
    private readonly int _screenWidth;
    private readonly int _screenHeight;
    private readonly Action<string, ulong> _addToast;

    public DrawerMouseInputHandler(
        UIStateManager uiStateManager,
        int screenWidth,
        int screenHeight,
        Action<string, ulong> addToast)
    {
        _uiStateManager = uiStateManager ?? throw new ArgumentNullException(nameof(uiStateManager));
        _screenWidth = screenWidth;
        _screenHeight = screenHeight;
        _addToast = addToast ?? throw new ArgumentNullException(nameof(addToast));
    }

    public bool HandleLeftClick(Point localPos)
    {
        return TryHandleDockClick(localPos)
            || TryHandleQuickClick(localPos)
            || TryHandleDrawerTabClick(localPos)
            || TryHandleStockItemFilterClick(localPos);
    }

    private int CalculateDrawerTopY()
    {
        int drawerHeight = Math.Max(10, _screenHeight - 7);
        return _screenHeight - 1 - drawerHeight;
    }
}
