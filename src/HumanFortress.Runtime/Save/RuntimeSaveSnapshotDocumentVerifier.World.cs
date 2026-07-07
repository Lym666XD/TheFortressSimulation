using HumanFortress.Contracts.Runtime.Save;
using HumanFortress.Contracts.Simulation.Save;

namespace HumanFortress.Runtime.Save;

internal static partial class RuntimeSaveSnapshotDocumentVerifier
{
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

        ValidateWorldPayloadCounts(payload, issues);
        ValidateWorldPayloadSections(payload, document, issues);
    }
}
