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
            CreateSection(RuntimeSaveManifestSections.World, checkpoint.WorldHash, worldCounts?.ChunkCount),
            CreateSection(RuntimeSaveManifestSections.WorldTerrain, worldSectionHashes?.TerrainHash, worldCounts?.TileCount),
            CreateSection(RuntimeSaveManifestSections.WorldItems, worldSectionHashes?.ItemsHash, worldCounts?.ItemCount),
            CreateSection(RuntimeSaveManifestSections.WorldCreatures, worldSectionHashes?.CreaturesHash, worldCounts?.CreatureCount),
            CreateSection(RuntimeSaveManifestSections.WorldReservations, worldSectionHashes?.ReservationsHash, CountReservations(worldCounts)),
            CreateSection(RuntimeSaveManifestSections.WorldStockpiles, worldSectionHashes?.StockpileZonesHash, worldCounts?.StockpileZoneCount),
            CreateSection(RuntimeSaveManifestSections.WorldPlaceables, worldSectionHashes?.PlaceablesHash, worldCounts?.OwnedPlaceableCount),
            CreateSection(RuntimeSaveManifestSections.WorldOrders, worldSectionHashes?.OrdersHash, CountOrders(worldCounts)),
            CreateSection(RuntimeSaveManifestSections.Rng, checkpoint.RngHash, checkpoint.RngStreamCount),
            CreateSection(RuntimeSaveManifestSections.CommandsExecuted, checkpoint.CommandLogHash, checkpoint.CommandLogRecordCount),
            CreateSection(RuntimeSaveManifestSections.CommandsPending, checkpoint.PendingCommandLogHash, checkpoint.PendingCommandLogRecordCount),
            CreateSection(
                RuntimeSaveManifestSections.JobsTransport,
                checkpoint.TransportHash,
                CountSection(checkpoint.TransportHash, checkpoint.TransportRecordCount)),
            CreateSection(
                RuntimeSaveManifestSections.JobsMining,
                checkpoint.MiningHash,
                CountSection(checkpoint.MiningHash, checkpoint.MiningRecordCount)),
            CreateSection(
                RuntimeSaveManifestSections.JobsCraft,
                checkpoint.CraftHash,
                CountSection(checkpoint.CraftHash, checkpoint.CraftRecordCount))
        };

        return new RuntimeSaveManifestData(
            RuntimeSaveFormat.CurrentVersion,
            EngineBuild,
            checkpoint.Metadata,
            RuntimeSaveContentSignatureFactory.FromRuntimeContent(content),
            RuntimeSaveContentCatalogSummaryFactory.FromRuntimeContent(content),
            checkpoint,
            sections);
    }

    private static RuntimeSaveManifestSectionData CreateSection(
        string name,
        string? hash,
        long? recordCount = null)
    {
        if (!RuntimeSaveManifestSections.TryGetRequirement(name, out var requiredForFortressMode))
            throw new InvalidOperationException($"Runtime save manifest section '{name}' is not defined.");

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

    private static long? CountSection(string? hash, int recordCount)
    {
        return hash == null
            ? null
            : recordCount;
    }

}
