using HumanFortress.Simulation.Orders;

namespace HumanFortress.Jobs.Mining;

internal sealed class MiningDeferredStairwellBuffer
{
    private const ulong RetryCadenceTicks = 10UL;
    private readonly Queue<MiningSystem.PlannedDig> _queue = new();

    public int Count => _queue.Count;

    public void Enqueue(in MiningSystem.PlannedDig dig)
    {
        _queue.Enqueue(dig);
    }

    public int RetryInto(ulong tick, int intakeBudget, IList<MiningSystem.PlannedDig> into)
    {
        if (into.Count >= intakeBudget || (tick % RetryCadenceTicks) != 0UL)
        {
            return 0;
        }

        int retried = 0;
        while (into.Count < intakeBudget && _queue.Count > 0)
        {
            into.Add(_queue.Dequeue());
            retried++;
        }

        return retried;
    }
}
