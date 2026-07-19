using HumanFortress.App.Input;
using HumanFortress.App.Runtime;
using HumanFortress.App.UI;
using HumanFortress.Content.Definitions;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;
using HumanFortress.Runtime.Commands;
using HumanFortress.Runtime.Snapshots;
using SadRogue.Primitives;

internal static class ConstructionMaterialPolicyRegressionTests
{
    internal static void RunAll()
    {
        TestContentCatalogReloadAndDefinitionOrderAreStable();
        TestAppForwardsContentOwnedMaterialSelectionWithoutRemapping();
        TestRuntimeKeepsResultMaterialSeparateFromItemDefinitionRequirements();
        TestAppUsesRuntimeOwnedWorkshopCategories();
        TestAppUsesRuntimeOwnedZoneOptions();
        TestAppUsesRuntimeOwnedDebugOptions();
    }

    private static void TestRuntimeKeepsResultMaterialSeparateFromItemDefinitionRequirements()
    {
        var requirement = new RuntimeConstructionMaterialRequirement(
            null,
            "content_item_definition",
            2);
        var filter = RuntimePlacementCommandFactory.CreateMaterialFilter(
            RuntimeConstructionShape.Wall,
            preferredMaterialId: null,
            requirements: new[] { requirement });

        RegressionAssert.True(
            filter.PreferredMaterialId == null
            && filter.Requirements.Length == 1
            && filter.Requirements[0] == requirement,
            "Runtime treated an item definition requirement as a geology material or supplied a hidden default.");

        Console.WriteLine("[PASS] Runtime keeps result material separate from item definition requirements");
    }

    private static void TestContentCatalogReloadAndDefinitionOrderAreStable()
    {
        var root = Path.Combine(Path.GetTempPath(), $"humanfortress-construction-options-{Guid.NewGuid():N}");
        var placeableDirectory = Path.Combine(root, "placeable");
        Directory.CreateDirectory(placeableDirectory);
        Directory.CreateDirectory(Path.Combine(root, "workshops"));
        var path = Path.Combine(placeableDirectory, "constructions.json");

        try
        {
            File.WriteAllText(path, BuildConstructionContent(reverse: false));
            var first = BuildCatalogSnapshotBuilder.Build(
                CoreDataRegistryLoader.Load(root).Constructions.Catalog);

            File.WriteAllText(path, BuildConstructionContent(reverse: true));
            var second = BuildCatalogSnapshotBuilder.Build(
                CoreDataRegistryLoader.Load(root).Constructions.Catalog);

            var firstRows = first.ConstructionMaterialOptions.Select(Describe).ToArray();
            var secondRows = second.ConstructionMaterialOptions.Select(Describe).ToArray();
            RegressionAssert.True(
                firstRows.SequenceEqual(secondRows, StringComparer.Ordinal)
                && firstRows.SequenceEqual(
                    new[]
                    {
                        "Wall|core_construction_wall_stone|core_mat_result_stone|tag:stone_block:4",
                        "Wall|core_construction_wall_wood|core_mat_result_wood|tag:wood_log:3",
                        "Floor|core_construction_floor_custom|core_mat_result_custom|def:core_item_custom_floor:2"
                    },
                    StringComparer.Ordinal),
                "Content-authored construction material options changed across reload or definition order.");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }

        Console.WriteLine("[PASS] Content-authored construction material catalog is reload/order stable");
    }

    private static void TestAppForwardsContentOwnedMaterialSelectionWithoutRemapping()
    {
        var ui = new UiStore
        {
            SelectedConstructionShape = UiConstructionShape.Floor,
            ConstructionMaterialDialogOpen = true
        };
        var option = new ConstructionMaterialOptionView(
            "content_test_floor",
            "Test Floor",
            RuntimeConstructionShape.Floor,
            "core_mat_result_custom",
            new[]
            {
                new RuntimeConstructionMaterialRequirement("content_tag_b", null, 3),
                new RuntimeConstructionMaterialRequirement(null, "content_item_a", 2)
            });

        FortressConstructionMaterialDialogInput.ApplySelection(
            ui,
            currentZ: 2,
            uiTick: 10,
            option: option);
        ui.PlaceFirstCorner = new Point(1, 1);

        var commands = new RecordingPlacementCommands();
        var runtime = new FortressPlacementRuntimePorts(new EmptyPlacementQueries(), commands);
        bool redrawn = false;
        var handled = FortressConstructionPlacementController.TryHandleSecondCornerClick(
            new FortressConstructionPlacementContext(
                ui,
                runtime,
                new RuntimeWorldBounds(0, 0, 20, 20, 0, 5),
                CurrentZ: 2,
                UiTick: 11,
                Redraw: () => redrawn = true),
            new Point(3, 4));

        RegressionAssert.True(
            handled
            && redrawn
            && commands.ConstructionCall.HasValue
            && commands.ConstructionCall.Value.Shape == RuntimeConstructionShape.Floor
            && commands.ConstructionCall.Value.ResultMaterialId == "core_mat_result_custom"
            && commands.ConstructionCall.Value.MaterialRequirements.SequenceEqual(
                option.Requirements),
            "App remapped or replaced the material filter selected from the Runtime read model.");

        Console.WriteLine("[PASS] App forwards Content-owned construction material selection without remapping");
    }

    private static void TestAppUsesRuntimeOwnedWorkshopCategories()
    {
        var ui = new UiStore();
        var category =
            new WorkshopCategoryView(
                "content_category_custom",
                "Custom Category",
                new[]
                {
                    new BuildableConstructionView(
                        "content_workshop_custom",
                        "Custom Workshop",
                        "workshop",
                        3,
                        3,
                        1,
                        "Nonblocking",
                        Array.Empty<string>())
                });
        var catalog = new SimulationBuildCatalogData(
            Array.Empty<BuildableConstructionView>(),
            Array.Empty<ConstructionMaterialOptionView>(),
            new[] { category });
        var categories = WorkshopCategoryPresentation.GetCategories(catalog);

        var handled = FortressBuildKeyboardInput.SelectWorkshopCategory(ui, 10, categories, 0);

        RegressionAssert.True(
            handled
            && ui.SelectedWorkshopCategory == "content_category_custom"
            && ui.WorkshopBrowsingItems,
            "App replaced or rejected the Runtime-owned workshop category id.");

        Console.WriteLine("[PASS] App consumes Runtime-owned workshop categories");
    }

    private static void TestAppUsesRuntimeOwnedZoneOptions()
    {
        var ui = new UiStore();
        var option = new ZoneMenuOptionView(
            "content_zone_custom",
            "production",
            "Custom Zone",
            "Q");
        var catalog = new SimulationZoneCatalogData(new[] { option });
        var options = ZoneOptionPresentation.GetOptions(catalog, ZoneSubmenu.Production);

        var handled = FortressZonesKeyboardInput.SelectZoneOption(
            ui,
            options.Single(),
            currentZ: 3,
            uiTick: 10);

        RegressionAssert.True(
            handled
            && options[0].Keybind == "Q"
            && ui.SelectedZoneDefId == "content_zone_custom"
            && ui.PlaceMode == PlacementMode.ZoneFirstCorner,
            "App replaced or rejected the Runtime-owned zone definition id.");

        Console.WriteLine("[PASS] App consumes Runtime-owned zone options");
    }

    private static void TestAppUsesRuntimeOwnedDebugOptions()
    {
        var ui = new UiStore();
        var debugMenu = new SimulationDebugMenuData(
            new DebugWorldStatusView(true, 0, 0, 1, 0, 2),
            new[]
            {
                new DebugItemCategoryView(
                    "Custom",
                    new[] { new DebugItemView("content_item_custom", "Custom Item") })
            },
            new[]
            {
                new DebugCreatureView("content_creature_a", "Creature A"),
                new DebugCreatureView("content_creature_b", "Creature B")
            });

        DebugSelectionPolicy.EnsureValidSelections(ui, debugMenu);
        var selected = DebugSelectionPolicy.SelectCreatureByIndex(ui, debugMenu, 1);

        RegressionAssert.True(
            selected
            && ui.DebugSelectedCreature == "content_creature_b"
            && ui.DebugSelectedItem == "content_item_custom",
            "App replaced or rejected Runtime-owned debug spawn options.");

        Console.WriteLine("[PASS] App consumes Runtime-owned debug options");
    }

    private static string Describe(ConstructionMaterialOptionView option)
    {
        return $"{option.Shape}|{option.Id}|{option.ResultMaterialId}|{string.Join(',', option.Requirements.Select(DescribeRequirement))}";
    }

    private static string DescribeRequirement(RuntimeConstructionMaterialRequirement requirement)
    {
        return requirement.Tag != null
            ? $"tag:{requirement.Tag}:{requirement.Count}"
            : $"def:{requirement.DefinitionId}:{requirement.Count}";
    }

    private static string BuildConstructionContent(bool reverse)
    {
        const string stone = """
            {
              "id": "core_construction_wall_stone",
              "name": "Stone Wall",
              "category": "wall",
              "result_material_id": "core_mat_result_stone",
              "build_time_ticks": 10,
              "materials_required": [{ "tag": "stone_block", "count": 4 }],
              "footprint": { "w": 1, "d": 1, "h": 1 },
              "passability": "blocking"
            }
            """;
        const string wood = """
            {
              "id": "core_construction_wall_wood",
              "name": "Wood Wall",
              "category": "wall",
              "result_material_id": "core_mat_result_wood",
              "build_time_ticks": 10,
              "materials_required": [{ "tag": "wood_log", "count": 3 }],
              "footprint": { "w": 1, "d": 1, "h": 1 },
              "passability": "blocking"
            }
            """;
        const string custom = """
            {
              "id": "core_construction_floor_custom",
              "name": "Custom Floor",
              "category": "floor",
              "result_material_id": "core_mat_result_custom",
              "build_time_ticks": 10,
              "materials_required": [{ "def_id": "core_item_custom_floor", "count": 2 }],
              "footprint": { "w": 1, "d": 1, "h": 1 },
              "passability": "nonblocking"
            }
            """;
        var definitions = reverse
            ? new[] { custom, wood, stone }
            : new[] { stone, wood, custom };
        return $$"""
            {
              "constructions": [
                {{string.Join(",", definitions)}}
              ]
            }
            """;
    }

    private sealed class EmptyPlacementQueries : IFortressRuntimePlacementQueryAccess
    {
        public SimulationWorldAvailabilityData GetWorldAvailabilityData() =>
            SimulationWorldAvailabilityData.Empty;

        public ZoneHitData FindZoneAt(Point worldPosition, int z) => ZoneHitData.Empty;

        public StockpileHitData FindStockpileAt(Point worldPosition, int z) => StockpileHitData.Empty;
    }

    private sealed class RecordingPlacementCommands : IFortressRuntimePlacementCommandAccess
    {
        internal ConstructionCallData? ConstructionCall { get; private set; }

        public void QueueConstructionOrder(
            Rectangle rect,
            int zMin,
            int zMax,
            RuntimeConstructionShape shape,
            string? resultMaterialId,
            RuntimeConstructionMaterialRequirement[] materialRequirements,
            int priority = 50)
        {
            ConstructionCall = new ConstructionCallData(
                rect,
                zMin,
                zMax,
                shape,
                resultMaterialId,
                materialRequirements.ToArray(),
                priority);
        }

        public void QueueHaulOrder(Rectangle rect, int z, int priority = 50) { }

        public void QueueAdvancedMiningOrder(
            Rectangle rect,
            int zMin,
            int zMax,
            RuntimeMiningAction action,
            int priority = 50) { }

        public void QueueBuildableConstructionOrder(
            string constructionId,
            Point anchor,
            int z,
            int priority = 50) { }

        public void QueueCreateZone(string defId, Rectangle rect, int z) { }

        public void QueueDeleteZone(int zoneId) { }

        public void QueueCreateStockpile(Rectangle rect, int z, string presetId) { }

        public void QueueDeleteStockpile(int zoneId) { }
    }

    private readonly record struct ConstructionCallData(
        Rectangle Rect,
        int ZMin,
        int ZMax,
        RuntimeConstructionShape Shape,
        string? ResultMaterialId,
        RuntimeConstructionMaterialRequirement[] MaterialRequirements,
        int Priority);
}
