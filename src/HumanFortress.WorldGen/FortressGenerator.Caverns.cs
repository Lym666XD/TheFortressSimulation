using System;
using System.Text.Json.Nodes;

namespace HumanFortress.WorldGen.Implementation
{
    internal sealed partial class FortressGenerator
    {
        private void CarveCavernConnected(FortressMap map, int[,] surfaceZ, JsonObject? mapgen)
        {
            int tilesPerChunk = 32;
            int worldSize = _fortressSize * tilesPerChunk;
            var bands = Array(mapgen, "bands");
            if (bands == null || bands.Count == 0) return;

            int bestZMin = 0, bestZMax = 0; double bestDensity = -1;
            foreach (var bandNode in bands)
            {
                var band = bandNode as JsonObject;
                if (band == null) continue;

                double d = Value(Object(band, "caves"), "density", 0.0);
                if (d > bestDensity)
                {
                    bestDensity = d;
                    bestZMin = Value(band, "z_min", 0);
                    bestZMax = Value(band, "z_max", 0);
                }
            }

            if (bestDensity <= 0) return;
            int zC = Math.Clamp((bestZMin + bestZMax) / 2, 1, map.MaxZ - 2);

            var cavTune = _content.CavernTuning;
            var pathTune = Object(cavTune, "path");
            var roomTune = Object(cavTune, "rooms");
            var shaftTune = Object(cavTune, "shafts");
            int pathThickness = Value(pathTune, "thickness", 4);
            double stepsFactor = Value(pathTune, "steps_factor", 6.0);
            double biasEast = Value(pathTune, "direction_bias_east", 0.7);
            int tunnelWidth = Value(pathTune, "tunnel_width", 3);
            int roomRMin = Value(roomTune, "radius_min", 4);
            int roomRMax = Value(roomTune, "radius_max", 7);
            double roomIntervalFactor = Value(roomTune, "interval_factor", 0.15);
            int shaftsCount = Value(shaftTune, "count", 3);
            bool mossOnFloor = Value(cavTune, "moss_on_floor", true);

            var rng = new HumanFortress.Core.Random.DeterministicRng(_seed ^ 0xC0FEBABEu);
            var cav = new bool[worldSize, worldSize];
            int sx = 2; int sy = (int)(rng.NextFloat() * worldSize);
            int steps = Math.Max(worldSize * 2, (int)Math.Round(worldSize * stepsFactor));
            int x = sx, y = sy;
            for (int i = 0; i < steps; i++)
            {
                if (zC < surfaceZ[x, y])
                {
                    int r = Math.Max(1, pathThickness);
                    int r2 = r * r;
                    for (int dx = -r; dx <= r; dx++)
                        for (int dy = -r; dy <= r; dy++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= worldSize || yy >= worldSize) continue;
                            if (dx * dx + dy * dy > r2) continue;
                            if (zC < surfaceZ[xx, yy]) cav[xx, yy] = true;
                        }
                }

                int roomInterval = Math.Max(8, (int)Math.Round(worldSize * roomIntervalFactor));
                if (roomInterval > 0 && (i % roomInterval) == 0)
                {
                    int r = roomRMin + (int)(rng.NextFloat() * Math.Max(1, roomRMax - roomRMin));
                    for (int dx = -r; dx <= r; dx++)
                        for (int dy = -r; dy <= r; dy++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= worldSize || yy >= worldSize) continue;
                            if (dx * dx + dy * dy <= r * r && zC < surfaceZ[xx, yy]) cav[xx, yy] = true;
                        }
                }

                int dir = (int)(rng.NextFloat() * 4);
                if (rng.NextFloat() < biasEast) dir = 1;
                switch (dir)
                {
                    case 0: if (y > 1) y--; break;
                    case 1: if (x < worldSize - 2) x++; break;
                    case 2: if (y < worldSize - 2) y++; break;
                    case 3: if (x > 1) x--; break;
                }
            }

            for (int gx = 0; gx < worldSize; gx++)
            {
                for (int gy = 0; gy < worldSize; gy++)
                {
                    if (!cav[gx, gy]) continue;
                    if (zC >= surfaceZ[gx, gy]) continue;
                    int cx = gx / tilesPerChunk; int lx = gx % tilesPerChunk;
                    int cy = gy / tilesPerChunk; int ly = gy % tilesPerChunk;

                    string belowId = zC > 0 ? map.GetChunk(cx, cy).GetGeologyId(lx, ly, zC - 1) : "core_terrain_wall_rock_limestone";
                    string floorId = belowId.Replace("_wall_", "_floor_");
                    if (floorId == belowId) floorId = "core_terrain_floor_rock_limestone";
                    map.GetChunk(cx, cy).SetGeology(lx, ly, zC, floorId);
                    if (mossOnFloor)
                    {
                        byte moss = (byte)(1 << 3);
                        map.GetChunk(cx, cy).SetSurfaceBits(lx, ly, zC, moss);
                    }

                    map.GetChunk(cx, cy).SetGeology(lx, ly, zC + 1, "core_terrain_air");
                }
            }

            for (int i = 0; i < Math.Max(1, shaftsCount); i++)
            {
                int gx = (i == 0) ? 2 : worldSize - 3;
                int gy = (int)(rng.NextFloat() * worldSize);
                int sZ = surfaceZ[gx, gy];
                int zTop = Math.Max(1, sZ);
                // Keep the cavern floor as the shaft landing and clear only the air column above it.
                int zBot = Math.Max(1, zC + 1);
                for (int z = zBot; z <= zTop; z++)
                {
                    int ccx = gx / tilesPerChunk; int llx = gx % tilesPerChunk;
                    int ccy = gy / tilesPerChunk; int lly = gy % tilesPerChunk;
                    var shaftChunk = map.GetChunk(ccx, ccy);
                    shaftChunk.SetGeology(llx, lly, z, "core_terrain_air");
                    shaftChunk.SetSurfaceBits(llx, lly, z, 0);
                }

                int tx = gx, ty = gy;
                int safety = worldSize * 2;
                while (!cav[tx, ty] && safety-- > 0)
                {
                    int ccx = tx / tilesPerChunk; int llx = tx % tilesPerChunk;
                    int ccy = ty / tilesPerChunk; int lly = ty % tilesPerChunk;
                    string belowId = zC > 0 ? map.GetChunk(ccx, ccy).GetGeologyId(llx, lly, zC - 1) : "core_terrain_wall_rock_limestone";
                    string floorId = belowId.Replace("_wall_", "_floor_");
                    if (floorId == belowId) floorId = "core_terrain_floor_rock_limestone";
                    int r = Math.Max(1, tunnelWidth);
                    int r2 = r * r;
                    for (int dx = -r; dx <= r; dx++)
                        for (int dy = -r; dy <= r; dy++)
                        {
                            int gxw = tx + dx; int gyw = ty + dy;
                            if (gxw < 0 || gyw < 0 || gxw >= worldSize || gyw >= worldSize) continue;
                            if (dx * dx + dy * dy > r2) continue;
                            int cccx = gxw / tilesPerChunk; int wwwx = gxw % tilesPerChunk;
                            int cccy = gyw / tilesPerChunk; int wwwy = gyw % tilesPerChunk;
                            map.GetChunk(cccx, cccy).SetGeology(wwwx, wwwy, zC, floorId);
                            if (mossOnFloor) map.GetChunk(cccx, cccy).SetSurfaceBits(wwwx, wwwy, zC, (byte)(1 << 3));
                            map.GetChunk(cccx, cccy).SetGeology(wwwx, wwwy, zC + 1, "core_terrain_air");
                        }

                    if (i == 0) tx++; else tx--;
                    ty += ty < worldSize / 2 ? 1 : -1;
                    ty = Math.Clamp(ty, 1, worldSize - 2);
                    tx = Math.Clamp(tx, 1, worldSize - 2);
                }
            }
        }
    }
}
