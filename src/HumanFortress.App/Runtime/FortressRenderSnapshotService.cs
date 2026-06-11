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
            Logger.Log("[BuildSnapshot] Starting snapshot build");

            if (snapshotBuilder == null)
            {
                Logger.Log("[BuildSnapshot] WARNING: SnapshotBuilder is null");
                return null;
            }

            if (world == null)
            {
                Logger.Log("[BuildSnapshot] WARNING: World is null");
                return null;
            }

            var chunkX = cameraPosition.X / 32;
            var chunkY = cameraPosition.Y / 32;
            Logger.Log($"[BuildSnapshot] Camera chunk: {chunkX},{chunkY} at Z={currentZ}");

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

            Logger.Log("[BuildSnapshot] Building snapshot");
            var snapshot = snapshotBuilder.BuildSnapshot(camera, viewport, 0);
            Logger.Log($"[BuildSnapshot] Snapshot built with {snapshot?.Chunks?.Count ?? 0} chunks");
            return snapshot;
        }
        catch (Exception ex)
        {
            Logger.Error("UI.BuildSnapshot", $"[BuildSnapshot] ERROR: {ex.Message}", ex);
            return null;
        }
    }
}
