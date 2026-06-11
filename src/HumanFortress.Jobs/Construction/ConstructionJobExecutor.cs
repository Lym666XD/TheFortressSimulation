using HumanFortress.Core.Content.Registry;
using HumanFortress.Simulation.Placeables;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Construction;

internal sealed class ConstructionJobExecutor
{
    public const string SystemId = "Jobs.Construction";

    private readonly WorldModel _world;
    private readonly IConstructionDiffEmitter _diffEmitter;
    private readonly ConstructionSiteProgress _progress;
    private readonly ConstructionCompletionCoordinator _completionCoordinator;
    private readonly int _maxPerTick;

    public ConstructionJobExecutor(
        WorldModel world,
        IConstructionDiffEmitter diffEmitter,
        IConstructionCatalog constructions,
        IConstructionWorkshopCompletionSink? workshopCompletionSink,
        IConstructionJobLogger? logger,
        int maxPerTick = 64)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _diffEmitter = diffEmitter ?? throw new ArgumentNullException(nameof(diffEmitter));
        constructions = constructions ?? throw new ArgumentNullException(nameof(constructions));
        var log = logger ?? NullConstructionJobLogger.Instance;
        var footprintCells = new ConstructionFootprintCells(world);
        var materials = new ConstructionMaterialTracker(world, footprintCells, diffEmitter, log);
        var siteSafety = new ConstructionSiteSafety(world, diffEmitter, log);
        var completion = new ConstructionCompletionApplier(world, diffEmitter, constructions, workshopCompletionSink, log);
        var tuning = ConstructionTuning.LoadFromContent();
        _progress = new ConstructionSiteProgress(world, materials, tuning, log);
        _completionCoordinator = new ConstructionCompletionCoordinator(materials, siteSafety, completion, log);
        _maxPerTick = Math.Max(1, maxPerTick);
    }

    public int LastProcessedSites { get; private set; }

    public int LastIntakeCount { get; private set; }

    public void WriteTick(ulong tick)
    {
        if (!_diffEmitter.CanEmitWorldDiffs)
        {
            return;
        }

        int processed = 0;
        foreach (var chunk in _world.GetAllChunks())
        {
            var pd = chunk.GetPlaceableData();
            if (pd == null)
            {
                continue;
            }

            foreach (PlaceableInstance p in pd.GetAllOwnedPlaceables().ToList())
            {
                if (processed >= _maxPerTick)
                {
                    break;
                }

                if (p.ConstructionSite == null)
                {
                    continue;
                }

                if (_progress.AdvanceIfReady(p, tick) && _completionCoordinator.TryComplete(p, tick))
                {
                    processed++;
                }
            }

            if (processed >= _maxPerTick)
            {
                break;
            }
        }

        LastProcessedSites = processed;
        LastIntakeCount = processed;
    }
}
