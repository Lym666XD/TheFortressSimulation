namespace HumanFortress.Simulation.Zones;

internal sealed partial class ZoneManager
{
    internal readonly record struct MutationMemento(
        IReadOnlyList<ZoneInstance> Zones,
        int NextZoneId);

    internal MutationMemento CaptureMutationMemento()
    {
        lock (_lock)
        {
            return new MutationMemento(
                _zones.Values
                    .OrderBy(static zone => zone.ZoneId)
                    .Select(static zone => zone.CloneForMutationMemento())
                    .ToArray(),
                _nextZoneId);
        }
    }

    internal void RestoreMutationMemento(MutationMemento memento)
    {
        lock (_lock)
        {
            _zones.Clear();
            foreach (var zone in memento.Zones
                .OrderBy(static zone => zone.ZoneId)
                .Select(static zone => zone.CloneForMutationMemento()))
            {
                _zones.Add(zone.ZoneId, zone);
            }

            _nextZoneId = memento.NextZoneId;
        }
    }
}
