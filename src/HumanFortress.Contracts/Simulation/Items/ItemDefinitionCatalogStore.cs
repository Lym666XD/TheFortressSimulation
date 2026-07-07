namespace HumanFortress.Contracts.Simulation.Items;

/// <summary>
/// Immutable read-only item definition catalog snapshot.
/// </summary>
public sealed class ItemDefinitionCatalogStore : IItemDefinitionCatalog
{
    public static ItemDefinitionCatalogStore Empty { get; } = new(
        new Dictionary<string, ItemDefinition>(StringComparer.Ordinal),
        new Dictionary<string, string[]>(StringComparer.Ordinal),
        new Dictionary<string, string[]>(StringComparer.Ordinal));

    private readonly IReadOnlyDictionary<string, ItemDefinition> _definitions;
    private readonly IReadOnlyDictionary<string, string[]> _kindIndex;
    private readonly IReadOnlyDictionary<string, string[]> _tagIndex;

    private ItemDefinitionCatalogStore(
        IReadOnlyDictionary<string, ItemDefinition> definitions,
        IReadOnlyDictionary<string, string[]> kindIndex,
        IReadOnlyDictionary<string, string[]> tagIndex)
    {
        _definitions = definitions;
        _kindIndex = kindIndex;
        _tagIndex = tagIndex;
    }

    public int DefinitionCount => _definitions.Count;

    public static ItemDefinitionCatalogStore FromDefinitions(IEnumerable<ItemDefinition> definitions)
    {
        var definitionMap = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            definitionMap[definition.Id] = definition;
        }

        return new ItemDefinitionCatalogStore(
            definitionMap,
            BuildKindIndex(definitionMap.Values),
            BuildTagIndex(definitionMap.Values));
    }

    public ItemDefinition? GetDefinition(string id)
    {
        return _definitions.GetValueOrDefault(id);
    }

    public IEnumerable<ItemDefinition> GetAllDefinitions()
    {
        return _definitions.Values;
    }

    public IEnumerable<ItemDefinition> GetByKind(string kind)
    {
        var normalizedKind = kind.ToLowerInvariant();
        return _kindIndex.TryGetValue(normalizedKind, out var ids)
            ? ids.Select(id => _definitions[id])
            : Enumerable.Empty<ItemDefinition>();
    }

    public IEnumerable<string> GetAvailableKinds()
    {
        return _kindIndex.Keys.OrderBy(kind => kind);
    }

    public IEnumerable<ItemDefinition> GetByTag(string tag)
    {
        return _tagIndex.TryGetValue(tag, out var ids)
            ? ids.Select(id => _definitions[id])
            : Enumerable.Empty<ItemDefinition>();
    }

    private static Dictionary<string, string[]> BuildKindIndex(IEnumerable<ItemDefinition> definitions)
    {
        var index = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            var kind = definition.Kind.ToLowerInvariant();
            if (!index.TryGetValue(kind, out var ids))
            {
                ids = new List<string>();
                index[kind] = ids;
            }

            ids.Add(definition.Id);
        }

        return FreezeIndex(index);
    }

    private static Dictionary<string, string[]> BuildTagIndex(IEnumerable<ItemDefinition> definitions)
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

        return FreezeIndex(index);
    }

    private static Dictionary<string, string[]> FreezeIndex(Dictionary<string, List<string>> index)
    {
        var frozen = new Dictionary<string, string[]>(index.Count, StringComparer.Ordinal);
        foreach (var (key, ids) in index)
        {
            frozen[key] = ids.ToArray();
        }

        return frozen;
    }
}
