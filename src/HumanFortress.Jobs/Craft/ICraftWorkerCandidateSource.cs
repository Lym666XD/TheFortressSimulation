using HumanFortress.Navigation;
using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Jobs;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Craft;

internal interface ICraftWorkerCandidateSource
{
    IEnumerable<CreatureInstance> SelectCandidates(
        WorldModel world,
        string jobTag,
        HashSet<Guid> busy,
        ReservationManager reservations,
        ulong currentTick,
        Point3 referencePoint);
}
