using HumanFortress.App.Rendering;
using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.Session;

internal sealed record FortressSessionLoadResult(
    bool HasWorld,
    bool HasFortressMap,
    NavigationOverlay? NavigationOverlay,
    FortressUiServices? UiServices,
    EmbarkSiteSummary? EmbarkSite,
    SimulationWorldAvailabilityData WorldAvailability,
    bool UsedFallbackWorld);
