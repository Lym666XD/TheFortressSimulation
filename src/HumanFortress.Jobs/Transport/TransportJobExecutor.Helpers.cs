using HumanFortress.Contracts.Navigation;

namespace HumanFortress.Jobs.Transport;

internal sealed partial class TransportJobExecutor
{
    private Point3 GetCreaturePos(Guid creatureId)
    {
        var cr = _world.Creatures.GetInstance(creatureId);
        if (cr == null)
        {
            return new Point3(0, 0, 0);
        }

        return new Point3(cr.Position.X, cr.Position.Y, cr.Z);
    }

    private Point3 GetItemPos(ActiveJob job)
    {
        var it = _world.Items.GetInstance(job.ItemId);
        if (it == null)
        {
            return new Point3(0, 0, 0);
        }

        return new Point3(it.Position.X, it.Position.Y, it.Z);
    }

    private static uint SeedFrom(Guid a, Guid b)
    {
        unchecked
        {
            var ba = a.ToByteArray();
            var bb = b.ToByteArray();
            uint s = 2166136261;
            foreach (var t in ba)
            {
                s = (s ^ t) * 16777619;
            }

            foreach (var t in bb)
            {
                s = (s ^ t) * 16777619;
            }

            return s;
        }
    }
}
