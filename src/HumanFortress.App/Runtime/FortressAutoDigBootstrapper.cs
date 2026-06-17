using HumanFortress.App.Commands;
using HumanFortress.App.UI;
using HumanFortress.Runtime;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal static class FortressAutoDigBootstrapper
{
    public static void EnqueueAfterWorldFill(World world, FortressRuntimeAccess runtime, int currentZ)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(runtime);

        try
        {
            if (StartupDigTargetFinder.TryFindNearestDigTarget(world, out var target))
            {
                var rect = new Rectangle(target.X, target.Y, 1, 1);
                Logger.Log($"[DEBUG] Creating mining order command zMin={target.Z} zMax={target.Z} rect=({rect.X},{rect.Y},{rect.Width}x{rect.Height})");
                runtime.EnqueueCurrentTickCommand(tick => new CreateAdvancedMiningOrderCommand(
                    tick,
                    rect,
                    target.Z,
                    target.Z,
                    MiningAction.Dig,
                    priority: 50));
                Logger.Log($"[AUTO-DIG] Enqueued test Dig at ({target.X},{target.Y},{target.Z}) after world fill");
                return;
            }

            int tiles = world.SizeInTiles;
            int cx = tiles / 2;
            int cy = tiles / 2;
            int fallbackZ = Math.Max(0, Math.Min(world.MaxZ - 1, currentZ));
            var fallbackRect = new Rectangle(cx, cy, 1, 1);
            Logger.Log($"[DEBUG] Creating mining order command zMin={fallbackZ} zMax={fallbackZ} rect=({fallbackRect.X},{fallbackRect.Y},{fallbackRect.Width}x{fallbackRect.Height})");
            runtime.EnqueueCurrentTickCommand(tick => new CreateAdvancedMiningOrderCommand(
                tick,
                fallbackRect,
                fallbackZ,
                fallbackZ,
                MiningAction.Dig,
                priority: 50));
            Logger.Log($"[AUTO-DIG] Enqueued fallback Dig at ({fallbackRect.X},{fallbackRect.Y},{fallbackZ})");
        }
        catch (Exception ex)
        {
            Logger.Log($"[AUTO-DIG] ERROR (UI phase): {ex.Message}");
        }
    }
}
