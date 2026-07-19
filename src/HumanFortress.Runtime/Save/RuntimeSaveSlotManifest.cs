using System.Text.Json;
using HumanFortress.Contracts.Runtime.Save;

namespace HumanFortress.Runtime.Save;

internal static class RuntimeSaveSlotManifestBuilder
{
    internal static RuntimeSaveSlotManifestData Build(RuntimeSaveSnapshotDocumentData document)
    {
        return new RuntimeSaveSlotManifestData(
            RuntimeSaveSlotFormat.CurrentVersion,
            RuntimeSaveSlotFormat.SlotKind,
            RuntimeSaveSnapshotDocumentStore.DocumentFileName,
            document.Manifest.FormatVersion,
            document.Manifest.EngineBuild,
            document.Manifest.Metadata,
            document.Manifest.Checkpoint.AggregateHash,
            document.Manifest.Checkpoint.WorldHash,
            document.Manifest.Sections?.Count ?? 0,
            document.RngStreams?.Length ?? 0,
            document.ExecutedCommandRecords?.Length ?? 0,
            document.PendingCommandRecords?.Length ?? 0);
    }
}

internal static class RuntimeSaveSlotManifestCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    internal static string Serialize(RuntimeSaveSlotManifestData manifest)
    {
        ValidateShape(manifest);
        return JsonSerializer.Serialize(manifest, JsonOptions);
    }

    internal static RuntimeSaveSlotManifestData Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Save slot manifest JSON must not be blank.", nameof(json));

        var manifest = JsonSerializer.Deserialize<RuntimeSaveSlotManifestData>(json, JsonOptions);
        ValidateShape(manifest);
        return manifest;
    }

    private static void ValidateShape(RuntimeSaveSlotManifestData manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.SlotKind))
            throw new InvalidDataException("Save slot manifest has a blank slot kind.");
        if (string.IsNullOrWhiteSpace(manifest.RuntimeSnapshotDocumentFileName))
            throw new InvalidDataException("Save slot manifest has a blank snapshot document file name.");
        if (string.IsNullOrWhiteSpace(manifest.CheckpointAggregateHash))
            throw new InvalidDataException("Save slot manifest has a blank checkpoint aggregate hash.");
        if (manifest.ManifestSectionCount < 0)
            throw new InvalidDataException("Save slot manifest has a negative section count.");
        if (manifest.RngStreamCount < 0)
            throw new InvalidDataException("Save slot manifest has a negative RNG stream count.");
        if (manifest.ExecutedCommandRecordCount < 0)
            throw new InvalidDataException("Save slot manifest has a negative executed command count.");
        if (manifest.PendingCommandRecordCount < 0)
            throw new InvalidDataException("Save slot manifest has a negative pending command count.");
    }
}

internal static class RuntimeSaveSlotManifestVerifier
{
    internal static RuntimeSaveSnapshotDocumentValidationResultData Validate(
        RuntimeSaveSlotManifestData slotManifest,
        RuntimeSaveSnapshotDocumentData document)
    {
        var issues = new List<RuntimeSaveSnapshotDocumentIssueData>();
        var compatibility = RuntimeSaveSlotCompatibilityPolicy.Evaluate(slotManifest);
        var compatibilityIssue = RuntimeSaveSlotCompatibilityPolicy.ToValidationIssue(compatibility);
        if (compatibilityIssue.HasValue)
            issues.Add(compatibilityIssue.Value);

        AddIf(slotManifest.SlotFormatVersion != RuntimeSaveSlotFormat.CurrentVersion,
            "Unsupported save slot manifest format version.");
        AddIf(!string.Equals(slotManifest.SlotKind, RuntimeSaveSlotFormat.SlotKind, StringComparison.Ordinal),
            "Save slot manifest kind is not recognized by this runtime.");
        AddIf(!string.Equals(slotManifest.RuntimeSnapshotDocumentFileName, RuntimeSaveSnapshotDocumentStore.DocumentFileName, StringComparison.Ordinal),
            "Save slot manifest points at an unsupported snapshot document file.");
        AddIf(slotManifest.RuntimeSnapshotFormatVersion != document.Manifest.FormatVersion,
            "Save slot manifest snapshot format version does not match the snapshot document.");
        AddIf(!string.Equals(slotManifest.EngineBuild, document.Manifest.EngineBuild, StringComparison.Ordinal),
            "Save slot manifest engine build does not match the snapshot document.");
        AddIf(slotManifest.Metadata != document.Manifest.Metadata,
            "Save slot manifest snapshot metadata does not match the snapshot document.");
        AddIf(!string.Equals(slotManifest.CheckpointAggregateHash, document.Manifest.Checkpoint.AggregateHash, StringComparison.Ordinal),
            "Save slot manifest checkpoint hash does not match the snapshot document.");
        AddIf(!string.Equals(slotManifest.WorldHash, document.Manifest.Checkpoint.WorldHash, StringComparison.Ordinal),
            "Save slot manifest world hash does not match the snapshot document.");
        AddIf(slotManifest.ManifestSectionCount != (document.Manifest.Sections?.Count ?? 0),
            "Save slot manifest section count does not match the snapshot document.");
        AddIf(slotManifest.RngStreamCount != (document.RngStreams?.Length ?? 0),
            "Save slot manifest RNG stream count does not match the snapshot document.");
        AddIf(slotManifest.ExecutedCommandRecordCount != (document.ExecutedCommandRecords?.Length ?? 0),
            "Save slot manifest executed command count does not match the snapshot document.");
        AddIf(slotManifest.PendingCommandRecordCount != (document.PendingCommandRecords?.Length ?? 0),
            "Save slot manifest pending command count does not match the snapshot document.");

        return issues.Count == 0
            ? RuntimeSaveSnapshotDocumentValidationResultData.Valid
            : new RuntimeSaveSnapshotDocumentValidationResultData(false, issues.ToArray());

        void AddIf(bool condition, string message)
        {
            if (!condition)
                return;

            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                "slot.manifest",
                null,
                message));
        }
    }
}
