using System;
using System.Collections.Generic;
using System.Linq;
using HumanFortress.Simulation.Tiles;

namespace HumanFortress.Simulation.World
{
    /// <summary>
    /// Manages chunk lifecycle and LOD transitions per SIM_LOD_POLICY.md.
    /// </summary>
    internal sealed class ChunkLifecycleManager
    {
        private readonly World _world;
        private readonly Dictionary<ChunkKey, ChunkState> _chunkStates = new();
        private readonly Dictionary<ChunkKey, ulong> _heatScores = new();
        private readonly Dictionary<ChunkKey, HashSet<PinReason>> _pinnedChunks = new();
        
        // LOD thresholds from tuning (in chunks)
        private const int R0_ACTIVE = 1;     // L0 radius
        private const int R1_NEAR = 3;       // L1 radius
        private const int R2_FAR = 5;        // L2 radius
        private const int R0_DOWN = R0_ACTIVE + 1; // Hysteresis
        private const int R1_DOWN = R1_NEAR + 1;
        private const int R2_DOWN = R2_FAR + 1;
        
        // Heat thresholds
        private const ulong HEAT_HOT = 100;
        private const ulong HEAT_COLD = 20;
        private const ulong HEAT_DECAY_RATE = 1;
        
        public ChunkLifecycleManager(World world)
        {
            _world = world;
        }
        
        /// <summary>
        /// Update LOD levels based on camera and heat per section 1.
        /// </summary>
        public void UpdateLODLevels(int cameraX, int cameraY, int cameraZ, ulong tick)
        {
            var cameraChunkX = cameraX / Chunk.SIZE_XY;
            var cameraChunkY = cameraY / Chunk.SIZE_XY;
            
            // Get all chunks that should be considered
            var activeChunks = GetChunksInRadius(cameraChunkX, cameraChunkY, cameraZ, R2_FAR + 1);
            
            foreach (var chunkKey in activeChunks)
            {
                var newLod = CalculateLOD(chunkKey, cameraChunkX, cameraChunkY, cameraZ);
                UpdateChunkLOD(chunkKey, newLod, tick);
            }
            
            // Decay heat scores
            DecayHeatScores();
            
            // Process unload queue for L4 chunks
            ProcessUnloadQueue();
        }
        
        private LODLevel CalculateLOD(ChunkKey key, int cameraChunkX, int cameraChunkY, int cameraZ)
        {
            // Calculate distance to camera
            var distX = Math.Abs(key.ChunkX - cameraChunkX);
            var distY = Math.Abs(key.ChunkY - cameraChunkY);
            var distZ = Math.Abs(key.Z - cameraZ);
            var maxDist = Math.Max(Math.Max(distX, distY), distZ);
            
            // Get current state for hysteresis
            var currentLod = GetChunkLOD(key);
            
            // Check heat score
            var heat = _heatScores.GetValueOrDefault(key);
            if (heat >= HEAT_HOT)
                return LODLevel.L0_Active;
            
            // Check pinned reasons
            if (_pinnedChunks.ContainsKey(key))
                return LODLevel.L1_Near;
            
            // Apply distance bands with hysteresis
            if (currentLod == LODLevel.L0_Active)
            {
                // Currently L0, use down-shift thresholds
                if (maxDist <= R0_DOWN)
                    return LODLevel.L0_Active;
                else if (maxDist <= R1_DOWN)
                    return LODLevel.L1_Near;
                else if (maxDist <= R2_DOWN)
                    return LODLevel.L2_Far;
                else
                    return LODLevel.L3_Dormant;
            }
            else
            {
                // Use up-shift thresholds
                if (maxDist <= R0_ACTIVE)
                    return LODLevel.L0_Active;
                else if (maxDist <= R1_NEAR)
                    return LODLevel.L1_Near;
                else if (maxDist <= R2_FAR)
                    return LODLevel.L2_Far;
                else
                    return LODLevel.L3_Dormant;
            }
        }
        
        private void UpdateChunkLOD(ChunkKey key, LODLevel newLod, ulong tick)
        {
            var currentLod = GetChunkLOD(key);
            if (currentLod == newLod)
                return;
            
            // Handle LOD transitions
            if (newLod < currentLod)
            {
                // Promoting (L4->L3->L2->L1->L0)
                PromoteChunk(key, currentLod, newLod, tick);
            }
            else
            {
                // Demoting (L0->L1->L2->L3->L4)
                DemoteChunk(key, currentLod, newLod, tick);
            }
            
            // Update chunk's LOD level
            var chunk = _world.GetChunk(key);
            if (chunk != null)
            {
                chunk.LODLevel = (int)newLod;
            }
            
            // Update state tracking
            _chunkStates[key] = new ChunkState
            {
                LOD = newLod,
                LastTransitionTick = tick
            };
        }
        
        private void PromoteChunk(ChunkKey key, LODLevel from, LODLevel to, ulong tick)
        {
            // Handle promotion based on levels
            if (from == LODLevel.L4_Unloaded)
            {
                // Load chunk from disk
                LoadChunk(key);
            }
            
            if (from >= LODLevel.L2_Far && to <= LODLevel.L1_Near)
            {
                // Apply catch-up integration per section 3
                ApplyCatchUpIntegration(key, tick);
            }
        }
        
        private void DemoteChunk(ChunkKey key, LODLevel from, LODLevel to, ulong tick)
        {
            // Handle demotion
            if (to == LODLevel.L4_Unloaded)
            {
                // Mark for unload
                MarkForUnload(key);
            }
            
            if (from <= LODLevel.L1_Near && to >= LODLevel.L2_Far)
            {
                // Disable active systems
                DisableActiveSystems(key);
            }
        }
        
        private void LoadChunk(ChunkKey key)
        {
            // Load chunk data from disk if persistent
            // For now, just ensure chunk exists
            _world.GetOrCreateChunk(key);
        }
        
        private void MarkForUnload(ChunkKey key)
        {
            // Add to unload queue
            // In real implementation, would save to disk first
        }
        
        private void ApplyCatchUpIntegration(ChunkKey key, ulong tick)
        {
            // Apply background integrator catch-up per section 3
            // This would run aging, rot, growth counters, etc.
            ChunkState? state = null;
            _chunkStates.TryGetValue(key, out state);
            if (state != null)
            {
                var sleepTicks = tick - state.LastTransitionTick;
                var cappedTicks = Math.Min(sleepTicks, 1000); // Cap catch-up
                // Apply deterministic updates for cappedTicks
            }
        }
        
        private void DisableActiveSystems(ChunkKey key)
        {
            // Disable AI, pathfinding, jobs for this chunk
            // Clear nav caches per section 4.3
        }
        
        private void ProcessUnloadQueue()
        {
            // Process chunks marked for unload
            // Save dirty chunks to disk
            // Remove from memory
        }
        
        private void DecayHeatScores()
        {
            // Decay heat scores per tick
            var keys = OrderChunkKeys(_heatScores.Keys).ToArray();
            foreach (var key in keys)
            {
                var heat = _heatScores[key];
                if (heat > 0)
                {
                    _heatScores[key] = Math.Max(0, heat - HEAT_DECAY_RATE);
                }
            }
        }
        
        /// <summary>
        /// Add heat to a chunk (combat, fire, etc).
        /// </summary>
        public void AddHeat(ChunkKey key, ulong amount)
        {
            _heatScores[key] = _heatScores.GetValueOrDefault(key) + amount;
        }
        
        /// <summary>
        /// Pin a chunk for a reason (UI focus, etc).
        /// </summary>
        public void PinChunk(ChunkKey key, PinReason reason)
        {
            if (!_pinnedChunks.TryGetValue(key, out var reasons))
            {
                reasons = new HashSet<PinReason>();
                _pinnedChunks[key] = reasons;
            }
            reasons.Add(reason);
        }
        
        /// <summary>
        /// Unpin a chunk.
        /// </summary>
        public void UnpinChunk(ChunkKey key, PinReason reason)
        {
            if (_pinnedChunks.TryGetValue(key, out var reasons))
            {
                reasons.Remove(reason);
                if (reasons.Count == 0)
                {
                    _pinnedChunks.Remove(key);
                }
            }
        }
        
        private LODLevel GetChunkLOD(ChunkKey key)
        {
            if (_chunkStates.TryGetValue(key, out var state))
                return state.LOD;
            
            // Default to unloaded for new chunks
            return LODLevel.L4_Unloaded;
        }
        
        private List<ChunkKey> GetChunksInRadius(int centerX, int centerY, int centerZ, int radius)
        {
            var chunks = new List<ChunkKey>();
            
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dz = -2; dz <= 2; dz++) // Limited Z range
                    {
                        var x = centerX + dx;
                        var y = centerY + dy;
                        var z = centerZ + dz;
                        
                        if (x >= 0 && x < _world.SizeInChunks &&
                            y >= 0 && y < _world.SizeInChunks &&
                            z >= 0 && z < _world.MaxZ)
                        {
                            chunks.Add(new ChunkKey(x, y, z));
                        }
                    }
                }
            }
            
            return chunks;
        }

        private static IOrderedEnumerable<ChunkKey> OrderChunkKeys(IEnumerable<ChunkKey> keys)
        {
            return keys
                .OrderBy(static key => key.Z)
                .ThenBy(static key => key.ChunkY)
                .ThenBy(static key => key.ChunkX);
        }
        
        private class ChunkState
        {
            public LODLevel LOD { get; set; }
            public ulong LastTransitionTick { get; set; }
            public ulong SleepAccumTicks { get; set; }
        }
    }
    
    /// <summary>
    /// LOD levels per SIM_LOD_POLICY.md section 0.
    /// </summary>
    internal enum LODLevel
    {
        L0_Active = 0,   // Full simulation
        L1_Near = 1,     // Reduced frequency
        L2_Far = 2,      // Background only
        L3_Dormant = 3,  // No simulation
        L4_Unloaded = 4  // Not in memory
    }
    
    /// <summary>
    /// Reasons to pin a chunk at higher LOD.
    /// </summary>
    internal enum PinReason
    {
        UIFocus,
        StockpileEditor,
        BlueprintPreview,
        BuildInProgress,
        ArtifactPresent,
        QueuedCaravan,
        StorytellerTarget
    }
}
