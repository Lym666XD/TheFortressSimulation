using System;
using System.Collections.Generic;
using System.Linq;
using HumanFortress.Core.Random;
using HumanFortress.Contracts.Simulation.Creatures;
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
internal sealed class CreatureManager : ICreatureDefinitionCatalog
{
    private const ulong CreatureInstanceGuidScope = 0x4352454154555245UL;

    // Static definition catalog (loaded at startup, swapped as an immutable snapshot on reload)
    private CreatureDefinitionCatalogStore _definitionCatalog = CreatureDefinitionCatalogStore.Empty;

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

    public int DefinitionCount => _definitionCatalog.DefinitionCount;
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
        ArgumentNullException.ThrowIfNull(world);

        _world = world;
    }

    /// <summary>
    /// Replace the static creature definition catalog with an already-loaded immutable snapshot.
    /// </summary>
    public void SetDefinitionCatalog(CreatureDefinitionCatalogStore catalog)
    {
        _definitionCatalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
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
            Emit($"[CreatureManager] Definitions loaded: {_definitionCatalog.DefinitionCount}");

            // Validate definition exists
            var def = _definitionCatalog.GetDefinition(creatureId);
            if (def == null)
            {
                Emit($"[CreatureManager] ERROR: Unknown creature '{creatureId}'");
                Emit($"[CreatureManager] Available creatures: {string.Join(", ", _definitionCatalog.GetAllDefinitions().Select(definition => definition.Id).Take(5))}");
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
        return _definitionCatalog.GetAllDefinitions();
    }

    /// <summary>
    /// Get creature definitions by tag
    /// </summary>
    public IEnumerable<CreatureDefinition> GetByTag(string tag)
    {
        return _definitionCatalog.GetByTag(tag);
    }

    /// <summary>
    /// Get definition by ID
    /// </summary>
    public CreatureDefinition? GetDefinition(string id)
    {
        return _definitionCatalog.GetDefinition(id);
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
