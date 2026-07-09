using System.Text.RegularExpressions;
using System.Xml.Linq;

internal static class ArchitectureBoundarySmokeTests
{
    private static readonly string[] ForbiddenAppSourcePatterns =
    {
        "using HumanFortress.Simulation",
        "using HumanFortress.Jobs",
        "using HumanFortress.Navigation",
        "using HumanFortress.WorldGen",
        "using HumanFortress.Core",
        "using HumanFortress.Content",
        "using HumanFortress.Runtime.Commands",
        "using HumanFortress.Runtime.Replay",
        "using HumanFortress.Runtime.Save",
        "IFortressRuntimePlacementAccess",
        "IFortressRuntimeDebugSpawnAccess",
        "IFortressRuntimeWorkshopPanelAccess",
        "IFortressRuntimeSessionAccess",
        "IFortressRuntimeSaveAccess"
    };

    private static readonly Regex AppRuntimeUsingPattern = new(
        @"^\s*using\s+HumanFortress\.App\.Runtime\s*;",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex RuntimeUsingPattern = new(
        @"^\s*using\s+HumanFortress\.Runtime\s*;",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex WorldGenImplementationUsingPattern = new(
        @"^\s*using\s+HumanFortress\.WorldGen\.Implementation\s*;",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex HumanFortressUsingPattern = new(
        @"^\s*using\s+(?:static\s+)?(?:[A-Za-z_][A-Za-z0-9_]*\s*=\s*)?(HumanFortress(?:\.[A-Za-z_][A-Za-z0-9_]*)*)\s*;",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex PublicTypePattern = new(
        @"^\s*public\s+(?:sealed\s+|static\s+|readonly\s+|partial\s+)*(class|interface|record\s+struct|record|enum|struct)\s+([A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex InternalsVisibleToAttributePattern = new(
        @"InternalsVisibleTo\s*\(\s*""([^""]+)""\s*\)",
        RegexOptions.Compiled);

    private static readonly Regex InternalsVisibleToItemPattern = new(
        @"<InternalsVisibleTo\s+Include\s*=\s*""([^""]+)""\s*/?>",
        RegexOptions.Compiled);

    private const string RootNavigationImplementationNamespace = "namespace HumanFortress.Navigation;";
    private const string RootJobsNamespace = "namespace HumanFortress.Jobs;";
    private const string RootJobsUsingDirective = "using HumanFortress.Jobs;";
    private const string RootRuntimeNamespace = "namespace HumanFortress.Runtime;";
    private const string RootWorldGenImplementationNamespace = "namespace HumanFortress.WorldGen;";

    private static readonly Regex RootWorldGenBlockNamespacePattern = new(
        @"^\s*namespace\s+HumanFortress\.WorldGen\s*\{",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly string[] AllowedAppRuntimeUsingSuffixes =
    {
        "GameStates/GameStateRuntimeCoordinator.cs",
        "Input/FortressInputRuntimePortDependencies.cs",
        "Input/FortressKeyboardRuntimePorts.cs",
        "Input/FortressMapRuntimePorts.cs",
        "Input/FortressPlacementRuntimePorts.cs",
        "Rendering/FortressViewRuntimePorts.cs",
        "Rendering/FortressViewReadRuntimePorts.cs",
        "Rendering/FortressViewUiInputRuntimePorts.cs",
        "Session/FortressSessionRuntimePorts.cs"
    };

    private static readonly string[] AllowedRuntimeUsingSuffixes =
    {
        "Content/AppContentFileLocator.cs",
        "GameStates/GameStateRuntimeCoordinator.cs",
        "Runtime/FortressRuntimeAccess.cs",
        "Startup/FortressRuntimeLoggingBridge.cs",
        "Startup/StartupContentGate.cs",
        "WorldGeneration/WorldGenerationServiceProvider.cs"
    };

    private static readonly string[] AllowedRuntimeWorldGenImplementationUsingSuffixes =
    {
        "FortressRuntimeWorldGenerationFactory.cs",
        "RuntimeFortressGenerationRunner.cs"
    };

    private static readonly string[] PresentationPrimitiveTokens =
    {
        "SadRogue",
        "SadConsole",
        "MonoGame",
        "Microsoft.Xna"
    };

    private static readonly IReadOnlyDictionary<string, string> RuntimeFocusedHelperNamespaces = new Dictionary<string, string>
    {
        ["Navigation/SimulationNavigationSource.cs"] = "namespace HumanFortress.Runtime.Navigation;",
        ["Navigation/SimulationNavigationSource.Mapping.cs"] = "namespace HumanFortress.Runtime.Navigation;",
        ["Navigation/SimulationNavigationSource.ConstructionSites.cs"] = "namespace HumanFortress.Runtime.Navigation;",
        ["Navigation/SimulationNavigationFactory.cs"] = "namespace HumanFortress.Runtime.Navigation;",
        ["Navigation/RuntimePathServiceRegistry.cs"] = "namespace HumanFortress.Runtime.Navigation;",
        ["Content/SimulationWorldContentLoader.cs"] = "namespace HumanFortress.Runtime.Content;",
        ["Content/SimulationWorldContentLoader.Logging.cs"] = "namespace HumanFortress.Runtime.Content;",
        ["Content/RuntimeCraftRecipeCatalogAdapter.cs"] = "namespace HumanFortress.Runtime.Content;",
        ["Content/FortressRuntimeStockpilePresetCatalog.cs"] = "namespace HumanFortress.Runtime.Content;",
        ["Diff/RuntimeMutationDiffLogs.cs"] = "namespace HumanFortress.Runtime.Diff;",
        ["Diff/ProfessionAssignmentDiffLog.cs"] = "namespace HumanFortress.Runtime.Diff;",
        ["Session/FortressRuntimeSession.cs"] = "namespace HumanFortress.Runtime.Session;",
        ["Session/RuntimeSessionServices.cs"] = "namespace HumanFortress.Runtime.Session;",
        ["Session/SimulationRuntimeSession.cs"] = "namespace HumanFortress.Runtime.Session;",
        ["Session/SimulationRuntimeSessionFactory.cs"] = "namespace HumanFortress.Runtime.Session;",
        ["Session/SimulationRuntimeSessionNavigation.cs"] = "namespace HumanFortress.Runtime.Session;",
        ["Host/IRuntimeTickSystems.cs"] = "namespace HumanFortress.Runtime.Host;",
        ["Host/SimulationRuntimeContext.cs"] = "namespace HumanFortress.Runtime.Host;",
        ["Host/SimulationRuntimeHost.cs"] = "namespace HumanFortress.Runtime.Host;",
        ["Host/SimulationRuntimeHost.Accessors.cs"] = "namespace HumanFortress.Runtime.Host;",
        ["Host/SimulationRuntimeHost.Lifecycle.cs"] = "namespace HumanFortress.Runtime.Host;",
        ["Host/SimulationRuntimeHostCore.cs"] = "namespace HumanFortress.Runtime.Host;",
        ["Host/SimulationRuntimeHostCore.Lifecycle.cs"] = "namespace HumanFortress.Runtime.Host;",
        ["Host/SimulationTickPipeline.cs"] = "namespace HumanFortress.Runtime.Host;",
        ["Host/SimulationTickPipeline.PostTick.cs"] = "namespace HumanFortress.Runtime.Host;",
        ["Geometry/RuntimeGeometryMapper.cs"] = "namespace HumanFortress.Runtime.Geometry;",
        ["WorldGeneration/RuntimeFortressGenerationRunner.cs"] = "namespace HumanFortress.Runtime.WorldGeneration;",
        ["Startup/FortressRuntimeStartup.cs"] = "namespace HumanFortress.Runtime.Startup;",
        ["Startup/RuntimeAutoDigSeeder.cs"] = "namespace HumanFortress.Runtime.Startup;",
        ["Startup/RuntimeAutoDigSeeder.Commands.cs"] = "namespace HumanFortress.Runtime.Startup;",
        ["Startup/StartupDigTargetFinder.cs"] = "namespace HumanFortress.Runtime.Startup;",
        ["Startup/StartupDigTargetFinder.Eligibility.cs"] = "namespace HumanFortress.Runtime.Startup;",
        ["Startup/StartupDigTargetFinder.Fallback.cs"] = "namespace HumanFortress.Runtime.Startup;",
        ["Startup/StartupDigTargetFinder.Nearest.cs"] = "namespace HumanFortress.Runtime.Startup;",
        ["Startup/SimulationInitialWorkerSpawner.cs"] = "namespace HumanFortress.Runtime.Startup;",
        ["Composition/FortressRuntimeCatalogs.cs"] = "namespace HumanFortress.Runtime.Composition;",
        ["Composition/FortressRuntimeTunings.cs"] = "namespace HumanFortress.Runtime.Composition;",
        ["Composition/FortressRuntimeWorkforce.cs"] = "namespace HumanFortress.Runtime.Composition;",
        ["Composition/FortressRuntimeDependencies.cs"] = "namespace HumanFortress.Runtime.Composition;",
        ["Composition/FortressRuntimePlanningSystems.cs"] = "namespace HumanFortress.Runtime.Composition;",
        ["Composition/FortressRuntimeSystemsFactory.cs"] = "namespace HumanFortress.Runtime.Composition;",
        ["Composition/SimulationRuntimeSystems.cs"] = "namespace HumanFortress.Runtime.Composition;",
        ["Composition/FortressRuntimeJobSystems.cs"] = "namespace HumanFortress.Runtime.Composition;",
        ["Composition/FortressRuntimeJobSystems.MiningTransport.cs"] = "namespace HumanFortress.Runtime.Composition;",
        ["Composition/FortressRuntimeJobSystems.ConstructionCraft.cs"] = "namespace HumanFortress.Runtime.Composition;",
        ["Composition/FortressRuntimeHostFactory.cs"] = "namespace HumanFortress.Runtime.Composition;",
        ["Composition/FortressRuntimeLogging.cs"] = "namespace HumanFortress.Runtime.Composition;",
        ["Composition/FortressRuntimeLogBindings.cs"] = "namespace HumanFortress.Runtime.Composition;",
        ["Composition/FortressRuntimeWorkshopCompletionNotifier.cs"] = "namespace HumanFortress.Runtime.Composition;",
        ["Save/RuntimeSaveManifestSections.cs"] = "namespace HumanFortress.Runtime.Save;",
        ["Commands/RuntimeCommandReplayFactory.cs"] = "namespace HumanFortress.Runtime.Commands;",
        ["Commands/RuntimeCommandReplayFactory.Debug.cs"] = "namespace HumanFortress.Runtime.Commands;",
        ["Commands/RuntimeCommandReplayFactory.Orders.cs"] = "namespace HumanFortress.Runtime.Commands;",
        ["Commands/RuntimeCommandReplayFactory.ProfessionWorkshop.cs"] = "namespace HumanFortress.Runtime.Commands;",
        ["Commands/RuntimeCommandReplayFactory.ZonesStockpiles.cs"] = "namespace HumanFortress.Runtime.Commands;",
        ["Commands/Execution/IRuntimeCommandClockContext.cs"] = "namespace HumanFortress.Runtime.Commands;",
        ["Commands/Execution/SimulationCommandExecutionContext.cs"] = "namespace HumanFortress.Runtime.Commands;",
        ["Commands/Execution/SimulationCommandExecutionContext.Clock.cs"] = "namespace HumanFortress.Runtime.Commands;",
        ["Commands/Execution/SimulationCommandExecutionContext.Simulation.cs"] = "namespace HumanFortress.Runtime.Commands;",
        ["Commands/Execution/SimulationCommandExecutionContext.TargetRoles.cs"] = "namespace HumanFortress.Runtime.Commands;",
        ["Commands/Execution/SimulationCommandStage.cs"] = "namespace HumanFortress.Runtime.Commands;",
        ["Commands/Targets/ICreatureSpawnCommandTarget.cs"] = "namespace HumanFortress.Runtime.Commands;",
        ["Commands/Targets/IItemSpawnCommandTarget.cs"] = "namespace HumanFortress.Runtime.Commands;",
        ["Commands/Targets/IOrderCommandTarget.cs"] = "namespace HumanFortress.Runtime.Commands;",
        ["Commands/Targets/IProfessionAssignmentCommandTarget.cs"] = "namespace HumanFortress.Runtime.Commands;",
        ["Commands/Targets/IRuntimeCommandTargetRoleContexts.cs"] = "namespace HumanFortress.Runtime.Commands;",
        ["Commands/Targets/IRuntimeProfessionCommandBindings.cs"] = "namespace HumanFortress.Runtime.Commands;",
        ["Commands/Targets/IStockpileCommandTarget.cs"] = "namespace HumanFortress.Runtime.Commands;",
        ["Commands/Targets/IWorkshopQueueCommandTarget.cs"] = "namespace HumanFortress.Runtime.Commands;",
        ["Commands/Targets/IZoneCommandTarget.cs"] = "namespace HumanFortress.Runtime.Commands;",
        ["Commands/Targets/CreatureSpawnCommandTarget.cs"] = "namespace HumanFortress.Runtime.Commands;",
        ["Commands/Targets/ItemSpawnCommandTarget.cs"] = "namespace HumanFortress.Runtime.Commands;",
        ["Commands/Targets/OrderCommandTarget.cs"] = "namespace HumanFortress.Runtime.Commands;",
        ["Commands/Targets/ProfessionAssignmentCommandTarget.cs"] = "namespace HumanFortress.Runtime.Commands;",
        ["Commands/Targets/SimulationRuntimeCommandTargets.cs"] = "namespace HumanFortress.Runtime.Commands;",
        ["Commands/Targets/StockpileCommandTarget.cs"] = "namespace HumanFortress.Runtime.Commands;",
        ["Commands/Targets/StockpileCommandTarget.Cells.cs"] = "namespace HumanFortress.Runtime.Commands;",
        ["Commands/Targets/StockpileCommandTarget.Naming.cs"] = "namespace HumanFortress.Runtime.Commands;",
        ["Commands/Targets/WorkshopQueueCommandTarget.cs"] = "namespace HumanFortress.Runtime.Commands;",
        ["Commands/Targets/ZoneCommandTarget.cs"] = "namespace HumanFortress.Runtime.Commands;"
    };

    private static readonly IReadOnlyDictionary<string, string> ContentFocusedHelperNamespaces = new Dictionary<string, string>
    {
        ["Registry/ContentRegistry.cs"] = "namespace HumanFortress.Content.Registry;",
        ["Registry/ContentRegistry.BiomesGeology.cs"] = "namespace HumanFortress.Content.Registry;",
        ["Registry/ContentRegistry.MaterialsTerrain.cs"] = "namespace HumanFortress.Content.Registry;",
        ["Registry/ContentRegistry.TuningZonesValidation.cs"] = "namespace HumanFortress.Content.Registry;"
    };

    private static readonly IReadOnlyDictionary<string, string> ContentDefinitionFocusedHelperNamespaces = new Dictionary<string, string>
    {
        ["Definitions/CoreDataRegistryLoader.cs"] = "namespace HumanFortress.Content.Definitions;",
        ["Definitions/CoreDataRegistryLoader.Common.cs"] = "namespace HumanFortress.Content.Definitions;",
        ["Definitions/CoreDataRegistryLoader.Constructions.cs"] = "namespace HumanFortress.Content.Definitions;",
        ["Definitions/CoreDataRegistryLoader.Recipes.cs"] = "namespace HumanFortress.Content.Definitions;"
    };

    private static readonly IReadOnlyDictionary<string, string> ContentItemDefinitionFocusedHelperNamespaces = new Dictionary<string, string>
    {
        ["Definitions/ItemDefinitionCatalogLoader.cs"] = "namespace HumanFortress.Content.Definitions;",
        ["Definitions/ItemDefinitionCatalogLoader.Furniture.cs"] = "namespace HumanFortress.Content.Definitions;",
        ["Definitions/ItemDefinitionCatalogLoader.Validation.cs"] = "namespace HumanFortress.Content.Definitions;"
    };

    private static readonly IReadOnlyDictionary<string, string> ContentCreatureDefinitionFocusedHelperNamespaces = new Dictionary<string, string>
    {
        ["Definitions/CreatureDefinitionCatalogLoader.cs"] = "namespace HumanFortress.Content.Definitions;",
        ["Definitions/CreatureDefinitionCatalogLoader.Validation.cs"] = "namespace HumanFortress.Content.Definitions;"
    };

    private static readonly IReadOnlyDictionary<string, string> WorldGenFocusedHelperNamespaces = new Dictionary<string, string>
    {
        ["FortressGenerator.cs"] = "namespace HumanFortress.WorldGen.Implementation",
        ["FortressGenerator.Caverns.cs"] = "namespace HumanFortress.WorldGen.Implementation",
        ["FortressGenerator.Ores.cs"] = "namespace HumanFortress.WorldGen.Implementation",
        ["FortressGenerator.Strata.cs"] = "namespace HumanFortress.WorldGen.Implementation",
        ["FortressGenerator.TuningJson.cs"] = "namespace HumanFortress.WorldGen.Implementation"
    };

    private static readonly IReadOnlyDictionary<string, string> SimulationSaveFocusedHelperNamespaces = new Dictionary<string, string>
    {
        ["Save/WorldSavePayloadBuilder.cs"] = "namespace HumanFortress.Simulation.Save;",
        ["Save/WorldSavePayloadBuilder.Common.cs"] = "namespace HumanFortress.Simulation.Save;",
        ["Save/WorldSavePayloadBuilder.Entities.cs"] = "namespace HumanFortress.Simulation.Save;",
        ["Save/WorldSavePayloadBuilder.MetadataTerrain.cs"] = "namespace HumanFortress.Simulation.Save;",
        ["Save/WorldSavePayloadBuilder.Orders.cs"] = "namespace HumanFortress.Simulation.Save;",
        ["Save/WorldSavePayloadBuilder.Placeables.cs"] = "namespace HumanFortress.Simulation.Save;",
        ["Save/WorldSavePayloadBuilder.Stockpiles.cs"] = "namespace HumanFortress.Simulation.Save;",
        ["Save/WorldSavePayloadRestorer.cs"] = "namespace HumanFortress.Simulation.Save;",
        ["Save/WorldSavePayloadRestorer.Conversion.cs"] = "namespace HumanFortress.Simulation.Save;",
        ["Save/WorldSavePayloadRestorer.Placeables.cs"] = "namespace HumanFortress.Simulation.Save;",
        ["Save/WorldSavePayloadRestorer.Validation.cs"] = "namespace HumanFortress.Simulation.Save;"
    };

    private static readonly IReadOnlyDictionary<string, string> SimulationItemsFocusedHelperNamespaces = new Dictionary<string, string>
    {
        ["Items/ItemManager.cs"] = "namespace HumanFortress.Simulation.Items;",
        ["Items/ItemManager.Catalog.cs"] = "namespace HumanFortress.Simulation.Items;",
        ["Items/ItemManager.Indexing.cs"] = "namespace HumanFortress.Simulation.Items;",
        ["Items/ItemManager.Mutations.cs"] = "namespace HumanFortress.Simulation.Items;",
        ["Items/ItemManager.Queries.cs"] = "namespace HumanFortress.Simulation.Items;",
        ["Items/ItemManager.SaveRestore.cs"] = "namespace HumanFortress.Simulation.Items;",
        ["Items/ItemManager.Spawning.cs"] = "namespace HumanFortress.Simulation.Items;"
    };

    private static readonly IReadOnlyDictionary<string, string> SimulationCreaturesFocusedHelperNamespaces = new Dictionary<string, string>
    {
        ["Creatures/CreatureManager.cs"] = "namespace HumanFortress.Simulation.Creatures;",
        ["Creatures/CreatureManager.Catalog.cs"] = "namespace HumanFortress.Simulation.Creatures;",
        ["Creatures/CreatureManager.Indexing.cs"] = "namespace HumanFortress.Simulation.Creatures;",
        ["Creatures/CreatureManager.Queries.cs"] = "namespace HumanFortress.Simulation.Creatures;",
        ["Creatures/CreatureManager.SaveRestore.cs"] = "namespace HumanFortress.Simulation.Creatures;",
        ["Creatures/CreatureManager.Spawning.cs"] = "namespace HumanFortress.Simulation.Creatures;"
    };

    private static readonly IReadOnlyDictionary<string, string> SimulationPlaceablesFocusedHelperNamespaces = new Dictionary<string, string>
    {
        ["Placeables/ChunkPlaceableData.cs"] = "namespace HumanFortress.Simulation.Placeables;",
        ["Placeables/ChunkPlaceableData.FurnitureSync.cs"] = "namespace HumanFortress.Simulation.Placeables;",
        ["Placeables/PlaceableInstance.cs"] = "namespace HumanFortress.Simulation.Placeables;",
        ["Placeables/PlaceableInstance.ConstructionFactory.cs"] = "namespace HumanFortress.Simulation.Placeables;",
        ["Placeables/PlaceableInstance.ItemFactory.cs"] = "namespace HumanFortress.Simulation.Placeables;",
        ["Placeables/PlaceableKind.cs"] = "namespace HumanFortress.Simulation.Placeables;",
        ["Placeables/DoorState.cs"] = "namespace HumanFortress.Simulation.Placeables;",
        ["Placeables/ConstructionSiteState.cs"] = "namespace HumanFortress.Simulation.Placeables;",
        ["Placeables/PlaceableManager.cs"] = "namespace HumanFortress.Simulation.Placeables;",
        ["Placeables/PlaceableManager.AffectedChunks.cs"] = "namespace HumanFortress.Simulation.Placeables;",
        ["Placeables/PlaceableManager.Collision.cs"] = "namespace HumanFortress.Simulation.Placeables;",
        ["Placeables/PlaceableManager.Placement.cs"] = "namespace HumanFortress.Simulation.Placeables;",
        ["Placeables/PlaceableManager.Removal.cs"] = "namespace HumanFortress.Simulation.Placeables;",
        ["Placeables/CollisionResult.cs"] = "namespace HumanFortress.Simulation.Placeables;"
    };

    private static readonly IReadOnlyDictionary<string, string> SimulationOrdersFocusedHelperNamespaces = new Dictionary<string, string>
    {
        ["Orders/OrdersManager.cs"] = "namespace HumanFortress.Simulation.Orders;",
        ["Orders/OrdersManager.Construction.cs"] = "namespace HumanFortress.Simulation.Orders;",
        ["Orders/OrdersManager.Haul.cs"] = "namespace HumanFortress.Simulation.Orders;",
        ["Orders/OrdersManager.Mining.cs"] = "namespace HumanFortress.Simulation.Orders;",
        ["Orders/OrdersManager.SaveRestore.cs"] = "namespace HumanFortress.Simulation.Orders;"
    };

    private static readonly IReadOnlySet<string> AllowedRuntimePublicTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "FortressRuntimeContentLoader",
        "FortressRuntimeLoggingBootstrap",
        "FortressRuntimeSessionFactory",
        "FortressRuntimeWorldGenerationFactory",
        "IFortressRuntimeAppSessionPorts",
        "IFortressRuntimeSessionBootstrapPort",
        "IFortressRuntimeSessionDebugCommandPort",
        "IFortressRuntimeSessionLifecyclePort",
        "IFortressRuntimeSessionPlacementCommandPort",
        "IFortressRuntimeSessionProfessionCommandPort",
        "IFortressRuntimeSessionReadPort",
        "IFortressRuntimeSessionSimulationControlPort",
        "IFortressRuntimeSessionSnapshotPort",
        "IFortressRuntimeSessionWorkshopCommandPort"
    };

    private static readonly IReadOnlySet<string> AllowedCorePublicTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "BiomeType",
        "CommandQueue",
        "CommandQueueReplaySnapshot",
        "CommandReplayJournalHashBuilder",
        "CommandReplayRecord",
        "DeterministicGuidGenerator",
        "DeterministicRng",
        "DiffLog",
        "DiffOp",
        "DiffOpType",
        "DiffTarget",
        "DiffTargetEncoding",
        "DifficultyPreset",
        "EventBus",
        "FortressParams",
        "ICommand",
        "ICommandReplayFactory",
        "ICommandReplayIdentity",
        "IEventBus",
        "IGameEvent",
        "ISimulationContext",
        "ITick",
        "IWorldReader",
        "Priority",
        "ReplayHashBuilder",
        "RngReplayHashBuilder",
        "RngState",
        "RngStreamManager",
        "RngStreamStateSnapshot",
        "StreamNames",
        "SystemId",
        "TickScheduler",
        "UpdateOrder",
        "WorldParams",
        "WorldTile"
    };

    private static readonly IReadOnlyDictionary<string, string[]> AllowedProjectReferences = new Dictionary<string, string[]>
    {
        ["HumanFortress.Contracts.csproj"] = Array.Empty<string>(),
        ["HumanFortress.Core.csproj"] = new[] { "HumanFortress.Contracts.csproj" },
        ["HumanFortress.Content.csproj"] = new[] { "HumanFortress.Contracts.csproj" },
        ["HumanFortress.Navigation.csproj"] = new[] { "HumanFortress.Contracts.csproj" },
        ["HumanFortress.Simulation.csproj"] = new[] { "HumanFortress.Contracts.csproj", "HumanFortress.Core.csproj" },
        ["HumanFortress.Jobs.csproj"] = new[] { "HumanFortress.Contracts.csproj", "HumanFortress.Core.csproj", "HumanFortress.Simulation.csproj" },
        ["HumanFortress.WorldGen.csproj"] = new[] { "HumanFortress.Contracts.csproj", "HumanFortress.Core.csproj", "HumanFortress.Simulation.csproj" },
        ["HumanFortress.Runtime.csproj"] = new[]
        {
            "HumanFortress.Contracts.csproj",
            "HumanFortress.Content.csproj",
            "HumanFortress.Core.csproj",
            "HumanFortress.Jobs.csproj",
            "HumanFortress.Navigation.csproj",
            "HumanFortress.Simulation.csproj",
            "HumanFortress.WorldGen.csproj"
        },
        ["HumanFortress.App.csproj"] = new[]
        {
            "HumanFortress.Contracts.csproj",
            "HumanFortress.Runtime.csproj"
        }
    };

    private static readonly IReadOnlyDictionary<string, string[]> AllowedProjectSourceImports = new Dictionary<string, string[]>
    {
        ["HumanFortress.Contracts"] = new[] { "HumanFortress.Contracts" },
        ["HumanFortress.Core"] = new[] { "HumanFortress.Contracts", "HumanFortress.Core" },
        ["HumanFortress.Content"] = new[] { "HumanFortress.Contracts", "HumanFortress.Content" },
        ["HumanFortress.Navigation"] = new[] { "HumanFortress.Contracts", "HumanFortress.Navigation" },
        ["HumanFortress.Simulation"] = new[] { "HumanFortress.Contracts", "HumanFortress.Core", "HumanFortress.Simulation" },
        ["HumanFortress.Jobs"] = new[] { "HumanFortress.Contracts", "HumanFortress.Core", "HumanFortress.Simulation", "HumanFortress.Jobs" },
        ["HumanFortress.WorldGen"] = new[] { "HumanFortress.Contracts", "HumanFortress.Core", "HumanFortress.Simulation", "HumanFortress.WorldGen" },
        ["HumanFortress.Runtime"] = new[]
        {
            "HumanFortress.Contracts",
            "HumanFortress.Content",
            "HumanFortress.Core",
            "HumanFortress.Jobs",
            "HumanFortress.Navigation",
            "HumanFortress.Simulation",
            "HumanFortress.WorldGen",
            "HumanFortress.Runtime"
        },
        ["HumanFortress.App"] = new[] { "HumanFortress.Contracts", "HumanFortress.Runtime", "HumanFortress.App" }
    };

    private static readonly IReadOnlyDictionary<string, string[]> AllowedFriendAssemblies = new Dictionary<string, string[]>
    {
        ["HumanFortress.Contracts"] = Array.Empty<string>(),
        ["HumanFortress.Core"] = new[] { "HumanFortress.Core.Tests" },
        ["HumanFortress.Content"] = new[] { "HumanFortress.App.Tests", "HumanFortress.Runtime" },
        ["HumanFortress.Navigation"] = new[] { "HumanFortress.App.Tests", "HumanFortress.Runtime" },
        ["HumanFortress.Simulation"] = new[] { "HumanFortress.App.Tests", "HumanFortress.Jobs", "HumanFortress.Runtime", "HumanFortress.WorldGen" },
        ["HumanFortress.Jobs"] = new[] { "HumanFortress.App.Tests", "HumanFortress.Runtime" },
        ["HumanFortress.WorldGen"] = new[] { "HumanFortress.App.Tests", "HumanFortress.Runtime" },
        ["HumanFortress.Runtime"] = new[] { "HumanFortress.App.Tests" },
        ["HumanFortress.App"] = new[] { "HumanFortress.App.Tests" }
    };

    public static void RunAll()
    {
        Console.WriteLine("=== Architecture Boundary Smoke Tests ===");

        string root = TestRepositoryPaths.FindRepositoryRoot();
        TestAppSourceDoesNotReferenceForbiddenRuntimeModules(root);
        TestAppRuntimeNamespaceUseIsAdapterOnly(root);
        TestRuntimeNamespaceUseIsBoundaryOnly(root);
        TestAppProjectDoesNotReferenceLowerImplementationProjects(root);
        TestContentLoaderImplementationStaysInternal(root);
        TestNavigationImplementationUsesExplicitNamespace(root);
        TestWorldGenImplementationUsesExplicitNamespace(root);
        TestWorldGenFocusedHelpersUseImplementationNamespace(root);
        TestWorldGenImplementationImportsAreRuntimeCompositionOnly(root);
        TestJobsImplementationUsesDirectoryNamespaces(root);
        TestContentFocusedHelpersUseDirectoryNamespaces(root);
        TestContentDefinitionFocusedHelpersUseDirectoryNamespaces(root);
        TestContentItemDefinitionFocusedHelpersUseDirectoryNamespaces(root);
        TestContentCreatureDefinitionFocusedHelpersUseDirectoryNamespaces(root);
        TestSimulationSaveFocusedHelpersUseDirectoryNamespaces(root);
        TestSimulationItemsFocusedHelpersUseDirectoryNamespaces(root);
        TestSimulationCreaturesFocusedHelpersUseDirectoryNamespaces(root);
        TestSimulationPlaceablesFocusedHelpersUseDirectoryNamespaces(root);
        TestSimulationOrdersFocusedHelpersUseDirectoryNamespaces(root);
        TestRuntimeFocusedHelpersUseDirectoryNamespaces(root);
        TestRuntimeSaveCodecStaysInternal(root);
        TestRuntimeFullSessionPortsStayInternal(root);
        TestImplementationProjectsDoNotExposePublicTypes(root);
        TestCorePublicSurfaceIsApproved(root);
        TestRuntimePublicSurfaceIsApproved(root);
        TestContractsAndRuntimePublicPortsAvoidPresentationPrimitives(root);
        TestAppPublicSurfaceIsProgramOnly(root);
        TestProductionProjectReferenceGraph(root);
        TestContractsProjectHasNoExternalReferences(root);
        TestProjectSourceImportsFollowModuleBoundaries(root);
        TestFriendAssemblyGraphIsApproved(root);

        Console.WriteLine("=== Architecture Boundary Smoke Tests Completed ===\n");
    }

    private static void TestAppSourceDoesNotReferenceForbiddenRuntimeModules(string root)
    {
        var violations = new List<string>();
        foreach (var file in TestRepositoryPaths.EnumerateSourceFiles(Path.Combine(root, "src", "HumanFortress.App")))
        {
            string text = File.ReadAllText(file);
            foreach (var pattern in ForbiddenAppSourcePatterns)
            {
                if (text.Contains(pattern, StringComparison.Ordinal))
                    violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} contains {pattern}");
            }
        }

        RegressionAssert.True(violations.Count == 0, "App source boundary violations:\n" + string.Join('\n', violations));
        Console.WriteLine("[PASS] App source avoids forbidden implementation module references");
    }

    private static void TestAppRuntimeNamespaceUseIsAdapterOnly(string root)
    {
        var violations = new List<string>();
        foreach (var file in TestRepositoryPaths.EnumerateSourceFiles(Path.Combine(root, "src", "HumanFortress.App")))
        {
            string relative = TestRepositoryPaths.RelativePath(root, file);
            if (!AppRuntimeUsingPattern.IsMatch(File.ReadAllText(file)))
                continue;

            if (!AllowedAppRuntimeUsingSuffixes.Any(suffix => relative.EndsWith(suffix, StringComparison.Ordinal)))
                violations.Add(relative);
        }

        RegressionAssert.True(violations.Count == 0, "Unexpected App.Runtime using directives:\n" + string.Join('\n', violations));
        Console.WriteLine("[PASS] App.Runtime imports are limited to adapter/port composition files");
    }

    private static void TestRuntimeNamespaceUseIsBoundaryOnly(string root)
    {
        var violations = new List<string>();
        foreach (var file in TestRepositoryPaths.EnumerateSourceFiles(Path.Combine(root, "src", "HumanFortress.App")))
        {
            string relative = TestRepositoryPaths.RelativePath(root, file);
            if (!RuntimeUsingPattern.IsMatch(File.ReadAllText(file)))
                continue;

            if (!AllowedRuntimeUsingSuffixes.Any(suffix => relative.EndsWith(suffix, StringComparison.Ordinal)))
                violations.Add(relative);
        }

        RegressionAssert.True(violations.Count == 0, "Unexpected Runtime using directives in App:\n" + string.Join('\n', violations));
        Console.WriteLine("[PASS] Runtime imports in App are limited to startup/adapter/content-location boundaries");
    }

    private static void TestAppProjectDoesNotReferenceLowerImplementationProjects(string root)
    {
        string projectPath = Path.Combine(root, "src", "HumanFortress.App", "HumanFortress.App.csproj");
        string text = File.ReadAllText(projectPath);
        var forbiddenReferences = new[]
        {
            "HumanFortress.Content.csproj",
            "HumanFortress.Core.csproj",
            "HumanFortress.Simulation.csproj",
            "HumanFortress.Jobs.csproj",
            "HumanFortress.Navigation.csproj",
            "HumanFortress.WorldGen.csproj"
        };

        var violations = forbiddenReferences
            .Where(reference => text.Contains(reference, StringComparison.Ordinal))
            .ToArray();

        RegressionAssert.True(violations.Length == 0, "App project references lower implementation projects: " + string.Join(", ", violations));
        Console.WriteLine("[PASS] App project references stay above lower implementation modules");
    }

    private static void TestContentLoaderImplementationStaysInternal(string root)
    {
        string contentLoaderPath = Path.Combine(root, "src", "HumanFortress.Content", "Loading", "FortressContentLoader.cs");
        string text = File.ReadAllText(contentLoaderPath);

        RegressionAssert.True(
            !text.Contains("public static class FortressContentLoader", StringComparison.Ordinal)
            && !text.Contains("public sealed class FortressContentLoadResult", StringComparison.Ordinal),
            "Content loader/package implementation should remain internal; App-facing content load data belongs in Contracts and Runtime facades.");
        Console.WriteLine("[PASS] Content loader implementation stays internal/friend-only");
    }

    private static void TestNavigationImplementationUsesExplicitNamespace(string root)
    {
        string navigationRoot = Path.Combine(root, "src", "HumanFortress.Navigation");
        var violations = TestRepositoryPaths
            .EnumerateSourceFiles(navigationRoot)
            .Where(file => File.ReadAllText(file).Contains(RootNavigationImplementationNamespace, StringComparison.Ordinal))
            .Select(file => TestRepositoryPaths.RelativePath(root, file))
            .Order(StringComparer.Ordinal)
            .ToArray();

        RegressionAssert.True(
            violations.Length == 0,
            "Concrete Navigation implementation should use HumanFortress.Navigation.Implementation, not the compatibility root namespace:\n"
            + string.Join('\n', violations));
        Console.WriteLine("[PASS] Concrete Navigation implementation namespace is explicit");
    }

    private static void TestWorldGenImplementationUsesExplicitNamespace(string root)
    {
        string worldGenRoot = Path.Combine(root, "src", "HumanFortress.WorldGen");
        var violations = TestRepositoryPaths
            .EnumerateSourceFiles(worldGenRoot)
            .Where(file =>
            {
                string text = File.ReadAllText(file);
                return text.Contains(RootWorldGenImplementationNamespace, StringComparison.Ordinal)
                    || RootWorldGenBlockNamespacePattern.IsMatch(text);
            })
            .Select(file => TestRepositoryPaths.RelativePath(root, file))
            .Order(StringComparer.Ordinal)
            .ToArray();

        RegressionAssert.True(
            violations.Length == 0,
            "Concrete WorldGen implementation should use HumanFortress.WorldGen.Implementation, not the compatibility root namespace:\n"
            + string.Join('\n', violations));
        Console.WriteLine("[PASS] Concrete WorldGen implementation namespace is explicit");
    }

    private static void TestWorldGenImplementationImportsAreRuntimeCompositionOnly(string root)
    {
        string runtimeRoot = Path.Combine(root, "src", "HumanFortress.Runtime");
        var violations = TestRepositoryPaths
            .EnumerateSourceFiles(runtimeRoot)
            .Where(file => WorldGenImplementationUsingPattern.IsMatch(File.ReadAllText(file)))
            .Select(file => TestRepositoryPaths.RelativePath(runtimeRoot, file))
            .Where(relative => !AllowedRuntimeWorldGenImplementationUsingSuffixes.Any(
                suffix => relative.EndsWith(suffix, StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)
            .ToArray();

        RegressionAssert.True(
            violations.Length == 0,
            "Runtime should import concrete WorldGen implementation only at composition boundaries:\n"
            + string.Join('\n', violations));
        Console.WriteLine("[PASS] Runtime WorldGen implementation imports are limited to composition boundaries");
    }

    private static void TestWorldGenFocusedHelpersUseImplementationNamespace(string root)
    {
        string worldGenRoot = Path.Combine(root, "src", "HumanFortress.WorldGen");
        var violations = new List<string>();
        foreach (var rule in WorldGenFocusedHelperNamespaces)
        {
            string file = Path.Combine(worldGenRoot, rule.Key);
            if (!File.Exists(file))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} is missing");
                continue;
            }

            string text = File.ReadAllText(file);
            if (!text.Contains(rule.Value, StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} missing {rule.Value}");
            }

            if (!text.Contains("partial class FortressGenerator", StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} should keep FortressGenerator split as partials");
            }
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Focused WorldGen helpers should keep FortressGenerator split by generation phase:\n"
            + string.Join('\n', violations));
        Console.WriteLine("[PASS] Focused WorldGen helpers keep FortressGenerator split by generation phase");
    }

    private static void TestJobsImplementationUsesDirectoryNamespaces(string root)
    {
        string jobsRoot = Path.Combine(root, "src", "HumanFortress.Jobs");
        var violations = TestRepositoryPaths
            .EnumerateSourceFiles(jobsRoot)
            .Where(file => File.ReadAllText(file).Contains(RootJobsNamespace, StringComparison.Ordinal))
            .Select(file => TestRepositoryPaths.RelativePath(root, file))
            .Order(StringComparer.Ordinal)
            .ToArray();

        RegressionAssert.True(
            violations.Length == 0,
            "Jobs implementation sources should use directory namespaces instead of the root Jobs namespace:\n"
            + string.Join('\n', violations));

        var rootUsingViolations = TestRepositoryPaths
            .EnumerateSourceFiles(Path.Combine(root, "src"))
            .Concat(TestRepositoryPaths.EnumerateSourceFiles(Path.Combine(root, "tests")))
            .Where(file => !string.Equals(file, Path.Combine(root, "tests", "HumanFortress.App.Tests", "ArchitectureBoundarySmokeTests.cs"), StringComparison.Ordinal))
            .Where(file => File.ReadAllText(file).Contains(RootJobsUsingDirective, StringComparison.Ordinal))
            .Select(file => TestRepositoryPaths.RelativePath(root, file))
            .Order(StringComparer.Ordinal)
            .ToArray();

        RegressionAssert.True(
            rootUsingViolations.Length == 0,
            "Source files should import focused Jobs module namespaces instead of the root Jobs namespace:\n"
            + string.Join('\n', rootUsingViolations));
        Console.WriteLine("[PASS] Jobs implementation namespaces match their module directories");
    }

    private static void TestRuntimeFocusedHelpersUseDirectoryNamespaces(string root)
    {
        string runtimeRoot = Path.Combine(root, "src", "HumanFortress.Runtime");
        var violations = new List<string>();
        foreach (var rule in RuntimeFocusedHelperNamespaces)
        {
            string file = Path.Combine(runtimeRoot, rule.Key);
            string text = File.ReadAllText(file);
            if (!text.Contains(rule.Value, StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} missing {rule.Value}");
            }

            if (text.Contains(RootRuntimeNamespace, StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} still uses root Runtime namespace");
            }
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Focused Runtime helpers should use their directory namespaces:\n"
            + string.Join('\n', violations));
        Console.WriteLine("[PASS] Focused Runtime helper namespaces match their module directories");
    }

    private static void TestContentFocusedHelpersUseDirectoryNamespaces(string root)
    {
        string contentRoot = Path.Combine(root, "src", "HumanFortress.Content");
        var violations = new List<string>();
        foreach (var rule in ContentFocusedHelperNamespaces)
        {
            string file = Path.Combine(contentRoot, rule.Key);
            if (!File.Exists(file))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} is missing");
                continue;
            }

            string text = File.ReadAllText(file);
            if (!text.Contains(rule.Value, StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} missing {rule.Value}");
            }

            if (!text.Contains("partial class ContentRegistry", StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} should keep ContentRegistry split as partials");
            }
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Focused Content helpers should keep ContentRegistry split by responsibility:\n"
            + string.Join('\n', violations));
        Console.WriteLine("[PASS] Focused Content helpers keep ContentRegistry split by responsibility");
    }

    private static void TestContentDefinitionFocusedHelpersUseDirectoryNamespaces(string root)
    {
        string contentRoot = Path.Combine(root, "src", "HumanFortress.Content");
        var violations = new List<string>();
        foreach (var rule in ContentDefinitionFocusedHelperNamespaces)
        {
            string file = Path.Combine(contentRoot, rule.Key);
            if (!File.Exists(file))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} is missing");
                continue;
            }

            string text = File.ReadAllText(file);
            if (!text.Contains(rule.Value, StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} missing {rule.Value}");
            }

            if (!text.Contains("partial class CoreDataRegistryLoader", StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} should keep CoreDataRegistryLoader split as partials");
            }
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Focused Content definition helpers should keep CoreDataRegistryLoader split by catalog family:\n"
            + string.Join('\n', violations));
        Console.WriteLine("[PASS] Focused Content definition helpers keep CoreDataRegistryLoader split by catalog family");
    }

    private static void TestContentItemDefinitionFocusedHelpersUseDirectoryNamespaces(string root)
    {
        string contentRoot = Path.Combine(root, "src", "HumanFortress.Content");
        var violations = new List<string>();
        foreach (var rule in ContentItemDefinitionFocusedHelperNamespaces)
        {
            string file = Path.Combine(contentRoot, rule.Key);
            if (!File.Exists(file))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} is missing");
                continue;
            }

            string text = File.ReadAllText(file);
            if (!text.Contains(rule.Value, StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} missing {rule.Value}");
            }

            if (!text.Contains("partial class ItemDefinitionCatalogLoader", StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} should keep ItemDefinitionCatalogLoader split as partials");
            }
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Focused Content item definition helpers should keep ItemDefinitionCatalogLoader split by responsibility:\n"
            + string.Join('\n', violations));
        Console.WriteLine("[PASS] Focused Content item definition helpers keep ItemDefinitionCatalogLoader split by responsibility");
    }

    private static void TestContentCreatureDefinitionFocusedHelpersUseDirectoryNamespaces(string root)
    {
        string contentRoot = Path.Combine(root, "src", "HumanFortress.Content");
        var violations = new List<string>();
        foreach (var rule in ContentCreatureDefinitionFocusedHelperNamespaces)
        {
            string file = Path.Combine(contentRoot, rule.Key);
            if (!File.Exists(file))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} is missing");
                continue;
            }

            string text = File.ReadAllText(file);
            if (!text.Contains(rule.Value, StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} missing {rule.Value}");
            }

            if (!text.Contains("partial class CreatureDefinitionCatalogLoader", StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} should keep CreatureDefinitionCatalogLoader split as partials");
            }
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Focused Content creature definition helpers should keep CreatureDefinitionCatalogLoader split by responsibility:\n"
            + string.Join('\n', violations));
        Console.WriteLine("[PASS] Focused Content creature definition helpers keep CreatureDefinitionCatalogLoader split by responsibility");
    }

    private static void TestSimulationSaveFocusedHelpersUseDirectoryNamespaces(string root)
    {
        string simulationRoot = Path.Combine(root, "src", "HumanFortress.Simulation");
        var violations = new List<string>();
        foreach (var rule in SimulationSaveFocusedHelperNamespaces)
        {
            string file = Path.Combine(simulationRoot, rule.Key);
            if (!File.Exists(file))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} is missing");
                continue;
            }

            string text = File.ReadAllText(file);
            if (!text.Contains(rule.Value, StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} missing {rule.Value}");
            }

            if (Path.GetFileName(file).StartsWith("WorldSavePayloadBuilder", StringComparison.Ordinal)
                && !text.Contains("partial class WorldSavePayloadBuilder", StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} should keep WorldSavePayloadBuilder split as partials");
            }

            if (Path.GetFileName(file).StartsWith("WorldSavePayloadRestorer", StringComparison.Ordinal)
                && !text.Contains("partial class WorldSavePayloadRestorer", StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} should keep WorldSavePayloadRestorer split as partials");
            }
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Focused Simulation save helpers should keep world payload authority split by section:\n"
            + string.Join('\n', violations));
        Console.WriteLine("[PASS] Focused Simulation save helpers keep world payload authority split by section");
    }

    private static void TestSimulationItemsFocusedHelpersUseDirectoryNamespaces(string root)
    {
        string simulationRoot = Path.Combine(root, "src", "HumanFortress.Simulation");
        var violations = new List<string>();
        foreach (var rule in SimulationItemsFocusedHelperNamespaces)
        {
            string file = Path.Combine(simulationRoot, rule.Key);
            if (!File.Exists(file))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} is missing");
                continue;
            }

            string text = File.ReadAllText(file);
            if (!text.Contains(rule.Value, StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} missing {rule.Value}");
            }

            if (!text.Contains("partial class ItemManager", StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} should keep ItemManager split as partials");
            }
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Focused Simulation item helpers should keep ItemManager split by responsibility:\n"
            + string.Join('\n', violations));
        Console.WriteLine("[PASS] Focused Simulation item helpers keep ItemManager split by responsibility");
    }

    private static void TestSimulationCreaturesFocusedHelpersUseDirectoryNamespaces(string root)
    {
        string simulationRoot = Path.Combine(root, "src", "HumanFortress.Simulation");
        var violations = new List<string>();
        foreach (var rule in SimulationCreaturesFocusedHelperNamespaces)
        {
            string file = Path.Combine(simulationRoot, rule.Key);
            if (!File.Exists(file))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} is missing");
                continue;
            }

            string text = File.ReadAllText(file);
            if (!text.Contains(rule.Value, StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} missing {rule.Value}");
            }

            if (!text.Contains("partial class CreatureManager", StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} should keep CreatureManager split as partials");
            }
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Focused Simulation creature helpers should keep CreatureManager split by responsibility:\n"
            + string.Join('\n', violations));
        Console.WriteLine("[PASS] Focused Simulation creature helpers keep CreatureManager split by responsibility");
    }

    private static void TestSimulationPlaceablesFocusedHelpersUseDirectoryNamespaces(string root)
    {
        string simulationRoot = Path.Combine(root, "src", "HumanFortress.Simulation");
        var violations = new List<string>();
        foreach (var rule in SimulationPlaceablesFocusedHelperNamespaces)
        {
            string file = Path.Combine(simulationRoot, rule.Key);
            if (!File.Exists(file))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} is missing");
                continue;
            }

            string text = File.ReadAllText(file);
            if (!text.Contains(rule.Value, StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} missing {rule.Value}");
            }

            string fileName = Path.GetFileName(file);
            if (fileName.StartsWith("PlaceableInstance", StringComparison.Ordinal)
                && !text.Contains("partial class PlaceableInstance", StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} should keep PlaceableInstance split as partials");
            }

            if (fileName.StartsWith("PlaceableManager", StringComparison.Ordinal)
                && !text.Contains("partial class PlaceableManager", StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} should keep PlaceableManager split as partials");
            }

            if (fileName.StartsWith("ChunkPlaceableData", StringComparison.Ordinal)
                && !text.Contains("partial class ChunkPlaceableData", StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} should keep ChunkPlaceableData split as partials");
            }
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Focused Simulation placeable helpers should keep placeable state/factory/world/cache operations split by responsibility:\n"
            + string.Join('\n', violations));
        Console.WriteLine("[PASS] Focused Simulation placeable helpers keep placeable state/factory/world/cache operations split by responsibility");
    }

    private static void TestSimulationOrdersFocusedHelpersUseDirectoryNamespaces(string root)
    {
        string simulationRoot = Path.Combine(root, "src", "HumanFortress.Simulation");
        var violations = new List<string>();
        foreach (var rule in SimulationOrdersFocusedHelperNamespaces)
        {
            string file = Path.Combine(simulationRoot, rule.Key);
            if (!File.Exists(file))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} is missing");
                continue;
            }

            string text = File.ReadAllText(file);
            if (!text.Contains(rule.Value, StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} missing {rule.Value}");
            }

            if (!text.Contains("partial class OrdersManager", StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} should keep OrdersManager split as partials");
            }
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Focused Simulation order helpers should keep OrdersManager split by responsibility:\n"
            + string.Join('\n', violations));
        Console.WriteLine("[PASS] Focused Simulation order helpers keep OrdersManager split by responsibility");
    }

    private static void TestRuntimeSaveCodecStaysInternal(string root)
    {
        string codecPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Save", "RuntimeSaveSnapshotDocumentCodec.cs");
        string text = File.ReadAllText(codecPath);

        RegressionAssert.True(
            !text.Contains("public static class RuntimeSaveSnapshotDocumentCodec", StringComparison.Ordinal),
            "Runtime save document codec should remain internal; App-facing persistence should go through Runtime ports and Contracts DTOs.");
        Console.WriteLine("[PASS] Runtime save document codec stays internal/friend-only");
    }

    private static void TestRuntimeFullSessionPortsStayInternal(string root)
    {
        string runtimeRoot = Path.Combine(root, "src", "HumanFortress.Runtime");
        var forbiddenDeclarations = new[]
        {
            "public interface IFortressRuntimeSessionPorts",
            "public interface IFortressRuntimeSessionReplayCheckpointPort",
            "public interface IFortressRuntimeSessionSaveManifestPort",
            "public interface IFortressRuntimeSessionSaveSnapshotPort"
        };

        var violations = new List<string>();
        foreach (var file in TestRepositoryPaths.EnumerateSourceFiles(runtimeRoot))
        {
            string text = File.ReadAllText(file);
            foreach (string declaration in forbiddenDeclarations)
            {
                if (text.Contains(declaration, StringComparison.Ordinal))
                    violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} contains {declaration}");
            }
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Runtime full/save/replay session ports should remain internal:\n" + string.Join('\n', violations));
        Console.WriteLine("[PASS] Runtime full/save/replay session ports stay internal");
    }

    private static void TestImplementationProjectsDoNotExposePublicTypes(string root)
    {
        var projectNames = new[]
        {
            "HumanFortress.Content",
            "HumanFortress.Jobs",
            "HumanFortress.Navigation",
            "HumanFortress.Simulation",
            "HumanFortress.WorldGen"
        };

        var violations = new List<string>();
        foreach (string projectName in projectNames)
        {
            string projectRoot = Path.Combine(root, "src", projectName);
            foreach (var file in TestRepositoryPaths.EnumerateSourceFiles(projectRoot))
            {
                foreach (var typeName in FindPublicTypeNames(file))
                    violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} exposes public {typeName}");
            }
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Implementation projects should not expose public implementation types:\n" + string.Join('\n', violations));
        Console.WriteLine("[PASS] Content/Jobs/Navigation/Simulation/WorldGen expose no public implementation types");
    }

    private static void TestCorePublicSurfaceIsApproved(string root)
    {
        string coreRoot = Path.Combine(root, "src", "HumanFortress.Core");
        var violations = new List<string>();
        foreach (var file in TestRepositoryPaths.EnumerateSourceFiles(coreRoot))
        {
            foreach (var typeName in FindPublicTypeNames(file))
            {
                if (!AllowedCorePublicTypes.Contains(typeName))
                    violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} exposes unapproved public {typeName}");
            }
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Core public foundation surface drifted:\n" + string.Join('\n', violations));
        Console.WriteLine("[PASS] Core public surface matches the approved foundation type set");
    }

    private static void TestRuntimePublicSurfaceIsApproved(string root)
    {
        string runtimeRoot = Path.Combine(root, "src", "HumanFortress.Runtime");
        var violations = new List<string>();
        foreach (var file in TestRepositoryPaths.EnumerateSourceFiles(runtimeRoot))
        {
            foreach (var typeName in FindPublicTypeNames(file))
            {
                if (!AllowedRuntimePublicTypes.Contains(typeName))
                    violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} exposes unapproved public {typeName}");
            }
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Runtime public surface drifted:\n" + string.Join('\n', violations));
        Console.WriteLine("[PASS] Runtime public surface matches the approved factory/port set");
    }

    private static void TestContractsAndRuntimePublicPortsAvoidPresentationPrimitives(string root)
    {
        var files = TestRepositoryPaths
            .EnumerateSourceFiles(Path.Combine(root, "src", "HumanFortress.Contracts"))
            .Concat(EnumerateRuntimePublicPortSurfaceFiles(root))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var violations = new List<string>();
        foreach (string file in files)
        {
            string text = File.ReadAllText(file);
            foreach (string token in PresentationPrimitiveTokens)
            {
                if (text.Contains(token, StringComparison.Ordinal))
                    violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} contains {token}");
            }
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Contracts and Runtime public ports should use project-owned DTOs, not presentation primitives:\n"
            + string.Join('\n', violations));
        Console.WriteLine("[PASS] Contracts and Runtime public ports avoid presentation primitives");
    }

    private static IEnumerable<string> EnumerateRuntimePublicPortSurfaceFiles(string root)
    {
        string runtimeRoot = Path.Combine(root, "src", "HumanFortress.Runtime");
        foreach (string file in Directory.EnumerateFiles(runtimeRoot, "FortressRuntimeSessionPorts*.cs", SearchOption.TopDirectoryOnly))
            yield return file;

        yield return Path.Combine(runtimeRoot, "FortressRuntimeSessionFactory.cs");
    }

    private static void TestAppPublicSurfaceIsProgramOnly(string root)
    {
        string appRoot = Path.Combine(root, "src", "HumanFortress.App");
        var violations = new List<string>();
        foreach (var file in TestRepositoryPaths.EnumerateSourceFiles(appRoot))
        {
            foreach (var typeName in FindPublicTypeNames(file))
            {
                if (!string.Equals(typeName, "Program", StringComparison.Ordinal))
                    violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} exposes public {typeName}");
            }
        }

        RegressionAssert.True(
            violations.Count == 0,
            "App should expose only the executable Program entrypoint:\n" + string.Join('\n', violations));
        Console.WriteLine("[PASS] App public surface is limited to Program");
    }

    private static IEnumerable<string> FindPublicTypeNames(string file)
    {
        string text = File.ReadAllText(file);
        foreach (Match match in PublicTypePattern.Matches(text))
            yield return match.Groups[2].Value;
    }

    private static void TestProductionProjectReferenceGraph(string root)
    {
        var violations = new List<string>();
        string sourceRoot = Path.Combine(root, "src");

        foreach (var rule in AllowedProjectReferences)
        {
            string projectName = Path.GetFileNameWithoutExtension(rule.Key);
            string projectPath = Path.Combine(sourceRoot, projectName, rule.Key);
            var actual = ReadProjectReferences(projectPath);
            var expected = rule.Value.Order(StringComparer.Ordinal).ToArray();

            var unexpected = actual.Except(expected, StringComparer.Ordinal).ToArray();
            var missing = expected.Except(actual, StringComparer.Ordinal).ToArray();

            if (unexpected.Length == 0 && missing.Length == 0)
                continue;

            violations.Add($"{rule.Key}: unexpected=[{string.Join(", ", unexpected)}], missing=[{string.Join(", ", missing)}]");
        }

        RegressionAssert.True(violations.Count == 0, "Production project reference graph drifted:\n" + string.Join('\n', violations));
        Console.WriteLine("[PASS] Production project references match the approved module graph");
    }

    private static void TestContractsProjectHasNoExternalReferences(string root)
    {
        string projectPath = Path.Combine(root, "src", "HumanFortress.Contracts", "HumanFortress.Contracts.csproj");
        var document = XDocument.Load(projectPath);
        var forbiddenReferences = document
            .Descendants()
            .Where(element => element.Name.LocalName is "ProjectReference" or "PackageReference" or "FrameworkReference")
            .Select(element => element.Attribute("Include")?.Value ?? element.ToString(SaveOptions.DisableFormatting))
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Order(StringComparer.Ordinal)
            .ToArray();

        RegressionAssert.True(
            forbiddenReferences.Length == 0,
            "Contracts must remain dependency-free; forbidden references: " + string.Join(", ", forbiddenReferences));
        Console.WriteLine("[PASS] Contracts project remains dependency-free");
    }

    private static string[] ReadProjectReferences(string projectPath)
    {
        var document = XDocument.Load(projectPath);
        return document
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => include!.Replace('\\', '/').Split('/').Last())
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static void TestProjectSourceImportsFollowModuleBoundaries(string root)
    {
        var violations = new List<string>();
        foreach (var rule in AllowedProjectSourceImports)
        {
            string projectRoot = Path.Combine(root, "src", rule.Key);
            foreach (var file in TestRepositoryPaths.EnumerateSourceFiles(projectRoot))
            {
                string text = File.ReadAllText(file);
                foreach (Match match in HumanFortressUsingPattern.Matches(text))
                {
                    string importedNamespace = match.Groups[1].Value;
                    if (!rule.Value.Any(prefix => IsNamespaceWithin(importedNamespace, prefix)))
                    {
                        violations.Add(
                            $"{TestRepositoryPaths.RelativePath(root, file)} imports {importedNamespace}; allowed=[{string.Join(", ", rule.Value)}]");
                    }
                }
            }
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Project source import boundaries drifted:\n" + string.Join('\n', violations));
        Console.WriteLine("[PASS] Project source imports follow the approved module direction matrix");
    }

    private static void TestFriendAssemblyGraphIsApproved(string root)
    {
        var violations = new List<string>();
        foreach (var rule in AllowedFriendAssemblies)
        {
            string projectRoot = Path.Combine(root, "src", rule.Key);
            var actual = ReadFriendAssemblies(projectRoot);
            var expected = rule.Value.Order(StringComparer.Ordinal).ToArray();

            var unexpected = actual.Except(expected, StringComparer.Ordinal).ToArray();
            var missing = expected.Except(actual, StringComparer.Ordinal).ToArray();

            if (unexpected.Length == 0 && missing.Length == 0)
                continue;

            violations.Add($"{rule.Key}: unexpected=[{string.Join(", ", unexpected)}], missing=[{string.Join(", ", missing)}]");
        }

        RegressionAssert.True(
            violations.Count == 0,
            "InternalsVisibleTo graph drifted:\n" + string.Join('\n', violations));
        Console.WriteLine("[PASS] Friend assembly graph matches the approved runtime/test-only access model");
    }

    private static string[] ReadFriendAssemblies(string projectRoot)
    {
        var friends = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string file in Directory.EnumerateFiles(projectRoot, "*.csproj", SearchOption.TopDirectoryOnly))
            AddFriendAssemblyMatches(File.ReadAllText(file), InternalsVisibleToItemPattern, friends);

        foreach (string file in TestRepositoryPaths.EnumerateSourceFiles(projectRoot))
            AddFriendAssemblyMatches(File.ReadAllText(file), InternalsVisibleToAttributePattern, friends);

        return friends.ToArray();
    }

    private static void AddFriendAssemblyMatches(string text, Regex pattern, ISet<string> friends)
    {
        foreach (Match match in pattern.Matches(text))
            friends.Add(match.Groups[1].Value);
    }

    private static bool IsNamespaceWithin(string namespaceName, string allowedPrefix)
    {
        return string.Equals(namespaceName, allowedPrefix, StringComparison.Ordinal)
            || namespaceName.StartsWith(allowedPrefix + ".", StringComparison.Ordinal);
    }
}
