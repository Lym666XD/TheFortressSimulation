using HumanFortress.App.Diagnostics;
using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal sealed record FortressUiOverlayRenderContext(
    UiOverlaySurface UiSurface,
    MapScreenSurface MapSurface,
    UiStore Ui,
    FortressViewRuntimePorts Runtime,
    IFortressDiagnosticsAccess Diagnostics,
    FortressUiServices? UiServices,
    FortressUiOverlayPresenterCache UiOverlayPresenter,
    SimulationMapViewportData MapViewport,
    Point CameraPosition,
    Point CursorPosition,
    Point? LastMousePosition,
    int CurrentZ,
    int ZoomLevel,
    int FortressSize,
    ulong UiTick);
