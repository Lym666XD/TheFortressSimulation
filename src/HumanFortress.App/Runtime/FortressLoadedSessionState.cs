using HumanFortress.App.Rendering;
using HumanFortress.Navigation;
using HumanFortress.Simulation.Rendering;
using HumanFortress.Simulation.World;
using HumanFortress.WorldGen;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal readonly record struct FortressLoadedSessionSnapshot(
    World? World,
    FortressMap? FortressMap,
    RenderSnapshot? CurrentSnapshot,
    bool OverlayFromSnapshot,
    NavigationOverlay? NavigationOverlay,
    NavigationManager? NavigationManager,
    FortressUiServices? UiServices)
{
    public bool HasFortressMap => FortressMap != null;
}

internal sealed class FortressLoadedSessionState
{
    public World? World { get; private set; }
    public FortressMap? FortressMap { get; private set; }
    public RenderSnapshotBuilder? SnapshotBuilder { get; private set; }
    public RenderSnapshot? CurrentSnapshot { get; private set; }
    public bool OverlayFromSnapshot { get; private set; }
    public NavigationOverlay? NavigationOverlay { get; private set; }
    public NavigationManager? NavigationManager { get; private set; }
    public FortressUiServices? UiServices { get; private set; }

    public bool HasFortressMap => FortressMap != null;

    public FortressLoadedSessionSnapshot Capture()
    {
        return new FortressLoadedSessionSnapshot(
            World,
            FortressMap,
            CurrentSnapshot,
            OverlayFromSnapshot,
            NavigationOverlay,
            NavigationManager,
            UiServices);
    }

    public void Apply(FortressSessionLoadResult loaded)
    {
        World = loaded.World;
        FortressMap = loaded.FortressMap;
        SnapshotBuilder = loaded.SnapshotBuilder;
        NavigationManager = loaded.NavigationManager;
        NavigationOverlay = loaded.NavigationOverlay;
        OverlayFromSnapshot = loaded.OverlayFromSnapshot;
        UiServices = loaded.UiServices;
        CurrentSnapshot = null;
    }

    public void RefreshSnapshot(
        Point cameraPosition,
        int currentZ,
        int viewportWidth,
        int viewportHeight)
    {
        CurrentSnapshot = FortressRenderSnapshotService.Build(
            SnapshotBuilder,
            World,
            cameraPosition,
            currentZ,
            viewportWidth,
            viewportHeight);
    }
}
