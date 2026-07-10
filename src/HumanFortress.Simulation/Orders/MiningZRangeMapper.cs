using System;

namespace HumanFortress.Simulation.Orders;

internal static class MiningZRangeMapper
{
    internal static MiningZRangeMapping ToSimulationRange(int uiZMin, int uiZMax, MiningAction action)
    {
        if (action != MiningAction.DigStairwell || uiZMax <= uiZMin)
            return new MiningZRangeMapping(uiZMin, uiZMax, WasInverted: false, LayerCount: 0);

        var layerCount = uiZMax - uiZMin;
        return new MiningZRangeMapping(
            ZMin: Math.Max(0, uiZMin - layerCount),
            ZMax: uiZMin,
            WasInverted: true,
            LayerCount: layerCount);
    }
}

internal readonly record struct MiningZRangeMapping(int ZMin, int ZMax, bool WasInverted, int LayerCount);
