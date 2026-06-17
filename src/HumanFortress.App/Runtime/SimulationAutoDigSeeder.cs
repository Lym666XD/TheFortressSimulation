using HumanFortress.App.Commands;
using HumanFortress.App.UI;
using HumanFortress.Core.Commands;
using HumanFortress.Runtime;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

/// <summary>
/// Optional startup self-test hook that enqueues a reproducible mining command.
/// </summary>
internal static class SimulationAutoDigSeeder
{
    public static void EnqueueIfPossible(World world, CommandQueue commandQueue, ulong currentTick)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(commandQueue);

        if (!StartupDigTargetFinder.TryFindAnyDigTarget(world, out var target))
        {
            Logger.Log("[AUTO-DIG] No SolidWall or Ramp found anywhere; skip.");
            return;
        }

        var rect = new Rectangle(target.X, target.Y, 1, 1);
        Logger.Log($"[DEBUG] Creating mining order command zMin={target.Z} zMax={target.Z} rect=({rect.X},{rect.Y},{rect.Width}x{rect.Height})");
        commandQueue.Enqueue(new CreateAdvancedMiningOrderCommand(
            currentTick,
            rect,
            target.Z,
            target.Z,
            MiningAction.Dig,
            priority: 50));
        Logger.Log($"[AUTO-DIG] Enqueued test Dig at ({rect.X},{rect.Y},{target.Z})");
    }
}
