namespace HumanFortress.Simulation.Creatures;

using HumanFortress.Simulation.Diagnostics;

internal static class CreaturesDiffApplicator
{
    public static Action<string>? LogCallback { get; set; }

    public static void ApplyAll(World.World world, IReadOnlyList<CreaturesDiff> diffs, ulong tick)
    {
        if (diffs.Count == 0) return;

        foreach (var diff in diffs)
        {
            try
            {
                if (diff.Op == CreaturesDiffOp.SpawnCreature)
                {
                    world.Creatures.SpawnCreature(diff.CreatureId, diff.WorldPos, diff.Z, diff.FactionId, tick);
                }
            }
            catch (Exception ex)
            {
                Emit($"[CreaturesDiffApplicator] Failed to apply {diff.Op}: {ex.Message}");
            }
        }
    }

    private static void Emit(string message)
    {
        SimulationDiagnostics.Error(LogCallback, "Simulation.CreaturesDiff", message);
    }
}
