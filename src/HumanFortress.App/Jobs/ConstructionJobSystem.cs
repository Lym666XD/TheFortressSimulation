using HumanFortress.Core.Content.Registry;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Jobs.Construction;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Orders;
using SadRogue.Primitives;

namespace HumanFortress.App.Jobs;

/// <summary>
/// App-owned composition shell for the Jobs-owned construction executor.
/// </summary>
public sealed class ConstructionJobSystem : ITick
{
    private readonly ConstructionJobExecutor _executor;

    public ConstructionJobSystem(
        HumanFortress.Simulation.World.World world,
        ConstructionSystem planner,
        DiffLog? diffLog,
        ItemsDiffLog itemsDiffLog,
        IConstructionCatalog constructions,
        ConstructionTuning tuning,
        PlaceableTuning placeableTuning,
        int maxPerTick = 64)
    {
        var diffEmitter = new ConstructionDiffEmitter(diffLog, itemsDiffLog, SystemId, Priority);
        _executor = new ConstructionJobExecutor(
            world,
            diffEmitter,
            constructions,
            tuning,
            placeableTuning,
            new AppConstructionWorkshopCompletionSink(),
            AppConstructionJobLogger.Instance,
            maxPerTick);
    }

    public int LastProcessedSites => _executor.LastProcessedSites;

    public int LastIntakeCount => _executor.LastIntakeCount;

    public static Action<int, int, int, Rectangle, string, ulong>? UiNotifyWorkshopComplete;

    public int Priority => UpdateOrder.Priority.WorldTerrain;

    public string SystemId => ConstructionJobExecutor.SystemId;

    public void ReadTick(ulong tick)
    {
    }

    public void WriteTick(ulong tick) => _executor.WriteTick(tick);
}
