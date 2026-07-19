using HumanFortress.Contracts.Runtime.Save;

namespace HumanFortress.Runtime.Save;

internal static partial class RuntimeSaveSnapshotDocumentVerifier
{
    private static void ValidateMiningJobs(
        RuntimeSaveSnapshotDocumentData document,
        ICollection<RuntimeSaveSnapshotDocumentIssueData> issues)
    {
        var section = FindSection(document, RuntimeSaveManifestSections.JobsMining);
        if (section == null)
            return;

        if (!section.Value.Present)
        {
            if (document.MiningJobs.HasValue
                && RuntimeSaveSnapshotDocumentMiningMapper.CountRecords(document.MiningJobs.Value) > 0)
            {
                issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                    RuntimeSaveManifestSections.JobsMining,
                    null,
                    "Mining job payload is present but the manifest section is absent."));
            }

            return;
        }

        var expectedCount = section.Value.RecordCount;
        if (!expectedCount.HasValue)
        {
            if (document.MiningJobs.HasValue)
            {
                issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                    RuntimeSaveManifestSections.JobsMining,
                    null,
                    "Mining job payload is present but the manifest section has no record count."));
            }

            return;
        }

        if (expectedCount.Value == 0 && !document.MiningJobs.HasValue)
            return;

        if (!document.MiningJobs.HasValue)
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                RuntimeSaveManifestSections.JobsMining,
                null,
                "Mining job manifest section is non-empty but the document has no mining job payload."));
            return;
        }

        var payload = document.MiningJobs.Value;
        try
        {
            var actualCount = RuntimeSaveSnapshotDocumentMiningMapper.CountRecords(payload);
            if (expectedCount.Value != actualCount)
            {
                issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                    RuntimeSaveManifestSections.JobsMining,
                    null,
                    $"Mining job payload count {actualCount} does not match manifest count {expectedCount.Value}."));
            }

            var hash = RuntimeSaveSnapshotDocumentMiningMapper.BuildReplayHash(payload);
            if (!string.Equals(hash, section.Value.Hash, StringComparison.Ordinal)
                || !string.Equals(hash, document.Manifest.Checkpoint.MiningHash, StringComparison.Ordinal))
            {
                issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                    RuntimeSaveManifestSections.JobsMining,
                    null,
                    "Mining job payload hash does not match the manifest checkpoint."));
            }
        }
        catch (InvalidDataException ex)
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                RuntimeSaveManifestSections.JobsMining,
                null,
                ex.Message));
        }
    }

    private static void ValidateTransportJobs(
        RuntimeSaveSnapshotDocumentData document,
        ICollection<RuntimeSaveSnapshotDocumentIssueData> issues)
    {
        var section = FindSection(document, RuntimeSaveManifestSections.JobsTransport);
        if (section == null)
            return;

        if (!section.Value.Present)
        {
            if (document.TransportJobs.HasValue
                && RuntimeSaveSnapshotDocumentTransportMapper.CountRecords(document.TransportJobs.Value) > 0)
            {
                issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                    RuntimeSaveManifestSections.JobsTransport,
                    null,
                    "Transport job payload is present but the manifest section is absent."));
            }

            return;
        }

        var expectedCount = section.Value.RecordCount;
        if (!expectedCount.HasValue)
        {
            if (document.TransportJobs.HasValue)
            {
                issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                    RuntimeSaveManifestSections.JobsTransport,
                    null,
                    "Transport job payload is present but the manifest section has no record count."));
            }

            return;
        }

        if (expectedCount.Value == 0 && !document.TransportJobs.HasValue)
            return;

        if (!document.TransportJobs.HasValue)
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                RuntimeSaveManifestSections.JobsTransport,
                null,
                "Transport job manifest section is non-empty but the document has no transport job payload."));
            return;
        }

        var payload = document.TransportJobs.Value;
        try
        {
            var actualCount = RuntimeSaveSnapshotDocumentTransportMapper.CountRecords(payload);
            if (expectedCount.Value != actualCount)
            {
                issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                    RuntimeSaveManifestSections.JobsTransport,
                    null,
                    $"Transport job payload count {actualCount} does not match manifest count {expectedCount.Value}."));
            }

            var hash = RuntimeSaveSnapshotDocumentTransportMapper.BuildReplayHash(payload);
            if (!string.Equals(hash, section.Value.Hash, StringComparison.Ordinal)
                || !string.Equals(hash, document.Manifest.Checkpoint.TransportHash, StringComparison.Ordinal))
            {
                issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                    RuntimeSaveManifestSections.JobsTransport,
                    null,
                    "Transport job payload hash does not match the manifest checkpoint."));
            }
        }
        catch (InvalidDataException ex)
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                RuntimeSaveManifestSections.JobsTransport,
                null,
                ex.Message));
        }
    }

    private static void ValidateCraftJobs(
        RuntimeSaveSnapshotDocumentData document,
        ICollection<RuntimeSaveSnapshotDocumentIssueData> issues)
    {
        var section = FindSection(document, RuntimeSaveManifestSections.JobsCraft);
        if (section == null)
            return;

        if (!section.Value.Present)
        {
            if (document.CraftJobs.HasValue
                && RuntimeSaveSnapshotDocumentCraftMapper.CountRecords(document.CraftJobs.Value) > 0)
            {
                issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                    RuntimeSaveManifestSections.JobsCraft,
                    null,
                    "Craft job payload is present but the manifest section is absent."));
            }

            return;
        }

        var expectedCount = section.Value.RecordCount;
        if (!expectedCount.HasValue)
        {
            if (document.CraftJobs.HasValue)
            {
                issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                    RuntimeSaveManifestSections.JobsCraft,
                    null,
                    "Craft job payload is present but the manifest section has no record count."));
            }

            return;
        }

        if (expectedCount.Value == 0 && !document.CraftJobs.HasValue)
            return;

        if (!document.CraftJobs.HasValue)
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                RuntimeSaveManifestSections.JobsCraft,
                null,
                "Craft job manifest section is non-empty but the document has no craft job payload."));
            return;
        }

        var payload = document.CraftJobs.Value;
        try
        {
            var actualCount = RuntimeSaveSnapshotDocumentCraftMapper.CountRecords(payload);
            if (expectedCount.Value != actualCount)
            {
                issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                    RuntimeSaveManifestSections.JobsCraft,
                    null,
                    $"Craft job payload count {actualCount} does not match manifest count {expectedCount.Value}."));
            }

            var hash = RuntimeSaveSnapshotDocumentCraftMapper.BuildReplayHash(payload);
            if (!string.Equals(hash, section.Value.Hash, StringComparison.Ordinal)
                || !string.Equals(hash, document.Manifest.Checkpoint.CraftHash, StringComparison.Ordinal))
            {
                issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                    RuntimeSaveManifestSections.JobsCraft,
                    null,
                    "Craft job payload hash does not match the manifest checkpoint."));
            }
        }
        catch (InvalidDataException ex)
        {
            issues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                RuntimeSaveManifestSections.JobsCraft,
                null,
                ex.Message));
        }
    }
}
