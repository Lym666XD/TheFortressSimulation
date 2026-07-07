using HumanFortress.Core.Determinism;
using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Stockpile;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;
using SimulationWorld = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Replay;

internal static class WorldReplayHashBuilder
{
    internal static string Build(SimulationWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("world.snapshot.v1");
            hash.AddInt32(world.SizeInChunks);
            hash.AddInt32(world.MaxZ);
            AddTerrainHash(hash, world);
            AddItemsHash(hash, world.Items.GetAllInstances());
            AddCreaturesHash(hash, world.Creatures.GetAllInstances());
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

    private static string BuildTerrainHash(SimulationWorld world)
    {
        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("world.terrain.snapshot.v1");
            hash.AddInt32(world.SizeInChunks);
            hash.AddInt32(world.MaxZ);
            AddTerrainHash(hash, world);
        });
    }

    private static string BuildItemsHash(SimulationWorld world)
    {
        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("world.items.snapshot.v1");
            AddItemsHash(hash, world.Items.GetAllInstances());
        });
    }

    private static string BuildCreaturesHash(SimulationWorld world)
    {
        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("world.creatures.snapshot.v1");
            AddCreaturesHash(hash, world.Creatures.GetAllInstances());
        });
    }

    private static string BuildReservationsHash(SimulationWorld world)
    {
        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("world.reservations.snapshot.v1");
            AddReservationsHash(hash, world);
        });
    }

    private static string BuildStockpileZonesHash(SimulationWorld world)
    {
        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("world.stockpile_zones.snapshot.v1");
            AddStockpileZonesHash(hash, world.Stockpiles.GetAllZones());
        });
    }

    private static void AddTerrainHash(ReplayHashBuilder hash, SimulationWorld world)
    {
        var chunks = world.GetAllChunks()
            .OrderBy(chunk => chunk.Key.Z)
            .ThenBy(chunk => chunk.Key.ChunkY)
            .ThenBy(chunk => chunk.Key.ChunkX)
            .ToArray();

        hash.AddInt32(chunks.Length);
        foreach (var chunk in chunks)
        {
            hash.AddInt32(chunk.Key.ChunkX);
            hash.AddInt32(chunk.Key.ChunkY);
            hash.AddInt32(chunk.Key.Z);

            var tiles = chunk.GetTilesCopy();
            hash.AddInt32(tiles.Length);
            for (var i = 0; i < tiles.Length; i++)
            {
                var tile = tiles[i];
                hash.AddInt32(i);
                hash.AddInt32(tile.GeoMatId);
                hash.AddInt32(tile.TerrainBits);
                hash.AddByte(tile.SurfaceBits);
                hash.AddByte(tile.FluidKind);
                hash.AddByte(tile.FluidDepth);
                hash.AddByte(tile.MetaBits);
                hash.AddInt32(tile.TrafficCost);
            }
        }
    }

    private static void AddItemsHash(ReplayHashBuilder hash, IEnumerable<ItemInstance> items)
    {
        var ordered = items
            .OrderBy(item => item.Guid)
            .ToArray();

        hash.AddInt32(ordered.Length);
        foreach (var item in ordered)
        {
            hash.AddGuid(item.Guid);
            hash.AddString(item.DefinitionId);
            hash.AddNullableString(item.MaterialId);
            hash.AddInt32(item.StackCount);
            AddPointHash(hash, item.Position);
            hash.AddInt32(item.Z);
            AddNullableGuid(hash, item.ContainedBy);
            AddNullableGuid(hash, item.CarriedBy);
            AddNullableGuid(hash, item.EquippedBy);
            AddPlacementHash(hash, item.InstalledAt);
            hash.AddNullableString(item.OwnerFactionId);
            AddNullableGuid(hash, item.OwnerCreatureGuid);
            hash.AddInt32((int)item.UsePolicy);
            hash.AddBoolean(item.Forbidden);
            hash.AddInt32(item.QualityTier);
            hash.AddBoolean(item.Artifact);
            hash.AddNullableString(item.ArtifactName);
            hash.AddString(item.ConditionState);
            AddNullableInt32(hash, item.DurabilityCurrent);
            AddNullableInt32(hash, item.DurabilityMax);
            AddNullableGuid(hash, item.CraftedBy);
            hash.AddNullableString(item.MakerFactionId);
            hash.AddNullableString(item.StyleTag);
            hash.AddUInt64(item.SpawnedAtTick);
            AddReservationTokensHash(hash, item.ReservationTokens);
            AddImprovementsHash(hash, item.Improvements);
            AddPerishableHash(hash, item.Perishable);
        }
    }

    private static void AddCreaturesHash(ReplayHashBuilder hash, IEnumerable<CreatureInstance> creatures)
    {
        var ordered = creatures
            .OrderBy(creature => creature.Guid)
            .ToArray();

        hash.AddInt32(ordered.Length);
        foreach (var creature in ordered)
        {
            hash.AddGuid(creature.Guid);
            hash.AddString(creature.DefinitionId);
            hash.AddString(creature.FactionId);
            AddPointHash(hash, creature.Position);
            hash.AddInt32(creature.Z);
            hash.AddInt32(creature.HP);
            hash.AddInt32(creature.MaxHP);
            hash.AddUInt64(creature.SpawnedAtTick);
        }
    }

    private static void AddReservationsHash(ReplayHashBuilder hash, SimulationWorld world)
    {
        var itemReservations = world.Reservations.GetItemReservationsSnapshot()
            .OrderBy(reservation => reservation.itemId)
            .ThenBy(reservation => reservation.holderId)
            .ToArray();
        hash.AddInt32(itemReservations.Length);
        foreach (var reservation in itemReservations)
        {
            hash.AddGuid(reservation.itemId);
            hash.AddGuid(reservation.holderId);
            hash.AddUInt64(reservation.expireTick);
        }

        var creatureReservations = world.Reservations.GetCreatureReservationsSnapshot()
            .OrderBy(reservation => reservation.workerId)
            .ThenBy(reservation => reservation.holderSystem, StringComparer.Ordinal)
            .ThenBy(reservation => reservation.jobId, StringComparer.Ordinal)
            .ToArray();
        hash.AddInt32(creatureReservations.Length);
        foreach (var reservation in creatureReservations)
        {
            hash.AddGuid(reservation.workerId);
            hash.AddString(reservation.holderSystem);
            hash.AddNullableString(reservation.jobId);
            hash.AddUInt64(reservation.expireTick);
        }
    }

    private static void AddStockpileZonesHash(ReplayHashBuilder hash, IEnumerable<StockpileZone> zones)
    {
        var ordered = zones
            .OrderBy(zone => zone.ZoneId)
            .ToArray();

        hash.AddInt32(ordered.Length);
        foreach (var zone in ordered)
        {
            hash.AddInt32(zone.ZoneId);
            hash.AddString(zone.Name);
            AddChunkKeyHash(hash, zone.HomeChunk);
            hash.AddInt32((int)zone.Filter.Mode);
            AddStringSetHash(hash, zone.Filter.Tags);
            AddStringSetHash(hash, zone.Filter.ItemIds);
            AddStringSetHash(hash, zone.Filter.Materials);
            hash.AddInt32(zone.Priority);
            hash.AddInt32(zone.TargetStacks);
            hash.AddInt32(zone.HysteresisLow);
            hash.AddInt32(zone.HysteresisHigh);
            hash.AddInt32((int)zone.Generation);
            hash.AddUInt64(zone.CreatedTick);
            var memberChunks = zone.MemberChunks
                .OrderBy(chunk => chunk.Z)
                .ThenBy(chunk => chunk.ChunkY)
                .ThenBy(chunk => chunk.ChunkX)
                .ToArray();
            hash.AddInt32(memberChunks.Length);
            foreach (var chunk in memberChunks)
            {
                AddChunkKeyHash(hash, chunk);
            }
        }
    }

    private static void AddReservationTokensHash(ReplayHashBuilder hash, IEnumerable<ReservationToken> reservations)
    {
        var ordered = reservations
            .OrderBy(token => token.JobGuid)
            .ThenBy(token => token.ClaimantCreatureGuid)
            .ThenBy(token => token.ReservationType, StringComparer.Ordinal)
            .ToArray();

        hash.AddInt32(ordered.Length);
        foreach (var token in ordered)
        {
            hash.AddGuid(token.JobGuid);
            AddNullableGuid(hash, token.ClaimantCreatureGuid);
            hash.AddInt32(token.ReservedCount);
            hash.AddUInt64(token.ExpiresAtTick);
            hash.AddString(token.ReservationType);
        }
    }

    private static void AddPlacementHash(ReplayHashBuilder hash, PlacementData? placement)
    {
        hash.AddBoolean(placement != null);
        if (placement == null)
            return;

        AddPointHash(hash, placement.AnchorWorld);
        hash.AddInt32(placement.Z);
        hash.AddInt32(placement.Rotation);
        hash.AddNullableString(placement.StateId);
    }

    private static void AddImprovementsHash(ReplayHashBuilder hash, IReadOnlyList<Improvement>? improvements)
    {
        hash.AddBoolean(improvements != null);
        if (improvements == null)
            return;

        var ordered = improvements
            .OrderBy(improvement => improvement.Type, StringComparer.Ordinal)
            .ThenBy(improvement => improvement.MaterialId, StringComparer.Ordinal)
            .ThenBy(improvement => improvement.QualityTier)
            .ThenBy(improvement => improvement.CreatedBy)
            .ThenBy(improvement => improvement.Description, StringComparer.Ordinal)
            .ToArray();

        hash.AddInt32(ordered.Length);
        foreach (var improvement in ordered)
        {
            hash.AddString(improvement.Type);
            hash.AddNullableString(improvement.MaterialId);
            hash.AddInt32(improvement.QualityTier);
            AddNullableGuid(hash, improvement.CreatedBy);
            hash.AddNullableString(improvement.Description);
        }
    }

    private static void AddPerishableHash(ReplayHashBuilder hash, PerishableState? perishable)
    {
        hash.AddBoolean(perishable != null);
        if (perishable == null)
            return;

        hash.AddUInt64(perishable.CreatedAtTick);
        hash.AddInt32(perishable.FreshDurationTicks);
        hash.AddInt32(perishable.SpoilDurationTicks);
        hash.AddInt32(BitConverter.SingleToInt32Bits(perishable.CurrentFreshness));
    }

    private static void AddPointHash(ReplayHashBuilder hash, Point point)
    {
        hash.AddInt32(point.X);
        hash.AddInt32(point.Y);
    }

    private static void AddChunkKeyHash(ReplayHashBuilder hash, ChunkKey key)
    {
        hash.AddInt32(key.ChunkX);
        hash.AddInt32(key.ChunkY);
        hash.AddInt32(key.Z);
    }

    private static void AddStringSetHash(ReplayHashBuilder hash, IEnumerable<string> values)
    {
        var ordered = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Order(StringComparer.Ordinal)
            .ToArray();

        hash.AddInt32(ordered.Length);
        foreach (var value in ordered)
        {
            hash.AddString(value);
        }
    }

    private static void AddNullableGuid(ReplayHashBuilder hash, Guid? value)
    {
        hash.AddBoolean(value.HasValue);
        if (value.HasValue)
        {
            hash.AddGuid(value.Value);
        }
    }

    private static void AddNullableInt32(ReplayHashBuilder hash, int? value)
    {
        hash.AddBoolean(value.HasValue);
        if (value.HasValue)
        {
            hash.AddInt32(value.Value);
        }
    }
}
