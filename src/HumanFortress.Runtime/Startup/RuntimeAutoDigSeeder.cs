using HumanFortress.Core.Commands;
using HumanFortress.Runtime.Commands;
using HumanFortress.Simulation.Orders;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Startup;

/// <summary>
/// Optional startup self-test hook that enqueues reproducible mining commands.
/// </summary>
internal static partial class RuntimeAutoDigSeeder
{
    internal static void EnqueueIfPossible(
        World world,
        CommandQueue commandQueue,
        ulong currentTick,
        Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(commandQueue);

        if (!StartupDigTargetFinder.TryFindAnyDigTarget(world, out var target))
        {
            log?.Invoke("[AUTO-DIG] No SolidWall or Ramp found anywhere; skip.");
            return;
        }

        EnqueueDig(commandQueue, currentTick, target.X, target.Y, target.Z, log);
        log?.Invoke($"[AUTO-DIG] Enqueued test Dig at ({target.X},{target.Y},{target.Z})");
    }

    internal static void EnqueueAfterWorldFill(
        World world,
        int currentZ,
        Action<Func<ulong, ICommand>> enqueueCurrentTickCommand,
        Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(enqueueCurrentTickCommand);

        try
        {
            if (StartupDigTargetFinder.TryFindNearestDigTarget(world, out var target))
            {
                EnqueueDig(enqueueCurrentTickCommand, target.X, target.Y, target.Z, log);
                log?.Invoke($"[AUTO-DIG] Enqueued test Dig at ({target.X},{target.Y},{target.Z}) after world fill");
                return;
            }

            int tiles = world.SizeInTiles;
            int fallbackX = tiles / 2;
            int fallbackY = tiles / 2;
            int fallbackZ = Math.Max(0, Math.Min(world.MaxZ - 1, currentZ));
            EnqueueDig(enqueueCurrentTickCommand, fallbackX, fallbackY, fallbackZ, log);
            log?.Invoke($"[AUTO-DIG] Enqueued fallback Dig at ({fallbackX},{fallbackY},{fallbackZ})");
        }
        catch (Exception ex)
        {
            log?.Invoke($"[AUTO-DIG] ERROR (UI phase): {ex.Message}");
        }
    }

}
