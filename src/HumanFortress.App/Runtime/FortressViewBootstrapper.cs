using HumanFortress.App.UI;
using HumanFortress.App.UI.Selection;
using HumanFortress.Simulation.World;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal sealed record FortressViewBootstrapContext(
    FortressRuntimeAccess Runtime,
    UiStore Ui,
    int WorldSizeTiles,
    Func<ulong> UiTickProvider,
    Func<World?> WorldProvider,
    Action<Point> OnMapMouseMoved,
    Action<Point> OnMapLeftClicked,
    Action<Point> OnOverlayLeftClicked,
    Action<Point> OnOverlayRightClicked,
    Action<Point> OnOverlayMouseMoved,
    Action Redraw);

internal sealed record FortressViewBootstrapResult(
    MapScreenSurface MapSurface,
    UiOverlaySurface UiSurface,
    ISelectionTool SelectionTool);

internal static class FortressViewBootstrapper
{
    public static FortressViewBootstrapResult Create(ScreenObject owner, GameHost gameHost, FortressViewBootstrapContext context)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(gameHost);
        ArgumentNullException.ThrowIfNull(context);

        var layout = FortressScreenLayoutFactory.Create(gameHost);
        var interaction = FortressInteractionBootstrapper.Configure(
            layout,
            context.Runtime,
            context.Ui,
            context.WorldSizeTiles,
            context.UiTickProvider,
            context.WorldProvider,
            context.OnMapMouseMoved,
            context.OnMapLeftClicked,
            context.OnOverlayLeftClicked,
            context.OnOverlayRightClicked,
            context.OnOverlayMouseMoved,
            context.Redraw);

        owner.Children.Add(layout.RootSurface);
        owner.IsFocused = true;
        owner.UseKeyboard = true;
        owner.UseMouse = true;
        Logger.Log("[FortressState] UI hierarchy established");

        return new FortressViewBootstrapResult(layout.MapSurface, layout.UiSurface, interaction.SelectionTool);
    }
}
