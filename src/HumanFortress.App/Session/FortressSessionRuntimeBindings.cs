using HumanFortress.App.Rendering;
using HumanFortress.App.UI;

namespace HumanFortress.App.Session;

internal sealed record FortressSessionRuntimeBindings(
    NavigationOverlay NavigationOverlay,
    FortressUiServices UiServices);
