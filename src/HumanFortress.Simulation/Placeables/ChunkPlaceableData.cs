using System;
using System.Collections.Generic;
using System.Linq;
using HumanFortress.Simulation.World;

namespace HumanFortress.Simulation.Placeables;

/// <summary>
/// Chunk-level placeable storage (Layer 2: placeables/constructions)
/// Stores placeable instances owned by this chunk.
/// For multi-chunk large placeables (e.g., 5x5 workshop spanning multiple chunks),
/// only the primary chunk owns the instance; other chunks store external references.
///
/// NOTE: Will be serialized to chunk saves when save system is implemented.
/// Currently stored in memory only via Chunk._placeableData.
/// </summary>
internal sealed partial class ChunkPlaceableData
{
    // Owned placeable instances (key = local cell index)
    // Local cell index = y * Chunk.SIZE_XY + x
    private readonly Dictionary<int, PlaceableInstance> _ownedPlaceables = new();

    // External references to placeables owned by other chunks
    // (for multi-chunk large structures, key = local cell index, value = placeable GUID)
    private readonly Dictionary<int, Guid> _externalRefs = new();

    /// <summary>
    /// Get owned placeable at local cell index.
    /// External references are resolved through PlaceableManager.TryGetPlaceableAt.
    /// </summary>
    internal PlaceableInstance? GetPlaceableAt(int localIndex)
    {
        if (_ownedPlaceables.TryGetValue(localIndex, out var placeable))
            return placeable;

        return null;
    }

    /// <summary>
    /// Add owned placeable at local cell index.
    /// Caller MUST call SyncToFurnitureCell and BumpConnectivityVersion separately.
    /// </summary>
    internal void AddPlaceable(int localIndex, PlaceableInstance placeable)
    {
        _ownedPlaceables[localIndex] = placeable;
    }

    /// <summary>
    /// Remove owned placeable at local cell index
    /// </summary>
    internal bool RemovePlaceable(int localIndex)
    {
        return _ownedPlaceables.Remove(localIndex);
    }

    /// <summary>
    /// Add external reference to placeable owned by another chunk
    /// </summary>
    internal void AddExternalRef(int localIndex, Guid placeableGuid)
    {
        _externalRefs[localIndex] = placeableGuid;
    }

    /// <summary>
    /// Remove external reference at local cell index
    /// </summary>
    internal bool RemoveExternalRef(int localIndex)
    {
        return _externalRefs.Remove(localIndex);
    }

    /// <summary>
    /// Try get the external owner GUID stored at a local cell.
    /// World-level resolution is owned by PlaceableManager so chunk data does not
    /// depend on manager/global world state.
    /// </summary>
    internal bool TryGetExternalRefAt(int localIndex, out Guid placeableGuid)
    {
        return _externalRefs.TryGetValue(localIndex, out placeableGuid);
    }

    /// <summary>
    /// Get external references with their local storage cell in stable order.
    /// </summary>
    internal IReadOnlyList<(int LocalIndex, Guid PlaceableGuid)> GetExternalReferenceSnapshot()
    {
        return _externalRefs
            .OrderBy(static entry => entry.Key)
            .Select(static entry => (entry.Key, entry.Value))
            .ToArray();
    }

    /// <summary>
    /// Get all owned placeables
    /// </summary>
    internal IEnumerable<PlaceableInstance> GetAllOwnedPlaceables()
    {
        return GetOwnedPlaceableSnapshot()
            .Select(static entry => entry.Placeable)
            .ToArray();
    }

    /// <summary>
    /// Get owned placeables with their authoritative local storage cell.
    /// </summary>
    internal IReadOnlyList<(int LocalIndex, PlaceableInstance Placeable)> GetOwnedPlaceableSnapshot()
    {
        return _ownedPlaceables
            .OrderBy(static entry => entry.Key)
            .Select(static entry => (entry.Key, entry.Value))
            .ToArray();
    }

    /// <summary>
    /// Try get owned placeable at local cell index.
    /// </summary>
    internal bool TryGetOwnedAt(int localIndex, out PlaceableInstance placeable)
    {
        return _ownedPlaceables.TryGetValue(localIndex, out placeable!);
    }

    /// <summary>
    /// Check if cell has any placeable (owned or external ref)
    /// </summary>
    internal bool HasPlaceableAt(int localIndex)
    {
        return _ownedPlaceables.ContainsKey(localIndex) || _externalRefs.ContainsKey(localIndex);
    }

    /// <summary>
    /// Clear all placeable data
    /// </summary>
    internal void Clear()
    {
        _ownedPlaceables.Clear();
        _externalRefs.Clear();
    }

    internal int OwnedCount => _ownedPlaceables.Count;
    internal int ExternalRefCount => _externalRefs.Count;
}
