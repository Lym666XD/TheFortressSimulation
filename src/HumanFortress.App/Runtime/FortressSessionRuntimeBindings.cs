using HumanFortress.App.Rendering;

namespace HumanFortress.App.Runtime;

internal sealed record FortressSessionRuntimeBindings(
    NavigationOverlay NavigationOverlay,
    bool OverlayFromSnapshot,
    FortressUiServices UiServices);
