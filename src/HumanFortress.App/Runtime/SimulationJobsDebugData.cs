using HumanFortress.App.Jobs;

namespace HumanFortress.App.Runtime;

public readonly record struct SimulationJobsDebugData(
    ulong Tick,
    TransportJobSystem.TransportDebugSnapshot? Transport,
    MiningJobSystem.MiningDebugSnapshot? Mining,
    CraftJobStatsSnapshot? Craft,
    SchedulerTunings? Tunings);
