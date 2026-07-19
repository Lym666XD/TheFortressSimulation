using HumanFortress.Core.Determinism;
using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.Runtime.Snapshots;

internal sealed partial class RuntimeFrameSnapshotPublisher
{
    private SimulationMapViewportDeltaData PublishMapViewportDelta(
        SimulationMapViewportData mapViewport,
        RuntimeFrameRenderRequest request)
    {
        if (!mapViewport.IsAvailable)
            return SimulationMapViewportDeltaData.Unavailable;

        var requestHash = BuildMapViewportRequestHash(request);
        var (currentCells, currentCellsByKey) = BuildFinalMapViewportCells(mapViewport);
        var currentRows = BuildMapViewportRows(currentCells, mapViewport.Height);
        var currentRowPayloadHashesByY = currentRows.ToDictionary(
            static row => row.ScreenY,
            static row => row.PayloadHash);
        var currentRegions = BuildMapViewportRegions(
            currentCells,
            mapViewport.Width,
            mapViewport.Height);
        var currentRegionPayloadHashesByKey = currentRegions.ToDictionary(
            static region => MapViewportRegionKey(region.RegionX, region.RegionY),
            static region => region.PayloadHash);
        var payloadHash = BuildMapViewportPayloadHash(mapViewport, currentCells);

        lock (_gate)
        {
            var previousFrame = _lastMapViewport;
            var hasPreviousFrame = previousFrame.HasValue;
            var previous = previousFrame.GetValueOrDefault();
            var canApplyToBase = hasPreviousFrame
                && string.Equals(previous.RequestHash, requestHash, StringComparison.Ordinal);
            var changedCells = canApplyToBase
                ? BuildChangedMapViewportCells(currentCells, previous.CellsByKey, mapViewport.Width)
                : currentCells;
            var changedRows = canApplyToBase
                ? BuildChangedMapViewportRows(currentRows, previous.RowPayloadHashesByY)
                : currentRows;
            var changedRegions = canApplyToBase
                ? BuildChangedMapViewportRegions(currentRegions, previous.RegionPayloadHashesByKey)
                : currentRegions;

            _lastMapViewport = new PublishedMapViewportFrame(
                requestHash,
                payloadHash,
                currentCellsByKey,
                currentRowPayloadHashesByY,
                currentRegionPayloadHashesByKey);

            return canApplyToBase
                ? SimulationMapViewportDeltaData.Delta(
                    payloadHash,
                    previous.PayloadHash,
                    changedCells,
                    changedRows,
                    changedRegions)
                : SimulationMapViewportDeltaData.FullSnapshot(
                    payloadHash,
                    changedCells,
                    changedRows,
                    changedRegions);
        }
    }

    private static (MapViewportCellView[] Cells, Dictionary<int, MapViewportCellView> CellsByKey) BuildFinalMapViewportCells(
        SimulationMapViewportData mapViewport)
    {
        var cellsByKey = new Dictionary<int, MapViewportCellView>();
        foreach (var cell in mapViewport.Cells)
        {
            if (cell.ScreenX < 0
                || cell.ScreenY < 0
                || cell.ScreenX >= mapViewport.Width
                || cell.ScreenY >= mapViewport.Height)
            {
                continue;
            }

            cellsByKey[MapViewportCellKey(cell.ScreenX, cell.ScreenY, mapViewport.Width)] = cell;
        }

        var orderedCells = cellsByKey
            .OrderBy(static entry => entry.Key)
            .Select(static entry => entry.Value)
            .ToArray();
        return (orderedCells, cellsByKey);
    }

    private static MapViewportCellView[] BuildChangedMapViewportCells(
        MapViewportCellView[] currentCells,
        IReadOnlyDictionary<int, MapViewportCellView> previousCellsByKey,
        int width)
    {
        return currentCells
            .Where(cell =>
            {
                var key = MapViewportCellKey(cell.ScreenX, cell.ScreenY, width);
                return !previousCellsByKey.TryGetValue(key, out var previous)
                    || !previous.Equals(cell);
            })
            .ToArray();
    }

    private static MapViewportRegionDeltaView[] BuildMapViewportRegions(
        IReadOnlyList<MapViewportCellView> finalCells,
        int width,
        int height)
    {
        int regionSize = SimulationMapViewportDeltaSchema.RegionSize;
        int regionColumns = CeilingDivide(Math.Max(0, width), regionSize);
        int regionRows = CeilingDivide(Math.Max(0, height), regionSize);
        var cellsByRegion = finalCells
            .GroupBy(cell => MapViewportRegionKey(cell.ScreenX / regionSize, cell.ScreenY / regionSize))
            .ToDictionary(
                static group => group.Key,
                static group => group
                    .OrderBy(static cell => cell.ScreenY)
                    .ThenBy(static cell => cell.ScreenX)
                    .ToArray());

        var regions = new MapViewportRegionDeltaView[regionColumns * regionRows];
        int index = 0;
        for (int regionY = 0; regionY < regionRows; regionY++)
        {
            for (int regionX = 0; regionX < regionColumns; regionX++)
            {
                int screenX = regionX * regionSize;
                int screenY = regionY * regionSize;
                int regionWidth = Math.Min(regionSize, width - screenX);
                int regionHeight = Math.Min(regionSize, height - screenY);
                long regionKey = MapViewportRegionKey(regionX, regionY);
                var cells = cellsByRegion.TryGetValue(regionKey, out var regionCells)
                    ? regionCells
                    : Array.Empty<MapViewportCellView>();
                var regionPayloadHash = BuildMapViewportRegionPayloadHash(
                    regionX,
                    regionY,
                    screenX,
                    screenY,
                    regionWidth,
                    regionHeight,
                    cells);
                regions[index++] = new MapViewportRegionDeltaView(
                    regionX,
                    regionY,
                    screenX,
                    screenY,
                    regionWidth,
                    regionHeight,
                    regionPayloadHash,
                    SimulationSnapshotPayloadHashAlgorithm.CanonicalSha256V1,
                    cells);
            }
        }

        return regions;
    }

    private static MapViewportRegionDeltaView[] BuildChangedMapViewportRegions(
        IReadOnlyList<MapViewportRegionDeltaView> currentRegions,
        IReadOnlyDictionary<long, string> previousRegionPayloadHashesByKey)
    {
        return currentRegions
            .Where(region =>
            {
                long regionKey = MapViewportRegionKey(region.RegionX, region.RegionY);
                return !previousRegionPayloadHashesByKey.TryGetValue(regionKey, out var previousHash)
                    || !string.Equals(previousHash, region.PayloadHash, StringComparison.Ordinal);
            })
            .ToArray();
    }

    private static MapViewportRowDeltaView[] BuildMapViewportRows(
        IReadOnlyList<MapViewportCellView> finalCells,
        int height)
    {
        var cellsByRow = finalCells
            .GroupBy(static cell => cell.ScreenY)
            .ToDictionary(
                static group => group.Key,
                static group => group
                    .OrderBy(static cell => cell.ScreenX)
                    .ToArray());
        var rows = new MapViewportRowDeltaView[Math.Max(0, height)];
        for (int y = 0; y < rows.Length; y++)
        {
            var cells = cellsByRow.TryGetValue(y, out var rowCells)
                ? rowCells
                : Array.Empty<MapViewportCellView>();
            var rowPayloadHash = BuildMapViewportRowPayloadHash(y, cells);
            rows[y] = new MapViewportRowDeltaView(
                y,
                rowPayloadHash,
                SimulationSnapshotPayloadHashAlgorithm.CanonicalSha256V1,
                cells);
        }

        return rows;
    }

    private static MapViewportRowDeltaView[] BuildChangedMapViewportRows(
        IReadOnlyList<MapViewportRowDeltaView> currentRows,
        IReadOnlyDictionary<int, string> previousRowPayloadHashesByY)
    {
        return currentRows
            .Where(row =>
            {
                return !previousRowPayloadHashesByY.TryGetValue(row.ScreenY, out var previousHash)
                    || !string.Equals(previousHash, row.PayloadHash, StringComparison.Ordinal);
            })
            .ToArray();
    }

    private static string BuildMapViewportPayloadHash(
        SimulationMapViewportData mapViewport,
        IReadOnlyList<MapViewportCellView> finalCells)
    {
        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("runtime.frame.map-viewport.payload.v2");
            hash.AddBoolean(mapViewport.IsAvailable);
            hash.AddBoolean(mapViewport.HasWorld);
            hash.AddInt32(mapViewport.Width);
            hash.AddInt32(mapViewport.Height);
            hash.AddInt32(mapViewport.CameraX);
            hash.AddInt32(mapViewport.CameraY);
            hash.AddInt32(mapViewport.CurrentZ);
            AddViewport(hash, mapViewport.Viewport);
            hash.AddInt32(finalCells.Count);
            foreach (var cell in finalCells)
                AddMapViewportCell(hash, cell);
        });
    }

    private static string BuildMapViewportRowPayloadHash(
        int screenY,
        IReadOnlyList<MapViewportCellView> rowCells)
    {
        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("runtime.frame.map-viewport.row.payload.v1");
            hash.AddInt32(screenY);
            hash.AddInt32(rowCells.Count);
            foreach (var cell in rowCells)
                AddMapViewportCell(hash, cell);
        });
    }

    private static string BuildMapViewportRegionPayloadHash(
        int regionX,
        int regionY,
        int screenX,
        int screenY,
        int regionWidth,
        int regionHeight,
        IReadOnlyList<MapViewportCellView> regionCells)
    {
        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("runtime.frame.map-viewport.region.payload.v1");
            hash.AddInt32(regionX);
            hash.AddInt32(regionY);
            hash.AddInt32(screenX);
            hash.AddInt32(screenY);
            hash.AddInt32(regionWidth);
            hash.AddInt32(regionHeight);
            hash.AddInt32(regionCells.Count);
            foreach (var cell in regionCells)
                AddMapViewportCell(hash, cell);
        });
    }

    private static void AddMapViewportCell(ReplayHashBuilder hash, MapViewportCellView cell)
    {
        hash.AddInt32(cell.ScreenX);
        hash.AddInt32(cell.ScreenY);
        hash.AddInt32(cell.Glyph);
        hash.AddInt32(cell.Color.R);
        hash.AddInt32(cell.Color.G);
        hash.AddInt32(cell.Color.B);
    }

    private static int MapViewportCellKey(int screenX, int screenY, int width)
    {
        return checked((screenY * width) + screenX);
    }

    private static long MapViewportRegionKey(int regionX, int regionY)
    {
        return ((long)regionY << 32) | (uint)regionX;
    }

    private static int CeilingDivide(int value, int divisor)
    {
        if (value <= 0)
            return 0;
        return ((value - 1) / divisor) + 1;
    }
}
