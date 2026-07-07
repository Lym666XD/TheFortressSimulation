namespace HumanFortress.Simulation.Creatures;

internal sealed partial class CreatureManager
{
    /// <summary>
    /// Get instance by GUID
    /// </summary>
    public CreatureInstance? GetInstance(Guid guid)
    {
        lock (_instanceLock)
        {
            return _instances.GetValueOrDefault(guid);
        }
    }

    /// <summary>
    /// Get all instances (creates a snapshot for thread safety)
    /// </summary>
    public IEnumerable<CreatureInstance> GetAllInstances()
    {
        lock (_instanceLock)
        {
            return _instances.Values.ToList();
        }
    }
}
