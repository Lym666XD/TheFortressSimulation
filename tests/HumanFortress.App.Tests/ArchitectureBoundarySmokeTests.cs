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

    private static readonly (string Description, Regex Pattern)[] ForbiddenActiveSourceCompatibilityNamespaces =
    {
        ("legacy Core.Content namespace", new Regex(
            @"^\s*namespace\s+HumanFortress\.Core\.Content(?:\s*[;{]|\.)",
            RegexOptions.Compiled | RegexOptions.Multiline)),
        ("legacy Core.Content using", new Regex(
            @"^\s*using\s+HumanFortress\.Core\.Content(?:\s*;|\.)",
            RegexOptions.Compiled | RegexOptions.Multiline)),
        ("root Navigation implementation namespace", new Regex(
            @"^\s*namespace\s+HumanFortress\.Navigation\s*[;{]",
            RegexOptions.Compiled | RegexOptions.Multiline)),
        ("root Navigation using", new Regex(
            @"^\s*using\s+HumanFortress\.Navigation\s*;",
            RegexOptions.Compiled | RegexOptions.Multiline)),
        ("root WorldGen implementation namespace", new Regex(
            @"^\s*namespace\s+HumanFortress\.WorldGen\s*[;{]",
            RegexOptions.Compiled | RegexOptions.Multiline)),
        ("root WorldGen using", new Regex(
            @"^\s*using\s+HumanFortress\.WorldGen\s*;",
            RegexOptions.Compiled | RegexOptions.Multiline)),
        ("root Jobs using", new Regex(
            @"^\s*using\s+HumanFortress\.Jobs\s*;",
            RegexOptions.Compiled | RegexOptions.Multiline))
    };

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

    private static readonly string[] ForbiddenContractsAuthorityTokens =
    {
        "System.Random",
        "new Random",
        "Random.Shared",
        "Guid.NewGuid(",
        "DateTime.Now",
        "DateTime.UtcNow",
        "Stopwatch"
    };

    private static readonly IReadOnlyDictionary<string, string> RuntimeFocusedHelperNamespaces = new Dictionary<string, string>
    {
        ["Navigation/SimulationNavigationSource.cs"] = "namespace HumanFortress.Runtime.Navigation;",
        ["Navigation/SimulationNavigationSource.Mapping.cs"] = "namespace HumanFortress.Runtime.Navigation;",
        ["Navigation/SimulationNavigationSource.ConstructionSites.cs"] = "namespace HumanFortress.Runtime.Navigation;",
        ["Navigation/SimulationNavigationFactory.cs"] = "namespace HumanFortress.Runtime.Navigation;",
        ["Navigation/RuntimePathServiceRegistry.cs"] = "namespace HumanFortress.Runtime.Navigation;",
        ["Navigation/RuntimeNavigationServices.cs"] = "namespace HumanFortress.Runtime.Navigation;",
        ["Content/SimulationWorldContentLoader.cs"] = "namespace HumanFortress.Runtime.Content;",
        ["Content/SimulationWorldContentLoader.Logging.cs"] = "namespace HumanFortress.Runtime.Content;",
        ["Content/RuntimeCraftRecipeCatalogAdapter.cs"] = "namespace HumanFortress.Runtime.Content;",
        ["Content/FortressRuntimeStockpilePresetCatalog.cs"] = "namespace HumanFortress.Runtime.Content;",
        ["Diff/RuntimeMutationDiffLogs.cs"] = "namespace HumanFortress.Runtime.Diff;",
        ["Diff/ProfessionAssignmentDiffLog.cs"] = "namespace HumanFortress.Runtime.Diff;",
        ["Diagnostics/CallbackFactoryDiagnosticSink.cs"] = "namespace HumanFortress.Runtime.Diagnostics;",
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
        ["Snapshots/RuntimeFrameSnapshotPublisher.cs"] = "namespace HumanFortress.Runtime.Snapshots;",
        ["Snapshots/RuntimeFrameSnapshotPublisher.MapDelta.cs"] = "namespace HumanFortress.Runtime.Snapshots;",
        ["Snapshots/RuntimeFrameSnapshotPublisher.OverlayDelta.cs"] = "namespace HumanFortress.Runtime.Snapshots;",
        ["Snapshots/RuntimeFrameSnapshotPublisher.Presenter.cs"] = "namespace HumanFortress.Runtime.Snapshots;",
        ["Snapshots/RuntimeFrameSnapshotPublisher.RequestHash.cs"] = "namespace HumanFortress.Runtime.Snapshots;",
        ["Snapshots/RuntimeFrameSnapshotPublisher.State.cs"] = "namespace HumanFortress.Runtime.Snapshots;",
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
        ["Save/RuntimeSaveContentCatalogSummaryFactory.cs"] = "namespace HumanFortress.Runtime.Save;",
        ["Save/RuntimeSaveContentSignatureFactory.cs"] = "namespace HumanFortress.Runtime.Save;",
        ["Save/RuntimeSaveJobStateRestorePolicy.cs"] = "namespace HumanFortress.Runtime.Save;",
        ["Save/RuntimeSaveSlotCompatibilityPolicy.cs"] = "namespace HumanFortress.Runtime.Save;",
        ["Save/RuntimeSaveSlotContentCompatibilityPolicy.cs"] = "namespace HumanFortress.Runtime.Save;",
        ["Save/RuntimeSaveSlotManifest.cs"] = "namespace HumanFortress.Runtime.Save;",
        ["Save/RuntimeSaveSlotMigrationPlanBuilder.cs"] = "namespace HumanFortress.Runtime.Save;",
        ["Save/RuntimeSaveSlotMigrationTransformRegistry.cs"] = "namespace HumanFortress.Runtime.Save;",
        ["Save/RuntimeSaveSlotMigrator.cs"] = "namespace HumanFortress.Runtime.Save;",
        ["Save/RuntimeSaveSlotRestorePlanBuilder.cs"] = "namespace HumanFortress.Runtime.Save;",
        ["Save/RuntimeSaveSnapshotDocumentCraftMapper.cs"] = "namespace HumanFortress.Runtime.Save;",
        ["Save/RuntimeSaveSnapshotDocumentMiningMapper.cs"] = "namespace HumanFortress.Runtime.Save;",
        ["Save/RuntimeSaveSnapshotDocumentTransportMapper.cs"] = "namespace HumanFortress.Runtime.Save;",
        ["Save/RuntimeSaveSnapshotDocumentVerifier.Jobs.cs"] = "namespace HumanFortress.Runtime.Save;",
        ["Save/RuntimeSaveSnapshotCraftJobRestorer.cs"] = "namespace HumanFortress.Runtime.Save;",
        ["Save/RuntimeSaveSnapshotMiningJobRestorer.cs"] = "namespace HumanFortress.Runtime.Save;",
        ["Save/RuntimeSaveSnapshotTransportJobRestorer.cs"] = "namespace HumanFortress.Runtime.Save;",
        ["Save/RuntimeSaveSnapshotDocumentStore.Inspection.cs"] = "namespace HumanFortress.Runtime.Save;",
        ["Save/RuntimeSaveSnapshotDocumentStore.IO.cs"] = "namespace HumanFortress.Runtime.Save;",
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

    private static readonly IReadOnlyDictionary<string, string> JobsTransportFocusedHelperNamespaces = new Dictionary<string, string>
    {
        ["Transport/TransportJobExecutor.cs"] = "namespace HumanFortress.Jobs.Transport;",
        ["Transport/TransportJobExecutor.Helpers.cs"] = "namespace HumanFortress.Jobs.Transport;",
        ["Transport/TransportJobExecutor.Read.cs"] = "namespace HumanFortress.Jobs.Transport;",
        ["Transport/TransportJobExecutor.Restore.cs"] = "namespace HumanFortress.Jobs.Transport;",
        ["Transport/TransportJobExecutor.Scheduling.cs"] = "namespace HumanFortress.Jobs.Transport;",
        ["Transport/TransportJobExecutor.Snapshots.cs"] = "namespace HumanFortress.Jobs.Transport;",
        ["Transport/TransportJobExecutor.Write.cs"] = "namespace HumanFortress.Jobs.Transport;"
    };

    private static readonly IReadOnlyDictionary<string, string> JobsCraftFocusedHelperNamespaces = new Dictionary<string, string>
    {
        ["Craft/CraftJobExecutor.cs"] = "namespace HumanFortress.Jobs.Craft;",
        ["Craft/CraftJobExecutor.Restore.cs"] = "namespace HumanFortress.Jobs.Craft;"
    };

    private static readonly IReadOnlyDictionary<string, string> JobsMiningFocusedHelperNamespaces = new Dictionary<string, string>
    {
        ["Mining/MiningJobExecutor.cs"] = "namespace HumanFortress.Jobs.Mining;",
        ["Mining/MiningJobExecutor.Restore.cs"] = "namespace HumanFortress.Jobs.Mining;"
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
        ["Save/WorldSavePayloadRestorer.Validation.Entities.cs"] = "namespace HumanFortress.Simulation.Save;",
        ["Save/WorldSavePayloadRestorer.Validation.Geometry.cs"] = "namespace HumanFortress.Simulation.Save;",
        ["Save/WorldSavePayloadRestorer.Validation.Orders.cs"] = "namespace HumanFortress.Simulation.Save;",
        ["Save/WorldSavePayloadRestorer.Validation.Stockpiles.cs"] = "namespace HumanFortress.Simulation.Save;",
        ["Save/WorldSavePayloadRestorer.Validation.cs"] = "namespace HumanFortress.Simulation.Save;"
    };

    private static readonly IReadOnlyDictionary<string, string> SimulationReplayFocusedHelperNamespaces = new Dictionary<string, string>
    {
        ["Replay/WorldReplayHashBuilder.cs"] = "namespace HumanFortress.Simulation.Replay;",
        ["Replay/WorldReplayHashBuilder.Common.cs"] = "namespace HumanFortress.Simulation.Replay;",
        ["Replay/WorldReplayHashBuilder.Entities.cs"] = "namespace HumanFortress.Simulation.Replay;",
        ["Replay/WorldReplayHashBuilder.Reservations.cs"] = "namespace HumanFortress.Simulation.Replay;",
        ["Replay/WorldReplayHashBuilder.Stockpiles.cs"] = "namespace HumanFortress.Simulation.Replay;",
        ["Replay/WorldReplayHashBuilder.Terrain.cs"] = "namespace HumanFortress.Simulation.Replay;"
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
        ["Placeables/ConstructionMaterialRequirement.cs"] = "namespace HumanFortress.Simulation.Placeables;",
        ["Placeables/PlaceableInstance.cs"] = "namespace HumanFortress.Simulation.Placeables;",
        ["Placeables/PlaceableInstance.ConstructionFactory.cs"] = "namespace HumanFortress.Simulation.Placeables;",
        ["Placeables/PlaceableInstance.ItemFactory.cs"] = "namespace HumanFortress.Simulation.Placeables;",
        ["Placeables/PlaceableKind.cs"] = "namespace HumanFortress.Simulation.Placeables;",
        ["Placeables/DoorState.cs"] = "namespace HumanFortress.Simulation.Placeables;",
        ["Placeables/ConstructionSiteState.cs"] = "namespace HumanFortress.Simulation.Placeables;",
        ["Placeables/PlaceableManager.cs"] = "namespace HumanFortress.Simulation.Placeables;",
        ["Placeables/PlaceableManager.AffectedChunks.cs"] = "namespace HumanFortress.Simulation.Placeables;",
        ["Placeables/PlaceableManager.Collision.cs"] = "namespace HumanFortress.Simulation.Placeables;",
        ["Placeables/PlaceableManager.Doors.cs"] = "namespace HumanFortress.Simulation.Placeables;",
        ["Placeables/PlaceableManager.Lookup.cs"] = "namespace HumanFortress.Simulation.Placeables;",
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
        ["Orders/OrdersManager.SaveRestore.cs"] = "namespace HumanFortress.Simulation.Orders;",
        ["Orders/MiningZRangeMapper.cs"] = "namespace HumanFortress.Simulation.Orders;"
    };

    private static readonly IReadOnlyDictionary<string, string> SimulationMiningFocusedHelperNamespaces = new Dictionary<string, string>
    {
        ["Orders/MiningSystem.cs"] = "namespace HumanFortress.Simulation.Orders;",
        ["Orders/MiningSystem.Tick.cs"] = "namespace HumanFortress.Simulation.Orders;",
        ["Orders/MiningSystem.Scanner.cs"] = "namespace HumanFortress.Simulation.Orders;",
        ["Orders/MiningSystem.Cancellation.cs"] = "namespace HumanFortress.Simulation.Orders;",
        ["Orders/MiningSystem.Helpers.cs"] = "namespace HumanFortress.Simulation.Orders;",
        ["Orders/MiningActiveDesignation.cs"] = "namespace HumanFortress.Simulation.Orders;"
    };

    private static readonly IReadOnlyDictionary<string, string> SimulationDiffFocusedHelperNamespaces = new Dictionary<string, string>
    {
        ["Diff/SimulationDiffApplicator.cs"] = "namespace HumanFortress.Simulation.Diff;",
        ["Diff/SimulationDiffApplicator.Terrain.cs"] = "namespace HumanFortress.Simulation.Diff;",
        ["Diff/SimulationDiffApplicator.Items.cs"] = "namespace HumanFortress.Simulation.Diff;",
        ["Diff/SimulationDiffApplicator.Creatures.cs"] = "namespace HumanFortress.Simulation.Diff;",
        ["Diff/SimulationDiffApplicator.Targets.cs"] = "namespace HumanFortress.Simulation.Diff;"
    };

    private static readonly IReadOnlySet<string> AllowedRuntimePublicTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "FortressRuntimeContentLoader",
        "FortressRuntimeLoggingBootstrap",
        "FortressRuntimeSessionFactory",
        "FortressRuntimeWorldGenerationFactory",
        "IFortressRuntimeAppSessionPorts",
        "IFortressRuntimeSessionBootstrapPort",
        "IFortressRuntimeSessionCatalogQueryPort",
        "IFortressRuntimeSessionDebugCommandPort",
        "IFortressRuntimeSessionLifecyclePort",
        "IFortressRuntimeSessionPlacementCommandPort",
        "IFortressRuntimeSessionProfessionCommandPort",
        "IFortressRuntimeSessionReadPort",
        "IFortressRuntimeSessionSimulationControlPort",
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
        "IReadPlanStage",
        "ISequentialCompatibilityStage",
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
        "TickSchedulerHealthSnapshot",
        "TickSchedulerSystemFailureSnapshot",
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
        ["HumanFortress.Core"] = Array.Empty<string>(),
        ["HumanFortress.Content"] = new[] { "HumanFortress.App.Tests", "HumanFortress.Runtime" },
        ["HumanFortress.Navigation"] = new[] { "HumanFortress.App.Tests", "HumanFortress.Runtime" },
        ["HumanFortress.Simulation"] = new[] { "HumanFortress.App.Tests", "HumanFortress.Jobs", "HumanFortress.Runtime", "HumanFortress.WorldGen" },
        ["HumanFortress.Jobs"] = new[] { "HumanFortress.App.Tests", "HumanFortress.Runtime" },
        ["HumanFortress.WorldGen"] = new[] { "HumanFortress.App.Tests", "HumanFortress.Runtime" },
        ["HumanFortress.Runtime"] = new[] { "HumanFortress.App.Tests", "HumanFortress.Scenarios" },
        ["HumanFortress.App"] = new[] { "HumanFortress.App.Tests" }
    };

    public static void RunAll()
    {
        Console.WriteLine("=== Architecture Boundary Smoke Tests ===");

        string root = TestRepositoryPaths.FindRepositoryRoot();
        TestAppSourceDoesNotReferenceForbiddenRuntimeModules(root);
        TestAppRuntimeNamespaceUseIsAdapterOnly(root);
        TestAppGameplayOptionsComeFromRuntimeReadModels(root);
        TestRuntimeNamespaceUseIsBoundaryOnly(root);
        TestAppProjectDoesNotReferenceLowerImplementationProjects(root);
        TestContentLoaderImplementationStaysInternal(root);
        TestContentImplementationMembersStayInternalExceptSerializerDtos(root);
        TestActiveSourceAvoidsLegacyCompatibilityNamespaces(root);
        TestNavigationImplementationUsesExplicitNamespace(root);
        TestWorldGenImplementationUsesExplicitNamespace(root);
        TestWorldGenFocusedHelpersUseImplementationNamespace(root);
        TestWorldGenImplementationImportsAreRuntimeCompositionOnly(root);
        TestWorldGenDiagnosticsAreRuntimeInjected(root);
        TestJobsImplementationUsesDirectoryNamespaces(root);
        TestJobsTransportExecutorFocusedHelpersUseDirectoryNamespaces(root);
        TestJobsCraftExecutorFocusedHelpersUseDirectoryNamespaces(root);
        TestJobsMiningExecutorFocusedHelpersUseDirectoryNamespaces(root);
        TestContentFocusedHelpersUseDirectoryNamespaces(root);
        TestContentDefinitionFocusedHelpersUseDirectoryNamespaces(root);
        TestContentItemDefinitionFocusedHelpersUseDirectoryNamespaces(root);
        TestContentCreatureDefinitionFocusedHelpersUseDirectoryNamespaces(root);
        TestSimulationSaveFocusedHelpersUseDirectoryNamespaces(root);
        TestSimulationReplayFocusedHelpersUseDirectoryNamespaces(root);
        TestSimulationItemsFocusedHelpersUseDirectoryNamespaces(root);
        TestSimulationCreaturesFocusedHelpersUseDirectoryNamespaces(root);
        TestSimulationPlaceablesFocusedHelpersUseDirectoryNamespaces(root);
        TestSimulationOrdersFocusedHelpersUseDirectoryNamespaces(root);
        TestSimulationMiningFocusedHelpersUseDirectoryNamespaces(root);
        TestSimulationDiffFocusedHelpersUseDirectoryNamespaces(root);
        TestSimulationZoneImplementationMembersStayInternal(root);
        TestSimulationStockpileImplementationMembersStayInternal(root);
        TestSimulationItemCreatureImplementationMembersStayInternal(root);
        TestSimulationWorldImplementationMembersStayInternal(root);
        TestSimulationPlaceablesAndTilesImplementationMembersStayInternal(root);
        TestSimulationJobsImplementationMembersStayInternal(root);
        TestSimulationOrdersImplementationMembersStayInternal(root);
        TestSimulationImplementationMembersStayInternal(root);
        TestRuntimeFocusedHelpersUseDirectoryNamespaces(root);
        TestRuntimeJobWrappersUseNavigationServices(root);
        TestJobsImplementationMembersStayInternal(root);
        TestWorldGenImplementationMembersStayInternal(root);
        TestRuntimeFrameSnapshotsUsePublisherBoundary(root);
        TestRuntimeSaveCodecStaysInternal(root);
        TestRuntimeFullSessionPortsStayInternal(root);
        TestImplementationProjectsDoNotExposePublicTypes(root);
        TestCorePublicSurfaceIsApproved(root);
        TestRuntimePublicSurfaceIsApproved(root);
        TestRuntimeImplementationMembersStayInternal(root);
        TestContractsAndRuntimePublicPortsAvoidPresentationPrimitives(root);
        TestContractsAvoidRuntimeAuthorityHelpers(root);
        TestAppPublicSurfaceIsProgramOnly(root);
        TestProductionProjectReferenceGraph(root);
        TestContractsProjectHasNoExternalReferences(root);
        TestProjectSourceImportsFollowModuleBoundaries(root);
        TestFriendAssemblyGraphIsApproved(root);
        TestLowerModulesAvoidConsoleOutputFallbacks(root);
        TestLowerModuleDiagnosticsAvoidDirectHubEmission(root);
        TestCoreInfrastructureDiagnosticsCanBeInjected(root);
        TestRuntimeDiagnosticsAvoidProcessGlobalCallbacks(root);
        TestRepositoryHasCiGateForStandardTestsAndArtifacts(root);

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

    private static void TestAppGameplayOptionsComeFromRuntimeReadModels(string root)
    {
        var forbiddenTokens = new[]
        {
            "ui.workshop_categories.json",
            "WorkshopCategoryMapper",
            "core_race_",
            "core_item_boulder_granite",
            "\"stone_block\"",
            "\"wood_log\"",
            "\"wood_plank\"",
            "\"gather_plants\"",
            "\"sand_clay\"",
            "\"restricted_traffic\"",
            "\"military_grounds\""
        };
        var violations = new List<string>();
        foreach (var file in TestRepositoryPaths.EnumerateSourceFiles(
                     Path.Combine(root, "src", "HumanFortress.App")))
        {
            var text = File.ReadAllText(file);
            foreach (var token in forbiddenTokens)
            {
                if (text.Contains(token, StringComparison.Ordinal))
                    violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} contains {token}");
            }
        }

        var contractsText = string.Join(
            '\n',
            TestRepositoryPaths.EnumerateSourceFiles(
                    Path.Combine(root, "src", "HumanFortress.Contracts", "Runtime", "Snapshots"))
                .Select(File.ReadAllText));
        var runtimeFactoryText = File.ReadAllText(Path.Combine(
            root,
            "src",
            "HumanFortress.Runtime",
            "Commands",
            "RuntimePlacementCommandFactory.Materials.cs"));

        RegressionAssert.True(
            violations.Count == 0
            && contractsText.Contains("ConstructionMaterialOptionView", StringComparison.Ordinal)
            && contractsText.Contains("WorkshopCategoryView", StringComparison.Ordinal)
            && contractsText.Contains("SimulationZoneCatalogData", StringComparison.Ordinal)
            && contractsText.Contains("DebugCreatureView", StringComparison.Ordinal)
            && !runtimeFactoryText.Contains("core_mat_stone_granite", StringComparison.Ordinal),
            "App gameplay options should come from Content/Runtime read models, without baked fallbacks:\n"
            + string.Join('\n', violations));
        Console.WriteLine("[PASS] App gameplay options come from Content/Runtime read models");
    }

    private static void TestRepositoryHasCiGateForStandardTestsAndArtifacts(string root)
    {
        string workflowPath = Path.Combine(root, ".github", "workflows", "dotnet-ci.yml");
        RegressionAssert.True(
            File.Exists(workflowPath),
            "Repository should keep a GitHub Actions CI workflow for build and standard test coverage.");

        string text = File.ReadAllText(workflowPath);
        var requiredTokens = new[]
        {
            "actions/setup-dotnet@v4",
            "dotnet-version: 8.0.x",
            "ubuntu-latest",
            "windows-latest",
            "DOTNET_TieredCompilation: \"0\"",
            "dotnet restore HumanFortress.sln",
            "dotnet build HumanFortress.sln",
            "dotnet test tests/HumanFortress.App.Tests/HumanFortress.App.Tests.csproj",
            "TestCategory=content-identity",
            "TestCategory=discoverable",
            "TestCategory=end-to-end",
            "--logger \"trx;LogFileName=",
            "--results-directory artifacts/test-results",
            "actions/upload-artifact@v4",
            "if: always()"
        };

        var missing = requiredTokens
            .Where(token => !text.Contains(token, StringComparison.Ordinal))
            .ToArray();

        RegressionAssert.True(
            missing.Length == 0,
            "CI workflow is missing standard test/result artifact tokens:\n" + string.Join('\n', missing));
        Console.WriteLine("[PASS] Repository CI gate builds the solution and publishes standard test results");
    }

    private static void TestLowerModulesAvoidConsoleOutputFallbacks(string root)
    {
        var scannedRoots = new[]
        {
            Path.Combine(root, "src", "HumanFortress.Core"),
            Path.Combine(root, "src", "HumanFortress.Content"),
            Path.Combine(root, "src", "HumanFortress.Simulation"),
            Path.Combine(root, "src", "HumanFortress.Jobs"),
            Path.Combine(root, "src", "HumanFortress.Runtime"),
            Path.Combine(root, "src", "HumanFortress.WorldGen")
        };

        var violations = scannedRoots
            .SelectMany(TestRepositoryPaths.EnumerateSourceFiles)
            .Where(file => File.ReadAllText(file).Contains("Console.WriteLine", StringComparison.Ordinal))
            .Select(file => TestRepositoryPaths.RelativePath(root, file))
            .Order(StringComparer.Ordinal)
            .ToArray();

        RegressionAssert.True(
            violations.Length == 0,
            "Implementation modules should emit diagnostics through callbacks or DiagnosticHub, not Console.WriteLine fallbacks:\n"
            + string.Join('\n', violations));
        Console.WriteLine("[PASS] Implementation modules avoid Console.WriteLine diagnostic fallbacks");
    }

    private static void TestLowerModuleDiagnosticsAvoidDirectHubEmission(string root)
    {
        var scannedRoots = new[]
        {
            Path.Combine(root, "src", "HumanFortress.Core"),
            Path.Combine(root, "src", "HumanFortress.Content"),
            Path.Combine(root, "src", "HumanFortress.Simulation"),
            Path.Combine(root, "src", "HumanFortress.Jobs"),
            Path.Combine(root, "src", "HumanFortress.Navigation"),
            Path.Combine(root, "src", "HumanFortress.Runtime"),
            Path.Combine(root, "src", "HumanFortress.WorldGen")
        };

        var violations = scannedRoots
            .SelectMany(TestRepositoryPaths.EnumerateSourceFiles)
            .Where(file =>
            {
                string text = File.ReadAllText(file);
                return text.Contains("DiagnosticHub.Sink.", StringComparison.Ordinal)
                    || text.Contains("DiagnosticHub.Error(", StringComparison.Ordinal);
            })
            .Select(file => TestRepositoryPaths.RelativePath(root, file))
            .Order(StringComparer.Ordinal)
            .ToArray();

        RegressionAssert.True(
            violations.Length == 0,
            "Implementation modules should use injected/owned diagnostic sinks instead of direct DiagnosticHub emission:\n"
            + string.Join('\n', violations));
        Console.WriteLine("[PASS] Lower module diagnostics avoid direct DiagnosticHub emission");
    }

    private static void TestCoreInfrastructureDiagnosticsCanBeInjected(string root)
    {
        string commandQueuePath = Path.Combine(root, "src", "HumanFortress.Core", "Commands", "CommandQueue.cs");
        string eventBusPath = Path.Combine(root, "src", "HumanFortress.Core", "Events", "EventBus.cs");
        string schedulerPath = Path.Combine(root, "src", "HumanFortress.Core", "Time", "TickScheduler.cs");
        string runtimeServicesPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Session", "RuntimeSessionServices.cs");

        string commandQueueText = File.ReadAllText(commandQueuePath);
        string eventBusText = File.ReadAllText(eventBusPath);
        string schedulerText = File.ReadAllText(schedulerPath);
        string runtimeServicesText = File.ReadAllText(runtimeServicesPath);
        string coreText = commandQueueText + '\n' + eventBusText + '\n' + schedulerText;

        RegressionAssert.True(
            !coreText.Contains("DiagnosticHub.Error(", StringComparison.Ordinal)
            && commandQueueText.Contains("public CommandQueue(IDiagnosticSink? diagnostics = null)", StringComparison.Ordinal)
            && commandQueueText.Contains("private IDiagnosticSink Diagnostics => _diagnostics ?? DiagnosticHub.Sink", StringComparison.Ordinal)
            && eventBusText.Contains("public EventBus(IDiagnosticSink? diagnostics = null)", StringComparison.Ordinal)
            && eventBusText.Contains("private IDiagnosticSink Diagnostics => _diagnostics ?? DiagnosticHub.Sink", StringComparison.Ordinal)
            && schedulerText.Contains("public TickScheduler(IDiagnosticSink? diagnostics = null)", StringComparison.Ordinal)
            && schedulerText.Contains("private IDiagnosticSink Diagnostics => _diagnostics ?? DiagnosticHub.Sink", StringComparison.Ordinal)
            && runtimeServicesText.Contains("internal RuntimeSessionServices(", StringComparison.Ordinal)
            && runtimeServicesText.Contains("IDiagnosticSink diagnostics,", StringComparison.Ordinal)
            && runtimeServicesText.Contains("new TickScheduler(diagnostics)", StringComparison.Ordinal)
            && runtimeServicesText.Contains("new CommandQueue(diagnostics)", StringComparison.Ordinal)
            && runtimeServicesText.Contains("new EventBus(diagnostics)", StringComparison.Ordinal)
            && runtimeServicesText.Contains("internal IDiagnosticSink Diagnostics { get; }", StringComparison.Ordinal),
            "Core infrastructure diagnostics should support injected sinks, with DiagnosticHub only as a compatibility fallback.");
        Console.WriteLine("[PASS] Core infrastructure diagnostics can be injected");
    }

    private static void TestRuntimeDiagnosticsAvoidProcessGlobalCallbacks(string root)
    {
        var scannedRoots = new[]
        {
            Path.Combine(root, "src", "HumanFortress.Navigation"),
            Path.Combine(root, "src", "HumanFortress.Simulation")
        };
        var violations = scannedRoots
            .SelectMany(TestRepositoryPaths.EnumerateSourceFiles)
            .Where(file =>
            {
                string text = File.ReadAllText(file);
                return text.Contains("static Action<string>? LogCallback", StringComparison.Ordinal)
                    || text.Contains("static System.Action<string>? LogCallback", StringComparison.Ordinal);
            })
            .Select(file => TestRepositoryPaths.RelativePath(root, file))
            .Order(StringComparer.Ordinal)
            .ToArray();

        string bindingsPath = Path.Combine(
            root,
            "src",
            "HumanFortress.Runtime",
            "Composition",
            "FortressRuntimeLogBindings.cs");
        string bindingsText = File.ReadAllText(bindingsPath);
        string worldText = File.ReadAllText(Path.Combine(
            root,
            "src",
            "HumanFortress.Simulation",
            "World",
            "World.cs"));

        RegressionAssert.True(
            violations.Length == 0
            && !bindingsText.Contains(".LogCallback =", StringComparison.Ordinal)
            && worldText.Contains("internal IDiagnosticSink Diagnostics => _diagnostics;", StringComparison.Ordinal)
            && worldText.Contains("internal void SetDiagnostics(IDiagnosticSink diagnostics)", StringComparison.Ordinal),
            "Active Runtime diagnostics should be session-owned instead of process-global callbacks:\n"
            + string.Join('\n', violations));
        Console.WriteLine("[PASS] Runtime diagnostics avoid process-global callback authority");
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

    private static void TestContentImplementationMembersStayInternalExceptSerializerDtos(string root)
    {
        string contentRoot = Path.Combine(root, "src", "HumanFortress.Content");
        var violations = new List<string>();
        foreach (var file in TestRepositoryPaths.EnumerateSourceFiles(contentRoot))
        {
            string relative = TestRepositoryPaths.RelativePath(contentRoot, file).Replace('\\', '/');
            int lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;
                var trimmed = line.TrimStart();
                if (!trimmed.StartsWith("public ", StringComparison.Ordinal))
                    continue;

                bool allowedSerializerDto =
                    relative == "Registry/ContentRegistry.TuningZonesValidation.cs"
                    && trimmed == "public List<RuntimeZoneDefinitionData>? Zones { get; set; }";

                if (!allowedSerializerDto)
                    violations.Add($"{TestRepositoryPaths.RelativePath(root, file)}:{lineNumber} exposes ordinary public members outside approved serializer DTO accessors");
            }
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Content implementation members should remain internal/friend-only except approved serializer DTO accessors:\n" + string.Join('\n', violations));
        Console.WriteLine("[PASS] Content implementation members stay internal/friend-only except approved serializer DTO accessors");
    }

    private static void TestActiveSourceAvoidsLegacyCompatibilityNamespaces(string root)
    {
        var violations = new List<string>();
        foreach (var file in TestRepositoryPaths.EnumerateSourceFiles(Path.Combine(root, "src")))
        {
            string relative = TestRepositoryPaths.RelativePath(root, file);
            string text = File.ReadAllText(file);
            foreach (var rule in ForbiddenActiveSourceCompatibilityNamespaces)
            {
                if (rule.Pattern.IsMatch(text))
                    violations.Add($"{relative} contains {rule.Description}");
            }
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Active source should not reintroduce legacy compatibility namespaces/usings:\n"
            + string.Join('\n', violations));
        Console.WriteLine("[PASS] Active source avoids legacy compatibility namespaces");
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

    private static void TestWorldGenDiagnosticsAreRuntimeInjected(string root)
    {
        string worldGeneratorPath = Path.Combine(root, "src", "HumanFortress.WorldGen", "WorldGenerator.cs");
        string worldServicePath = Path.Combine(root, "src", "HumanFortress.WorldGen", "WorldGenerationService.cs");
        string worldServiceFactoryPath = Path.Combine(root, "src", "HumanFortress.WorldGen", "WorldGenerationServiceFactory.cs");
        string fortressGeneratorPath = Path.Combine(root, "src", "HumanFortress.WorldGen", "FortressGenerator.cs");
        string fortressMapPath = Path.Combine(root, "src", "HumanFortress.WorldGen", "FortressMap.cs");
        string runtimeWorldFactoryPath = Path.Combine(root, "src", "HumanFortress.Runtime", "FortressRuntimeWorldGenerationFactory.cs");
        string runtimeFortressRunnerPath = Path.Combine(root, "src", "HumanFortress.Runtime", "WorldGeneration", "RuntimeFortressGenerationRunner.cs");

        string worldGeneratorText = File.ReadAllText(worldGeneratorPath);
        string worldServiceText = File.ReadAllText(worldServicePath);
        string worldServiceFactoryText = File.ReadAllText(worldServiceFactoryPath);
        string fortressGeneratorText = File.ReadAllText(fortressGeneratorPath);
        string fortressMapText = File.ReadAllText(fortressMapPath);
        string runtimeWorldFactoryText = File.ReadAllText(runtimeWorldFactoryPath);
        string runtimeFortressRunnerText = File.ReadAllText(runtimeFortressRunnerPath);
        string worldGenText = string.Join('\n', new[]
        {
            worldGeneratorText,
            worldServiceText,
            worldServiceFactoryText,
            fortressGeneratorText,
            fortressMapText
        });

        RegressionAssert.True(
            !worldGenText.Contains("DiagnosticHub.Sink.", StringComparison.Ordinal)
            && worldGeneratorText.Contains("WorldGenerator(IDiagnosticSink? diagnostics = null)", StringComparison.Ordinal)
            && worldGeneratorText.Contains("private IDiagnosticSink Diagnostics => _diagnostics ?? DiagnosticHub.Sink", StringComparison.Ordinal)
            && worldServiceText.Contains("WorldGenerationService(IDiagnosticSink? diagnostics = null)", StringComparison.Ordinal)
            && worldServiceText.Contains("new WorldGenerator(diagnostics)", StringComparison.Ordinal)
            && worldServiceFactoryText.Contains("Create(IDiagnosticSink? diagnostics = null)", StringComparison.Ordinal)
            && fortressGeneratorText.Contains("IDiagnosticSink? diagnostics = null", StringComparison.Ordinal)
            && fortressGeneratorText.Contains("new FortressMap(_fortressSize, 50, _content.Geology, _diagnostics)", StringComparison.Ordinal)
            && fortressMapText.Contains("FortressMap(int size, int maxZ, IRuntimeGeologyCatalog geology, IDiagnosticSink? diagnostics = null)", StringComparison.Ordinal)
            && fortressMapText.Contains("private IDiagnosticSink Diagnostics => _diagnostics ?? DiagnosticHub.Sink", StringComparison.Ordinal)
            && runtimeWorldFactoryText.Contains("WorldGenerationServiceFactory.Create(DiagnosticHub.Sink)", StringComparison.Ordinal)
            && runtimeFortressRunnerText.Contains("IDiagnosticSink? diagnostics = null", StringComparison.Ordinal)
            && runtimeFortressRunnerText.Contains("CreateFortressGenerationContent(content),\n            diagnostics)", StringComparison.Ordinal),
            "WorldGen diagnostics should be injected by Runtime composition, with DiagnosticHub only as a compatibility fallback.");
        Console.WriteLine("[PASS] WorldGen diagnostics are Runtime-injected");
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

    private static void TestJobsTransportExecutorFocusedHelpersUseDirectoryNamespaces(string root)
    {
        string jobsRoot = Path.Combine(root, "src", "HumanFortress.Jobs");
        var violations = new List<string>();
        foreach (var rule in JobsTransportFocusedHelperNamespaces)
        {
            string file = Path.Combine(jobsRoot, rule.Key);
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

            if (!text.Contains("partial class TransportJobExecutor", StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} should keep TransportJobExecutor split as partials");
            }
        }

        string mainPath = Path.Combine(jobsRoot, "Transport", "TransportJobExecutor.cs");
        string mainText = File.ReadAllText(mainPath);
        if (mainText.Contains("internal void ReadTick", StringComparison.Ordinal)
            || mainText.Contains("internal void WriteTick", StringComparison.Ordinal)
            || mainText.Contains("TransportDebugSnapshot GetDebugSnapshot", StringComparison.Ordinal)
            || mainText.Contains("TransportJobReplaySnapshot GetReplaySnapshot", StringComparison.Ordinal)
            || mainText.Contains("TransportJobRestoreResult RestoreReplaySnapshot", StringComparison.Ordinal)
            || mainText.Contains("int GetAllowedActiveCount", StringComparison.Ordinal))
        {
            violations.Add($"{TestRepositoryPaths.RelativePath(root, mainPath)} should keep tick/snapshot/scheduling behavior in focused partials");
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Focused Jobs transport helpers should keep TransportJobExecutor split by responsibility:\n"
            + string.Join('\n', violations));
        Console.WriteLine("[PASS] Focused Jobs transport helpers keep TransportJobExecutor split by responsibility");
    }

    private static void TestJobsCraftExecutorFocusedHelpersUseDirectoryNamespaces(string root)
    {
        string jobsRoot = Path.Combine(root, "src", "HumanFortress.Jobs");
        var violations = new List<string>();
        foreach (var rule in JobsCraftFocusedHelperNamespaces)
        {
            string file = Path.Combine(jobsRoot, rule.Key);
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

            if (!text.Contains("partial class CraftJobExecutor", StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} should keep CraftJobExecutor split as partials");
            }
        }

        string mainPath = Path.Combine(jobsRoot, "Craft", "CraftJobExecutor.cs");
        string mainText = File.ReadAllText(mainPath);
        if (mainText.Contains("CraftJobRestoreResult RestoreReplaySnapshot", StringComparison.Ordinal))
        {
            violations.Add($"{TestRepositoryPaths.RelativePath(root, mainPath)} should keep save/replay restore behavior in the focused restore partial");
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Focused Jobs craft helpers should keep CraftJobExecutor split by responsibility:\n"
            + string.Join('\n', violations));
        Console.WriteLine("[PASS] Focused Jobs craft helpers keep CraftJobExecutor split by responsibility");
    }

    private static void TestJobsMiningExecutorFocusedHelpersUseDirectoryNamespaces(string root)
    {
        string jobsRoot = Path.Combine(root, "src", "HumanFortress.Jobs");
        var violations = new List<string>();
        foreach (var rule in JobsMiningFocusedHelperNamespaces)
        {
            string file = Path.Combine(jobsRoot, rule.Key);
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

            if (!text.Contains("partial class MiningJobExecutor", StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} should keep MiningJobExecutor split as partials");
            }
        }

        string mainPath = Path.Combine(jobsRoot, "Mining", "MiningJobExecutor.cs");
        string mainText = File.ReadAllText(mainPath);
        if (mainText.Contains("MiningJobRestoreResult RestoreReplaySnapshot", StringComparison.Ordinal))
        {
            violations.Add($"{TestRepositoryPaths.RelativePath(root, mainPath)} should keep save/replay restore behavior in the focused restore partial");
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Focused Jobs mining helpers should keep MiningJobExecutor split by responsibility:\n"
            + string.Join('\n', violations));
        Console.WriteLine("[PASS] Focused Jobs mining helpers keep MiningJobExecutor split by responsibility");
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

    private static void TestRuntimeJobWrappersUseNavigationServices(string root)
    {
        string runtimeRoot = Path.Combine(root, "src", "HumanFortress.Runtime");
        string navigationServicesPath = Path.Combine(runtimeRoot, "Navigation", "RuntimeNavigationServices.cs");
        var violations = new List<string>();
        foreach (var file in TestRepositoryPaths.EnumerateSourceFiles(runtimeRoot))
        {
            string relative = TestRepositoryPaths.RelativePath(root, file);
            if (string.Equals(file, navigationServicesPath, StringComparison.Ordinal))
                continue;

            string text = File.ReadAllText(file);
            if (text.Contains("new PathService", StringComparison.Ordinal)
                || text.Contains("new MovementExecutor", StringComparison.Ordinal)
                || text.Contains("new WorldNavigationView", StringComparison.Ordinal)
                || text.Contains("new DeterministicAStar", StringComparison.Ordinal))
            {
                violations.Add($"{relative} creates concrete path/world-view/movement services instead of using RuntimeNavigationServices.");
            }
        }

        string navigationServicesText = File.ReadAllText(navigationServicesPath);
        if (!navigationServicesText.Contains("new PathService(_tuning)", StringComparison.Ordinal)
            || !navigationServicesText.Contains("_pathServices?.Register(paths)", StringComparison.Ordinal)
            || !navigationServicesText.Contains("new WorldNavigationView(navigation)", StringComparison.Ordinal)
            || !navigationServicesText.Contains("new MovementExecutor(query.PathService, _tuning)", StringComparison.Ordinal)
            || !navigationServicesText.Contains("CreatePathQueryServices", StringComparison.Ordinal))
        {
            violations.Add($"{TestRepositoryPaths.RelativePath(root, navigationServicesPath)} should remain the Runtime-owned path/world-view/movement service creation seam.");
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Runtime path/world-view/movement users must obtain services through RuntimeNavigationServices:\n"
            + string.Join('\n', violations));
        Console.WriteLine("[PASS] Runtime path/world-view/movement users use RuntimeNavigationServices");
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

    private static void TestSimulationReplayFocusedHelpersUseDirectoryNamespaces(string root)
    {
        string simulationRoot = Path.Combine(root, "src", "HumanFortress.Simulation");
        var violations = new List<string>();
        foreach (var rule in SimulationReplayFocusedHelperNamespaces)
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

            if (!text.Contains("partial class WorldReplayHashBuilder", StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} should keep WorldReplayHashBuilder split as partials");
            }
        }

        string mainFile = Path.Combine(simulationRoot, "Replay", "WorldReplayHashBuilder.cs");
        string mainText = File.ReadAllText(mainFile);
        string[] forbiddenMainHelpers =
        {
            "BuildTerrainHash",
            "BuildItemsHash",
            "BuildCreaturesHash",
            "BuildReservationsHash",
            "BuildStockpileZonesHash",
            "AddTerrainHash",
            "AddItemsHash",
            "AddCreaturesHash",
            "AddReservationsHash",
            "AddStockpileZonesHash"
        };
        foreach (var helper in forbiddenMainHelpers)
        {
            if (mainText.Contains($"private static string {helper}", StringComparison.Ordinal)
                || mainText.Contains($"private static void {helper}", StringComparison.Ordinal))
            {
                violations.Add($"Replay/WorldReplayHashBuilder.cs should not own section helper {helper}; keep it in a focused partial.");
            }
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Focused Simulation replay helpers should keep world replay hash authority split by section:\n"
            + string.Join('\n', violations));
        Console.WriteLine("[PASS] Focused Simulation replay helpers keep world replay hash authority split by section");
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
            var fileName = Path.GetFileName(file);
            if (!text.Contains(rule.Value, StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} missing {rule.Value}");
            }

            if (fileName.StartsWith("OrdersManager", StringComparison.Ordinal)
                && !text.Contains("partial class OrdersManager", StringComparison.Ordinal))
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

    private static void TestSimulationMiningFocusedHelpersUseDirectoryNamespaces(string root)
    {
        string simulationRoot = Path.Combine(root, "src", "HumanFortress.Simulation");
        var violations = new List<string>();
        foreach (var rule in SimulationMiningFocusedHelperNamespaces)
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
            if (fileName.StartsWith("MiningSystem", StringComparison.Ordinal)
                && !text.Contains("partial class MiningSystem", StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} should keep MiningSystem split as partials");
            }

            if (fileName == "MiningActiveDesignation.cs"
                && !text.Contains("struct ActiveDesignation", StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} should keep mining designation cursor state separate");
            }
        }

        string mainFile = Path.Combine(simulationRoot, "Orders", "MiningSystem.cs");
        string mainText = File.ReadAllText(mainFile);
        string[] forbiddenMainHelpers =
        {
            "public void ReadTick",
            "public void WriteTick",
            "TryNextDigFrom",
            "DrainNewDesignations",
            "DrainCancellationRegions",
            "IsCanceled",
            "IsTileCanceled",
            "HasStandableAdjacency",
            "SeedFrom",
            "AdvanceCursor"
        };
        foreach (var helper in forbiddenMainHelpers)
        {
            if (mainText.Contains(helper, StringComparison.Ordinal))
            {
                violations.Add($"Orders/MiningSystem.cs should not own {helper}; keep planner behavior in focused partials.");
            }
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Focused Simulation mining planner helpers should keep MiningSystem split by planner responsibility:\n"
            + string.Join('\n', violations));
        Console.WriteLine("[PASS] Focused Simulation mining planner helpers keep MiningSystem split by responsibility");
    }

    private static void TestSimulationDiffFocusedHelpersUseDirectoryNamespaces(string root)
    {
        string simulationRoot = Path.Combine(root, "src", "HumanFortress.Simulation");
        var violations = new List<string>();
        foreach (var rule in SimulationDiffFocusedHelperNamespaces)
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

            if (!text.Contains("partial class SimulationDiffApplicator", StringComparison.Ordinal))
            {
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} should keep SimulationDiffApplicator split as partials");
            }
        }

        string mainFile = Path.Combine(simulationRoot, "Diff", "SimulationDiffApplicator.cs");
        string mainText = File.ReadAllText(mainFile);
        string[] forbiddenMainHelperDeclarations =
        {
            "private static void ApplySetTerrain",
            "private static void EjectOccupantsFromBlockedTerrain",
            "private static void MarkTerrainNeighborsDirty",
            "private static void ApplyMoveItem",
            "private static void ApplyMarkCarried",
            "private static void ApplyUnmarkCarried",
            "private static void ApplyMoveCreature",
            "private static ItemInstance? FindItemByTarget",
            "private static CreatureInstance? FindCreatureByTarget",
            "private static CreatureInstance? FindCreatureByEntityArgument",
            "private static bool TryLegacyEntityId",
            "private static (ChunkKey ck, int lx, int ly) DecodeTarget"
        };
        foreach (var helper in forbiddenMainHelperDeclarations)
        {
            if (mainText.Contains(helper, StringComparison.Ordinal))
            {
                violations.Add($"Diff/SimulationDiffApplicator.cs should not own {helper}; keep operation/entity lookup behavior in focused partials.");
            }
        }

        string terrainText = File.ReadAllText(Path.Combine(simulationRoot, "Diff", "SimulationDiffApplicator.Terrain.cs"));
        string topologyTransactionText = File.ReadAllText(Path.Combine(
            simulationRoot,
            "Topology",
            "TopologyChangeTransaction.cs"));
        string itemsText = File.ReadAllText(Path.Combine(simulationRoot, "Diff", "SimulationDiffApplicator.Items.cs"));
        string creaturesText = File.ReadAllText(Path.Combine(simulationRoot, "Diff", "SimulationDiffApplicator.Creatures.cs"));
        string targetsText = File.ReadAllText(Path.Combine(simulationRoot, "Diff", "SimulationDiffApplicator.Targets.cs"));

        if (!terrainText.Contains("ApplySetTerrain", StringComparison.Ordinal)
            || !terrainText.Contains("WorldSafetyQueries.FindNearestStandableNonConstructionSite", StringComparison.Ordinal)
            || !terrainText.Contains("TopologyChangeTransaction.ApplyTerrain", StringComparison.Ordinal)
            || !topologyTransactionText.Contains("CommitTopologyChange", StringComparison.Ordinal)
            || !topologyTransactionText.Contains("_world.MarkChunkDirty", StringComparison.Ordinal))
        {
            violations.Add("Terrain diffs should own decoding/ejection and delegate atomic dirty/version publication to TopologyChangeTransaction.");
        }

        if (!itemsText.Contains("ApplyMoveItem", StringComparison.Ordinal)
            || !itemsText.Contains("ApplyMarkCarried", StringComparison.Ordinal)
            || !itemsText.Contains("ApplyUnmarkCarried", StringComparison.Ordinal)
            || !itemsText.Contains("MergeStacksAt", StringComparison.Ordinal))
        {
            violations.Add("Diff/SimulationDiffApplicator.Items.cs should own item move/carry mutation and stack merge behavior.");
        }

        if (!creaturesText.Contains("ApplyMoveCreature", StringComparison.Ordinal))
        {
            violations.Add("Diff/SimulationDiffApplicator.Creatures.cs should own creature move mutation.");
        }

        if (!targetsText.Contains("FindItemByTarget", StringComparison.Ordinal)
            || !targetsText.Contains("FindCreatureByTarget", StringComparison.Ordinal)
            || !targetsText.Contains("GetInstanceByEntityKey", StringComparison.Ordinal)
            || !targetsText.Contains("TryLegacyEntityId", StringComparison.Ordinal)
            || !targetsText.Contains("DecodeTarget", StringComparison.Ordinal))
        {
            violations.Add("Diff/SimulationDiffApplicator.Targets.cs should own encoded target/entity-key lookup helpers.");
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Focused Simulation diff helpers should keep SimulationDiffApplicator split by operation and target lookup responsibility:\n"
            + string.Join('\n', violations));
        Console.WriteLine("[PASS] Focused Simulation diff helpers keep SimulationDiffApplicator split by responsibility");
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

    private static void TestRuntimeFrameSnapshotsUsePublisherBoundary(string root)
    {
        string runtimeRoot = Path.Combine(root, "src", "HumanFortress.Runtime");
        string sessionFramePath = Path.Combine(runtimeRoot, "FortressRuntimeSessionCore.Snapshots.Frame.cs");
        string publisherPath = Path.Combine(runtimeRoot, "Snapshots", "RuntimeFrameSnapshotPublisher.cs");
        string presenterPath = Path.Combine(runtimeRoot, "Snapshots", "RuntimeFrameSnapshotPublisher.Presenter.cs");
        string overlayDeltaPath = Path.Combine(runtimeRoot, "Snapshots", "RuntimeFrameSnapshotPublisher.OverlayDelta.cs");
        string mapDeltaPath = Path.Combine(runtimeRoot, "Snapshots", "RuntimeFrameSnapshotPublisher.MapDelta.cs");
        string requestHashPath = Path.Combine(runtimeRoot, "Snapshots", "RuntimeFrameSnapshotPublisher.RequestHash.cs");
        string statePath = Path.Combine(runtimeRoot, "Snapshots", "RuntimeFrameSnapshotPublisher.State.cs");

        string sessionFrameText = File.ReadAllText(sessionFramePath);
        string publisherText = File.ReadAllText(publisherPath);
        string presenterText = File.ReadAllText(presenterPath);
        string overlayDeltaText = File.ReadAllText(overlayDeltaPath);
        string mapDeltaText = File.ReadAllText(mapDeltaPath);
        string requestHashText = File.ReadAllText(requestHashPath);
        string stateText = File.ReadAllText(statePath);

        var violations = new List<string>();
        if (!sessionFrameText.Contains("_frameSnapshots.PublishUiOverlayFrame", StringComparison.Ordinal))
            violations.Add("Session frame read port should publish overlay frames through RuntimeFrameSnapshotPublisher.");
        if (!sessionFrameText.Contains("_frameSnapshots.PublishFrameRender", StringComparison.Ordinal))
            violations.Add("Session frame read port should publish render frames through RuntimeFrameSnapshotPublisher.");
        if (sessionFrameText.Contains("SimulationSnapshotMetadata.Current", StringComparison.Ordinal)
            || sessionFrameText.Contains("FortressRuntimeSessionSnapshotFacade.BuildUiOverlayFrameSnapshot", StringComparison.Ordinal)
            || sessionFrameText.Contains("FortressRuntimeSessionSnapshotFacade.BuildFrameRenderSnapshot", StringComparison.Ordinal))
        {
            violations.Add("Session frame read port should not author metadata or call snapshot facade builders directly.");
        }
        if (!sessionFrameText.Contains("allowCache: !_services.TickScheduler.IsRunning", StringComparison.Ordinal))
            violations.Add("Session frame read port should disable request cache while the background scheduler is running.");

        if (!publisherText.Contains("SimulationSnapshotMetadata.Current(runtimeTick)", StringComparison.Ordinal))
            violations.Add("RuntimeFrameSnapshotPublisher should author Runtime snapshot metadata.");
        if (!publisherText.Contains("FortressRuntimeSessionSnapshotFacade.BuildUiOverlayFrameSnapshot", StringComparison.Ordinal)
            || !publisherText.Contains("FortressRuntimeSessionSnapshotFacade.BuildFrameRenderSnapshot", StringComparison.Ordinal))
        {
            violations.Add("RuntimeFrameSnapshotPublisher should be the frame/overlay facade invocation point.");
        }
        if (!requestHashText.Contains("ReplayHashBuilder.Compute", StringComparison.Ordinal)
            || !requestHashText.Contains("BuildUiOverlayRequestHash", StringComparison.Ordinal)
            || !requestHashText.Contains("BuildFrameRenderRequestHash", StringComparison.Ordinal)
            || !requestHashText.Contains("BuildMapViewportRequestHash", StringComparison.Ordinal)
            || !publisherText.Contains("Publication = publication", StringComparison.Ordinal))
        {
            violations.Add("RuntimeFrameSnapshotPublisher should generate stable request publication metadata.");
        }
        if (!presenterText.Contains("BuildSnapshotPayloadHash", StringComparison.Ordinal)
            || !presenterText.Contains("JsonSerializer.SerializeToUtf8Bytes", StringComparison.Ordinal)
            || !presenterText.Contains("SimulationSnapshotPresenterFrameData.FullSnapshot", StringComparison.Ordinal)
            || !publisherText.Contains("PresenterFrame = presenterFrame", StringComparison.Ordinal)
            || !stateText.Contains("_lastUiOverlayFrame = null", StringComparison.Ordinal)
            || !stateText.Contains("_lastFrameRender = null", StringComparison.Ordinal))
        {
            violations.Add("RuntimeFrameSnapshotPublisher should generate presenter-frame payload identity and reset diff bases on invalidation.");
        }
        if (!publisherText.Contains("PublishUiOverlayDelta", StringComparison.Ordinal)
            || !overlayDeltaText.Contains("BuildUiOverlaySectionHashes", StringComparison.Ordinal)
            || !overlayDeltaText.Contains("BuildChangedUiOverlaySections", StringComparison.Ordinal)
            || !overlayDeltaText.Contains("SimulationUiOverlayFrameDeltaData.Delta", StringComparison.Ordinal)
            || !stateText.Contains("_lastUiOverlaySections = null", StringComparison.Ordinal))
        {
            violations.Add("RuntimeFrameSnapshotPublisher should generate UI overlay section deltas and reset their bases on invalidation.");
        }
        if (!publisherText.Contains("PublishMapViewportDelta", StringComparison.Ordinal)
            || !mapDeltaText.Contains("BuildMapViewportPayloadHash", StringComparison.Ordinal)
            || !mapDeltaText.Contains("BuildChangedMapViewportCells", StringComparison.Ordinal)
            || !mapDeltaText.Contains("BuildChangedMapViewportRows", StringComparison.Ordinal)
            || !mapDeltaText.Contains("BuildChangedMapViewportRegions", StringComparison.Ordinal)
            || !mapDeltaText.Contains("SimulationMapViewportDeltaData.Delta", StringComparison.Ordinal)
            || !mapDeltaText.Contains("MapViewportRowDeltaView", StringComparison.Ordinal)
            || !mapDeltaText.Contains("MapViewportRegionDeltaView", StringComparison.Ordinal)
            || !stateText.Contains("_lastMapViewport = null", StringComparison.Ordinal))
        {
            violations.Add("RuntimeFrameSnapshotPublisher should generate map viewport changed-cell/row/region deltas and reset their bases on invalidation.");
        }
        if (publisherText.Contains("BuildChangedMapViewportCells", StringComparison.Ordinal)
            || publisherText.Contains("BuildChangedMapViewportRows", StringComparison.Ordinal)
            || publisherText.Contains("BuildChangedMapViewportRegions", StringComparison.Ordinal)
            || publisherText.Contains("BuildChangedUiOverlaySections", StringComparison.Ordinal)
            || publisherText.Contains("BuildSnapshotPayloadHash", StringComparison.Ordinal)
            || publisherText.Contains("internal void Invalidate", StringComparison.Ordinal)
            || publisherText.Contains("PublishedUiOverlayFrame", StringComparison.Ordinal)
            || publisherText.Contains("PublishedFrameRender", StringComparison.Ordinal)
            || publisherText.Contains("PayloadJsonOptions", StringComparison.Ordinal)
            || publisherText.Contains("lock (_gate)", StringComparison.Ordinal))
        {
            violations.Add("RuntimeFrameSnapshotPublisher main file should stay focused on frame publication entrypoints; state/cache helpers belong in the State partial.");
        }
        if (!stateText.Contains("PayloadJsonOptions", StringComparison.Ordinal)
            || !stateText.Contains("internal void Invalidate", StringComparison.Ordinal)
            || !stateText.Contains("TryGetCachedUiOverlayFrame", StringComparison.Ordinal)
            || !stateText.Contains("CacheUiOverlayFrame", StringComparison.Ordinal)
            || !stateText.Contains("TryGetCachedFrameRender", StringComparison.Ordinal)
            || !stateText.Contains("CacheFrameRender", StringComparison.Ordinal)
            || !stateText.Contains("PublishedUiOverlayFrame", StringComparison.Ordinal)
            || !stateText.Contains("PublishedFrameRender", StringComparison.Ordinal)
            || !stateText.Contains("_uiOverlayFrame = null", StringComparison.Ordinal)
            || !stateText.Contains("_frameRender = null", StringComparison.Ordinal))
        {
            violations.Add("RuntimeFrameSnapshotPublisher.State should own cache state, publication records, and invalidation.");
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Runtime frame snapshot publication boundary drifted:\n" + string.Join('\n', violations));
        Console.WriteLine("[PASS] Runtime frame snapshots publish through the Runtime-owned frame snapshot publisher");
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

    private static void TestSimulationZoneImplementationMembersStayInternal(string root)
    {
        string zonesRoot = Path.Combine(root, "src", "HumanFortress.Simulation", "Zones");
        var violations = new List<string>();
        foreach (var file in TestRepositoryPaths.EnumerateSourceFiles(zonesRoot))
        {
            string text = File.ReadAllText(file);
            if (text.Contains("public ", StringComparison.Ordinal))
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} exposes public members in an internal implementation module");
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Simulation zone implementation members should remain internal/friend-only:\n" + string.Join('\n', violations));
        Console.WriteLine("[PASS] Simulation zone implementation members stay internal/friend-only");
    }

    private static void TestSimulationStockpileImplementationMembersStayInternal(string root)
    {
        string stockpileRoot = Path.Combine(root, "src", "HumanFortress.Simulation", "Stockpile");
        var violations = new List<string>();
        foreach (var file in TestRepositoryPaths.EnumerateSourceFiles(stockpileRoot))
        {
            string text = File.ReadAllText(file);
            if (text.Contains("public ", StringComparison.Ordinal))
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} exposes public members in an internal implementation module");
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Simulation stockpile implementation members should remain internal/friend-only:\n" + string.Join('\n', violations));
        Console.WriteLine("[PASS] Simulation stockpile implementation members stay internal/friend-only");
    }

    private static void TestSimulationItemCreatureImplementationMembersStayInternal(string root)
    {
        var directories = new[]
        {
            Path.Combine(root, "src", "HumanFortress.Simulation", "Items"),
            Path.Combine(root, "src", "HumanFortress.Simulation", "Creatures")
        };

        var violations = new List<string>();
        foreach (var directory in directories)
        {
            foreach (var file in TestRepositoryPaths.EnumerateSourceFiles(directory))
            {
                string text = File.ReadAllText(file);
                if (text.Contains("public ", StringComparison.Ordinal))
                    violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} exposes public members in an internal implementation module");
            }
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Simulation item/creature implementation members should remain internal/friend-only:\n" + string.Join('\n', violations));
        Console.WriteLine("[PASS] Simulation item/creature implementation members stay internal/friend-only");
    }

    private static void TestSimulationWorldImplementationMembersStayInternal(string root)
    {
        string worldRoot = Path.Combine(root, "src", "HumanFortress.Simulation", "World");
        var violations = new List<string>();
        foreach (var file in TestRepositoryPaths.EnumerateSourceFiles(worldRoot))
        {
            int lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("public ", StringComparison.Ordinal)
                    && !trimmed.StartsWith("public override ", StringComparison.Ordinal))
                {
                    violations.Add($"{TestRepositoryPaths.RelativePath(root, file)}:{lineNumber} exposes public members in an internal implementation module");
                }
            }
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Simulation world implementation members should remain internal/friend-only except required object overrides:\n" + string.Join('\n', violations));
        Console.WriteLine("[PASS] Simulation world implementation members stay internal/friend-only");
    }

    private static void TestSimulationPlaceablesAndTilesImplementationMembersStayInternal(string root)
    {
        var directories = new[]
        {
            Path.Combine(root, "src", "HumanFortress.Simulation", "Placeables"),
            Path.Combine(root, "src", "HumanFortress.Simulation", "Tiles")
        };

        var violations = new List<string>();
        foreach (var directory in directories)
        {
            foreach (var file in TestRepositoryPaths.EnumerateSourceFiles(directory))
            {
                string text = File.ReadAllText(file);
                if (text.Contains("public ", StringComparison.Ordinal))
                    violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} exposes public members in an internal implementation module");
            }
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Simulation placeables/tile implementation members should remain internal/friend-only:\n" + string.Join('\n', violations));
        Console.WriteLine("[PASS] Simulation placeables/tile implementation members stay internal/friend-only");
    }

    private static void TestSimulationJobsImplementationMembersStayInternal(string root)
    {
        string jobsRoot = Path.Combine(root, "src", "HumanFortress.Simulation", "Jobs");
        var violations = new List<string>();
        foreach (var file in TestRepositoryPaths.EnumerateSourceFiles(jobsRoot))
        {
            string text = File.ReadAllText(file);
            if (text.Contains("public ", StringComparison.Ordinal))
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} exposes public members in an internal implementation module");
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Simulation jobs implementation members should remain internal/friend-only:\n" + string.Join('\n', violations));
        Console.WriteLine("[PASS] Simulation jobs implementation members stay internal/friend-only");
    }

    private static void TestSimulationOrdersImplementationMembersStayInternal(string root)
    {
        string ordersRoot = Path.Combine(root, "src", "HumanFortress.Simulation", "Orders");
        var violations = new List<string>();
        foreach (var file in TestRepositoryPaths.EnumerateSourceFiles(ordersRoot))
        {
            string text = File.ReadAllText(file);
            if (text.Contains("public ", StringComparison.Ordinal))
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} exposes public members in an internal implementation module");
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Simulation orders implementation members should remain internal/friend-only:\n" + string.Join('\n', violations));
        Console.WriteLine("[PASS] Simulation orders implementation members stay internal/friend-only");
    }

    private static void TestSimulationImplementationMembersStayInternal(string root)
    {
        string simulationRoot = Path.Combine(root, "src", "HumanFortress.Simulation");
        var violations = new List<string>();
        foreach (var file in TestRepositoryPaths.EnumerateSourceFiles(simulationRoot))
        {
            int lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("public ", StringComparison.Ordinal)
                    && !trimmed.StartsWith("public override ", StringComparison.Ordinal))
                {
                    violations.Add($"{TestRepositoryPaths.RelativePath(root, file)}:{lineNumber} exposes ordinary public members in an implementation project");
                }
            }
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Simulation implementation members should remain internal/friend-only except required object overrides:\n" + string.Join('\n', violations));
        Console.WriteLine("[PASS] Simulation implementation members stay internal/friend-only");
    }

    private static void TestJobsImplementationMembersStayInternal(string root)
    {
        string jobsRoot = Path.Combine(root, "src", "HumanFortress.Jobs");
        var violations = new List<string>();
        foreach (var file in TestRepositoryPaths.EnumerateSourceFiles(jobsRoot))
        {
            string text = File.ReadAllText(file);
            if (text.Contains("public ", StringComparison.Ordinal))
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} exposes public members in an internal implementation project");
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Jobs implementation members should remain internal/friend-only:\n" + string.Join('\n', violations));
        Console.WriteLine("[PASS] Jobs implementation members stay internal/friend-only");
    }

    private static void TestWorldGenImplementationMembersStayInternal(string root)
    {
        string worldGenRoot = Path.Combine(root, "src", "HumanFortress.WorldGen");
        var violations = new List<string>();
        foreach (var file in TestRepositoryPaths.EnumerateSourceFiles(worldGenRoot))
        {
            string text = File.ReadAllText(file);
            if (text.Contains("public ", StringComparison.Ordinal))
                violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} exposes public members in an internal implementation project");
        }

        RegressionAssert.True(
            violations.Count == 0,
            "WorldGen implementation members should remain internal/friend-only:\n" + string.Join('\n', violations));
        Console.WriteLine("[PASS] WorldGen implementation members stay internal/friend-only");
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

    private static void TestRuntimeImplementationMembersStayInternal(string root)
    {
        string runtimeRoot = Path.Combine(root, "src", "HumanFortress.Runtime");
        var allowedPublicMemberFiles = new HashSet<string>(StringComparer.Ordinal)
        {
            "FortressRuntimeContentLoader.cs",
            "FortressRuntimeLoggingBootstrap.cs",
            "FortressRuntimeSessionFactory.cs",
            "FortressRuntimeSessionPorts.CatalogQueries.cs",
            "FortressRuntimeSessionPorts.Commands.cs",
            "FortressRuntimeSessionPorts.Lifecycle.cs",
            "FortressRuntimeSessionPorts.Read.cs",
            "FortressRuntimeSessionPorts.Snapshots.cs",
            "FortressRuntimeSessionPorts.cs",
            "FortressRuntimeWorldGenerationFactory.cs"
        };

        var violations = new List<string>();
        foreach (var file in TestRepositoryPaths.EnumerateSourceFiles(runtimeRoot))
        {
            string relative = TestRepositoryPaths.RelativePath(runtimeRoot, file).Replace('\\', '/');
            if (allowedPublicMemberFiles.Contains(relative))
                continue;

            int lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;
                if (line.TrimStart().StartsWith("public ", StringComparison.Ordinal))
                    violations.Add($"{TestRepositoryPaths.RelativePath(root, file)}:{lineNumber} exposes ordinary public members outside Runtime's approved App-facing API files");
            }
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Runtime implementation members should remain internal/friend-only outside approved public API files:\n" + string.Join('\n', violations));
        Console.WriteLine("[PASS] Runtime implementation members stay internal/friend-only outside public API files");
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

    private static void TestContractsAvoidRuntimeAuthorityHelpers(string root)
    {
        var files = TestRepositoryPaths
            .EnumerateSourceFiles(Path.Combine(root, "src", "HumanFortress.Contracts"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        var violations = new List<string>();
        foreach (string file in files)
        {
            string text = File.ReadAllText(file);
            foreach (string token in ForbiddenContractsAuthorityTokens)
            {
                if (text.Contains(token, StringComparison.Ordinal))
                    violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} contains {token}");
            }
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Contracts should remain passive DTO/port definitions and avoid runtime authority helpers:\n"
            + string.Join('\n', violations));
        Console.WriteLine("[PASS] Contracts avoid runtime authority helpers");
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
