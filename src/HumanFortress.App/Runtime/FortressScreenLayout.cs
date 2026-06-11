using HumanFortress.App.UI;
using SadConsole;

namespace HumanFortress.App.Runtime;

internal sealed record FortressScreenLayout(
    ScreenSurface RootSurface,
    MapScreenSurface MapSurface,
    UiOverlaySurface UiSurface);
