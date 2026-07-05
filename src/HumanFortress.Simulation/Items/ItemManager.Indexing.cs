using SadRogue.Primitives;

namespace HumanFortress.Simulation.Items;

internal sealed partial class ItemManager
{
    private static (int, int, int) KeyFor(Point pos, int z) => (pos.X, pos.Y, z);

    private void IndexAdd(Guid id, Point pos, int z)
    {
        var key = KeyFor(pos, z);
        if (!_posIndex.TryGetValue(key, out var list))
        {
            list = new List<Guid>();
            _posIndex[key] = list;
        }

        list.Add(id);
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
