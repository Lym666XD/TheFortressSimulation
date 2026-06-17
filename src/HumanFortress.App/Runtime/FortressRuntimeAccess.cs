using HumanFortress.App.GameStates;
using HumanFortress.App.Diagnostics;
using HumanFortress.App.Jobs;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Content.Registry;
using HumanFortress.Navigation;
using HumanFortress.Runtime;
using HumanFortress.Simulation.World;
using HumanFortress.WorldGen;

namespace HumanFortress.App.Runtime;

/// <summary>
/// Narrow runtime facade used by FortressState to avoid reaching into GameStateManager directly.
/// </summary>
public sealed class FortressRuntimeAccess
{
    private readonly GameStateManager _stateManager;

    public FortressRuntimeAccess(GameStateManager stateManager)
    {
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
    }

    public World? World => _stateManager.World;
    public NavigationManager? NavManager => _stateManager.NavManager;
    public SimulationStatus SimulationStatus => _stateManager.SimulationStatus;
    public TransportJobSystem? TransportJobs => _stateManager.TransportJobs;
    public MiningJobSystem? MiningJobs => _stateManager.MiningJobs;
    public ConstructionJobSystem? ConstructionJobs => _stateManager.ConstructionJobs;
    public CraftJobSystem? CraftJobs => _stateManager.CraftJobs;
    public ProfessionAssignments? ProfessionAssignments => _stateManager.ProfessionAssignments;
    public UnifiedJobsOrchestrator? JobsOrchestrator => _stateManager.JobsOrchestrator;
    public SchedulerTunings? SchedulerTunings => _stateManager.SchedulerTunings;
    public NavigationTuning? NavigationTuning => _stateManager.NavigationTuning;
    public IRecipeCatalog? Recipes => _stateManager.Recipes;
    public IConstructionCatalog? Constructions => _stateManager.Constructions;
    public IRuntimeGeologyCatalog? Geology => _stateManager.Geology;
    public FortressGenerationContent? GenerationContent => _stateManager.GenerationContent;

    public DiagnosticSnapshot GetDiagnosticSnapshot()
    {
        return _stateManager.GetDiagnosticSnapshot();
    }

    public IReadOnlyList<ProfessionAssignments.ProfessionRosterEntry> GetProfessionRosterSnapshot()
    {
        return _stateManager.GetProfessionRosterSnapshot();
    }

    public void SetProfessionWeight(Guid workerId, string professionId, int weight)
    {
        _stateManager.SetProfessionWeight(workerId, professionId, weight);
    }

    public SimulationJobsDebugData? GetJobsDebugData(ulong tick, bool force = false)
    {
        var debug = _stateManager.GetJobsDebugData(tick, force);
        if (!debug.HasValue) return null;

        var value = debug.Value;
        return new SimulationJobsDebugData(
            value.Tick,
            value.Transport,
            value.Mining,
            value.Craft,
            value.Tunings);
    }

    public void EnqueueCurrentTickCommand(Func<ulong, ICommand> commandFactory)
    {
        _stateManager.EnqueueCurrentTickCommand(commandFactory);
    }

    public SimulationStatus ToggleSimulationPause()
    {
        return _stateManager.ToggleSimulationPause();
    }

    public SimulationStatus CycleSimulationSpeedDown()
    {
        return _stateManager.CycleSimulationSpeedDown();
    }

    public SimulationStatus CycleSimulationSpeedUp()
    {
        return _stateManager.CycleSimulationSpeedUp();
    }
}
