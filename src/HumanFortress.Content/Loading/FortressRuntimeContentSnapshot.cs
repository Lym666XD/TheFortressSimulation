using HumanFortress.Contracts.Content.Registry;
using StructuredContentRegistry = HumanFortress.Content.Registry.ContentRegistry;

namespace HumanFortress.Content.Loading;

/// <summary>
/// Content-owned snapshot of runtime catalogs and tuning JSON used by App composition.
/// </summary>
internal sealed class FortressRuntimeContentSnapshot
{
    internal FortressRuntimeContentSnapshot(
        IRuntimeMaterialCatalog materials,
        IRuntimeTerrainKindCatalog terrainKinds,
        IConstructionCatalog constructions,
        IRecipeCatalog recipes,
        IRuntimeGeologyCatalog geology,
        IReadOnlyDictionary<string, GeologyData> geologyEntries,
        IReadOnlyDictionary<string, ZoneDefinitionData> zonesById,
        string? constructionTuningJson,
        string? mapgenTuningJson,
        string? miningTuningJson,
        string? oreTuningJson,
        string? cavernTuningJson,
        string? navigationTuningJson,
        string? placeableTuningJson,
        string? schedulerTuningJson,
        string? workshopTuningJson)
    {
        Materials = materials ?? throw new ArgumentNullException(nameof(materials));
        TerrainKinds = terrainKinds ?? throw new ArgumentNullException(nameof(terrainKinds));
        Constructions = constructions ?? throw new ArgumentNullException(nameof(constructions));
        Recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
        Geology = geology ?? throw new ArgumentNullException(nameof(geology));
        GeologyEntries = geologyEntries?.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase)
            ?? throw new ArgumentNullException(nameof(geologyEntries));
        ZonesById = zonesById?.ToDictionary(zone => zone.Key, zone => zone.Value, StringComparer.OrdinalIgnoreCase)
            ?? throw new ArgumentNullException(nameof(zonesById));
        ZoneDefinitions = ZonesById.Values.ToArray();
        ConstructionTuningJson = constructionTuningJson;
        MapgenTuningJson = mapgenTuningJson;
        MiningTuningJson = miningTuningJson;
        OreTuningJson = oreTuningJson;
        CavernTuningJson = cavernTuningJson;
        NavigationTuningJson = navigationTuningJson;
        PlaceableTuningJson = placeableTuningJson;
        SchedulerTuningJson = schedulerTuningJson;
        WorkshopTuningJson = workshopTuningJson;
    }

    internal IRuntimeMaterialCatalog Materials { get; }
    internal IRuntimeTerrainKindCatalog TerrainKinds { get; }
    internal IConstructionCatalog Constructions { get; }
    internal IRecipeCatalog Recipes { get; }
    internal IRuntimeGeologyCatalog Geology { get; }
    internal IReadOnlyDictionary<string, GeologyData> GeologyEntries { get; }
    internal IReadOnlyDictionary<string, ZoneDefinitionData> ZonesById { get; }
    internal IReadOnlyList<ZoneDefinitionData> ZoneDefinitions { get; }
    internal string? ConstructionTuningJson { get; }
    internal string? MapgenTuningJson { get; }
    internal string? MiningTuningJson { get; }
    internal string? OreTuningJson { get; }
    internal string? CavernTuningJson { get; }
    internal string? NavigationTuningJson { get; }
    internal string? PlaceableTuningJson { get; }
    internal string? SchedulerTuningJson { get; }
    internal string? WorkshopTuningJson { get; }
}

internal static class FortressRuntimeContentSnapshotLoader
{
    internal static FortressRuntimeContentSnapshot ApplyCoreData(CoreDataLoadResult coreData)
    {
        ArgumentNullException.ThrowIfNull(coreData);

        StructuredContentRegistry.Instance.ApplyCoreData(coreData);
        return CaptureLoaded();
    }

    internal static FortressRuntimeContentSnapshot CaptureLoaded()
    {
        var registry = StructuredContentRegistry.Instance;

        return new FortressRuntimeContentSnapshot(
            registry.Materials,
            registry.TerrainKinds,
            registry.Constructions,
            registry.Recipes,
            registry,
            registry.GeologyEntries,
            registry.Zones,
            registry.GetTuningJson("tuning.construction", "$"),
            registry.GetTuningJson("tuning.mapgen", "$"),
            registry.GetTuningJson("tuning.mining", "$"),
            registry.GetTuningJson("tuning.ore", "$"),
            registry.GetTuningJson("tuning.cavern", "$"),
            registry.GetTuningJson("tuning.navigation", "$"),
            registry.GetTuningJson("tuning.placeable", "$"),
            registry.GetTuningJson("tuning.scheduler", "$"),
            registry.GetTuningJson("tuning.workshops", "$"));
    }
}
