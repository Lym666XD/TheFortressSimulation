using HumanFortress.Jobs;
using HumanFortress.Jobs.Craft;
using HumanFortress.Jobs.Mining;
using HumanFortress.Jobs.Transport;

namespace HumanFortress.App.Runtime;

public readonly record struct SimulationJobsDebugData(
    ulong Tick,
    TransportDebugSnapshot? Transport,
    MiningDebugSnapshot? Mining,
    CraftJobStatsSnapshot? Craft,
    SchedulerTunings? Tunings);
