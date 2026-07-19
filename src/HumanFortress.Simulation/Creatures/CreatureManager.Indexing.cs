using HumanFortress.Core.Simulation;

namespace HumanFortress.Simulation.Creatures;

internal sealed partial class CreatureManager
{
    private void EntityKeyIndexAdd(Guid id)
    {
        var claim = _identityIndex.TryAdd(id);
        if (!claim.Success)
        {
            throw new InvalidOperationException(claim.Describe("creature"));
        }

        LegacyEntityIdIndexAdd(id);
    }

    private void EntityKeyIndexRemove(Guid id)
    {
        if (!_identityIndex.Remove(id))
        {
            throw new InvalidOperationException(
                $"The live creature identity index does not own GUID {id}.");
        }

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
