using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Contracts.Simulation.Save;
using HumanFortress.Simulation.Placeables;
using HumanFortress.Simulation.World;
using SimulationWorld = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Save;

internal static partial class WorldSavePayloadRestorer
{
    private static IReadOnlyList<string> ValidatePlaceablesSnapshot(
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
        return issues;
    }

    private static void RestoreValidatedPlaceablesSnapshot(
        SimulationWorld world,
        IReadOnlyList<WorldSavePlaceablePayloadData> placeables)
    {
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
}
