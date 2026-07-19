using System;
using HumanFortress.Simulation.Orders;
using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.Jobs;
using SadRogue.Primitives;

namespace HumanFortress.Jobs.Mining;

internal sealed class ActiveMiningJob
{
    internal Guid WorkerId { get; init; }
    internal Point Target { get; init; }
    internal int Z { get; init; }
    internal Point Adjacent { get; set; }
    internal MiningStage Stage { get; set; }
    internal int ProgressTicks { get; set; }
    internal int RequiredTicks { get; set; }
    internal ushort GeologyHandle { get; init; }
    internal TerrainKind TerrainKind { get; init; }
    internal int Priority { get; init; }
    internal ulong AssignedTick { get; init; }
    internal int ReplanFailCount { get; set; }
    internal MiningAction Action { get; init; }
    internal MiningSegment Segment { get; init; }
    internal int DesignationId { get; init; }
    internal byte PathSearchAttempt { get; set; }
    internal ReservationManager.CreatureToken CreatureReservation { get; set; }
}
