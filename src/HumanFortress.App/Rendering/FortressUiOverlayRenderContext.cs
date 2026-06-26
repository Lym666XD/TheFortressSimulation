using HumanFortress.App.Diagnostics;
using HumanFortress.App.Runtime;
using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal sealed record FortressUiOverlayRenderContext(
    UiOverlaySurface UiSurface,
    MapScreenSurface MapSurface,
    UiStore Ui,
    IFortressRuntimeReadAccess Runtime,
    IFortressDiagnosticsAccess Diagnostics,
    FortressUiServices? UiServices,
    SimulationMapViewportData MapViewport,
    Point CameraPosition,
    Point CursorPosition,
    Point? LastMousePosition,
    int CurrentZ,
    int ZoomLevel,
    int FortressSize,
    ulong UiTick);
