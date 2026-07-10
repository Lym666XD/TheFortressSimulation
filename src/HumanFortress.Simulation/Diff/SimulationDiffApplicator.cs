using System;
using System.Collections.Generic;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Core.Simulation;
using HumanFortress.Simulation.Diagnostics;
using SimulationWorld = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Diff;

/// <summary>
/// Applies core DiffLog operations to the simulation world (v1.1 minimal).
/// This runs after systems' Write phase per UPDATE_ORDER.
/// </summary>
internal static partial class SimulationDiffApplicator
{
    /// <summary>
    /// Optional logging callback supplied by Runtime composition.
    /// </summary>
    internal static Action<string>? LogCallback { get; set; }

    internal static void ApplyAll(SimulationWorld world, IReadOnlyList<DiffOp> ops, IRuntimeGeologyCatalog? geology = null)
    {
        if (ops.Count == 0) return;

        foreach (var op in ops)
        {
            try
            {
                switch (op.Op)
                {
                    case DiffOpType.SetTerrain:
                        ApplySetTerrain(world, op, geology);
                        break;
                    case DiffOpType.MoveCreature:
                        ApplyMoveCreature(world, op);
                        break;
                    case DiffOpType.MoveItem:
                        ApplyMoveItem(world, op);
                        break;
                    case DiffOpType.MarkCarried:
                        ApplyMarkCarried(world, op);
                        break;
                    case DiffOpType.UnmarkCarried:
                        ApplyUnmarkCarried(world, op);
                        break;
                    default:
                        // Stockpile and other typed operations use their own applicators.
                        break;
                }
            }
            catch (Exception ex)
            {
                EmitError($"[SimulationDiffApplicator] Failed to apply {op.Op}: {ex.Message}", ex);
            }
        }
    }

    private static void Emit(string message)
    {
        SimulationDiagnostics.Information(LogCallback, "Simulation.Diff", message);
    }

    private static void EmitError(string message, Exception exception)
    {
        SimulationDiagnostics.Error(LogCallback, "Simulation.Diff", message, exception);
    }
}
