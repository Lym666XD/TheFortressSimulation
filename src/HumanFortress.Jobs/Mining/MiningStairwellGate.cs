using HumanFortress.Simulation.Orders;
using HumanFortress.Simulation.Tiles;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Mining;

internal sealed class MiningStairwellGate
{
    private readonly WorldModel _world;
    private readonly MiningDeferredStairwellBuffer _deferredStairwells;
    private readonly IMiningJobLogger _logger;

    internal MiningStairwellGate(WorldModel world, MiningDeferredStairwellBuffer deferredStairwells, IMiningJobLogger? logger)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _deferredStairwells = deferredStairwells ?? throw new ArgumentNullException(nameof(deferredStairwells));
        _logger = logger ?? NullMiningJobLogger.Instance;
    }

    internal bool ShouldProcess(in MiningSystem.PlannedDig dig, ulong tick, out bool middleAlreadySatisfied)
    {
        middleAlreadySatisfied = false;
        if (dig.Action != MiningAction.DigStairwell)
        {
            return true;
        }

        if (!CheckAlreadySatisfied(dig, tick, out middleAlreadySatisfied))
        {
            return false;
        }

        if (dig.Segment == MiningSegment.Top)
        {
            return true;
        }

        if (HasVerticalConnection(dig))
        {
            return true;
        }

        _deferredStairwells.Enqueue(dig);
        _logger.Log($"[MINING][{tick}] Stairwell seg={dig.Segment} at ({dig.Cell.X},{dig.Cell.Y},{dig.Z}) id={dig.DesignationId} blocked: no vertical connection; defer");
        return false;
    }

    private bool CheckAlreadySatisfied(in MiningSystem.PlannedDig dig, ulong tick, out bool middleAlreadySatisfied)
    {
        middleAlreadySatisfied = false;
        var tile = _world.GetTile(dig.Cell.X, dig.Cell.Y, dig.Z);
        if (tile == null)
        {
            return true;
        }

        var expected = dig.Segment switch
        {
            MiningSegment.Top => TerrainKind.StairsDown,
            MiningSegment.Middle => TerrainKind.StairsUD,
            MiningSegment.Bottom => TerrainKind.StairsUp,
            _ => tile.Value.Kind
        };
        _logger.Log($"[MINING][{tick}] Check skip: seg={dig.Segment} at ({dig.Cell.X},{dig.Cell.Y},{dig.Z}) current={tile.Value.Kind} expected={expected}");
        if (tile.Value.Kind != expected)
        {
            return true;
        }

        if (dig.Segment == MiningSegment.Middle && expected == TerrainKind.StairsUD)
        {
            middleAlreadySatisfied = true;
            return true;
        }

        _logger.Log($"[MINING][{tick}] Skip stairwell seg={dig.Segment} already {expected} at ({dig.Cell.X},{dig.Cell.Y},{dig.Z}) id={dig.DesignationId}");
        return false;
    }

    private bool HasVerticalConnection(in MiningSystem.PlannedDig dig)
    {
        var tileAbove = _world.GetTile(dig.Cell.X, dig.Cell.Y, dig.Z + 1);
        if (tileAbove != null)
        {
            var kindAbove = tileAbove.Value.Kind;
            if (kindAbove == TerrainKind.StairsDown || kindAbove == TerrainKind.StairsUD)
            {
                return true;
            }
        }

        var tileBelow = _world.GetTile(dig.Cell.X, dig.Cell.Y, dig.Z - 1);
        if (tileBelow != null)
        {
            var kindBelow = tileBelow.Value.Kind;
            if (kindBelow == TerrainKind.StairsUp || kindBelow == TerrainKind.StairsUD)
            {
                return true;
            }
        }

        return false;
    }
}
