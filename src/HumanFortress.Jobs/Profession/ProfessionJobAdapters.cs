using HumanFortress.Contracts.Navigation;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Jobs.Craft;
using HumanFortress.Jobs.Configuration;
using HumanFortress.Jobs.Mining;
using HumanFortress.Jobs.Transport;
using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Jobs;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Profession;

internal sealed class TransportProfessionCandidateSource : ITransportWorkerCandidateSource
{
    private readonly ProfessionAssignments _professions;
    private readonly WorkerSelectionStrategy _workerStrategy;

    internal TransportProfessionCandidateSource(
        ProfessionAssignments professions,
        WorkerSelectionStrategy workerStrategy)
    {
        _professions = professions;
        _workerStrategy = workerStrategy;
    }

    IEnumerable<CreatureInstance> ITransportWorkerCandidateSource.SelectCandidates(
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

internal sealed partial class TransportProfessionCompletionSink : ITransportJobCompletionSink
{
    private readonly ProfessionAssignments _professions;
    private readonly string _jobTag;

    internal TransportProfessionCompletionSink(ProfessionAssignments professions, string jobTag)
    {
        _professions = professions;
        _jobTag = jobTag;
    }

    void ITransportJobCompletionSink.RecordJobCompletion(Guid workerId, string jobTag)
    {
        _professions.RecordJobCompletion(workerId, string.IsNullOrWhiteSpace(jobTag) ? _jobTag : jobTag);
    }
}

internal sealed class MiningProfessionCandidateSource : IMiningWorkerCandidateSource
{
    private readonly ProfessionAssignments _professions;
    private readonly WorkerSelectionStrategy _workerStrategy;

    internal MiningProfessionCandidateSource(
        ProfessionAssignments professions,
        WorkerSelectionStrategy workerStrategy)
    {
        _professions = professions;
        _workerStrategy = workerStrategy;
    }

    IEnumerable<CreatureInstance> IMiningWorkerCandidateSource.SelectCandidates(
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

    internal MiningProfessionCompletionSink(ProfessionAssignments professions, string jobTag)
    {
        _professions = professions;
        _jobTag = jobTag;
    }

    void IMiningJobCompletionSink.RecordJobCompletion(Guid workerId, string jobTag)
    {
        _professions.RecordJobCompletion(workerId, string.IsNullOrWhiteSpace(jobTag) ? _jobTag : jobTag);
    }
}

internal sealed class CraftRecipeCatalogAdapter : ICraftRecipeCatalog
{
    private readonly IRecipeCatalog _recipes;

    internal CraftRecipeCatalogAdapter(IRecipeCatalog recipes)
    {
        _recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
    }

    RecipeDefinition? ICraftRecipeCatalog.GetRecipe(string recipeId)
    {
        return _recipes.GetRecipe(recipeId);
    }
}

internal sealed class CraftProfessionCandidateSource : ICraftWorkerCandidateSource
{
    private readonly ProfessionAssignments _professions;
    private readonly WorkerSelectionStrategy _workerStrategy;

    internal CraftProfessionCandidateSource(
        ProfessionAssignments professions,
        WorkerSelectionStrategy workerStrategy)
    {
        _professions = professions;
        _workerStrategy = workerStrategy;
    }

    IEnumerable<CreatureInstance> ICraftWorkerCandidateSource.SelectCandidates(
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
