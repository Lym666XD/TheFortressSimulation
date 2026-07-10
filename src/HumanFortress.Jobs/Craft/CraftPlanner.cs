using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Simulation.Jobs;
using HumanFortress.Simulation.Placeables;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Craft;

/// <summary>
/// Planner that inspects workshop queues, requests materials, and produces planned craft jobs.
/// </summary>
internal sealed class CraftPlanner : ITick, ICraftJobPlanner
{
    private const int RequestRetryTicks = 80;

    private readonly CraftWorkshopLocator _workshops;
    private readonly CraftMaterialReadinessChecker _materialReadiness;
    private readonly ICraftRecipeCatalog _recipes;
    private readonly Queue<PlannedCraftJob> _outbox = new();
    private readonly int _scanBudgetPerTick;

    internal CraftPlanner(
        WorldModel world,
        ITransportIntake transport,
        ICraftRecipeCatalog recipes,
        IConstructionCatalog constructions,
        int scanBudgetPerTick = 24)
    {
        _workshops = new CraftWorkshopLocator(world, constructions);
        _recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
        var transportRequests = new CraftTransportRequestEmitter(world, transport, SystemId);
        _materialReadiness = new CraftMaterialReadinessChecker(
            new CraftInputCounter(world),
            transportRequests,
            RequestRetryTicks);
        _scanBudgetPerTick = Math.Max(1, scanBudgetPerTick);
    }

    internal int Priority => UpdateOrder.Priority.Items;

    internal string SystemId => "Jobs.CraftPlanner";

    internal void ReadTick(ulong tick)
    {
        int scanned = 0;
        foreach (var (placeable, _) in _workshops.EnumerateWorkshops())
        {
            if (scanned >= _scanBudgetPerTick) break;
            scanned++;
            var state = placeable.Workshop;
            if (state == null || state.Queue.Count == 0) continue;
            if (state.ActiveJobs >= state.AllowedWorkers) continue;

            var entry = state.Queue[0];
            var recipe = _recipes.GetRecipe(entry.RecipeId);
            if (recipe == null)
            {
                entry.Status = CraftQueueStatus.Pending;
                entry.BlockingReason = "Recipe missing";
                continue;
            }

            if (!_materialReadiness.HasMaterials(placeable, state, entry, recipe, tick))
                continue;

            if (entry.IsScheduled) continue;
            entry.IsScheduled = true;
            entry.Status = CraftQueueStatus.Scheduled;
            entry.BlockingReason = null;
            var job = new PlannedCraftJob(
                WorkshopGuid: placeable.Guid,
                QueueEntryId: entry.EntryId,
                RecipeId: recipe.Id,
                DurationTicks: recipe.DurationTicks,
                Anchor: placeable.Position,
                Z: placeable.Z);
            _outbox.Enqueue(job);
        }
    }

    internal void WriteTick(ulong tick)
    {
        // Planner is read-only.
    }

    internal int DequeuePlannedJobs(int max, IList<PlannedCraftJob> into)
    {
        int n = 0;
        while (n < max && _outbox.TryDequeue(out var job))
        {
            into.Add(job);
            n++;
        }

        return n;
    }

    int ITick.Priority => Priority;

    string ITick.SystemId => SystemId;

    void ITick.ReadTick(ulong tick) => ReadTick(tick);

    void ITick.WriteTick(ulong tick) => WriteTick(tick);

    int ICraftJobPlanner.DequeuePlannedJobs(int max, IList<PlannedCraftJob> into) => DequeuePlannedJobs(max, into);
}
