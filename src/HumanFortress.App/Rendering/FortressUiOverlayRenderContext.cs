using HumanFortress.App.Runtime;
using HumanFortress.App.UI;
using HumanFortress.Simulation.Rendering;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal sealed record FortressUiOverlayRenderContext(
    UiOverlaySurface UiSurface,
    MapScreenSurface MapSurface,
    UiStore Ui,
    FortressRuntimeAccess Runtime,
    World? World,
    FortressUiServices? UiServices,
    RenderSnapshot? CurrentSnapshot,
    bool OverlayFromSnapshot,
    Point CameraPosition,
    Point CursorPosition,
    Point? LastMousePosition,
    int CurrentZ,
    int ZoomLevel,
    int FortressSize,
    ulong UiTick);
