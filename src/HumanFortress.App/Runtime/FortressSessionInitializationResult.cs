using HumanFortress.Core.World;
using HumanFortress.Navigation;
using HumanFortress.Simulation.Rendering;
using HumanFortress.Simulation.World;
using HumanFortress.WorldGen;

namespace HumanFortress.App.Runtime;

internal sealed record FortressSessionInitializationResult(
    World? World,
    FortressMap? FortressMap,
    RenderSnapshotBuilder? SnapshotBuilder,
    NavigationManager? NavigationManager,
    WorldTile? WorldTile,
    bool UsedFallbackWorld);
