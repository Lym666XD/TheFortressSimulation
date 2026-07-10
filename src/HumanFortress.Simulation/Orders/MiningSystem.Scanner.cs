using HumanFortress.Simulation.Tiles;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Orders;

internal sealed partial class MiningSystem
{
    private bool TryNextDigFrom(ref ActiveDesignation designation, out PlannedDig plannedDig)
    {
        int scannedCells = 0;
        int rejectedByFilter = 0;
        bool isStairwell = designation.Action == MiningAction.DigStairwell;

        while (true)
        {
            if (IsOutOfZRange(designation, isStairwell))
                break;

            for (; designation.CurY <= designation.Rect.MaxExtentY; designation.CurY++, designation.CurX = designation.Rect.X)
            {
                for (; designation.CurX <= designation.Rect.MaxExtentX; designation.CurX++)
                {
                    if (IsCanceled(designation.CurX, designation.CurY, designation.CurZ))
                        continue;

                    var tileOpt = _world.GetTile(designation.CurX, designation.CurY, designation.CurZ);
                    if (tileOpt == null)
                        continue;

                    var tile = tileOpt.Value;
                    scannedCells++;
                    var cell = new Point(designation.CurX, designation.CurY);
                    ushort geologyHandle = tile.GeoMatId;
                    byte terrainKind = (byte)tile.Kind;

                    switch (designation.Action)
                    {
                        case MiningAction.Dig:
                            if (tile.Kind != TerrainKind.SolidWall && tile.Kind != TerrainKind.Ramp)
                                break;
                            if (tile.Kind == TerrainKind.Ramp && !HasStandableAdjacency(designation.CurX, designation.CurY, designation.CurZ))
                                break;
                            plannedDig = new PlannedDig(cell, designation.CurZ, geologyHandle, terrainKind, designation.Priority, SeedFrom(designation.CurX, designation.CurY, designation.CurZ), MiningAction.Dig, MiningSegment.None, designation.Id);
                            AdvanceCursor(ref designation);
                            return true;
                        case MiningAction.DigRamp:
                            if (tile.Kind != TerrainKind.SolidWall)
                                break;
                            plannedDig = new PlannedDig(cell, designation.CurZ, geologyHandle, terrainKind, designation.Priority, SeedFrom(designation.CurX, designation.CurY, designation.CurZ), MiningAction.DigRamp, MiningSegment.None, designation.Id);
                            AdvanceCursor(ref designation);
                            return true;
                        case MiningAction.DigChannel:
                            if (tile.Kind == TerrainKind.OpenNoFloor)
                                break;
                            plannedDig = new PlannedDig(cell, designation.CurZ, geologyHandle, terrainKind, designation.Priority, SeedFrom(designation.CurX, designation.CurY, designation.CurZ), MiningAction.DigChannel, MiningSegment.None, designation.Id);
                            AdvanceCursor(ref designation);
                            return true;
                        case MiningAction.DigStairwell:
                            if (designation.ZMin == designation.ZMax)
                                break;
                            if (scannedCells <= 10)
                            {
                                Log($"[MINING][PLAN] Stairwell id={designation.Id} scan cell ({designation.CurX},{designation.CurY},{designation.CurZ}) kind={tile.Kind} ZMin={designation.ZMin} ZMax={designation.ZMax}");
                            }
                            if (tile.Kind == TerrainKind.OpenNoFloor)
                            {
                                if (scannedCells <= 5)
                                {
                                    Log($"[MINING][PLAN] Stairwell id={designation.Id} reject cell ({designation.CurX},{designation.CurY},{designation.CurZ}) kind={tile.Kind} (can't dig stairs in air)");
                                }
                                rejectedByFilter++;
                                break;
                            }

                            var segment = designation.CurZ == designation.ZMax
                                ? MiningSegment.Top
                                : designation.CurZ == designation.ZMin
                                    ? MiningSegment.Bottom
                                    : MiningSegment.Middle;
                            plannedDig = new PlannedDig(cell, designation.CurZ, geologyHandle, terrainKind, designation.Priority, SeedFrom(designation.CurX, designation.CurY, designation.CurZ), MiningAction.DigStairwell, segment, designation.Id);
                            Log($"[MINING][PLAN] Stairwell id={designation.Id} PRODUCE PlannedDig at ({designation.CurX},{designation.CurY},{designation.CurZ}) seg={segment}");
                            AdvanceCursor(ref designation);
                            return true;
                        default:
                            break;
                    }
                }
            }

            if (designation.CurY > designation.Rect.MaxExtentY)
            {
                designation.CurY = designation.Rect.Y;
                designation.CurX = designation.Rect.X;
                if (isStairwell)
                    designation.CurZ--;
                else
                    designation.CurZ++;
            }
        }

        if (rejectedByFilter > 0)
        {
            Log($"[MINING][PLAN] Stairwell id={designation.Id} done: scanned={scannedCells} rejected={rejectedByFilter} (all non-SolidWall)");
        }
        designation.MarkDone();
        plannedDig = default;
        return false;
    }

    private static bool IsOutOfZRange(ActiveDesignation designation, bool isStairwell)
    {
        return isStairwell
            ? designation.CurZ < designation.ZMin
            : designation.CurZ > designation.ZMax;
    }

    private static void AdvanceCursor(ref ActiveDesignation designation)
    {
        designation.CurX++;
        if (designation.CurX > designation.Rect.MaxExtentX)
        {
            designation.CurX = designation.Rect.X;
            designation.CurY++;
        }

        if (designation.CurY > designation.Rect.MaxExtentY)
        {
            designation.CurY = designation.Rect.Y;
            if (designation.Action == MiningAction.DigStairwell)
                designation.CurZ--;
            else
                designation.CurZ++;
        }

        if (designation.Action == MiningAction.DigStairwell)
        {
            if (designation.CurZ < designation.ZMin)
                designation.MarkDone();
        }
        else
        {
            if (designation.CurZ > designation.ZMax)
                designation.MarkDone();
        }
    }
}
