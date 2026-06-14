using HumanFortress.Core.Commands;
using HumanFortress.Core.Time;
using HumanFortress.Runtime;

namespace HumanFortress.App.Runtime;

internal static class FortressRuntimeStartup
{
    public static void Start(
        SimulationRuntimeHost<SimulationRuntimeSystems> runtime,
        bool enqueueAutoDig,
        CommandQueue commandQueue,
        TickScheduler tickScheduler)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(commandQueue);
        ArgumentNullException.ThrowIfNull(tickScheduler);

        runtime.Start(systems =>
        {
            SimulationInitialWorkerSpawner.SpawnIfNeeded(runtime.World);
            systems.ProfessionAssignments.Initialize(runtime.World.Creatures.GetAllInstances());

            if (!enqueueAutoDig)
            {
                return;
            }

            try
            {
                SimulationAutoDigSeeder.EnqueueIfPossible(runtime.World, commandQueue, tickScheduler.CurrentTick);
            }
            catch (Exception ex)
            {
                Logger.Log($"[AUTO-DIG] ERROR: {ex.Message}");
            }
        });
    }
}
