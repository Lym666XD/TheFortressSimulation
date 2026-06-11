using System;
using System.Collections.Generic;
using HumanFortress.Jobs.Mining;
using Newtonsoft.Json.Linq;
using ContentRegistry = HumanFortress.Core.Content.Registry.ContentRegistry;
using SimTerrainKind = HumanFortress.Simulation.Tiles.TerrainKind;

namespace HumanFortress.App.Jobs;

internal sealed class MiningDropResolver : IMiningDropResolver
{
    private static readonly object DropsCacheLock = new();
    private static readonly Dictionary<string, DropTable> DropsCache = new();
    private static bool _dropsCacheBuilt;

    public int CalculateRequiredTicks(ushort geologyHandle, SimTerrainKind terrainKind)
    {
        try
        {
            var reg = ContentRegistry.Instance;
            var ticksObj = reg.GetTuning<JObject>("tuning.mining", "$.geology_ticks.default");
            if (ticksObj == null) return 20;

            string key = terrainKind == SimTerrainKind.Ramp ? "ramp" : "wall";
            var ticksToken = ticksObj[key];
            var ticks = ticksToken?.ToObject<int?>() ?? 20;
            return Math.Max(1, ticks);
        }
        catch
        {
            return 20;
        }
    }

    public List<(string itemId, int qty)> ChooseDropsFor(ushort geologyHandle, SimTerrainKind terrainKind)
    {
        var result = new List<(string, int)>();
        try
        {
            var reg = ContentRegistry.Instance;
            EnsureDropsCache(reg);

            var geology = reg.GetGeologyByHandle(geologyHandle);
            string geoKey = geology != null ? geology.Id : "default";

            DropsCache.TryGetValue(geoKey, out var cacheTable);
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
                        norm = "core_geology_" + id.Substring(pRockWall.Length);
                    else if (id.StartsWith(pRockFloor, StringComparison.OrdinalIgnoreCase))
                        norm = "core_geology_" + id.Substring(pRockFloor.Length);
                    else if (id.StartsWith(pOreWall, StringComparison.OrdinalIgnoreCase))
                        norm = "core_geology_" + id.Substring(pOreWall.Length);

                    if (!string.IsNullOrEmpty(norm)) DropsCache.TryGetValue(norm, out cacheTable);
                }

                if (cacheTable == null) DropsCache.TryGetValue("default", out cacheTable);
            }

            if (cacheTable != null)
            {
                var list = terrainKind == SimTerrainKind.Ramp ? cacheTable.Ramp : cacheTable.Wall;
                if (list.Count > 0)
                {
                    var seed = (uint)geologyHandle ^ (uint)terrainKind;
                    var rng = new Random((int)seed);
                    foreach (var d in list)
                    {
                        int q = rng.Next(d.Min, d.Max + 1);
                        if (q > 0) result.Add((d.Id, q));
                    }
                    return result;
                }
            }

            var dropsObj = reg.GetTuning<JObject>("tuning.mining", $"$.geology_drops.{geoKey}");
            dropsObj ??= reg.GetTuning<JObject>("tuning.mining", "$.geology_drops.default");
            if (dropsObj == null) return result;

            string key = terrainKind == SimTerrainKind.Ramp ? "ramp" : "wall";
            var dropsList = dropsObj[key] as JArray;
            if (dropsList == null)
            {
                if (geology != null)
                {
                    var id = geology.Id;
                    const string pRockWall = "core_terrain_wall_rock_";
                    const string pOreWall = "core_terrain_wall_ore_";
                    if (id.StartsWith(pRockWall, StringComparison.OrdinalIgnoreCase))
                    {
                        var rock = id.Substring(pRockWall.Length);
                        string itemId = $"core_item_boulder_{rock}";
                        int qty = terrainKind == SimTerrainKind.Ramp ? 1 : 3;
                        result.Add((itemId, qty));
                        return result;
                    }
                    if (id.StartsWith(pOreWall, StringComparison.OrdinalIgnoreCase))
                    {
                        var ore = id.Substring(pOreWall.Length);
                        string itemId = $"core_item_ore_{ore}";
                        int qty = terrainKind == SimTerrainKind.Ramp ? 1 : 2;
                        result.Add((itemId, qty));
                        return result;
                    }
                }
                return result;
            }

            foreach (var dropEntry in dropsList)
            {
                var itemIdToken = dropEntry["item_id"];
                var itemId = itemIdToken?.ToObject<string>();
                if (string.IsNullOrEmpty(itemId)) continue;

                var minToken = dropEntry["min"];
                var maxToken = dropEntry["max"];
                var min = minToken?.ToObject<int?>() ?? 1;
                var max = maxToken?.ToObject<int?>() ?? min;

                var seed = (uint)geologyHandle ^ (uint)terrainKind;
                var rng = new Random((int)seed);
                var qty = rng.Next(min, max + 1);

                result.Add((itemId, qty));
            }

            return result;
        }
        catch (Exception ex)
        {
            Logger.Log($"[MINING] Failed to load drops: {ex.Message}");
            return new List<(string, int)> { ("core_item_boulder_granite", 1) };
        }
    }

    private static void EnsureDropsCache(ContentRegistry reg)
    {
        if (_dropsCacheBuilt) return;
        lock (DropsCacheLock)
        {
            if (_dropsCacheBuilt) return;
            var root = reg.GetTuning<JObject>("tuning.mining", "$.geology_drops");
            if (root != null)
            {
                foreach (var prop in root.Properties())
                {
                    var key = prop.Name;
                    var val = prop.Value as JObject;
                    if (val == null) continue;
                    var table = new DropTable();
                    FillDropTable(val, "wall", table.Wall);
                    FillDropTable(val, "ramp", table.Ramp);
                    DropsCache[key] = table;
                    if (key.StartsWith("core_geology_", StringComparison.OrdinalIgnoreCase))
                    {
                        var sfx = key.Substring("core_geology_".Length);
                        DropsCache["core_terrain_wall_rock_" + sfx] = table;
                        DropsCache["core_terrain_floor_rock_" + sfx] = table;
                        DropsCache["core_terrain_wall_ore_" + sfx] = table;
                    }
                }
            }
            _dropsCacheBuilt = true;
        }
    }

    private static void FillDropTable(JObject value, string name, List<DropDef> into)
    {
        var arr = value[name] as JArray;
        if (arr == null) return;
        foreach (var e in arr)
        {
            var id = e["item_id"]?.ToObject<string>();
            if (string.IsNullOrWhiteSpace(id)) continue;
            int min = e["min"]?.ToObject<int?>() ?? 1;
            int max = e["max"]?.ToObject<int?>() ?? min;
            double weight = e["weight"]?.ToObject<double?>() ?? 1.0;
            into.Add(new DropDef { Id = id!, Min = min, Max = max, Weight = weight });
        }
    }

    private struct DropDef
    {
        public string Id;
        public int Min;
        public int Max;
        public double Weight;
    }

    private sealed class DropTable
    {
        public List<DropDef> Wall { get; } = new();
        public List<DropDef> Ramp { get; } = new();
    }
}
