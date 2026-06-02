using HumanFortress.Core.World;
using HumanFortress.Navigation;
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

            System.Console.WriteLine("[GenerateFortressMap] Starting fortress generation");
            System.Console.WriteLine($"[GenerateFortressMap] FortressSize: {fortressSize}, EmbarkLocation: {embarkLocation}");

            var currentWorld = _session.CurrentWorld;
            var worldTiles = currentWorld.Tiles;
            if (!currentWorld.Success)
            {
                System.Console.WriteLine("[GenerateFortressMap] ERROR: CurrentWorld is null");
                return FallbackToRuntimeWorld();
            }

            if (worldTiles == null)
            {
                System.Console.WriteLine("[GenerateFortressMap] WARNING: World tiles are null, using fallback");
                return FallbackToRuntimeWorld();
            }

            System.Console.WriteLine($"[GenerateFortressMap] World tiles dimensions: {worldTiles.GetLength(0)}x{worldTiles.GetLength(1)}");

            if (embarkLocation.X < 0 ||
                embarkLocation.Y < 0 ||
                embarkLocation.X >= worldTiles.GetLength(0) ||
                embarkLocation.Y >= worldTiles.GetLength(1))
            {
                System.Console.WriteLine($"[GenerateFortressMap] ERROR: EmbarkLocation {embarkLocation} out of bounds");
                return FallbackToRuntimeWorld();
            }

            var worldTile = worldTiles[embarkLocation.X, embarkLocation.Y];
            System.Console.WriteLine($"[GenerateFortressMap] Got world tile at {embarkLocation}");

            System.Console.WriteLine("[GenerateFortressMap] Creating FortressGenerator");
            var generator = new FortressGenerator(
                fortressSize,
                worldTile,
                embarkLocation,
                (uint)(embarkLocation.X * 1000 + embarkLocation.Y)
            );

            System.Console.WriteLine("[GenerateFortressMap] Generating fortress map");
            var fortressMap = generator.Generate();
            System.Console.WriteLine($"[GenerateFortressMap] Fortress map generated: {fortressMap.Size}x{fortressMap.Size} chunks");

            System.Console.WriteLine("[GenerateFortressMap] Getting World from runtime");
            var world = _runtime.World;

            if (world == null)
            {
                System.Console.WriteLine("[GenerateFortressMap] ERROR: runtime World is null");
                return new FortressSessionInitializationResult(
                    null,
                    fortressMap,
                    null,
                    null,
                    worldTile,
                    UsedFallbackWorld: false);
            }

            System.Console.WriteLine($"[GenerateFortressMap] World obtained: {world.SizeInChunks}x{world.SizeInChunks} chunks");
            System.Console.WriteLine($"[GenerateFortressMap] Creature definitions loaded: {world.Creatures.DefinitionCount}");
            System.Console.WriteLine($"[GenerateFortressMap] Item definitions loaded: {world.Items.DefinitionCount}");

            System.Console.WriteLine("[GenerateFortressMap] Filling world with terrain data");
            fortressMap.FillWorld(world);
            System.Console.WriteLine("[GenerateFortressMap] World filled with terrain data");

            System.Console.WriteLine("[GenerateFortressMap] Creating RenderSnapshotBuilder");
            var snapshotBuilder = new RenderSnapshotBuilder(world);

            System.Console.WriteLine("[GenerateFortressMap] Using shared NavigationManager");
            var navigationManager = _runtime.NavManager ?? new NavigationManager(world);
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
            System.Console.WriteLine($"[GenerateFortressMap] ERROR: {ex.Message}");
            System.Console.WriteLine($"[GenerateFortressMap] Stack trace: {ex.StackTrace}");

            System.Console.WriteLine("[GenerateFortressMap] Using runtime World despite error");
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
