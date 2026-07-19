using HumanFortress.Core.Commands;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Core.Events;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Navigation.Implementation;
using HumanFortress.Runtime.Commands;
using HumanFortress.Runtime.Content;
using HumanFortress.Runtime.Diff;
using HumanFortress.Runtime.Navigation;
using HumanFortress.Runtime.Session;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Host;

/// <summary>
/// Owns the lifecycle and composition hooks for one active simulation session.
/// </summary>
internal sealed partial class SimulationRuntimeHost<TSystems>
    where TSystems : class, IRuntimeTickSystems
{
    private readonly World _world;
    private readonly NavigationManager _navigation;
    private readonly NavigationTuning _navigationTuning;
    private readonly IRecipeCatalog _recipes;
    private readonly IConstructionCatalog _constructions;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _workshopCategoryTags;
    private readonly IRuntimeGeologyCatalog _geology;
    private readonly FortressRuntimeStockpilePresetCatalog _stockpilePresets;
    private readonly RuntimePathServiceRegistry? _pathServices;
    private readonly SimulationCommandExecutionContext _commandContext;
    private readonly SimulationRuntimeHostCore _core;
    private readonly Func<TSystems> _createSystems;
    private readonly Action<IRuntimeProfessionCommandBindings, TSystems>? _afterSystemsRegistered;
    private Action<TSystems, ulong>? _afterPostTickCommit;

    private TSystems? _systems;

    internal SimulationRuntimeHost(
        World world,
        RuntimeSessionServices services,
        NavigationManager navigation,
        Func<TSystems> createSystems,
        Action<IRuntimeProfessionCommandBindings, TSystems>? afterSystemsRegistered = null,
        Action<string>? log = null,
        IRecipeCatalog? recipes = null,
        IConstructionCatalog? constructions = null,
        IRuntimeGeologyCatalog? geology = null,
        NavigationTuning? navigationTuning = null,
        FortressRuntimeStockpilePresetCatalog? stockpilePresets = null,
        RuntimePathServiceRegistry? pathServices = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? workshopCategoryTags = null)
        : this(
            world,
            services.TickScheduler,
            services.CommandQueue,
            services.EventBus,
            services.DiffLog,
            services.MutationDiffs,
            navigation,
            createSystems,
            afterSystemsRegistered,
            log,
            recipes,
            constructions,
            geology,
            navigationTuning,
            stockpilePresets,
            pathServices,
            workshopCategoryTags)
    {
    }

    internal void SetPostTickCommitHandler(Action<TSystems, ulong>? handler)
    {
        if (IsRunning || HasActiveTickThread)
            throw new InvalidOperationException("Cannot replace the PostTick commit handler while the runtime is running.");

        _afterPostTickCommit = handler;
    }

    private SimulationRuntimeHost(
        World world,
        TickScheduler tickScheduler,
        CommandQueue commandQueue,
        IEventBus eventBus,
        DiffLog diffLog,
        RuntimeMutationDiffLogs mutationDiffs,
        NavigationManager navigation,
        Func<TSystems> createSystems,
        Action<IRuntimeProfessionCommandBindings, TSystems>? afterSystemsRegistered = null,
        Action<string>? log = null,
        IRecipeCatalog? recipes = null,
        IConstructionCatalog? constructions = null,
        IRuntimeGeologyCatalog? geology = null,
        NavigationTuning? navigationTuning = null,
        FortressRuntimeStockpilePresetCatalog? stockpilePresets = null,
        RuntimePathServiceRegistry? pathServices = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? workshopCategoryTags = null)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        ArgumentNullException.ThrowIfNull(tickScheduler);
        ArgumentNullException.ThrowIfNull(commandQueue);
        ArgumentNullException.ThrowIfNull(diffLog);
        ArgumentNullException.ThrowIfNull(mutationDiffs);
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _navigationTuning = navigationTuning ?? NavigationTuning.Default;
        _recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
        _constructions = constructions ?? throw new ArgumentNullException(nameof(constructions));
        _geology = geology ?? throw new ArgumentNullException(nameof(geology));
        _stockpilePresets = stockpilePresets ?? FortressRuntimeStockpilePresetCatalog.Empty;
        _workshopCategoryTags = (workshopCategoryTags
                ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal))
            .OrderBy(static category => category.Key, StringComparer.Ordinal)
            .ToDictionary(
                static category => category.Key,
                static category => (IReadOnlyList<string>)Array.AsReadOnly(category.Value.ToArray()),
                StringComparer.Ordinal);
        _pathServices = pathServices;
        _createSystems = createSystems ?? throw new ArgumentNullException(nameof(createSystems));
        _afterSystemsRegistered = afterSystemsRegistered;

        var context = new SimulationRuntimeContext(
            diffLog,
            world,
            eventBus);
        _commandContext = new SimulationCommandExecutionContext(
            context,
            context,
            world,
            mutationDiffs,
            _recipes,
            _stockpilePresets,
            log);
        _core = new SimulationRuntimeHostCore(
            world,
            tickScheduler,
            commandQueue,
            _commandContext,
            _commandContext,
            diffLog,
            mutationDiffs,
            _constructions,
            navigation,
            _geology,
            pathServices);
    }

}
