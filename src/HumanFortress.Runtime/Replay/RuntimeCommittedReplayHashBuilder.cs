using HumanFortress.Contracts.Runtime.Replay;
using HumanFortress.Core.Determinism;
using HumanFortress.Jobs.Profession;
using HumanFortress.Jobs.Replay;
using HumanFortress.Runtime.Composition;

namespace HumanFortress.Runtime.Replay;

internal readonly record struct RuntimeCommittedProfessionReplayData(
    string Hash,
    int RecordCount);

internal readonly record struct RuntimeCommittedReplayProjection(
    RuntimeReplayCheckpointData Replay,
    RuntimeCommittedProfessionReplayData Professions);

internal static class RuntimeCommittedReplayHashBuilder
{
    internal static RuntimeCommittedReplayProjection Build(
        RuntimeReplayCheckpointData replay,
        SimulationRuntimeSystems? systems)
    {
        var snapshot = systems?.ProfessionAssignments.GetReplaySnapshot()
            ?? ProfessionAssignmentsReplaySnapshot.Empty;
        var professionHash = ProfessionReplayHashBuilder.Build(snapshot);
        var professionRecordCount = snapshot.Workers.Sum(static worker =>
            1 + worker.Weights.Count + worker.SkillLevels.Count);
        var aggregateHash = ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("runtime.committed-replay.checkpoint.v2");
            hash.AddString(replay.AggregateHash);
            hash.AddString("jobs.professions");
            hash.AddString(professionHash);
            hash.AddInt32(professionRecordCount);
        });

        return new RuntimeCommittedReplayProjection(
            replay with { AggregateHash = aggregateHash },
            new RuntimeCommittedProfessionReplayData(
                professionHash,
                professionRecordCount));
    }
}
