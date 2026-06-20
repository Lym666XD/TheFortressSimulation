using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Jobs.Craft;
using HumanFortress.Jobs.Mining;
using HumanFortress.Jobs.Transport;
using HumanFortress.Navigation;
using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Jobs;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs;

internal sealed class TransportProfessionCandidateSource : ITransportWorkerCandidateSource
{
    private readonly ProfessionAssignments _professions;
    private readonly WorkerSelectionStrategy _workerStrategy;

    public TransportProfessionCandidateSource(
        ProfessionAssignments professions,
        WorkerSelectionStrategy workerStrategy)
    {
        _professions = professions;
        _workerStrategy = workerStrategy;
    }

    public IEnumerable<CreatureInstance> SelectCandidates(
        WorldModel world,
        string jobTag,
        HashSet<Guid> busy,
        ReservationManager reservations,
        ulong currentTick,
        Point3 referencePoint)
    {
        return _professions.SelectCandidates(
            world,
            jobTag,
            _workerStrategy,
            busy,
            reservations,
            currentTick,
            referencePoint);
    }
}

internal sealed class TransportProfessionCompletionSink : ITransportJobCompletionSink
{
    private readonly ProfessionAssignments _professions;
    private readonly string _jobTag;

    public TransportProfessionCompletionSink(ProfessionAssignments professions, string jobTag)
    {
        _professions = professions;
        _jobTag = jobTag;
    }

    public void RecordJobCompletion(Guid workerId, string jobTag)
    {
        _professions.RecordJobCompletion(workerId, string.IsNullOrWhiteSpace(jobTag) ? _jobTag : jobTag);
    }
}

internal sealed class MiningProfessionCandidateSource : IMiningWorkerCandidateSource
{
    private readonly ProfessionAssignments _professions;
    private readonly WorkerSelectionStrategy _workerStrategy;

    public MiningProfessionCandidateSource(
        ProfessionAssignments professions,
        WorkerSelectionStrategy workerStrategy)
    {
        _professions = professions;
        _workerStrategy = workerStrategy;
    }

    public IEnumerable<CreatureInstance> SelectCandidates(
        WorldModel world,
        string jobTag,
        HashSet<Guid> busy,
        ReservationManager reservations,
        ulong currentTick,
        Point3 referencePoint)
    {
        return _professions.SelectCandidates(
            world,
            jobTag,
            _workerStrategy,
            busy,
            reservations,
            currentTick,
            referencePoint);
    }
}

internal sealed class MiningProfessionCompletionSink : IMiningJobCompletionSink
{
    private readonly ProfessionAssignments _professions;
    private readonly string _jobTag;

    public MiningProfessionCompletionSink(ProfessionAssignments professions, string jobTag)
    {
        _professions = professions;
        _jobTag = jobTag;
    }

    public void RecordJobCompletion(Guid workerId, string jobTag)
    {
        _professions.RecordJobCompletion(workerId, string.IsNullOrWhiteSpace(jobTag) ? _jobTag : jobTag);
    }
}

internal sealed class CraftRecipeCatalogAdapter : ICraftRecipeCatalog
{
    private readonly IRecipeCatalog _recipes;

    public CraftRecipeCatalogAdapter(IRecipeCatalog recipes)
    {
        _recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
    }

    public RecipeDefinition? GetRecipe(string recipeId)
    {
        return _recipes.GetRecipe(recipeId);
    }
}

internal sealed class CraftProfessionCandidateSource : ICraftWorkerCandidateSource
{
    private readonly ProfessionAssignments _professions;
    private readonly WorkerSelectionStrategy _workerStrategy;

    public CraftProfessionCandidateSource(
        ProfessionAssignments professions,
        WorkerSelectionStrategy workerStrategy)
    {
        _professions = professions;
        _workerStrategy = workerStrategy;
    }

    public IEnumerable<CreatureInstance> SelectCandidates(
        WorldModel world,
        string jobTag,
        HashSet<Guid> busy,
        ReservationManager reservations,
        ulong currentTick,
        Point3 referencePoint)
    {
        return _professions.SelectCandidates(
            world,
            jobTag,
            _workerStrategy,
            busy,
            reservations,
            currentTick,
            referencePoint);
    }
}
