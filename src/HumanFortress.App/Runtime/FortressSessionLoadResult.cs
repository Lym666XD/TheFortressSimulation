using HumanFortress.App.Rendering;
using HumanFortress.Core.World;
using HumanFortress.Navigation;
using HumanFortress.Simulation.Rendering;
using HumanFortress.Simulation.World;
using HumanFortress.WorldGen;

namespace HumanFortress.App.Runtime;

internal sealed record FortressSessionLoadResult(
    World? World,
    FortressMap? FortressMap,
    RenderSnapshotBuilder? SnapshotBuilder,
    NavigationManager? NavigationManager,
    NavigationOverlay? NavigationOverlay,
    bool OverlayFromSnapshot,
    FortressUiServices? UiServices,
    WorldTile? WorldTile,
    bool UsedFallbackWorld);
