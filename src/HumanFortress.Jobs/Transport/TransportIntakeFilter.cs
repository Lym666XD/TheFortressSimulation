using HumanFortress.Simulation.Jobs;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Transport;

internal sealed class TransportIntakeFilter
{
    private readonly WorldModel _world;

    internal TransportIntakeFilter(WorldModel world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    internal List<TransportRequest> FilterReadyRequests(IEnumerable<TransportRequest> requests, ulong tick)
    {
        var seen = new HashSet<Guid>();
        return requests
            .OrderBy(r => r.ItemGuid)
            .Where(r =>
            {
                if (!seen.Add(r.ItemGuid)) return false;
                var item = _world.Items.GetInstance(r.ItemGuid);
                if (item == null || !item.IsOnGround) return false;
                if (_world.Reservations.IsItemReserved(r.ItemGuid, tick)) return false;
                return true;
            })
            .ToList();
    }
}
