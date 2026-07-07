using HumanFortress.Core.Commands;
using HumanFortress.Core.Time;
using HumanFortress.Runtime.Composition;
using HumanFortress.Runtime.Host;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Startup;

internal static class FortressRuntimeStartup
{
    internal static void Start(
        SimulationRuntimeHost<SimulationRuntimeSystems> runtime,
        bool enqueueAutoDig,
        CommandQueue commandQueue,
        TickScheduler tickScheduler,
        Action<World, CommandQueue, ulong>? autoDigSeeder = null,
        Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(commandQueue);
        ArgumentNullException.ThrowIfNull(tickScheduler);

        runtime.Start(systems =>
        {
            SimulationInitialWorkerSpawner.SpawnIfNeeded(runtime.World, log: log);
            systems.ProfessionAssignments.Initialize(runtime.World.Creatures.GetAllInstances());

            if (!enqueueAutoDig || autoDigSeeder == null)
            {
                return;
            }

            try
            {
                autoDigSeeder(runtime.World, commandQueue, tickScheduler.CurrentTick);
            }
            catch (Exception ex)
            {
                log?.Invoke($"[AUTO-DIG] ERROR: {ex.Message}");
            }
        });
    }
}
