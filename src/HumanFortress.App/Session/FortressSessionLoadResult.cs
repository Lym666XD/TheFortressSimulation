using HumanFortress.App.Rendering;
using HumanFortress.App.UI;

namespace HumanFortress.App.Session;

internal sealed record FortressSessionLoadResult(
    bool HasWorld,
    bool HasFortressMap,
    NavigationOverlay? NavigationOverlay,
    FortressUiServices? UiServices,
    EmbarkSiteSummary? EmbarkSite,
    bool UsedFallbackWorld);
