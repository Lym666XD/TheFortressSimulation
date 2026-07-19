using HumanFortress.Core.Determinism;
using HumanFortress.Jobs.Profession;

namespace HumanFortress.Jobs.Replay;

internal static class ProfessionReplayHashBuilder
{
    internal static string Build(ProfessionAssignmentsReplaySnapshot snapshot)
    {
        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("jobs.professions.snapshot.v1");
            var workers = snapshot.Workers
                .OrderBy(static worker => worker.WorkerId)
                .ToArray();
            hash.AddInt32(workers.Length);
            foreach (var worker in workers)
            {
                hash.AddGuid(worker.WorkerId);
                AppendValues(hash, worker.Weights);
                AppendValues(hash, worker.SkillLevels);
            }
        });
    }

    private static void AppendValues(
        ReplayHashBuilder hash,
        IReadOnlyList<ProfessionValueStateSnapshot> values)
    {
        var ordered = values
            .OrderBy(static value => value.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static value => value.Id, StringComparer.Ordinal)
            .ToArray();
        hash.AddInt32(ordered.Length);
        foreach (var value in ordered)
        {
            hash.AddString(value.Id);
            hash.AddInt32(value.Value);
        }
    }
}
