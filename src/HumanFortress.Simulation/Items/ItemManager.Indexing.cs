using SadRogue.Primitives;

namespace HumanFortress.Simulation.Items;

internal sealed partial class ItemManager
{
    private static (int, int, int) KeyFor(Point pos, int z) => (pos.X, pos.Y, z);

    private void EntityKeyIndexAdd(Guid id)
    {
        var claim = _identityIndex.TryAdd(id);
        if (!claim.Success)
        {
            throw new InvalidOperationException(claim.Describe("item"));
        }

        LegacyEntityIdIndexAdd(id);
    }

    private void EntityKeyIndexRemove(Guid id)
    {
        if (!_identityIndex.Remove(id))
        {
            throw new InvalidOperationException(
                $"The live item identity index does not own GUID {id}.");
        }

        LegacyEntityIdIndexRemove(id);
    }

    private void LegacyEntityIdIndexAdd(Guid id)
    {
        uint entityId = ToEntityId(id);
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
        uint entityId = ToEntityId(id);
        if (!_legacyEntityIdIndex.TryGetValue(entityId, out var ids))
            return;

        ids.Remove(id);
        if (ids.Count == 0)
            _legacyEntityIdIndex.Remove(entityId);
    }

    private void IndexAdd(Guid id, Point pos, int z)
    {
        var key = KeyFor(pos, z);
        if (!_posIndex.TryGetValue(key, out var list))
        {
            list = new List<Guid>();
            _posIndex[key] = list;
        }

        list.Add(id);
        list.Sort();
    }

    private void IndexRemove(Guid id, Point pos, int z)
    {
        var key = KeyFor(pos, z);
        if (_posIndex.TryGetValue(key, out var list))
        {
            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] == id)
                    list.RemoveAt(i);
            }

            if (list.Count == 0)
                _posIndex.Remove(key);
        }
    }
}
