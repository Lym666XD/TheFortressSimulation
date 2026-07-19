using HumanFortress.Core.Time;
using HumanFortress.Core.Simulation;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Orders;

/// <summary>
/// Serialized compatibility stage that consumes mining designations, advances
/// cursors, and hands PlannedDig DTOs to the executor inbox.
/// </summary>
internal enum MiningSegment { None, Top, Middle, Bottom }

internal sealed partial class MiningSystem : ISequentialCompatibilityStage
{
    private readonly World.World _world;
    private readonly OrdersManager _orders;
    private readonly int _maxPerTick;

    private readonly List<PlannedDig> _planned = new();
    private readonly Queue<PlannedDig> _outbox = new();

    private readonly Dictionary<int, ActiveDesignation> _active = new();
    private readonly List<OrdersManager.MiningCancelRegion> _cancels = new();

    internal MiningSystem(World.World world, OrdersManager orders, int maxPerTick = 128)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _maxPerTick = Math.Max(1, maxPerTick);
    }

    internal int Priority => UpdateOrder.Priority.Items; // plan before unit jobs write; same as items stage
    internal string SystemId => "Jobs.Mining";

    void ISequentialCompatibilityStage.PrepareSequentialCompatibility(ulong tick)
        => PrepareSequentialCompatibility(tick);

    void ISequentialCompatibilityStage.ApplySequentialCompatibility(ulong tick)
        => ApplySequentialCompatibility(tick);

    internal readonly record struct PlannedDig(
        Point Cell,
        int Z,
        ushort GeologyHandle,
        byte TerrainKind,
        int Priority,
        ulong Seed,
        MiningAction Action,
        MiningSegment Segment,
        int DesignationId,
        byte PathSearchAttempt = 0);
}
