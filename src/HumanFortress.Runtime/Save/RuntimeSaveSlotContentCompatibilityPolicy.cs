using HumanFortress.Content.Loading;
using HumanFortress.Contracts.Runtime.Save;

namespace HumanFortress.Runtime.Save;

internal static class RuntimeSaveSlotContentCompatibilityPolicy
{
    internal static RuntimeSaveSlotContentCompatibilityData Evaluate(
        RuntimeSaveContentSignatureData savedContent,
        FortressRuntimeContentSnapshot? currentContent)
    {
        return Evaluate(
            savedContent,
            RuntimeSaveContentCatalogSummaryData.Unavailable,
            currentContent);
    }

    internal static RuntimeSaveSlotContentCompatibilityData Evaluate(
        RuntimeSaveContentSignatureData savedContent,
        RuntimeSaveContentCatalogSummaryData savedCatalog,
        FortressRuntimeContentSnapshot? currentContent)
    {
        return Evaluate(
            savedContent,
            savedCatalog,
            RuntimeSaveContentSignatureFactory.FromRuntimeContent(currentContent),
            RuntimeSaveContentCatalogSummaryFactory.FromRuntimeContent(currentContent));
    }

    internal static RuntimeSaveSlotContentCompatibilityData Evaluate(
        RuntimeSaveContentSignatureData savedContent,
        RuntimeSaveContentSignatureData currentContent)
    {
        return Evaluate(
            savedContent,
            RuntimeSaveContentCatalogSummaryData.Unavailable,
            currentContent,
            RuntimeSaveContentCatalogSummaryData.Unavailable);
    }

    internal static RuntimeSaveSlotContentCompatibilityData Evaluate(
        RuntimeSaveContentSignatureData savedContent,
        RuntimeSaveContentSignatureData currentContent,
        RuntimeSaveContentCatalogSummaryData currentCatalog)
    {
        return Evaluate(
            savedContent,
            RuntimeSaveContentCatalogSummaryData.Unavailable,
            currentContent,
            currentCatalog);
    }

    internal static RuntimeSaveSlotContentCompatibilityData Evaluate(
        RuntimeSaveContentSignatureData savedContent,
        RuntimeSaveContentCatalogSummaryData savedCatalog,
        RuntimeSaveContentSignatureData currentContent,
        RuntimeSaveContentCatalogSummaryData currentCatalog)
    {
        savedCatalog = NormalizeCatalog(savedCatalog);
        currentCatalog = NormalizeCatalog(currentCatalog);

        if (!savedContent.HasContent)
        {
            return Blocked(
                RuntimeSaveSlotContentCompatibilityStatus.MissingSavedContentSignature,
                savedContent,
                currentContent,
                savedCatalog,
                currentCatalog,
                "Save slot does not contain a content signature.");
        }

        if (!currentContent.HasContent)
        {
            return Blocked(
                RuntimeSaveSlotContentCompatibilityStatus.CurrentContentUnavailable,
                savedContent,
                currentContent,
                savedCatalog,
                currentCatalog,
                "Current runtime content signature is not available.");
        }

        var differenceDetails = BuildDifferenceDetails(savedContent, savedCatalog, currentContent, currentCatalog);
        if (differenceDetails.Length == 0)
        {
            return new RuntimeSaveSlotContentCompatibilityData(
                Status: RuntimeSaveSlotContentCompatibilityStatus.Compatible,
                CanBindContent: true,
                RequiresMissingContentPolicy: false,
                SavedContent: savedContent,
                CurrentContent: currentContent,
                SavedCatalog: savedCatalog,
                CurrentCatalog: currentCatalog,
                DifferenceDetails: Array.Empty<RuntimeSaveContentCompatibilityDifferenceData>(),
                Differences: Array.Empty<string>(),
                BlockingIssues: Array.Empty<RuntimeSaveSnapshotDocumentIssueData>());
        }

        return Blocked(
            DetermineMismatchStatus(savedContent, currentContent),
            savedContent,
            currentContent,
            savedCatalog,
            currentCatalog,
            differenceDetails);
    }

    internal static RuntimeSaveSnapshotDocumentIssueData CreateBlockingIssue(
        RuntimeSaveSlotContentCompatibilityData compatibility)
    {
        if (compatibility.BlockingIssues.Length > 0)
            return compatibility.BlockingIssues[0];

        return new RuntimeSaveSnapshotDocumentIssueData(
            "slot.content",
            null,
            "Save slot content is not compatible with the current runtime content.");
    }

    private static RuntimeSaveSlotContentCompatibilityData Blocked(
        RuntimeSaveSlotContentCompatibilityStatus status,
        RuntimeSaveContentSignatureData savedContent,
        RuntimeSaveContentSignatureData currentContent,
        RuntimeSaveContentCatalogSummaryData savedCatalog,
        RuntimeSaveContentCatalogSummaryData currentCatalog,
        string difference)
    {
        return Blocked(
            status,
            savedContent,
            currentContent,
            savedCatalog,
            currentCatalog,
            new[]
            {
                new RuntimeSaveContentCompatibilityDifferenceData(
                    RuntimeSaveContentCompatibilityDifferenceKind.Unknown,
                    "content",
                    string.Empty,
                    string.Empty,
                    HasSavedCatalogKeys: false,
                    HasCurrentCatalogKeys: currentCatalog.HasCatalog,
                    MissingCurrentKeys: Array.Empty<string>(),
                    AdditionalCurrentKeys: Array.Empty<string>(),
                    difference)
            });
    }

    private static RuntimeSaveSlotContentCompatibilityData Blocked(
        RuntimeSaveSlotContentCompatibilityStatus status,
        RuntimeSaveContentSignatureData savedContent,
        RuntimeSaveContentSignatureData currentContent,
        RuntimeSaveContentCatalogSummaryData savedCatalog,
        RuntimeSaveContentCatalogSummaryData currentCatalog,
        RuntimeSaveContentCompatibilityDifferenceData[] differenceDetails)
    {
        var differences = differenceDetails
            .Select(static difference => difference.Message)
            .ToArray();

        return new RuntimeSaveSlotContentCompatibilityData(
            Status: status,
            CanBindContent: false,
            RequiresMissingContentPolicy: true,
            SavedContent: savedContent,
            CurrentContent: currentContent,
            SavedCatalog: savedCatalog,
            CurrentCatalog: currentCatalog,
            DifferenceDetails: differenceDetails,
            Differences: differences,
            BlockingIssues: new[]
            {
                new RuntimeSaveSnapshotDocumentIssueData(
                    "slot.content",
                    null,
                    "Save slot content signature does not match the current runtime content. "
                    + string.Join(" ", differences))
            });
    }

    private static RuntimeSaveSlotContentCompatibilityStatus DetermineMismatchStatus(
        RuntimeSaveContentSignatureData savedContent,
        RuntimeSaveContentSignatureData currentContent)
    {
        if (!string.Equals(savedContent.ContentVersion, currentContent.ContentVersion, StringComparison.Ordinal))
            return RuntimeSaveSlotContentCompatibilityStatus.ContentVersionMismatch;

        if (!string.Equals(savedContent.ContentHash, currentContent.ContentHash, StringComparison.Ordinal))
            return RuntimeSaveSlotContentCompatibilityStatus.ContentHashMismatch;

        if (!string.Equals(savedContent.MaterialContentHash, currentContent.MaterialContentHash, StringComparison.Ordinal))
            return RuntimeSaveSlotContentCompatibilityStatus.MaterialContentHashMismatch;

        return RuntimeSaveSlotContentCompatibilityStatus.CatalogShapeMismatch;
    }

    private static RuntimeSaveContentCompatibilityDifferenceData[] BuildDifferenceDetails(
        RuntimeSaveContentSignatureData savedContent,
        RuntimeSaveContentCatalogSummaryData savedCatalog,
        RuntimeSaveContentSignatureData currentContent,
        RuntimeSaveContentCatalogSummaryData currentCatalog)
    {
        var differences = new List<RuntimeSaveContentCompatibilityDifferenceData>();
        AddStringDifference(differences, RuntimeSaveContentCompatibilityDifferenceKind.ContentVersion, "content version", savedContent.ContentVersion, currentContent.ContentVersion, savedCatalog, currentCatalog);
        AddStringDifference(differences, RuntimeSaveContentCompatibilityDifferenceKind.ContentHash, "content hash", savedContent.ContentHash, currentContent.ContentHash, savedCatalog, currentCatalog);
        AddStringDifference(differences, RuntimeSaveContentCompatibilityDifferenceKind.MaterialContentHash, "material content hash", savedContent.MaterialContentHash, currentContent.MaterialContentHash, savedCatalog, currentCatalog);
        AddCountDifference(differences, RuntimeSaveContentCompatibilityDifferenceKind.MaterialCount, "material count", savedContent.MaterialCount, currentContent.MaterialCount, savedCatalog, currentCatalog);
        AddCountDifference(differences, RuntimeSaveContentCompatibilityDifferenceKind.TerrainKindCount, "terrain kind count", savedContent.TerrainKindCount, currentContent.TerrainKindCount, savedCatalog, currentCatalog);
        AddCountDifference(differences, RuntimeSaveContentCompatibilityDifferenceKind.ConstructionCount, "construction count", savedContent.ConstructionCount, currentContent.ConstructionCount, savedCatalog, currentCatalog);
        AddCountDifference(differences, RuntimeSaveContentCompatibilityDifferenceKind.RecipeCount, "recipe count", savedContent.RecipeCount, currentContent.RecipeCount, savedCatalog, currentCatalog);
        AddCountDifference(differences, RuntimeSaveContentCompatibilityDifferenceKind.GeologyCount, "geology count", savedContent.GeologyCount, currentContent.GeologyCount, savedCatalog, currentCatalog);
        AddCountDifference(differences, RuntimeSaveContentCompatibilityDifferenceKind.ZoneCount, "zone count", savedContent.ZoneCount, currentContent.ZoneCount, savedCatalog, currentCatalog);

        return differences.ToArray();
    }

    private static void AddStringDifference(
        List<RuntimeSaveContentCompatibilityDifferenceData> differences,
        RuntimeSaveContentCompatibilityDifferenceKind kind,
        string label,
        string saved,
        string current,
        RuntimeSaveContentCatalogSummaryData savedCatalog,
        RuntimeSaveContentCatalogSummaryData currentCatalog)
    {
        if (string.Equals(saved, current, StringComparison.Ordinal))
            return;

        differences.Add(new RuntimeSaveContentCompatibilityDifferenceData(
            kind,
            label,
            saved,
            current,
            HasSavedCatalogKeys: HasCatalogKeys(kind, savedCatalog),
            HasCurrentCatalogKeys: HasCatalogKeys(kind, currentCatalog),
            MissingCurrentKeys: BuildMissingCurrentKeys(kind, savedCatalog, currentCatalog),
            AdditionalCurrentKeys: BuildAdditionalCurrentKeys(kind, savedCatalog, currentCatalog),
            $"Saved {label} '{saved}' does not match current '{current}'."));
    }

    private static void AddCountDifference(
        List<RuntimeSaveContentCompatibilityDifferenceData> differences,
        RuntimeSaveContentCompatibilityDifferenceKind kind,
        string label,
        int saved,
        int current,
        RuntimeSaveContentCatalogSummaryData savedCatalog,
        RuntimeSaveContentCatalogSummaryData currentCatalog)
    {
        if (saved == current)
            return;

        var savedText = saved.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var currentText = current.ToString(System.Globalization.CultureInfo.InvariantCulture);
        differences.Add(new RuntimeSaveContentCompatibilityDifferenceData(
            kind,
            label,
            savedText,
            currentText,
            HasSavedCatalogKeys: HasCatalogKeys(kind, savedCatalog),
            HasCurrentCatalogKeys: HasCatalogKeys(kind, currentCatalog),
            MissingCurrentKeys: BuildMissingCurrentKeys(kind, savedCatalog, currentCatalog),
            AdditionalCurrentKeys: BuildAdditionalCurrentKeys(kind, savedCatalog, currentCatalog),
            $"Saved {label} {savedText} does not match current {currentText}."));
    }

    private static RuntimeSaveContentCatalogSummaryData NormalizeCatalog(
        RuntimeSaveContentCatalogSummaryData catalog)
    {
        if (!catalog.HasCatalog)
            return RuntimeSaveContentCatalogSummaryData.Unavailable;

        return new RuntimeSaveContentCatalogSummaryData(
            HasCatalog: true,
            MaterialNames: NormalizeKeys(catalog.MaterialNames),
            TerrainKindNames: NormalizeKeys(catalog.TerrainKindNames),
            ConstructionIds: NormalizeKeys(catalog.ConstructionIds),
            RecipeIds: NormalizeKeys(catalog.RecipeIds),
            GeologyIds: NormalizeKeys(catalog.GeologyIds),
            ZoneIds: NormalizeKeys(catalog.ZoneIds));
    }

    private static string[] NormalizeKeys(string[]? keys)
    {
        return keys == null
            ? Array.Empty<string>()
            : keys
                .Where(static key => !string.IsNullOrWhiteSpace(key))
                .OrderBy(static key => key, StringComparer.Ordinal)
                .ToArray();
    }

    private static bool HasCatalogKeys(
        RuntimeSaveContentCompatibilityDifferenceKind kind,
        RuntimeSaveContentCatalogSummaryData catalog)
    {
        return catalog.HasCatalog
            && (kind is RuntimeSaveContentCompatibilityDifferenceKind.MaterialCount
                or RuntimeSaveContentCompatibilityDifferenceKind.TerrainKindCount
                or RuntimeSaveContentCompatibilityDifferenceKind.ConstructionCount
                or RuntimeSaveContentCompatibilityDifferenceKind.RecipeCount
                or RuntimeSaveContentCompatibilityDifferenceKind.GeologyCount
                or RuntimeSaveContentCompatibilityDifferenceKind.ZoneCount);
    }

    private static string[] BuildMissingCurrentKeys(
        RuntimeSaveContentCompatibilityDifferenceKind kind,
        RuntimeSaveContentCatalogSummaryData savedCatalog,
        RuntimeSaveContentCatalogSummaryData currentCatalog)
    {
        if (!HasCatalogKeys(kind, savedCatalog) || !HasCatalogKeys(kind, currentCatalog))
            return Array.Empty<string>();

        return SelectKeys(kind, savedCatalog)
            .Except(SelectKeys(kind, currentCatalog), StringComparer.Ordinal)
            .OrderBy(static key => key, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] BuildAdditionalCurrentKeys(
        RuntimeSaveContentCompatibilityDifferenceKind kind,
        RuntimeSaveContentCatalogSummaryData savedCatalog,
        RuntimeSaveContentCatalogSummaryData currentCatalog)
    {
        if (!HasCatalogKeys(kind, savedCatalog) || !HasCatalogKeys(kind, currentCatalog))
            return Array.Empty<string>();

        return SelectKeys(kind, currentCatalog)
            .Except(SelectKeys(kind, savedCatalog), StringComparer.Ordinal)
            .OrderBy(static key => key, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] SelectKeys(
        RuntimeSaveContentCompatibilityDifferenceKind kind,
        RuntimeSaveContentCatalogSummaryData catalog)
    {
        return kind switch
        {
            RuntimeSaveContentCompatibilityDifferenceKind.MaterialCount => catalog.MaterialNames,
            RuntimeSaveContentCompatibilityDifferenceKind.TerrainKindCount => catalog.TerrainKindNames,
            RuntimeSaveContentCompatibilityDifferenceKind.ConstructionCount => catalog.ConstructionIds,
            RuntimeSaveContentCompatibilityDifferenceKind.RecipeCount => catalog.RecipeIds,
            RuntimeSaveContentCompatibilityDifferenceKind.GeologyCount => catalog.GeologyIds,
            RuntimeSaveContentCompatibilityDifferenceKind.ZoneCount => catalog.ZoneIds,
            _ => Array.Empty<string>()
        };
    }
}
