using HumanFortress.Runtime.Diagnostics;
using HumanFortress.Core.Commands;
using HumanFortress.Runtime.Commands;

namespace HumanFortress.Runtime;

/// <summary>
/// Tool-only deterministic driver over the production Runtime composition.
/// Kept internal so App cannot bypass the normal lifecycle or tick thread.
/// </summary>
internal interface IFortressRuntimeHeadlessScenarioSessionPorts : IFortressRuntimeSessionPorts
{
    void ConfigureManualTicks(int initialCreatureCount);

    void FillDeterministicFlatWorld(int standableZ);

    RuntimeHeadlessWorkloadResult SeedWorkload(RuntimeHeadlessWorkloadRequest request);

    RuntimeHeadlessCachePrimeResult PrimeDerivedCaches();

    RuntimeCommandReplayRestoreResult RestoreCommandJournal(
        IReadOnlyList<CommandReplayRecord> records);

    void ExecuteSingleTick();

    RuntimeHeadlessMetricsSnapshot CaptureHeadlessMetrics();
}
