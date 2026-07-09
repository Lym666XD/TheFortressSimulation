using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Contracts.Simulation.Save;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Placeables;
using HumanFortress.Simulation.Tiles;
using SadRogue.Primitives;
using SimulationWorld = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Save;

internal static partial class WorldSavePayloadRestorer
{
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

        foreach (var row in rows.OrderBy(static row => row.Key, StringComparer.Ordinal))
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
            .OrderBy(improvement => improvement.Type, StringComparer.Ordinal)
            .ThenBy(improvement => improvement.MaterialId, StringComparer.Ordinal)
            .ThenBy(improvement => improvement.QualityTier)
            .ThenBy(improvement => improvement.CreatedBy)
            .ThenBy(improvement => improvement.Description, StringComparer.Ordinal)
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
