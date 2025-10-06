using SadRogue.Primitives;

namespace HumanFortress.Simulation.Orders;

public readonly record struct MiningAdvancedDesignation(
    Rectangle Rect,
    int ZMin,
    int ZMax,
    MiningAction Action,
    int Priority,
    ulong CreatedTick);

public enum MiningSegment : byte { None = 0, Top = 1, Middle = 2, Bottom = 3 }

