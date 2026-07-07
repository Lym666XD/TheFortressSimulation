using HumanFortress.Contracts.Runtime.Save;
using HumanFortress.Contracts.Simulation.Save;

namespace HumanFortress.Runtime.Save;

internal static partial class RuntimeSaveSnapshotDocumentVerifier
{
    private static void ValidateWorldPayloadCounts(
        WorldSavePayloadData payload,
        ICollection<RuntimeSaveSnapshotDocumentIssueData> issues)
    {
        if (payload.Chunks == null)
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                "world.payload",
                null,
                "World payload chunks are missing."));
        }
        else
        {
            if (payload.Counts.ChunkCount != payload.Chunks.Length)
            {
                issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                    "world.payload",
                    null,
                    "World payload chunk count does not match payload chunks."));
            }

            var tileCount = 0;
            foreach (var chunk in payload.Chunks)
            {
                tileCount += chunk.Tiles?.Length ?? 0;
            }

            if (payload.Counts.TileCount != tileCount)
            {
                issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                    "world.payload",
                    null,
                    "World payload tile count does not match payload tiles."));
            }
        }

        if (payload.Items == null)
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                "world.payload",
                null,
                "World payload items are missing."));
        }
        else if (payload.Counts.ItemCount != payload.Items.Length)
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                "world.payload",
                null,
                "World payload item count does not match payload items."));
        }

        ValidateWorldPayloadArrayCount(
            "World payload creature count does not match payload creatures.",
            "World payload creatures are missing.",
            payload.Creatures,
            payload.Counts.CreatureCount,
            issues);
        ValidateWorldPayloadArrayCount(
            "World payload item reservation count does not match payload item reservations.",
            "World payload item reservations are missing.",
            payload.ItemReservations,
            payload.Counts.ItemReservationCount,
            issues);
        ValidateWorldPayloadArrayCount(
            "World payload creature reservation count does not match payload creature reservations.",
            "World payload creature reservations are missing.",
            payload.CreatureReservations,
            payload.Counts.CreatureReservationCount,
            issues);
        ValidateWorldPayloadArrayCount(
            "World payload stockpile zone count does not match payload stockpile zones.",
            "World payload stockpile zones are missing.",
            payload.StockpileZones,
            payload.Counts.StockpileZoneCount,
            issues);
        ValidateWorldPayloadArrayCount(
            "World payload placeable count does not match payload placeables.",
            "World payload placeables are missing.",
            payload.Placeables,
            payload.Counts.OwnedPlaceableCount,
            issues);
        ValidateWorldPayloadArrayCount(
            "World payload mining order count does not match payload mining orders.",
            "World payload mining orders are missing.",
            payload.MiningOrders,
            payload.Counts.MiningOrderCount,
            issues);
        ValidateWorldPayloadArrayCount(
            "World payload haul order count does not match payload haul orders.",
            "World payload haul orders are missing.",
            payload.HaulOrders,
            payload.Counts.HaulOrderCount,
            issues);
        ValidateWorldPayloadArrayCount(
            "World payload construction order count does not match payload construction orders.",
            "World payload construction orders are missing.",
            payload.ConstructionOrders,
            payload.Counts.ConstructionOrderCount,
            issues);
        ValidateWorldPayloadArrayCount(
            "World payload buildable order count does not match payload buildable orders.",
            "World payload buildable orders are missing.",
            payload.BuildableOrders,
            payload.Counts.BuildableOrderCount,
            issues);
    }

    private static void ValidateWorldPayloadArrayCount<T>(
        string mismatchMessage,
        string missingMessage,
        T[]? payloadRows,
        int expectedCount,
        ICollection<RuntimeSaveSnapshotDocumentIssueData> issues)
    {
        if (payloadRows == null)
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                "world.payload",
                null,
                missingMessage));
            return;
        }

        if (payloadRows.Length != expectedCount)
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                "world.payload",
                null,
                mismatchMessage));
        }
    }
}
