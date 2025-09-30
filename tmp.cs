        private void CreateStockpile(string presetId)
        {
            if (_stockpileManager == null || !_ui.PlaceFirstCorner.HasValue || !_ui.PlaceSecondCorner.HasValue)
                return;

            var corner1 = _ui.PlaceFirstCorner.Value;
            var corner2 = _ui.PlaceSecondCorner.Value;

            // Create zone
            var zoneId = _stockpileManager.CreateZone($"Stockpile {_stockpileManager.GetAllZones().Count() + 1}",
                new HumanFortress.Simulation.World.ChunkKey(corner1.X / 32, corner1.Y / 32, _currentZ), _uiTick);

            // Calculate cells to add
            int minX = Math.Min(corner1.X, corner2.X);
            int maxX = Math.Max(corner1.X, corner2.X);
            int minY = Math.Min(corner1.Y, corner2.Y);
            int maxY = Math.Max(corner1.Y, corner2.Y);

            // Group cells by chunk - 检查地形和重叠
            var cellsByChunk = new Dictionary<HumanFortress.Simulation.World.ChunkKey, List<int>>();
            int skippedInvalid = 0;
            int skippedOverlap = 0;
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    int chunkX = x / 32;
                    int chunkY = y / 32;
                    int localX = x % 32;
                    int localY = y % 32;
                    var chunkKey = new HumanFortress.Simulation.World.ChunkKey(chunkX, chunkY, _currentZ);

                    // 检查地形是否有效（必须是OpenWithFloor�?                    if (_world != null)
                    {
                        var chunk = _world.GetChunk(chunkKey);
                        if (chunk != null)
                        {
                            var tile = chunk.GetTile(localX, localY);
                            if (tile.Kind != HumanFortress.Simulation.Tiles.TerrainKind.OpenWithFloor)
                            {
                                skippedInvalid++;
                                continue; // 只能在开放地板上创建储存�?                            }

                            // 检查是否已有其他储存区
                            var stockpileData = chunk.GetStockpileData();
                            if (stockpileData != null)
                            {
                                int cellIndex = localY * 32 + localX;
                                if (stockpileData.GetZoneAtCell(cellIndex) != 0)
                                {
                                    skippedOverlap++;
                                    continue; // 跳过已有储存区的格子
                                }
