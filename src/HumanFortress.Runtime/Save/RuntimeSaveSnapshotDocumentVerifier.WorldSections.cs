using HumanFortress.Contracts.Runtime.Save;
using HumanFortress.Contracts.Simulation.Save;

namespace HumanFortress.Runtime.Save;

internal static partial class RuntimeSaveSnapshotDocumentVerifier
{
    private static void ValidateWorldPayloadSections(
        WorldSavePayloadData payload,
        RuntimeSaveSnapshotDocumentData document,
        ICollection<RuntimeSaveSnapshotDocumentIssueData> issues)
    {
        ValidateWorldPayloadSection(
            "world.terrain",
            payload.SectionHashes.TerrainHash,
            payload.Counts.TileCount,
            document,
            issues);
        ValidateWorldPayloadSection(
            "world.items",
            payload.SectionHashes.ItemsHash,
            payload.Counts.ItemCount,
            document,
            issues);
        ValidateWorldPayloadSection(
            "world.creatures",
            payload.SectionHashes.CreaturesHash,
            payload.Counts.CreatureCount,
            document,
            issues);
        ValidateWorldPayloadSection(
            "world.stockpiles",
            payload.SectionHashes.StockpileZonesHash,
            payload.Counts.StockpileZoneCount,
            document,
            issues);
        ValidateWorldPayloadSection(
            "world.placeables",
            payload.SectionHashes.PlaceablesHash,
            payload.Counts.OwnedPlaceableCount,
            document,
            issues);
        ValidateWorldPayloadSection(
            "world.reservations",
            payload.SectionHashes.ReservationsHash,
            payload.Counts.ItemReservationCount + payload.Counts.CreatureReservationCount,
            document,
            issues);
        ValidateWorldPayloadSection(
            "world.orders",
            payload.SectionHashes.OrdersHash,
            payload.Counts.MiningOrderCount
                + payload.Counts.HaulOrderCount
                + payload.Counts.ConstructionOrderCount
                + payload.Counts.BuildableOrderCount,
            document,
            issues);
    }

    private static void ValidateWorldPayloadSection(
        string sectionName,
        string payloadHash,
        long payloadCount,
        RuntimeSaveSnapshotDocumentData document,
        ICollection<RuntimeSaveSnapshotDocumentIssueData> issues)
    {
        var section = FindSection(document, sectionName);
        if (section == null)
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(sectionName, null, "Manifest section is missing."));
            return;
        }

        if (!string.Equals(section.Value.Hash, payloadHash, StringComparison.Ordinal))
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                sectionName,
                null,
                "World payload section hash does not match manifest section hash."));
        }

        if (section.Value.RecordCount != payloadCount)
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                sectionName,
                null,
                "World payload section count does not match manifest section count."));
        }
    }
}
