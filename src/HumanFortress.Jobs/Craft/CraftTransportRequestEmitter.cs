using HumanFortress.Simulation.Jobs;
using HumanFortress.Simulation.Placeables;
using SadRogue.Primitives;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Craft;

internal sealed class CraftTransportRequestEmitter
{
    private readonly WorldModel _world;
    private readonly ITransportIntake _transport;
    private readonly string _systemId;

    public CraftTransportRequestEmitter(WorldModel world, ITransportIntake transport, string systemId)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _systemId = systemId ?? throw new ArgumentNullException(nameof(systemId));
    }

    public int RequestMaterials(PlaceableInstance workshop, string defId, int amountNeeded, ulong tick)
    {
        int requested = 0;
        foreach (var item in _world.Items.GetGroundInstances().OrderBy(i => i.Guid))
        {
            if (requested >= amountNeeded)
            {
                break;
            }

            if (item.DefinitionId != defId)
            {
                continue;
            }

            if (_world.Reservations.IsItemReserved(item.Guid, tick))
            {
                continue;
            }

            if (CraftInputCounter.IsInInputArea(workshop, item.Position.X, item.Position.Y, item.Z))
            {
                continue;
            }

            int take = Math.Min(amountNeeded - requested, item.StackCount);
            if (take <= 0)
            {
                continue;
            }

            var dropCell = new Point(workshop.Position.X, workshop.Position.Y);
            var request = new TransportRequest(
                ItemGuid: item.Guid,
                From: item.Position,
                FromZ: item.Z,
                To: dropCell,
                ToZ: workshop.Z,
                Quantity: take,
                Reason: TransportReason.ToWorkshopInput,
                Priority: 45,
                RequestorId: _systemId,
                CreatedTick: tick,
                Seed: CraftTransportSeed.From(item.Guid));
            _transport.Enqueue(in request);
            requested += take;
        }

        return requested;
    }
}
