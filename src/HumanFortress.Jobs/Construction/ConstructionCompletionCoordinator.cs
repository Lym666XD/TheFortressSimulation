using HumanFortress.Simulation.Placeables;
using HumanFortress.Simulation.Tiles;

namespace HumanFortress.Jobs.Construction;

internal sealed class ConstructionCompletionCoordinator
{
    private readonly ConstructionMaterialTracker _materials;
    private readonly ConstructionSiteSafety _siteSafety;
    private readonly ConstructionCompletionApplier _completion;
    private readonly IConstructionJobLogger _logger;

    internal ConstructionCompletionCoordinator(
        ConstructionMaterialTracker materials,
        ConstructionSiteSafety siteSafety,
        ConstructionCompletionApplier completion,
        IConstructionJobLogger? logger)
    {
        _materials = materials ?? throw new ArgumentNullException(nameof(materials));
        _siteSafety = siteSafety ?? throw new ArgumentNullException(nameof(siteSafety));
        _completion = completion ?? throw new ArgumentNullException(nameof(completion));
        _logger = logger ?? NullConstructionJobLogger.Instance;
    }

    internal bool TryComplete(PlaceableInstance site, ulong tick)
    {
        var construction = site.ConstructionSite;
        if (construction == null)
        {
            return false;
        }

        if (_siteSafety.IsAnchorOccupied(site))
        {
            bool moved = _siteSafety.TryRelocateAnchorOccupants(site);
            _logger.Log($"[BUILD.EXEC] waiting: occupied site=({site.Position.X},{site.Position.Y},{site.Z}) moved={moved}");
            if (_siteSafety.IsAnchorOccupied(site))
            {
                return false;
            }
        }

        if (ConstructionTargetMapper.IsTerrainTarget(construction.TargetId)
            && ConstructionTargetMapper.ToTerrainKind(construction.TargetId) == TerrainKind.SolidWall)
        {
            _siteSafety.RelocateWallFootprintCreatures(site, tick);
        }

        var toConsume = new Dictionary<string, int>(construction.MaterialsRequired, StringComparer.OrdinalIgnoreCase);
        if (!_materials.TryConsume(site, toConsume))
        {
            return false;
        }

        _siteSafety.MoveResidualItemsOffAnchor(site);
        _completion.Complete(site, tick);

        if (_siteSafety.IsAnchorOccupied(site))
        {
            _siteSafety.TryRelocateAnchorOccupants(site);
        }

        return true;
    }
}
