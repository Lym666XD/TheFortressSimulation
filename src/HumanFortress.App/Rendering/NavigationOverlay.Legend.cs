using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal sealed partial class NavigationOverlay
{
    private void RenderLegend(ScreenSurface surface, SimulationNavigationOverlayData overlay)
    {
        int legendY = surface.Surface.Height - 5, legendX = 2;
        surface.Surface.Print(legendX, legendY, $"Navigation: {_currentMode}", Color.White);
        if (_currentMode == OverlayMode.PathDisplay && _currentPath.HasResult)
        {
            double totalCost = _currentPath.TotalCost / 10.0;
            surface.Surface.Print(legendX + 24, legendY, $"Path {_currentPath.Kind} len={_currentPath.Length} cost={totalCost:F1}", Color.Gold);
        }

        RenderModeLegend(surface, overlay, legendX, legendY);
        surface.Surface.Print(legendX, legendY + 2, "F9: Mode  |  F10: Set/Path  |  Ctrl+F10: Clear", Color.DarkGray);
    }

    private void RenderModeLegend(ScreenSurface surface, SimulationNavigationOverlayData overlay, int legendX, int legendY)
    {
        switch (_currentMode)
        {
            case OverlayMode.Walkability:
                surface.Surface.Print(legendX, legendY + 1, ". Walk", Color.Green);
                surface.Surface.Print(legendX + 10, legendY + 1, "~ Swim", Color.Blue);
                surface.Surface.Print(legendX + 20, legendY + 1, "o Fly", Color.Gray);
                surface.Surface.Print(legendX + 30, legendY + 1, "X Block", Color.DarkRed);
                break;
            case OverlayMode.MovementCost:
                surface.Surface.Print(legendX, legendY + 1, "0-9,A-Z: FP cost bins (Green=Low, Red=High)", Color.White);
                surface.Surface.Print(legendX, legendY + 2, $"Base={overlay.MovementBaseCost} (tile cost shown as x{10}/Base)", Color.DarkGray);
                break;
            case OverlayMode.PathDisplay:
                surface.Surface.Print(legendX, legendY + 1, "S Start  ./>^\\ Path  G Goal", Color.White);
                break;
            case OverlayMode.RampMask:
                surface.Surface.Print(legendX, legendY + 1, "Ramp ascend: ^ > v < / \\ (*=multi)", Color.White);
                break;
        }
    }
}
