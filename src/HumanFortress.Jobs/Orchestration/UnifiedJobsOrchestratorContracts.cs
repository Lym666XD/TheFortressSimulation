using HumanFortress.Core.Time;
using HumanFortress.Jobs.Mining;
using HumanFortress.Jobs.Transport;

namespace HumanFortress.App.Jobs;

public interface IUnifiedJobExecutor : ITick
{
    int LastIntakeCount { get; }
}

public interface IUnifiedTransportJobExecutor : IUnifiedJobExecutor
{
    TransportJobStatsSnapshot GetLastStatsSnapshot();
    void ApplySchedulingHints(int? intakeCap, int? maxActiveCap, int reserveSlots);
}

public interface IUnifiedMiningJobExecutor : IUnifiedJobExecutor
{
    int GetBacklogCount();
    MiningJobStatsSnapshot GetLastStatsSnapshot();
}

public interface IUnifiedConstructionJobExecutor : IUnifiedJobExecutor
{
}

public interface IUnifiedCraftJobExecutor : IUnifiedJobExecutor
{
}
