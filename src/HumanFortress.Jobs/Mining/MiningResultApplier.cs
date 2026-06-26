using HumanFortress.Simulation.Orders;
using SimTerrainKind = HumanFortress.Simulation.Tiles.TerrainKind;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Mining;

internal sealed class MiningResultApplier
{
    private readonly WorldModel _world;
    private readonly IMiningDiffEmitter _diffEmitter;
    private readonly IMiningDropResolver _dropResolver;

    internal MiningResultApplier(WorldModel world, IMiningDiffEmitter diffEmitter, IMiningDropResolver dropResolver)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _diffEmitter = diffEmitter ?? throw new ArgumentNullException(nameof(diffEmitter));
        _dropResolver = dropResolver ?? throw new ArgumentNullException(nameof(dropResolver));
    }

    internal void Apply(ActiveMiningJob job)
    {
        var here = _world.GetTile(job.Target.X, job.Target.Y, job.Z);
        SimTerrainKind? kindHere = here != null ? here.Value.Kind : null;
        switch (job.Action)
        {
            case MiningAction.Dig:
                SetTerrainAndDrops(job, SimTerrainKind.OpenWithFloor, job.GeologyHandle, SimTerrainKind.SolidWall);
                break;
            case MiningAction.DigRamp:
                ApplyRamp(job, kindHere);
                break;
            case MiningAction.DigChannel:
                ApplyChannel(job);
                break;
            case MiningAction.DigStairwell:
                ApplyStairwell(job, kindHere);
                break;
            default:
                SetTerrainAndDrops(job, SimTerrainKind.OpenWithFloor, job.GeologyHandle, SimTerrainKind.SolidWall);
                break;
        }
    }

    private void ApplyRamp(ActiveMiningJob job, SimTerrainKind? kindHere)
    {
        if (kindHere == SimTerrainKind.SolidWall)
        {
            SetTerrainAndDrops(job, SimTerrainKind.Ramp, job.GeologyHandle, SimTerrainKind.Ramp);
            if (job.Z + 1 < _world.MaxZ)
            {
                var above = _world.GetTile(job.Target.X, job.Target.Y, job.Z + 1);
                if (above != null && above.Value.Kind == SimTerrainKind.OpenWithFloor)
                {
                    _diffEmitter.SetTerrain(job.Target, job.Z + 1, SimTerrainKind.OpenNoFloor, above.Value.GeoMatId);
                }
            }
        }
        else
        {
            SetTerrainAndDrops(job, SimTerrainKind.OpenWithFloor, job.GeologyHandle, SimTerrainKind.SolidWall);
        }
    }

    private void ApplyChannel(ActiveMiningJob job)
    {
        ushort airGeo = _dropResolver.ResolveAirGeologyHandle();
        _diffEmitter.SetTerrain(job.Target, job.Z, SimTerrainKind.OpenNoFloor, airGeo);
        if (job.Z <= 0)
        {
            return;
        }

        var below = _world.GetTile(job.Target.X, job.Target.Y, job.Z - 1);
        if (below == null || below.Value.Kind != SimTerrainKind.SolidWall)
        {
            return;
        }

        _diffEmitter.SetTerrain(job.Target, job.Z - 1, SimTerrainKind.Ramp, below.Value.GeoMatId);
        EmitDrops(job.Target, job.Z, below.Value.GeoMatId, SimTerrainKind.Ramp);
    }

    private void ApplyStairwell(ActiveMiningJob job, SimTerrainKind? kindHere)
    {
        var targetKind = job.Segment switch
        {
            MiningSegment.Top => SimTerrainKind.StairsDown,
            MiningSegment.Middle => SimTerrainKind.StairsUD,
            MiningSegment.Bottom => SimTerrainKind.StairsUp,
            _ => SimTerrainKind.StairsUD
        };
        _diffEmitter.SetTerrain(job.Target, job.Z, targetKind, job.GeologyHandle);
        if (kindHere == SimTerrainKind.SolidWall)
        {
            EmitDrops(job.Target, job.Z, job.GeologyHandle, SimTerrainKind.SolidWall);
        }
    }

    private void SetTerrainAndDrops(ActiveMiningJob job, SimTerrainKind newKind, ushort geologyHandle, SimTerrainKind dropTerrainKind)
    {
        _diffEmitter.SetTerrain(job.Target, job.Z, newKind, geologyHandle);
        EmitDrops(job.Target, job.Z, geologyHandle, dropTerrainKind);
    }

    private void EmitDrops(SadRogue.Primitives.Point target, int z, ushort geologyHandle, SimTerrainKind terrainKind)
    {
        foreach (var (dropId, qty) in _dropResolver.ChooseDropsFor(geologyHandle, terrainKind))
        {
            if (!string.IsNullOrEmpty(dropId) && qty > 0)
            {
                _diffEmitter.AddItem(target, z, dropId, qty);
            }
        }
    }

}
