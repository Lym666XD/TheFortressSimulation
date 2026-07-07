namespace HumanFortress.Contracts.Content.Registry;

public interface IRuntimeGeologyCatalog
{
    GeologyData? GetGeologyEntry(string id);

    GeologyData? GetGeologyByHandle(ushort handle);

    ushort GetGeologyHandle(string id);

    bool TryGetGeologyHandleByMaterialAndKind(string materialId, string terrainKindName, out ushort handle);
}
