using HumanFortress.Core.Content.Registry;
using HumanFortress.Simulation.Placeables;
using SadRogue.Primitives;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Construction;

internal sealed class ConstructionCompletionApplier
{
    private readonly WorldModel _world;
    private readonly IConstructionDiffEmitter _diffEmitter;
    private readonly IConstructionWorkshopCompletionSink? _workshopCompletionSink;
    private readonly IConstructionJobLogger _logger;
    private readonly IConstructionCatalog _constructions;

    public ConstructionCompletionApplier(
        WorldModel world,
        IConstructionDiffEmitter diffEmitter,
        IConstructionCatalog constructions,
        IConstructionWorkshopCompletionSink? workshopCompletionSink,
        IConstructionJobLogger? logger)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _diffEmitter = diffEmitter ?? throw new ArgumentNullException(nameof(diffEmitter));
        _constructions = constructions ?? throw new ArgumentNullException(nameof(constructions));
        _workshopCompletionSink = workshopCompletionSink;
        _logger = logger ?? NullConstructionJobLogger.Instance;
    }

    public void Complete(PlaceableInstance site, ulong tick)
    {
        if (site.ConstructionSite == null)
        {
            return;
        }

        if (ConstructionTargetMapper.IsTerrainTarget(site.ConstructionSite.TargetId))
        {
            CompleteTerrain(site, tick);
            return;
        }

        CompletePlaceable(site, tick);
    }

    private void CompleteTerrain(PlaceableInstance site, ulong tick)
    {
        var construction = site.ConstructionSite!;
        var kind = ConstructionTargetMapper.ToTerrainKind(construction.TargetId);
        _diffEmitter.SetTerrain(site.Position, site.Z, kind);

        bool removed = RemoveConstructionSite(site, tick);
        _logger.Log($"[BUILD.COMPLETE] site=({site.Position.X},{site.Position.Y},{site.Z}) kind={kind} consumed={FormatDict(construction.MaterialsRequired)} set-terrain removedSite={removed}");
    }

    private void CompletePlaceable(PlaceableInstance site, ulong tick)
    {
        var construction = site.ConstructionSite!;
        var def = _constructions.GetConstruction(construction.TargetId);
        if (def == null)
        {
            _logger.Log($"[BUILD.COMPLETE] ERROR: unknown construction id='{construction.TargetId}'");
            return;
        }

        RemoveConstructionSite(site, tick);

        var instance = PlaceableInstance.CreateFromConstruction(def, site.Position, site.Z, tick);
        PlaceableManager.PlacePlaceable(_world, instance, tick);
        _logger.Log($"[BUILD.COMPLETE] workshop id={def.Id} pos=({site.Position.X},{site.Position.Y},{site.Z}) footprint={def.PlaceableProfile.Footprint.W}x{def.PlaceableProfile.Footprint.D}");

        var rect = new Rectangle(site.Position.X, site.Position.Y, def.PlaceableProfile.Footprint.W, def.PlaceableProfile.Footprint.D);
        _workshopCompletionSink?.NotifyWorkshopComplete(site.Position.X, site.Position.Y, site.Z, rect, def.Id, tick);
    }

    private bool RemoveConstructionSite(PlaceableInstance site, ulong tick)
    {
        try
        {
            return PlaceableManager.RemoveOwnedAt(_world, site.Position, site.Z, tick);
        }
        catch (Exception ex)
        {
            _logger.Log($"[BUILD.COMPLETE] WARNING: failed to remove construction site at ({site.Position.X},{site.Position.Y},{site.Z}): {ex.Message}");
            return false;
        }
    }

    private static string FormatDict(IReadOnlyDictionary<string, int> values)
    {
        return "{" + string.Join(", ", values.Select(kv => $"{kv.Key}:{kv.Value}")) + "}";
    }
}
