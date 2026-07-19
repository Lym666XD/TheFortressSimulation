using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace HumanFortress.Content.Definitions;

internal static partial class CoreDataRegistryLoader
{
    private static string[]? ParseStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var arrayElement)
            || arrayElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return arrayElement.EnumerateArray()
            .Select(value => value.GetString() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string? ParseOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
               && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static void AddIfNotBlank(List<string> values, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            values.Add(value);
        }
    }
}
