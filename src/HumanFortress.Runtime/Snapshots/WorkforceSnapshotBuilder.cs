using HumanFortress.Runtime.Composition;
using HumanFortress.Runtime.Host;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Snapshots;

internal static class WorkforceSnapshotBuilder
{
    internal static WorkforceDebugData Build(
        SimulationRuntimeHost<SimulationRuntimeSystems>? runtimeHost,
        World? world)
    {
        var professions = runtimeHost?.Systems?.ProfessionAssignments;
        if (professions == null)
            return new WorkforceDebugData(
                Array.Empty<ProfessionDefinitionView>(),
                Array.Empty<ProfessionRosterEntryView>(),
                0,
                0);

        var roster = professions.GetRosterSnapshot(world)
            .Select(entry =>
            {
                var creature = world?.Creatures.GetInstance(entry.WorkerId);
                return new ProfessionRosterEntryView(
                    entry.WorkerId,
                    entry.Name,
                    creature?.HP > 0,
                    entry.Weights);
            })
            .ToList();

        var definitions = professions.Registry.Definitions
            .Select(definition => new ProfessionDefinitionView(definition.Id, definition.Name))
            .ToList();

        return new WorkforceDebugData(
            definitions,
            roster,
            roster.Count,
            roster.Count(entry => entry.IsAvailable));
    }
}
