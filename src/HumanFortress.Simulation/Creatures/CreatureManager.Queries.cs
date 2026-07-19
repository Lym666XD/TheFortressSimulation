using HumanFortress.Core.Simulation;

namespace HumanFortress.Simulation.Creatures;

internal sealed partial class CreatureManager
{
    /// <summary>
    /// Get instance by GUID
    /// </summary>
    internal CreatureInstance? GetInstance(Guid guid)
    {
        lock (_instanceLock)
        {
            return _instances.GetValueOrDefault(guid);
        }
    }

    /// <summary>
    /// Find a creature by the compact legacy entity id used in older DiffTarget operations.
    /// </summary>
    internal CreatureInstance? GetInstanceByEntityId(uint entityId)
    {
        lock (_instanceLock)
        {
            return _legacyEntityIdIndex.TryGetValue(entityId, out var ids)
                && ids.Count > 0
                && _instances.TryGetValue(ids[0], out var instance)
                ? instance
                : null;
        }
    }

    /// <summary>
    /// Find a creature by the wider stable entity key used by entity-scoped DiffTarget operations.
    /// </summary>
    internal CreatureInstance? GetInstanceByEntityKey(ulong entityKey)
    {
        lock (_instanceLock)
        {
            return _identityIndex.TryResolve(entityKey, out var guid)
                ? _instances.GetValueOrDefault(guid)
                : null;
        }
    }

    /// <summary>
    /// Get all instances (creates a snapshot for thread safety)
    /// </summary>
    internal IEnumerable<CreatureInstance> GetAllInstances()
    {
        lock (_instanceLock)
        {
            return _instances
                .OrderBy(static entry => entry.Key)
                .Select(static entry => entry.Value)
                .ToList();
        }
    }
}
