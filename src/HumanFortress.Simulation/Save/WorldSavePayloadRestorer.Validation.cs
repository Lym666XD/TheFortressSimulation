using HumanFortress.Contracts.Simulation.Save;
using HumanFortress.Simulation.World;

namespace HumanFortress.Simulation.Save;

internal static partial class WorldSavePayloadRestorer
{
    private static void ValidatePayload(WorldSavePayloadData payload, ICollection<string> issues)
    {
        if (payload.SchemaVersion != WorldSavePayloadFormat.CurrentVersion)
        {
            issues.Add($"Unsupported world payload schema version {payload.SchemaVersion}.");
        }

        if (payload.SizeInChunks < 2 || payload.SizeInChunks > 8)
        {
            issues.Add($"World payload size in chunks {payload.SizeInChunks} is outside the supported range.");
        }

        if (payload.SizeInTiles != payload.SizeInChunks * Chunk.SIZE_XY)
        {
            issues.Add("World payload tile dimensions do not match chunk dimensions.");
        }

        if (payload.MaxZ <= 0)
        {
            issues.Add($"World payload max Z {payload.MaxZ} must be positive.");
        }

        if (string.IsNullOrWhiteSpace(payload.ReplayHash))
        {
            issues.Add("World payload replay hash is blank.");
        }

        if (payload.Chunks == null)
        {
            issues.Add("World payload chunks are missing.");
            return;
        }

        if (payload.Counts.ChunkCount != payload.Chunks.Length)
        {
            issues.Add("World payload chunk count does not match payload chunks.");
        }

        if (payload.Items == null)
        {
            issues.Add("World payload items are missing.");
        }
        else if (payload.Counts.ItemCount != payload.Items.Length)
        {
            issues.Add("World payload item count does not match payload items.");
        }
        else
        {
            ValidateUniquePayloadIds(
                payload.Items,
                static item => item.Guid,
                "item",
                issues);
        }

        if (payload.Creatures == null)
        {
            issues.Add("World payload creatures are missing.");
        }
        else if (payload.Counts.CreatureCount != payload.Creatures.Length)
        {
            issues.Add("World payload creature count does not match payload creatures.");
        }
        else
        {
            ValidateUniquePayloadIds(
                payload.Creatures,
                static creature => creature.Guid,
                "creature",
                issues);
        }

        if (payload.ItemReservations == null)
        {
            issues.Add("World payload item reservations are missing.");
        }
        else if (payload.Counts.ItemReservationCount != payload.ItemReservations.Length)
        {
            issues.Add("World payload item reservation count does not match payload item reservations.");
        }

        if (payload.CreatureReservations == null)
        {
            issues.Add("World payload creature reservations are missing.");
        }
        else if (payload.Counts.CreatureReservationCount != payload.CreatureReservations.Length)
        {
            issues.Add("World payload creature reservation count does not match payload creature reservations.");
        }

        if (payload.StockpileZones == null)
        {
            issues.Add("World payload stockpile zones are missing.");
        }
        else if (payload.Counts.StockpileZoneCount != payload.StockpileZones.Length)
        {
            issues.Add("World payload stockpile zone count does not match payload stockpile zones.");
        }

        if (payload.Placeables == null)
        {
            issues.Add("World payload placeables are missing.");
        }
        else if (payload.Counts.OwnedPlaceableCount != payload.Placeables.Length)
        {
            issues.Add("World payload placeable count does not match payload placeables.");
        }

        if (payload.MiningOrders == null)
        {
            issues.Add("World payload mining orders are missing.");
        }
        else if (payload.Counts.MiningOrderCount != payload.MiningOrders.Length)
        {
            issues.Add("World payload mining order count does not match payload mining orders.");
        }

        if (payload.HaulOrders == null)
        {
            issues.Add("World payload haul orders are missing.");
        }
        else if (payload.Counts.HaulOrderCount != payload.HaulOrders.Length)
        {
            issues.Add("World payload haul order count does not match payload haul orders.");
        }

        if (payload.ConstructionOrders == null)
        {
            issues.Add("World payload construction orders are missing.");
        }
        else if (payload.Counts.ConstructionOrderCount != payload.ConstructionOrders.Length)
        {
            issues.Add("World payload construction order count does not match payload construction orders.");
        }

        if (payload.BuildableOrders == null)
        {
            issues.Add("World payload buildable orders are missing.");
        }
        else if (payload.Counts.BuildableOrderCount != payload.BuildableOrders.Length)
        {
            issues.Add("World payload buildable order count does not match payload buildable orders.");
        }

        var seen = new HashSet<ChunkKey>();
        var tileCount = 0;
        foreach (var chunk in payload.Chunks)
        {
            var key = new ChunkKey(chunk.ChunkX, chunk.ChunkY, chunk.Z);
            if (!seen.Add(key))
            {
                issues.Add($"World payload contains duplicate chunk {chunk.ChunkX},{chunk.ChunkY},{chunk.Z}.");
            }

            if (chunk.ChunkX < 0 || chunk.ChunkX >= payload.SizeInChunks
                || chunk.ChunkY < 0 || chunk.ChunkY >= payload.SizeInChunks
                || chunk.Z < 0 || chunk.Z >= payload.MaxZ)
            {
                issues.Add($"World payload chunk {chunk.ChunkX},{chunk.ChunkY},{chunk.Z} is outside world bounds.");
            }

            if (chunk.Tiles == null)
            {
                issues.Add($"World payload chunk {chunk.ChunkX},{chunk.ChunkY},{chunk.Z} is missing tiles.");
                continue;
            }

            if (chunk.Tiles.Length != Chunk.CELLS_PER_LAYER)
            {
                issues.Add($"World payload chunk {chunk.ChunkX},{chunk.ChunkY},{chunk.Z} has {chunk.Tiles.Length} tiles instead of {Chunk.CELLS_PER_LAYER}.");
            }

            tileCount += chunk.Tiles.Length;
        }

        if (payload.Counts.TileCount != tileCount)
        {
            issues.Add("World payload tile count does not match payload tiles.");
        }
    }

    private static void ValidateUniquePayloadIds<T>(
        IReadOnlyList<T> rows,
        Func<T, Guid> getGuid,
        string label,
        ICollection<string> issues)
    {
        var seen = new HashSet<Guid>();
        for (var i = 0; i < rows.Count; i++)
        {
            var guid = getGuid(rows[i]);
            if (guid == Guid.Empty)
            {
                issues.Add($"World payload {label}[{i}] has an empty guid.");
                continue;
            }

            if (!seen.Add(guid))
            {
                issues.Add($"World payload {label}[{i}] duplicates guid {guid}.");
            }
        }
    }

    private static void ValidateSupportedSections(
        WorldSavePayloadData payload,
        bool restoreSupportedState,
        ICollection<string> issues)
    {
        if (!restoreSupportedState
            && (payload.Counts.ItemCount != 0
                || payload.Counts.CreatureCount != 0
                || payload.Counts.ItemReservationCount != 0
                || payload.Counts.CreatureReservationCount != 0
                || payload.Counts.StockpileZoneCount != 0
                || payload.Counts.OwnedPlaceableCount != 0
                || payload.Counts.MiningOrderCount != 0
                || payload.Counts.HaulOrderCount != 0
                || payload.Counts.ConstructionOrderCount != 0
                || payload.Counts.BuildableOrderCount != 0))
        {
            issues.Add("World payload restore currently supports terrain-only worlds; non-terrain world sections are present.");
        }

        if (restoreSupportedState)
        {
            ValidateSupportedItemSlice(payload, issues);
            ValidateReservationReferences(payload, issues);
            ValidateStockpileZonePayloads(payload, issues);
            ValidateOrderPayloads(payload, issues);
        }
    }

    private static void ValidateSupportedItemSlice(
        WorldSavePayloadData payload,
        ICollection<string> issues)
    {
        if (payload.Items == null)
            return;

        for (var i = 0; i < payload.Items.Length; i++)
        {
            var item = payload.Items[i];
            if (item.ContainedBy.HasValue
                || item.CarriedBy.HasValue
                || item.EquippedBy.HasValue
                || item.InstalledAt.HasValue)
            {
                issues.Add($"World item payload[{i}] is not a ground item; carried, contained, equipped, and installed item restore is not supported yet.");
            }

            if (item.ReservationTokens is { Length: > 0 })
            {
                issues.Add($"World item payload[{i}] has item-local reservation tokens; reservation restore is not supported yet.");
            }
        }
    }

    private static void ValidateReservationReferences(
        WorldSavePayloadData payload,
        ICollection<string> issues)
    {
        if (payload.Items != null && payload.ItemReservations != null)
        {
            var itemIds = payload.Items.Select(item => item.Guid).ToHashSet();
            for (var i = 0; i < payload.ItemReservations.Length; i++)
            {
                if (!itemIds.Contains(payload.ItemReservations[i].ItemId))
                {
                    issues.Add($"World item reservation payload[{i}] references missing item {payload.ItemReservations[i].ItemId}.");
                }
            }
        }

        if (payload.Creatures != null && payload.CreatureReservations != null)
        {
            var creatureIds = payload.Creatures.Select(creature => creature.Guid).ToHashSet();
            for (var i = 0; i < payload.CreatureReservations.Length; i++)
            {
                if (!creatureIds.Contains(payload.CreatureReservations[i].WorkerId))
                {
                    issues.Add($"World creature reservation payload[{i}] references missing creature {payload.CreatureReservations[i].WorkerId}.");
                }
            }
        }
    }

    private static void ValidateStockpileZonePayloads(
        WorldSavePayloadData payload,
        ICollection<string> issues)
    {
        if (payload.StockpileZones == null
            || payload.Counts.StockpileZoneCount != payload.StockpileZones.Length)
        {
            return;
        }

        var seenZoneIds = new HashSet<int>();
        for (var i = 0; i < payload.StockpileZones.Length; i++)
        {
            var zone = payload.StockpileZones[i];
            var prefix = $"World stockpile zone payload[{i}]";

            if (zone.ZoneId <= 0)
            {
                issues.Add($"{prefix} has non-positive zone id {zone.ZoneId}.");
            }
            else if (!seenZoneIds.Add(zone.ZoneId))
            {
                issues.Add($"{prefix} duplicates zone id {zone.ZoneId}.");
            }

            if (string.IsNullOrWhiteSpace(zone.Name))
            {
                issues.Add($"{prefix} has a blank name.");
            }

            ValidateWorldChunkKey(zone.HomeChunk, $"{prefix} home chunk", payload, issues);
            ValidateStockpileFilterArrays(zone.Filter, prefix, issues);

            if (zone.MemberChunks == null)
            {
                issues.Add($"{prefix} member chunks are missing.");
            }
            else
            {
                var seenMemberChunks = new HashSet<ChunkKey>();
                for (var j = 0; j < zone.MemberChunks.Length; j++)
                {
                    var memberKey = zone.MemberChunks[j];
                    ValidateWorldChunkKey(memberKey, $"{prefix} member chunk[{j}]", payload, issues);
                    if (!seenMemberChunks.Add(new ChunkKey(memberKey.ChunkX, memberKey.ChunkY, memberKey.Z)))
                    {
                        issues.Add($"{prefix} member chunk[{j}] duplicates chunk {memberKey.ChunkX},{memberKey.ChunkY},{memberKey.Z}.");
                    }
                }
            }
        }
    }

    private static void ValidateStockpileFilterArrays(
        WorldSaveStockpileFilterPayloadData filter,
        string prefix,
        ICollection<string> issues)
    {
        ValidateStringArray(filter.Tags, $"{prefix} filter tags", issues);
        ValidateStringArray(filter.ItemIds, $"{prefix} filter item ids", issues);
        ValidateStringArray(filter.Materials, $"{prefix} filter materials", issues);
    }

    private static void ValidateStringArray(
        string[]? values,
        string label,
        ICollection<string> issues)
    {
        if (values == null)
        {
            issues.Add($"{label} are missing.");
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < values.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(values[i]))
            {
                issues.Add($"{label}[{i}] is blank.");
            }
            else if (!seen.Add(values[i]))
            {
                issues.Add($"{label}[{i}] duplicates '{values[i]}'.");
            }
        }
    }

    private static void ValidateOrderPayloads(
        WorldSavePayloadData payload,
        ICollection<string> issues)
    {
        ValidateMiningOrderPayloads(payload, issues);
        ValidateHaulOrderPayloads(payload, issues);
        ValidateConstructionOrderPayloads(payload, issues);
        ValidateBuildableOrderPayloads(payload, issues);
    }

    private static void ValidateMiningOrderPayloads(
        WorldSavePayloadData payload,
        ICollection<string> issues)
    {
        if (payload.MiningOrders == null
            || payload.Counts.MiningOrderCount != payload.MiningOrders.Length)
        {
            return;
        }

        var seen = new HashSet<int>();
        for (var i = 0; i < payload.MiningOrders.Length; i++)
        {
            var order = payload.MiningOrders[i];
            var prefix = $"World mining order payload[{i}]";

            if (order.Id <= 0)
            {
                issues.Add($"{prefix} has non-positive id {order.Id}.");
            }
            else if (!seen.Add(order.Id))
            {
                issues.Add($"{prefix} duplicates mining id {order.Id}.");
            }

            ValidateWorldRectangle(order.Rect, prefix, payload, issues);
            ValidateWorldZRange(order.ZMin, order.ZMax, prefix, payload, issues);
        }
    }

    private static void ValidateHaulOrderPayloads(
        WorldSavePayloadData payload,
        ICollection<string> issues)
    {
        if (payload.HaulOrders == null
            || payload.Counts.HaulOrderCount != payload.HaulOrders.Length)
        {
            return;
        }

        for (var i = 0; i < payload.HaulOrders.Length; i++)
        {
            var order = payload.HaulOrders[i];
            var prefix = $"World haul order payload[{i}]";
            ValidateWorldRectangle(order.WorldRect, prefix, payload, issues);
            ValidateWorldZ(order.Z, prefix, payload, issues);
        }
    }

    private static void ValidateConstructionOrderPayloads(
        WorldSavePayloadData payload,
        ICollection<string> issues)
    {
        if (payload.ConstructionOrders == null
            || payload.Counts.ConstructionOrderCount != payload.ConstructionOrders.Length)
        {
            return;
        }

        for (var i = 0; i < payload.ConstructionOrders.Length; i++)
        {
            var order = payload.ConstructionOrders[i];
            var prefix = $"World construction order payload[{i}]";
            ValidateWorldRectangle(order.WorldRect, prefix, payload, issues);
            ValidateWorldZRange(order.ZMin, order.ZMax, prefix, payload, issues);
            ValidateMaterialFilter(order.Filter, prefix, issues);
        }
    }

    private static void ValidateBuildableOrderPayloads(
        WorldSavePayloadData payload,
        ICollection<string> issues)
    {
        if (payload.BuildableOrders == null
            || payload.Counts.BuildableOrderCount != payload.BuildableOrders.Length)
        {
            return;
        }

        for (var i = 0; i < payload.BuildableOrders.Length; i++)
        {
            var order = payload.BuildableOrders[i];
            var prefix = $"World buildable order payload[{i}]";

            if (string.IsNullOrWhiteSpace(order.ConstructionId))
            {
                issues.Add($"{prefix} has a blank construction id.");
            }

            ValidateWorldPoint(order.Anchor, $"{prefix} anchor", payload, issues);
            ValidateWorldZ(order.Z, prefix, payload, issues);
        }
    }

    private static void ValidateMaterialFilter(
        WorldSaveMaterialFilterPayloadData filter,
        string prefix,
        ICollection<string> issues)
    {
        if (string.IsNullOrWhiteSpace(filter.CategoryKey))
        {
            issues.Add($"{prefix} has a blank material filter category key.");
        }

        ValidateStringArray(filter.Tags, $"{prefix} material filter tags", issues);
    }

    private static void ValidateWorldRectangle(
        WorldSaveRectangleData rectangle,
        string prefix,
        WorldSavePayloadData payload,
        ICollection<string> issues)
    {
        if (rectangle.Width <= 0 || rectangle.Height <= 0)
        {
            issues.Add($"{prefix} has non-positive rectangle dimensions.");
            return;
        }

        var right = (long)rectangle.X + rectangle.Width;
        var bottom = (long)rectangle.Y + rectangle.Height;
        if (rectangle.X < 0
            || rectangle.Y < 0
            || right > payload.SizeInTiles
            || bottom > payload.SizeInTiles)
        {
            issues.Add($"{prefix} rectangle is outside world bounds.");
        }
    }

    private static void ValidateWorldPoint(
        WorldSavePointData point,
        string prefix,
        WorldSavePayloadData payload,
        ICollection<string> issues)
    {
        if (point.X < 0
            || point.Y < 0
            || point.X >= payload.SizeInTiles
            || point.Y >= payload.SizeInTiles)
        {
            issues.Add($"{prefix} is outside world bounds.");
        }
    }

    private static void ValidateWorldChunkKey(
        WorldSaveChunkKeyData chunk,
        string prefix,
        WorldSavePayloadData payload,
        ICollection<string> issues)
    {
        if (chunk.ChunkX < 0
            || chunk.ChunkY < 0
            || chunk.Z < 0
            || chunk.ChunkX >= payload.SizeInChunks
            || chunk.ChunkY >= payload.SizeInChunks
            || chunk.Z >= payload.MaxZ)
        {
            issues.Add($"{prefix} is outside world bounds.");
        }
    }

    private static void ValidateWorldZRange(
        int zMin,
        int zMax,
        string prefix,
        WorldSavePayloadData payload,
        ICollection<string> issues)
    {
        if (zMin > zMax)
        {
            issues.Add($"{prefix} has zMin greater than zMax.");
            return;
        }

        ValidateWorldZ(zMin, $"{prefix} zMin", payload, issues);
        ValidateWorldZ(zMax, $"{prefix} zMax", payload, issues);
    }

    private static void ValidateWorldZ(
        int z,
        string prefix,
        WorldSavePayloadData payload,
        ICollection<string> issues)
    {
        if (z < 0 || z >= payload.MaxZ)
        {
            issues.Add($"{prefix} is outside world z bounds.");
        }
    }
}
