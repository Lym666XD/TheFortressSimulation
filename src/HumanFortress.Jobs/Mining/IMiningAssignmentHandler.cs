using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Orders;
using SadRogue.Primitives;

namespace HumanFortress.Jobs.Mining;

internal interface IMiningAssignmentHandler
{
    ActiveMiningJob? TryAssign(
        in MiningSystem.PlannedDig dig,
        Point adjacent,
        IReadOnlyList<CreatureInstance> creatures,
        HashSet<Guid> busy,
        ulong tick,
        bool middleAlreadySatisfied);
}
