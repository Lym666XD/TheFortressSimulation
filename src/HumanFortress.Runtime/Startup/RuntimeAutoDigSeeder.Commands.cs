using HumanFortress.Core.Commands;
using HumanFortress.Runtime.Commands;
using HumanFortress.Simulation.Orders;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Startup;

internal static partial class RuntimeAutoDigSeeder
{
    private static void EnqueueDig(
        CommandQueue commandQueue,
        ulong currentTick,
        int x,
        int y,
        int z,
        Action<string>? log)
    {
        ArgumentNullException.ThrowIfNull(commandQueue);

        var rect = CreateDigRect(x, y, z, log);
        commandQueue.Enqueue(new CreateAdvancedMiningOrderCommand(
            currentTick,
            rect,
            z,
            z,
            MiningAction.Dig,
            priority: 50));
    }

    private static void EnqueueDig(
        Action<Func<ulong, ICommand>> enqueueCurrentTickCommand,
        int x,
        int y,
        int z,
        Action<string>? log)
    {
        ArgumentNullException.ThrowIfNull(enqueueCurrentTickCommand);

        var rect = CreateDigRect(x, y, z, log);
        enqueueCurrentTickCommand(tick => new CreateAdvancedMiningOrderCommand(
            tick,
            rect,
            z,
            z,
            MiningAction.Dig,
            priority: 50));
    }

    private static Rectangle CreateDigRect(int x, int y, int z, Action<string>? log)
    {
        var rect = new Rectangle(x, y, 1, 1);
        log?.Invoke($"[DEBUG] Creating mining order command zMin={z} zMax={z} rect=({rect.X},{rect.Y},{rect.Width}x{rect.Height})");
        return rect;
    }
}
