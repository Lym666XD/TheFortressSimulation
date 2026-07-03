using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Contracts.Simulation.Save;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Placeables;
using HumanFortress.Simulation.Replay;
using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;
using SimulationWorld = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Save;

internal static class WorldSavePayloadRestorer
{
    internal static WorldSavePayloadRestoreResult RestoreTerrainOnly(WorldSavePayloadData payload)
    {
        return Restore(payload, restoreSupportedState: false);
    }

    internal static WorldSavePayloadRestoreResult RestoreTerrainAndItems(WorldSavePayloadData payload)
    {
        return RestoreSupportedSections(payload);
    }

    internal static WorldSavePayloadRestoreResult RestoreSupportedSections(WorldSavePayloadData payload)
    {
        return Restore(payload, restoreSupportedState: true);
    }

    private static WorldSavePayloadRestoreResult Restore(
        WorldSavePayloadData payload,
        bool restoreSupportedState)
    {
        var issues = new List<string>();
        ValidatePayload(payload, issues);
        ValidateSupportedSections(payload, restoreSupportedState, issues);

        if (issues.Count > 0)
            return Failed(payload, issues);

        var world = new SimulationWorld(payload.SizeInChunks, payload.MaxZ);
        foreach (var chunkPayload in payload.Chunks)
        {
            var chunk = world.GetOrCreateChunk(new ChunkKey(chunkPayload.ChunkX, chunkPayload.ChunkY, chunkPayload.Z));
            for (var i = 0; i < chunkPayload.Tiles.Length; i++)
            {
                var (x, y) = Chunk.IndexToLocal(i);
                chunk.SetTile(x, y, ToTileBase(chunkPayload.Tiles[i]), tick: 0);
            }
        }

        if (restoreSupportedState)
        {
            world.Items.SetDependencies(world);
            var itemIssues = world.Items.RestoreItemsSnapshot(payload.Items);
            issues.AddRange(itemIssues);
            var creatureIssues = world.Creatures.RestoreCreaturesSnapshot(payload.Creatures);
            issues.AddRange(creatureIssues);
            var reservationIssues = world.Reservations.RestoreSnapshot(
                payload.ItemReservations,
                payload.CreatureReservations);
            issues.AddRange(reservationIssues);
            var stockpileIssues = world.Stockpiles.RestoreZonesSnapshot(payload.StockpileZones);
            issues.AddRange(stockpileIssues);
            var placeableIssues = RestorePlaceablesSnapshot(world, payload.Placeables);
            issues.AddRange(placeableIssues);
            var orderIssues = world.Orders.RestoreActiveSnapshot(
                payload.MiningOrders,
                payload.HaulOrders,
                payload.ConstructionOrders,
                payload.BuildableOrders);
            issues.AddRange(orderIssues);

            if (issues.Count > 0)
                return FailedAfterPartialRestore(payload, world, issues);
        }

        var restoredSnapshot = WorldSaveSnapshotBuilder.Build(world);
        if (!string.Equals(restoredSnapshot.ReplayHash, payload.ReplayHash, StringComparison.Ordinal))
        {
            issues.Add("Restored world hash does not match saved world hash.");
            return new WorldSavePayloadRestoreResult(
                success: false,
                world: null,
                savedWorldHash: payload.ReplayHash ?? string.Empty,
                restoredWorldHash: restoredSnapshot.ReplayHash,
                restoredChunkCount: restoredSnapshot.Counts.ChunkCount,
                restoredTileCount: restoredSnapshot.Counts.TileCount,
                issues);
        }

        return new WorldSavePayloadRestoreResult(
            success: true,
            world,
            payload.ReplayHash,
            restoredSnapshot.ReplayHash,
            restoredSnapshot.Counts.ChunkCount,
            restoredSnapshot.Counts.TileCount,
            Array.Empty<string>());
    }

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

        if (payload.Creatures == null)
        {
            issues.Add("World payload creatures are missing.");
        }
        else if (payload.Counts.CreatureCount != payload.Creatures.Length)
        {
            issues.Add("World payload creature count does not match payload creatures.");
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

    private static IReadOnlyList<string> RestorePlaceablesSnapshot(
        SimulationWorld world,
        WorldSavePlaceablePayloadData[]? placeables)
    {
        var issues = new List<string>();
        if (placeables == null)
        {
            issues.Add("World payload placeables are missing.");
            return issues;
        }

        ValidatePlaceableRows(world, placeables, issues);
        if (issues.Count > 0)
            return issues;

        foreach (var payload in placeables
                     .OrderBy(placeable => placeable.Guid)
                     .ThenBy(placeable => placeable.OwnerChunk.Z)
                     .ThenBy(placeable => placeable.OwnerChunk.ChunkY)
                     .ThenBy(placeable => placeable.OwnerChunk.ChunkX)
                     .ThenBy(placeable => placeable.OwnerLocalIndex))
        {
            PlaceableManager.PlacePlaceable(
                world,
                ToPlaceableInstance(payload),
                tick: 0);
        }

        return issues;
    }

    private static void ValidatePlaceableRows(
        SimulationWorld world,
        IReadOnlyList<WorldSavePlaceablePayloadData> placeables,
        ICollection<string> issues)
    {
        var seenGuids = new HashSet<Guid>();
        var seenOwners = new HashSet<(int ChunkX, int ChunkY, int Z, int LocalIndex)>();
        var occupiedCells = new HashSet<(int X, int Y, int Z)>();

        for (var i = 0; i < placeables.Count; i++)
        {
            var placeable = placeables[i];
            if (placeable.Guid == Guid.Empty)
                issues.Add($"World placeable payload[{i}] has an empty guid.");
            else if (!seenGuids.Add(placeable.Guid))
                issues.Add($"World placeable payload[{i}] duplicates placeable {placeable.Guid}.");

            if (string.IsNullOrWhiteSpace(placeable.DefinitionId))
                issues.Add($"World placeable payload[{i}] has a blank definition id.");

            if (!Enum.IsDefined(typeof(PlaceableKind), placeable.Kind))
                issues.Add($"World placeable payload[{i}] has invalid kind {placeable.Kind}.");

            if (!Enum.IsDefined(typeof(PassabilityMode), placeable.Passability))
                issues.Add($"World placeable payload[{i}] has invalid passability {placeable.Passability}.");

            if (placeable.Footprint.W <= 0 || placeable.Footprint.D <= 0 || placeable.Footprint.H <= 0)
                issues.Add($"World placeable payload[{i}] has an invalid footprint.");

            if (placeable.MaxHitPoints < 0
                || placeable.HitPoints < 0
                || (placeable.MaxHitPoints > 0 && placeable.HitPoints > placeable.MaxHitPoints))
            {
                issues.Add($"World placeable payload[{i}] has invalid hit points.");
            }

            ValidateOwnerStorage(world, placeable, i, seenOwners, issues);
            ValidateFootprintCells(world, placeable, i, occupiedCells, issues);
            ValidateConstructionSite(placeable.ConstructionSite, i, issues);
            ValidateWorkshop(placeable.Workshop, i, issues);
        }
    }

    private static void ValidateOwnerStorage(
        SimulationWorld world,
        WorldSavePlaceablePayloadData placeable,
        int index,
        ISet<(int ChunkX, int ChunkY, int Z, int LocalIndex)> seenOwners,
        ICollection<string> issues)
    {
        if (placeable.OwnerChunk.ChunkX < 0
            || placeable.OwnerChunk.ChunkX >= world.SizeInChunks
            || placeable.OwnerChunk.ChunkY < 0
            || placeable.OwnerChunk.ChunkY >= world.SizeInChunks
            || placeable.OwnerChunk.Z < 0
            || placeable.OwnerChunk.Z >= world.MaxZ)
        {
            issues.Add($"World placeable payload[{index}] owner chunk is outside world bounds.");
            return;
        }

        if (placeable.OwnerLocalIndex < 0 || placeable.OwnerLocalIndex >= Chunk.CELLS_PER_LAYER)
        {
            issues.Add($"World placeable payload[{index}] owner local index is outside chunk bounds.");
            return;
        }

        if (world.GetChunk(new ChunkKey(
                placeable.OwnerChunk.ChunkX,
                placeable.OwnerChunk.ChunkY,
                placeable.OwnerChunk.Z)) == null)
        {
            issues.Add($"World placeable payload[{index}] owner chunk is missing from the restored terrain payload.");
            return;
        }

        if (!seenOwners.Add((
                placeable.OwnerChunk.ChunkX,
                placeable.OwnerChunk.ChunkY,
                placeable.OwnerChunk.Z,
                placeable.OwnerLocalIndex)))
        {
            issues.Add($"World placeable payload[{index}] duplicates owner storage cell.");
        }

        if (!world.IsValidPosition(placeable.Position.X, placeable.Position.Y, placeable.Z))
        {
            issues.Add($"World placeable payload[{index}] anchor is outside world bounds.");
            return;
        }

        var expectedChunkX = placeable.Position.X / Chunk.SIZE_XY;
        var expectedChunkY = placeable.Position.Y / Chunk.SIZE_XY;
        var expectedLocalIndex = Chunk.LocalIndex(
            placeable.Position.X % Chunk.SIZE_XY,
            placeable.Position.Y % Chunk.SIZE_XY);
        if (placeable.OwnerChunk.ChunkX != expectedChunkX
            || placeable.OwnerChunk.ChunkY != expectedChunkY
            || placeable.OwnerChunk.Z != placeable.Z
            || placeable.OwnerLocalIndex != expectedLocalIndex)
        {
            issues.Add($"World placeable payload[{index}] owner storage does not match its anchor position.");
        }
    }

    private static void ValidateFootprintCells(
        SimulationWorld world,
        WorldSavePlaceablePayloadData placeable,
        int index,
        ISet<(int X, int Y, int Z)> occupiedCells,
        ICollection<string> issues)
    {
        if (placeable.Footprint.W <= 0 || placeable.Footprint.D <= 0 || placeable.Footprint.H <= 0)
            return;

        for (var dy = 0; dy < placeable.Footprint.D; dy++)
        {
            for (var dx = 0; dx < placeable.Footprint.W; dx++)
            {
                var x = placeable.Position.X + dx;
                var y = placeable.Position.Y + dy;
                if (!world.IsValidPosition(x, y, placeable.Z))
                {
                    issues.Add($"World placeable payload[{index}] footprint leaves world bounds.");
                    return;
                }

                if (world.GetChunk(new ChunkKey(x / Chunk.SIZE_XY, y / Chunk.SIZE_XY, placeable.Z)) == null)
                {
                    issues.Add($"World placeable payload[{index}] footprint references a chunk missing from the restored terrain payload.");
                    return;
                }

                if (!occupiedCells.Add((x, y, placeable.Z)))
                    issues.Add($"World placeable payload[{index}] overlaps another placeable footprint at {x},{y},{placeable.Z}.");
            }
        }
    }

    private static void ValidateConstructionSite(
        WorldSaveConstructionSitePayloadData? construction,
        int placeableIndex,
        ICollection<string> issues)
    {
        if (construction == null)
            return;

        if (string.IsNullOrWhiteSpace(construction.Value.TargetId))
            issues.Add($"World placeable payload[{placeableIndex}] construction site has a blank target id.");
        if (construction.Value.BuildProgressTicks < 0 || construction.Value.TotalBuildTicks < 0)
            issues.Add($"World placeable payload[{placeableIndex}] construction site has negative progress.");
        if (construction.Value.TotalBuildTicks > 0
            && construction.Value.BuildProgressTicks > construction.Value.TotalBuildTicks)
        {
            issues.Add($"World placeable payload[{placeableIndex}] construction site progress exceeds total build ticks.");
        }

        ValidateStringIntRows(
            construction.Value.MaterialsRequired,
            $"World placeable payload[{placeableIndex}] construction required materials",
            issues);
        ValidateStringIntRows(
            construction.Value.MaterialsDelivered,
            $"World placeable payload[{placeableIndex}] construction delivered materials",
            issues);
    }

    private static void ValidateWorkshop(
        WorldSaveWorkshopPayloadData? workshop,
        int placeableIndex,
        ICollection<string> issues)
    {
        if (workshop == null)
            return;

        if (workshop.Value.MaxWorkers <= 0
            || workshop.Value.AllowedWorkers <= 0
            || workshop.Value.AllowedWorkers > workshop.Value.MaxWorkers
            || workshop.Value.ActiveJobs < 0
            || workshop.Value.ActiveJobs > workshop.Value.MaxWorkers)
        {
            issues.Add($"World placeable payload[{placeableIndex}] workshop worker counts are invalid.");
        }

        if (workshop.Value.Queue == null)
        {
            issues.Add($"World placeable payload[{placeableIndex}] workshop queue is missing.");
            return;
        }

        var seenEntries = new HashSet<Guid>();
        for (var i = 0; i < workshop.Value.Queue.Length; i++)
        {
            var entry = workshop.Value.Queue[i];
            if (entry.EntryId == Guid.Empty)
                issues.Add($"World placeable payload[{placeableIndex}] workshop queue[{i}] has an empty entry id.");
            else if (!seenEntries.Add(entry.EntryId))
                issues.Add($"World placeable payload[{placeableIndex}] workshop queue[{i}] duplicates entry {entry.EntryId}.");

            if (string.IsNullOrWhiteSpace(entry.RecipeId))
                issues.Add($"World placeable payload[{placeableIndex}] workshop queue[{i}] has a blank recipe id.");
            if (string.IsNullOrWhiteSpace(entry.DisplayName))
                issues.Add($"World placeable payload[{placeableIndex}] workshop queue[{i}] has a blank display name.");
            if (!Enum.IsDefined(typeof(CraftQueueStatus), entry.Status))
                issues.Add($"World placeable payload[{placeableIndex}] workshop queue[{i}] has invalid status {entry.Status}.");
        }
    }

    private static void ValidateStringIntRows(
        WorldSaveStringIntData[]? rows,
        string label,
        ICollection<string> issues)
    {
        if (rows == null)
        {
            issues.Add($"{label} are missing.");
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < rows.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(rows[i].Key))
                issues.Add($"{label}[{i}] has a blank key.");
            else if (!seen.Add(rows[i].Key))
                issues.Add($"{label}[{i}] duplicates key '{rows[i].Key}'.");
            if (rows[i].Value < 0)
                issues.Add($"{label}[{i}] has a negative value.");
        }
    }

    private static PlaceableInstance ToPlaceableInstance(WorldSavePlaceablePayloadData payload)
    {
        return new PlaceableInstance(
            payload.Guid,
            (PlaceableKind)payload.Kind,
            payload.DefinitionId,
            ToPoint(payload.Position),
            payload.Z,
            new Footprint(payload.Footprint.W, payload.Footprint.D, payload.Footprint.H))
        {
            SourceItemGuid = payload.SourceItemGuid,
            SourceItemMaterial = payload.SourceItemMaterial,
            SourceItemQuality = payload.SourceItemQuality,
            SourceItemDecorations = ToImprovements(payload.SourceItemDecorations),
            SourceItemMaker = payload.SourceItemMaker,
            Effects = new EffectsBlock
            {
                Beauty = payload.Effects.Beauty,
                Comfort = payload.Effects.Comfort,
                LightLumen = payload.Effects.LightLumen,
                HeatW = payload.Effects.HeatW
            },
            Passability = (PassabilityMode)payload.Passability,
            IsGhost = payload.IsGhost,
            ConstructionSite = ToConstructionSite(payload.ConstructionSite),
            Workshop = payload.Workshop.HasValue
                ? WorkshopState.RestoreSnapshot(payload.Workshop.Value)
                : null,
            DoorState = payload.DoorState.HasValue
                ? new DoorState
                {
                    IsOpen = payload.DoorState.Value.IsOpen,
                    IsLocked = payload.DoorState.Value.IsLocked
                }
                : null,
            OwnerFactionId = payload.OwnerFactionId,
            OwnerCreatureGuid = payload.OwnerCreatureGuid,
            Forbidden = payload.Forbidden,
            HitPoints = payload.HitPoints,
            MaxHitPoints = payload.MaxHitPoints
        };
    }

    private static ConstructionSiteState? ToConstructionSite(
        WorldSaveConstructionSitePayloadData? payload)
    {
        if (!payload.HasValue)
            return null;

        return new ConstructionSiteState
        {
            TargetId = payload.Value.TargetId,
            MaterialsRequired = ToStringIntDictionary(payload.Value.MaterialsRequired),
            MaterialsDelivered = ToStringIntDictionary(payload.Value.MaterialsDelivered),
            BuildProgressTicks = payload.Value.BuildProgressTicks,
            TotalBuildTicks = payload.Value.TotalBuildTicks
        };
    }

    private static Dictionary<string, int> ToStringIntDictionary(WorldSaveStringIntData[]? rows)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        if (rows == null)
            return result;

        foreach (var row in rows)
        {
            result[row.Key] = row.Value;
        }

        return result;
    }

    private static List<Improvement>? ToImprovements(WorldSaveItemImprovementData[]? improvements)
    {
        if (improvements == null)
            return null;

        return improvements
            .Select(improvement => new Improvement
            {
                Type = improvement.Type,
                MaterialId = improvement.MaterialId,
                QualityTier = improvement.QualityTier,
                CreatedBy = improvement.CreatedBy,
                Description = improvement.Description
            })
            .ToList();
    }

    private static Point ToPoint(WorldSavePointData point)
    {
        return new Point(point.X, point.Y);
    }

    private static WorldSavePayloadRestoreResult Failed(
        WorldSavePayloadData payload,
        IReadOnlyList<string> issues)
    {
        return new WorldSavePayloadRestoreResult(
            success: false,
            world: null,
            savedWorldHash: payload.ReplayHash ?? string.Empty,
            restoredWorldHash: string.Empty,
            restoredChunkCount: 0,
            restoredTileCount: 0,
            issues);
    }

    private static WorldSavePayloadRestoreResult FailedAfterPartialRestore(
        WorldSavePayloadData payload,
        SimulationWorld world,
        IReadOnlyList<string> issues)
    {
        var restoredSnapshot = WorldSaveSnapshotBuilder.Build(world);
        return new WorldSavePayloadRestoreResult(
            success: false,
            world: null,
            savedWorldHash: payload.ReplayHash ?? string.Empty,
            restoredWorldHash: restoredSnapshot.ReplayHash,
            restoredChunkCount: restoredSnapshot.Counts.ChunkCount,
            restoredTileCount: restoredSnapshot.Counts.TileCount,
            issues);
    }

    private static TileBase ToTileBase(WorldSaveTilePayloadData tile)
    {
        return new TileBase(
            tile.GeoMatId,
            tile.TerrainBits,
            tile.SurfaceBits,
            tile.FluidKind,
            tile.FluidDepth,
            tile.MetaBits,
            tile.TrafficCost);
    }
}
