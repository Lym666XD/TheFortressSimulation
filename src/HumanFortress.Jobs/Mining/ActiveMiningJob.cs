using System;
using HumanFortress.Simulation.Orders;
using HumanFortress.Simulation.Tiles;
using SadRogue.Primitives;

namespace HumanFortress.Jobs.Mining;

internal sealed class ActiveMiningJob
{
    public Guid WorkerId { get; init; }
    public Point Target { get; init; }
    public int Z { get; init; }
    public Point Adjacent { get; set; }
    public MiningStage Stage { get; set; }
    public int ProgressTicks { get; set; }
    public int RequiredTicks { get; set; }
    public ushort GeologyHandle { get; init; }
    public TerrainKind TerrainKind { get; init; }
    public int Priority { get; init; }
    public ulong AssignedTick { get; init; }
    public int ReplanFailCount { get; set; }
    public MiningAction Action { get; init; }
    public MiningSegment Segment { get; init; }
    public int DesignationId { get; init; }
}
