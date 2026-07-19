namespace HumanFortress.Simulation.Stockpile;

internal sealed partial class StockpileManager
{
    internal readonly record struct MutationMemento(
        IReadOnlyList<StockpileZone> Zones,
        int NextZoneId);

    internal MutationMemento CaptureMutationMemento()
    {
        lock (_lock)
        {
            return new MutationMemento(
                _zones.Values
                    .OrderBy(static zone => zone.ZoneId)
                    .Select(CloneZone)
                    .ToArray(),
                _nextZoneId);
        }
    }

    internal void RestoreMutationMemento(MutationMemento memento)
    {
        lock (_lock)
        {
            _zones.Clear();
            foreach (var zone in memento.Zones.OrderBy(static zone => zone.ZoneId).Select(CloneZone))
                _zones.Add(zone.ZoneId, zone);
            _nextZoneId = memento.NextZoneId;
        }
    }

    private static StockpileZone CloneZone(StockpileZone zone)
    {
        return new StockpileZone(
            zone.ZoneId,
            zone.Name,
            zone.HomeChunk,
            new StockpileFilter
            {
                Mode = zone.Filter.Mode,
                Tags = zone.Filter.Tags,
                ItemIds = zone.Filter.ItemIds,
                Materials = zone.Filter.Materials
            },
            zone.Priority,
            zone.TargetStacks,
            zone.HysteresisLow,
            zone.HysteresisHigh,
            zone.Generation,
            zone.CreatedTick,
            zone.GetMemberChunksSnapshot());
    }
}
