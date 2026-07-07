using System.Text.Json.Nodes;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Core.Random;
using HumanFortress.Jobs.Mining;
using SimTerrainKind = HumanFortress.Simulation.Tiles.TerrainKind;

namespace HumanFortress.Jobs.Mining;

internal sealed class MiningDropResolver : IMiningDropResolver
{
    private readonly object _dropsCacheLock = new();
    private readonly Dictionary<string, DropTable> _dropsCache = new();
    private readonly IRuntimeGeologyCatalog? _geology;
    private readonly JsonObject? _miningTuning;
    private readonly Action<string>? _log;
    private bool _dropsCacheBuilt;

    internal MiningDropResolver(IRuntimeGeologyCatalog? geology, string? miningTuningJson, Action<string>? log = null)
    {
        _geology = geology;
        _miningTuning = ParseMiningTuning(miningTuningJson);
        _log = log;
    }

    internal int CalculateRequiredTicks(ushort geologyHandle, SimTerrainKind terrainKind)
    {
        try
        {
            var ticksObj = _miningTuning?["geology_ticks"]?["default"] as JsonObject;
            if (ticksObj == null) return 20;

            string key = terrainKind == SimTerrainKind.Ramp ? "ramp" : "wall";
            var ticks = ReadInt(ticksObj[key], 20);
            return Math.Max(1, ticks);
        }
        catch
        {
            return 20;
        }
    }

    internal ushort ResolveAirGeologyHandle()
    {
        try
        {
            if (_geology != null
                && _geology.TryGetGeologyHandleByMaterialAndKind("air", SimTerrainKind.OpenNoFloor.ToString(), out var handle))
            {
                return handle;
            }
        }
        catch
        {
        }

        return 0;
    }

    internal List<(string itemId, int qty)> ChooseDropsFor(ushort geologyHandle, SimTerrainKind terrainKind)
    {
        var result = new List<(string, int)>();
        try
        {
            EnsureDropsCache();

            var geology = _geology?.GetGeologyByHandle(geologyHandle);
            string geoKey = geology != null ? geology.Id : "default";

            _dropsCache.TryGetValue(geoKey, out var cacheTable);
            if (cacheTable == null)
            {
                if (geology != null)
                {
                    var id = geology.Id;
                    string? norm = null;
                    const string pRockWall = "core_terrain_wall_rock_";
                    const string pRockFloor = "core_terrain_floor_rock_";
                    const string pOreWall = "core_terrain_wall_ore_";
                    if (id.StartsWith(pRockWall, StringComparison.OrdinalIgnoreCase))
                    {
                        norm = "core_geology_" + id[pRockWall.Length..];
                    }
                    else if (id.StartsWith(pRockFloor, StringComparison.OrdinalIgnoreCase))
                    {
                        norm = "core_geology_" + id[pRockFloor.Length..];
                    }
                    else if (id.StartsWith(pOreWall, StringComparison.OrdinalIgnoreCase))
                    {
                        norm = "core_geology_" + id[pOreWall.Length..];
                    }

                    if (!string.IsNullOrEmpty(norm))
                    {
                        _dropsCache.TryGetValue(norm, out cacheTable);
                    }
                }

                cacheTable ??= _dropsCache.GetValueOrDefault("default");
            }

            if (cacheTable != null)
            {
                var list = terrainKind == SimTerrainKind.Ramp ? cacheTable.Ramp : cacheTable.Wall;
                if (list.Count > 0)
                {
                    var rng = new DeterministicRng(CreateDropSeed(geologyHandle, terrainKind));
                    foreach (var drop in list)
                    {
                        int quantity = NextInclusive(rng, drop.Min, drop.Max);
                        if (quantity > 0)
                        {
                            result.Add((drop.Id, quantity));
                        }
                    }

                    return result;
                }
            }

            var dropsObj = _miningTuning?["geology_drops"]?[geoKey] as JsonObject
                ?? _miningTuning?["geology_drops"]?["default"] as JsonObject;
            if (dropsObj == null) return result;

            string key = terrainKind == SimTerrainKind.Ramp ? "ramp" : "wall";
            var dropsList = dropsObj[key] as JsonArray;
            if (dropsList == null)
            {
                if (geology != null)
                {
                    var id = geology.Id;
                    const string pRockWall = "core_terrain_wall_rock_";
                    const string pOreWall = "core_terrain_wall_ore_";
                    if (id.StartsWith(pRockWall, StringComparison.OrdinalIgnoreCase))
                    {
                        var rock = id[pRockWall.Length..];
                        string itemId = $"core_item_boulder_{rock}";
                        int qty = terrainKind == SimTerrainKind.Ramp ? 1 : 3;
                        result.Add((itemId, qty));
                        return result;
                    }

                    if (id.StartsWith(pOreWall, StringComparison.OrdinalIgnoreCase))
                    {
                        var ore = id[pOreWall.Length..];
                        string itemId = $"core_item_ore_{ore}";
                        int qty = terrainKind == SimTerrainKind.Ramp ? 1 : 2;
                        result.Add((itemId, qty));
                        return result;
                    }
                }

                return result;
            }

            var rawDropRng = new DeterministicRng(CreateDropSeed(geologyHandle, terrainKind));
            foreach (var dropEntry in dropsList)
            {
                if (dropEntry is not JsonObject dropObject) continue;

                var itemId = ReadString(dropObject["item_id"]);
                if (string.IsNullOrEmpty(itemId)) continue;

                var min = ReadInt(dropObject["min"], 1);
                var max = ReadInt(dropObject["max"], min);
                var qty = NextInclusive(rawDropRng, min, max);

                result.Add((itemId, qty));
            }

            return result;
        }
        catch (Exception ex)
        {
            _log?.Invoke($"[MINING] Failed to load drops: {ex.Message}");
            return new List<(string, int)> { ("core_item_boulder_granite", 1) };
        }
    }

    int IMiningWorkCostResolver.CalculateRequiredTicks(ushort geologyHandle, SimTerrainKind terrainKind) =>
        CalculateRequiredTicks(geologyHandle, terrainKind);

    ushort IMiningDropResolver.ResolveAirGeologyHandle() => ResolveAirGeologyHandle();

    List<(string itemId, int qty)> IMiningDropResolver.ChooseDropsFor(ushort geologyHandle, SimTerrainKind terrainKind) =>
        ChooseDropsFor(geologyHandle, terrainKind);

    private static ulong CreateDropSeed(ushort geologyHandle, SimTerrainKind terrainKind)
    {
        return 0x4D494E4544524F50UL ^ ((ulong)geologyHandle << 32) ^ (uint)terrainKind;
    }

    private static int NextInclusive(DeterministicRng rng, int min, int max)
    {
        if (max < min)
            max = min;

        return rng.NextInt(min, max + 1);
    }

    private void EnsureDropsCache()
    {
        if (_dropsCacheBuilt) return;
        lock (_dropsCacheLock)
        {
            if (_dropsCacheBuilt) return;

            if (_miningTuning?["geology_drops"] is JsonObject root)
            {
                foreach (var pair in root)
                {
                    var key = pair.Key;
                    if (pair.Value is not JsonObject value) continue;

                    var table = new DropTable();
                    FillDropTable(value, "wall", table.Wall);
                    FillDropTable(value, "ramp", table.Ramp);
                    _dropsCache[key] = table;

                    if (key.StartsWith("core_geology_", StringComparison.OrdinalIgnoreCase))
                    {
                        var suffix = key["core_geology_".Length..];
                        _dropsCache["core_terrain_wall_rock_" + suffix] = table;
                        _dropsCache["core_terrain_floor_rock_" + suffix] = table;
                        _dropsCache["core_terrain_wall_ore_" + suffix] = table;
                    }
                }
            }

            _dropsCacheBuilt = true;
        }
    }

    private static JsonObject? ParseMiningTuning(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private static void FillDropTable(JsonObject value, string name, List<DropDef> into)
    {
        if (value[name] is not JsonArray array) return;

        foreach (var entry in array)
        {
            if (entry is not JsonObject drop) continue;

            var id = ReadString(drop["item_id"]);
            if (string.IsNullOrWhiteSpace(id)) continue;

            int min = ReadInt(drop["min"], 1);
            int max = ReadInt(drop["max"], min);
            double weight = ReadDouble(drop["weight"], 1.0);
            into.Add(new DropDef { Id = id, Min = min, Max = max, Weight = weight });
        }
    }

    private static string? ReadString(JsonNode? node)
    {
        return node?.GetValue<string>();
    }

    private static int ReadInt(JsonNode? node, int fallback)
    {
        return node == null ? fallback : node.GetValue<int>();
    }

    private static double ReadDouble(JsonNode? node, double fallback)
    {
        return node == null ? fallback : node.GetValue<double>();
    }

    private struct DropDef
    {
        internal string Id;
        internal int Min;
        internal int Max;
        internal double Weight;
    }

    private sealed class DropTable
    {
        internal List<DropDef> Wall { get; } = new();
        internal List<DropDef> Ramp { get; } = new();
    }
}
