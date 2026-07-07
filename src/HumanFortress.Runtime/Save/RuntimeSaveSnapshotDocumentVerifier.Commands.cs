using HumanFortress.Contracts.Runtime.Save;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Determinism;

namespace HumanFortress.Runtime.Save;

internal static partial class RuntimeSaveSnapshotDocumentVerifier
{
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
}
