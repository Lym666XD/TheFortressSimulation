using HumanFortress.Contracts.Runtime.Save;

namespace HumanFortress.Runtime.Save;

internal static class RuntimeSaveSlotRestorePlanBuilder
{
    internal static RuntimeSaveSlotRestorePlanData Build(
        RuntimeSaveSnapshotDocumentValidationResultData validation,
        RuntimeSaveSlotCompatibilityData compatibility,
        RuntimeSaveSlotContentCompatibilityData contentCompatibility,
        bool documentAvailable,
        RuntimeSaveSnapshotDocumentData document,
        RuntimeSaveSlotManifestData? manifest)
    {
        if (!validation.Success || !compatibility.CanRead || !contentCompatibility.CanBindContent || !documentAvailable)
            return Blocked(BuildBlockingIssues(validation, compatibility, contentCompatibility, documentAvailable));

        var blockingIssues = new List<RuntimeSaveSnapshotDocumentIssueData>();
        var canRestorePendingCommands = SectionPresent(document, RuntimeSaveManifestSections.CommandsPending);
        var canRestoreWorld = document.WorldPayload.HasValue
            && SectionPresent(document, RuntimeSaveManifestSections.World)
            && manifest.HasValue;
        var unsupportedJobStateSections = RuntimeSaveJobStateRestorePolicy.GetPresentUnsupportedSections(document);
        var canRestoreFull = canRestoreWorld
            && SectionPresent(document, RuntimeSaveManifestSections.Rng)
            && unsupportedJobStateSections.Length == 0;

        if (!canRestorePendingCommands)
        {
            blockingIssues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                "slot.restore_plan",
                null,
                "Save slot does not contain a present pending-command section."));
        }

        if (!canRestoreWorld)
        {
            blockingIssues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                "slot.restore_plan",
                null,
                "Save slot does not contain a restorable world payload."));
        }

        if (!canRestoreFull && unsupportedJobStateSections.Length == 0)
        {
            blockingIssues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                "slot.restore_plan",
                null,
                "Save slot does not contain every section required for full restore."));
        }

        if (unsupportedJobStateSections.Length > 0)
        {
            blockingIssues.Add(RuntimeSaveJobStateRestorePolicy.CreateBlockingIssue(
                unsupportedJobStateSections));
        }

        return new RuntimeSaveSlotRestorePlanData(
            CanRestorePendingCommands: canRestorePendingCommands,
            CanRestoreWorld: canRestoreWorld,
            CanRestoreFull: canRestoreFull,
            BlockingIssues: blockingIssues.ToArray());
    }

    private static RuntimeSaveSnapshotDocumentIssueData[] BuildBlockingIssues(
        RuntimeSaveSnapshotDocumentValidationResultData validation,
        RuntimeSaveSlotCompatibilityData compatibility,
        RuntimeSaveSlotContentCompatibilityData contentCompatibility,
        bool documentAvailable)
    {
        var issues = new List<RuntimeSaveSnapshotDocumentIssueData>();
        if (!validation.Success)
            issues.AddRange(validation.Issues);

        if (!compatibility.CanRead && !issues.Any(static issue => issue.Section == "slot.compatibility"))
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                "slot.compatibility",
                null,
                compatibility.Message));
        }

        if (documentAvailable
            && !contentCompatibility.CanBindContent
            && !issues.Any(static issue => issue.Section == "slot.content"))
        {
            issues.Add(RuntimeSaveSlotContentCompatibilityPolicy.CreateBlockingIssue(
                contentCompatibility));
        }

        if (!documentAvailable && !issues.Any(static issue => issue.Section == "snapshot.document"))
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                "snapshot.document",
                null,
                "Save snapshot document was not readable."));
        }

        return issues.ToArray();
    }

    private static RuntimeSaveSlotRestorePlanData Blocked(
        RuntimeSaveSnapshotDocumentIssueData[] blockingIssues)
    {
        return new RuntimeSaveSlotRestorePlanData(
            CanRestorePendingCommands: false,
            CanRestoreWorld: false,
            CanRestoreFull: false,
            BlockingIssues: blockingIssues);
    }

    private static bool SectionPresent(
        RuntimeSaveSnapshotDocumentData document,
        string sectionName)
    {
        if (document.Manifest.Sections == null)
            return false;

        foreach (var section in document.Manifest.Sections)
        {
            if (string.Equals(section.Name, sectionName, StringComparison.Ordinal))
                return section.Present;
        }

        return false;
    }
}
