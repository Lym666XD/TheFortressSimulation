using System;
using HumanFortress.Jobs;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Jobs.Construction;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Orders;
using HumanFortress.Simulation.Stockpile;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Jobs;

/// <summary>
/// Tick-facing composition shell for the Jobs-owned construction executor.
/// </summary>
internal sealed class ConstructionJobSystem : ITick, IUnifiedConstructionJobExecutor
{
    private readonly ConstructionJobExecutor _executor;

    internal ConstructionJobSystem(
        HumanFortress.Simulation.World.World world,
        ConstructionSystem planner,
        DiffLog? diffLog,
        ItemsDiffLog itemsDiffLog,
        IConstructionCatalog constructions,
        ConstructionTuning tuning,
        PlaceableTuning placeableTuning,
        int maxPerTick = 64,
        Action<string>? log = null,
        Action<int, int, int, Rectangle, string, ulong>? workshopCompletion = null,
        StockpileDiffLog? stockpileDiffLog = null)
    {
        var diffEmitter = new ConstructionDiffEmitter(
            diffLog,
            itemsDiffLog,
            SystemId,
            Priority,
            world,
            stockpileDiffLog);
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

    internal int LastProcessedSites => _executor.LastProcessedSites;

    internal int LastIntakeCount => _executor.LastIntakeCount;

    internal int Priority => UpdateOrder.Priority.WorldTerrain;

    internal string SystemId => ConstructionJobExecutor.SystemId;

    int IUnifiedJobExecutor.LastIntakeCount => LastIntakeCount;

    int ITick.Priority => Priority;

    string ITick.SystemId => SystemId;

    void ITick.ReadTick(ulong tick) => ReadTick(tick);

    void ITick.WriteTick(ulong tick) => WriteTick(tick);

    internal void ReadTick(ulong tick)
    {
    }

    internal void WriteTick(ulong tick) => _executor.WriteTick(tick);

    private sealed class CallbackConstructionWorkshopCompletionSink : IConstructionWorkshopCompletionSink
    {
        private readonly Action<int, int, int, Rectangle, string, ulong>? _workshopCompletion;

        internal CallbackConstructionWorkshopCompletionSink(
            Action<int, int, int, Rectangle, string, ulong>? workshopCompletion)
        {
            _workshopCompletion = workshopCompletion;
        }

        void IConstructionWorkshopCompletionSink.NotifyWorkshopComplete(int x, int y, int z, Rectangle footprint, string constructionId, ulong tick)
        {
            _workshopCompletion?.Invoke(x, y, z, footprint, constructionId, tick);
        }
    }
}
