namespace HumanFortress.Simulation.Creatures;

using HumanFortress.Simulation.Diagnostics;

internal static class CreaturesDiffApplicator
{
    internal static void ApplyAll(World.World world, IReadOnlyList<CreaturesDiff> diffs, ulong tick)
    {
        if (diffs.Count == 0) return;

        foreach (var diff in diffs)
        {
            try
            {
                if (diff.Op == CreaturesDiffOp.SpawnCreature)
                {
                    var spawned = world.Creatures.SpawnCreature(
                        diff.CreatureId,
                        diff.WorldPos,
                        diff.Z,
                        diff.FactionId,
                        tick);
                    if (!spawned.HasValue)
                    {
                        throw new InvalidOperationException(
                            $"Creature spawn was rejected for '{diff.CreatureId}'.");
                    }
                }
            }
            catch (Exception ex)
            {
                Emit(world, $"[CreaturesDiffApplicator] Failed to apply {diff.Op}: {ex.Message}");
                throw;
            }
        }
    }

    private static void Emit(World.World world, string message)
    {
        SimulationDiagnostics.Error(world.Diagnostics, "Simulation.CreaturesDiff", message);
    }
}
