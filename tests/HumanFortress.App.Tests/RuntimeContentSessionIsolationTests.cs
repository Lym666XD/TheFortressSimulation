using HumanFortress.Content.Loading;
using HumanFortress.Contracts.Content.Registry;

internal static class RuntimeContentSessionIsolationTests
{
    internal static void RunAll()
    {
        TestLoadedRegistriesAndCapturedCatalogsAreSessionIsolated();
        Console.WriteLine("[PASS] Runtime content registry session isolation");
    }

    private static void TestLoadedRegistriesAndCapturedCatalogsAreSessionIsolated()
    {
        var loadA = FortressContentLoader.Load(
            AppContext.BaseDirectory,
            includeCoreCatalogs: false);
        var registriesA = loadA.Registries;
        RegressionAssert.True(
            registriesA is { StructuredLoaded: true }
            && ReferenceEquals(loadA.StructuredRegistry, registriesA.Registry),
            "Session A did not retain the independently loaded structured registry.");

        var emptySnapshotA = FortressRuntimeContentSnapshotLoader.CaptureLoaded(
            loadA.StructuredRegistry);
        var snapshotA = FortressRuntimeContentSnapshotLoader.ApplyCoreData(
            loadA.StructuredRegistry,
            CreateCoreData("a"));

        var loadB = FortressContentLoader.Load(
            AppContext.BaseDirectory,
            includeCoreCatalogs: false);
        var registriesB = loadB.Registries;
        RegressionAssert.True(
            registriesB is { StructuredLoaded: true }
            && ReferenceEquals(loadB.StructuredRegistry, registriesB.Registry)
            && !ReferenceEquals(loadA.StructuredRegistry, loadB.StructuredRegistry),
            "FortressContentLoader reused a structured registry across sessions.");

        var snapshotB = FortressRuntimeContentSnapshotLoader.ApplyCoreData(
            loadB.StructuredRegistry,
            CreateCoreData("b"));

        RegressionAssert.True(
            emptySnapshotA.Constructions.Count == 0
            && snapshotA.Constructions.GetConstruction("core_construction_session_a") != null
            && snapshotA.Constructions.GetConstruction("core_construction_session_b") == null
            && snapshotA.Recipes.GetRecipe("core_recipe_session_a") != null
            && snapshotA.Recipes.GetRecipe("core_recipe_session_b") == null
            && snapshotB.Constructions.GetConstruction("core_construction_session_b") != null
            && snapshotB.Constructions.GetConstruction("core_construction_session_a") == null
            && snapshotB.Recipes.GetRecipe("core_recipe_session_b") != null
            && snapshotB.Recipes.GetRecipe("core_recipe_session_a") == null
            && !ReferenceEquals(snapshotA.Materials, snapshotB.Materials)
            && !ReferenceEquals(snapshotA.Geology, snapshotB.Geology),
            "Session-specific content catalogs leaked or overwrote another registry.");

        _ = FortressRuntimeContentSnapshotLoader.ApplyCoreData(
            loadB.StructuredRegistry,
            CreateCoreData("b_reloaded"));

        RegressionAssert.True(
            snapshotA.Constructions.GetConstruction("core_construction_session_a") != null
            && snapshotA.Constructions.GetConstruction("core_construction_session_b_reloaded") == null
            && snapshotB.Constructions.GetConstruction("core_construction_session_b") != null
            && snapshotB.Constructions.GetConstruction("core_construction_session_b_reloaded") == null,
            "A previously captured runtime snapshot changed after a later registry load.");
    }

    private static CoreDataLoadResult CreateCoreData(string suffix)
    {
        var construction = new ConstructionDefinition
        {
            Id = $"core_construction_session_{suffix}",
            Name = $"Session {suffix} construction",
            Category = "test",
            BuildTimeTicks = 1,
            MaterialCosts = new[]
            {
                new MaterialCost { Tag = "stone", Count = 1 }
            },
            PlaceableProfile = new PlaceableProfile()
        };
        var constructions = ConstructionCatalogStore.FromDefinitions(new[] { construction });

        var recipe = new RecipeDefinition
        {
            Id = $"core_recipe_session_{suffix}",
            Name = $"Session {suffix} recipe",
            Workshops = new[] { construction.Id }
        };
        var recipes = RecipeCatalogStore.FromDefinitions(new[] { recipe });

        return new CoreDataLoadResult(
            new ConstructionContentLoadResult(
                constructions,
                constructions.Count,
                errorCount: 0,
                duplicatesSkipped: 0,
                categories: new[] { "test" },
                messages: Array.Empty<string>()),
            new RecipeContentLoadResult(
                recipes,
                recipes.Count,
                errorCount: 0,
                messages: Array.Empty<string>()));
    }
}
