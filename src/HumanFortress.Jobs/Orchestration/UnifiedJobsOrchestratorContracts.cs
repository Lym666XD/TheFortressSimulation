using HumanFortress.Core.Time;
using HumanFortress.Jobs.Mining;
using HumanFortress.Jobs.Transport;

namespace HumanFortress.Jobs.Orchestration;

internal interface IUnifiedJobExecutor : ISequentialCompatibilityStage
{
    int LastIntakeCount { get; }
}

internal interface IUnifiedTransportJobExecutor : IUnifiedJobExecutor
{
    TransportJobStatsSnapshot GetLastStatsSnapshot();
    void ApplySchedulingHints(int? intakeCap, int? maxActiveCap, int reserveSlots);
}

internal interface IUnifiedMiningJobExecutor : IUnifiedJobExecutor
{
    int GetBacklogCount();
    MiningJobStatsSnapshot GetLastStatsSnapshot();
}

internal interface IUnifiedConstructionJobExecutor : IUnifiedJobExecutor
{
}

internal interface IUnifiedCraftJobExecutor : IUnifiedJobExecutor
{
}
