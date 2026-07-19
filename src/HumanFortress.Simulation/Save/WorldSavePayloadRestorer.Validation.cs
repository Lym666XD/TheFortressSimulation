using HumanFortress.Contracts.Simulation.Save;
using HumanFortress.Core.Simulation;
using HumanFortress.Simulation.World;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Save;

internal static partial class WorldSavePayloadRestorer
{
    private static void ValidatePayload(WorldSavePayloadData payload, ICollection<string> issues)
    {
        if (payload.SchemaVersion != WorldSavePayloadFormat.CurrentVersion)
        {
            issues.Add($"Unsupported world payload schema version {payload.SchemaVersion}.");
        }

        if (payload.SizeInChunks < WorldModel.MinSizeInChunks || payload.SizeInChunks > WorldModel.MaxSizeInChunks)
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
        var ownersByEntityKey = new Dictionary<ulong, Guid>();
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
                continue;
            }

            ulong entityKey = DiffTargetEncoding.EntityKey(guid);
            if (ownersByEntityKey.TryGetValue(entityKey, out var existingOwner))
            {
                issues.Add(
                    $"World payload {label}[{i}] guid {guid} collides with {existingOwner} at entity key 0x{entityKey:X16}.");
            }
            else
            {
                ownersByEntityKey.Add(entityKey, guid);
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
}
