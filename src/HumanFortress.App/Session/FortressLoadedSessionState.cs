using HumanFortress.App.Rendering;
using HumanFortress.App.UI;

namespace HumanFortress.App.Session;

internal readonly record struct FortressLoadedSessionSnapshot(
    bool HasFortressMap,
    NavigationOverlay? NavigationOverlay,
    FortressUiServices? UiServices);

internal sealed class FortressLoadedSessionState
{
    internal bool HasFortressMap { get; private set; }
    internal NavigationOverlay? NavigationOverlay { get; private set; }
    internal FortressUiServices? UiServices { get; private set; }

    internal FortressLoadedSessionSnapshot Capture()
    {
        return new FortressLoadedSessionSnapshot(
            HasFortressMap,
            NavigationOverlay,
            UiServices);
    }

    internal void Apply(FortressSessionLoadResult loaded)
    {
        HasFortressMap = loaded.HasFortressMap;
        NavigationOverlay = loaded.NavigationOverlay;
        UiServices = loaded.UiServices;
    }
}
