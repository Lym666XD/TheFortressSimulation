using System.Text.Json;
using HumanFortress.Contracts.Jobs;
using HumanFortress.Content.Loading;

namespace HumanFortress.Content.Definitions;

public sealed class ProfessionRegistry : IProfessionRegistry
{
    public IReadOnlyList<ProfessionDefinition> Definitions { get; }
    public ProfessionDefinition DefaultProfession { get; }

    private ProfessionRegistry(IReadOnlyList<ProfessionDefinition> definitions)
    {
        Definitions = definitions;
        DefaultProfession = definitions.FirstOrDefault(d => d.IsDefault) ?? definitions.First();
    }

    public static ProfessionRegistry Load(string baseDir, Action<string>? log = null)
    {
        try
        {
            var registryFile = FortressContentLoader.ResolveRegistryFile(baseDir, "professions.json");
            if (registryFile.ResolvedPath == null)
            {
                log?.Invoke("[PROFESSIONS] Registry missing; falling back to Laborer-only default.");
                return new ProfessionRegistry(CreateDefaultDefinitions());
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(registryFile.ResolvedPath));
            var list = new List<ProfessionDefinition>();
            if (doc.RootElement.TryGetProperty("professions", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in arr.EnumerateArray())
                {
                    var id = el.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? string.Empty : string.Empty;
                    if (string.IsNullOrWhiteSpace(id)) continue;

                    var name = el.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? id : id;
                    var isDefault = el.TryGetProperty("default", out var defEl) && defEl.GetBoolean();
                    var tags = el.TryGetProperty("job_tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array
                        ? tagsEl.EnumerateArray()
                            .Select(t => t.GetString() ?? string.Empty)
                            .Where(t => !string.IsNullOrWhiteSpace(t))
                            .ToArray()
                        : Array.Empty<string>();

                    list.Add(new ProfessionDefinition(id, name, tags, isDefault));
                }
            }

            if (list.Count == 0)
            {
                list.AddRange(CreateDefaultDefinitions());
            }

            return new ProfessionRegistry(list);
        }
        catch (Exception ex)
        {
            log?.Invoke($"[PROFESSIONS] Failed to load registry: {ex.Message}");
            return new ProfessionRegistry(CreateDefaultDefinitions());
        }
    }

    public IReadOnlyList<ProfessionDefinition> GetProfessionsForJob(string jobTag)
    {
        if (string.IsNullOrWhiteSpace(jobTag)) return Definitions;
        var matches = Definitions
            .Where(d => d.JobTags.Contains(jobTag, StringComparer.OrdinalIgnoreCase))
            .ToList();
        return matches.Count > 0 ? matches : Definitions;
    }

    private static ProfessionDefinition[] CreateDefaultDefinitions()
    {
        return
        [
            new ProfessionDefinition("laborer", "Laborer", ["hauling", "construction", "support"], true)
        ];
    }
}
