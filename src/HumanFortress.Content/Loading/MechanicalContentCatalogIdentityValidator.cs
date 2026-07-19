using HumanFortress.Content.Definitions;
using HumanFortress.Contracts.Content.Identity;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Contracts.Jobs;
using StructuredContentRegistry = HumanFortress.Content.Registry.ContentRegistry;

namespace HumanFortress.Content.Loading;

/// <summary>
/// Binds the catalogs Runtime will actually consume to the strict mechanical
/// identity. Source identity is insufficient when a permissive legacy loader
/// skipped, renamed, or replaced a definition before composition.
/// </summary>
internal static class MechanicalContentCatalogIdentityValidator
{
    private static readonly IReadOnlySet<string> ProductionNamespaces = new HashSet<string>(
        new[]
        {
            "construction",
            "creature",
            "geology",
            "item",
            "material",
            "profession",
            "recipe",
            "stockpile.preset",
            "terrain",
            "zone",
        },
        StringComparer.Ordinal);

    internal static IReadOnlyList<MechanicalContentIssueData> Validate(
        StructuredContentRegistry structuredRegistry,
        CoreContentCatalogLoadResult coreCatalogs,
        IProfessionRegistry? professions,
        IReadOnlyList<StockpilePresetDefinition> stockpilePresetDefinitions,
        MechanicalContentIdentityData identity)
    {
        ArgumentNullException.ThrowIfNull(structuredRegistry);
        ArgumentNullException.ThrowIfNull(coreCatalogs);
        ArgumentNullException.ThrowIfNull(stockpilePresetDefinitions);
        ArgumentNullException.ThrowIfNull(identity);

        var rows = new List<CatalogIdentityRow>();
        AddRows(
            rows,
            "item",
            coreCatalogs.Items.Catalog.GetAllDefinitions().Select(static value => value.Id));
        AddRows(
            rows,
            "creature",
            coreCatalogs.Creatures.Catalog.GetAllDefinitions().Select(static value => value.Id));
        AddRows(
            rows,
            "construction",
            coreCatalogs.Constructions.Catalog.GetAllConstructions().Select(static value => value.Id));
        AddRows(
            rows,
            "recipe",
            coreCatalogs.Recipes.Catalog.GetAllRecipes().Select(static value => value.Id));
        AddRows(rows, "material", structuredRegistry.GetLoadedMaterialCanonicalIds());
        AddRows(
            rows,
            "terrain",
            structuredRegistry.TerrainKinds.GetAllKinds().Select(static value => value.Name));
        AddRows(rows, "geology", structuredRegistry.GeologyEntries.Keys);
        AddRows(rows, "zone", structuredRegistry.Zones.Keys);
        if (professions != null)
            AddRows(rows, "profession", professions.Definitions.Select(static value => value.Id));
        AddRows(rows, "stockpile.preset", stockpilePresetDefinitions.Select(static value => value.Id));

        var issues = new List<MechanicalContentIssueData>();
        foreach (var group in rows
                     .GroupBy(static row => row.QualifiedId, StringComparer.Ordinal)
                     .OrderBy(static group => group.Key, StringComparer.Ordinal))
        {
            if (group.Count() > 1)
            {
                issues.Add(Error(
                    "Content.Identity.CatalogDuplicateId",
                    group.First(),
                    $"Loaded catalog id '{group.Key}' appears {group.Count()} times."));
            }
        }

        foreach (var group in rows
                     .GroupBy(static row => row.QualifiedId, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            var spellings = group
                .Select(static row => row.QualifiedId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static value => value, StringComparer.Ordinal)
                .ToArray();
            if (spellings.Length <= 1)
                continue;

            issues.Add(Error(
                "Content.Identity.CatalogAmbiguousId",
                group.First(),
                $"Loaded catalog ids are case-ambiguous: {string.Join(", ", spellings)}."));
        }

        var boundHandleOwners = new Dictionary<uint, CatalogIdentityRow>();
        var distinctRows = rows
            .Distinct(CatalogIdentityRowComparer.Instance)
            .OrderBy(static value => value.QualifiedId, StringComparer.Ordinal)
            .ToArray();
        foreach (var row in distinctRows
                     .OrderBy(static value => value.QualifiedId, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(row.CanonicalId))
            {
                issues.Add(Error(
                    "Content.Identity.CatalogIdMissing",
                    row,
                    $"Loaded {row.Namespace} catalog contains an empty canonical id."));
                continue;
            }

            if (!identity.TryGetLocalHandle(row.Namespace, row.CanonicalId, out var handle))
            {
                issues.Add(Error(
                    "Content.Identity.CatalogHandleMissing",
                    row,
                    $"Loaded catalog id '{row.QualifiedId}' has no compiled local handle."));
                continue;
            }

            if (boundHandleOwners.TryGetValue(handle, out var owner)
                && owner != row)
            {
                issues.Add(Error(
                    "Content.Identity.CatalogHandleCollision",
                    row,
                    $"Loaded catalog ids '{owner.QualifiedId}' and '{row.QualifiedId}' bind local handle {handle}."));
                continue;
            }

            boundHandleOwners[handle] = row;
        }

        var loadedQualifiedIds = distinctRows
            .Select(static row => row.QualifiedId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var handle in identity.LocalHandles
                     .Where(handle => ProductionNamespaces.Contains(handle.Namespace))
                     .OrderBy(static handle => handle.QualifiedId, StringComparer.Ordinal))
        {
            if (loadedQualifiedIds.Contains(handle.QualifiedId))
                continue;

            issues.Add(Error(
                "Content.Identity.CatalogDefinitionMissing",
                new CatalogIdentityRow(handle.Namespace, handle.CanonicalId),
                $"Compiled active id '{handle.QualifiedId}' is missing from the loaded Runtime catalog."));
        }

        return issues
            .OrderBy(static issue => issue.Source, StringComparer.Ordinal)
            .ThenBy(static issue => issue.Path, StringComparer.Ordinal)
            .ThenBy(static issue => issue.Code, StringComparer.Ordinal)
            .ThenBy(static issue => issue.Message, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddRows(
        ICollection<CatalogIdentityRow> rows,
        string @namespace,
        IEnumerable<string> canonicalIds)
    {
        foreach (var canonicalId in canonicalIds)
            rows.Add(new CatalogIdentityRow(@namespace, canonicalId ?? string.Empty));
    }

    private static MechanicalContentIssueData Error(
        string code,
        CatalogIdentityRow row,
        string message)
    {
        return new MechanicalContentIssueData(
            MechanicalContentIssueSeverity.Error,
            code,
            $"catalog/{row.Namespace}",
            "$[" + row.CanonicalId + "]",
            message);
    }

    private readonly record struct CatalogIdentityRow(string Namespace, string CanonicalId)
    {
        internal string QualifiedId => $"{Namespace}:{CanonicalId}";
    }

    private sealed class CatalogIdentityRowComparer : IEqualityComparer<CatalogIdentityRow>
    {
        internal static CatalogIdentityRowComparer Instance { get; } = new();

        bool IEqualityComparer<CatalogIdentityRow>.Equals(
            CatalogIdentityRow left,
            CatalogIdentityRow right)
        {
            return string.Equals(left.Namespace, right.Namespace, StringComparison.Ordinal)
                && string.Equals(left.CanonicalId, right.CanonicalId, StringComparison.Ordinal);
        }

        int IEqualityComparer<CatalogIdentityRow>.GetHashCode(CatalogIdentityRow value)
        {
            return HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(value.Namespace),
                StringComparer.Ordinal.GetHashCode(value.CanonicalId));
        }
    }
}
