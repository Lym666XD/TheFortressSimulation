using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HumanFortress.Core.Random;
using HumanFortress.Simulation.Diagnostics;
using SadRogue.Primitives;
using HumanFortress.Simulation.World;
using HumanFortress.Simulation.Tiles;

namespace HumanFortress.Simulation.Creatures;

/// <summary>
/// Global manager for creature definitions and runtime instances.
/// Thread-safe for concurrent reads; writes use locks.
/// Follows data-driven principles per CREATURE_SPEC.md and UPDATE_ORDER.md
/// </summary>
public sealed class CreatureManager
{
    private const ulong CreatureInstanceGuidScope = 0x4352454154555245UL;

    // Registry (loaded at startup, read-only after)
    private readonly Dictionary<string, CreatureDefinition> _definitions = new();
    private readonly Dictionary<string, List<string>> _tagIndex = new();

    // Runtime instances (modified during gameplay)
    private readonly Dictionary<Guid, CreatureInstance> _instances = new();
    private readonly object _instanceLock = new();
    private ulong _nextInstanceSequence;

    // Dependencies
    private HumanFortress.Simulation.World.World? _world;

    /// <summary>
    /// Optional logging callback set by the app diagnostics layer.
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

    /// <summary>
    /// Set world reference (called after World is created)
    /// </summary>
    public void SetWorld(HumanFortress.Simulation.World.World world)
    {
        _world = world;
    }

    /// <summary>
    /// Load creature definitions from data files.
    /// Each file is wrapped in try-catch; errors are logged but don't stop loading.
    /// </summary>
    public void LoadDefinitions(string dataPath)
    {
        var creaturesPath = Path.Combine(dataPath, "creatures");
        if (!Directory.Exists(creaturesPath))
        {
            Emit($"[CreatureManager] WARNING: Creatures directory not found: {creaturesPath}");
            return;
        }

        var files = Directory.GetFiles(creaturesPath, "*.json");
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

                var defs = JsonSerializer.Deserialize<List<CreatureDefinition>>(json, options);
                if (defs == null) continue;

                foreach (var def in defs)
                {
                    try
                    {
                        ValidateDefinition(def);
                        _definitions[def.Id] = def;
                        IndexByTags(def);
                        loaded++;
                    }
                    catch (Exception ex)
                    {
                        Emit($"[CreatureManager] ERROR: Invalid definition '{def.Id}' in {Path.GetFileName(file)}: {ex.Message}");
                        failed++;
                    }
                }
            }
            catch (Exception ex)
            {
                Emit($"[CreatureManager] ERROR: Failed to load {Path.GetFileName(file)}: {ex.Message}");
                failed++;
            }
        }

        Emit($"[CreatureManager] Loaded {loaded} creature definitions from {files.Length} files ({failed} errors)");
    }

    /// <summary>
    /// Validate creature definition (basic checks)
    /// </summary>
    private void ValidateDefinition(CreatureDefinition def)
    {
        if (string.IsNullOrWhiteSpace(def.Id))
            throw new ArgumentException("Creature ID cannot be empty");

        if (string.IsNullOrWhiteSpace(def.Name))
            throw new ArgumentException($"Creature '{def.Id}' has no name");

        // Validate stats are in reasonable range
        if (def.BaseSpeed <= 0)
            throw new ArgumentException($"Creature '{def.Id}' has invalid speed: {def.BaseSpeed}");

        if (def.BaseStrength < 1 || def.BaseStrength > 100)
            throw new ArgumentException($"Creature '{def.Id}' has invalid strength: {def.BaseStrength}");

        // TODO: Validate body_plan_id against ContentRegistry when CREATURE_SPEC is fully implemented
    }

    /// <summary>
    /// Build tag index for fast queries
    /// </summary>
    private void IndexByTags(CreatureDefinition def)
    {
        foreach (var tag in def.Tags)
        {
            if (!_tagIndex.ContainsKey(tag))
                _tagIndex[tag] = new List<string>();

            _tagIndex[tag].Add(def.Id);
        }
    }

    /// <summary>
    /// Spawn a creature at the specified world position.
    /// Returns creature GUID on success, null on failure.
    /// TODO: Use Diff-Log instead of direct Chunk write (per UPDATE_ORDER.md)
    /// </summary>
    public Guid? SpawnCreature(string creatureId, Point worldPos, int z, string factionId = "neutral", ulong currentTick = 0)
    {
        try
        {
            Emit($"[CreatureManager] SpawnCreature called: id={creatureId}, pos=({worldPos.X},{worldPos.Y},{z})");
            Emit($"[CreatureManager] Definitions loaded: {_definitions.Count}");

            // Validate definition exists
            if (!_definitions.TryGetValue(creatureId, out var def))
            {
                Emit($"[CreatureManager] ERROR: Unknown creature '{creatureId}'");
                Emit($"[CreatureManager] Available creatures: {string.Join(", ", _definitions.Keys.Take(5))}");
                return null;
            }

            // Validate world is set
            if (_world == null)
            {
                Emit("[CreatureManager] ERROR: World not set");
                return null;
            }

            // Calculate chunk and local position
            int chunkX = worldPos.X / 32;
            int chunkY = worldPos.Y / 32;
            int localX = worldPos.X % 32;
            int localY = worldPos.Y % 32;

            Emit($"[CreatureManager] Chunk coords: ({chunkX},{chunkY},{z}), Local: ({localX},{localY})");

            var chunkKey = new ChunkKey(chunkX, chunkY, z);
            var chunk = _world.GetChunk(chunkKey);

            if (chunk == null)
            {
                Emit($"[CreatureManager] ERROR: Chunk not found at ({chunkX},{chunkY},{z})");
                return null;
            }

            Emit("[CreatureManager] Chunk found, getting tile...");

            // Validate tile is walkable (allow floors, ramps, slopes, stairs)
            var tile = chunk.GetTile(localX, localY);
            Emit($"[CreatureManager] Tile kind: {tile.Kind}");

            if (!tile.IsWalkable)
            {
                Emit($"[CreatureManager] ERROR: Tile at ({worldPos.X},{worldPos.Y},{z}) is not walkable (kind={tile.Kind})");
                return null;
            }

            var maxHP = 100; // TODO: Calculate from creature stats
            Guid guid;

            lock (_instanceLock)
            {
                guid = DeterministicGuidGenerator.GenerateFromSequence(CreatureInstanceGuidScope, ++_nextInstanceSequence);
                var instance = new CreatureInstance(guid, creatureId, factionId, worldPos, z, maxHP, currentTick);
                _instances[guid] = instance;
            }

            // TODO: Write to Chunk L6 layer via Diff-Log (currently just tracking in manager)
            Emit($"[CreatureManager] SUCCESS: Spawned '{def.Name}' (id={creatureId}, guid={guid}) at ({worldPos.X},{worldPos.Y},{z})");

            return guid;
        }
        catch (Exception ex)
        {
            Emit($"[CreatureManager] EXCEPTION: Failed to spawn '{creatureId}' at ({worldPos.X},{worldPos.Y},{z})");
            Emit($"[CreatureManager] Exception: {ex.GetType().Name}: {ex.Message}");
            Emit($"[CreatureManager] StackTrace: {ex.StackTrace}");
            return null;
        }
    }

    private static void Emit(string message)
    {
        SimulationDiagnostics.Information(LogCallback, "Simulation.Creatures", message);
    }

    /// <summary>
    /// Get all creature definitions
    /// </summary>
    public IEnumerable<CreatureDefinition> GetAllDefinitions()
    {
        return _definitions.Values;
    }

    /// <summary>
    /// Get creature definitions by tag
    /// </summary>
    public IEnumerable<CreatureDefinition> GetByTag(string tag)
    {
        if (!_tagIndex.TryGetValue(tag, out var ids))
            return Enumerable.Empty<CreatureDefinition>();

        return ids.Select(id => _definitions[id]);
    }

    /// <summary>
    /// Get definition by ID
    /// </summary>
    public CreatureDefinition? GetDefinition(string id)
    {
        return _definitions.GetValueOrDefault(id);
    }

    /// <summary>
    /// Get instance by GUID
    /// </summary>
    public CreatureInstance? GetInstance(Guid guid)
    {
        lock (_instanceLock)
        {
            return _instances.GetValueOrDefault(guid);
        }
    }

    /// <summary>
    /// Get all instances (creates a snapshot for thread safety)
    /// </summary>
    public IEnumerable<CreatureInstance> GetAllInstances()
    {
        lock (_instanceLock)
        {
            return _instances.Values.ToList();
        }
    }
}
