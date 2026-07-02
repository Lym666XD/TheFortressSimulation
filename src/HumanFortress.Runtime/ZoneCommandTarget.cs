using HumanFortress.Simulation.Zones;
using SadRogue.Primitives;

namespace HumanFortress.Runtime;

internal sealed class ZoneCommandTarget : IZoneCommandTarget
{
    private const int CommandPriority = 50;
    private const string SystemId = "Runtime.ZoneCommand";

    private readonly ZoneDiffLog _zoneDiffLog;
    private readonly Action<string>? _log;

    internal ZoneCommandTarget(ZoneDiffLog zoneDiffLog, Action<string>? log = null)
    {
        _zoneDiffLog = zoneDiffLog ?? throw new ArgumentNullException(nameof(zoneDiffLog));
        _log = log;
    }

    void IZoneCommandTarget.CreateZone(string defId, string name, Rectangle worldRect, int z, ulong createdTick)
    {
        _zoneDiffLog.AddCreateZone(defId, name, worldRect, z, createdTick, CommandPriority, SystemId);
        _log?.Invoke($"[ZONE] Queued create def={defId} rect=({worldRect.X},{worldRect.Y},{worldRect.Width}x{worldRect.Height}) z={z}");
    }

    void IZoneCommandTarget.AddZoneCells(int zoneId, Rectangle worldRect, int z)
    {
        _zoneDiffLog.AddCells(zoneId, worldRect, z, CommandPriority, SystemId);
        _log?.Invoke($"[ZONE] Queued add-cells zone={zoneId} rect=({worldRect.X},{worldRect.Y},{worldRect.Width}x{worldRect.Height}) z={z}");
    }

    void IZoneCommandTarget.RemoveZoneCells(int zoneId, Rectangle worldRect, int z)
    {
        _zoneDiffLog.RemoveCells(zoneId, worldRect, z, CommandPriority, SystemId);
        _log?.Invoke($"[ZONE] Queued remove-cells zone={zoneId} rect=({worldRect.X},{worldRect.Y},{worldRect.Width}x{worldRect.Height}) z={z}");
    }

    void IZoneCommandTarget.DeleteZone(int zoneId)
    {
        _zoneDiffLog.AddDeleteZone(zoneId, CommandPriority, SystemId);
        _log?.Invoke($"[ZONE] Queued delete zone={zoneId}");
    }
}
