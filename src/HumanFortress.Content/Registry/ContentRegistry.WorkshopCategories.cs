using System.Text.Json;

namespace HumanFortress.Content.Registry;

internal sealed partial class ContentRegistry
{
    private void LoadWorkshopCategories(string file)
    {
        if (!File.Exists(file))
        {
            ValidationResult.Warnings.Add($"Workshop category file not found: {file}");
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(
                File.ReadAllText(file),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                ValidationResult.Errors.Add($"Workshop category file must contain an object: {file}");
                return;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.NameEquals("_comment"))
                    continue;
                if (property.Value.ValueKind != JsonValueKind.Array)
                {
                    ValidationResult.Errors.Add(
                        $"Workshop category '{property.Name}' must contain a tag array in {Path.GetFileName(file)}");
                    continue;
                }
                if (_workshopCategoryTags.ContainsKey(property.Name))
                {
                    ValidationResult.Errors.Add(
                        $"Duplicate workshop category '{property.Name}' in {Path.GetFileName(file)}");
                    continue;
                }

                var tags = property.Value.EnumerateArray()
                    .Where(static element => element.ValueKind == JsonValueKind.String)
                    .Select(static element => element.GetString() ?? string.Empty)
                    .Where(static tag => !string.IsNullOrWhiteSpace(tag))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static tag => tag, StringComparer.Ordinal)
                    .ToArray();
                if (tags.Length == 0)
                {
                    ValidationResult.Errors.Add(
                        $"Workshop category '{property.Name}' has no tags in {Path.GetFileName(file)}");
                    continue;
                }

                _workshopCategoryTags.Add(property.Name, tags);
            }

            ContentRegistryDiagnostics.Emit(
                $"[ContentRegistry] Loaded {_workshopCategoryTags.Count} workshop categories");
        }
        catch (Exception ex)
        {
            ValidationResult.Errors.Add(
                $"Error loading workshop categories {Path.GetFileName(file)}: {ex.Message}");
        }
    }
}
