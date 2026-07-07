using HumanFortress.Contracts.Runtime.Save;
using HumanFortress.Core.Determinism;
using HumanFortress.Core.Random;

namespace HumanFortress.Runtime.Save;

internal static partial class RuntimeSaveSnapshotDocumentVerifier
{
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
