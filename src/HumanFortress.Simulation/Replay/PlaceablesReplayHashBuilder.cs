using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Core.Determinism;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Placeables;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;
using SimulationWorld = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Replay;

internal static class PlaceablesReplayHashBuilder
{
    internal static string Build(SimulationWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("placeables.snapshot.v1");
            AppendFields(hash, CaptureOwnedPlaceables(world));
        });
    }

    internal static void Append(ReplayHashBuilder hash, SimulationWorld world)
    {
        ArgumentNullException.ThrowIfNull(hash);
        ArgumentNullException.ThrowIfNull(world);

        hash.AddString("placeables.snapshot.v1");
        AppendFields(hash, CaptureOwnedPlaceables(world));
    }

    private static void AppendFields(
        ReplayHashBuilder hash,
        IReadOnlyList<OwnedPlaceableSnapshot> placeables)
    {
        hash.AddInt32(placeables.Count);
        foreach (var snapshot in placeables)
        {
            AddChunkKeyHash(hash, snapshot.OwnerChunk);
            hash.AddInt32(snapshot.LocalIndex);
            AddPlaceableHash(hash, snapshot.Placeable);
        }
    }

    private static IReadOnlyList<OwnedPlaceableSnapshot> CaptureOwnedPlaceables(SimulationWorld world)
    {
        return world.GetAllChunks()
            .SelectMany(chunk =>
            {
                var placeableData = chunk.GetPlaceableData();
                if (placeableData == null)
                    return Array.Empty<OwnedPlaceableSnapshot>();

                return placeableData.GetOwnedPlaceableSnapshot()
                    .Select(entry => new OwnedPlaceableSnapshot(chunk.Key, entry.LocalIndex, entry.Placeable))
                    .ToArray();
            })
            .OrderBy(snapshot => snapshot.Placeable.Guid)
            .ThenBy(snapshot => snapshot.OwnerChunk.Z)
            .ThenBy(snapshot => snapshot.OwnerChunk.ChunkY)
            .ThenBy(snapshot => snapshot.OwnerChunk.ChunkX)
            .ThenBy(snapshot => snapshot.LocalIndex)
            .ToArray();
    }

    private static void AddPlaceableHash(ReplayHashBuilder hash, PlaceableInstance placeable)
    {
        hash.AddGuid(placeable.Guid);
        hash.AddInt32((int)placeable.Kind);
        hash.AddString(placeable.DefinitionId);
        AddPointHash(hash, placeable.Position);
        hash.AddInt32(placeable.Z);
        AddFootprintHash(hash, placeable.Footprint);
        AddNullableGuid(hash, placeable.SourceItemGuid);
        hash.AddNullableString(placeable.SourceItemMaterial);
        hash.AddInt32(placeable.SourceItemQuality);
        AddImprovementsHash(hash, placeable.SourceItemDecorations);
        AddNullableGuid(hash, placeable.SourceItemMaker);
        AddEffectsHash(hash, placeable.Effects);
        hash.AddInt32((int)placeable.Passability);
        hash.AddBoolean(placeable.IsGhost);
        AddConstructionSiteHash(hash, placeable.ConstructionSite);
        AddWorkshopHash(hash, placeable.Workshop);
        AddDoorStateHash(hash, placeable.DoorState);
        hash.AddNullableString(placeable.OwnerFactionId);
        AddNullableGuid(hash, placeable.OwnerCreatureGuid);
        hash.AddBoolean(placeable.Forbidden);
        hash.AddInt32(placeable.HitPoints);
        hash.AddInt32(placeable.MaxHitPoints);
    }

    private static void AddConstructionSiteHash(ReplayHashBuilder hash, ConstructionSiteState? construction)
    {
        hash.AddBoolean(construction != null);
        if (construction == null)
            return;

        hash.AddString(construction.TargetId);
        AddStringIntMapHash(hash, construction.GetRequiredMaterialsSnapshot());
        AddStringIntMapHash(hash, construction.GetDeliveredMaterialsSnapshot());
        hash.AddInt32(construction.BuildProgressTicks);
        hash.AddInt32(construction.TotalBuildTicks);
    }

    private static void AddWorkshopHash(ReplayHashBuilder hash, WorkshopState? workshop)
    {
        hash.AddBoolean(workshop != null);
        if (workshop == null)
            return;

        hash.AddBoolean(workshop.AutoRequestMaterials);
        hash.AddBoolean(workshop.AutoStockpileOutputs);
        hash.AddInt32(workshop.AllowedWorkers);
        hash.AddInt32(workshop.MaxWorkers);
        hash.AddInt32(workshop.ActiveJobs);
        hash.AddUInt64(workshop.NextEntrySequence);
        hash.AddInt32(workshop.Queue.Count);
        foreach (var entry in workshop.Queue)
        {
            hash.AddGuid(entry.EntryId);
            hash.AddString(entry.RecipeId);
            hash.AddInt32((int)entry.Status);
            hash.AddBoolean(entry.HasPendingRequests);
            hash.AddUInt64(entry.LastRequestTick);
            AddNullableGuid(hash, entry.ActiveWorkerId);
            hash.AddBoolean(entry.IsScheduled);
            hash.AddNullableString(entry.BlockingReason);
        }
    }

    private static void AddDoorStateHash(ReplayHashBuilder hash, DoorState? door)
    {
        hash.AddBoolean(door != null);
        if (door == null)
            return;

        hash.AddBoolean(door.IsOpen);
        hash.AddBoolean(door.IsLocked);
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

    private static void AddEffectsHash(ReplayHashBuilder hash, EffectsBlock effects)
    {
        hash.AddInt32(effects.Beauty);
        hash.AddInt32(effects.Comfort);
        hash.AddInt32(effects.LightLumen);
        hash.AddInt32(effects.HeatW);
    }

    private static void AddStringIntMapHash(ReplayHashBuilder hash, IEnumerable<KeyValuePair<string, int>> values)
    {
        var ordered = values
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .ToArray();

        hash.AddInt32(ordered.Length);
        foreach (var entry in ordered)
        {
            hash.AddString(entry.Key);
            hash.AddInt32(entry.Value);
        }
    }

    private static void AddFootprintHash(ReplayHashBuilder hash, Footprint footprint)
    {
        hash.AddInt32(footprint.W);
        hash.AddInt32(footprint.D);
        hash.AddInt32(footprint.H);
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

    private static void AddNullableGuid(ReplayHashBuilder hash, Guid? value)
    {
        hash.AddBoolean(value.HasValue);
        if (value.HasValue)
        {
            hash.AddGuid(value.Value);
        }
    }

    private readonly record struct OwnedPlaceableSnapshot(
        ChunkKey OwnerChunk,
        int LocalIndex,
        PlaceableInstance Placeable);
}
