using System.Text.Json;
using HumanFortress.Contracts.Content.Identity;

namespace HumanFortress.Content.Identity;

internal static class MechanicalContentReferenceValidator
{
    internal static void Validate(
        IReadOnlyList<MechanicalContentIdentityCompiler.ParsedMechanicalContentSource> sources,
        IReadOnlyList<MechanicalContentLocalHandleData> handles,
        ICollection<MechanicalContentIssueData> issues)
    {
        var idsByNamespace = handles
            .GroupBy(static row => row.Namespace, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.Select(static row => row.CanonicalId).ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);

        foreach (var source in sources
                     .OrderBy(static source => source.Source.SourceId, StringComparer.Ordinal))
        {
            ValidateElement(
                source.Document.RootElement,
                source.Source,
                "$",
                propertyName: null,
                idsByNamespace,
                issues);
        }
    }

    private static void ValidateElement(
        JsonElement element,
        MechanicalContentSourceDocument source,
        string path,
        string? propertyName,
        IReadOnlyDictionary<string, HashSet<string>> idsByNamespace,
        ICollection<MechanicalContentIssueData> issues)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var targetNamespace = GetReferenceNamespace(source.HandleNamespace, propertyName);
            if (targetNamespace != null)
            {
                ValidateReference(
                    targetNamespace,
                    element.GetString() ?? string.Empty,
                    source.SourceId,
                    path,
                    idsByNamespace,
                    issues);
            }
            return;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject()
                         .OrderBy(static property => property.Name, StringComparer.Ordinal))
            {
                ValidateElement(
                    property.Value,
                    source,
                    path + "/" + EscapePointer(property.Name),
                    property.Name,
                    idsByNamespace,
                    issues);
            }
            return;
        }

        if (element.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var child in element.EnumerateArray())
        {
            ValidateElement(
                child,
                source,
                path + "/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                propertyName,
                idsByNamespace,
                issues);
            index++;
        }
    }

    private static string? GetReferenceNamespace(string sourceNamespace, string? propertyName)
    {
        if (propertyName == null)
            return null;

        if (propertyName.Equals("fixed_material", StringComparison.OrdinalIgnoreCase))
            return "material";
        if (propertyName.Equals("result_material_id", StringComparison.OrdinalIgnoreCase))
            return "material";
        if (propertyName.Equals("def_id", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("defId", StringComparison.Ordinal)
            || propertyName.Equals("item_id", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("itemIds", StringComparison.Ordinal))
        {
            return "item";
        }
        if (propertyName.Equals("body_plan_id", StringComparison.OrdinalIgnoreCase))
            return "creature.body_plan";
        if (sourceNamespace.Equals("geology", StringComparison.Ordinal)
            && propertyName.Equals("material", StringComparison.OrdinalIgnoreCase))
        {
            return "material";
        }
        if (sourceNamespace.Equals("tuning.ore", StringComparison.Ordinal)
            && propertyName.Equals("id", StringComparison.Ordinal))
        {
            return "geology";
        }
        if (sourceNamespace.Equals("stockpile.preset", StringComparison.Ordinal)
            && propertyName.Equals("materials", StringComparison.OrdinalIgnoreCase))
        {
            return "material";
        }
        if (sourceNamespace.Equals("recipe", StringComparison.Ordinal)
            && (propertyName.Equals("workshop", StringComparison.OrdinalIgnoreCase)
                || propertyName.Equals("workshop_id", StringComparison.OrdinalIgnoreCase)
                || propertyName.Equals("workshops", StringComparison.OrdinalIgnoreCase)))
        {
            return "construction";
        }
        if (sourceNamespace.Equals("recipe", StringComparison.Ordinal)
            && (propertyName.Equals("enablers", StringComparison.OrdinalIgnoreCase)
                || propertyName.Equals("requires_enablers", StringComparison.OrdinalIgnoreCase)))
        {
            return "construction.attachment";
        }
        if (sourceNamespace.Equals("construction", StringComparison.Ordinal)
            && propertyName.Equals("upgrade_to", StringComparison.OrdinalIgnoreCase))
        {
            return "construction.attachment";
        }

        return null;
    }

    private static void ValidateReference(
        string targetNamespace,
        string canonicalId,
        string source,
        string path,
        IReadOnlyDictionary<string, HashSet<string>> idsByNamespace,
        ICollection<MechanicalContentIssueData> issues)
    {
        if (string.IsNullOrWhiteSpace(canonicalId))
            return;
        if (idsByNamespace.TryGetValue(targetNamespace, out var ids)
            && ids.Contains(canonicalId))
        {
            return;
        }

        issues.Add(new MechanicalContentIssueData(
            MechanicalContentIssueSeverity.Error,
            "Content.Reference.Missing",
            source,
            path,
            $"Reference '{targetNamespace}:{canonicalId}' does not resolve to a canonical content id."));
    }

    private static string EscapePointer(string value)
    {
        return value.Replace("~", "~0", StringComparison.Ordinal)
            .Replace("/", "~1", StringComparison.Ordinal);
    }
}
