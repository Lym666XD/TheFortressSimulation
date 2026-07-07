using HumanFortress.Contracts.Runtime.Save;
using HumanFortress.Core.Commands;

namespace HumanFortress.Runtime.Save;

internal static partial class RuntimeSaveSnapshotDocumentVerifier
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
}
