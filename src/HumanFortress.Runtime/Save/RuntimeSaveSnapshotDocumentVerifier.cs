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

        ValidateManifestSections(document, issues);
        ValidateWorldPayload(document, issues);
        ValidateRngStreams(document, issues);

        var executedRecords = MapRecords(
            document,
            map: RuntimeSaveSnapshotDocumentCommandMapper.ToExecutedCommandReplayRecords,
            RuntimeSaveManifestSections.CommandsExecuted,
            issues);
        var pendingRecords = MapRecords(
            document,
            map: RuntimeSaveSnapshotDocumentCommandMapper.ToPendingCommandReplayRecords,
            RuntimeSaveManifestSections.CommandsPending,
            issues);

        if (executedRecords != null)
        {
            ValidateCommandJournal(
                RuntimeSaveManifestSections.CommandsExecuted,
                executedRecords,
                document.Manifest.Checkpoint.CommandLogHash,
                document.Manifest.Checkpoint.CommandLogRecordCount,
                FindSection(document, RuntimeSaveManifestSections.CommandsExecuted),
                issues);
        }

        if (pendingRecords != null)
        {
            ValidateCommandJournal(
                RuntimeSaveManifestSections.CommandsPending,
                pendingRecords,
                document.Manifest.Checkpoint.PendingCommandLogHash,
                document.Manifest.Checkpoint.PendingCommandLogRecordCount,
                FindSection(document, RuntimeSaveManifestSections.CommandsPending),
                issues);
        }

        return issues.Count == 0
            ? RuntimeSaveSnapshotDocumentValidationResultData.Valid
            : new RuntimeSaveSnapshotDocumentValidationResultData(false, issues.ToArray());
    }

    private static void ValidateManifestSections(
        RuntimeSaveSnapshotDocumentData document,
        ICollection<RuntimeSaveSnapshotDocumentIssueData> issues)
    {
        if (document.Manifest.Sections == null)
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                "manifest.sections",
                null,
                "Manifest sections are missing."));
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < document.Manifest.Sections.Count; i++)
        {
            var section = document.Manifest.Sections[i];
            if (string.IsNullOrWhiteSpace(section.Name))
            {
                issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                    "manifest.sections",
                    i,
                    "Manifest section name is blank."));
                continue;
            }

            if (!RuntimeSaveManifestSections.TryGetRequirement(section.Name, out var expectedRequiredForFortressMode))
            {
                issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                    "manifest.sections",
                    i,
                    $"Manifest section '{section.Name}' is not recognized by this runtime."));
            }
            else if (section.RequiredForFortressMode != expectedRequiredForFortressMode)
            {
                issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                    "manifest.sections",
                    i,
                    $"Manifest section '{section.Name}' has an unexpected fortress-mode requirement flag."));
            }

            if (section.RecordCount.HasValue && section.RecordCount.Value < 0)
            {
                issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                    "manifest.sections",
                    i,
                    $"Manifest section '{section.Name}' has a negative record count."));
            }

            if (section.Present)
            {
                if (string.IsNullOrWhiteSpace(section.Hash))
                {
                    issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                        "manifest.sections",
                        i,
                        $"Manifest section '{section.Name}' is present but has a blank hash."));
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(section.Hash))
                {
                    issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                        "manifest.sections",
                        i,
                        $"Manifest section '{section.Name}' is absent but still has a hash."));
                }

                if (section.RecordCount.HasValue)
                {
                    issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                        "manifest.sections",
                        i,
                        $"Manifest section '{section.Name}' is absent but still has a record count."));
                }
            }

            if (!seen.Add(section.Name))
            {
                issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                    "manifest.sections",
                    i,
                    $"Manifest section duplicates '{section.Name}'."));
            }
        }

        foreach (var sectionName in RuntimeSaveManifestSections.OrderedNames)
        {
            if (!seen.Contains(sectionName))
            {
                issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                    "manifest.sections",
                    null,
                    $"Manifest section '{sectionName}' is missing."));
            }
        }
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
