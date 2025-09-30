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

    // Dependencies
    private HumanFortress.Simulation.World.World? _world;
    private ContentRegistry? _contentRegistry;

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

            // Create instance
            var guid = Guid.NewGuid();
            var instance = new ItemInstance(guid, itemId, worldPos, z, quantity, currentTick);
            instance.MaterialId = def.FixedMaterial;

            lock (_instanceLock)
            {
                _instances[guid] = instance;
            }

            // TODO: Write to Chunk L5 layer via Diff-Log (currently just tracking in manager)
            // TODO: Check for existing stacks and merge if stackable
            Console.WriteLine($"[ItemManager] SUCCESS: Spawned '{def.Name}' (id={itemId}, guid={guid}, qty={quantity}) at ({worldPos.X},{worldPos.Y},{z})");

            return guid;
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