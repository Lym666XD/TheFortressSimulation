using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SadRogue.Primitives;
using HumanFortress.Simulation.World;
using HumanFortress.Simulation.Tiles;
using HumanFortress.Core.Content.Registry;

namespace HumanFortress.Simulation.Items;

/// <summary>
/// Global manager for item definitions and runtime instances.
/// Thread-safe for concurrent reads; writes use locks.
/// Follows data-driven principles per ITEMS_SPEC.md and UPDATE_ORDER.md
/// </summary>
public sealed class ItemManager
{
    // Registry (loaded at startup, read-only after)
    private readonly Dictionary<string, ItemDefinition> _definitions = new();
    private readonly Dictionary<string, List<string>> _kindIndex = new(); // kind -> [item_ids]
    private readonly Dictionary<string, List<string>> _tagIndex = new();  // tag -> [item_ids]

    // Runtime instances (modified during gameplay)
    private readonly Dictionary<Guid, ItemInstance> _instances = new();
    private readonly object _instanceLock = new();
    // Position index for fast per-tile queries
    private readonly Dictionary<(int X,int Y,int Z), List<Guid>> _posIndex = new();

    // Dependencies
    private HumanFortress.Simulation.World.World? _world;
    private ContentRegistry? _contentRegistry;

    /// <summary>
    /// Optional logging callback (set by App layer to write to fortress_debug.log)
    /// </summary>
    public static Action<string>? LogCallback { get; set; }

    public int DefinitionCount => _definitions.Count;
    public int InstanceCount
    {
        get
        {
            lock (_instanceLock)
            {
                return _instances.Count;
            }
        }
    }

    private static (int,int,int) KeyFor(Point pos, int z) => (pos.X, pos.Y, z);
    private void IndexAdd(Guid id, Point pos, int z)
    {
        var key = KeyFor(pos, z);
        if (!_posIndex.TryGetValue(key, out var list)) { list = new List<Guid>(); _posIndex[key] = list; }
        list.Add(id);
    }
    private void IndexRemove(Guid id, Point pos, int z)
    {
        var key = KeyFor(pos, z);
        if (_posIndex.TryGetValue(key, out var list))
        {
            for (int i = list.Count - 1; i >= 0; i--) if (list[i] == id) list.RemoveAt(i);
            if (list.Count == 0) _posIndex.Remove(key);
        }
    }

    /// <summary>
    /// Update item position and maintain position index.
    /// </summary>
    public void UpdateItemPosition(Guid id, Point oldPos, int oldZ, Point newPos, int newZ)
    {
        lock (_instanceLock)
        {
            if (_instances.TryGetValue(id, out var inst))
            {
                IndexRemove(id, oldPos, oldZ);
                inst.Position = newPos;
                inst.Z = newZ;
                IndexAdd(id, newPos, newZ);
            }
        }
    }

    /// <summary>
    /// Merge stacks at a given world position (post-move consolidation).
    /// Current policy: same DefinitionId at same (x,y,z) merge by increasing first instance's StackCount,
    /// deleting the redundant instances. Returns number of instances removed.
    /// </summary>
    public int MergeStacksAt(Point worldPos, int z)
    {
        lock (_instanceLock)
        {
            var key = KeyFor(worldPos, z);
            if (!_posIndex.TryGetValue(key, out var ids) || ids.Count <= 1)
                return 0;

            var byDef = new Dictionary<string, List<Guid>>();
            foreach (var gid in ids)
            {
                if (!_instances.TryGetValue(gid, out var inst)) continue;
                if (inst.IsCarried) continue;
                if (!byDef.TryGetValue(inst.DefinitionId, out var list))
                {
                    list = new List<Guid>();
                    byDef[inst.DefinitionId] = list;
                }
                list.Add(gid);
            }

            int removed = 0;
            foreach (var kv in byDef)
            {
                var list = kv.Value;
                if (list.Count <= 1) continue;
                var targetId = list[0];
                if (!_instances.TryGetValue(targetId, out var target)) continue;
                int sum = target.StackCount;
                for (int i = 1; i < list.Count; i++)
                {
                    var gid = list[i];
                    if (_instances.TryGetValue(gid, out var other))
                    {
                        sum += other.StackCount;
                        _instances.Remove(gid);
                        removed++;
                    }
                }
                target.StackCount = sum;
                _posIndex[key] = new List<Guid> { targetId };
                string msg = $"[ItemManager] MERGE: Consolidated {list.Count} stacks of '{target.DefinitionId}' at ({worldPos.X},{worldPos.Y},{z}) -> qty={target.StackCount}";
                LogCallback?.Invoke(msg);
                Console.WriteLine(msg);
            }
            return removed;
        }
    }

    /// <summary>
    /// Set dependencies (called after initialization)
    /// </summary>
    public void SetDependencies(HumanFortress.Simulation.World.World world, ContentRegistry contentRegistry)
    {
        _world = world;
        _contentRegistry = contentRegistry;
    }

    /// <summary>
    /// Load item definitions from data files.
    /// Each file is wrapped in try-catch; errors are logged but don't stop loading.
    /// </summary>
    public void LoadDefinitions(string dataPath)
    {
        var itemsPath = Path.Combine(dataPath, "items");
        if (!Directory.Exists(itemsPath))
        {
            Console.WriteLine($"[ItemManager] WARNING: Items directory not found: {itemsPath}");
            return;
        }

        var files = Directory.GetFiles(itemsPath, "*.json");
        int loaded = 0;
        int failed = 0;

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };

                var defs = JsonSerializer.Deserialize<List<ItemDefinition>>(json, options);
                if (defs == null) continue;

                foreach (var def in defs)
                {
                    try
                    {
                        ValidateDefinition(def);
                        _definitions[def.Id] = def;
                        IndexByKind(def);
                        IndexByTags(def);
                        loaded++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ItemManager] ERROR: Invalid definition '{def.Id}' in {Path.GetFileName(file)}: {ex.Message}");
                        failed++;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ItemManager] ERROR: Failed to load {Path.GetFileName(file)}: {ex.Message}");
                failed++;
            }
        }

        Console.WriteLine($"[ItemManager] Loaded {loaded} item definitions from {files.Length} files ({failed} errors)");
        Console.WriteLine($"[ItemManager] Indexed {_kindIndex.Count} kinds: {string.Join(", ", _kindIndex.Keys)}");
    }

    /// <summary>
    /// Validate item definition (basic checks)
    /// </summary>
    private void ValidateDefinition(ItemDefinition def)
    {
        if (string.IsNullOrWhiteSpace(def.Id))
            throw new ArgumentException("Item ID cannot be empty");

        if (string.IsNullOrWhiteSpace(def.Name))
            throw new ArgumentException($"Item '{def.Id}' has no name");

        // Validate kind is valid
        var validKinds = new[] { "resource", "weapon", "armor", "tool", "container", "consumable", "placeable" };
        if (!validKinds.Contains(def.Kind.ToLower()))
            throw new ArgumentException($"Item '{def.Id}' has invalid kind: {def.Kind}");

        // TODO: Validate fixed_material against MaterialRegistry when available
        if (_contentRegistry != null && !string.IsNullOrWhiteSpace(def.FixedMaterial))
        {
            // Basic check - full validation would use ContentRegistry.Materials
            // For now just log a warning if material looks suspicious
            if (!def.FixedMaterial.StartsWith("core_mat_"))
            {
                Console.WriteLine($"[ItemManager] WARNING: Item '{def.Id}' has unusual material: {def.FixedMaterial}");
            }
        }
    }

    /// <summary>
    /// Build kind index for category queries
    /// </summary>
    private void IndexByKind(ItemDefinition def)
    {
        var kind = def.Kind.ToLower();
        if (!_kindIndex.ContainsKey(kind))
            _kindIndex[kind] = new List<string>();

        _kindIndex[kind].Add(def.Id);
    }

    /// <summary>
    /// Build tag index for fast queries
    /// </summary>
    private void IndexByTags(ItemDefinition def)
    {
        foreach (var tag in def.Tags)
        {
            if (!_tagIndex.ContainsKey(tag))
                _tagIndex[tag] = new List<string>();

            _tagIndex[tag].Add(def.Id);
        }
    }

    /// <summary>
    /// Spawn an item at the specified world position.
    /// Returns item GUID on success, null on failure.
    /// TODO: Use Diff-Log instead of direct Chunk write (per UPDATE_ORDER.md)
    /// TODO: Implement stack merging when spawning stackable items
    /// </summary>
    public Guid? SpawnItem(string itemId, Point worldPos, int z, int quantity = 1, ulong currentTick = 0)
    {
        try
        {
            Console.WriteLine($"[ItemManager] SpawnItem called: id={itemId}, pos=({worldPos.X},{worldPos.Y},{z}), qty={quantity}");
            Console.WriteLine($"[ItemManager] Definitions loaded: {_definitions.Count}");

            // Validate definition exists
            if (!_definitions.TryGetValue(itemId, out var def))
            {
                Console.WriteLine($"[ItemManager] ERROR: Unknown item '{itemId}'");
                Console.WriteLine($"[ItemManager] Available items: {string.Join(", ", _definitions.Keys.Take(5))}");
                return null;
            }

            // Validate world is set
            if (_world == null)
            {
                Console.WriteLine($"[ItemManager] ERROR: World not set");
                return null;
            }

            // Calculate chunk and local position
            int chunkX = worldPos.X / 32;
            int chunkY = worldPos.Y / 32;
            int localX = worldPos.X % 32;
            int localY = worldPos.Y % 32;

            Console.WriteLine($"[ItemManager] Chunk coords: ({chunkX},{chunkY},{z}), Local: ({localX},{localY})");

            var chunkKey = new ChunkKey(chunkX, chunkY, z);
            var chunk = _world.GetChunk(chunkKey);

            if (chunk == null)
            {
                Console.WriteLine($"[ItemManager] ERROR: Chunk not found at ({chunkX},{chunkY},{z})");
                return null;
            }

            Console.WriteLine($"[ItemManager] Chunk found, getting tile...");

            // Validate tile is walkable (OpenWithFloor)
            var tile = chunk.GetTile(localX, localY);
            Console.WriteLine($"[ItemManager] Tile kind: {tile.Kind}");

            if (tile.Kind != TerrainKind.OpenWithFloor)
            {
                Console.WriteLine($"[ItemManager] ERROR: Tile at ({worldPos.X},{worldPos.Y},{z}) is not walkable (kind={tile.Kind})");
                return null;
            }

            // Check for existing stacks and merge if stackable (L5 layer)
            lock (_instanceLock)
            {
                // Find existing items at same position with same itemId
                var existingAtPos = _instances.Values
                    .Where(i => i.Position.X == worldPos.X && i.Position.Y == worldPos.Y && i.Z == z && i.DefinitionId == itemId)
                    .ToList();

                if (existingAtPos.Count > 0)
                {
                    // Stack with first existing item
                    var existingItem = existingAtPos[0];
                    existingItem.StackCount += quantity;
                    string stackMsg = $"[ItemManager] SUCCESS: Stacked '{def.Name}' +{quantity} onto existing stack (guid={existingItem.Guid}, new qty={existingItem.StackCount}) at ({worldPos.X},{worldPos.Y},{z})";
                    LogCallback?.Invoke(stackMsg);
                    Console.WriteLine(stackMsg);
                    return existingItem.Guid;
                }

                // Extra diagnostics: if no stack match but there are other items here, log what's present
                var anyAtPos = _instances.Values
                    .Where(i => i.Position.X == worldPos.X && i.Position.Y == worldPos.Y && i.Z == z)
                    .ToList();
                if (anyAtPos.Count > 0)
                {
                    var byId = anyAtPos
                        .GroupBy(i => i.DefinitionId)
                        .Select(g => $"{g.Key}*{g.Sum(it => it.StackCount)}")
                        .ToList();
                    string diag = $"[ItemManager] STACK-CHECK: No stack match for id={itemId} at ({worldPos.X},{worldPos.Y},{z}); present={{{string.Join(", ", byId)}}}";
                    LogCallback?.Invoke(diag);
                    Console.WriteLine(diag);
                }

                // Create new instance
                var guid = Guid.NewGuid();
                var instance = new ItemInstance(guid, itemId, worldPos, z, quantity, currentTick);
                instance.MaterialId = def.FixedMaterial;
                _instances[guid] = instance;
                IndexAdd(guid, worldPos, z);

                string spawnMsg = $"[ItemManager] SUCCESS: Spawned '{def.Name}' (id={itemId}, guid={guid}, qty={quantity}) at ({worldPos.X},{worldPos.Y},{z})";
                LogCallback?.Invoke(spawnMsg);
                Console.WriteLine(spawnMsg);
                return guid;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ItemManager] EXCEPTION: Failed to spawn '{itemId}' at ({worldPos.X},{worldPos.Y},{z})");
            Console.WriteLine($"[ItemManager] Exception: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[ItemManager] StackTrace: {ex.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// Get all item definitions
    /// </summary>
    public IEnumerable<ItemDefinition> GetAllDefinitions()
    {
        return _definitions.Values;
    }

    /// <summary>
    /// Get item definitions by kind (resource/weapon/armor/tool/container/consumable)
    /// </summary>
    public IEnumerable<ItemDefinition> GetByKind(string kind)
    {
        var normalizedKind = kind.ToLower();
        if (!_kindIndex.TryGetValue(normalizedKind, out var ids))
            return Enumerable.Empty<ItemDefinition>();

        return ids.Select(id => _definitions[id]);
    }

    /// <summary>
    /// Get available kinds (for UI category display)
    /// </summary>
    public IEnumerable<string> GetAvailableKinds()
    {
        return _kindIndex.Keys.OrderBy(k => k);
    }

    /// <summary>
    /// Get item definitions by tag
    /// </summary>
    public IEnumerable<ItemDefinition> GetByTag(string tag)
    {
        if (!_tagIndex.TryGetValue(tag, out var ids))
            return Enumerable.Empty<ItemDefinition>();

        return ids.Select(id => _definitions[id]);
    }

    /// <summary>
    /// Get definition by ID
    /// </summary>
    public ItemDefinition? GetDefinition(string id)
    {
        return _definitions.GetValueOrDefault(id);
    }

    /// <summary>
    /// Get instance by GUID
    /// </summary>
    public ItemInstance? GetInstance(Guid guid)
    {
        lock (_instanceLock)
        {
            return _instances.GetValueOrDefault(guid);
        }
    }

    /// <summary>
    /// Get all instances (creates a snapshot for thread safety)
    /// </summary>
    public IEnumerable<ItemInstance> GetAllInstances()
    {
        lock (_instanceLock)
        {
            return _instances.Values.ToList();
        }
    }
}
