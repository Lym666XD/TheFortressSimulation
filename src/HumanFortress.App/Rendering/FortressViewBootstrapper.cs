using HumanFortress.App.UI;
using HumanFortress.App.UI.Selection;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal sealed record FortressViewBootstrapContext(
    FortressUiInteractionDataSource InteractionData,
    UiStore Ui,
    int WorldSizeTiles,
    Func<ulong> UiTickProvider,
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
            context.InteractionData,
            context.Ui,
            context.WorldSizeTiles,
            context.UiTickProvider,
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
