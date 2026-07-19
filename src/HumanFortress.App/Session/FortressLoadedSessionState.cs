using HumanFortress.App.Rendering;
using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime;

namespace HumanFortress.App.Session;

internal readonly record struct FortressLoadedSessionSnapshot(
    bool HasFortressMap,
    NavigationOverlay? NavigationOverlay,
    FortressUiServices? UiServices,
    RuntimeWorldBounds WorldBounds);

internal sealed class FortressLoadedSessionState
{
    internal bool HasFortressMap { get; private set; }
    internal NavigationOverlay? NavigationOverlay { get; private set; }
    internal FortressUiServices? UiServices { get; private set; }
    internal RuntimeWorldBounds WorldBounds { get; private set; } = RuntimeWorldBounds.Empty;

    internal FortressLoadedSessionSnapshot Capture()
    {
        return new FortressLoadedSessionSnapshot(
            HasFortressMap,
            NavigationOverlay,
            UiServices,
            WorldBounds);
    }

    internal void Apply(FortressSessionLoadResult loaded)
    {
        HasFortressMap = loaded.HasFortressMap;
        NavigationOverlay = loaded.NavigationOverlay;
        UiServices = loaded.UiServices;
        WorldBounds = loaded.WorldAvailability.WorldBounds;
    }
}
