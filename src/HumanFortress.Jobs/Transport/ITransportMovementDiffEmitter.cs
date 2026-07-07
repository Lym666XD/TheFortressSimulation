using HumanFortress.Contracts.Navigation;

namespace HumanFortress.Jobs.Transport;

internal interface ITransportMovementDiffEmitter
{
    void MoveCreature(uint entityId, Point3 position);
}
