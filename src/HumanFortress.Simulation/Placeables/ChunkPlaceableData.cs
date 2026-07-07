using System;
using System.Collections.Generic;
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
    /// Get placeable at local cell index (resolves both owned and external)
    /// </summary>
    public PlaceableInstance? GetPlaceableAt(int localIndex)
    {
        if (_ownedPlaceables.TryGetValue(localIndex, out var placeable))
            return placeable;

        // TODO: resolve external refs via PlaceableManager when implemented
        // if (_externalRefs.TryGetValue(localIndex, out var guid))
        //     return PlaceableManager.GetPlaceable(guid);

        return null;
    }

    /// <summary>
    /// Add owned placeable at local cell index.
    /// Caller MUST call SyncToFurnitureCell and BumpConnectivityVersion separately.
    /// </summary>
    public void AddPlaceable(int localIndex, PlaceableInstance placeable)
    {
        _ownedPlaceables[localIndex] = placeable;
    }

    /// <summary>
    /// Remove owned placeable at local cell index
    /// </summary>
    public bool RemovePlaceable(int localIndex)
    {
        return _ownedPlaceables.Remove(localIndex);
    }

    /// <summary>
    /// Add external reference to placeable owned by another chunk
    /// </summary>
    public void AddExternalRef(int localIndex, Guid placeableGuid)
    {
        _externalRefs[localIndex] = placeableGuid;
    }

    /// <summary>
    /// Remove external reference at local cell index
    /// </summary>
    public bool RemoveExternalRef(int localIndex)
    {
        return _externalRefs.Remove(localIndex);
    }

    /// <summary>
    /// Get all owned placeables
    /// </summary>
    public IEnumerable<PlaceableInstance> GetAllOwnedPlaceables()
    {
        return _ownedPlaceables.Values;
    }

    /// <summary>
    /// Get owned placeables with their authoritative local storage cell.
    /// Snapshot consumers must sort the result before hashing or persistence.
    /// </summary>
    internal IReadOnlyList<(int LocalIndex, PlaceableInstance Placeable)> GetOwnedPlaceableSnapshot()
    {
        return _ownedPlaceables
            .Select(static entry => (entry.Key, entry.Value))
            .ToArray();
    }

    /// <summary>
    /// Try get owned placeable at local cell index.
    /// </summary>
    public bool TryGetOwnedAt(int localIndex, out PlaceableInstance placeable)
    {
        return _ownedPlaceables.TryGetValue(localIndex, out placeable!);
    }

    /// <summary>
    /// Check if cell has any placeable (owned or external ref)
    /// </summary>
    public bool HasPlaceableAt(int localIndex)
    {
        return _ownedPlaceables.ContainsKey(localIndex) || _externalRefs.ContainsKey(localIndex);
    }

    /// <summary>
    /// Clear all placeable data
    /// </summary>
    public void Clear()
    {
        _ownedPlaceables.Clear();
        _externalRefs.Clear();
    }

    public int OwnedCount => _ownedPlaceables.Count;
    public int ExternalRefCount => _externalRefs.Count;
}
