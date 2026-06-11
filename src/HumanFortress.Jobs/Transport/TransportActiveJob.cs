using HumanFortress.Navigation;
using HumanFortress.Simulation.Jobs;

namespace HumanFortress.Jobs.Transport;

internal enum JobStage
{
    ToItem,
    ToDest
}

internal sealed class ActiveJob
{
    public Guid CreatureId { get; set; }
    public Guid ItemId { get; set; }
    public Point3 Dest { get; set; }
    public JobStage Stage { get; set; }
    public int Quantity { get; set; }
    public int InvalidReplanCount { get; set; }
    public TransportReason Reason { get; set; }
}
