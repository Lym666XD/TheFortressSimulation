namespace HumanFortress.Simulation.Creatures;

/// <summary>
/// Immutable read-only creature definition catalog snapshot.
/// </summary>
public sealed class CreatureDefinitionCatalogStore : ICreatureDefinitionCatalog
{
    public static CreatureDefinitionCatalogStore Empty { get; } = new(
        new Dictionary<string, CreatureDefinition>(StringComparer.Ordinal),
        new Dictionary<string, string[]>(StringComparer.Ordinal));

    private readonly IReadOnlyDictionary<string, CreatureDefinition> _definitions;
    private readonly IReadOnlyDictionary<string, string[]> _tagIndex;

    private CreatureDefinitionCatalogStore(
        IReadOnlyDictionary<string, CreatureDefinition> definitions,
        IReadOnlyDictionary<string, string[]> tagIndex)
    {
        _definitions = definitions;
        _tagIndex = tagIndex;
    }

    public int DefinitionCount => _definitions.Count;

    public static CreatureDefinitionCatalogStore FromDefinitions(IEnumerable<CreatureDefinition> definitions)
    {
        var definitionMap = new Dictionary<string, CreatureDefinition>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            definitionMap[definition.Id] = definition;
        }

        return new CreatureDefinitionCatalogStore(definitionMap, BuildTagIndex(definitionMap.Values));
    }

    public CreatureDefinition? GetDefinition(string id)
    {
        return _definitions.GetValueOrDefault(id);
    }

    public IEnumerable<CreatureDefinition> GetAllDefinitions()
    {
        return _definitions.Values;
    }

    public IEnumerable<CreatureDefinition> GetByTag(string tag)
    {
        return _tagIndex.TryGetValue(tag, out var ids)
            ? ids.Select(id => _definitions[id])
            : Enumerable.Empty<CreatureDefinition>();
    }

    private static Dictionary<string, string[]> BuildTagIndex(IEnumerable<CreatureDefinition> definitions)
    {
        var index = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            foreach (var tag in definition.Tags)
            {
                if (!index.TryGetValue(tag, out var ids))
                {
                    ids = new List<string>();
                    index[tag] = ids;
                }

                ids.Add(definition.Id);
            }
        }

        var frozen = new Dictionary<string, string[]>(index.Count, StringComparer.Ordinal);
        foreach (var (key, ids) in index)
        {
            frozen[key] = ids.ToArray();
        }

        return frozen;
    }
}
