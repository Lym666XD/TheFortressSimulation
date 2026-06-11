using HumanFortress.Jobs.Mining;
using HumanFortress.Navigation;
using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Jobs;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.App.Jobs;

internal sealed class AppMiningJobLogger : IMiningJobLogger
{
    public static readonly AppMiningJobLogger Instance = new();

    private AppMiningJobLogger()
    {
    }

    public void Log(string message)
    {
        Logger.Log(message);
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
