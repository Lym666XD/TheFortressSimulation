using HumanFortress.Core.Simulation;

namespace HumanFortress.Simulation.Creatures;

internal sealed partial class CreatureManager
{
    private void EntityKeyIndexAdd(Guid id)
    {
        _entityKeyIndex[DiffTargetEncoding.EntityKey(id)] = id;
        LegacyEntityIdIndexAdd(id);
    }

    private void EntityKeyIndexRemove(Guid id)
    {
        _entityKeyIndex.Remove(DiffTargetEncoding.EntityKey(id));
        LegacyEntityIdIndexRemove(id);
    }

    private void LegacyEntityIdIndexAdd(Guid id)
    {
        uint entityId = DiffTargetEncoding.EntityId(id);
        if (!_legacyEntityIdIndex.TryGetValue(entityId, out var ids))
        {
            ids = new List<Guid>();
            _legacyEntityIdIndex[entityId] = ids;
        }

        if (ids.Contains(id))
            return;

        ids.Add(id);
        ids.Sort();
    }

    private void LegacyEntityIdIndexRemove(Guid id)
    {
        uint entityId = DiffTargetEncoding.EntityId(id);
        if (!_legacyEntityIdIndex.TryGetValue(entityId, out var ids))
            return;

        ids.Remove(id);
        if (ids.Count == 0)
            _legacyEntityIdIndex.Remove(entityId);
    }
}
