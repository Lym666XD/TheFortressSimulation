using HumanFortress.Contracts.Navigation;

namespace HumanFortress.Jobs.Transport;

internal interface ITransportMovementDiffEmitter
{
    void MoveCreature(Guid creatureId, Point3 position);
}
