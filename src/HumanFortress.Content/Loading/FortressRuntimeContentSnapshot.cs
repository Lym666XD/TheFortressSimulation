using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Contracts.Content.Identity;
using HumanFortress.Contracts.Jobs;
using StructuredContentRegistry = HumanFortress.Content.Registry.ContentRegistry;

namespace HumanFortress.Content.Loading;

/// <summary>
/// Content-owned snapshot of runtime catalogs and tuning JSON used by App composition.
/// </summary>
internal sealed class FortressRuntimeContentSnapshot
{
    internal FortressRuntimeContentSnapshot(
        ContentVersion contentVersion,
        string contentHash,
        IRuntimeMaterialCatalog materials,
        IRuntimeTerrainKindCatalog terrainKinds,
        IConstructionCatalog constructions,
        IRecipeCatalog recipes,
        IRuntimeGeologyCatalog geology,
        IReadOnlyDictionary<string, GeologyData> geologyEntries,
        IReadOnlyDictionary<string, ZoneDefinitionData> zonesById,
        IReadOnlyDictionary<string, string[]> workshopCategoryTags,
        string? constructionTuningJson,
        string? mapgenTuningJson,
        string? miningTuningJson,
        string? oreTuningJson,
        string? cavernTuningJson,
        string? navigationTuningJson,
        string? placeableTuningJson,
        string? schedulerTuningJson,
        string? workshopTuningJson,
        MechanicalContentIdentityData? mechanicalIdentity = null,
        IProfessionRegistry? professions = null,
        IReadOnlyList<StockpilePresetDefinition>? stockpilePresetDefinitions = null)
    {
        ContentVersion = contentVersion;
        ContentHash = contentHash ?? string.Empty;
        Materials = materials ?? throw new ArgumentNullException(nameof(materials));
        TerrainKinds = terrainKinds ?? throw new ArgumentNullException(nameof(terrainKinds));
        Constructions = constructions ?? throw new ArgumentNullException(nameof(constructions));
        Recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
        Geology = geology ?? throw new ArgumentNullException(nameof(geology));
        GeologyEntries = geologyEntries?.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase)
            ?? throw new ArgumentNullException(nameof(geologyEntries));
        ZonesById = zonesById?.ToDictionary(zone => zone.Key, zone => zone.Value, StringComparer.OrdinalIgnoreCase)
            ?? throw new ArgumentNullException(nameof(zonesById));
        ZoneDefinitions = ZonesById
            .OrderBy(zone => zone.Key, StringComparer.Ordinal)
            .Select(zone => zone.Value)
            .ToArray();
        WorkshopCategoryTags = workshopCategoryTags
            .OrderBy(static category => category.Key, StringComparer.Ordinal)
            .ToDictionary(
                static category => category.Key,
                static category => (IReadOnlyList<string>)Array.AsReadOnly(category.Value
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static tag => tag, StringComparer.Ordinal)
                    .ToArray()),
                StringComparer.Ordinal);
        ConstructionTuningJson = constructionTuningJson;
        MapgenTuningJson = mapgenTuningJson;
        MiningTuningJson = miningTuningJson;
        OreTuningJson = oreTuningJson;
        CavernTuningJson = cavernTuningJson;
        NavigationTuningJson = navigationTuningJson;
        PlaceableTuningJson = placeableTuningJson;
        SchedulerTuningJson = schedulerTuningJson;
        WorkshopTuningJson = workshopTuningJson;
        MechanicalIdentity = mechanicalIdentity;
        Professions = professions;
        StockpilePresetDefinitions = Array.AsReadOnly(
            stockpilePresetDefinitions?.ToArray() ?? Array.Empty<StockpilePresetDefinition>());
    }

    internal ContentVersion ContentVersion { get; }
    internal string ContentHash { get; }
    internal IRuntimeMaterialCatalog Materials { get; }
    internal IRuntimeTerrainKindCatalog TerrainKinds { get; }
    internal IConstructionCatalog Constructions { get; }
    internal IRecipeCatalog Recipes { get; }
    internal IRuntimeGeologyCatalog Geology { get; }
    internal IReadOnlyDictionary<string, GeologyData> GeologyEntries { get; }
    internal IReadOnlyDictionary<string, ZoneDefinitionData> ZonesById { get; }
    internal IReadOnlyList<ZoneDefinitionData> ZoneDefinitions { get; }
    internal IReadOnlyDictionary<string, IReadOnlyList<string>> WorkshopCategoryTags { get; }
    internal string? ConstructionTuningJson { get; }
    internal string? MapgenTuningJson { get; }
    internal string? MiningTuningJson { get; }
    internal string? OreTuningJson { get; }
    internal string? CavernTuningJson { get; }
    internal string? NavigationTuningJson { get; }
    internal string? PlaceableTuningJson { get; }
    internal string? SchedulerTuningJson { get; }
    internal string? WorkshopTuningJson { get; }
    internal MechanicalContentIdentityData? MechanicalIdentity { get; }
    internal IProfessionRegistry? Professions { get; }
    internal IReadOnlyList<StockpilePresetDefinition> StockpilePresetDefinitions { get; }
}

internal static class FortressRuntimeContentSnapshotLoader
{
    internal static FortressRuntimeContentSnapshot ApplyCoreData(
        StructuredContentRegistry registry,
        CoreDataLoadResult coreData,
        MechanicalContentIdentityData? mechanicalIdentity = null,
        IProfessionRegistry? professions = null,
        IReadOnlyList<StockpilePresetDefinition>? stockpilePresetDefinitions = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(coreData);

        registry.ApplyCoreData(coreData);
        return CaptureLoaded(
            registry,
            mechanicalIdentity,
            professions,
            stockpilePresetDefinitions);
    }

    internal static FortressRuntimeContentSnapshot CaptureLoaded(
        StructuredContentRegistry registry,
        MechanicalContentIdentityData? mechanicalIdentity = null,
        IProfessionRegistry? professions = null,
        IReadOnlyList<StockpilePresetDefinition>? stockpilePresetDefinitions = null)
    {
        ArgumentNullException.ThrowIfNull(registry);

        return new FortressRuntimeContentSnapshot(
            registry.Version,
            registry.ContentHash,
            registry.Materials,
            registry.TerrainKinds,
            registry.Constructions,
            registry.Recipes,
            registry,
            registry.GeologyEntries,
            registry.Zones,
            registry.WorkshopCategoryTags,
            registry.GetTuningJson("tuning.construction", "$"),
            registry.GetTuningJson("tuning.mapgen", "$"),
            registry.GetTuningJson("tuning.mining", "$"),
            registry.GetTuningJson("tuning.ore", "$"),
            registry.GetTuningJson("tuning.cavern", "$"),
            registry.GetTuningJson("tuning.navigation", "$"),
            registry.GetTuningJson("tuning.placeable", "$"),
            registry.GetTuningJson("tuning.scheduler", "$"),
            registry.GetTuningJson("tuning.workshops", "$"),
            mechanicalIdentity,
            professions,
            stockpilePresetDefinitions);
    }
}
