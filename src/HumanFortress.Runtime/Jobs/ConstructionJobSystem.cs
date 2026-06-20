using System;
using HumanFortress.Jobs;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Jobs.Construction;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Orders;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Jobs;

/// <summary>
/// Tick-facing composition shell for the Jobs-owned construction executor.
/// </summary>
public sealed class ConstructionJobSystem : ITick, IUnifiedConstructionJobExecutor
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
        int maxPerTick = 64,
        Action<string>? log = null,
        Action<int, int, int, Rectangle, string, ulong>? workshopCompletion = null)
    {
        var diffEmitter = new ConstructionDiffEmitter(diffLog, itemsDiffLog, SystemId, Priority);
        _executor = new ConstructionJobExecutor(
            world,
            diffEmitter,
            constructions,
            tuning,
            placeableTuning,
            new CallbackConstructionWorkshopCompletionSink(workshopCompletion),
            new ConstructionCallbackJobLogger(log),
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

    private sealed class CallbackConstructionWorkshopCompletionSink : IConstructionWorkshopCompletionSink
    {
        private readonly Action<int, int, int, Rectangle, string, ulong>? _workshopCompletion;

        public CallbackConstructionWorkshopCompletionSink(
            Action<int, int, int, Rectangle, string, ulong>? workshopCompletion)
        {
            _workshopCompletion = workshopCompletion;
        }

        public void NotifyWorkshopComplete(int x, int y, int z, Rectangle footprint, string constructionId, ulong tick)
        {
            var callback = _workshopCompletion ?? UiNotifyWorkshopComplete;
            callback?.Invoke(x, y, z, footprint, constructionId, tick);
        }
    }
}
