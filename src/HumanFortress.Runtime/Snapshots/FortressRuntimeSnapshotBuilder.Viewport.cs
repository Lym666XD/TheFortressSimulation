using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class FortressRuntimeSnapshotBuilder
{
    internal static SimulationMapViewportData BuildMapViewportSnapshot(
        World? world,
        IRuntimeGeologyCatalog? geologyCatalog,
        RuntimeViewportGeometry viewport,
        Point cursorPosition,
        int cursorGlyph)
    {
        return MapViewportSnapshotBuilder.Build(
            world,
            geologyCatalog,
            viewport,
            cursorPosition,
            cursorGlyph);
    }
}
