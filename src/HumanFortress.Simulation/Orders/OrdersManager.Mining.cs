using SadRogue.Primitives;

namespace HumanFortress.Simulation.Orders;

internal sealed partial class OrdersManager
{
    /// <summary>
    /// Legacy wrapper kept for compatibility: simple mining becomes Advanced DIG at single Z.
    /// </summary>
    internal void EnqueueMining(Rectangle worldRect, int z, int priority, ulong createdTick)
    {
        EnqueueMiningAdvanced(worldRect, z, z, MiningAction.Dig, priority, createdTick);
    }

    /// <summary>
    /// Enqueue advanced mining order. DIG/DIG_RAMP decomposed into per-Z MiningDesignation immediately.
    /// Others queued for future handling.
    /// </summary>
    internal void EnqueueMiningAdvanced(Rectangle worldRect, int zMin, int zMax, MiningAction action, int priority, ulong createdTick)
    {
        // === Z-AXIS INVERSION FOR STAIRWELLS ===
        // Game internal: Z increases upward (z=25 is ground, z=32 is higher)
        // Player perception: Higher Z values = deeper underground
        //
        // When player scrolls "up" from z=25 to z=32, they expect to dig DOWN into the earth.
        // But internally, we're moving UP in Z. So we need to invert the range for stairwells.
        //
        // Example: Player at z=25 (surface) scrolls to z=32 (wants to dig 7 layers deep)
        //   UI input: zMin=25, zMax=32 (scroll direction: up)
        //   Converted: zMin=18, zMax=25 (actual digging: down into lower Z values)
        //
        // TODO: Long-term fix should align game Z-axis with player perception

        var actualZMin = zMin;
        var actualZMax = zMax;

        if (action == MiningAction.DigStairwell && zMax > zMin)
        {
            var startZ = zMin;  // Player's starting position (e.g., z=25 surface)
            var layerCount = zMax - zMin;  // How many layers player selected (e.g., 7 layers)

            // Invert: dig DOWN from starting point (decrease Z values)
            actualZMin = Math.Max(0, startZ - layerCount);  // e.g., 25-7=18 (deepest point)
            actualZMax = startZ;  // e.g., 25 (surface, starting point)

            var msgConvert = $"[ORDERS] Stairwell Z-inversion: UI z={zMin}..{zMax} ({layerCount} layers) → actual dig z={actualZMin}..{actualZMax} (down from surface)";
            Log(msgConvert);
        }

        var msg = $"[ORDERS] MiningAdvanced enqueued action={action} rect=({worldRect.X},{worldRect.Y},{worldRect.Width}x{worldRect.Height}) z={actualZMin}..{actualZMax} pri={priority}";
        Log(msg);

        // Unified path: either add designation or emit cancellation region
        if (action == MiningAction.RemoveDigging)
        {
            _miningCancel.Enqueue(new MiningCancelRegion(worldRect, actualZMin, actualZMax, MiningCancelKind.AllMining));
        }
        else
        {
            var id = System.Threading.Interlocked.Increment(ref _nextMiningId);
            var d = new MiningDesignation(id, worldRect, actualZMin, actualZMax, action, priority, createdTick);
            _miningAdd.Enqueue(d);
            _recentMining.Enqueue(d);
            _activeMining.Add(d);
            while (_recentMining.Count > RecentCapacity && _recentMining.TryDequeue(out _)) { }
        }
    }

    /// <summary>
    /// Drain new unified mining designations (V2) into provided list.
    /// </summary>
    internal int DrainMiningAdds(ICollection<MiningDesignation> into, int maxCount)
    {
        var drained = 0;
        while (drained < maxCount && _miningAdd.TryDequeue(out var d))
        {
            into.Add(d);
            drained++;
        }

        return drained;
    }

    /// <summary>
    /// Drain mining cancellation regions (RemoveDigging) into provided list.
    /// </summary>
    internal int DrainMiningCancels(ICollection<MiningCancelRegion> into, int maxCount)
    {
        var drained = 0;
        while (drained < maxCount && _miningCancel.TryDequeue(out var d))
        {
            into.Add(d);
            drained++;
        }

        return drained;
    }

    // V2 snapshots for UI/debug
    internal List<MiningDesignation> GetRecentMining() => _recentMining.ToList();

    internal List<MiningDesignation> GetActiveMiningSnapshot() => _activeMining.ToList();

    // Unified mining designation contract
    internal readonly record struct MiningDesignation(int Id, Rectangle Rect, int ZMin, int ZMax, MiningAction Action, int Priority, ulong CreatedTick);

    internal enum MiningCancelKind { AllMining }

    internal readonly record struct MiningCancelRegion(Rectangle Rect, int ZMin, int ZMax, MiningCancelKind Kind);
}
