using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class TileInspectionSnapshotBuilder
{
    private static SimulationTileInspectionData CreateEmpty(Point tileWorldPosition, int tileZ)
    {
        return new SimulationTileInspectionData(
            HasTile: false,
            X: tileWorldPosition.X,
            Y: tileWorldPosition.Y,
            Z: tileZ,
            TerrainKind: string.Empty,
            GeologyLabel: string.Empty,
            IsNatural: false,
            IsModifiable: false,
            HasMud: false,
            HasGrass: false,
            HasSnow: false,
            Fertility: 0,
            FluidKind: string.Empty,
            FluidDepth: 0,
            IsRevealed: false,
            IsForbidden: false,
            TrafficLevel: 0,
            HasBlood: false,
            Items: Array.Empty<TileInspectionItemView>(),
            Creatures: Array.Empty<TileInspectionCreatureView>());
    }
}
