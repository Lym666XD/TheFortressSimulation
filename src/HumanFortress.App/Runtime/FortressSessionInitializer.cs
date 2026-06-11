using HumanFortress.Core.World;
using HumanFortress.Navigation;
using HumanFortress.Runtime;
using HumanFortress.Simulation.Rendering;
using HumanFortress.Simulation.World;
using HumanFortress.WorldGen;

namespace HumanFortress.App.Runtime;

internal sealed class FortressSessionInitializer
{
    private readonly FortressRuntimeAccess _runtime;
    private readonly FortressSessionContext _session;

    public FortressSessionInitializer(FortressRuntimeAccess runtime, FortressSessionContext session)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public FortressSessionInitializationResult Initialize()
    {
        try
        {
            int fortressSize = _session.FortressSize;
            var embarkLocation = _session.EmbarkLocation;

            Logger.Log("[GenerateFortressMap] Starting fortress generation");
            Logger.Log($"[GenerateFortressMap] FortressSize: {fortressSize}, EmbarkLocation: {embarkLocation}");

            var currentWorld = _session.CurrentWorld;
            var worldTiles = currentWorld.Tiles;
            if (!currentWorld.Success)
            {
                Logger.Log("[GenerateFortressMap] ERROR: CurrentWorld is null");
                return FallbackToRuntimeWorld();
            }

            if (worldTiles == null)
            {
                Logger.Log("[GenerateFortressMap] WARNING: World tiles are null, using fallback");
                return FallbackToRuntimeWorld();
            }

            Logger.Log($"[GenerateFortressMap] World tiles dimensions: {worldTiles.GetLength(0)}x{worldTiles.GetLength(1)}");

            if (embarkLocation.X < 0 ||
                embarkLocation.Y < 0 ||
                embarkLocation.X >= worldTiles.GetLength(0) ||
                embarkLocation.Y >= worldTiles.GetLength(1))
            {
                Logger.Log($"[GenerateFortressMap] ERROR: EmbarkLocation {embarkLocation} out of bounds");
                return FallbackToRuntimeWorld();
            }

            var worldTile = worldTiles[embarkLocation.X, embarkLocation.Y];
            Logger.Log($"[GenerateFortressMap] Got world tile at {embarkLocation}");

            Logger.Log("[GenerateFortressMap] Creating FortressGenerator");
            var generator = new FortressGenerator(
                fortressSize,
                worldTile,
                embarkLocation,
                (uint)(embarkLocation.X * 1000 + embarkLocation.Y)
            );

            Logger.Log("[GenerateFortressMap] Generating fortress map");
            var fortressMap = generator.Generate();
            Logger.Log($"[GenerateFortressMap] Fortress map generated: {fortressMap.Size}x{fortressMap.Size} chunks");

            Logger.Log("[GenerateFortressMap] Getting World from runtime");
            var world = _runtime.World;

            if (world == null)
            {
                Logger.Log("[GenerateFortressMap] ERROR: runtime World is null");
                return new FortressSessionInitializationResult(
                    null,
                    fortressMap,
                    null,
                    null,
                    worldTile,
                    UsedFallbackWorld: false);
            }

            Logger.Log($"[GenerateFortressMap] World obtained: {world.SizeInChunks}x{world.SizeInChunks} chunks");
            Logger.Log($"[GenerateFortressMap] Creature definitions loaded: {world.Creatures.DefinitionCount}");
            Logger.Log($"[GenerateFortressMap] Item definitions loaded: {world.Items.DefinitionCount}");

            Logger.Log("[GenerateFortressMap] Filling world with terrain data");
            fortressMap.FillWorld(world);
            Logger.Log("[GenerateFortressMap] World filled with terrain data");

            Logger.Log("[GenerateFortressMap] Creating RenderSnapshotBuilder");
            var snapshotBuilder = new RenderSnapshotBuilder(world);

            Logger.Log("[GenerateFortressMap] Using shared NavigationManager");
            var navigationManager = _runtime.NavManager ?? SimulationNavigationFactory.Create(world, rebuildAll: false);
            navigationManager.RebuildAll();

            return new FortressSessionInitializationResult(
                world,
                fortressMap,
                snapshotBuilder,
                navigationManager,
                worldTile,
                UsedFallbackWorld: false);
        }
        catch (Exception ex)
        {
            Logger.Error("UI.GenerateFortressMap", $"[GenerateFortressMap] ERROR: {ex.Message}", ex);

            Logger.Log("[GenerateFortressMap] Using runtime World despite error");
            return FallbackToRuntimeWorld();
        }
    }

    private FortressSessionInitializationResult FallbackToRuntimeWorld()
    {
        return new FortressSessionInitializationResult(
            _runtime.World,
            null,
            null,
            null,
            null,
            UsedFallbackWorld: true);
    }
}
