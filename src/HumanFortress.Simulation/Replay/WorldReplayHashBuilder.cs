using HumanFortress.Core.Determinism;
using SimulationWorld = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Replay;

internal static partial class WorldReplayHashBuilder
{
    internal static string Build(SimulationWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("world.snapshot.v2");
            hash.AddInt32(world.SizeInChunks);
            hash.AddInt32(world.MaxZ);
            AddTerrainHash(hash, world);
            AddItemsHash(hash, world.Items.GetAllInstances());
            AddIdentityAuthorityHash(hash, world.Items.GetIdentityAuthoritySnapshot());
            AddCreaturesHash(hash, world.Creatures.GetAllInstances());
            AddIdentityAuthorityHash(hash, world.Creatures.GetIdentityAuthoritySnapshot());
            AddReservationsHash(hash, world);
            AddStockpileZonesHash(hash, world.Stockpiles.GetAllZones());
            PlaceablesReplayHashBuilder.Append(hash, world);
            OrdersReplayHashBuilder.Append(hash, world);
        });
    }

    internal static WorldReplaySectionHashes BuildSectionHashes(SimulationWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        return new WorldReplaySectionHashes(
            TerrainHash: BuildTerrainHash(world),
            ItemsHash: BuildItemsHash(world),
            CreaturesHash: BuildCreaturesHash(world),
            ReservationsHash: BuildReservationsHash(world),
            StockpileZonesHash: BuildStockpileZonesHash(world),
            PlaceablesHash: PlaceablesReplayHashBuilder.Build(world),
            OrdersHash: OrdersReplayHashBuilder.Build(world));
    }
}
