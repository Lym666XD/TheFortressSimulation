namespace HumanFortress.Simulation.Placeables;

/// <summary>
/// Furniture cells are a derived cache of authoritative placeable owner/ref
/// rows. Mutation is intentionally owned by TopologyChangeTransaction through
/// PlaceableManager; ChunkPlaceableData exposes no independent sync operation.
/// </summary>
internal sealed partial class ChunkPlaceableData
{
}
