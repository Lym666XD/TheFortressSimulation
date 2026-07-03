using HumanFortress.Contracts.Runtime.Save;
using HumanFortress.Contracts.Simulation.Save;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Determinism;
using HumanFortress.Core.Random;

namespace HumanFortress.Runtime.Save;

internal static class RuntimeSaveSnapshotDocumentVerifier
{
    internal static RuntimeSaveSnapshotDocumentValidationResultData Validate(
        RuntimeSaveSnapshotDocumentData document)
    {
        var issues = new List<RuntimeSaveSnapshotDocumentIssueData>();

        if (document.Manifest.FormatVersion != RuntimeSaveFormat.CurrentVersion)
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                "manifest",
                null,
                $"Unsupported save format version {document.Manifest.FormatVersion}."));
        }

        ValidateWorldPayload(document, issues);
        ValidateRngStreams(document, issues);

        var executedRecords = MapRecords(
            document,
            map: RuntimeSaveSnapshotDocumentCommandMapper.ToExecutedCommandReplayRecords,
            "commands.executed",
            issues);
        var pendingRecords = MapRecords(
            document,
            map: RuntimeSaveSnapshotDocumentCommandMapper.ToPendingCommandReplayRecords,
            "commands.pending",
            issues);

        if (executedRecords != null)
        {
            ValidateCommandJournal(
                "commands.executed",
                executedRecords,
                document.Manifest.Checkpoint.CommandLogHash,
                document.Manifest.Checkpoint.CommandLogRecordCount,
                FindSection(document, "commands.executed"),
                issues);
        }

        if (pendingRecords != null)
        {
            ValidateCommandJournal(
                "commands.pending",
                pendingRecords,
                document.Manifest.Checkpoint.PendingCommandLogHash,
                document.Manifest.Checkpoint.PendingCommandLogRecordCount,
                FindSection(document, "commands.pending"),
                issues);
        }

        return issues.Count == 0
            ? RuntimeSaveSnapshotDocumentValidationResultData.Valid
            : new RuntimeSaveSnapshotDocumentValidationResultData(false, issues.ToArray());
    }

    private static void ValidateRngStreams(
        RuntimeSaveSnapshotDocumentData document,
        ICollection<RuntimeSaveSnapshotDocumentIssueData> issues)
    {
        IReadOnlyList<RngStreamStateSnapshot> streams;
        try
        {
            streams = RuntimeSaveSnapshotDocumentRngMapper.ToRngStreamStateSnapshots(document);
        }
        catch (InvalidDataException ex)
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData("rng", null, ex.Message));
            return;
        }

        ValidateRngSection(
            streams,
            document.Manifest.Checkpoint.RngHash,
            document.Manifest.Checkpoint.RngStreamCount,
            FindSection(document, "rng"),
            issues);
    }

    private static IReadOnlyList<CommandReplayRecord>? MapRecords(
        RuntimeSaveSnapshotDocumentData document,
        Func<RuntimeSaveSnapshotDocumentData, IReadOnlyList<CommandReplayRecord>> map,
        string sectionName,
        ICollection<RuntimeSaveSnapshotDocumentIssueData> issues)
    {
        try
        {
            return map(document);
        }
        catch (InvalidDataException ex)
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(sectionName, null, ex.Message));
            return null;
        }
    }

    private static RuntimeSaveManifestSectionData? FindSection(
        RuntimeSaveSnapshotDocumentData document,
        string sectionName)
    {
        if (document.Manifest.Sections == null)
            return null;

        foreach (var section in document.Manifest.Sections)
        {
            if (string.Equals(section.Name, sectionName, StringComparison.Ordinal))
                return section;
        }

        return null;
    }

    private static void ValidateWorldPayload(
        RuntimeSaveSnapshotDocumentData document,
        ICollection<RuntimeSaveSnapshotDocumentIssueData> issues)
    {
        var worldSection = FindSection(document, "world");
        if (worldSection is not { } section || !section.Present)
            return;

        if (!document.WorldPayload.HasValue)
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                "world.payload",
                null,
                "Manifest contains a world section but the document is missing a world payload."));
            return;
        }

        var payload = document.WorldPayload.Value;
        if (payload.SchemaVersion != WorldSavePayloadFormat.CurrentVersion)
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                "world.payload",
                null,
                $"Unsupported world payload schema version {payload.SchemaVersion}."));
        }

        if (!string.Equals(payload.ReplayHash, document.Manifest.Checkpoint.WorldHash, StringComparison.Ordinal))
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                "world.payload",
                null,
                "World payload hash does not match manifest checkpoint world hash."));
        }

        if (!string.Equals(section.Hash, payload.ReplayHash, StringComparison.Ordinal))
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                "world.payload",
                null,
                "World payload hash does not match manifest world section hash."));
        }

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

    private static void ValidateCommandJournal(
        string sectionName,
        IReadOnlyList<CommandReplayRecord> records,
        string expectedHash,
        int expectedCount,
        RuntimeSaveManifestSectionData? manifestSection,
        ICollection<RuntimeSaveSnapshotDocumentIssueData> issues)
    {
        if (string.IsNullOrWhiteSpace(expectedHash))
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(sectionName, null, "Manifest checkpoint command hash is blank."));
            return;
        }

        if (expectedCount != records.Count)
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                sectionName,
                null,
                $"Manifest checkpoint command count {expectedCount} does not match document count {records.Count}."));
        }

        var actualHash = CommandReplayJournalHashBuilder.Build(records);
        if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                sectionName,
                null,
                "Manifest checkpoint command hash does not match document command records."));
        }

        if (manifestSection is not { } section)
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(sectionName, null, "Manifest section is missing."));
            return;
        }

        if (!section.Present)
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(sectionName, null, "Manifest section is not marked present."));
        }

        if (!string.Equals(section.Hash, expectedHash, StringComparison.Ordinal))
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                sectionName,
                null,
                "Manifest section hash does not match checkpoint command hash."));
        }

        if (section.RecordCount != expectedCount)
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                sectionName,
                null,
                "Manifest section record count does not match checkpoint command count."));
        }
    }

    private static void ValidateRngSection(
        IReadOnlyList<RngStreamStateSnapshot> streams,
        string expectedHash,
        int expectedCount,
        RuntimeSaveManifestSectionData? manifestSection,
        ICollection<RuntimeSaveSnapshotDocumentIssueData> issues)
    {
        const string sectionName = "rng";

        if (string.IsNullOrWhiteSpace(expectedHash))
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(sectionName, null, "Manifest checkpoint RNG hash is blank."));
            return;
        }

        if (expectedCount != streams.Count)
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                sectionName,
                null,
                $"Manifest checkpoint RNG stream count {expectedCount} does not match document count {streams.Count}."));
        }

        var actualHash = RngReplayHashBuilder.Build(streams);
        if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                sectionName,
                null,
                "Manifest checkpoint RNG hash does not match document RNG stream records."));
        }

        if (manifestSection is not { } section)
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(sectionName, null, "Manifest section is missing."));
            return;
        }

        if (!section.Present)
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(sectionName, null, "Manifest section is not marked present."));
        }

        if (!string.Equals(section.Hash, expectedHash, StringComparison.Ordinal))
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                sectionName,
                null,
                "Manifest section hash does not match checkpoint RNG hash."));
        }

        if (section.RecordCount != expectedCount)
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                sectionName,
                null,
                "Manifest section record count does not match checkpoint RNG stream count."));
        }
    }
}
