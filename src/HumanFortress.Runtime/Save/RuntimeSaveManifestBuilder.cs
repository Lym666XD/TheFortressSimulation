using HumanFortress.Content.Loading;
using HumanFortress.Contracts.Runtime.Replay;
using HumanFortress.Contracts.Runtime.Save;
using HumanFortress.Simulation.Save;

namespace HumanFortress.Runtime.Save;

internal static class RuntimeSaveManifestBuilder
{
    private const string EngineBuild = "humanfortress-dev";

    internal static RuntimeSaveManifestData Build(
        RuntimeReplayCheckpointData checkpoint,
        FortressRuntimeContentSnapshot? content,
        WorldSaveSnapshot? worldSnapshot)
    {
        if (worldSnapshot.HasValue && checkpoint.WorldHash != worldSnapshot.Value.ReplayHash)
            throw new InvalidOperationException("Save manifest world snapshot does not match replay checkpoint world hash.");

        var worldSectionHashes = worldSnapshot?.SectionHashes;
        var worldCounts = worldSnapshot?.Counts;
        var sections = new[]
        {
            CreateSection("world", checkpoint.WorldHash, requiredForFortressMode: true, worldCounts?.ChunkCount),
            CreateSection("world.terrain", worldSectionHashes?.TerrainHash, requiredForFortressMode: true, worldCounts?.TileCount),
            CreateSection("world.items", worldSectionHashes?.ItemsHash, requiredForFortressMode: true, worldCounts?.ItemCount),
            CreateSection("world.creatures", worldSectionHashes?.CreaturesHash, requiredForFortressMode: true, worldCounts?.CreatureCount),
            CreateSection("world.reservations", worldSectionHashes?.ReservationsHash, requiredForFortressMode: true, CountReservations(worldCounts)),
            CreateSection("world.stockpiles", worldSectionHashes?.StockpileZonesHash, requiredForFortressMode: true, worldCounts?.StockpileZoneCount),
            CreateSection("world.placeables", worldSectionHashes?.PlaceablesHash, requiredForFortressMode: true, worldCounts?.OwnedPlaceableCount),
            CreateSection("world.orders", worldSectionHashes?.OrdersHash, requiredForFortressMode: true, CountOrders(worldCounts)),
            CreateSection("rng", checkpoint.RngHash, requiredForFortressMode: true, checkpoint.RngStreamCount),
            CreateSection("commands.executed", checkpoint.CommandLogHash, requiredForFortressMode: false, checkpoint.CommandLogRecordCount),
            CreateSection("commands.pending", checkpoint.PendingCommandLogHash, requiredForFortressMode: false, checkpoint.PendingCommandLogRecordCount),
            CreateSection("jobs.transport", checkpoint.TransportHash, requiredForFortressMode: false),
            CreateSection("jobs.mining", checkpoint.MiningHash, requiredForFortressMode: false),
            CreateSection("jobs.craft", checkpoint.CraftHash, requiredForFortressMode: false)
        };

        return new RuntimeSaveManifestData(
            RuntimeSaveFormat.CurrentVersion,
            EngineBuild,
            checkpoint.Metadata,
            CreateContentSignature(content),
            checkpoint,
            sections);
    }

    private static RuntimeSaveManifestSectionData CreateSection(
        string name,
        string? hash,
        bool requiredForFortressMode,
        long? recordCount = null)
    {
        return new RuntimeSaveManifestSectionData(
            name,
            hash != null,
            hash,
            requiredForFortressMode,
            recordCount);
    }

    private static long? CountReservations(WorldSaveCounts? counts)
    {
        return counts.HasValue
            ? counts.Value.ItemReservationCount + counts.Value.CreatureReservationCount
            : null;
    }

    private static long? CountOrders(WorldSaveCounts? counts)
    {
        return counts.HasValue
            ? counts.Value.MiningOrderCount
                + counts.Value.HaulOrderCount
                + counts.Value.ConstructionOrderCount
                + counts.Value.BuildableOrderCount
            : null;
    }

    private static RuntimeSaveContentSignatureData CreateContentSignature(
        FortressRuntimeContentSnapshot? content)
    {
        if (content == null)
            return RuntimeSaveContentSignatureData.Unavailable;

        return new RuntimeSaveContentSignatureData(
            HasContent: true,
            ContentVersion: content.ContentVersion.ToString(),
            ContentHash: content.ContentHash,
            MaterialContentHash: content.Materials.ContentHash,
            MaterialCount: content.Materials.GetNameToIdSnapshot().Count,
            TerrainKindCount: content.TerrainKinds.GetAllKinds().Count(),
            ConstructionCount: content.Constructions.Count,
            RecipeCount: content.Recipes.Count,
            GeologyCount: content.GeologyEntries.Count,
            ZoneCount: content.ZonesById.Count);
    }
}
