using RuntimeGeologyData = HumanFortress.Core.Content.GeologyData;

namespace HumanFortress.Contracts.Content.Registry;

public interface IRuntimeGeologyCatalog
{
    RuntimeGeologyData? GetGeologyEntry(string id);

    RuntimeGeologyData? GetGeologyByHandle(ushort handle);

    ushort GetGeologyHandle(string id);

    bool TryGetGeologyHandleByMaterialAndKind(string materialId, string terrainKindName, out ushort handle);
}
