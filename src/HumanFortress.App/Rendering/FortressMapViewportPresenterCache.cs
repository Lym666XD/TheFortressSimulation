using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.Rendering;

internal sealed class FortressMapViewportPresenterCache
{
    private readonly Dictionary<int, MapViewportCellView> _cellsByKey = new();
    private string? _payloadHash;
    private bool _hasWorld;
    private int _width;
    private int _height;
    private int _cameraX;
    private int _cameraY;
    private int _currentZ;

    internal SimulationMapViewportData Present(SimulationMapViewportData mapData)
    {
        if (!mapData.IsAvailable)
        {
            Reset();
            return mapData;
        }

        var delta = mapData.Delta;
        if (CanApplyDelta(mapData, delta))
        {
            ApplyDelta(mapData, delta);
            _payloadHash = delta.PayloadHash;
            return mapData with { Cells = OrderedCells() };
        }

        var fullCells = SelectFullSnapshotCells(mapData, delta);
        Seed(mapData, delta.IsAvailable ? delta.PayloadHash : null, fullCells);
        return mapData with { Cells = OrderedCells() };
    }

    internal void Reset()
    {
        _cellsByKey.Clear();
        _payloadHash = null;
        _hasWorld = false;
        _width = 0;
        _height = 0;
        _cameraX = 0;
        _cameraY = 0;
        _currentZ = 0;
    }

    private bool CanApplyDelta(
        SimulationMapViewportData mapData,
        SimulationMapViewportDeltaData delta)
    {
        return delta.IsAvailable
            && delta.SchemaVersion == SimulationMapViewportDeltaSchema.CurrentVersion
            && delta.CanApplyToBase
            && !string.IsNullOrWhiteSpace(delta.BasePayloadHash)
            && string.Equals(delta.BasePayloadHash, _payloadHash, StringComparison.Ordinal)
            && string.Equals(delta.PayloadHashAlgorithm, SimulationSnapshotPayloadHashAlgorithm.CanonicalSha256V1, StringComparison.Ordinal)
            && _hasWorld == mapData.HasWorld
            && _width == mapData.Width
            && _height == mapData.Height
            && _cameraX == mapData.CameraX
            && _cameraY == mapData.CameraY
            && _currentZ == mapData.CurrentZ;
    }

    private void ApplyDelta(
        SimulationMapViewportData mapData,
        SimulationMapViewportDeltaData delta)
    {
        if (delta.ChangedRows.Count > 0)
        {
            foreach (var row in delta.ChangedRows.OrderBy(static row => row.ScreenY))
                ApplyRow(mapData, row);
            return;
        }

        if (delta.ChangedRegions.Count > 0)
        {
            foreach (var region in delta.ChangedRegions
                .OrderBy(static region => region.RegionY)
                .ThenBy(static region => region.RegionX))
            {
                ApplyRegion(mapData, region);
            }

            return;
        }

        ApplyCells(mapData, delta.ChangedCells);
    }

    private void Seed(
        SimulationMapViewportData mapData,
        string? payloadHash,
        IEnumerable<MapViewportCellView> cells)
    {
        _cellsByKey.Clear();
        _payloadHash = string.IsNullOrWhiteSpace(payloadHash) ? null : payloadHash;
        _hasWorld = mapData.HasWorld;
        _width = mapData.Width;
        _height = mapData.Height;
        _cameraX = mapData.CameraX;
        _cameraY = mapData.CameraY;
        _currentZ = mapData.CurrentZ;
        ApplyCells(mapData, cells);
    }

    private static IReadOnlyList<MapViewportCellView> SelectFullSnapshotCells(
        SimulationMapViewportData mapData,
        SimulationMapViewportDeltaData delta)
    {
        if (!delta.IsAvailable || delta.CanApplyToBase)
            return mapData.Cells;

        if (delta.ChangedRows.Count > 0)
        {
            return delta.ChangedRows
                .OrderBy(static row => row.ScreenY)
                .SelectMany(static row => row.Cells)
                .ToArray();
        }

        if (delta.ChangedRegions.Count > 0)
        {
            return delta.ChangedRegions
                .OrderBy(static region => region.RegionY)
                .ThenBy(static region => region.RegionX)
                .SelectMany(static region => region.Cells)
                .ToArray();
        }

        return delta.ChangedCells.Count > 0
            ? delta.ChangedCells
            : mapData.Cells;
    }

    private void ApplyRow(
        SimulationMapViewportData mapData,
        MapViewportRowDeltaView row)
    {
        if (row.ScreenY < 0 || row.ScreenY >= mapData.Height)
            return;

        for (var x = 0; x < mapData.Width; x++)
            _cellsByKey.Remove(CellKey(x, row.ScreenY, mapData.Width));

        ApplyCells(mapData, row.Cells);
    }

    private void ApplyRegion(
        SimulationMapViewportData mapData,
        MapViewportRegionDeltaView region)
    {
        int minX = Math.Max(0, region.ScreenX);
        int minY = Math.Max(0, region.ScreenY);
        int maxX = Math.Min(mapData.Width, region.ScreenX + Math.Max(0, region.Width));
        int maxY = Math.Min(mapData.Height, region.ScreenY + Math.Max(0, region.Height));
        for (var y = minY; y < maxY; y++)
        {
            for (var x = minX; x < maxX; x++)
                _cellsByKey.Remove(CellKey(x, y, mapData.Width));
        }

        ApplyCells(mapData, region.Cells);
    }

    private void ApplyCells(
        SimulationMapViewportData mapData,
        IEnumerable<MapViewportCellView> cells)
    {
        foreach (var cell in cells)
        {
            if (cell.ScreenX < 0
                || cell.ScreenY < 0
                || cell.ScreenX >= mapData.Width
                || cell.ScreenY >= mapData.Height)
            {
                continue;
            }

            _cellsByKey[CellKey(cell.ScreenX, cell.ScreenY, mapData.Width)] = cell;
        }
    }

    private MapViewportCellView[] OrderedCells()
    {
        return _cellsByKey.Values
            .OrderBy(static cell => cell.ScreenY)
            .ThenBy(static cell => cell.ScreenX)
            .ToArray();
    }

    private static int CellKey(int x, int y, int width)
    {
        return checked((y * width) + x);
    }
}
