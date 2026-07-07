namespace HumanFortress.Contracts.Content.Registry;

public interface IRuntimeMaterialCatalog
{
    ContentVersion Version { get; }

    string ContentHash { get; }

    MaterialDefinition? GetMaterial(ushort id);

    MaterialDefinition? GetMaterial(string name);

    ushort? GetMaterialId(string name);

    ushort ResolveMaterial(string name, ushort fallback = 0);

    IEnumerable<MaterialDefinition> GetMaterialsByCategory(string category);

    IEnumerable<MaterialDefinition> GetMaterialsByTag(string tag);

    bool HasMaterial(ushort id);

    bool HasMaterial(string name);

    Dictionary<string, ushort> GetNameToIdSnapshot();

    ushort? ResolveStringId(string stringId);
}

public interface IRuntimeTerrainKindCatalog
{
    ContentVersion Version { get; }

    TerrainBitLayout BitLayout { get; }

    TerrainKindDefinition? GetKind(byte id);

    TerrainKindDefinition? GetKind(string name);

    byte? GetKindId(string name);

    byte ResolveKind(string name, byte fallback = 0);

    bool IsMaterialAllowed(TerrainKindDefinition kind, string materialCategory);

    IEnumerable<TerrainKindDefinition> GetKindsForMaterial(string materialCategory);

    byte BuildNavigationMask(TerrainKindDefinition kind);

    IEnumerable<TerrainKindDefinition> GetAllKinds();
}
