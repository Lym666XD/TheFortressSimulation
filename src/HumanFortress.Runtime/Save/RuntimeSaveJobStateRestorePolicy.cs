using HumanFortress.Contracts.Runtime.Save;

namespace HumanFortress.Runtime.Save;

internal static class RuntimeSaveJobStateRestorePolicy
{
    private static readonly string[] JobSectionNames =
    {
        RuntimeSaveManifestSections.JobsTransport,
        RuntimeSaveManifestSections.JobsMining,
        RuntimeSaveManifestSections.JobsCraft
    };

    internal static string[] GetPresentUnsupportedSections(RuntimeSaveSnapshotDocumentData document)
    {
        if (document.Manifest.Sections == null)
            return Array.Empty<string>();

        return JobSectionNames
            .Where(sectionName => IsUnsupportedSectionPresent(document, sectionName))
            .OrderBy(static sectionName => sectionName, StringComparer.Ordinal)
            .ToArray();
    }

    internal static RuntimeSaveSnapshotDocumentIssueData CreateBlockingIssue(
        IReadOnlyList<string> presentSections)
    {
        var sections = presentSections.Count == 0
            ? "none"
            : string.Join(", ", presentSections);

        return new RuntimeSaveSnapshotDocumentIssueData(
            "slot.restore_plan",
            null,
            $"Save slot contains non-empty or uncounted job-state checkpoint sections without supported job-state payload restore: {sections}.");
    }

    private static bool IsUnsupportedSectionPresent(
        RuntimeSaveSnapshotDocumentData document,
        string sectionName)
    {
        foreach (var section in document.Manifest.Sections)
        {
            if (string.Equals(section.Name, sectionName, StringComparison.Ordinal))
            {
                if (!section.Present)
                    return false;
                if (!section.RecordCount.HasValue)
                    return true;
                if (section.RecordCount.Value <= 0)
                    return false;
                if (string.Equals(sectionName, RuntimeSaveManifestSections.JobsTransport, StringComparison.Ordinal))
                    return !HasSupportedTransportPayload(document, section.RecordCount.Value);
                if (string.Equals(sectionName, RuntimeSaveManifestSections.JobsMining, StringComparison.Ordinal))
                    return !HasSupportedMiningPayload(document, section.RecordCount.Value);
                if (string.Equals(sectionName, RuntimeSaveManifestSections.JobsCraft, StringComparison.Ordinal))
                    return !HasSupportedCraftPayload(document, section.RecordCount.Value);

                return true;
            }
        }

        return false;
    }

    private static bool HasSupportedTransportPayload(
        RuntimeSaveSnapshotDocumentData document,
        long expectedRecordCount)
    {
        if (!document.TransportJobs.HasValue)
            return false;

        return RuntimeSaveSnapshotDocumentTransportMapper.CountRecords(document.TransportJobs.Value) == expectedRecordCount;
    }

    private static bool HasSupportedMiningPayload(
        RuntimeSaveSnapshotDocumentData document,
        long expectedRecordCount)
    {
        if (!document.MiningJobs.HasValue)
            return false;

        return RuntimeSaveSnapshotDocumentMiningMapper.CountRecords(document.MiningJobs.Value) == expectedRecordCount;
    }

    private static bool HasSupportedCraftPayload(
        RuntimeSaveSnapshotDocumentData document,
        long expectedRecordCount)
    {
        if (!document.CraftJobs.HasValue)
            return false;

        return RuntimeSaveSnapshotDocumentCraftMapper.CountRecords(document.CraftJobs.Value) == expectedRecordCount;
    }
}
