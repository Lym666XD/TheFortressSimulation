using HumanFortress.Content.Loading;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Jobs;
using HumanFortress.Navigation;

namespace HumanFortress.Runtime;

internal sealed class FortressRuntimeTunings
{
    private FortressRuntimeTunings(
        ConstructionTuning construction,
        string? miningJson,
        NavigationTuning navigation,
        PlaceableTuning placeable,
        SchedulerTunings scheduler,
        WorkshopTunings workshops,
        FortressRuntimeStockpilePresetCatalog stockpilePresets)
    {
        Construction = construction;
        MiningJson = miningJson;
        Navigation = navigation;
        Placeable = placeable;
        Scheduler = scheduler;
        Workshops = workshops;
        StockpilePresets = stockpilePresets;
    }

    internal ConstructionTuning Construction { get; }
    internal string? MiningJson { get; }
    internal NavigationTuning Navigation { get; }
    internal PlaceableTuning Placeable { get; }
    internal SchedulerTunings Scheduler { get; }
    internal WorkshopTunings Workshops { get; }
    internal FortressRuntimeStockpilePresetCatalog StockpilePresets { get; }

    internal static FortressRuntimeTunings FromContent(
        FortressRuntimeContentSnapshot content,
        string baseDir,
        Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDir);

        return new FortressRuntimeTunings(
            ConstructionTuning.LoadFromJson(content.ConstructionTuningJson),
            content.MiningTuningJson,
            NavigationTuning.LoadFromJson(content.NavigationTuningJson),
            PlaceableTuning.LoadFromJson(content.PlaceableTuningJson),
            SchedulerTunings.LoadFromJson(content.SchedulerTuningJson, "runtime content snapshot", log),
            WorkshopTunings.LoadFromJson(content.WorkshopTuningJson, "runtime content snapshot", log),
            FortressRuntimeStockpilePresetCatalog.Load(baseDir, log));
    }
}
