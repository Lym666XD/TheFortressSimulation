using HumanFortress.App.UI;
using SadConsole;

namespace HumanFortress.App.Rendering;

internal sealed record FortressScreenLayout(
    ScreenSurface RootSurface,
    MapScreenSurface MapSurface,
    UiOverlaySurface UiSurface);
