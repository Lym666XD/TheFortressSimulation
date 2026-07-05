using HumanFortress.Core.Commands;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Runtime.Content;
using HumanFortress.Runtime.Diff;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Commands;

internal sealed partial class SimulationCommandExecutionContext
{
    private readonly IRuntimeCommandClockContext _clockContext;
    private readonly ISimulationContext _simulationContext;
    private readonly SimulationRuntimeCommandTargets _commandTargets;

    internal SimulationCommandExecutionContext(
        IRuntimeCommandClockContext clockContext,
        ISimulationContext simulationContext,
        World world,
        RuntimeMutationDiffLogs mutationDiffs,
        IRecipeCatalog recipes,
        FortressRuntimeStockpilePresetCatalog? stockpilePresets = null,
        Action<string>? log = null)
    {
        _clockContext = clockContext ?? throw new ArgumentNullException(nameof(clockContext));
        _simulationContext = simulationContext ?? throw new ArgumentNullException(nameof(simulationContext));
        _commandTargets = new SimulationRuntimeCommandTargets(
            world,
            mutationDiffs,
            recipes,
            stockpilePresets,
            log);
    }

    internal IRuntimeProfessionCommandBindings ProfessionCommandBindings => _commandTargets;
}
