using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Simulation.Topology;
using HumanFortress.Simulation.World;
using WorldClass = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Placeables;

internal sealed partial class PlaceableManager
{
    /// <summary>
    /// Unique live owner for door state mutation. Door state and derived blocker
    /// occupancy are committed together through the same topology transaction.
    /// </summary>
    internal static bool TrySetDoorState(
        WorldClass world,
        Guid placeableGuid,
        bool isOpen,
        bool isLocked,
        ulong tick,
        out TopologyChangeDescription? committedChange)
    {
        ArgumentNullException.ThrowIfNull(world);

        lock (world.TopologyLock)
        {
            committedChange = null;
            if (!TryGetUniqueOwnedPlaceableByGuid(world, placeableGuid, out var placeable)
                || placeable == null
                || placeable.Passability != PassabilityMode.Doorway
                || !TryGetOwnedAtAnchor(
                    world,
                    placeable.Position,
                    placeable.Z,
                    out var anchorOwner,
                    out var primaryCell)
                || !ReferenceEquals(anchorOwner, placeable)
                || !TryValidateRemovalFootprint(world, placeable, primaryCell, out var cells))
            {
                return false;
            }

            var oldState = placeable.DoorState ?? new DoorState();
            var newState = new DoorState(isOpen, isLocked);
            var topologyChanged = oldState.IsOpen != newState.IsOpen;
            var transaction = new TopologyChangeTransaction(
                world,
                tick,
                TopologyChangeKind.DoorState,
                placeable.Guid,
                applyPreparedWrites: () =>
                {
                    if (topologyChanged)
                    {
                        foreach (var cell in cells)
                        {
                            cell.Chunk.RemoveDerivedFurniture(cell.LocalIndex, placeable.Guid, tick);
                            if (!cell.Chunk.TryPlaceDerivedFurniture(
                                    cell.LocalIndex,
                                    new FurnitureRef(placeable.Guid),
                                    isBlocker: !newState.IsOpen,
                                    tick))
                            {
                                return false;
                            }
                        }
                    }

                    placeable.ApplyCommittedDoorState(newState);
                    return true;
                });

            if (topologyChanged)
                MarkFootprintCellsDirtyForChunk(transaction, cells);
            committedChange = transaction.Commit();
            return true;
        }
    }
}
