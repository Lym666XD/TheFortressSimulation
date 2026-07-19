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
        var mappedRange = MiningZRangeMapper.ToSimulationRange(zMin, zMax, action);
        var actualZMin = mappedRange.ZMin;
        var actualZMax = mappedRange.ZMax;

        if (mappedRange.WasInverted)
        {
            var msgConvert = $"[ORDERS] Stairwell Z-inversion: UI z={zMin}..{zMax} ({mappedRange.LayerCount} layers) -> actual dig z={actualZMin}..{actualZMax} (down from surface)";
            Log(msgConvert);
        }

        var msg = $"[ORDERS] MiningAdvanced enqueued action={action} rect=({worldRect.X},{worldRect.Y},{worldRect.Width}x{worldRect.Height}) z={actualZMin}..{actualZMax} pri={priority}";
        Log(msg);

        // Unified path: either add designation or emit cancellation region
        if (action == MiningAction.RemoveDigging)
        {
            lock (_sync)
            {
                _miningCancel.Enqueue(new MiningCancelRegion(worldRect, actualZMin, actualZMax, MiningCancelKind.AllMining));
            }
        }
        else
        {
            lock (_sync)
            {
                var id = ++_nextMiningId;
                var d = new MiningDesignation(id, worldRect, actualZMin, actualZMax, action, priority, createdTick);
                _miningAdd.Enqueue(d);
                _recentMining.Enqueue(d);
                _activeMining.Add(d);
                TrimRecentQueue(_recentMining);
            }
        }
    }

    /// <summary>
    /// Drain new unified mining designations (V2) into provided list.
    /// </summary>
    internal int DrainMiningAdds(ICollection<MiningDesignation> into, int maxCount)
    {
        return DrainQueue(_miningAdd, into, maxCount);
    }

    /// <summary>
    /// Drain mining cancellation regions (RemoveDigging) into provided list.
    /// </summary>
    internal int DrainMiningCancels(ICollection<MiningCancelRegion> into, int maxCount)
    {
        return DrainQueue(_miningCancel, into, maxCount);
    }

    // V2 snapshots for UI/debug
    internal List<MiningDesignation> GetRecentMining()
    {
        lock (_sync)
        {
            return _recentMining.ToList();
        }
    }

    internal List<MiningDesignation> GetActiveMiningSnapshot()
    {
        lock (_sync)
        {
            return OrderMining(_activeMining).ToList();
        }
    }

    // Unified mining designation contract
    internal readonly record struct MiningDesignation(int Id, Rectangle Rect, int ZMin, int ZMax, MiningAction Action, int Priority, ulong CreatedTick);

    internal enum MiningCancelKind { AllMining }

    internal readonly record struct MiningCancelRegion(Rectangle Rect, int ZMin, int ZMax, MiningCancelKind Kind);
}
