using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Simulation;
using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.App.Commands;

/// <summary>
/// Command to create a stockpile zone and its per-chunk stockpile cell shards.
/// </summary>
public sealed class CreateStockpileCommand : ICommand
{
    public ulong Tick { get; }
    public Guid CommandId { get; } = Guid.NewGuid();
    public string CommandType => "stockpiles.create";

    private readonly Rectangle _worldRect;
    private readonly int _z;
    private readonly string _presetId;

    public CreateStockpileCommand(ulong tick, Rectangle worldRect, int z, string presetId)
    {
        Tick = tick;
        _worldRect = worldRect;
        _z = z;
        _presetId = string.IsNullOrWhiteSpace(presetId) ? "all" : presetId;
    }

    public void Execute(ISimulationContext context)
    {
        if (context.World is not World world)
            return;

        var cellsByChunk = new Dictionary<ChunkKey, List<int>>();
        int skippedInvalid = 0;
        int skippedOverlap = 0;

        for (int wx = _worldRect.X; wx < _worldRect.X + _worldRect.Width; wx++)
        {
            for (int wy = _worldRect.Y; wy < _worldRect.Y + _worldRect.Height; wy++)
            {
                if (!TryCollectCell(world, wx, wy, cellsByChunk, ref skippedInvalid, ref skippedOverlap))
                    continue;
            }
        }

        int totalCells = cellsByChunk.Values.Sum(static list => list.Count);
        if (totalCells == 0)
        {
            Logger.Log($"[STOCKPILE] Skipped empty stockpile command preset={_presetId} rect=({_worldRect.X},{_worldRect.Y},{_worldRect.Width}x{_worldRect.Height}) z={_z} invalid={skippedInvalid} overlap={skippedOverlap}");
            return;
        }

        var homeChunk = GetHomeChunk(cellsByChunk.Keys);
        var zoneId = world.Stockpiles.CreateZone(BuildZoneName(world), homeChunk, context.CurrentTick);

        foreach (var (chunkKey, cells) in cellsByChunk)
        {
            var chunk = world.GetChunk(chunkKey);
            if (chunk == null)
                continue;

            chunk.EnsureStockpileData();
            var stockpileData = chunk.GetStockpileData();
            stockpileData?.CreateOrUpdateShard(zoneId, chunkKey);
            stockpileData?.AddCellsToZone(zoneId, cells);
        }

        world.Stockpiles.GetZone(zoneId)?.UpdateMemberChunks(cellsByChunk.Keys);

        Logger.Log($"[STOCKPILE] Created zone {zoneId} preset={_presetId} cells={totalCells} invalid={skippedInvalid} overlap={skippedOverlap}");
    }

    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write((int)_worldRect.X);
        bw.Write((int)_worldRect.Y);
        bw.Write((int)_worldRect.Width);
        bw.Write((int)_worldRect.Height);
        bw.Write(_z);
        bw.Write(_presetId);
        return ms.ToArray();
    }

    private bool TryCollectCell(
        World world,
        int wx,
        int wy,
        Dictionary<ChunkKey, List<int>> cellsByChunk,
        ref int skippedInvalid,
        ref int skippedOverlap)
    {
        if (!world.IsValidPosition(wx, wy, _z))
        {
            skippedInvalid++;
            return false;
        }

        int chunkX = wx / Chunk.SIZE_XY;
        int chunkY = wy / Chunk.SIZE_XY;
        int localX = wx % Chunk.SIZE_XY;
        int localY = wy % Chunk.SIZE_XY;
        var chunkKey = new ChunkKey(chunkX, chunkY, _z);
        var chunk = world.GetChunk(chunkKey);
        if (chunk == null)
        {
            skippedInvalid++;
            return false;
        }

        var tile = chunk.GetTile(localX, localY);
        if (tile.Kind != TerrainKind.OpenWithFloor)
        {
            skippedInvalid++;
            return false;
        }

        int cellIndex = Chunk.LocalIndex(localX, localY);
        var stockpileData = chunk.GetStockpileData();
        if (stockpileData != null && stockpileData.GetZoneAtCell(cellIndex) != 0)
        {
            skippedOverlap++;
            return false;
        }

        if (!cellsByChunk.TryGetValue(chunkKey, out var cells))
        {
            cells = new List<int>();
            cellsByChunk.Add(chunkKey, cells);
        }

        cells.Add(cellIndex);
        return true;
    }

    private static ChunkKey GetHomeChunk(IEnumerable<ChunkKey> chunkKeys)
    {
        return chunkKeys.OrderBy(static key => key.Z)
            .ThenBy(static key => key.ChunkY)
            .ThenBy(static key => key.ChunkX)
            .First();
    }

    private string BuildZoneName(World world)
    {
        int number = world.Stockpiles.GetAllZones().Count() + 1;
        return _presetId.Equals("all", StringComparison.OrdinalIgnoreCase)
            ? $"Stockpile {number}"
            : $"{ToTitle(_presetId)} Stockpile {number}";
    }

    private static string ToTitle(string value)
    {
        return value.Length == 0
            ? "Stockpile"
            : char.ToUpperInvariant(value[0]) + value[1..];
    }
}
