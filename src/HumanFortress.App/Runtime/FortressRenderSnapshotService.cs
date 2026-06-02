using HumanFortress.Simulation.Rendering;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal static class FortressRenderSnapshotService
{
    public static RenderSnapshot? Build(
        RenderSnapshotBuilder? snapshotBuilder,
        World? world,
        Point cameraPosition,
        int currentZ,
        int viewportWidth,
        int viewportHeight)
    {
        try
        {
            System.Console.WriteLine("[BuildSnapshot] Starting snapshot build");

            if (snapshotBuilder == null)
            {
                System.Console.WriteLine("[BuildSnapshot] WARNING: SnapshotBuilder is null");
                return null;
            }

            if (world == null)
            {
                System.Console.WriteLine("[BuildSnapshot] WARNING: World is null");
                return null;
            }

            var chunkX = cameraPosition.X / 32;
            var chunkY = cameraPosition.Y / 32;
            System.Console.WriteLine($"[BuildSnapshot] Camera chunk: {chunkX},{chunkY} at Z={currentZ}");

            var camera = new CameraInfo
            {
                ChunkKey = new ChunkKey(chunkX, chunkY, currentZ),
                CenterX = cameraPosition.X % 32,
                CenterY = cameraPosition.Y % 32,
                Z = currentZ,
                Z0 = currentZ,
                ZCount = 1
            };

            var viewport = new ViewportInfo
            {
                TilesWidth = viewportWidth,
                TilesHeight = viewportHeight
            };

            System.Console.WriteLine("[BuildSnapshot] Building snapshot");
            var snapshot = snapshotBuilder.BuildSnapshot(camera, viewport, 0);
            System.Console.WriteLine($"[BuildSnapshot] Snapshot built with {snapshot?.Chunks?.Count ?? 0} chunks");
            return snapshot;
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[BuildSnapshot] ERROR: {ex.Message}");
            System.Console.WriteLine($"[BuildSnapshot] Stack trace: {ex.StackTrace}");
            return null;
        }
    }
}
