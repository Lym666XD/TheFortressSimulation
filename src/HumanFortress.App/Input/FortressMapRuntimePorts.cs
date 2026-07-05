using HumanFortress.App.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal sealed class FortressMapRuntimePorts
{
    internal FortressMapRuntimePorts(
        FortressPlacementRuntimePorts placement,
        IFortressRuntimeDebugSpawnQueryAccess debugSpawnQueries,
        IFortressRuntimeDebugSpawnCommandAccess debugSpawnCommands,
        IFortressRuntimeMapInspectionAccess mapInspection)
    {
        Placement = placement ?? throw new ArgumentNullException(nameof(placement));
        DebugSpawn = new FortressDebugSpawnRuntimePorts(debugSpawnQueries, debugSpawnCommands);
        MapInspection = new FortressMapInspectionRuntimePorts(mapInspection);
    }

    internal FortressPlacementRuntimePorts Placement { get; }

    internal FortressDebugSpawnRuntimePorts DebugSpawn { get; }

    internal FortressMapInspectionRuntimePorts MapInspection { get; }
}

internal sealed class FortressDebugSpawnRuntimePorts
{
    private readonly IFortressRuntimeDebugSpawnQueryAccess _queries;
    private readonly IFortressRuntimeDebugSpawnCommandAccess _commands;

    internal FortressDebugSpawnRuntimePorts(
        IFortressRuntimeDebugSpawnQueryAccess queries,
        IFortressRuntimeDebugSpawnCommandAccess commands)
    {
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
    }

    internal SimulationDebugSpawnData GetDebugSpawnData() =>
        _queries.GetDebugSpawnData();

    internal void QueueCreatureSpawn(
        string creatureId,
        Point position,
        int z,
        string factionId) =>
        _commands.QueueCreatureSpawn(creatureId, position, z, factionId);

    internal void QueueItemSpawn(
        string itemId,
        Point position,
        int z,
        int quantity = 1) =>
        _commands.QueueItemSpawn(itemId, position, z, quantity);
}

internal sealed class FortressMapInspectionRuntimePorts
{
    private readonly IFortressRuntimeMapInspectionAccess _runtime;

    internal FortressMapInspectionRuntimePorts(IFortressRuntimeMapInspectionAccess runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    internal ZoneHitData FindZoneAt(Point worldPosition, int z) =>
        _runtime.FindZoneAt(worldPosition, z);

    internal StockpileHitData FindStockpileAt(Point worldPosition, int z) =>
        _runtime.FindStockpileAt(worldPosition, z);

    internal SimulationTileInspectionData GetTileInspectionData(Point tileWorldPosition, int tileZ) =>
        _runtime.GetTileInspectionData(tileWorldPosition, tileZ);

    internal SimulationWorkshopDebugData GetWorkshopDebugData() =>
        _runtime.GetWorkshopDebugData();
}
